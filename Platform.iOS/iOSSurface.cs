// ────── ╔╗                                                                            PLATFORM.IOS
// ╔═╦╦═╦╦╬╣ iOSSurface.cs
// ║║║║╬║╔╣║ ISurface implementation using a UIView with CAMetalLayer for iOS WebGPU rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class MetalView -------------------------------------------------------------------------------
/// <summary>UIView subclass backed by a CAMetalLayer for GPU rendering</summary>
class MetalView : UIView {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a MetalView with the given frame</summary>
   public MetalView (CGRect frame) : base (frame) {
      ContentScaleFactor = UIScreen.MainScreen.Scale;
      mMetalLayer = new CAMetalLayer ();
      mMetalLayer.Frame = Bounds;
      mMetalLayer.ContentsScale = ContentScaleFactor;
      Layer.AddSublayer (mMetalLayer);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The underlying CAMetalLayer for GPU rendering</summary>
   public CAMetalLayer MetalLayer => mMetalLayer;

   // Overrides ----------------------------------------------------------------
   public override void LayoutSubviews () {
      base.LayoutSubviews ();
      mMetalLayer.Frame = Bounds;
      mMetalLayer.ContentsScale = ContentScaleFactor;
      LayoutChanged?.Invoke ();
   }

   // Events -------------------------------------------------------------------
   /// <summary>Fired when layout changes (used by iOSSurface to emit Resized)</summary>
   internal Action? LayoutChanged;

   // Private data -------------------------------------------------------------
   CAMetalLayer mMetalLayer;
}
#endregion

#region class iOSSurface ------------------------------------------------------------------------------
/// <summary>ISurface implementation backed by a UIView with CAMetalLayer for iOS WebGPU rendering</summary>
class iOSSurface : ISurface {
   // Constructors -------------------------------------------------------------
   /// <summary>Create an iOSSurface wrapping a MetalView</summary>
   public iOSSurface (MetalView view) {
      mView = view;
      mView.LayoutChanged = OnLayoutChanged;
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Surface size in pixels (view bounds scaled by screen DPI)</summary>
   public Vec2S Size {
      get {
         nfloat scale = UIScreen.MainScreen.Scale;
         CGRect bounds = mView.Bounds;
         return new Vec2S ((int)(bounds.Width * scale), (int)(bounds.Height * scale));
      }
   }

   /// <summary>DPI scale factor (1x, 2x, or 3x on iOS devices)</summary>
   public double DPIScale => UIScreen.MainScreen.Scale;

   /// <summary>Native surface handle — the CAMetalLayer handle for WebGPU surface creation</summary>
   public IntPtr NativeHandle => mView.MetalLayer.Handle;

   /// <summary>Show or hide the cursor — no-op on iOS (touch devices have no cursor)</summary>
   public bool CursorVisible { set { } }

   /// <summary>Fired when the surface is resized</summary>
   public IObservable<Vec2S> Resized => mResized;

   /// <summary>Fired when the surface is ready for rendering</summary>
   public IObservable<int> Ready => mReady;

   // Methods ------------------------------------------------------------------
   /// <summary>Request a redraw of the surface</summary>
   public void Invalidate () => mDirty = true;

   /// <summary>Signal that the surface is ready for rendering</summary>
   internal void SignalReady () => mReady.OnNext (0);

   /// <summary>Check and clear the dirty flag</summary>
   internal bool ConsumeInvalidation () {
      bool wasDirty = mDirty;
      mDirty = false;
      return wasDirty;
   }

   /// <summary>The underlying MetalView</summary>
   internal MetalView View => mView;

   // Implementation -----------------------------------------------------------
   // Called when the MetalView layout changes — emit a resize notification
   void OnLayoutChanged () {
      nfloat scale = UIScreen.MainScreen.Scale;
      CGRect bounds = mView.Bounds;
      int w = (int)(bounds.Width * scale), h = (int)(bounds.Height * scale);
      mResized.OnNext (new Vec2S (w, h));
   }

   // Private data -------------------------------------------------------------
   MetalView mView;
   Subject<Vec2S> mResized = new ();
   Subject<int> mReady = new ();
   bool mDirty = true;
}
#endregion
