/**
 * @nori/renderer — Framework-agnostic WebGPU rendering engine
 *
 * Connects to a Nori.Server instance via WebSocket and renders
 * 2D/3D scenes using WebGPU. Integrable into any frontend framework.
 */

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
 * NoriRenderer — the main entry point for @nori/renderer
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

  constructor (config: NoriRendererConfig) {
    this.config = config;
  }

  /** Connect to the server and start rendering */
  async connect (): Promise<void> {
    this.state = 'connecting';
    // TODO: Initialize WebGPU, connect WebSocket, start render loop
    this.state = 'connected';
  }

  /** Disconnect from the server and stop rendering */
  disconnect (): void {
    this.state = 'disconnected';
    // TODO: Close WebSocket, stop render loop
  }

  /** Current connection state */
  get connectionState (): ConnectionState { return this.state; }

  /** Whether the renderer is connected to a server */
  get connected (): boolean { return this.state === 'connected'; }

  // Camera controls -----------------------------------------------------------

  /** Zoom in/out by the given factor, optionally around a screen-space center point */
  zoom (factor: number, center?: { x: number; y: number }): void {
    // TODO: Adjust zoom, recompute projection, send ViewState to server
  }

  /** Pan the view by the given amount in clip-space coordinates */
  pan (dx: number, dy: number): void {
    // TODO: Adjust pan vector, recompute projection
  }

  /** Reset view to show the full scene extents */
  resetView (): void {
    // TODO: Reset zoom=1, pan=(0,0), recompute projection
  }

  /** For 3D scenes: set the orbit camera angles (in degrees) */
  orbit (xRot: number, zRot: number): void {
    // TODO: Update viewpoint, recompute projection
  }

  // Events --------------------------------------------------------------------

  set onEntityPicked (cb: NoriRendererEvents['onEntityPicked']) { this.events.onEntityPicked = cb; }
  set onConnected (cb: NoriRendererEvents['onConnected']) { this.events.onConnected = cb; }
  set onDisconnected (cb: NoriRendererEvents['onDisconnected']) { this.events.onDisconnected = cb; }
  set onSceneLoaded (cb: NoriRendererEvents['onSceneLoaded']) { this.events.onSceneLoaded = cb; }
  set onFps (cb: NoriRendererEvents['onFps']) { this.events.onFps = cb; }

  // Lifecycle -----------------------------------------------------------------

  /** Release all GPU resources, close connections, and clean up */
  dispose (): void {
    this.disconnect ();
    // TODO: Destroy GPU device, release buffers/pipelines
  }
}
