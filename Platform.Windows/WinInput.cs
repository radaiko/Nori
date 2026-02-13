// ────── ╔╗                                                                       PLATFORM.WINDOWS
// ╔═╦╦═╦╦╬╣ WinInput.cs
// ║║║║╬║╔╣║ Windows implementation of IInput using EventWrapper pattern from HWEvent.cs
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Nori;

#region class EventWrapper<T> ----------------------------------------------------------------------
/// <summary>Base class converting Windows Forms events to IObservable streams</summary>
/// Derived classes implement Connect to attach/detach event handlers. When the
/// event fires, the derived class calls Push(T) and this distributes to all observers.
abstract class EventWrapper<T> : IObservable<T> {
   // Methods ------------------------------------------------------------------
   /// <summary>Subscribe an observer; connects the underlying event on first subscription</summary>
   public IDisposable Subscribe (IObserver<T> observer) {
      (mObservers ??= []).Add (observer);
      if (mObservers.Count == 1) Connect (true);
      return new Disposer (this, observer);
   }
   List<IObserver<T>>? mObservers;

   // Implementation -----------------------------------------------------------
   // Must be implemented by derived class to actually connect / disconnect from the event
   protected abstract void Connect (bool connect);

   // Push an item to all observers (most-recent subscriber first)
   protected void Push (T item) {
      if (mObservers == null) return;
      for (int i = mObservers.Count - 1; i >= 0; i--)
         mObservers[i].OnNext (item);
   }

   // Remove an observer; disconnects the event handler when the last one leaves
   void Remove (IObserver<T> observer) {
      if (mObservers?.Count > 0) {
         mObservers.Remove (observer);
         if (mObservers.Count == 0) Connect (false);
      }
   }

   // Nested types -------------------------------------------------------------
   // IDisposable that removes this observer from its owner
   class Disposer (EventWrapper<T> owner, IObserver<T> observer) : IDisposable {
      public void Dispose () => owner.Remove (observer);
   }
}
#endregion

#region class WinInput -----------------------------------------------------------------------------
/// <summary>Windows implementation of IInput providing keyboard and mouse event streams</summary>
class WinInput : IInput {
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
   public bool IsShiftDown => (GetKeyState (VK_SHIFT) & PRESSED) != 0;

   /// <summary>Is the CONTROL key currently pressed?</summary>
   public bool IsCtrlDown => (GetKeyState (VK_CONTROL) & PRESSED) != 0;

   /// <summary>Is the ALT key currently pressed?</summary>
   public bool IsAltDown => (GetKeyState (VK_ALT) & PRESSED) != 0;

   // Methods ------------------------------------------------------------------
   /// <summary>Capture or release the mouse, returns true if successful</summary>
   public bool CaptureMouse (bool capture) {
      if (mPanel == null) return false;
      mPanel.Capture = capture;
      return mPanel.Capture;
   }

   /// <summary>Attach event handlers to the given panel</summary>
   public void SetPanel (UserControl? panel) {
      mPanel = panel;
      mKeys.Panel = panel;
      mMouseClicks.Panel = panel;
      mMouseMoves.Panel = panel;
      mMouseWheel.Panel = panel;
      mMouseLeave.Panel = panel;
      mMouseLost.Panel = panel;
   }

   // Implementation -----------------------------------------------------------
   const int PRESSED = 0x8000;
   const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_ALT = 0x12;
   [DllImport ("user32.dll")]
   static extern ushort GetKeyState (int key);

   // Private data -------------------------------------------------------------
   UserControl? mPanel;
   KeysWrap mKeys = new ();
   MouseClicksWrap mMouseClicks = new ();
   MouseMovesWrap mMouseMoves = new ();
   MouseWheelWrap mMouseWheel = new ();
   MouseLeaveWrap mMouseLeave = new ();
   CaptureLostWrap mMouseLost = new ();
}
#endregion

#region class PanelEventWrapper<T> -----------------------------------------------------------------
/// <summary>EventWrapper that holds a reference to the target panel</summary>
abstract class PanelEventWrapper<T> : EventWrapper<T> {
   /// <summary>The panel to attach/detach event handlers from</summary>
   public UserControl? Panel { get; set; }
}
#endregion

#region class CaptureLostWrap ----------------------------------------------------------------------
/// <summary>EventWrapper for the mouse-capture-lost event</summary>
class CaptureLostWrap : PanelEventWrapper<int> {
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) panel.MouseCaptureChanged += OnCaptureLost;
      else panel.MouseCaptureChanged -= OnCaptureLost;
   }

   void OnCaptureLost (object? sender, EventArgs e)
      => Push (0);
}
#endregion

