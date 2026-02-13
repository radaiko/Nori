// ────── ╔╗                                                                        PLATFORM.DESKTOP
// ╔═╦╦═╦╦╬╣ SDLPlatform.cs
// ║║║║╬║╔╣║ Top-level SDL2 platform implementation providing window, input, and font services
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
namespace Nori;

#region class SDLPlatform --------------------------------------------------------------------------
/// <summary>SDL2 cross-platform implementation of IPlatform for macOS and Linux desktops</summary>
unsafe class SDLPlatform : IPlatform, IDisposable {
   // Constructors -------------------------------------------------------------
   /// <summary>Initialize SDL2 and create the platform services</summary>
   public SDLPlatform () {
      mSdl = Sdl.GetApi ();
      if (mSdl.Init (Sdl.InitVideo | Sdl.InitEvents) < 0)
         throw new Exception ($"SDL_Init failed: {mSdl.GetErrorS ()}");
      mInput = new SDLInput (mSdl);
      mFontLoader = new SDLFontLoader ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Hardware input abstraction</summary>
   public IInput Input => mInput;

   /// <summary>Font loading abstraction</summary>
   public IFontLoader FontLoader => mFontLoader;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a renderable SDL window surface with the given title and dimensions</summary>
   public ISurface CreateSurface (string title, int width, int height) {
      uint flags = (uint)(WindowFlags.Resizable | WindowFlags.AllowHighdpi);
      Window* window = mSdl.CreateWindow (title,
         Sdl.WindowposUndefined, Sdl.WindowposUndefined,
         width, height, flags);
      if (window == null)
         throw new Exception ($"SDL_CreateWindow failed: {mSdl.GetErrorS ()}");
      SDLSurface surface = new (mSdl, window);
      mSurface = surface;
      surface.SignalReady ();
      return surface;
   }

   /// <summary>Run the SDL event loop, calling onFrame each iteration with the elapsed delta time</summary>
   public void Run (Action<double> onFrame) {
      Stopwatch sw = Stopwatch.StartNew ();
      double lastTime = 0;
      mRunning = true;
      while (mRunning) {
         // Poll and process all pending SDL events
         Event ev = new ();
         while (mSdl.PollEvent (ref ev) != 0) {
            if ((EventType)ev.Type == EventType.Quit) { mRunning = false; break; }
            // Window resize events trigger surface notification
            if ((EventType)ev.Type == EventType.Windowevent &&
                (WindowEventID)ev.Window.Event == WindowEventID.Resized)
               mSurface?.SignalResized ();
            mInput.ProcessEvent (ev);
         }
         if (!mRunning) break;

         // Compute delta time and invoke the frame callback
         double now = sw.Elapsed.TotalSeconds;
         double delta = now - lastTime;
         lastTime = now;
         onFrame (delta);
      }
   }

   // IDisposable --------------------------------------------------------------
   /// <summary>Shut down SDL and release resources</summary>
   public void Dispose () {
      mSdl.Quit ();
      mSdl.Dispose ();
   }

   // Private data -------------------------------------------------------------
   Sdl mSdl;
   SDLInput mInput;
   SDLFontLoader mFontLoader;
   SDLSurface? mSurface;
   bool mRunning;
}
#endregion
