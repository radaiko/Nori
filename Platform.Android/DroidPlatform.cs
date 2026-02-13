// ────── ╔╗                                                                       PLATFORM.ANDROID
// ╔═╦╦═╦╦╬╣ DroidPlatform.cs
// ║║║║╬║╔╣║ Android IPlatform facade using SurfaceView and Choreographer for frame loop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Android.App;
using Android.Views;
namespace Nori;

#region class DroidPlatform --------------------------------------------------------------------------
/// <summary>Android platform implementation providing ISurface, IInput, and IFontLoader services</summary>
public class DroidPlatform : IPlatform {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a DroidPlatform for the given Android activity</summary>
   public DroidPlatform (Activity activity) {
      mActivity = activity;
      mInput = new DroidInput (activity);
      mFontLoader = new DroidFontLoader ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Hardware input abstraction</summary>
   public IInput Input => mInput;

   /// <summary>Font loading abstraction</summary>
   public IFontLoader FontLoader => mFontLoader;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a renderable surface (SurfaceView) with the given title and dimensions</summary>
   /// The title is applied to the activity. Width and height hint the initial layout
   /// but Android's layout system ultimately determines the actual surface size.
   public ISurface CreateSurface (string title, int width, int height) {
      mActivity.Title = title;
      DroidSurface surface = new (mActivity);
      // Wire touch events from the surface view to DroidInput
      surface.Touch += (_, e) => {
         if (e.Event != null) mInput.ProcessTouchEvent (e.Event);
      };
      mSurface = surface;
      // Add the surface view to the activity's content
      mActivity.SetContentView (surface);
      return surface;
   }

   /// <summary>Run the platform render loop using Choreographer frame callbacks</summary>
   /// Uses Android's Choreographer to schedule frame callbacks at the display's
   /// refresh rate, similar to requestAnimationFrame on the web.
   public void Run (Action<double> onFrame) {
      DroidFrameCallback callback = new (onFrame);
      Choreographer.Instance?.PostFrameCallback (callback);
   }

   // Private data -------------------------------------------------------------
   Activity mActivity;
   DroidSurface? mSurface;
   DroidInput mInput;
   DroidFontLoader mFontLoader;
}
#endregion

#region class DroidFrameCallback ---------------------------------------------------------------------
/// <summary>Choreographer frame callback that invokes onFrame with delta time each frame</summary>
class DroidFrameCallback : Java.Lang.Object, Choreographer.IFrameCallback {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a frame callback wrapping the given per-frame action</summary>
   public DroidFrameCallback (Action<double> onFrame) => mOnFrame = onFrame;

   // Methods ------------------------------------------------------------------
   /// <summary>Called on each choreographer frame with the frame timestamp in nanoseconds</summary>
   public void DoFrame (long frameTimeNanos) {
      // Convert nanoseconds to seconds
      double now = frameTimeNanos / 1_000_000_000.0;
      double dt = mLastTime < 0 ? 0 : now - mLastTime;
      mLastTime = now;
      mOnFrame (dt);
      // Schedule the next frame
      Choreographer.Instance?.PostFrameCallback (this);
   }

   // Private data -------------------------------------------------------------
   Action<double> mOnFrame;
   double mLastTime = -1;
}
#endregion
