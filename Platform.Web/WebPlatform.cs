// ────── ╔╗                                                                            PLATFORM.WEB
// ╔═╦╦═╦╦╬╣ WebPlatform.cs
// ║║║║╬║╔╣║ IPlatform implementation for Blazor WebAssembly using requestAnimationFrame
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class WebPlatform --------------------------------------------------------------------------
/// <summary>Web platform facade providing ISurface, IInput, and IFontLoader for browser-based rendering</summary>
public partial class WebPlatform : IPlatform {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a WebPlatform targeting the canvas element with the given ID</summary>
   public WebPlatform (string canvasId) {
      mCanvasId = canvasId;
      mInput.SetCanvasId (canvasId);
      mInput.Register ();
      RegisterInstance ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Hardware input abstraction</summary>
   public IInput Input => mInput;

   /// <summary>Font loading abstraction</summary>
   public IFontLoader FontLoader => mFontLoader;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a renderable surface wrapping the canvas element</summary>
   public ISurface CreateSurface (string title, int width, int height) {
      NoriWebPlatform.SetDocumentTitle (title);
      mSurface = new WebSurface (mCanvasId);
      NoriWebPlatform.SetupInputHandlers (mCanvasId);
      return mSurface;
   }

   /// <summary>Run the platform event loop using requestAnimationFrame</summary>
   /// In WASM we cannot block the thread, so this sets up the animation frame
   /// callback and returns. The JS side calls OnAnimationFrame via JSExport
   /// on each frame, which invokes the stored onFrame delegate.
   public void Run (Action<double> onFrame) {
      sOnFrame = onFrame;
      sLastTime = -1;
      NoriWebPlatform.RequestAnimationFrame ();
   }

   // JSExport callbacks -------------------------------------------------------
   /// <summary>Called from JavaScript on each requestAnimationFrame tick</summary>
   [JSExport]
   public static void OnAnimationFrame (double timestamp) {
      if (sOnFrame == null) return;
      // timestamp is in milliseconds from performance.now(); convert to seconds
      double now = timestamp / 1000.0;
      double dt = sLastTime < 0 ? 0 : now - sLastTime;
      sLastTime = now;
      sOnFrame (dt);
      // Request the next frame
      NoriWebPlatform.RequestAnimationFrame ();
   }

   /// <summary>Called from JavaScript when the canvas is resized</summary>
   [JSExport]
   public static void OnCanvasResized (int width, int height) {
      sInstance?.mSurface?.NotifyResized (width, height);
   }

   // Implementation -----------------------------------------------------------
   // Register this instance as the singleton for resize callbacks
   void RegisterInstance () => sInstance = this;

   // Private data -------------------------------------------------------------
   string mCanvasId;
   WebSurface? mSurface;
   WebInput mInput = new ();
   WebFontLoader mFontLoader = new ();
   static Action<double>? sOnFrame;
   static double sLastTime;
   static WebPlatform? sInstance;
}
#endregion
