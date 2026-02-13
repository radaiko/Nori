// ────── ╔╗                                                                            PLATFORM.WEB
// ╔═╦╦═╦╦╬╣ WebInput.cs
// ║║║║╬║╔╣║ IInput implementation using JSExport callbacks from browser DOM events
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class WebInput -----------------------------------------------------------------------------
/// <summary>IInput implementation receiving browser keyboard and mouse events via JSExport callbacks</summary>
partial class WebInput : IInput {
   // Properties ---------------------------------------------------------------
   /// <summary>Observable stream of keyboard events</summary>
   public IObservable<KeyInfo> Keys => mKeys;

   /// <summary>Observable stream of mouse button click and release events</summary>
   public IObservable<MouseClickInfo> MouseClicks => mMouseClicks;

   /// <summary>Observable stream of mouse move events</summary>
   public IObservable<Vec2S> MouseMoves => mMouseMoves;

   /// <summary>Observable stream of mouse wheel events</summary>
   public IObservable<MouseWheelInfo> MouseWheel => mMouseWheel;

   /// <summary>Fired when the mouse leaves the client area</summary>
   public IObservable<int> MouseLeave => mMouseLeave;

   /// <summary>Fired when mouse capture is lost</summary>
   public IObservable<int> MouseLost => mMouseLost;

   /// <summary>Is the SHIFT key currently pressed?</summary>
   public bool IsShiftDown => mShiftDown;

   /// <summary>Is the CONTROL key currently pressed?</summary>
   public bool IsCtrlDown => mCtrlDown;

   /// <summary>Is the ALT key currently pressed?</summary>
   public bool IsAltDown => mAltDown;

   // Methods ------------------------------------------------------------------
   /// <summary>Capture or release the mouse via setPointerCapture/releasePointerCapture</summary>
   public bool CaptureMouse (bool capture)
      => NoriWebPlatform.SetPointerCapture (mCanvasId, capture);

   /// <summary>Set the canvas element ID for pointer capture operations</summary>
   internal void SetCanvasId (string canvasId) => mCanvasId = canvasId;

   // JSExport callbacks -------------------------------------------------------
   /// <summary>Called from JavaScript when a key is pressed</summary>
   [JSExport]
   public static void OnKeyDown (int keyCode, bool shift, bool ctrl, bool alt) {
      if (sInstance == null) return;
      sInstance.mShiftDown = shift; sInstance.mCtrlDown = ctrl; sInstance.mAltDown = alt;
      EKey key = MapKeyCode (keyCode);
      EKeyModifier mods = BuildModifiers (shift, ctrl, alt);
      sInstance.mKeys.OnNext (new KeyInfo (key, mods, EKeyState.Pressed));
   }

   /// <summary>Called from JavaScript when a key is released</summary>
   [JSExport]
   public static void OnKeyUp (int keyCode, bool shift, bool ctrl, bool alt) {
      if (sInstance == null) return;
      sInstance.mShiftDown = shift; sInstance.mCtrlDown = ctrl; sInstance.mAltDown = alt;
      EKey key = MapKeyCode (keyCode);
      EKeyModifier mods = BuildModifiers (shift, ctrl, alt);
      sInstance.mKeys.OnNext (new KeyInfo (key, mods, EKeyState.Released));
   }

   /// <summary>Called from JavaScript when a mouse button is pressed</summary>
   [JSExport]
   public static void OnMouseDown (int button, int x, int y, bool shift, bool ctrl, bool alt) {
      if (sInstance == null) return;
      sInstance.mShiftDown = shift; sInstance.mCtrlDown = ctrl; sInstance.mAltDown = alt;
      EMouseButton btn = MapMouseButton (button);
      EKeyModifier mods = BuildModifiers (shift, ctrl, alt);
      sInstance.mMouseClicks.OnNext (new MouseClickInfo (btn, new Vec2S (x, y), mods, EKeyState.Pressed));
   }

   /// <summary>Called from JavaScript when a mouse button is released</summary>
   [JSExport]
   public static void OnMouseUp (int button, int x, int y, bool shift, bool ctrl, bool alt) {
      if (sInstance == null) return;
      sInstance.mShiftDown = shift; sInstance.mCtrlDown = ctrl; sInstance.mAltDown = alt;
      EMouseButton btn = MapMouseButton (button);
      EKeyModifier mods = BuildModifiers (shift, ctrl, alt);
      sInstance.mMouseClicks.OnNext (new MouseClickInfo (btn, new Vec2S (x, y), mods, EKeyState.Released));
   }

