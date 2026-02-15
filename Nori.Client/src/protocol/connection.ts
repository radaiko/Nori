// protocol/connection.ts -- WebSocket connection manager with auto-reconnection.
// Connects to a Nori.Server instance, deserializes incoming MessagePack messages,
// and provides methods to send client messages back to the server.

import { encode } from '@msgpack/msgpack';
import {
  deserializeEnvelope,
  deserializeSceneInit,
  deserializeEntityAdd,
  deserializeEntityRemove,
  deserializeEntityUpdate,
  deserializePickResult,
} from './deserializer.js';
import {
  MsgType,
  ClientMsgType,
  type SceneInitMsg,
  type EntityAddMsg,
  type EntityRemoveMsg,
  type EntityUpdateMsg,
  type PickResultMsg,
  type ViewStateMsg,
  type PickRequestMsg,
  type InteractionRequestMsg,
} from './messages.js';

// ═══════════════════════════════════════════════════════════════════════════════
// Event callback interface
// ═══════════════════════════════════════════════════════════════════════════════

/** Callbacks for server messages and connection lifecycle events */
export interface ConnectionEvents {
  onSceneInit?: (msg: SceneInitMsg) => void;
  onEntityAdd?: (msg: EntityAddMsg) => void;
  onEntityRemove?: (msg: EntityRemoveMsg) => void;
  onEntityUpdate?: (msg: EntityUpdateMsg) => void;
  onPickResult?: (msg: PickResultMsg) => void;
  onConnected?: () => void;
  onDisconnected?: () => void;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Connection
// ═══════════════════════════════════════════════════════════════════════════════

/** Reconnect delay in milliseconds */
const RECONNECT_DELAY_MS = 3000;

/**
 * NoriConnection -- manages WebSocket connection to a Nori.Server instance.
 *
 * Handles:
 * - Binary WebSocket communication with MessagePack encoding/decoding
 * - Automatic reconnection on disconnect
 * - Dispatching deserialized messages to event callbacks
 * - Sending client messages (ViewState, Pick, Interaction) back to server
 */
export class NoriConnection {
  private ws: WebSocket | null = null;
  private url: string;
  private events: ConnectionEvents;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private disposed: boolean = false;

  constructor(url: string, events: ConnectionEvents) {
    this.url = url;
    this.events = events;
  }

  /** Whether the connection is currently open */
  get connected(): boolean {
    return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
  }

  /** Establish a WebSocket connection to the server */
  connect(): void {
    if (this.disposed) return;
    this.cancelReconnect();

    this.ws = new WebSocket(this.url);
    this.ws.binaryType = 'arraybuffer';

    this.ws.onopen = () => {
      this.events.onConnected?.();
    };

    this.ws.onclose = () => {
      this.events.onDisconnected?.();
      this.scheduleReconnect();
    };

    this.ws.onerror = () => {
      // The close event always follows an error, so reconnection is handled there.
    };

    this.ws.onmessage = (ev: MessageEvent) => {
      const data = new Uint8Array(ev.data as ArrayBuffer);
      this.handleMessage(data);
    };
  }

  /** Disconnect from the server and stop reconnection attempts */
  disconnect(): void {
    this.disposed = true;
    this.cancelReconnect();
    if (this.ws) {
      // Remove handlers to prevent onclose from triggering reconnect
      this.ws.onclose = null;
      this.ws.onerror = null;
      this.ws.onmessage = null;
      this.ws.close();
      this.ws = null;
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Client → Server message senders
  // ─────────────────────────────────────────────────────────────────────────

  /** Send the client's current viewport/camera state to the server */
  sendViewState(msg: ViewStateMsg): void {
    this.sendEnvelope(ClientMsgType.ViewState, [
      msg.viewportW,
      msg.viewportH,
      msg.zoom,
      msg.panX,
      msg.panY,
      msg.xRot,
      msg.zRot,
    ]);
  }

  /** Send a pick request (screen coordinates) to the server */
  sendPick(msg: PickRequestMsg): void {
    this.sendEnvelope(ClientMsgType.Pick, [
      msg.x,
      msg.y,
    ]);
  }

  /** Send an interaction event to the server */
  sendInteraction(msg: InteractionRequestMsg): void {
    this.sendEnvelope(ClientMsgType.Interaction, [
      msg.type,
      msg.x,
      msg.y,
      msg.z,
      msg.modifiers,
    ]);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Message handling
  // ─────────────────────────────────────────────────────────────────────────

  /** Deserialize and dispatch an incoming binary message */
  private handleMessage(data: Uint8Array): void {
    try {
      const env = deserializeEnvelope(data);
      switch (env.type) {
        case MsgType.SceneInit:
          this.events.onSceneInit?.(deserializeSceneInit(env.payload));
          break;
        case MsgType.EntityAdd:
          this.events.onEntityAdd?.(deserializeEntityAdd(env.payload));
          break;
        case MsgType.EntityRemove:
          this.events.onEntityRemove?.(deserializeEntityRemove(env.payload));
          break;
        case MsgType.EntityUpdate:
          this.events.onEntityUpdate?.(deserializeEntityUpdate(env.payload));
          break;
        case MsgType.PickResult:
          this.events.onPickResult?.(deserializePickResult(env.payload));
          break;
        default:
          console.warn(`[NoriConnection] Unknown message type: ${env.type}`);
          break;
      }
    } catch (err) {
      console.error('[NoriConnection] Failed to handle message:', err);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Wire encoding
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Encode and send a client message as a MessagePack envelope.
   * Format mirrors C# MessageEnvelope: [type(byte), payload(bytes)]
   * where payload is the inner message serialized as a MessagePack array.
   */
  private sendEnvelope(type: number, payloadArray: unknown[]): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return;
    const payloadBytes = encode(payloadArray);
    const envelope = encode([type, payloadBytes]);
    this.ws.send(envelope);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Reconnection
  // ─────────────────────────────────────────────────────────────────────────

  /** Schedule an automatic reconnection attempt */
  private scheduleReconnect(): void {
    if (this.disposed) return;
    this.cancelReconnect();
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.connect();
    }, RECONNECT_DELAY_MS);
  }

  /** Cancel any pending reconnection timer */
  private cancelReconnect(): void {
    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }
}
