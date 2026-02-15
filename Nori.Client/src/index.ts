/**
 * @nori/renderer -- Framework-agnostic WebGPU rendering engine
 *
 * Connects to a Nori.Server instance via WebSocket and renders
 * 2D/3D scenes using WebGPU. Integrable into any frontend framework.
 */

import { GPUDeviceManager } from './engine/gpu-device.js';
import { PipelineFactory, Pipeline } from './engine/pipeline-factory.js';
import { Renderer } from './engine/renderer.js';
import { ClientScene, RenderEntity, RenderPrimitive, PrimType, SceneType } from './engine/scene-graph.js';
import { BufferManager } from './engine/buffers.js';
import { InputHandler } from './engine/input.js';
import { NoriConnection, type ConnectionEvents } from './protocol/connection.js';
import {
  SceneType as ProtoSceneType,
  type SceneInitMsg,
  type EntityAddMsg,
  type EntityRemoveMsg,
  type EntityUpdateMsg,
  type PickResultMsg,
  type EntityDataMsg,
  type RenderPrimitiveMsg,
  type ViewStateMsg,
  type PickRequestMsg,
  type InteractionRequestMsg,
} from './protocol/messages.js';

// Re-export engine types for consumers
export { GPUDeviceManager } from './engine/gpu-device.js';
export { PipelineFactory, Pipeline } from './engine/pipeline-factory.js';
export { Renderer } from './engine/renderer.js';
export { ClientScene, RenderEntity, RenderPrimitive, PrimType, SceneType } from './engine/scene-graph.js';
export { BufferManager } from './engine/buffers.js';
export { InputHandler } from './engine/input.js';

// Re-export protocol types for consumers
export { NoriConnection, type ConnectionEvents } from './protocol/connection.js';
export {
  MsgType, ClientMsgType, InteractionType,
  SceneType as ProtoSceneType,
  type MessageEnvelope, type SceneInitMsg,
  type EntityDataMsg, type RenderPrimitiveMsg,
  type EntityAddMsg, type EntityRemoveMsg, type EntityUpdateMsg,
  type PickResultMsg,
  type ViewStateMsg, type PickRequestMsg, type InteractionRequestMsg,
} from './protocol/messages.js';

/** Configuration for creating a NoriRenderer instance */
export interface NoriRendererConfig {
  /** Canvas element to render into */
  canvas: HTMLCanvasElement;
  /** WebSocket URL of the Nori server (e.g., "ws://localhost:5100/nori") */
  serverUrl: string;
  /** Background color override [r, g, b, a] each 0-255. If not set, uses server-provided color */
  backgroundColor?: [number, number, number, number];
}

/** Callback types for renderer events */
export interface NoriRendererEvents {
  /** Called when an entity is picked (clicked). entityId is -1 if nothing was hit */
  onEntityPicked?: (entityId: number, position: { x: number; y: number; z: number }) => void;
  /** Called when WebSocket connection is established */
  onConnected?: () => void;
  /** Called when WebSocket connection is lost */
  onDisconnected?: () => void;
  /** Called when the initial scene snapshot is loaded and first frame rendered */
  onSceneLoaded?: () => void;
  /** Called each frame with FPS count */
  onFps?: (fps: number) => void;
}

/** Connection state of the renderer */
export type ConnectionState = 'disconnected' | 'connecting' | 'connected';

/**
 * NoriRenderer -- the main entry point for @nori/renderer
 *
 * Usage:
 * ```ts
 * const renderer = new NoriRenderer({
 *   canvas: document.getElementById('canvas') as HTMLCanvasElement,
 *   serverUrl: 'ws://localhost:5100/nori',
 * });
 * renderer.onConnected = () => console.log('Connected!');
 * await renderer.connect();
 * // ... later
 * renderer.dispose();
 * ```
 */
export class NoriRenderer {
  private config: NoriRendererConfig;
  private state: ConnectionState = 'disconnected';
  private events: NoriRendererEvents = {};

  // Engine components (initialized on connect)
  private gpuDevice: GPUDeviceManager | null = null;
  private pipelineFactory: PipelineFactory | null = null;
  private renderer: Renderer | null = null;
  private inputHandler: InputHandler | null = null;
  private _scene: ClientScene;
  private connection: NoriConnection | null = null;

  constructor(config: NoriRendererConfig) {
    this.config = config;
    this._scene = new ClientScene();
    if (config.backgroundColor) {
      this._scene.bgColor = config.backgroundColor;
    }
  }

  /** Access the scene graph for direct manipulation */
  get scene(): ClientScene { return this._scene; }