   /// <summary>Called from JavaScript when the mouse moves</summary>
   [JSExport]
   public static void OnMouseMove (int x, int y) {
      if (sInstance == null) return;
      sInstance.mMouseMoves.OnNext (new Vec2S (x, y));
   }

   /// <summary>Called from JavaScript when the mouse wheel is scrolled</summary>
   [JSExport]
   public static void OnMouseWheel (int delta, int x, int y) {
      if (sInstance == null) return;
      sInstance.mMouseWheel.OnNext (new MouseWheelInfo (delta, new Vec2S (x, y)));
   }

   /// <summary>Called from JavaScript when the mouse leaves the canvas</summary>
   [JSExport]
   public static void OnMouseLeave () {
      if (sInstance == null) return;
      sInstance.mMouseLeave.OnNext (0);
   }

   /// <summary>Called from JavaScript when pointer capture is lost</summary>
   [JSExport]
   public static void OnPointerCaptureLost () {
      if (sInstance == null) return;
      sInstance.mMouseLost.OnNext (0);
   }

   /// <summary>Register this instance as the singleton for JSExport callbacks</summary>
   internal void Register () => sInstance = this;

   // Implementation -----------------------------------------------------------
   // Build an EKeyModifier from individual booleans
   static EKeyModifier BuildModifiers (bool shift, bool ctrl, bool alt) {
      EKeyModifier mods = EKeyModifier.None;
      if (shift) mods |= EKeyModifier.Shift;
      if (ctrl) mods |= EKeyModifier.Control;
      if (alt) mods |= EKeyModifier.Alt;
      return mods;
   }

   // Map JavaScript mouse button numbers to Nori EMouseButton
   static EMouseButton MapMouseButton (int jsButton)
      => jsButton switch { 1 => EMouseButton.Middle, 2 => EMouseButton.Right, _ => EMouseButton.Left };

   // Map JavaScript key codes (KeyboardEvent.keyCode) to Nori EKey.
   // JavaScript keyCode values for letters (65..90) and digits (48..57)
   // happen to match the EKey enum values directly since EKey uses
   // the ASCII char codes. Other keys need explicit mapping.
   static EKey MapKeyCode (int keyCode)
      => keyCode switch {
         >= 65 and <= 90 => (EKey)keyCode,          // A..Z
         >= 48 and <= 57 => (EKey)keyCode,          // 0..9
         >= 96 and <= 105 => (EKey)(keyCode - 96 + (int)EKey.NPad0),  // Numpad 0..9
         >= 112 and <= 123 => (EKey)(keyCode - 112 + (int)EKey.F1),   // F1..F12
         27 => EKey.Escape,
         8 => EKey.Backspace,
         9 => EKey.Tab,
         13 => EKey.Enter,
         32 => EKey.Space,
         16 => EKey.Shift,
         17 => EKey.Ctrl,
         18 => EKey.Alt,
         20 => EKey.CapsLock,
         144 => EKey.NumLock,
         145 => EKey.Scroll,
         19 => EKey.Pause,
         45 => EKey.Insert,
         46 => EKey.Delete,
         36 => EKey.Home,
         35 => EKey.End,
         33 => EKey.PageUp,
         34 => EKey.PageDown,
         37 => EKey.Left,
         38 => EKey.Up,
         39 => EKey.Right,
         40 => EKey.Down,
         91 or 92 => EKey.Windows,
         93 => EKey.Menu,
         192 => EKey.Tilde,
         189 => EKey.Hyphen,
         187 => EKey.Equals,
         219 => EKey.OpenBracket,
         221 => EKey.CloseBracket,
         220 => EKey.Backslash,
         186 => EKey.Semicolon,
         222 => EKey.Quote,
         111 => EKey.NDivide,
         106 => EKey.NMultiply,
         109 => EKey.NSubtract,
         107 => EKey.NAdd,
         110 => EKey.NPeriod,
         _ => (EKey)keyCode
      };

   // Private data -------------------------------------------------------------
   string mCanvasId = "";
   bool mShiftDown, mCtrlDown, mAltDown;
   Subject<KeyInfo> mKeys = new ();
   Subject<MouseClickInfo> mMouseClicks = new ();
   Subject<Vec2S> mMouseMoves = new ();
   Subject<MouseWheelInfo> mMouseWheel = new ();
   Subject<int> mMouseLeave = new ();
   Subject<int> mMouseLost = new ();
   static WebInput? sInstance;
}
#endregion
