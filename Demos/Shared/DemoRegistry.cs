// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ DemoRegistry.cs
// ║║║║╬║╔╣║ Central registry of all available demo scenes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DemoRegistry -------------------------------------------------------------------------
/// <summary>Central registry of all available demo scenes</summary>
public static class DemoRegistry {
   /// <summary>All registered demo scenes with display names and factory functions</summary>
   public static readonly (string Name, Func<Scene> Factory)[] Scenes = [
      ("Polygon Fill", () => new LeafDemoScene ()),
      ("Line Fonts", () => new LineFontScene ()),
      ("TrueType Text", () => new TrueTypeScene ()),
      ("Load TMesh", () => new MeshScene ()),
      ("Tessellation", () => new MeshScene (true)),
      ("Poly Boolean", () => new BooleanScene ()),
      ("Load DXF", () => new DwgScene ()),
      ("Robot IK/FK", () => new RobotScene ()),
      ("Load STEP", () => new STPScene ()),
      ("Streaming", () => new StreamDemoScene ()),
      ("AABB Tree", () => new AABBTreeDemo ()),
      ("Min. Sphere", () => new MinSphereScene ()),
      ("Load T3X File", () => new T3XDemoScene ()),
      ("Slice Mesh", () => new IntMeshPlaneScene ()),
      ("Convex Hull", () => new ConvexHullScene ()),
      ("Build OBB", () => new BuildOBBScene ()),
   ];
}
#endregion
