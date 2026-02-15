// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ RenderCapture.cs
// ║║║║╬║╔╣║ Converts Nori scene entities into protocol render primitives
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Immutable;
namespace Nori;

/// <summary>Converts Nori scene entities into protocol render primitives</summary>
/// This walks the scene's entity collections directly (rather than going through the
/// Lux VNode rendering pipeline) to extract renderable geometry in wire-format.
public class RenderCapture {
   /// <summary>Capture all renderable data from a 2D scene backed by a Dwg2</summary>
   public static EntityDataMsg[] CaptureDwg2 (Dwg2 dwg) {
      List<EntityDataMsg> entities = new ();

      // First pass: fill interiors of closed polys in a single stencil pass
      // (matches WPF DwgFillVN — shared hub, XOR stencil creates correct inside/outside)
      var closedPolys = dwg.Ents.OfType<E2Poly> ().Where (e => e.Poly.IsClosed).Select (e => e.Poly).ToList ();
      if (closedPolys.Count > 0) {
         Bound2 bound = dwg.Bound.InflatedF (1.01);
         var fillPrim = CaptureCombinedFill (closedPolys, [240, 240, 248, 255], bound);
         if (fillPrim != null)
            entities.Add (new EntityDataMsg { Id = -1, Primitives = [fillPrim] });
      }

      // Second pass: entity outlines and other geometry
      for (int i = 0; i < dwg.Ents.Count; i++) {
         Ent2 ent = dwg.Ents[i];
         EntityDataMsg? msg = CaptureEntity2 (ent, i);
         if (msg != null) entities.Add (msg);
      }
      return entities.ToArray ();
   }

   /// <summary>Capture a single 2D entity by index</summary>
   public static EntityDataMsg? CaptureEntity2 (Ent2 ent, int id) {
      List<RenderPrimitive> prims = new ();
      // Resolve color: entity color takes priority, then layer color, fallback to white
      Color4 color = ent.Color.IsNil ? ent.Layer.Color : ent.Color;
      if (color.IsNil) color = Color4.White;
      byte[] rgba = [color.R, color.G, color.B, color.A];
      ELineType lineType = ent.Layer.Linetype;

      switch (ent) {
         case E2Poly ep:
            CapturePoly (ep.Poly, rgba, lineType, prims);
            break;
         case E2Point ep:
            CapturePoint (ep.Pt, rgba, prims);
            break;
         case E2Text et:
            CaptureText (et, rgba, prims);
            break;
         case E2Solid es:
            CaptureSolid (es, rgba, prims);
            break;
         case E2Spline es:
            CaptureSpline (es, rgba, lineType, prims);
            break;
         case E2Bendline eb:
            CaptureBendline (eb, rgba, prims);
            break;
         case E2Insert ei:
            CaptureInsert (ei, rgba, prims);
            break;
         case E2Dimension ed:
            CaptureDimension (ed, rgba, prims);
            break;
      }

      if (prims.Count == 0) return null;
      return new EntityDataMsg { Id = id, Primitives = prims.ToArray () };
   }