#region class MouseLeaveWrap -----------------------------------------------------------------------
/// <summary>EventWrapper for the mouse-leave event</summary>
class MouseLeaveWrap : PanelEventWrapper<int> {
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) panel.MouseLeave += OnMouseLeave;
      else panel.MouseLeave -= OnMouseLeave;
   }

   void OnMouseLeave (object? sender, EventArgs e)
      => Push (0);
}
#endregion

#region class KeysWrap -----------------------------------------------------------------------------
/// <summary>EventWrapper for key-down and key-up events, maps Windows Keys to Nori EKey</summary>
class KeysWrap : PanelEventWrapper<KeyInfo> {
   // Overrides ----------------------------------------------------------------
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) { panel.KeyDown += OnKeyDown; panel.KeyUp += OnKeyUp; }
      else { panel.KeyDown -= OnKeyDown; panel.KeyUp -= OnKeyUp; }
   }

   // Implementation -----------------------------------------------------------
   void OnKeyDown (object? _, KeyEventArgs e) => Process (e, EKeyState.Pressed);
   void OnKeyUp (object? _, KeyEventArgs e) => Process (e, EKeyState.Released);

   // Convert Windows Keys to Nori EKey and push a KeyInfo
   void Process (KeyEventArgs e, EKeyState state) {
      if (!sMap.TryGetValue (e.KeyCode, out EKey key)) key = (EKey)e.KeyCode;
      EKeyModifier mods = EKeyModifier.None;
      if ((e.Modifiers & System.Windows.Forms.Keys.Shift) > 0) mods |= EKeyModifier.Shift;
      if ((e.Modifiers & System.Windows.Forms.Keys.Control) > 0) mods |= EKeyModifier.Control;
      if ((e.Modifiers & System.Windows.Forms.Keys.Alt) > 0) mods |= EKeyModifier.Alt;
      Push (new (key, mods, state));
   }

   // Map Windows.Forms.Keys to Nori.EKey (unmapped keys share the same numeric value)
   static readonly Dictionary<System.Windows.Forms.Keys, EKey> sMap = new () {
      [System.Windows.Forms.Keys.Escape] = EKey.Escape,
      [System.Windows.Forms.Keys.F1] = EKey.F1, [System.Windows.Forms.Keys.F2] = EKey.F2,
      [System.Windows.Forms.Keys.F3] = EKey.F3, [System.Windows.Forms.Keys.F4] = EKey.F4,
      [System.Windows.Forms.Keys.F5] = EKey.F5, [System.Windows.Forms.Keys.F6] = EKey.F6,
      [System.Windows.Forms.Keys.F7] = EKey.F7, [System.Windows.Forms.Keys.F8] = EKey.F8,
      [System.Windows.Forms.Keys.F9] = EKey.F9, [System.Windows.Forms.Keys.F10] = EKey.F10,
      [System.Windows.Forms.Keys.F11] = EKey.F11, [System.Windows.Forms.Keys.F12] = EKey.F12,
      [System.Windows.Forms.Keys.Scroll] = EKey.Scroll,
      [System.Windows.Forms.Keys.Oemtilde] = EKey.Tilde,
      [System.Windows.Forms.Keys.OemMinus] = EKey.Hyphen,
      [System.Windows.Forms.Keys.Oemplus] = EKey.Equals,
      [System.Windows.Forms.Keys.OemOpenBrackets] = EKey.OpenBracket,
      [System.Windows.Forms.Keys.OemCloseBrackets] = EKey.CloseBracket,
      [System.Windows.Forms.Keys.OemPipe] = EKey.Backslash,
      [System.Windows.Forms.Keys.LWin] = EKey.Windows, [System.Windows.Forms.Keys.RWin] = EKey.Windows,
      [System.Windows.Forms.Keys.ControlKey] = EKey.Ctrl,
      [System.Windows.Forms.Keys.ShiftKey] = EKey.Shift,
      [System.Windows.Forms.Keys.Menu] = EKey.Alt,
      [System.Windows.Forms.Keys.Capital] = EKey.CapsLock,
      [System.Windows.Forms.Keys.Apps] = EKey.Menu,
      [System.Windows.Forms.Keys.Pause] = EKey.Pause,
      [System.Windows.Forms.Keys.Insert] = EKey.Insert,
      [System.Windows.Forms.Keys.Home] = EKey.Home,
      [System.Windows.Forms.Keys.PageUp] = EKey.PageUp,
      [System.Windows.Forms.Keys.PageDown] = EKey.PageDown,
      [System.Windows.Forms.Keys.Delete] = EKey.Delete,
      [System.Windows.Forms.Keys.End] = EKey.End,
      [System.Windows.Forms.Keys.Up] = EKey.Up, [System.Windows.Forms.Keys.Down] = EKey.Down,
      [System.Windows.Forms.Keys.Left] = EKey.Left, [System.Windows.Forms.Keys.Right] = EKey.Right,
      [System.Windows.Forms.Keys.NumLock] = EKey.NumLock,
      [System.Windows.Forms.Keys.Divide] = EKey.NDivide,
      [System.Windows.Forms.Keys.Multiply] = EKey.NMultiply,
      [System.Windows.Forms.Keys.Subtract] = EKey.NSubtract,
      [System.Windows.Forms.Keys.Add] = EKey.NAdd,
      [System.Windows.Forms.Keys.Decimal] = EKey.NPeriod,
      [System.Windows.Forms.Keys.NumPad0] = EKey.NPad0, [System.Windows.Forms.Keys.NumPad1] = EKey.NPad1,
      [System.Windows.Forms.Keys.NumPad2] = EKey.NPad2, [System.Windows.Forms.Keys.NumPad3] = EKey.NPad3,
      [System.Windows.Forms.Keys.NumPad4] = EKey.NPad4, [System.Windows.Forms.Keys.Clear] = EKey.NPad5,
      [System.Windows.Forms.Keys.NumPad6] = EKey.NPad6, [System.Windows.Forms.Keys.NumPad7] = EKey.NPad7,
      [System.Windows.Forms.Keys.NumPad8] = EKey.NPad8, [System.Windows.Forms.Keys.NumPad9] = EKey.NPad9,
      [System.Windows.Forms.Keys.Space] = EKey.Space
   };
}
#endregion

