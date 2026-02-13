// ────── ╔╗                                                                               PLATFORM
// ╔═╦╦═╦╦╬╣ IPlatform.cs
// ║║║║╬║╔╣║ Top-level facade interface for platform abstraction
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IPlatform -------------------------------------------------------------------------
/// <summary>Top-level facade providing access to platform-specific services</summary>
public interface IPlatform {
   /// <summary>Create a renderable surface (window) with the given title and dimensions</summary>
   ISurface CreateSurface (string title, int width, int height);
   /// <summary>Hardware input abstraction</summary>
   IInput Input { get; }
   /// <summary>Font loading abstraction</summary>
   IFontLoader FontLoader { get; }
   /// <summary>Run the platform event loop, calling onFrame each frame with the elapsed time</summary>
   void Run (Action<double> onFrame);
}
#endregion
