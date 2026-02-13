// ────── ╔╗                                                                        PLATFORM.DESKTOP
// ╔═╦╦═╦╦╬╣ SDLInput.cs
// ║║║║╬║╔╣║ SDL2 input handler implementing IInput for cross-platform keyboard and mouse events
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class SDLInput -----------------------------------------------------------------------------
/// <summary>SDL2 input handler that translates SDL events into Nori's IInput observable streams</summary>
unsafe class SDLInput : IInput {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a new SDLInput backed by the given SDL instance</summary>
   public SDLInput (Sdl sdl) => mSdl = sdl;

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
   public bool IsShiftDown => (mSdl.GetModState () & Keymod.Shift) != 0;

   /// <summary>Is the CONTROL key currently pressed?</summary>
   public bool IsCtrlDown => (mSdl.GetModState () & Keymod.Ctrl) != 0;

   /// <summary>Is the ALT key currently pressed?</summary>
   public bool IsAltDown => (mSdl.GetModState () & Keymod.Alt) != 0;

   // Methods ------------------------------------------------------------------
   /// <summary>Capture or release the mouse, returns true if successful</summary>
   public bool CaptureMouse (bool capture)
      => mSdl.CaptureMouse (capture ? SdlBool.True : SdlBool.False) == 0;

   /// <summary>Process a raw SDL event, dispatching to the appropriate observable stream</summary>
   public void ProcessEvent (Event e) {
      switch ((EventType)e.Type) {
         case EventType.Keydown:
         case EventType.Keyup: {
            EKeyState state = (EventType)e.Type == EventType.Keydown ? EKeyState.Pressed : EKeyState.Released;
            EKey key = MapKey ((KeyCode)e.Key.Keysym.Sym);
            EKeyModifier mod = MapModifiers ((Keymod)e.Key.Keysym.Mod);
            mKeys.OnNext (new KeyInfo (key, mod, state));
            break;
         }
         case EventType.Mousebuttondown:
         case EventType.Mousebuttonup: {
            EKeyState state = (EventType)e.Type == EventType.Mousebuttondown ? EKeyState.Pressed : EKeyState.Released;
            EMouseButton button = e.Button.Button switch {
               (byte)Sdl.ButtonLeft => EMouseButton.Left,
               (byte)Sdl.ButtonMiddle => EMouseButton.Middle,
               (byte)Sdl.ButtonRight => EMouseButton.Right,
               _ => EMouseButton.Left
            };
            Vec2S pos = new (e.Button.X, e.Button.Y);
            EKeyModifier mod = MapModifiers (mSdl.GetModState ());
            mMouseClicks.OnNext (new MouseClickInfo (button, pos, mod, state));
            break;
         }
         case EventType.Mousemotion:
            mMouseMoves.OnNext (new Vec2S (e.Motion.X, e.Motion.Y));
            break;
         case EventType.Mousewheel: {
            // SDL2 2.26+ provides MouseX/MouseY on the wheel event directly
            int delta = e.Wheel.Y * 120;  // Multiply by 120 to match Windows convention
            Vec2S pos = new (e.Wheel.MouseX, e.Wheel.MouseY);
            mMouseWheel.OnNext (new MouseWheelInfo (delta, pos));
            break;
         }
         case EventType.Windowevent:
            switch ((WindowEventID)e.Window.Event) {
               case WindowEventID.Leave:
                  mMouseLeave.OnNext (0);
                  break;
            }
            break;
      }
   }

