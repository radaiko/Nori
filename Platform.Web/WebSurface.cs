// ────── ╔╗                                                                            PLATFORM.WEB
// ╔═╦╦═╦╦╬╣ WebSurface.cs
// ║║║║╬║╔╣║ ISurface implementation wrapping an HTML canvas element via JSImport interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class WebSurface ---------------------------------------------------------------------------
/// <summary>ISurface implementation backed by an HTML canvas element identified by string ID</summary>
class WebSurface : ISurface {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a WebSurface wrapping the canvas element with the given ID</summary>
   public WebSurface (string canvasId) {
      mCanvasId = canvasId;
      mReady.OnNext (0);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Surface size in pixels</summary>
   public Vec2S Size
      => new (NoriWebPlatform.GetCanvasWidth (mCanvasId), NoriWebPlatform.GetCanvasHeight (mCanvasId));

   /// <summary>DPI scale factor (1.0 for standard, 2.0 for retina/HiDPI)</summary>
   public double DPIScale => NoriWebPlatform.GetDevicePixelRatio ();

   /// <summary>Native surface handle — not applicable for web (GPU.Web uses canvas ID directly)</summary>
   public IntPtr NativeHandle => IntPtr.Zero;

   /// <summary>Show or hide the cursor over the surface</summary>
   public bool CursorVisible {
      set => NoriWebPlatform.SetCursorVisible (mCanvasId, value);
   }

   /// <summary>Fired when the surface is resized</summary>
   public IObservable<Vec2S> Resized => mResized;

   /// <summary>Fired when the surface is ready for rendering</summary>
   public IObservable<int> Ready => mReady;

   // Methods ------------------------------------------------------------------
   /// <summary>Request a redraw of the surface</summary>
   public void Invalidate () => mDirty = true;

   /// <summary>Check and clear the dirty flag</summary>
   internal bool ConsumeInvalidate () {
      if (!mDirty) return false;
      mDirty = false;
      return true;
   }

   /// <summary>Push a resize notification from the JS side</summary>
   internal void NotifyResized (int width, int height)
      => mResized.OnNext (new Vec2S (width, height));

   /// <summary>The canvas element ID this surface wraps</summary>
   internal string CanvasId => mCanvasId;

   // Private data -------------------------------------------------------------
   string mCanvasId;
   bool mDirty;
   Subject<Vec2S> mResized = new ();
   Subject<int> mReady = new ();
}
#endregion
