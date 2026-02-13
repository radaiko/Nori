// ────── ╔╗                                                                       PLATFORM.ANDROID
// ╔═╦╦═╦╦╬╣ DroidSurface.cs
// ║║║║╬║╔╣║ Android SurfaceView implementing ISurface for WebGPU rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Android.Content;
using Android.Runtime;
using Android.Views;
namespace Nori;

#region class DroidSurface ---------------------------------------------------------------------------
/// <summary>Android SurfaceView wrapper that provides a native surface for WebGPU rendering</summary>
class DroidSurface : SurfaceView, ISurface, ISurfaceHolderCallback {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a new DroidSurface within the given Android context</summary>
   public DroidSurface (Context context) : base (context) {
      Holder!.AddCallback (this);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Surface size in pixels</summary>
   Vec2S ISurface.Size => new (Width, Height);

   /// <summary>DPI scale factor based on display density</summary>
   public double DPIScale
      => Resources?.DisplayMetrics?.Density ?? 1.0;

   /// <summary>Native surface handle for WebGPU surface creation</summary>
   public IntPtr NativeHandle => mNativeHandle;

   /// <summary>Show or hide the cursor — no-op on touch devices</summary>
   bool ISurface.CursorVisible { set { } }

   /// <summary>Fired when the surface is resized</summary>
   public IObservable<Vec2S> Resized => mResized;

   /// <summary>Fired when the surface is ready for rendering</summary>
   public IObservable<int> Ready => mReady;

   // Methods ------------------------------------------------------------------
   /// <summary>Request a redraw of the surface</summary>
   void ISurface.Invalidate () => PostInvalidate ();

   // ISurfaceHolderCallback ---------------------------------------------------
   /// <summary>Called when the surface is first created and ready for rendering</summary>
   public void SurfaceCreated (ISurfaceHolder holder) {
      mNativeHandle = holder.Surface?.Handle ?? IntPtr.Zero;
      mReady.OnNext (0);
   }

   /// <summary>Called when the surface dimensions change</summary>
   public void SurfaceChanged (ISurfaceHolder holder, [GeneratedEnum] Android.Graphics.Format format, int width, int height) {
      mNativeHandle = holder.Surface?.Handle ?? IntPtr.Zero;
      mResized.OnNext (new Vec2S (width, height));
   }

   /// <summary>Called when the surface is being destroyed</summary>
   public void SurfaceDestroyed (ISurfaceHolder holder) {
      mNativeHandle = IntPtr.Zero;
   }

   // Private data -------------------------------------------------------------
   IntPtr mNativeHandle;
   Subject<Vec2S> mResized = new ();
   Subject<int> mReady = new ();
}
#endregion
