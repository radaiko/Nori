// ────── ╔╗                                                                                 DEMOS
// ╔═╦╦═╦╦╬╣ DemoScenes.cs
// ║║║║╬║╔╣║ Static builder methods that return SceneInitMsg for each demo scene
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
namespace Nori;

/// <summary>Static demo scene builders — each method returns a SceneInitMsg</summary>
public static class DemoScenes {
   /// <summary>Dispatch to a named demo builder</summary>
   public static SceneInitMsg Build (string name) => name switch {
      "dwg" => DwgDemo (),
      "linefont" => LineFontDemo (),
      "convexhull" => ConvexHullDemo (),
      "boolean" => BooleanDemo (),
      "leaf" => LeafDemo (),
      "mesh" => MeshDemo (),
      "tess" => TessDemo (),
      "mes" => MESDemo (),
      "aabbtree" => AABBTreeDemo (),
      "t3x" => T3XDemo (),
      "stp" => STPScene (),
      "obb" => BuildOBBDemo (),
      "meshslice" => IntMeshPlane (),
      _ => DwgDemo (),
   };

   // ═══════════════════════════════════════════════════════════════════════════════
   // 4a. DwgDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Drawing-entities demo (poly, circle, spline, block insert, solid, text, bendlines)</summary>
   static SceneInitMsg DwgDemo () {
      Dwg2 dwg = new ();
      Layer2 layer = dwg.CurrentLayer;

      List<Ent2> bSet = [];
      bSet.Add (new E2Poly (layer, Poly.Parse ("M-1,-1 V-3 H1 V-1 H3 V1 H1 V3 H-1 V1 H-3 V-1Z")));
      bSet.Add (new E2Point (layer, Point2.Zero));
      bSet.Add (new E2Poly (layer, Poly.Circle (Point2.Zero, 2)));
      Block2 b = new ("Cross", Point2.Zero, bSet);
      dwg.Add (b);

      Style2 s1 = new ("Std", "Simplex", 0, 1, 0);
      dwg.Add (s1);

      Point2[] pts = [new (80, 80), new (76, 58), new (70, 20), new (108, 78), new (102, 40), new (100, 20)];
      double[] knots = [0, 0, 0, 0, 20, 34, 54, 54, 54, 54];
      dwg.Add (new E2Spline (layer, new Spline2 ([.. pts], [.. knots], []), 0));

      dwg.Add (Poly.Parse ("M0,0 H200 V100 Q150,150,1 H0Z"));
      dwg.Add (Poly.Circle (new (150, 100), 20));
      dwg.Add (new E2Insert (dwg, layer, "Cross", new Point2 (15, 15), 45.D2R (), 4, 3));
      dwg.Add (new E2Solid (layer, Point2.List (30, 30, 40, 30, 40, 35, 30, 35)));
      dwg.Add (new E2Text (layer, s1, "Hello, World!", new Point2 (50, 20), 5, 0, 0, 1, ETextAlign.BaseLeft));

      dwg.Add (new E2Bendline (dwg, Point2.List (200, 75, 125, 0), Lib.HalfPI, 2, 0.42, 1));
      dwg.Add (new E2Bendline (dwg, Point2.List (200, 55, 145, 0), -Lib.HalfPI, 2, 0.42, 1));

      Bound2 bound = dwg.Bound.InflatedF (1.2);
      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [200, 200, 206, 255],
         Bounds = Bounds2D (bound),
         Transforms = IdentityTransform (),
         Entities = RenderCapture.CaptureDwg2 (dwg),
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 4b. LineFontDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Line-font text rendering with various alignments, oblique, x-stretch</summary>
   static SceneInitMsg LineFontDemo () {
      List<Poly> polys = [];
      List<Point2> refPts = [];
      LineFont lf = LineFont.Get ("simplex");

      refPts.AddRange ([new (0, 0), new (0, 5), new (0, 10), new (0, 15)]);
      Out (0, 0, ETextAlign.BotLeft);
      Out (0, 5, ETextAlign.BaseLeft);
      Out (0, 10, ETextAlign.MidLeft);
      Out (0, 15, ETextAlign.TopLeft);

      refPts.AddRange ([new (15, 0), new (15, 10), new (15, 22), new (15, 34), new (34, 34)]);
      Out2 (15, 0, ETextAlign.BotLeft);
      Out2 (15, 10, ETextAlign.BaseLeft);
      Out2 (15, 22, ETextAlign.MidLeft);
      Out2 (15, 34, ETextAlign.TopLeft);
      Out2 (34, 34, ETextAlign.TopRight);

      refPts.AddRange ([new (8, 20), new (8, 25), new (8, 30), new (0, 17)]);
      Out3 (8, 20, ETextAlign.BaseLeft);
      Out3 (8, 25, ETextAlign.BaseCenter);
      Out3 (8, 30, ETextAlign.BaseRight);
      lf.Render ("TRIPE", new (0, 17), ETextAlign.BaseLeft, 15.D2R (), 1, 2, 0, polys);

      polys.Add (Poly.Line (-1, 5, 9, 5));
      polys.Add (Poly.Line (-1, 7, 9, 7));

      refPts.AddRange ([new (30, 0), new (33, 17), new (30, 22), new (43, 0), new (58, 0), new (52, 14), new (47, 21), new (59, 30)]);
      Out4 (30, 0, ETextAlign.BaseLeft);
      Out4 (33, 17, ETextAlign.TopRight);
      Out4 (30, 22, ETextAlign.MidCenter);
      lf.Render ("ELONGATE", new (43, 0), ETextAlign.BaseLeft, 15.D2R (), 1.5, 3, 90.D2R (), polys);

      lf.Render ("Sub\nSaharan\nAntarctica", new (58, 0), ETextAlign.BaseRight, 0, 1, 1.5, 0, polys);
      lf.Render ("Sub\nSaharan\nAntarctica", new (52, 14), ETextAlign.MidCenter, 0, 1, 1.5, 0, polys);
      lf.Render ("Sub\nSaharan\nAntarctica", new (47, 21), ETextAlign.BaseLeft, 30.D2R (), 1, 1.5, 0, polys);
      lf.Render ("Reversed", new Point2 (59, 30), ETextAlign.BaseLeft, 0, -0.5, 4, 0, polys);

      Bound2 bound = new Bound2 (polys.Select (a => a.GetBound ())).InflatedF (1.1);

      // Capture text outlines as Lines2D + Beziers2D
      List<RenderPrimitive> textPrims = [];
      CapturePolys (polys, [0, 0, 0, 255], textPrims);

      // Capture reference points as Points2D
      List<float> ptData = [];
      foreach (Point2 pt in refPts) { ptData.Add ((float)pt.X); ptData.Add ((float)pt.Y); }
      RenderPrimitive pointsPrim = new () {
         Type = EPrimType.Points2D,
         Data = ptData.ToArray (),
         Color = [0, 0, 0, 255],
         PointSize = 4f,
      };

      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [216, 216, 216, 255],
         Bounds = Bounds2D (bound),
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg { Id = 0, Primitives = textPrims.ToArray () },
            new EntityDataMsg { Id = 1, Primitives = [pointsPrim] },
         ],
      };

