// ────── ╔╗                                                                                 DEMOS
// ╔═╦╦═╦╦╬╣ DemoScenes.cs
// ║║║║╬║╔╣║ Static builder methods that return SceneInitMsg for each demo scene
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.IO;
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
   // Helpers
   // ═══════════════════════════════════════════════════════════════════════════════

   static float[] IdentityTransform ()
      => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

   static float[] Bounds2D (Bound2 b)
      => [(float)b.X.Min, (float)b.Y.Min, (float)b.X.Max, (float)b.Y.Max];

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
