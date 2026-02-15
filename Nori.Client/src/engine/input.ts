// input.ts -- Mouse, keyboard, and touch event handling for the NoriRenderer.
// Camera state is managed client-side (no round-trip for zoom/pan/orbit).
// Pick/selection clicks are sent to the server via the connection.

import type { ClientScene } from './scene-graph.js';
import type { Renderer } from './renderer.js';
import type { NoriConnection } from '../protocol/connection.js';

// ---------------------------------------------------------------------------
// Click detection threshold (pixels of total movement)
// ---------------------------------------------------------------------------
const CLICK_THRESHOLD = 3;

// ---------------------------------------------------------------------------
// InputHandler
// ---------------------------------------------------------------------------
export class InputHandler {
  private canvas: HTMLCanvasElement;
  private scene: ClientScene;
  private renderer: Renderer;
  private connection: NoriConnection | null = null;

  // Drag state
  private isDragging: boolean = false;
  private dragButton: number = -1;
  private lastX: number = 0;
  private lastY: number = 0;
  private startX: number = 0;
  private startY: number = 0;

  // Touch state for pinch-zoom
  private activeTouches: Map<number, { x: number; y: number }> = new Map();
  private lastPinchDist: number = 0;

  // Bound event handlers (for removeEventListener)
  private boundWheel: (e: WheelEvent) => void;
  private boundPointerDown: (e: PointerEvent) => void;
  private boundPointerMove: (e: PointerEvent) => void;
  private boundPointerUp: (e: PointerEvent) => void;
  private boundContextMenu: (e: Event) => void;
  private boundTouchStart: (e: TouchEvent) => void;
  private boundTouchMove: (e: TouchEvent) => void;
  private boundTouchEnd: (e: TouchEvent) => void;

  constructor(canvas: HTMLCanvasElement, scene: ClientScene, renderer: Renderer) {
    this.canvas = canvas;
    this.scene = scene;
    this.renderer = renderer;

    // Bind event handlers
    this.boundWheel = this.onWheel.bind(this);
    this.boundPointerDown = this.onPointerDown.bind(this);
    this.boundPointerMove = this.onPointerMove.bind(this);
    this.boundPointerUp = this.onPointerUp.bind(this);
    this.boundContextMenu = this.onContextMenu.bind(this);
    this.boundTouchStart = this.onTouchStart.bind(this);
    this.boundTouchMove = this.onTouchMove.bind(this);
    this.boundTouchEnd = this.onTouchEnd.bind(this);

    // Add event listeners
    canvas.addEventListener('wheel', this.boundWheel, { passive: false });
    canvas.addEventListener('pointerdown', this.boundPointerDown);
    canvas.addEventListener('pointermove', this.boundPointerMove);
    canvas.addEventListener('pointerup', this.boundPointerUp);
    canvas.addEventListener('pointercancel', this.boundPointerUp);
    canvas.addEventListener('contextmenu', this.boundContextMenu);
    canvas.addEventListener('touchstart', this.boundTouchStart, { passive: false });
    canvas.addEventListener('touchmove', this.boundTouchMove, { passive: false });
    canvas.addEventListener('touchend', this.boundTouchEnd);
    canvas.addEventListener('touchcancel', this.boundTouchEnd);

    // Ensure the canvas can receive pointer events
    canvas.style.touchAction = 'none';
  }

  /** Set the connection for sending pick/interaction requests */
  setConnection(conn: NoriConnection | null): void {
    this.connection = conn;
  }

  /** Remove all event listeners */
  dispose(): void {
    const c = this.canvas;
    c.removeEventListener('wheel', this.boundWheel);
    c.removeEventListener('pointerdown', this.boundPointerDown);
    c.removeEventListener('pointermove', this.boundPointerMove);
    c.removeEventListener('pointerup', this.boundPointerUp);
    c.removeEventListener('pointercancel', this.boundPointerUp);
    c.removeEventListener('contextmenu', this.boundContextMenu);
    c.removeEventListener('touchstart', this.boundTouchStart);
    c.removeEventListener('touchmove', this.boundTouchMove);
    c.removeEventListener('touchend', this.boundTouchEnd);
    c.removeEventListener('touchcancel', this.boundTouchEnd);
  }

  // -------------------------------------------------------------------------
  // Wheel → Zoom
  // -------------------------------------------------------------------------

