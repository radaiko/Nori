// ────── ╔╗                                                                            PLATFORM.IOS
// ╔═╦╦═╦╦╬╣ iOSFontLoader.cs
// ║║║║╬║╔╣║ iOS font loader using FreeType P/Invoke for loading font faces
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
namespace Nori;

#region class iOSFontLoader ---------------------------------------------------------------------------
/// <summary>iOS IFontLoader implementation delegating to a bundled FreeType native library</summary>
class iOSFontLoader : IFontLoader {
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

   static iOSFontLoader () {
      NativeLibrary.SetDllImportResolver (typeof (iOSFontLoader).Assembly, ResolveLibrary);
   }

   // Resolve the FreeType native library for iOS
   // On iOS, native libraries are typically bundled as static libraries linked
   // into the main executable (use __Internal), or as dynamic frameworks.
   static IntPtr ResolveLibrary (string name, Assembly assembly, DllImportSearchPath? path) {
      if (name != DLL) return IntPtr.Zero;
      // Try the app bundle's Frameworks directory first (dynamic framework)
      string bundlePath = NSBundle.MainBundle.BundlePath;
      string frameworkPath = $"{bundlePath}/Frameworks/libfreetype.framework/libfreetype";
      if (NativeLibrary.TryLoad (frameworkPath, out IntPtr handle)) return handle;
      // Try a bare dylib in the bundle
      string dylibPath = $"{bundlePath}/libfreetype.dylib";
      if (NativeLibrary.TryLoad (dylibPath, out IntPtr handle2)) return handle2;
      // Try __Internal (statically linked into the main executable)
      if (NativeLibrary.TryLoad ("__Internal", assembly, path, out IntPtr handle3)) return handle3;
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