#region class MouseClicksWrap ----------------------------------------------------------------------
/// <summary>EventWrapper for mouse button press and release events</summary>
class MouseClicksWrap : PanelEventWrapper<MouseClickInfo> {
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) { panel.MouseDown += OnMouseDown; panel.MouseUp += OnMouseUp; }
      else { panel.MouseDown -= OnMouseDown; panel.MouseUp -= OnMouseUp; }
   }

   void OnMouseDown (object? sender, MouseEventArgs e) => Process (e, EKeyState.Pressed);
   void OnMouseUp (object? sender, MouseEventArgs e) => Process (e, EKeyState.Released);

   void Process (MouseEventArgs e, EKeyState state) {
      if (!sMap.TryGetValue (e.Button, out EMouseButton btn)) return;
      EKeyModifier mods = EKeyModifier.None;
      if ((GetKeyState (VK_CONTROL) & PRESSED) != 0) mods |= EKeyModifier.Control;
      if ((GetKeyState (VK_SHIFT) & PRESSED) != 0) mods |= EKeyModifier.Shift;
      if ((GetKeyState (VK_ALT) & PRESSED) != 0) mods |= EKeyModifier.Alt;
      Vec2S position = new (e.X, e.Y);
      Push (new (btn, position, mods, state));
   }

   const int PRESSED = 0x8000;
   const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_ALT = 0x12;
   [DllImport ("user32.dll")]
   static extern ushort GetKeyState (int key);

   static readonly Dictionary<MouseButtons, EMouseButton> sMap = new () {
      [MouseButtons.Left] = EMouseButton.Left,
      [MouseButtons.Middle] = EMouseButton.Middle,
      [MouseButtons.Right] = EMouseButton.Right
   };
}
#endregion

#region class MouseMovesWrap -----------------------------------------------------------------------
/// <summary>EventWrapper for mouse move events</summary>
class MouseMovesWrap : PanelEventWrapper<Vec2S> {
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) panel.MouseMove += OnMouseMove;
      else panel.MouseMove -= OnMouseMove;
   }

   void OnMouseMove (object? sender, MouseEventArgs e)
      => Push (new (e.X, e.Y));
}
#endregion

#region class MouseWheelWrap -----------------------------------------------------------------------
/// <summary>EventWrapper for mouse wheel events</summary>
class MouseWheelWrap : PanelEventWrapper<MouseWheelInfo> {
   protected override void Connect (bool connect) {
      UserControl? panel = Panel; if (panel == null) return;
      if (connect) panel.MouseWheel += OnMouseWheel;
      else panel.MouseWheel -= OnMouseWheel;
   }

   void OnMouseWheel (object? sender, MouseEventArgs e)
      => Push (new (e.Delta, new (e.X, e.Y)));
}
#endregion
