// ────── ╔╗                                                                       PLATFORM.ANDROID
// ╔═╦╦═╦╦╬╣ DroidFontLoader.cs
// ║║║║╬║╔╣║ Android font loader using FreeType P/Invoke for loading font faces
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
using System.Runtime.InteropServices;
namespace Nori;

#region class DroidFontLoader ------------------------------------------------------------------------
/// <summary>Android IFontLoader implementation delegating to FreeType native library (libfreetype.so)</summary>
class DroidFontLoader : IFontLoader {
   // Methods ------------------------------------------------------------------
   /// <summary>Load a font face from a file path</summary>
   public IntPtr LoadFace (string path, int faceIndex) {
      int err = FT_New_Face (Library, path, faceIndex, out IntPtr face);
      if (err != 0) throw new Exception ($"FreeType error {err} loading face from '{path}'");
      return face;
   }

   /// <summary>Load a font face from a byte array</summary>
   public IntPtr LoadFace (byte[] data, int faceIndex) {
      // Pin the data so FreeType can read from it
      GCHandle pin = GCHandle.Alloc (data, GCHandleType.Pinned);
      int err = FT_New_Memory_Face (Library, pin.AddrOfPinnedObject (), data.Length, faceIndex, out IntPtr face);
      if (err != 0) {
         pin.Free ();
         throw new Exception ($"FreeType error {err} loading face from byte array");
      }
      // Keep the pin alive — the face references this memory
      (mPins ??= []).Add (pin);
      return face;
   }

   // Implementation -----------------------------------------------------------
   // Lazily initialize the FreeType library handle
   static IntPtr Library {
      get {
         if (sLibrary == IntPtr.Zero) {
            int err = FT_Init_FreeType (out sLibrary);
            if (err != 0) throw new Exception ($"FreeType error {err} during initialization");
         }
         return sLibrary;
      }
   }

   // P/Invoke declarations for FreeType
   const string DLL = "freetype";

   static DroidFontLoader () {
      NativeLibrary.SetDllImportResolver (typeof (DroidFontLoader).Assembly, ResolveLibrary);
   }

   // Resolve the FreeType native library for Android (libfreetype.so)
   static IntPtr ResolveLibrary (string name, Assembly assembly, DllImportSearchPath? path) {
      if (name != DLL) return IntPtr.Zero;
      if (NativeLibrary.TryLoad ("libfreetype.so", assembly, path, out IntPtr handle)) return handle;
      if (NativeLibrary.TryLoad ("libfreetype", assembly, path, out IntPtr handle2)) return handle2;
      return IntPtr.Zero;
   }

   [DllImport (DLL, EntryPoint = "FT_Init_FreeType")]
   static extern int FT_Init_FreeType (out IntPtr library);

   [DllImport (DLL, EntryPoint = "FT_New_Face", CharSet = CharSet.Ansi)]
   static extern int FT_New_Face (IntPtr library, string path, int faceIndex, out IntPtr face);

   [DllImport (DLL, EntryPoint = "FT_New_Memory_Face")]
   static extern int FT_New_Memory_Face (IntPtr library, IntPtr fileBase, int fileSize, int faceIndex, out IntPtr face);

   // Private data -------------------------------------------------------------
   static IntPtr sLibrary;
   List<GCHandle>? mPins;        // Prevent GC of pinned byte arrays
}
#endregion
