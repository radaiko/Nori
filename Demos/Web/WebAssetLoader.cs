// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ WebAssetLoader.cs
// ║║║║╬║╔╣║ Pre-fetches data assets via HTTP and writes them to the WASM virtual filesystem
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
namespace Nori;

#region class WebAssetLoader -----------------------------------------------------------------------
/// <summary>Fetches bundled data assets from the web server and writes them to the WASM vfs</summary>
/// In Blazor WASM, System.IO.File works against Emscripten's in-memory filesystem. This class
/// fetches assets from wwwroot/data/ via HTTP during async initialization, then writes them
/// to the vfs so that demo code using File.ReadAllLines etc. works unchanged.
static class WebAssetLoader {
   /// <summary>The root path in the WASM vfs where assets are stored</summary>
   public const string VfsRoot = "/data";

   /// <summary>Pre-fetch all needed assets and write them to the WASM virtual filesystem</summary>
   public static async Task LoadAsync (HttpClient http) {
      List<Task> tasks = [];
      foreach (string path in sAssets)
         tasks.Add (FetchAndWrite (http, path));
      await Task.WhenAll (tasks);
   }

   // Implementation -----------------------------------------------------------
   static async Task FetchAndWrite (HttpClient http, string relativePath) {
      string vfsPath = $"{VfsRoot}/{relativePath}";
      string? dir = Path.GetDirectoryName (vfsPath);
      if (dir != null) Directory.CreateDirectory (dir);
      try {
         byte[] data = await http.GetByteArrayAsync ($"data/{relativePath}");
         File.WriteAllBytes (vfsPath, data);
      } catch {
         // Asset missing or fetch failed — demo will handle gracefully
      }
   }

   // The list of all assets to pre-fetch. Organized by category.
   static readonly string[] sAssets = [
      // WAD: Fonts
      "Wad/GL/Fonts/Roboto-Regular.ttf",
      "Wad/GL/Fonts/RobotoMono-Regular.ttf",
      // WAD: Line fonts
      "Wad/DXF/simplex.lfont",
      "Wad/DXF/bold.lfont",
      "Wad/DXF/complex.lfont",
      "Wad/DXF/default.lfont",
      "Wad/DXF/iso.lfont",
      "Wad/DXF/isoprop.lfont",
      "Wad/DXF/italic.lfont",
      "Wad/DXF/monotxt.lfont",
      "Wad/DXF/romans.lfont",
      "Wad/DXF/txt.lfont",
      // WAD: DXF data
      "Wad/DXF/color.txt",
      // WAD: STEP data
      "Wad/Core/STEPIgnore.txt",
      // WAD: Serialization
      "Wad/AuManifest.txt",
      // WAD: Robot mechanism
      "Wad/FanucX/mechanism.curl",
      "Wad/FanucX/Model/Base.mesh",
      "Wad/FanucX/Model/S.mesh",
      "Wad/FanucX/Model/L.mesh",
      "Wad/FanucX/Model/U.mesh",
      "Wad/FanucX/Model/R.mesh",
      "Wad/FanucX/Model/B.mesh",
      "Wad/FanucX/Model/T.mesh",
      // Demo data: Polygon Fill
      "TData/Misc/LeafDwg.txt",
      // Demo data: AABB Tree
      "TData/IO/MESH/cow.zip",
      // Demo data: Build OBB
      "TData/IO/T3X/5X-022.t3x",
      // Demo data: Load STEP
      "TData/Step/S00178.stp",
      // Demo data: T3X demos
      "Demos/Data/5x-043-blank.t3x",
      "Demos/Data/5x-043.t3x",
      "Demos/Data/5x-024-blank.t3x",
   ];
}
#endregion