  /** Connect to the server and start rendering */
  async connect(): Promise<void> {
    this.state = 'connecting';

    // Initialize WebGPU
    this.gpuDevice = new GPUDeviceManager();
    await this.gpuDevice.init(this.config.canvas);

    // Compile all pipelines
    this.pipelineFactory = new PipelineFactory();
    await this.pipelineFactory.init(this.gpuDevice.device, this.gpuDevice.format);

    // Create renderer and start the frame loop
    this.renderer = new Renderer(this.gpuDevice, this.pipelineFactory, this._scene);
    this.renderer.onFps = (fps) => this.events.onFps?.(fps);
    this.renderer.start();

    // Create input handler for mouse/touch/keyboard events
    this.inputHandler = new InputHandler(this.config.canvas, this._scene, this.renderer);

    // Connect WebSocket to server
    const connEvents: ConnectionEvents = {
      onSceneInit: (msg: SceneInitMsg) => this.handleSceneInit(msg),
      onEntityAdd: (msg: EntityAddMsg) => this.handleEntityAdd(msg),
      onEntityRemove: (msg: EntityRemoveMsg) => this.handleEntityRemove(msg),
      onEntityUpdate: (msg: EntityUpdateMsg) => this.handleEntityUpdate(msg),
      onPickResult: (msg: PickResultMsg) => this.handlePickResult(msg),
      onConnected: () => {
        this.state = 'connected';
        this.events.onConnected?.();
      },
      onDisconnected: () => {
        if (this.state !== 'disconnected') {
          this.state = 'connecting'; // Auto-reconnecting
          this.events.onDisconnected?.();
        }
      },
    };
    this.connection = new NoriConnection(this.config.serverUrl, connEvents);
    this.connection.connect();
    this.inputHandler.setConnection(this.connection);
  }

  /** Disconnect from the server and stop rendering */
  disconnect(): void {
    this.inputHandler?.setConnection(null);
    this.connection?.disconnect();
    this.connection = null;
    this.renderer?.stop();
    this.state = 'disconnected';
    this.events.onDisconnected?.();
  }

  /** Current connection state */
  get connectionState(): ConnectionState { return this.state; }

  /** Whether the renderer is connected to a server */
  get connected(): boolean { return this.state === 'connected'; }

  /** Request a re-render (call after modifying scene data) */
  invalidate(): void {
    this.renderer?.invalidate();
  }

  // Protocol message senders ------------------------------------------------

  /** Send the client's current view state to the server */
  sendViewState(msg: ViewStateMsg): void {
    this.connection?.sendViewState(msg);
  }

  /** Send a pick request to the server */
  sendPick(msg: PickRequestMsg): void {
    this.connection?.sendPick(msg);
  }

  /** Send an interaction event to the server */
  sendInteraction(msg: InteractionRequestMsg): void {
    this.connection?.sendInteraction(msg);
  }

  /** Send a command to the server */
  sendCommand(name: string, arg: string = ''): void {
    this.connection?.sendCommand(name, arg);
  }

  // Camera controls -----------------------------------------------------------

  /** Zoom in/out by the given factor, optionally around a screen-space center point */
  zoom(factor: number, _center?: { x: number; y: number }): void {
    const oldZoom = this._scene.zoom;
    this._scene.zoom = Math.max(0.01, Math.min(100, oldZoom * factor));
    this.renderer?.invalidate();
  }

  /** Pan the view by the given amount in clip-space coordinates */
  pan(dx: number, dy: number): void {
    this._scene.panX += dx;
    this._scene.panY += dy;
    this.renderer?.invalidate();
  }

  /** Reset view to show the full scene extents */
  resetView(): void {
    this._scene.zoom = 1;
    this._scene.panX = 0;
    this._scene.panY = 0;
    this.renderer?.invalidate();
  }

  /** For 3D scenes: set the orbit camera angles (in degrees) */
  orbit(xRot: number, zRot: number): void {
    this._scene.xRot = xRot;
    this._scene.zRot = zRot;
    this.renderer?.invalidate();
  }

  // Events --------------------------------------------------------------------

  set onEntityPicked(cb: NoriRendererEvents['onEntityPicked']) { this.events.onEntityPicked = cb; }
  set onConnected(cb: NoriRendererEvents['onConnected']) { this.events.onConnected = cb; }
  set onDisconnected(cb: NoriRendererEvents['onDisconnected']) { this.events.onDisconnected = cb; }
  set onSceneLoaded(cb: NoriRendererEvents['onSceneLoaded']) { this.events.onSceneLoaded = cb; }
  set onFps(cb: NoriRendererEvents['onFps']) { this.events.onFps = cb; }

