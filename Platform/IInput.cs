// ────── ╔╗                                                                               PLATFORM
// ╔═╦╦═╦╦╬╣ IInput.cs
// ║║║║╬║╔╣║ Interface abstracting hardware input (keyboard, mouse) for cross-platform use
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IInput ----------------------------------------------------------------------------
/// <summary>Abstracts hardware input events (keyboard, mouse) in a platform-independent manner</summary>
public interface IInput {
   /// <summary>Observable stream of keyboard events</summary>
   IObservable<KeyInfo> Keys { get; }
   /// <summary>Observable stream of mouse button click and release events</summary>
   IObservable<MouseClickInfo> MouseClicks { get; }
   /// <summary>Observable stream of mouse move events</summary>
   IObservable<Vec2S> MouseMoves { get; }
   /// <summary>Observable stream of mouse wheel events</summary>
   IObservable<MouseWheelInfo> MouseWheel { get; }
   /// <summary>Fired when the mouse leaves the client area</summary>
   IObservable<int> MouseLeave { get; }
   /// <summary>Fired when mouse capture is lost</summary>
   IObservable<int> MouseLost { get; }
   /// <summary>Capture or release the mouse, returns true if successful</summary>
   bool CaptureMouse (bool capture);
   /// <summary>Is the SHIFT key currently pressed?</summary>
   bool IsShiftDown { get; }
   /// <summary>Is the CONTROL key currently pressed?</summary>
   bool IsCtrlDown { get; }
   /// <summary>Is the ALT key currently pressed?</summary>
   bool IsAltDown { get; }
}
#endregion
