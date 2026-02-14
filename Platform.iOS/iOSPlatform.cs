// ────── ╔╗                                                                            PLATFORM.IOS
// ╔═╦╦═╦╦╬╣ iOSPlatform.cs
// ║║║║╬║╔╣║ Top-level iOS platform implementation providing surface, input, and font services
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class iOSPlatform -----------------------------------------------------------------------------
/// <summary>iOS implementation of IPlatform using UIKit and CADisplayLink for the render loop</summary>
public class iOSPlatform : IPlatform {
   // Properties ---------------------------------------------------------------
   /// <summary>Hardware input abstraction (touch events mapped to mouse events)</summary>
   public IInput Input => mInput;

   /// <summary>Font loading abstraction via FreeType</summary>
   public IFontLoader FontLoader => mFontLoader;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a renderable surface backed by a UIView with CAMetalLayer</summary>
   public ISurface CreateSurface (string title, int width, int height) {
      CGRect frame = new (0, 0, width, height);
      MetalView metalView = new (frame);
      metalView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
      iOSSurface surface = new (metalView);
      mSurface = surface;

      // Create the input overlay on top of the metal view
      iOSInputView inputView = mInput.CreateInputView (frame);
      inputView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
      mInputView = inputView;

      // Set up the view controller
      UIViewController vc = new ();
      vc.View!.AddSubview (metalView);
      vc.View.AddSubview (inputView);
      vc.Title = title;
      mViewController = vc;

      surface.SignalReady ();
      return surface;
   }

   /// <summary>Run the platform render loop using CADisplayLink</summary>
   /// The display link fires on each screen refresh (typically 60 or 120 Hz),
   /// calling the onFrame delegate with the elapsed delta time in seconds.
   public void Run (Action<double> onFrame) {
      mOnFrame = onFrame;
      mDisplayLink = CADisplayLink.Create (OnDisplayLinkFired);
      mDisplayLink.AddToRunLoop (NSRunLoop.Main, NSRunLoopMode.Default);
   }

   /// <summary>The view controller hosting the surface and input views</summary>
   public UIViewController? ViewController => mViewController;

   // Implementation -----------------------------------------------------------
   // Called by CADisplayLink on each screen refresh
   void OnDisplayLinkFired () {
      if (mOnFrame == null || mDisplayLink == null) return;
      double now = mDisplayLink.Timestamp;
      double dt = mLastTime < 0 ? 0 : now - mLastTime;
      mLastTime = now;
      mOnFrame (dt);
   }

   // Private data -------------------------------------------------------------
   iOSInput mInput = new ();
   iOSFontLoader mFontLoader = new ();
   iOSSurface? mSurface;
   iOSInputView? mInputView;
   UIViewController? mViewController;
   CADisplayLink? mDisplayLink;
   Action<double>? mOnFrame;
   double mLastTime = -1;
}
#endregion
