// ────── ╔╗                                                                               PLATFORM
// ╔═╦╦═╦╦╬╣ ISurface.cs
// ║║║║╬║╔╣║ Interface abstracting a renderable surface for cross-platform rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface ISurface --------------------------------------------------------------------------
/// <summary>Abstracts a renderable surface that hosts GPU rendering</summary>
public interface ISurface {
   /// <summary>Surface size in pixels</summary>
   Vec2S Size { get; }
   /// <summary>DPI scale factor (1.0 for standard, 2.0 for retina/HiDPI)</summary>
   double DPIScale { get; }
   /// <summary>Native surface handle for WebGPU surface creation</summary>
   IntPtr NativeHandle { get; }
   /// <summary>Request a redraw of the surface</summary>
   void Invalidate ();
   /// <summary>Show or hide the cursor over the surface</summary>
   bool CursorVisible { set; }
   /// <summary>Fired when the surface is resized</summary>
   IObservable<Vec2S> Resized { get; }
   /// <summary>Fired when the surface is ready for rendering</summary>
   IObservable<int> Ready { get; }
}
#endregion
