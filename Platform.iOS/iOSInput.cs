// ────── ╔╗                                                                            PLATFORM.IOS
// ╔═╦╦═╦╦╬╣ iOSInput.cs
// ║║║║╬║╔╣║ IInput implementation mapping iOS touch and gesture events to mouse-style observables
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class iOSInputView ----------------------------------------------------------------------------
/// <summary>UIView subclass that captures touches and gestures, routing them to iOSInput</summary>
class iOSInputView : UIView {
   // Constructors -------------------------------------------------------------
   /// <summary>Create an input overlay view with the given frame</summary>
   public iOSInputView (CGRect frame, iOSInput input) : base (frame) {
      mInput = input;
      MultipleTouchEnabled = true;
      UserInteractionEnabled = true;
      BackgroundColor = UIColor.Clear;
      SetupGestureRecognizers ();
   }

   // Overrides ----------------------------------------------------------------
   // Single-finger touch → Left mouse button events
   public override void TouchesBegan (NSSet touches, UIEvent? evt) {
      base.TouchesBegan (touches, evt);
      if (mPinching || mPanning) return;
      UITouch? touch = touches.AnyObject as UITouch;
      if (touch == null) return;
      Vec2S pos = ToPixels (touch.LocationInView (this));
      mInput.FireMouseClick (EMouseButton.Left, pos, EKeyState.Pressed);
   }

   public override void TouchesMoved (NSSet touches, UIEvent? evt) {
      base.TouchesMoved (touches, evt);
      if (mPinching || mPanning) return;
      UITouch? touch = touches.AnyObject as UITouch;
      if (touch == null) return;
      Vec2S pos = ToPixels (touch.LocationInView (this));
      mInput.FireMouseMove (pos);
   }

   public override void TouchesEnded (NSSet touches, UIEvent? evt) {
      base.TouchesEnded (touches, evt);
      if (mPinching || mPanning) return;
      UITouch? touch = touches.AnyObject as UITouch;
      if (touch == null) return;
      Vec2S pos = ToPixels (touch.LocationInView (this));
      mInput.FireMouseClick (EMouseButton.Left, pos, EKeyState.Released);
   }

   public override void TouchesCancelled (NSSet touches, UIEvent? evt) {
      base.TouchesCancelled (touches, evt);
      mPinching = false; mPanning = false;
      UITouch? touch = touches.AnyObject as UITouch;
      if (touch == null) return;
      Vec2S pos = ToPixels (touch.LocationInView (this));
      mInput.FireMouseClick (EMouseButton.Left, pos, EKeyState.Released);
      mInput.FireMouseLost ();
   }

   // Implementation -----------------------------------------------------------
   // Set up gesture recognizers for pinch (zoom) and two-finger pan
   void SetupGestureRecognizers () {
      UIPinchGestureRecognizer pinch = new (OnPinch);
      pinch.CancelsTouchesInView = false;
      AddGestureRecognizer (pinch);

      UIPanGestureRecognizer pan = new (OnPan);
      pan.MinimumNumberOfTouches = 2;
      pan.MaximumNumberOfTouches = 2;
      pan.CancelsTouchesInView = false;
      AddGestureRecognizer (pan);

      UILongPressGestureRecognizer longPress = new (OnLongPress);
      longPress.MinimumPressDuration = 0.5;
      longPress.CancelsTouchesInView = false;
      AddGestureRecognizer (longPress);
   }

   // Pinch gesture → mouse wheel events (zoom)
   void OnPinch (UIPinchGestureRecognizer gesture) {
      CGPoint center = gesture.LocationInView (this);
      Vec2S pos = ToPixels (center);
      switch (gesture.State) {
         case UIGestureRecognizerState.Began:
            mPinching = true;
            mLastPinchScale = 1.0;
            break;
         case UIGestureRecognizerState.Changed:
            // Convert scale delta to wheel delta (120 units per notch, like Windows)
            double scaleDelta = gesture.Scale / mLastPinchScale;
            int wheelDelta = (int)((scaleDelta - 1.0) * 120);
            if (wheelDelta != 0)
               mInput.FireMouseWheel (wheelDelta, pos);
            mLastPinchScale = gesture.Scale;
            break;
         case UIGestureRecognizerState.Ended:
         case UIGestureRecognizerState.Cancelled:
            mPinching = false;
            break;
      }
   }

