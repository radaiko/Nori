// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ IntMeshPlane.cs
// ║║║║╬║╔╣║ Demo for intersecting a mesh with multiple planes (mesh slicing)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class IntMeshPlaneScene --------------------------------------------------------------------
class IntMeshPlaneScene : Scene3 {
   public IntMeshPlaneScene () {
      Model3 model = new T3XReader ($"{Lib.DevRoot}/Demos/Data/5x-024-blank.t3x").Load ();

      List<Mesh3> meshes = [];
      List<Mesh3.Node> nodes = [];
      List<int> tris = [], wires = [];
      foreach (E3Surface ent in model.Ents.OfType<E3Surface> ()) {
         Mesh3 mesh = ent.Mesh;
         meshes.Add (mesh);
         int n = nodes.Count;
         nodes.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
         wires.AddRange (mesh.Wire.Select (a => a + n));
      }
      wires.Clear ();

      Lib.Tracer = TraceVN.Print;
      Mesh3 fullmesh = new ([.. nodes], [.. tris], [.. wires]);
      List<VNode> vnodes = [
         new MeshVN (fullmesh) { Color = Color4.White, Shading = EShadeMode.Glass },
         TraceVN.It
      ];
      Bound = fullmesh.Bound;

      AddIntersections (meshes, Bound, vnodes, 25);
      BgrdColor = new Color4 (32, 64, 96);
      Root = new GroupVN (vnodes);
   }

   // Implementation -----------------------------------------------------------
   void AddIntersections (IList<Mesh3> meshes, Bound3 bound, List<VNode> vnodes, int step) {
      using BlockTimer bt = new ("Compute Intersections");
      MeshSlicer pmi = new ([.. meshes]);
      List<Vec3F> ends = [];
      List<Polyline3> output = [];
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (bound.X);
         PlaneDef pdef = new (new (x, 0, 0), Vector3.XAxis);
         pmi.Compute (pdef, output);

         double y = (i / 100.0).Along (bound.Y);
         pdef = new (new (0, y, 0), Vector3.YAxis);
         pmi.Compute (pdef, output);

         double z = (i / 100.0).Along (bound.Z);
         pdef = new (new (0, 0, z), Vector3.ZAxis);
         pmi.Compute (pdef, output);
      }
      foreach (Polyline3 poly in output) {
         vnodes.Add (new Curve3VN (poly));
         if (!poly.Pts[0].EQ (poly.Pts[^1])) { ends.Add (poly.Start); ends.Add (poly.End); }
      }

      vnodes.Add (new SimpleVN (
         () => { Lux.PointSize = 7f; Lux.Color = Color4.Yellow; },
         () => Lux.Points (ends.AsSpan ())
      ));
   }
}
#endregion
