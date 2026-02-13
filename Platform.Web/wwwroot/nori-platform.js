// ────── ╔╗                                                                            PLATFORM.WEB
// ╔═╦╦═╦╦╬╣ nori-platform.js
// ║║║║╬║╔╣║ Browser platform module — canvas queries, input events, animation frame loop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────

// Tracks the most recent pointer event ID for setPointerCapture/releasePointerCapture
let lastPointerId = 0;

// Reference to the .NET WebInput and WebPlatform exported methods.
// These are resolved lazily on first use via getAssemblyExports.
let dotnetExports = null;

async function ensureExports () {
   if (dotnetExports) return;
   // The WASM runtime exposes getDotnetRuntime().getAssemblyExports() for JSExport access
   const runtime = globalThis.getDotnetRuntime ("0");
   dotnetExports = await runtime.getAssemblyExports ("Nori.Platform.Web");
}

export const noriPlatform = {
   // --- Canvas queries -------------------------------------------------------

   getCanvasWidth (canvasId) {
      const canvas = document.getElementById (canvasId);
      return canvas ? canvas.width : 0;
   },

   getCanvasHeight (canvasId) {
      const canvas = document.getElementById (canvasId);
      return canvas ? canvas.height : 0;
   },

   getDevicePixelRatio () {
      return window.devicePixelRatio || 1.0;
   },

   // --- Cursor and pointer ---------------------------------------------------

   setCursorVisible (canvasId, visible) {
      const canvas = document.getElementById (canvasId);
      if (canvas) canvas.style.cursor = visible ? "default" : "none";
   },

   setPointerCapture (canvasId, capture) {
      const canvas = document.getElementById (canvasId);
      if (!canvas) return false;
      try {
         if (capture) {
            canvas.setPointerCapture (lastPointerId);
         } else {
            canvas.releasePointerCapture (lastPointerId);
         }
         return true;
      } catch (e) {
         return false;
      }
   },

   // --- Input handlers -------------------------------------------------------

   async setupInputHandlers (canvasId) {
      await ensureExports ();
      const canvas = document.getElementById (canvasId);
      if (!canvas) return;

      const webInput = dotnetExports.Nori.WebInput;
      const webPlatform = dotnetExports.Nori.WebPlatform;

      // Make canvas focusable for keyboard events
      canvas.tabIndex = 0;
      canvas.style.outline = "none";
      canvas.focus ();

      // Keyboard events
      canvas.addEventListener ("keydown", (e) => {
         e.preventDefault ();
         webInput.OnKeyDown (e.keyCode, e.shiftKey, e.ctrlKey, e.altKey);
      });

      canvas.addEventListener ("keyup", (e) => {
         e.preventDefault ();
         webInput.OnKeyUp (e.keyCode, e.shiftKey, e.ctrlKey, e.altKey);
      });

      // Mouse events — use pointer events for capture support
      canvas.addEventListener ("pointerdown", (e) => {
         e.preventDefault ();
         lastPointerId = e.pointerId;
         canvas.focus ();
         webInput.OnMouseDown (e.button, e.offsetX | 0, e.offsetY | 0, e.shiftKey, e.ctrlKey, e.altKey);
      });

      canvas.addEventListener ("pointerup", (e) => {
         e.preventDefault ();
         lastPointerId = e.pointerId;
         webInput.OnMouseUp (e.button, e.offsetX | 0, e.offsetY | 0, e.shiftKey, e.ctrlKey, e.altKey);
      });

      canvas.addEventListener ("pointermove", (e) => {
         lastPointerId = e.pointerId;
         webInput.OnMouseMove (e.offsetX | 0, e.offsetY | 0);
      });

      canvas.addEventListener ("wheel", (e) => {
         e.preventDefault ();
         // Normalize delta: browsers report differently; deltaY in pixels or lines.
         // Windows scroll delta is typically 120 per notch; we approximate that.
         const delta = -Math.sign (e.deltaY) * 120;
         webInput.OnMouseWheel (delta, e.offsetX | 0, e.offsetY | 0);
      }, { passive: false });

      canvas.addEventListener ("pointerleave", (e) => {
         webInput.OnMouseLeave ();
      });

      canvas.addEventListener ("lostpointercapture", (e) => {
         webInput.OnPointerCaptureLost ();
      });

      // Context menu suppression — right-click should not show browser menu
      canvas.addEventListener ("contextmenu", (e) => {
         e.preventDefault ();
      });

      // Resize observer — detect when the canvas element size changes
      const resizeObserver = new ResizeObserver ((entries) => {
         for (const entry of entries) {
            const dpr = window.devicePixelRatio || 1;
            const width = (entry.contentRect.width * dpr) | 0;
            const height = (entry.contentRect.height * dpr) | 0;
            if (width > 0 && height > 0) {
               canvas.width = width;
               canvas.height = height;
               webPlatform.OnCanvasResized (width, height);
            }
         }
      });
      resizeObserver.observe (canvas);
   },

   // --- Animation and window -------------------------------------------------

   requestAnimationFrame () {
      window.requestAnimationFrame ((timestamp) => {
         if (dotnetExports) {
            dotnetExports.Nori.WebPlatform.OnAnimationFrame (timestamp);
         }
      });
   },

   setDocumentTitle (title) {
      document.title = title;
   }
};
