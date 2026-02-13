// ────── ╔╗                                                                       PLATFORM.ANDROID
// ╔═╦╦═╦╦╬╣ DroidInput.cs
// ║║║║╬║╔╣║ Android touch-to-mouse IInput mapping using gesture detectors
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Android.Content;
using Android.Views;
namespace Nori;

#region class DroidInput -----------------------------------------------------------------------------
/// <summary>IInput implementation translating Android touch gestures to mouse event streams</summary>
/// Single-finger touch maps to left mouse button clicks and moves. Two-finger drag
/// maps to middle mouse button (pan). Pinch maps to mouse wheel (zoom). Long press
/// maps to right mouse button.
class DroidInput : IInput {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a DroidInput with gesture detectors for the given Android context</summary>
   public DroidInput (Context context) {
      mGestureDetector = new GestureDetector (context, new GestureListener (this));
      mScaleDetector = new ScaleGestureDetector (context, new ScaleListener (this));
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Observable stream of keyboard events (no-op on Android, touch device)</summary>
   public IObservable<KeyInfo> Keys => mKeys;

   /// <summary>Observable stream of mouse button click and release events</summary>
   public IObservable<MouseClickInfo> MouseClicks => mMouseClicks;

   /// <summary>Observable stream of mouse move events</summary>
   public IObservable<Vec2S> MouseMoves => mMouseMoves;

   /// <summary>Observable stream of mouse wheel events</summary>
   public IObservable<MouseWheelInfo> MouseWheel => mMouseWheel;

   /// <summary>Fired when the mouse leaves the client area (not applicable on Android)</summary>
   public IObservable<int> MouseLeave => mMouseLeave;

   /// <summary>Fired when mouse capture is lost (not applicable on Android)</summary>
   public IObservable<int> MouseLost => mMouseLost;

   /// <summary>Is the SHIFT key currently pressed? Always false on Android</summary>
   public bool IsShiftDown => false;

   /// <summary>Is the CONTROL key currently pressed? Always false on Android</summary>
   public bool IsCtrlDown => false;

   /// <summary>Is the ALT key currently pressed? Always false on Android</summary>
   public bool IsAltDown => false;

   // Methods ------------------------------------------------------------------
   /// <summary>Capture or release the mouse — always succeeds on Android</summary>
   public bool CaptureMouse (bool capture) => true;

   /// <summary>Process a raw Android MotionEvent, dispatching to gesture detectors and observables</summary>
   public bool ProcessTouchEvent (MotionEvent e) {
      // Let gesture detectors process first
      mScaleDetector.OnTouchEvent (e);
      mGestureDetector.OnTouchEvent (e);

      MotionEventActions action = e.ActionMasked;
      int pointerCount = e.PointerCount;

      switch (action) {
         case MotionEventActions.Down: {
            // Single finger down — left mouse button press
            Vec2S pos = ToVec2S (e, 0);
            mLastPos = pos;
            mIsPanning = false;
            PushClick (EMouseButton.Left, pos, EKeyState.Pressed);
            break;
         }
         case MotionEventActions.PointerDown: {
            // Second finger down — begin pan mode with middle button
            if (pointerCount >= 2) {
               Vec2S center = MidPoint (e);
               mIsPanning = true;
               // Release the left button that was pressed on initial touch
               PushClick (EMouseButton.Left, center, EKeyState.Released);
               // Start middle button drag for pan
               PushClick (EMouseButton.Middle, center, EKeyState.Pressed);
               mLastPos = center;
            }
            break;
         }
         case MotionEventActions.Move: {
            if (mIsPanning && pointerCount >= 2) {
               // Two-finger move — pan via middle button drag
               Vec2S center = MidPoint (e);
               mMouseMoves.OnNext (center);
               mLastPos = center;
            } else if (!mIsPanning && pointerCount == 1) {
               // Single finger move — mouse move
               Vec2S pos = ToVec2S (e, 0);
               mMouseMoves.OnNext (pos);
               mLastPos = pos;
            }
            break;
         }
         case MotionEventActions.PointerUp: {
            // A secondary finger lifted — end pan
            if (mIsPanning) {
               PushClick (EMouseButton.Middle, mLastPos, EKeyState.Released);
               mIsPanning = false;
            }
            break;
         }
         case MotionEventActions.Up: {
            // Last finger lifted — release active button
            Vec2S pos = ToVec2S (e, 0);
            if (mIsPanning) {
               PushClick (EMouseButton.Middle, pos, EKeyState.Released);
               mIsPanning = false;
            } else {
               PushClick (EMouseButton.Left, pos, EKeyState.Released);
            }
            break;
         }
         case MotionEventActions.Cancel: {
            // Touch cancelled — release everything
            if (mIsPanning)
               PushClick (EMouseButton.Middle, mLastPos, EKeyState.Released);
            else
               PushClick (EMouseButton.Left, mLastPos, EKeyState.Released);
            mIsPanning = false;
            mMouseLost.OnNext (0);
            break;
         }
      }
      return true;
   }

   // Implementation -----------------------------------------------------------
   // Push a mouse click event to observers
   internal void PushClick (EMouseButton button, Vec2S pos, EKeyState state)
      => mMouseClicks.OnNext (new MouseClickInfo (button, pos, EKeyModifier.None, state));

   // Push a mouse wheel event to observers
   internal void PushWheel (int delta, Vec2S pos)
      => mMouseWheel.OnNext (new MouseWheelInfo (delta, pos));

   // Convert a touch pointer at the given index to a Vec2S pixel coordinate
   static Vec2S ToVec2S (MotionEvent e, int pointerIndex)
      => new ((int)e.GetX (pointerIndex), (int)e.GetY (pointerIndex));

   // Compute the midpoint between the first two touch pointers
   static Vec2S MidPoint (MotionEvent e)
      => new ((int)((e.GetX (0) + e.GetX (1)) / 2), (int)((e.GetY (0) + e.GetY (1)) / 2));

   // Private data -------------------------------------------------------------
   GestureDetector mGestureDetector;
   ScaleGestureDetector mScaleDetector;
   bool mIsPanning;
   Vec2S mLastPos;
   Subject<KeyInfo> mKeys = new ();
   Subject<MouseClickInfo> mMouseClicks = new ();
   Subject<Vec2S> mMouseMoves = new ();
   Subject<MouseWheelInfo> mMouseWheel = new ();
   Subject<int> mMouseLeave = new ();
   Subject<int> mMouseLost = new ();
}
#endregion

#region class GestureListener ------------------------------------------------------------------------
/// <summary>GestureDetector listener that maps long-press to right mouse button</summary>
class GestureListener : GestureDetector.SimpleOnGestureListener {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a listener that forwards gestures to the given DroidInput</summary>
   public GestureListener (DroidInput input) => mInput = input;

