// ────── ╔╗                                                                       PLATFORM.WINDOWS
// ╔═╦╦═╦╦╬╣ WinPlatform.cs
// ║║║║╬║╔╣║ Windows IPlatform facade using WPF Window + WindowsFormsHost + Dispatcher loop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
namespace Nori;

#region class WinPlatform --------------------------------------------------------------------------
/// <summary>Windows platform implementation using WPF for windowing and Windows Forms for the surface</summary>
public class WinPlatform : IPlatform {
   // Properties ---------------------------------------------------------------
   /// <summary>Hardware input abstraction</summary>
   public IInput Input => mInput;

   /// <summary>Font loading abstraction</summary>
   public IFontLoader FontLoader => mFontLoader;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a renderable surface (window) with the given title and dimensions</summary>
   public ISurface CreateSurface (string title, int width, int height) {
      mSurface = new WinSurface ();
      mInput.SetPanel (mSurface);
      mWindow = new Window {
         Title = title, Width = width, Height = height,
         Content = new WindowsFormsHost { Child = mSurface, Focusable = false }
      };
      mWindow.Loaded += (_, _) => {
         DispatcherTimer timer = new () { Interval = TimeSpan.FromSeconds (0.1), IsEnabled = true };
         timer.Tick += (_, _) => { mSurface.Focus (); timer.IsEnabled = false; };
      };
      mWindow.Show ();
      return mSurface;
   }

   /// <summary>Run the platform event loop, calling onFrame each frame with the elapsed time in seconds</summary>
   public void Run (Action<double> onFrame) {
      Stopwatch sw = Stopwatch.StartNew ();
      double lastTime = 0;
      DispatcherTimer frameTimer = new () { Interval = TimeSpan.FromMilliseconds (1), IsEnabled = true };
      frameTimer.Tick += (_, _) => {
         double now = sw.Elapsed.TotalSeconds;
         double dt = now - lastTime;
         lastTime = now;
         onFrame (dt);
      };
      Dispatcher.Run ();
   }

   // Private data -------------------------------------------------------------
   WinSurface? mSurface;
   Window? mWindow;
   WinInput mInput = new ();
   WinFontLoader mFontLoader = new ();
}
#endregion