  // Lifecycle -----------------------------------------------------------------

  /** Release all GPU resources, close connections, and clean up */
  dispose(): void {
    this.disconnect();
    this.inputHandler?.dispose();
    this.inputHandler = null;
    this.renderer?.dispose();
    this.renderer = null;
    this.gpuDevice?.dispose();
    this.gpuDevice = null;
    this.pipelineFactory = null;
  }

  // ═══════════════════════════════════════════════════════════════════════════
  // Protocol message handlers
  // ═══════════════════════════════════════════════════════════════════════════

  /** Handle full scene snapshot from server */
  private handleSceneInit(msg: SceneInitMsg): void {
    // Set scene type
    this._scene.sceneType = msg.sceneType === ProtoSceneType.Scene3D ? '3d' : '2d';

    // Set background color (use server-provided unless config overrides)
    if (!this.config.backgroundColor && msg.bgColor.length >= 4) {
      this._scene.bgColor = [msg.bgColor[0], msg.bgColor[1], msg.bgColor[2], msg.bgColor[3]];
    }

    // Set bounds
    this._scene.bounds = msg.bounds;

    // Clear existing entities and populate with server data
    this._scene.clear();
    for (const entityMsg of msg.entities) {
      const entity = convertEntityData(entityMsg);
      this._scene.addEntity(entity);
    }

    // Invalidate to trigger re-render
    this.renderer?.invalidate();
    this.events.onSceneLoaded?.();
  }

  /** Handle entity added to scene */
  private handleEntityAdd(msg: EntityAddMsg): void {
    const entity = convertEntityData(msg.entity);
    this._scene.addEntity(entity);
    this.renderer?.invalidate();
  }

  /** Handle entity removed from scene */
  private handleEntityRemove(msg: EntityRemoveMsg): void {
    this._scene.removeEntity(msg.entityId);
    this.renderer?.invalidate();
  }

  /** Handle entity updated (full replacement of render data) */
  private handleEntityUpdate(msg: EntityUpdateMsg): void {
    const entity = convertEntityData(msg.entity);
    this._scene.updateEntity(entity);
    this.renderer?.invalidate();
  }

  /** Handle pick result from server */
  private handlePickResult(msg: PickResultMsg): void {
    const pos = msg.position;
    this.events.onEntityPicked?.(msg.entityId, {
      x: pos[0] ?? 0,
      y: pos[1] ?? 0,
      z: pos[2] ?? 0,
    });
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Protocol → Scene graph conversion helpers
// ═══════════════════════════════════════════════════════════════════════════════

/**
 * Convert a protocol EntityDataMsg into a scene-graph RenderEntity.
 * Handles the EPrimType offset (server starts at 1, client enum starts at 0).
 */
function convertEntityData(msg: EntityDataMsg): RenderEntity {
  return {
    id: msg.id,
    primitives: msg.primitives.map(convertPrimitive),
  };
}

/**
 * Convert a protocol RenderPrimitiveMsg to a scene-graph RenderPrimitive.
 *
 * Key mapping: C# EPrimType values start at 1 (Lines2D=1), while the client
 * PrimType enum starts at 0 (Lines2D=0). We subtract 1 to align them.
 *
 * Data arrays are converted to typed arrays (Float32Array, Uint32Array) as
 * expected by the renderer.
 */
function convertPrimitive(msg: RenderPrimitiveMsg): RenderPrimitive {
  const prim: RenderPrimitive = {
    type: (msg.type - 1) as PrimType, // Server EPrimType starts at 1, client PrimType starts at 0
    data: new Float32Array(msg.data),
    color: [
      msg.color[0] ?? 255,
      msg.color[1] ?? 255,
      msg.color[2] ?? 255,
      msg.color[3] ?? 255,
    ],
    lineWidth: msg.lineWidth,
    lineType: msg.lineType,
    ltScale: msg.ltScale,
    pointSize: msg.pointSize,
    transformIndex: msg.transformIndex,
    zLevel: msg.zLevel,
    shadeMode: msg.shadeMode,
  };

  if (msg.indices != null) {
    prim.indices = new Uint32Array(msg.indices);
  }
  if (msg.text != null) {
    prim.text = msg.text;
  }
  if (msg.textAlign != null) {
    prim.textAlign = msg.textAlign;
  }
  if (msg.wireIndices != null) {
    prim.wireIndices = new Uint32Array(msg.wireIndices);
  }
  if (msg.boundData != null) {
    prim.boundData = new Float32Array(msg.boundData);
  }

  return prim;
}