   // Two-finger pan → middle mouse button drag (for panning the view)
   void OnPan (UIPanGestureRecognizer gesture) {
      CGPoint center = gesture.LocationInView (this);
      Vec2S pos = ToPixels (center);
      switch (gesture.State) {
         case UIGestureRecognizerState.Began:
            mPanning = true;
            mInput.FireMouseClick (EMouseButton.Middle, pos, EKeyState.Pressed);
            break;
         case UIGestureRecognizerState.Changed:
            mInput.FireMouseMove (pos);
            break;
         case UIGestureRecognizerState.Ended:
         case UIGestureRecognizerState.Cancelled:
            mInput.FireMouseClick (EMouseButton.Middle, pos, EKeyState.Released);
            mPanning = false;
            break;
      }
   }

   // Long press → right mouse button click (context menu)
   void OnLongPress (UILongPressGestureRecognizer gesture) {
      CGPoint location = gesture.LocationInView (this);
      Vec2S pos = ToPixels (location);
      switch (gesture.State) {
         case UIGestureRecognizerState.Began:
            mInput.FireMouseClick (EMouseButton.Right, pos, EKeyState.Pressed);
            break;
         case UIGestureRecognizerState.Ended:
         case UIGestureRecognizerState.Cancelled:
            mInput.FireMouseClick (EMouseButton.Right, pos, EKeyState.Released);
            break;
      }
   }

   // Convert a UIKit point (in points) to pixel coordinates
   Vec2S ToPixels (CGPoint pt) {
      nfloat scale = UIScreen.MainScreen.Scale;
      return new Vec2S ((int)(pt.X * scale), (int)(pt.Y * scale));
   }

   // Private data -------------------------------------------------------------
   iOSInput mInput;
   bool mPinching, mPanning;
   double mLastPinchScale = 1.0;
}
#endregion

#region class iOSInput ---------------------------------------------------------------------------------
/// <summary>IInput implementation translating iOS touch/gesture events into Nori observable streams</summary>
class iOSInput : IInput {
   // Properties ---------------------------------------------------------------
   /// <summary>Observable stream of keyboard events (no-op on iOS — no physical keyboard)</summary>
   public IObservable<KeyInfo> Keys => mKeys;

   /// <summary>Observable stream of mouse button click and release events</summary>
   public IObservable<MouseClickInfo> MouseClicks => mMouseClicks;

   /// <summary>Observable stream of mouse move events</summary>
   public IObservable<Vec2S> MouseMoves => mMouseMoves;

   /// <summary>Observable stream of mouse wheel events (mapped from pinch gesture)</summary>
   public IObservable<MouseWheelInfo> MouseWheel => mMouseWheel;

   /// <summary>Fired when touch leaves the view area</summary>
   public IObservable<int> MouseLeave => mMouseLeave;

   /// <summary>Fired when touch capture is lost (touch cancelled)</summary>
   public IObservable<int> MouseLost => mMouseLost;

   /// <summary>Is the SHIFT key currently pressed? (always false on iOS)</summary>
   public bool IsShiftDown => false;

   /// <summary>Is the CONTROL key currently pressed? (always false on iOS)</summary>
   public bool IsCtrlDown => false;

   /// <summary>Is the ALT key currently pressed? (always false on iOS)</summary>
   public bool IsAltDown => false;

   // Methods ------------------------------------------------------------------
   /// <summary>Capture or release touch — always returns true (touch is implicitly captured on iOS)</summary>
   public bool CaptureMouse (bool capture) => true;

   /// <summary>Create an input overlay view for the given frame</summary>
   internal iOSInputView CreateInputView (CGRect frame) => new (frame, this);

   // Internal fire methods called by iOSInputView ----------------------------
   /// <summary>Fire a mouse click event</summary>
   internal void FireMouseClick (EMouseButton button, Vec2S pos, EKeyState state)
      => mMouseClicks.OnNext (new MouseClickInfo (button, pos, EKeyModifier.None, state));

   /// <summary>Fire a mouse move event</summary>
   internal void FireMouseMove (Vec2S pos)
      => mMouseMoves.OnNext (pos);

   /// <summary>Fire a mouse wheel event</summary>
   internal void FireMouseWheel (int delta, Vec2S pos)
      => mMouseWheel.OnNext (new MouseWheelInfo (delta, pos));

   /// <summary>Fire a mouse-leave event</summary>
   internal void FireMouseLeave ()
      => mMouseLeave.OnNext (0);

   /// <summary>Fire a mouse-capture-lost event</summary>
   internal void FireMouseLost ()
      => mMouseLost.OnNext (0);

   // Private data -------------------------------------------------------------
   Subject<KeyInfo> mKeys = new ();
   Subject<MouseClickInfo> mMouseClicks = new ();
   Subject<Vec2S> mMouseMoves = new ();
   Subject<MouseWheelInfo> mMouseWheel = new ();
   Subject<int> mMouseLeave = new ();
   Subject<int> mMouseLost = new ();
}
#endregion
