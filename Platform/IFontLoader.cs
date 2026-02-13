// ────── ╔╗                                                                               PLATFORM
// ╔═╦╦═╦╦╬╣ IFontLoader.cs
// ║║║║╬║╔╣║ Interface abstracting font loading for cross-platform use
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IFontLoader ----------------------------------------------------------------------
/// <summary>Abstracts font loading (e.g. FreeType) in a platform-independent manner</summary>
public interface IFontLoader {
   /// <summary>Load a font face from a file path</summary>
   IntPtr LoadFace (string path, int faceIndex);
   /// <summary>Load a font face from a byte array</summary>
   IntPtr LoadFace (byte[] data, int faceIndex);
}
#endregion
