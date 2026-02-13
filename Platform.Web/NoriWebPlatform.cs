// ────── ╔╗                                                                            PLATFORM.WEB
// ╔═╦╦═╦╦╬╣ NoriWebPlatform.cs
// ║║║║╬║╔╣║ JSImport declarations for browser platform interop (canvas, input, animation)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class NoriWebPlatform ----------------------------------------------------------------------
/// <summary>JS interop bindings to the nori-platform.js browser platform module</summary>
/// Each method maps to a function in the "nori-platform" JS module, providing
/// platform services like canvas queries, input handler setup, cursor control,
/// pointer capture, animation frame scheduling, and document title management.
internal static partial class NoriWebPlatform {
   // Canvas queries -----------------------------------------------------------
   /// <summary>Get the width of a canvas element in pixels</summary>
   [JSImport ("noriPlatform.getCanvasWidth", "nori-platform")]
   internal static partial int GetCanvasWidth (string canvasId);

   /// <summary>Get the height of a canvas element in pixels</summary>
   [JSImport ("noriPlatform.getCanvasHeight", "nori-platform")]
   internal static partial int GetCanvasHeight (string canvasId);

   /// <summary>Get the device pixel ratio (window.devicePixelRatio)</summary>
   [JSImport ("noriPlatform.getDevicePixelRatio", "nori-platform")]
   internal static partial double GetDevicePixelRatio ();

   // Cursor and pointer -------------------------------------------------------
   /// <summary>Show or hide the cursor over a canvas element</summary>
   [JSImport ("noriPlatform.setCursorVisible", "nori-platform")]
   internal static partial void SetCursorVisible (string canvasId, bool visible);

   /// <summary>Set up DOM event listeners (keyboard, mouse) on the canvas</summary>
   [JSImport ("noriPlatform.setupInputHandlers", "nori-platform")]
   internal static partial void SetupInputHandlers (string canvasId);

   /// <summary>Capture or release the pointer on the canvas element</summary>
   [JSImport ("noriPlatform.setPointerCapture", "nori-platform")]
   internal static partial bool SetPointerCapture (string canvasId, bool capture);

   // Animation and window -----------------------------------------------------
   /// <summary>Schedule the next animation frame callback</summary>
   [JSImport ("noriPlatform.requestAnimationFrame", "nori-platform")]
   internal static partial void RequestAnimationFrame ();

   /// <summary>Set the browser document title</summary>
   [JSImport ("noriPlatform.setDocumentTitle", "nori-platform")]
   internal static partial void SetDocumentTitle (string title);
}
#endregion