  /**
   * Port of Scene.Zoom() from C#.
   *
   * Zooms around the cursor position so the point under the cursor stays stable.
   * The original C# code:
   *   1. Clamps the new zoom factor
   *   2. Computes the midpoint of the scene projected to screen space
   *   3. Scales the vector from midpoint to cursor by the zoom factor
   *   4. Adjusts the pan vector to compensate
   *
   * In our simplified 2D projection the midpoint projects to the center of the
   * viewport (offset by panX/panY). For 3D the same approach works because the
   * orthographic projection maps the midpoint linearly.
   */
  private onWheel(e: WheelEvent): void {
    e.preventDefault();
    const factor = e.deltaY > 0 ? 0.9 : 1.1; // Scroll down = zoom out, up = zoom in

    const rect = this.canvas.getBoundingClientRect();
    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;

    this.zoomAtScreenPoint(factor, mouseX, mouseY);
    this.renderer.invalidate();
  }

  /**
   * Zoom around a given screen-space point.
   *
   * Port of Scene.Zoom():
   *   oldZoom → newZoom (clamped)
   *   actualFactor = newZoom / oldZoom
   *   pmid = viewport center (where the scene midpoint projects)
   *   pmouse2 = pmid + (pt - pmid) * actualFactor
   *   vshift = pt - pmouse2
   *   pan += (2 * vshift.x / vp.w, -2 * vshift.y / vp.h)
   *
   * The scene midpoint in screen space is at (vp/2 + pan * vp/2), which
   * is equivalent to computing mid * (world * proj) then mapping to pixels.
   * Since our projection already incorporates pan, the midpoint in NDC is
   * (panX, panY) and in screen space is:
   *   pmid.x = (panX + 1) * vw / 2
   *   pmid.y = (1 - panY) * vh / 2
   */
  private zoomAtScreenPoint(factor: number, screenX: number, screenY: number): void {
    const oldZoom = this.scene.zoom;
    const newZoom = clamp(oldZoom * factor, 0.01, 100);
    const actualFactor = newZoom / oldZoom;

    const vw = this.canvas.width;
    const vh = this.canvas.height;

    // Screen-space position of the scene midpoint
    // In our projection, the midpoint maps to NDC (panX, panY).
    // NDC → screen: sx = (ndc_x + 1) * vw / 2, sy = (1 - ndc_y) * vh / 2
    const pmidX = (this.scene.panX + 1) * vw / 2;
    const pmidY = (1 - this.scene.panY) * vh / 2;

    // After zoom, the point that was at cursor moves to:
    const pmouse2X = pmidX + (screenX - pmidX) * actualFactor;
    const pmouse2Y = pmidY + (screenY - pmidY) * actualFactor;

    // Shift needed to keep cursor point stable
    const vshiftX = screenX - pmouse2X;
    const vshiftY = screenY - pmouse2Y;

    // Convert pixel shift back to NDC pan units
    this.scene.panX += (2 * vshiftX) / vw;
    this.scene.panY -= (2 * vshiftY) / vh;
    this.scene.zoom = newZoom;
  }

  // -------------------------------------------------------------------------
  // Pointer events → Drag (pan/orbit) and Click (pick)
  // -------------------------------------------------------------------------

  private onPointerDown(e: PointerEvent): void {
    // Ignore touch events here — handled via touch events for pinch support
    if (e.pointerType === 'touch') return;

    this.isDragging = true;
    this.dragButton = e.button;
    this.lastX = e.clientX;
    this.lastY = e.clientY;
    this.startX = e.clientX;
    this.startY = e.clientY;
    this.canvas.setPointerCapture(e.pointerId);
  }

  private onPointerMove(e: PointerEvent): void {
    if (!this.isDragging) return;
    if (e.pointerType === 'touch') return;

    const dx = e.clientX - this.lastX;
    const dy = e.clientY - this.lastY;
    this.lastX = e.clientX;
    this.lastY = e.clientY;

    if (this.dragButton === 0 && this.scene.sceneType === '3d') {
      // Left drag in 3D → orbit (turntable rotation, matches WPF SceneRotator)
      this.scene.zRot += dx * 0.5;
      this.scene.xRot += dy * 0.5;
    } else if (this.dragButton === 0 && this.scene.sceneType === '2d') {
      // Left drag in 2D → pan
      this.applyPanDelta(dx, dy);
    } else if (this.dragButton === 2) {
      // Right drag → pan (both 2D and 3D)
      this.applyPanDelta(dx, dy);
    }

    this.renderer.invalidate();
  }