      // Local helpers matching the shared demo's local functions
      void Out (double x, double y, ETextAlign align)
         => lf.Render ("Cray{}", new (x, y), align, 0, 1, 2, 0, polys);
      void Out2 (double x, double y, ETextAlign align)
         => lf.Render ("A()\nCray{}\n[123]", new (x, y), align, 0, 1, 1.5, 0, polys);
      void Out3 (double x, double y, ETextAlign align)
         => lf.Render ("MAX", new (x, y), align, 0, 0.5, 3, 0, polys);
      void Out4 (double x, double y, ETextAlign align)
         => lf.Render ("Hello\nWorld", new (x, y), align, 0, 1, 2, 30.D2R (), polys);
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 4c. ConvexHullDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Static convex hull of 3000 random points</summary>
   static SceneInitMsg ConvexHullDemo () {
      Random rng = new (42);
      int count = 3000;
      List<Point2> pts = [];
      for (int i = 0; i < count; i++) {
         double x = rng.NextDouble () * 1400 - 700;
         double y = rng.NextDouble () * 1000 - 500;
         pts.Add (new (x, y));
      }
      List<Point2> hull = ConvexHull.Compute (pts);

      // Pack points as Points2D
      List<float> ptData = [];
      foreach (Point2 pt in pts) { ptData.Add ((float)pt.X); ptData.Add ((float)pt.Y); }
      RenderPrimitive pointsPrim = new () {
         Type = EPrimType.Points2D,
         Data = ptData.ToArray (),
         Color = [255, 255, 255, 255],
         PointSize = 6f,
      };

      // Pack hull as Lines2D (line loop — connect consecutive + close)
      List<float> hullData = [];
      for (int i = 0; i < hull.Count; i++) {
         Point2 a = hull[i], b = hull[(i + 1) % hull.Count];
         hullData.Add ((float)a.X); hullData.Add ((float)a.Y);
         hullData.Add ((float)b.X); hullData.Add ((float)b.Y);
      }
      RenderPrimitive hullPrim = new () {
         Type = EPrimType.Lines2D,
         Data = hullData.ToArray (),
         Color = [255, 255, 255, 255],
         LineWidth = 1.5f,
      };

      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [40, 40, 40, 255],
         Bounds = [-700, -500, 700, 500],
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg { Id = 0, Primitives = [pointsPrim] },
            new EntityDataMsg { Id = 1, Primitives = [hullPrim] },
         ],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 4d. BooleanDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Boolean operations demo — shows input polys in 4 quadrants (ops not available cross-platform)</summary>
   static SceneInitMsg BooleanDemo () {
      // Build the same 6 poly sets as the shared version
      List<List<Poly>> polySets = [
         [Poly.Polygon ((300, 350), 300, 3),
         Poly.Polygon ((300, 350), 300, 3, Lib.PI)],

         [Poly.Circle ((325, 1250), 200),
         Poly.Circle ((200, 1000), 200),
         Poly.Circle ((425, 1000), 200)],

         [Poly.Rectangle (850, 50, 1350, 550),
         Poly.Circle ((1100, 300), 275)],

         [Poly.Circle ((1150, 1050), 250),
         Poly.Rectangle (850, 950, 1450, 1150)],

         [Poly.Rectangle (1700, 50, 2150, 650),
         Poly.Rectangle (1850, 150, 2300, 550)],

         [Poly.Parse ("M1700,850H2500V1250Q2300,1450,-1H1900Q1700,1250,1Z"),
         Poly.Circle ((2000, 1150), 180)],
      ];

      // Compute base bound from all polys
      Bound2 baseBound = new (polySets.SelectMany (a => a).Select (a => a.GetBound ()));
      // Extend to include 2x the max (for the 4-quadrant layout)
      baseBound += new Point2 (2 * baseBound.X.Max, 2 * baseBound.Y.Max);
      Bound2 sceneBound = baseBound.InflatedF (1.05);
      Point2 mid = baseBound.Midpoint;

      // Flatten all input polys
      List<Poly> allPolys = polySets.SelectMany (a => a).ToList ();

      // Quadrant offsets: TopLeft=Polys, TopRight=Union, BotLeft=Intersection, BotRight=Subtraction
      // In the shared version, the quadrant offsets depend on viewport. For the static version,
      // we use simple offsets based on bound halves.
      double dx = baseBound.Width * 0.02 / 2;
      double dy = baseBound.Height * 0.02 / 2;
      (double, double)[] quadOffsets = [
         (-dx, -dy),  // Polys (top-left)
         (dx, -dy),   // Union (top-right) — same polys since no boolean ops
         (-dx, dy),   // Intersection (bottom-left)
         (dx, dy),    // Subtraction (bottom-right)
      ];
      double halfW = baseBound.Width / 2, halfH = baseBound.Height / 2;
      (double, double)[] quadShifts = [
         (0, 0),
         (halfW, 0),
         (0, halfH),
         (halfW, halfH),
      ];

      List<EntityDataMsg> entities = [];
      int entId = 0;
      string[] labels = ["Polys", "Union", "Intersection", "Subtraction"];
      byte[][] colors = [
         [96, 96, 96, 255],     // Polys quadrant — gray
         [96, 96, 192, 255],    // Union — blue-ish
         [96, 96, 192, 255],    // Intersection — blue-ish
         [96, 96, 192, 255],    // Subtraction — blue-ish
      ];

      LineFont lf = LineFont.Get ("simplex");
      for (int q = 0; q < 4; q++) {
         List<RenderPrimitive> prims = [];
         (double offX, double offY) = (quadOffsets[q].Item1 + quadShifts[q].Item1,
                                        quadOffsets[q].Item2 + quadShifts[q].Item2);

         // Translate each poly and capture as lines
         List<Poly> translated = [];
         foreach (Poly poly in allPolys)
            translated.Add (poly * Matrix2.Translation (offX, offY));
         CapturePolys (translated, colors[q], prims);

         entities.Add (new EntityDataMsg { Id = entId++, Primitives = prims.ToArray () });
      }

      // Dividing lines (vertical and horizontal through midpoint)
      List<float> divData = [
         (float)mid.X, (float)sceneBound.Y.Min, (float)mid.X, (float)sceneBound.Y.Max,
         (float)sceneBound.X.Min, (float)mid.Y, (float)sceneBound.X.Max, (float)mid.Y,
      ];
      RenderPrimitive divPrim = new () {
         Type = EPrimType.Lines2D,
         Data = divData.ToArray (),
         Color = [150, 150, 150, 255],
         LineWidth = 2f,
      };
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [divPrim] });

      // Labels rendered as line-font text
      List<RenderPrimitive> labelPrims = [];
      List<Poly> labelPolys = [];
      double labelSize = baseBound.Width * 0.015;
      lf.Render ("Polys", new (mid.X - 10, mid.Y - 10), ETextAlign.TopRight, 0, 1, labelSize, 0, labelPolys);
      lf.Render ("Union", new (mid.X + 10, mid.Y - 10), ETextAlign.TopLeft, 0, 1, labelSize, 0, labelPolys);
      lf.Render ("Intersection", new (mid.X - 10, mid.Y + 10), ETextAlign.BotRight, 0, 1, labelSize, 0, labelPolys);
      lf.Render ("Subtraction", new (mid.X + 10, mid.Y + 10), ETextAlign.BotLeft, 0, 1, labelSize, 0, labelPolys);
      CapturePolys (labelPolys, [100, 100, 100, 255], labelPrims);
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = labelPrims.ToArray () });

      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [216, 216, 216, 255],
         Bounds = Bounds2D (sceneBound),
         Transforms = IdentityTransform (),
         Entities = entities.ToArray (),
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 4e. LeafDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Leaf contour fill demo — stencil-and-cover fill of a complex leaf shape</summary>
   static SceneInitMsg LeafDemo () {
      string[] lines = File.ReadAllLines ($"{Lib.DevRoot}/TData/Misc/LeafDwg.txt");
      int n = 0, cPoly = lines[n++].ToInt ();

      List<int> indices = [];
      List<Vec2F> pts = [new (0, 0)];
      List<Vec2F> trace = [];
      Bound2 b = new ();

      for (int i = 0; i < cPoly; i++) {
         int cVerts = lines[n++].ToInt ();
         indices.Add (0);
         int idx0 = pts.Count;
         for (int j = 0; j < cVerts; j++) {
            indices.Add (pts.Count);
            double[] v = [.. lines[n++].Split (',').Select (double.Parse)];
            Point2 pt = new (v[0], v[1]);
            b += pt; pts.Add (pt);
            trace.Add (pt); if (j > 0) trace.Add (pt);
         }
         trace.Add (pts[idx0]);
         indices.AddRange ([idx0, -1]);
      }
      pts[0] = b.Midpoint;
      Bound2 sceneBound = b.InflatedF (1.1);

      // Pack trace as Lines2D (black outlines)
      List<float> traceData = [];
      foreach (Vec2F pt in trace) { traceData.Add (pt.X); traceData.Add (pt.Y); }
      RenderPrimitive tracePrim = new () {
         Type = EPrimType.Lines2D,
         Data = traceData.ToArray (),
         Color = [0, 0, 0, 255],
         LineWidth = 2f,
      };

      // Pack fill as Fill2D
      List<float> fillData = [];
      foreach (Vec2F pt in pts) { fillData.Add (pt.X); fillData.Add (pt.Y); }
      RenderPrimitive fillPrim = new () {
         Type = EPrimType.Fill2D,
         Data = fillData.ToArray (),
         Indices = indices.ToArray (),
         Color = [192, 255, 192, 255],
         ZLevel = -10,
         BoundData = [(float)b.X.Min, (float)b.Y.Min, (float)b.X.Max, (float)b.Y.Max],
      };

      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [160, 160, 160, 255],
         Bounds = Bounds2D (sceneBound),
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg { Id = 0, Primitives = [tracePrim] },
            new EntityDataMsg { Id = 1, Primitives = [fillPrim] },
         ],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 5a. MeshDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>3D mesh demo — loads a Flux mesh with Phong shading</summary>
   static SceneInitMsg MeshDemo () {
      Mesh3 mesh = Mesh3.LoadFluxMesh ($"{Lib.DevRoot}/Wad/FanucX/Model/R.mesh");
      Bound3 bound = mesh.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [96, 96, 96, 255],
         Bounds = Bounds3D (bound),
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg {
               Id = 0,
               Primitives = [RenderCapture.CaptureMesh (mesh, [255, 255, 128, 255], shadeMode: 1)],
            },
         ],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 5b. TessDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Tessellation demo — thick polygon with holes via tessellation</summary>
   static SceneInitMsg TessDemo () {
      const double thk = 10;
      List<Poly> polys = [
         Poly.Parse ("M0,0H500V200Q400,300,-1H100Q0,200,1Z"),
         Poly.Circle ((80, 80), 60),
         Poly.Circle ((450, 70), 20),
         Poly.Circle ((250, 120), 20),
         Poly.Rectangle (160, 160, 180, 180),
         Poly.Rectangle (170, 20, 280, 40),
         Poly.Polygon ((350, 150), 30, 6),
         Poly.Polygon ((350, 50), 20, 5),
         Poly.Polygon ((50, 250), 20, 3),
         Poly.Circle ((250, 250), 20),
      ];

      Mesh3 mesh;
      try {
         // Discretize contours
         List<Point2> pts = []; List<int> splits = [0];
         foreach (Poly poly in polys) {
            poly.Discretize (pts, 0.1, 0.5411);
            splits.Add (pts.Count);
         }

         // Tessellate the polygon into triangles
         List<int> tries = Lib.Tessellate (pts, splits);

         // Create thick plane from triangles and contours
         List<Point3> nodes = tries.Select (n => (Point3)pts[n]).ToList ();
         nodes.AddRange ([.. nodes.Select (x => x.WithZ (thk))]);
         ReadOnlySpan<Point2> span = pts.AsSpan ();
         for (int i = 1; i < splits.Count; i++) {
            ReadOnlySpan<Point2> span2 = span[splits[i - 1]..splits[i]];
            for (int j = 1; j <= span2.Length; j++) {
               Point3 a = (Point3)span2[j - 1], b = (Point3)span2[j % span2.Length];
               Point3 c = a.WithZ (thk), d = b.WithZ (thk);
               nodes.AddRange (a, b, d, d, c, a);
            }
         }
         mesh = new Mesh3Builder (nodes.AsSpan ()).Build ();
      } catch (Exception) {
         // Tessellator not installed — fall back to displaying outer contour as lines
         List<Point2> pts = [];
         polys[0].Discretize (pts, 0.1, 0.5411);
         List<Point3> linePairs = [];
         for (int i = 0; i < pts.Count; i++) {
            Point3 a = (Point3)pts[i], b = (Point3)pts[(i + 1) % pts.Count];
            linePairs.Add (a); linePairs.Add (b);
         }
         Bound2 polyBound = polys[0].GetBound ();
         return new SceneInitMsg {
            SceneType = ESceneType.Scene3D,
            BgColor = [96, 96, 96, 255],
            Bounds = [polyBound.X.Min, polyBound.Y.Min, 0, polyBound.X.Max, polyBound.Y.Max, (float)thk],
            Transforms = IdentityTransform (),
            Entities = [
               new EntityDataMsg {
                  Id = 0,
                  Primitives = [RenderCapture.CaptureLines3 (linePairs, [255, 255, 128, 255])],
               },
            ],
         };
      }

      Bound3 bound = mesh.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [96, 96, 96, 255],
         Bounds = Bounds3D (bound),
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg {
               Id = 0,
               Primitives = [RenderCapture.CaptureMesh (mesh, [255, 255, 128, 255], shadeMode: 1)],
            },
         ],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 5c. MESDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Minimum enclosing sphere + OBB demo</summary>
   static SceneInitMsg MESDemo () {
      Random R = new ();
      Point3[] pts = [.. GeneratePoints (R, 10000, 1000)];

      // Compute minimum enclosing sphere
      MinSphere s = MinSphere.From (pts);

      // Classify points: on sphere (0), inside (1), outside (2)
      (Point3 Pt, int N)[] ptlie = [.. pts
         .Select (pt => (pt, d: pt.DistTo (s.Center)))
         .Select (x => (x.pt, x.d.EQ (s.Radius) ? 0 : x.d < s.Radius ? 1 : 2))];

      // Sphere mesh (Glass shading)
      Mesh3 sphereMesh = Mesh3.Sphere (s.Center, s.Radius);
      RenderPrimitive spherePrim = RenderCapture.CaptureMesh (sphereMesh, [255, 255, 255, 255], shadeMode: 4, wireframe: false);

      // Classify and capture point groups
      List<EntityDataMsg> entities = [];
      int entId = 0;

      // Sphere entity
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [spherePrim] });

      // Point groups by classification
      (byte[] Color, float Size)[] styles = [
         ([0, 255, 0, 255], 8f),     // on sphere = green
         ([255, 255, 255, 255], 3f), // inside = white
         ([255, 0, 0, 255], 8f),     // outside = red
      ];
      foreach (IGrouping<int, (Point3 Pt, int N)> g in ptlie.GroupBy (x => x.N)) {
         (byte[] color, float size) = styles[g.Key];
         RenderPrimitive ptPrim = RenderCapture.CapturePoints3 (g.Select (x => x.Pt), color, size);
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [ptPrim] });
      }

      // OBB wireframe
      Point3f[] ptsF = [.. pts.Select (x => (Point3f)x)];
      OBB obb = OBB.Build (ptsF);
      Matrix3 obbXfm = Matrix3.To (new CoordSystem ((Point3)obb.Center, (Vector3)obb.X, (Vector3)obb.Y));

      // Build OBB corners in local frame, then transform to world
      List<Point3> corners = [];
      (float ex, float ey, float ez) = ((float)obb.Extent.X, (float)obb.Extent.Y, (float)obb.Extent.Z);
      for (int dx = -1; dx <= 1; dx += 2)
         for (int dy = -1; dy <= 1; dy += 2)
            for (int dz = -1; dz <= 1; dz += 2)
               corners.Add (new Point3 (ex * dx, ey * dy, ez * dz) * obbXfm);

      int[] edgeIdx = [0, 1, 0, 2, 0, 4, 1, 3, 1, 5, 2, 3, 2, 6, 3, 7, 4, 5, 4, 6, 5, 7, 6, 7];
      List<Point3> obbLines = [];
      for (int i = 0; i < edgeIdx.Length; i += 2) {
         obbLines.Add (corners[edgeIdx[i]]);
         obbLines.Add (corners[edgeIdx[i + 1]]);
      }
      RenderPrimitive obbPrim = RenderCapture.CaptureLines3 (obbLines, [255, 255, 255, 255], lineWidth: 2f);
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [obbPrim] });

      // Axis lines
      List<Point3> axisLines = [
         new (0, 0, 0), new (100, 0, 0),
         new (0, 0, 0), new (0, 100, 0),
         new (0, 0, 0), new (0, 0, 100),
      ];
      RenderPrimitive axisPrim = RenderCapture.CaptureLines3 (axisLines, [255, 255, 255, 255]);
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [axisPrim] });

      // Center point
      RenderPrimitive centerPrim = RenderCapture.CapturePoints3 ([s.Center], [255, 0, 255, 255], pointSize: 6f);
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [centerPrim] });

      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [96, 96, 96, 255],
         Bounds = [0, 0, 0, 1000, 1000, 1000],
         Transforms = IdentityTransform (),
         Entities = entities.ToArray (),
      };
   }

   /// <summary>Generate random points within a randomly rotated cuboid</summary>
   static IEnumerable<Point3> GeneratePoints (Random R, int count, double size) {
      double half = size * 0.5, fsize = size * 0.01;
      (double w, double h, double d) = (Span (), Span (), Span ());
      Bound3 bound = new (-w, -h, -d, w, h, d);
      Matrix3 xfm = Matrix3.Rotation (V (), R.NextDouble () * Math.PI);
      xfm *= Matrix3.Translation (V () * half);
      int i = 0;
      do {
         Point3 pt = P () * half;
         if (!bound.Contains (pt)) continue;
         i++;
         yield return pt * xfm;
      } while (i < count);
      Point3 P () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      Vector3 V () => new (R.NextDouble (), R.NextDouble (), R.NextDouble ());
      double Span () => R.Next (5, 95) * fsize;
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 5d. AABBTreeDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>AABB tree demo — cow mesh with bounding volume hierarchy at level 5</summary>
   static SceneInitMsg AABBTreeDemo () {
      // Load cow mesh from zip
      using FileStream fs = File.OpenRead ($"{Lib.DevRoot}/TData/IO/MESH/cow.zip");
      using ZipArchive zar = new (fs);
      ZipArchiveEntry ze = zar.GetEntry ("cow.obj")!;
      ZipReadStream zstm = new (ze.Open (), ze.Length);
      Mesh3 mesh = Mesh3.LoadObj (zstm.ReadAllLines ());
      mesh *= Matrix3.Rotation (EAxis.X, Lib.HalfPI) * Matrix3.Rotation (EAxis.Z, -Lib.HalfPI);

      // Build collision mesh and extract AABB boxes at level 5
      CMesh cmesh = CMesh.Builder.Build (mesh);
      List<Bound3> boxes = cmesh.EnumBoxes (5).ToList ();

      // Build box wireframes
      List<Point3> boxLines = [];
      foreach (Bound3 box in boxes) {
         (Bound1 bx, Bound1 by, Bound1 bz) = (box.X, box.Y, box.Z);
         Point3 a = new (bx.Min, by.Min, bz.Min), b = new (bx.Max, by.Min, bz.Min);
         Point3 c = new (bx.Max, by.Max, bz.Min), d = new (bx.Min, by.Max, bz.Min);
         Point3 e = new (bx.Min, by.Min, bz.Max), f = new (bx.Max, by.Min, bz.Max);
         Point3 g = new (bx.Max, by.Max, bz.Max), h = new (bx.Min, by.Max, bz.Max);
         boxLines.AddRange ([a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h]);
      }

      Bound3 meshBound = mesh.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [64, 64, 64, 255],
         Bounds = Bounds3D (meshBound),
         Transforms = IdentityTransform (),
         Entities = [
            new EntityDataMsg {
               Id = 0,
               Primitives = [RenderCapture.CaptureMesh (mesh, [128, 128, 128, 255], shadeMode: 0)],
            },
            new EntityDataMsg {
               Id = 1,
               Primitives = [RenderCapture.CaptureLines3 (boxLines, [255, 255, 255, 255], lineWidth: 2f)],
            },
         ],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 6a. T3XDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>T3X file demo — blank (translucent) + part model overlaid</summary>
   static SceneInitMsg T3XDemo () {
      Model3 blank = new T3XReader ($"{Lib.DevRoot}/Demos/Data/5x-043-blank.t3x").Load ();
      Model3 part = new T3XReader ($"{Lib.DevRoot}/Demos/Data/5x-043.t3x").Load ();
      foreach (Ent3 ent in blank.Ents) ent.IsTranslucent = true;

      EntityDataMsg[] blankEnts = RenderCapture.CaptureModel3 (blank);
      EntityDataMsg[] partEnts = RenderCapture.CaptureModel3 (part);

      // Re-number part entity IDs to avoid collision with blank entity IDs
      int offset = blankEnts.Length;
      for (int i = 0; i < partEnts.Length; i++)
         partEnts[i] = new EntityDataMsg { Id = partEnts[i].Id + offset, Primitives = partEnts[i].Primitives };

      Bound3 bound = blank.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [80, 84, 88, 255],
         Bounds = Bounds3D (bound),
         Transforms = IdentityTransform (),
         Entities = [.. blankEnts, .. partEnts],
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 6b. STPScene
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>STEP file demo — loads and displays a STEP model</summary>
   static SceneInitMsg STPScene () {
      STEPReader sr = new ($"{Lib.DevRoot}/TData/Step/S00178.stp");
      Model3 model = sr.Load ();

      Bound3 bound = model.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [96, 96, 96, 255],
         Bounds = Bounds3D (bound),
         Transforms = IdentityTransform (),
         Entities = RenderCapture.CaptureModel3 (model),
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 6c. BuildOBBDemo
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>OBB construction demo — random rotations of surfaces with exact + fast OBB wireframes</summary>
   static SceneInitMsg BuildOBBDemo () {
      Random r = new (1);
      Model3 model = new T3XReader ($"{Lib.DevRoot}/TData/IO/T3X/5X-022.t3x").Load ();
      List<E3Surface> surfaces = [.. model.Ents.OfType<E3Surface> ().OrderByDescending (a => a.Mesh.GetArea ()).Take (40)];

      Bound3 b = new ();
      List<EntityDataMsg> entities = [];
      int entId = 0;

      for (int i = 0; i < surfaces.Count; i++) {
         E3Surface surface = surfaces[i];
         double xR = GetAngle (), yR = GetAngle (), zR = GetAngle ();
         Vector3 mid = (Vector3)surface.Bound.Midpoint;
         Matrix3 xfm = Matrix3.Translation (mid)
                 * Matrix3.Rotation (EAxis.X, xR) * Matrix3.Rotation (EAxis.Y, yR) * Matrix3.Rotation (EAxis.Z, zR)
                 * Matrix3.Translation (-mid);

         // Transform mesh vertices by the random rotation
         Mesh3 mesh = surface.Mesh;
         ImmutableArray<Mesh3.Node> verts = mesh.Vertex;
         float[] data = new float[verts.Length * 6];
         List<Point3f> xfmPts = [];
         for (int v = 0; v < verts.Length; v++) {
            Mesh3.Node node = verts[v];
            Point3f pos = node.Pos * xfm;
            Vector3 norm = ((Vector3)node.Vec) * xfm;
            xfmPts.Add (pos);
            int off = v * 6;
            data[off] = pos.X; data[off + 1] = pos.Y; data[off + 2] = pos.Z;
            data[off + 3] = (float)norm.X; data[off + 4] = (float)norm.Y; data[off + 5] = (float)norm.Z;
         }

         // Create mesh primitive with Glass shading (translucent)
         RenderPrimitive meshPrim = new () {
            Type = EPrimType.Mesh3D,
            Data = data,
            Indices = mesh.Triangle.ToArray (),
            WireIndices = mesh.Wire.Length > 0 ? mesh.Wire.ToArray () : [],
            Color = [255, 255, 255, 255],
            ShadeMode = 4, // Glass
         };
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [meshPrim] });

         // Compute OBB.Build and OBB.BuildFast on the transformed points
         ReadOnlySpan<Point3f> ptsSpan = xfmPts.ToArray ().AsSpan ();
         OBB obbExact = OBB.Build (ptsSpan);
         OBB obbFast = OBB.BuildFast (ptsSpan);

         // Draw exact OBB wireframe in yellow
         List<Point3> exactLines = OBBWireframe (obbExact);
         RenderPrimitive exactPrim = RenderCapture.CaptureLines3 (exactLines, [255, 255, 0, 255]);
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [exactPrim] });

         // Draw fast OBB wireframe in white
         List<Point3> fastLines = OBBWireframe (obbFast);
         RenderPrimitive fastPrim = RenderCapture.CaptureLines3 (fastLines, [255, 255, 255, 255]);
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [fastPrim] });

         b += mesh.GetBound (xfm);
      }

      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [128, 96, 64, 255],
         Bounds = Bounds3D (b),
         Transforms = IdentityTransform (),
         Entities = entities.ToArray (),
      };

      double GetAngle () => (r.NextDouble () - 0.5) * 90.D2R ();
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // 6d. IntMeshPlane
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Mesh slicing demo — intersect a mesh with multiple planes</summary>
   static SceneInitMsg IntMeshPlane () {
      Model3 model = new T3XReader ($"{Lib.DevRoot}/Demos/Data/5x-024-blank.t3x").Load ();

      List<Mesh3> meshes = [];
      List<Mesh3.Node> nodes = []; List<int> tris = [];
      foreach (E3Surface ent in model.Ents.OfType<E3Surface> ()) {
         Mesh3 mesh = ent.Mesh;
         meshes.Add (mesh);
         int n = nodes.Count;
         nodes.AddRange (mesh.Vertex);
         tris.AddRange (mesh.Triangle.Select (a => a + n));
      }
      Mesh3 fullmesh = new ([.. nodes], [.. tris], []);

      // Capture full mesh as Mesh3D with Glass shading
      RenderPrimitive meshPrim = RenderCapture.CaptureMesh (fullmesh, [255, 255, 255, 255], shadeMode: 4, wireframe: false);

      // Compute plane intersections
      Bound3 bound = fullmesh.Bound;
      MeshSlicer pmi = new ([.. meshes]);
      List<Polyline3> output = [];
      int step = 25;
      for (int i = step; i < 100; i += step) {
         double x = (i / 100.0).Along (bound.X);
         pmi.Compute (new PlaneDef (new (x, 0, 0), Vector3.XAxis), output);
         double y = (i / 100.0).Along (bound.Y);
         pmi.Compute (new PlaneDef (new (0, y, 0), Vector3.YAxis), output);
         double z = (i / 100.0).Along (bound.Z);
         pmi.Compute (new PlaneDef (new (0, 0, z), Vector3.ZAxis), output);
      }

      // Convert polylines to line pairs
      List<Point3> linePairs = [];
      List<Point3> endPts = [];
      foreach (Polyline3 poly in output) {
         ImmutableArray<Point3> pts = poly.Pts;
         for (int i = 0; i < pts.Length - 1; i++) {
            linePairs.Add (pts[i]);
            linePairs.Add (pts[i + 1]);
         }
         if (!pts[0].EQ (pts[^1])) { endPts.Add (poly.Start); endPts.Add (poly.End); }
      }

      List<EntityDataMsg> entities = [];
      int entId = 0;

      // Full mesh entity
      entities.Add (new EntityDataMsg { Id = entId++, Primitives = [meshPrim] });

      // Intersection lines entity
      if (linePairs.Count > 0) {
         RenderPrimitive linesPrim = RenderCapture.CaptureLines3 (linePairs, [255, 255, 255, 255], lineWidth: 2f);
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [linesPrim] });
      }

      // Open polyline endpoints
      if (endPts.Count > 0) {
         RenderPrimitive ptsPrim = RenderCapture.CapturePoints3 (endPts, [255, 255, 0, 255], pointSize: 7f);
         entities.Add (new EntityDataMsg { Id = entId++, Primitives = [ptsPrim] });
      }

      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [32, 64, 96, 255],
         Bounds = Bounds3D (bound),
         Transforms = IdentityTransform (),
         Entities = entities.ToArray (),
      };
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // Helpers
   // ═══════════════════════════════════════════════════════════════════════════════

   static float[] IdentityTransform ()
      => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

   static float[] Bounds2D (Bound2 b)
      => [(float)b.X.Min, (float)b.Y.Min, (float)b.X.Max, (float)b.Y.Max];

   static float[] Bounds3D (Bound3 b)
      => [b.X.Min, b.Y.Min, b.Z.Min, b.X.Max, b.Y.Max, b.Z.Max];

   /// <summary>Build OBB wireframe as 12 line-pair edges (24 points)</summary>
   static List<Point3> OBBWireframe (OBB bx) {
      Vector3 x = (Vector3)(bx.X * bx.Extent.X);
      Vector3 y = (Vector3)(bx.Y * bx.Extent.Y);
      Vector3 z = (Vector3)(bx.Z * bx.Extent.Z);
      Point3 C = (Point3)bx.Center;
      Point3 a = C - x - y - z, b = C + x - y - z, c = C + x + y - z, d = C - x + y - z;
      Point3 e = C - x - y + z, f = C + x - y + z, g = C + x + y + z, h = C - x + y + z;
      return [a, b, b, c, c, d, d, a, e, f, f, g, g, h, h, e, a, e, b, f, c, g, d, h];
   }

   /// <summary>Capture a list of Poly as Lines2D + Beziers2D primitives</summary>
   static void CapturePolys (IEnumerable<Poly> polys, byte[] rgba, List<RenderPrimitive> prims,
      float lineWidth = 2f) {
      List<float> lineData = new (), bezierData = new ();
      foreach (Poly poly in polys)
         foreach (Seg seg in poly.Segs) {
            if (seg.IsArc) {
               List<Vec2F> bezPts = new ();
               seg.ToBeziers (bezPts);
               foreach (Vec2F pt in bezPts) { bezierData.Add (pt.X); bezierData.Add (pt.Y); }
            } else {
               lineData.Add ((float)seg.A.X); lineData.Add ((float)seg.A.Y);
               lineData.Add ((float)seg.B.X); lineData.Add ((float)seg.B.Y);
            }
         }
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive { Type = EPrimType.Lines2D, Data = lineData.ToArray (), Color = rgba, LineWidth = lineWidth });
      if (bezierData.Count > 0)
         prims.Add (new RenderPrimitive { Type = EPrimType.Beziers2D, Data = bezierData.ToArray (), Color = rgba, LineWidth = lineWidth });
   }
}
