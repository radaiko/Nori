// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ AABBTreeDemo.cs
// ║║║║╬║╔╣║ Demo for creation of AABB hierarchy (used for collision checks)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.IO.Compression;
namespace Nori;

#region class AABBTreeDemo -------------------------------------------------------------------------
// This implements a demo scene for showing a BVH (bounding volume hierarchy) made up of
// AABBs (axis-aligned bounding boxes). We load a mesh from an OBJ file, and then create
// a BVH that drills down to the level of individual triangles with the
class AABBTreeDemo : Scene3 {
   public AABBTreeDemo () {
      ZipArchive zar = new (File.OpenRead ($"{Lib.DevRoot}/TData/IO/MESH/cow.zip"));
      ZipArchiveEntry ze = zar.GetEntry ("cow.obj")!;
      ZipReadStream zstm = new (ze.Open (), ze.Length);
      Mesh3 mesh = Mesh3.LoadObj (zstm.ReadAllLines ());
      mesh *= Matrix3.Rotation (EAxis.X, Lib.HalfPI) * Matrix3.Rotation (EAxis.Z, -Lib.HalfPI);
      CMesh cmesh = CMesh.Builder.Build (mesh);

      mCMeshVN = new CMeshVN (cmesh);
      MeshVN meshVN = new (mesh) {
         Shading = EShadeMode.Flat,
         Color = new Color4 (128, 128, 128)
      };
      Lib.Tracer = TraceVN.Print;
      TraceVN.It.Clear ();
      Root = new GroupVN ([meshVN, mCMeshVN, TraceVN.It]);
      BgrdColor = Color4.Gray (64);
      Bound = mesh.Bound;
      Viewpoint = new (-90, 90);
      Lib.Trace ("Right Click: Increase Level");
      Lib.Trace ("Shift+Right Click: Decrease Level");
   }

   CMeshVN mCMeshVN;
}
#endregion

#region class CMeshVN ------------------------------------------------------------------------------
// This VNode displays one level of the AABB hierarchy (by drawing boxes).
// This VNode also connects to the mouse click handler.
class CMeshVN (CMesh cm) : VNode {
   // Overrides ----------------------------------------------------------------
   public override void Draw () {
      List<Bound3> boxes = mCM.EnumBoxes (mLevel).ToList ();
      Lib.Trace ($"Level {mLevel}, {boxes.Count} boxes");
      List<Vec3F> pts = [];
      foreach (Bound3 box in boxes) {
         (Bound1 x, Bound1 y, Bound1 z) = (box.X, box.Y, box.Z);
         Vec3F a = new (x.Min, y.Min, z.Min), b = new (x.Max, y.Min, z.Min);
         Vec3F c = new (x.Max, y.Max, z.Min), d = new (x.Min, y.Max, z.Min);
         Vec3F e = new (x.Min, y.Min, z.Max), f = new (x.Max, y.Min, z.Max);
         Vec3F g = new (x.Max, y.Max, z.Max), h = new (x.Min, y.Max, z.Max);
         pts.AddRange ([a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h]);
      }
      Lux.Lines (pts.AsSpan ());
   }

   /// <summary>Increase the AABB hierarchy level by one</summary>
   public void LevelUp () { mLevel++; Redraw (); }

   /// <summary>Decrease the AABB hierarchy level by one</summary>
   public void LevelDown () { mLevel--; Redraw (); }

   public override void SetAttributes ()
      => (Lux.Color, Lux.LineWidth) = (Color4.White, 2f);

   // Private data -------------------------------------------------------------
   int mLevel = 5;
   readonly CMesh mCM = cm;
}
#endregion
