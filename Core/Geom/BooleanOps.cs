// ────── ╔╗                                                                                   CORE
// ╔═╦╦═╦╦╬╣ BooleanOps.cs
// ║║║║╬║╔╣║ Boolean operations on Poly using LibTessDotNet (cross-platform GLU tessellation port)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using LibTessDotNet;
namespace Nori;

#region class BooleanOps ---------------------------------------------------------------------------
/// <summary>Boolean operations (union, intersection, subtraction) on polys using GLU tessellation</summary>
public static class BooleanOps {
   /// <summary>Performs a union of two polys</summary>
   public static List<Poly> Union (this Poly a, Poly b) => Union ([a, b]);

   /// <summary>Performs a union of a set of polys</summary>
   public static List<Poly> Union (this ReadOnlySpan<Poly> input)
      => Process (input, WindingRule.Positive, normalizeCCW: true);

   /// <summary>Computes the intersection of two polys</summary>
   public static List<Poly> Intersect (this Poly a, Poly b)
      => Process ([a, b], WindingRule.AbsGeqTwo, normalizeCCW: true);

   /// <summary>Computes the intersection of a set of polys (pairwise)</summary>
   public static List<Poly> Intersect (this ReadOnlySpan<Poly> input) {
      if (input.Length < 2) return [.. input];
      List<Poly> result = [input[^1]];
      for (int i = input.Length - 2; i >= 0; i--) {
         var b = input[i];
         result = [.. result.SelectMany (a => Intersect (a, b))];
      }
      return result;
   }

   /// <summary>Subtracts a negative poly from a positive one</summary>
   public static List<Poly> Subtract (this Poly positive, Poly negative) => Subtract ([positive], [negative]);

   /// <summary>Subtracts a set of negative polys from positive polys</summary>
   /// Reverses the negative polys and performs union with Positive winding rule,
   /// so reversed polys must NOT be normalized (they need CW winding for subtraction).
   public static List<Poly> Subtract (this ReadOnlySpan<Poly> positive, ReadOnlySpan<Poly> negative) {
      List<Poly> input = [.. positive];
      foreach (var poly in negative) input.Add (poly.Reversed ());
      return Process (input.AsSpan (), WindingRule.Positive, normalizeCCW: false);
   }

   // Implementation -----------------------------------------------------------
   static List<Poly> Process (ReadOnlySpan<Poly> input, WindingRule winding, bool normalizeCCW) {
      var tess = new Tess ();

      // Discretize and add each poly as a contour
      List<Point2> pts = [];
      foreach (var poly in input) {
         pts.Clear ();
         if (poly.HasArcs) poly.Discretize (pts, 0.05, Lib.FineTessAngle);
         else pts.AddRange (poly.Pts);
         if (pts.Count < 3) continue;

         // For union/intersect, normalize winding to CCW for consistent results
         if (normalizeCCW && SignedArea (pts) < 0)
            pts.Reverse ();

         var contour = new ContourVertex[pts.Count];
         for (int j = 0; j < pts.Count; j++) {
            var pt = pts[j].R6 ();
            contour[j].Position = new Vec3 ((float)pt.X, (float)pt.Y, 0);
         }
         tess.AddContour (contour);
      }

      // Tessellate in boundary-only mode to get output contours
      tess.Tessellate (winding, ElementType.BoundaryContours, 0);

      // Extract output contours as Poly objects
      var output = new List<Poly> ();
      if (tess.Elements == null || tess.Vertices == null) return output;
      for (int i = 0; i < tess.ElementCount; i++) {
         int start = tess.Elements[i * 2];
         int count = tess.Elements[i * 2 + 1];
         if (count < 3) continue;
         PolyBuilder pb = new ();
         for (int j = 0; j < count; j++) {
            var v = tess.Vertices[start + j];
            pb.Line (new Point2 (v.Position.X, v.Position.Y));
         }
         var poly = pb.Close ().Build ();
         if (poly.Count > 2 && poly.GetBound ().Area > 0.1)
            output.Add (poly);
      }
      return output;
   }

   /// <summary>Compute signed area of a polygon (positive = CCW, negative = CW)</summary>
   static double SignedArea (List<Point2> pts) {
      double area = 0;
      for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
         area += (pts[j].X - pts[i].X) * (pts[j].Y + pts[i].Y);
      return area / 2;
   }
}
#endregion
