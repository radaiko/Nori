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
   /// <summary>Captures a Poly as lines and bezier primitives</summary>
   static void CapturePoly (Poly poly, byte[] rgba, ELineType lineType, List<RenderPrimitive> prims) {
      if (poly.Count == 0) return;
      List<float> lineData = new ();
      List<float> bezierData = new ();
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
      if (lineData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Lines2D, Data = lineData.ToArray (),
            Color = rgba, LineWidth = 2f,
            LineType = (byte)lineType,
         });
      if (bezierData.Count > 0)
         prims.Add (new RenderPrimitive {
            Type = EPrimType.Beziers2D, Data = bezierData.ToArray (),
            Color = rgba, LineWidth = 2f,
            LineType = (byte)lineType,
         });
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
   /// <summary>Captures an E2Solid as quads (4 points, with the DXF corner swap)</summary>
   static void CaptureSolid (E2Solid es, byte[] rgba, List<RenderPrimitive> prims) {
      IReadOnlyList<Point2> pts = es.Pts;
      if (pts.Count < 3) return;
      // DXF solids store 4 corners with 3rd and 4th swapped; the VNode does the same swap
      List<float> data = new ();
      data.Add ((float)pts[0].X); data.Add ((float)pts[0].Y);
      data.Add ((float)pts[1].X); data.Add ((float)pts[1].Y);
      if (pts.Count >= 4) {
         // Swap 3rd and 4th (same as E2SolidVN)
         data.Add ((float)pts[3].X); data.Add ((float)pts[3].Y);
         data.Add ((float)pts[2].X); data.Add ((float)pts[2].Y);
      } else {
         data.Add ((float)pts[2].X); data.Add ((float)pts[2].Y);
         data.Add ((float)pts[2].X); data.Add ((float)pts[2].Y);
      }
      prims.Add (new RenderPrimitive {
         Type = EPrimType.Quads2D, Data = data.ToArray (),
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
   /// <summary>Captures an E2Bendline as line pairs</summary>
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

   /// <summary>Capture renderable data from a 3D scene model</summary>
   public static EntityDataMsg[] CaptureModel3 (Model3 model) {
      List<EntityDataMsg> entities = new ();
      // TODO: Implement 3D entity capture (meshes, curves, surfaces)
      return entities.ToArray ();
   }
}