   // Implementation -----------------------------------------------------------
   // Map SDL key code to Nori EKey enum
   static EKey MapKey (KeyCode sdlKey) {
      return sdlKey switch {
         KeyCode.KEscape => EKey.Escape,
         KeyCode.KBackspace => EKey.Backspace,
         KeyCode.KTab => EKey.Tab,
         KeyCode.KReturn => EKey.Enter,
         KeyCode.KSpace => EKey.Space,

         KeyCode.K0 => EKey.D0, KeyCode.K1 => EKey.D1, KeyCode.K2 => EKey.D2,
         KeyCode.K3 => EKey.D3, KeyCode.K4 => EKey.D4, KeyCode.K5 => EKey.D5,
         KeyCode.K6 => EKey.D6, KeyCode.K7 => EKey.D7, KeyCode.K8 => EKey.D8,
         KeyCode.K9 => EKey.D9,

         KeyCode.KA => EKey.A, KeyCode.KB => EKey.B, KeyCode.KC => EKey.C,
         KeyCode.KD => EKey.D, KeyCode.KE => EKey.E, KeyCode.KF => EKey.F,
         KeyCode.KG => EKey.G, KeyCode.KH => EKey.H, KeyCode.KI => EKey.I,
         KeyCode.KJ => EKey.J, KeyCode.KK => EKey.K, KeyCode.KL => EKey.L,
         KeyCode.KM => EKey.M, KeyCode.KN => EKey.N, KeyCode.KO => EKey.O,
         KeyCode.KP => EKey.P, KeyCode.KQ => EKey.Q, KeyCode.KR => EKey.R,
         KeyCode.KS => EKey.S, KeyCode.KT => EKey.T, KeyCode.KU => EKey.U,
         KeyCode.KV => EKey.V, KeyCode.KW => EKey.W, KeyCode.KX => EKey.X,
         KeyCode.KY => EKey.Y, KeyCode.KZ => EKey.Z,

         KeyCode.KF1 => EKey.F1, KeyCode.KF2 => EKey.F2, KeyCode.KF3 => EKey.F3,
         KeyCode.KF4 => EKey.F4, KeyCode.KF5 => EKey.F5, KeyCode.KF6 => EKey.F6,
         KeyCode.KF7 => EKey.F7, KeyCode.KF8 => EKey.F8, KeyCode.KF9 => EKey.F9,
         KeyCode.KF10 => EKey.F10, KeyCode.KF11 => EKey.F11, KeyCode.KF12 => EKey.F12,

         KeyCode.KLshift or KeyCode.KRshift => EKey.Shift,
         KeyCode.KLctrl or KeyCode.KRctrl => EKey.Ctrl,
         KeyCode.KLalt or KeyCode.KRalt => EKey.Alt,

         KeyCode.KUp => EKey.Up, KeyCode.KDown => EKey.Down,
         KeyCode.KLeft => EKey.Left, KeyCode.KRight => EKey.Right,

         KeyCode.KHome => EKey.Home, KeyCode.KEnd => EKey.End,
         KeyCode.KPageup => EKey.PageUp, KeyCode.KPagedown => EKey.PageDown,
         KeyCode.KInsert => EKey.Insert, KeyCode.KDelete => EKey.Delete,

         KeyCode.KCapslock => EKey.CapsLock, KeyCode.KScrolllock => EKey.Scroll,
         KeyCode.KNumlockclear => EKey.NumLock, KeyCode.KPause => EKey.Pause,
         KeyCode.KMenu or KeyCode.KApplication => EKey.Menu,
         KeyCode.KLgui or KeyCode.KRgui => EKey.Windows,

         KeyCode.KKP0 => EKey.NPad0, KeyCode.KKP1 => EKey.NPad1,
         KeyCode.KKP2 => EKey.NPad2, KeyCode.KKP3 => EKey.NPad3,
         KeyCode.KKP4 => EKey.NPad4, KeyCode.KKP5 => EKey.NPad5,
         KeyCode.KKP6 => EKey.NPad6, KeyCode.KKP7 => EKey.NPad7,
         KeyCode.KKP8 => EKey.NPad8, KeyCode.KKP9 => EKey.NPad9,
         KeyCode.KKPDivide => EKey.NDivide, KeyCode.KKPMultiply => EKey.NMultiply,
         KeyCode.KKPMinus => EKey.NSubtract, KeyCode.KKPPlus => EKey.NAdd,
         KeyCode.KKPEnter => EKey.NEnter, KeyCode.KKPPeriod => EKey.NPeriod,

         KeyCode.KBackquote => EKey.Tilde,
         KeyCode.KMinus => EKey.Hyphen, KeyCode.KEquals => EKey.Equals,
         KeyCode.KLeftbracket => EKey.OpenBracket, KeyCode.KRightbracket => EKey.CloseBracket,
         KeyCode.KBackslash => EKey.Backslash,
         KeyCode.KSemicolon => EKey.Semicolon, KeyCode.KQuote => EKey.Quote,

         _ => 0
      };
   }

   // Map SDL key modifier flags to Nori EKeyModifier
   static EKeyModifier MapModifiers (Keymod mod) {
      EKeyModifier result = EKeyModifier.None;
      if ((mod & Keymod.Shift) != 0) result |= EKeyModifier.Shift;
      if ((mod & Keymod.Ctrl) != 0) result |= EKeyModifier.Control;
      if ((mod & Keymod.Alt) != 0) result |= EKeyModifier.Alt;
      return result;
   }

   // Private data -------------------------------------------------------------
   Sdl mSdl;
   Subject<KeyInfo> mKeys = new ();
   Subject<MouseClickInfo> mMouseClicks = new ();
   Subject<Vec2S> mMouseMoves = new ();
   Subject<MouseWheelInfo> mMouseWheel = new ();
   Subject<int> mMouseLeave = new ();
   Subject<int> mMouseLost = new ();
}
#endregion