   // Overrides ----------------------------------------------------------------
   /// <summary>Long press maps to right mouse button click (press + release)</summary>
   public override void OnLongPress (MotionEvent? e) {
      if (e == null) return;
      Vec2S pos = new ((int)e.GetX (), (int)e.GetY ());
      mInput.PushClick (EMouseButton.Right, pos, EKeyState.Pressed);
      mInput.PushClick (EMouseButton.Right, pos, EKeyState.Released);
   }

   // Private data -------------------------------------------------------------
   DroidInput mInput;
}
#endregion

#region class ScaleListener --------------------------------------------------------------------------
/// <summary>ScaleGestureDetector listener that maps pinch gestures to mouse wheel events</summary>
class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a listener that forwards scale events to the given DroidInput</summary>
   public ScaleListener (DroidInput input) => mInput = input;

   // Overrides ----------------------------------------------------------------
   /// <summary>Convert scale factor delta to a mouse wheel event</summary>
   public override bool OnScale (ScaleGestureDetector? detector) {
      if (detector == null) return false;
      // Scale factor > 1 means pinch apart (zoom in), < 1 means pinch together (zoom out)
      // Convert to wheel delta: multiply by 120 to match Windows convention
      float scaleFactor = detector.ScaleFactor;
      int delta = (int)((scaleFactor - 1.0f) * 120);
      if (delta == 0) return true;
      Vec2S pos = new ((int)detector.FocusX, (int)detector.FocusY);
      mInput.PushWheel (delta, pos);
      return true;
   }

   // Private data -------------------------------------------------------------
   DroidInput mInput;
}
#endregion
