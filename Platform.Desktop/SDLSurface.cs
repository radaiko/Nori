// ────── ╔╗                                                                        PLATFORM.DESKTOP
// ╔═╦╦═╦╦╬╣ SDLSurface.cs
// ║║║║╬║╔╣║ SDL2 window wrapper implementing ISurface for cross-platform rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class SDLSurface ---------------------------------------------------------------------------
/// <summary>SDL2 window wrapper that implements ISurface for cross-platform WebGPU rendering</summary>
unsafe class SDLSurface : ISurface {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a new SDLSurface wrapping an SDL window</summary>
   public SDLSurface (Sdl sdl, Window* window) {
      mSdl = sdl; mWindow = window;
      mResized = new ();
      mReady = new ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Surface size in pixels</summary>
   public Vec2S Size {
      get {
         int w = 0, h = 0;
         mSdl.GetWindowSize (mWindow, ref w, ref h);
         return new Vec2S (w, h);
      }
   }

   /// <summary>DPI scale factor (1.0 for standard, 2.0 for retina/HiDPI)</summary>
   public double DPIScale {
      get {
         int w = 0, h = 0, dw = 0, dh = 0;
         mSdl.GetWindowSize (mWindow, ref w, ref h);
         mSdl.GLGetDrawableSize (mWindow, ref dw, ref dh);
         return w > 0 ? (double)dw / w : 1.0;
      }
   }

   /// <summary>Native surface handle (SDL_Window pointer) for WebGPU surface creation</summary>
   public IntPtr NativeHandle => (IntPtr)mWindow;

   /// <summary>Show or hide the cursor over the surface</summary>
   public bool CursorVisible {
      set => mSdl.ShowCursor (value ? Sdl.Enable : Sdl.Disable);
   }

   /// <summary>Fired when the surface is resized</summary>
   public IObservable<Vec2S> Resized => mResized;

   /// <summary>Fired when the surface is ready for rendering</summary>
   public IObservable<int> Ready => mReady;

   // Methods ------------------------------------------------------------------
   /// <summary>Request a redraw of the surface</summary>
   public void Invalidate () => mDirty = true;

   /// <summary>Signal that the surface is ready for rendering</summary>
   public void SignalReady () => mReady.OnNext (0);

   /// <summary>Signal that the surface has been resized</summary>
   public void SignalResized () {
      int w = 0, h = 0;
      mSdl.GetWindowSize (mWindow, ref w, ref h);
      mResized.OnNext (new Vec2S (w, h));
   }

   /// <summary>Check and clear the dirty flag</summary>
   public bool ConsumeInvalidation () {
      bool wasDirty = mDirty;
      mDirty = false;
      return wasDirty;
   }

   // Private data -------------------------------------------------------------
   Sdl mSdl;
   Window* mWindow;
   Subject<Vec2S> mResized;
   Subject<int> mReady;
   bool mDirty = true;
}
#endregion