   // Poly capture ----------------------------------------------------------------
   /// <summary>Captures a Poly as Lines2D (arcs are discretized to line segments)</summary>
   static void CapturePoly (Poly poly, byte[] rgba, ELineType lineType, List<RenderPrimitive> prims) {
      if (poly.Count == 0) return;
      List<float> lineData = new ();
      List<Point2> pts = new ();
      poly.Discretize (pts, 0.1, 0.5);
      for (int i = 0; i < pts.Count - 1; i++) {
         lineData.Add ((float)pts[i].X); lineData.Add ((float)pts[i].Y);
         lineData.Add ((float)pts[i + 1].X); lineData.Add ((float)pts[i + 1].Y);
      }
      if (poly.IsClosed && pts.Count > 1) {
         lineData.Add ((float)pts[^1].X); lineData.Add ((float)pts[^1].Y);
         lineData.Add ((float)pts[0].X); lineData.Add ((float)pts[0].Y);
      }
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Lines2D, Data = lineData.ToArray (),
            Color = rgba, LineWidth = 2f,
            LineType = (byte)lineType,
         });
   }

   // Combined fill capture (for closed polys) ----------------------------------------
   /// <summary>Combines all closed polys into a single Fill2D with shared hub (matches WPF DwgFillVN)</summary>
   static RenderPrimitive? CaptureCombinedFill (List<Poly> polys, byte[] rgba, Bound2 bound) {
      List<float> fillData = [];
      List<int> indices = [];
      // Index 0 = shared hub vertex at bounding box midpoint
      Vec2F hub = bound.Midpoint;
      fillData.Add (hub.X); fillData.Add (hub.Y);

      foreach (var poly in polys) {
         List<Point2> pts = new ();
         poly.Discretize (pts, 0.05, Lib.FineTessAngle);
         if (pts.Count < 3) continue;
         indices.Add (0); // hub (start new fan)
         int idx0 = fillData.Count / 2;
         foreach (Point2 pt in pts) {
            indices.Add (fillData.Count / 2);
            fillData.Add ((float)pt.X); fillData.Add ((float)pt.Y);
         }
         indices.Add (idx0); // close back to first point
         indices.Add (-1);   // delimiter
      }

      if (fillData.Count < 6) return null; // need at least hub + 2 points
      return new RenderPrimitive {
         Type = EPrimType.Fill2D,
         Data = fillData.ToArray (),
         Indices = indices.ToArray (),
         Color = rgba,
         ZLevel = -10,
         BoundData = [(float)bound.X.Min, (float)bound.Y.Min, (float)bound.X.Max, (float)bound.Y.Max],
      };
   }

   // Point capture ---------------------------------------------------------------
   static void CapturePoint (Point2 pt, byte[] rgba, List<RenderPrimitive> prims) {
      prims.Add (new RenderPrimitive {
         Type = EPrimType.Points2D,
         Data = [(float)pt.X, (float)pt.Y],
         Color = rgba, PointSize = 4f,
      });
   }

   // Text capture ----------------------------------------------------------------
   /// <summary>Captures an E2Text by serializing the rendered poly outlines</summary>
   /// Nori renders text as line-font polylines (via LineFont), so we serialize the
   /// resulting Polys the same way the VNode does.
   static void CaptureText (E2Text et, byte[] rgba, List<RenderPrimitive> prims) {
      ImmutableArray<Poly> polys = et.Polys;
      if (polys.Length == 0) return;
      List<float> lineData = new ();
      List<float> bezierData = new ();
      foreach (Poly poly in polys) {
         foreach (Seg seg in poly.Segs) {
            if (seg.IsArc) {
               List<Vec2F> bezPts = new ();
               seg.ToBeziers (bezPts);
               foreach (Vec2F pt in bezPts) {
                  bezierData.Add (pt.X);
                  bezierData.Add (pt.Y);
               }
            } else {
               Point2 a = seg.A, b = seg.B;
               lineData.Add ((float)a.X); lineData.Add ((float)a.Y);
               lineData.Add ((float)b.X); lineData.Add ((float)b.Y);
            }
         }
      }
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Lines2D, Data = lineData.ToArray (),
            Color = rgba, LineWidth = 2f,
         });
      if (bezierData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Beziers2D, Data = bezierData.ToArray (),
            Color = rgba, LineWidth = 2f,
         });
   }

   // Solid capture ---------------------------------------------------------------
   /// <summary>Captures an E2Solid as two triangles (DXF corner swap applied)</summary>
   static void CaptureSolid (E2Solid es, byte[] rgba, List<RenderPrimitive> prims) {
      IReadOnlyList<Point2> pts = es.Pts;
      if (pts.Count < 3) return;
      // DXF solids store 4 corners with 3rd and 4th swapped
      Point2 p0 = pts[0], p1 = pts[1];
      Point2 p2 = pts.Count >= 4 ? pts[3] : pts[2]; // swapped
      Point2 p3 = pts.Count >= 4 ? pts[2] : pts[2]; // swapped
      // Emit as two triangles: (p0,p1,p2) and (p2,p1,p3)
      List<float> data = [(float)p0.X, (float)p0.Y, (float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y,
                          (float)p2.X, (float)p2.Y, (float)p1.X, (float)p1.Y, (float)p3.X, (float)p3.Y];
      prims.Add (new RenderPrimitive {
         Type = EPrimType.Triangles2D, Data = data.ToArray (),
         Color = rgba,
      });
   }

   // Spline capture --------------------------------------------------------------
   /// <summary>Captures an E2Spline by discretizing it into a line strip</summary>
   static void CaptureSpline (E2Spline es, byte[] rgba, ELineType lineType, List<RenderPrimitive> prims) {
      IReadOnlyList<Point2> pts = es.Pts;
      if (pts.Count < 2) return;
      // Convert line strip to pairs: [a,b, b,c, c,d, ...]
      List<float> lineData = new ();
      for (int i = 0; i < pts.Count - 1; i++) {
         lineData.Add ((float)pts[i].X); lineData.Add ((float)pts[i].Y);
         lineData.Add ((float)pts[i + 1].X); lineData.Add ((float)pts[i + 1].Y);
      }
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Lines2D, Data = lineData.ToArray (),
            Color = rgba, LineWidth = 2f,
            LineType = (byte)lineType,
         });
   }

   // Bendline capture ------------------------------------------------------------
   /// <summary>Captures an E2Bendline as line pairs plus angle text annotations</summary>
   static void CaptureBendline (E2Bendline eb, byte[] rgba, List<RenderPrimitive> prims) {
      ImmutableArray<Point2> pts = eb.Pts;
      if (pts.Length < 2) return;
      List<float> lineData = new ();
      for (int i = 0; i < pts.Length - 1; i += 2) {
         lineData.Add ((float)pts[i].X); lineData.Add ((float)pts[i].Y);
         lineData.Add ((float)pts[i + 1].X); lineData.Add ((float)pts[i + 1].Y);
      }
      // Bendlines are always green with a specific linetype
      byte[] green = [0, 192, 0, 255];
      ELineType lt = eb.Angle > 0 ? ELineType.Dash2 : ELineType.DashDotDot;
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Lines2D, Data = lineData.ToArray (),
            Color = green, LineWidth = 2f,
            LineType = (byte)lt,
         });

      // Angle text at midpoint of each bendline segment (matches WPF E2BendlineVN.DrawText)
      string text = Math.Round (eb.Angle.R2D (), 2).ToString (System.Globalization.CultureInfo.InvariantCulture);
      if (text == "-0") text = "0";
      text = eb.Angle > 0 ? $"+{text}\u00b0" : $"{text}\u00b0";
      List<Poly> textPolys = new ();
      for (int i = 0; i < pts.Length; i += 2) {
         Point2 midpt = pts[i].Midpoint (pts[i + 1]);
         LineFont.Get ("simplex").Render (text, midpt, ETextAlign.MidCenter, 0, 1, 1, 0, textPolys);
      }
      if (textPolys.Count > 0) {
         List<float> textData = new ();
         foreach (Poly poly in textPolys)
            foreach (Seg seg in poly.Segs) {
               textData.Add ((float)seg.A.X); textData.Add ((float)seg.A.Y);
               textData.Add ((float)seg.B.X); textData.Add ((float)seg.B.Y);
            }
         if (textData.Count > 0)
            prims.Add (new RenderPrimitive {
               Type = EPrimType.Lines2D, Data = textData.ToArray (),
               Color = [0, 0, 0, 255], LineWidth = 0.5f,
            });
      }
   }

   // Insert capture --------------------------------------------------------------
   /// <summary>Captures an E2Insert by recursively capturing block entities, transformed</summary>
   static void CaptureInsert (E2Insert ei, byte[] rgba, List<RenderPrimitive> prims) {
      Matrix2 xfm = ei.Xfm;
      Block2 block = ei.Block;
      foreach (Ent2 ent in block.Ents) {
         // Transform each block entity's geometry by the insert's transform
         Ent2 xformed = ent * xfm;
         // Resolve color: entity-level overrides block-level (entity in block uses
         // ByBlock color, which means the insert's color takes over)
         Color4 entColor = xformed.Color.IsNil ? xformed.Layer.Color : xformed.Color;
         if (entColor.IsNil) entColor = new Color4 (rgba[0], rgba[1], rgba[2]);
         byte[] entRgba = [entColor.R, entColor.G, entColor.B, entColor.A];

         switch (xformed) {
            case E2Poly ep:
               CapturePoly (ep.Poly, entRgba, xformed.Layer.Linetype, prims);
               break;
            case E2Point ep:
               CapturePoint (ep.Pt, entRgba, prims);
               break;
            case E2Text et:
               CaptureText (et, entRgba, prims);
               break;
            case E2Solid es:
               CaptureSolid (es, entRgba, prims);
               break;
            case E2Spline es:
               CaptureSpline (es, entRgba, xformed.Layer.Linetype, prims);
               break;
         }
      }
   }

   // Dimension capture -----------------------------------------------------------
   /// <summary>Captures an E2Dimension by recursively capturing its sub-entities</summary>
   static void CaptureDimension (E2Dimension ed, byte[] rgba, List<RenderPrimitive> prims) {
      foreach (Ent2 ent in ed.Ents) {
         Color4 entColor = ent.Color.IsNil ? ent.Layer.Color : ent.Color;
         if (entColor.IsNil) entColor = new Color4 (rgba[0], rgba[1], rgba[2]);
         byte[] entRgba = [entColor.R, entColor.G, entColor.B, entColor.A];

         switch (ent) {
            case E2Poly ep:
               CapturePoly (ep.Poly, entRgba, ent.Layer.Linetype, prims);
               break;
            case E2Point ep:
               CapturePoint (ep.Pt, entRgba, prims);
               break;
            case E2Text et:
               CaptureText (et, entRgba, prims);
               break;
            case E2Solid es:
               CaptureSolid (es, entRgba, prims);
               break;
         }
      }
   }

   // 3D capture ═════════════════════════════════════════════════════════════════

   /// <summary>Capture renderable data from a 3D scene model</summary>
   public static EntityDataMsg[] CaptureModel3 (Model3 model) {
      List<EntityDataMsg> entities = new ();
      for (int i = 0; i < model.Ents.Count; i++) {
         Ent3 ent = model.Ents[i];
         if (ent is E3Surface surf) {
            RenderPrimitive? prim = CaptureSurface (surf);
            if (prim != null)
               entities.Add (new EntityDataMsg { Id = i, Primitives = [prim] });
         }
      }
      return entities.ToArray ();
   }

   /// <summary>Tessellate an E3Surface into a Mesh3D render primitive</summary>
   static RenderPrimitive? CaptureSurface (E3Surface surf) {
      Mesh3 mesh = surf.Mesh;
      if (mesh.Vertex.Length == 0) return null;
      byte[] rgba = [255, 255, 255, 255];
      byte shadeMode = surf.IsTranslucent ? (byte)4 : (byte)1; // Glass=4, Phong=1
      return CaptureMesh (mesh, rgba, shadeMode);
   }

   /// <summary>Convert a Mesh3 into a Mesh3D render primitive</summary>
   public static RenderPrimitive CaptureMesh (Mesh3 mesh, byte[] rgba,
      byte shadeMode = 1, bool wireframe = true) {
      // Pack vertices: 6 floats per vertex (position xyz + normal xyz)
      ImmutableArray<Mesh3.Node> verts = mesh.Vertex;
      float[] data = new float[verts.Length * 6];
      for (int i = 0; i < verts.Length; i++) {
         Mesh3.Node node = verts[i];
         int off = i * 6;
         data[off] = node.Pos.X;
         data[off + 1] = node.Pos.Y;
         data[off + 2] = node.Pos.Z;
         data[off + 3] = (float)node.Vec.X;
         data[off + 4] = (float)node.Vec.Y;
         data[off + 5] = (float)node.Vec.Z;
      }

      // Pack triangle indices (already groups of 3)
      int[] indices = mesh.Triangle.ToArray ();

      // Extract wireframe edge indices
      int[] wireIndices;
      if (wireframe && mesh.Wire.Length > 0) {
         wireIndices = mesh.Wire.ToArray ();
      } else if (wireframe) {
         // Compute unique edges from triangles
         HashSet<(int A, int B)> edgeSet = new ();
         List<int> edges = new ();
         for (int i = 0; i < indices.Length; i += 3) {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            AddEdge (a, b); AddEdge (b, c); AddEdge (c, a);

            void AddEdge (int t1, int t2) {
               if (t1 > t2) (t1, t2) = (t2, t1);
               if (edgeSet.Add ((t1, t2))) { edges.Add (t1); edges.Add (t2); }
            }
         }
         wireIndices = edges.ToArray ();
      } else {
         wireIndices = [];
      }

      return new RenderPrimitive {
         Type = EPrimType.Mesh3D,
         Data = data,
         Indices = indices,
         WireIndices = wireIndices,
         Color = rgba,
         ShadeMode = shadeMode,
      };
   }

   /// <summary>Pack 3D points into a Points3D render primitive</summary>
   public static RenderPrimitive CapturePoints3 (IEnumerable<Point3> pts,
      byte[] rgba, float pointSize = 4f) {
      List<float> data = new ();
      foreach (Point3 pt in pts) {
         data.Add ((float)pt.X);
         data.Add ((float)pt.Y);
         data.Add ((float)pt.Z);
      }
      return new RenderPrimitive {
         Type = EPrimType.Points3D,
         Data = data.ToArray (),
         Color = rgba,
         PointSize = pointSize,
      };
   }

   /// <summary>Pack 3D line pairs into a Lines3D render primitive</summary>
   public static RenderPrimitive CaptureLines3 (IEnumerable<Point3> linePairs,
      byte[] rgba, float lineWidth = 2f) {
      List<float> data = new ();
      foreach (Point3 pt in linePairs) {
         data.Add ((float)pt.X);
         data.Add ((float)pt.Y);
         data.Add ((float)pt.Z);
      }
      return new RenderPrimitive {
         Type = EPrimType.Lines3D,
         Data = data.ToArray (),
         Color = rgba,
         LineWidth = lineWidth,
      };
   }

   /// <summary>Discretize polygons into a Fill2D render primitive</summary>
   public static RenderPrimitive CaptureFill2D (List<Poly> polys, byte[] rgba) {
      List<float> data = new ();
      List<int> indices = new ();
      List<Point2> allPts = new ();

      // Discretize all polys and collect points
      List<List<Point2>> polyPts = new ();
      foreach (Poly poly in polys) {
         List<Point2> pts = new ();
         poly.Discretize (pts, 0.1, 0.5411);
         polyPts.Add (pts);
         allPts.AddRange (pts);
      }

      if (allPts.Count == 0)
         return new RenderPrimitive { Type = EPrimType.Fill2D, Data = [], Color = rgba };

      // Compute overall midpoint as center vertex (index 0)
      Bound2 bound = new (allPts);
      Point2 mid = bound.Midpoint;
      data.Add ((float)mid.X);
      data.Add ((float)mid.Y);
      int vertIdx = 1;

      // Build triangle fan for each poly contour
      foreach (List<Point2> pts in polyPts) {
         if (pts.Count == 0) continue;
         int firstIdx = vertIdx;
         foreach (Point2 pt in pts) {
            data.Add ((float)pt.X);
            data.Add ((float)pt.Y);
            vertIdx++;
         }
         // Fan indices: center, then all contour vertices, back to first, terminated by -1
         indices.Add (0);
         for (int i = firstIdx; i < vertIdx; i++)
            indices.Add (i);
         indices.Add (firstIdx);
         indices.Add (-1);
      }

      return new RenderPrimitive {
         Type = EPrimType.Fill2D,
         Data = data.ToArray (),
         Indices = indices.ToArray (),
         Color = rgba,
         BoundData = [(float)bound.X.Min, (float)bound.Y.Min, (float)bound.X.Max, (float)bound.Y.Max],
      };
   }
}