  private onPointerUp(e: PointerEvent): void {
    if (e.pointerType === 'touch') return;

    if (this.isDragging) {
      // Detect click vs drag using total displacement from start
      const totalMoved =
        Math.abs(e.clientX - this.startX) + Math.abs(e.clientY - this.startY);

      if (totalMoved < CLICK_THRESHOLD && this.connection) {
        const rect = this.canvas.getBoundingClientRect();
        this.connection.sendPick({
          x: Math.round(e.clientX - rect.left),
          y: Math.round(e.clientY - rect.top),
        });
      }

      this.isDragging = false;
      this.dragButton = -1;
      this.canvas.releasePointerCapture(e.pointerId);
    }
  }

  // -------------------------------------------------------------------------
  // Touch events → Pinch zoom and single-finger drag
  // -------------------------------------------------------------------------

  private onTouchStart(e: TouchEvent): void {
    e.preventDefault();
    for (let i = 0; i < e.changedTouches.length; i++) {
      const t = e.changedTouches[i];
      this.activeTouches.set(t.identifier, { x: t.clientX, y: t.clientY });
    }

    if (this.activeTouches.size === 2) {
      this.lastPinchDist = this.pinchDistance();
    }
  }

  private onTouchMove(e: TouchEvent): void {
    e.preventDefault();

    if (this.activeTouches.size === 1 && e.touches.length === 1) {
      // Single finger drag → pan (2D) or orbit (3D)
      const t = e.touches[0];
      const prev = this.activeTouches.get(t.identifier);
      if (!prev) return;

      const dx = t.clientX - prev.x;
      const dy = t.clientY - prev.y;
      this.activeTouches.set(t.identifier, { x: t.clientX, y: t.clientY });

      if (this.scene.sceneType === '3d') {
        this.scene.zRot += dx * 0.5;
        this.scene.xRot += dy * 0.5;
      } else {
        this.applyPanDelta(dx, dy);
      }

      this.renderer.invalidate();
    } else if (this.activeTouches.size >= 2 && e.touches.length >= 2) {
      // Update positions
      for (let i = 0; i < e.changedTouches.length; i++) {
        const t = e.changedTouches[i];
        this.activeTouches.set(t.identifier, { x: t.clientX, y: t.clientY });
      }

      // Pinch zoom
      const dist = this.pinchDistance();
      if (this.lastPinchDist > 0) {
        const factor = dist / this.lastPinchDist;
        // Zoom around pinch midpoint
        const mid = this.pinchMidpoint();
        const rect = this.canvas.getBoundingClientRect();
        this.zoomAtScreenPoint(factor, mid.x - rect.left, mid.y - rect.top);
        this.renderer.invalidate();
      }
      this.lastPinchDist = dist;
    }
  }

  private onTouchEnd(e: TouchEvent): void {
    for (let i = 0; i < e.changedTouches.length; i++) {
      this.activeTouches.delete(e.changedTouches[i].identifier);
    }

    if (this.activeTouches.size < 2) {
      this.lastPinchDist = 0;
    }
  }

  /** Compute distance between the first two active touches */
  private pinchDistance(): number {
    const pts = [...this.activeTouches.values()];
    if (pts.length < 2) return 0;
    const dx = pts[1].x - pts[0].x;
    const dy = pts[1].y - pts[0].y;
    return Math.sqrt(dx * dx + dy * dy);
  }

  /** Compute midpoint between the first two active touches */
  private pinchMidpoint(): { x: number; y: number } {
    const pts = [...this.activeTouches.values()];
    if (pts.length < 2) return { x: 0, y: 0 };
    return {
      x: (pts[0].x + pts[1].x) / 2,
      y: (pts[0].y + pts[1].y) / 2,
    };
  }

  // -------------------------------------------------------------------------
  // Context menu suppression
  // -------------------------------------------------------------------------

  private onContextMenu(e: Event): void {
    e.preventDefault();
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  /** Apply a pixel-space drag delta as a pan adjustment in NDC */
  private applyPanDelta(dxPx: number, dyPx: number): void {
    const vw = this.canvas.width;
    const vh = this.canvas.height;
    this.scene.panX += (2 * dxPx) / vw;
    this.scene.panY -= (2 * dyPx) / vh;
  }
}

// ---------------------------------------------------------------------------
// Utility
// ---------------------------------------------------------------------------

function clamp(v: number, min: number, max: number): number {
  return v < min ? min : v > max ? max : v;
}
