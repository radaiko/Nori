// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ BooleanDemo.cs
// ║║║║╬║╔╣║ Demo for Union, Intersection, Subtraction boolean operations
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class BooleanScene -------------------------------------------------------------------------
class BooleanScene : Scene2 {
   public BooleanScene () {
      List<List<Poly>> polys = [
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
         Poly.Circle ((2000, 1150), 180)]
      ];
      BgrdColor = Color4.Gray (216);
      Bound2 bound = new (polys.SelectMany (a => a).Select (a => a.GetBound ()));
      bound += new Point2 (2 * bound.X.Max, 2 * bound.Y.Max);
      Bound = bound.InflatedF (1.05);
      Root = new BooleanRootVN (polys, bound);
   }
}
#endregion

#region class BooleanRootVN ------------------------------------------------------------------------
class BooleanRootVN (List<List<Poly>> polys, Bound2 bound) : VNode {
   enum EPane { None = -1, Polys = 0, Union = 1, Intersection = 2, Subtraction = 3 }

   readonly Bound2 ViewBound = bound;
   readonly List<List<Poly>> mPolys = polys;

   public override void SetAttributes () {
      Lux.LineWidth = 2f;
      Lux.Color = Color4.Gray (150);
      Lux.LineType = ELineType.Continuous;
   }

   public override VNode? GetChild (int n) {
      if (n > 3) return null;
      EPane pane = (EPane)n;
      // <<TODO>> Boolean operations (Union, Intersect, Subtract) not yet available in cross-platform Core.
      // For now, all panes show the input polys.
      List<Poly> polyList = mPolys.SelectMany (a => a).ToList ();
      return new XfmVN (Matrix3.Translation ((Vector3)GetOffset ((EPane)n, ViewBound)), new PolyVN (polyList, pane));
   }

   public override void Draw () {
      Vector2 vec = GetOffset (EPane.None, ViewBound);
      Bound2 b = ViewBound;
      Point2 mid = b.Midpoint;
      // Draw quadrant lines to divide the screen in four sections.
      Lux.Lines ([new Vec2F (mid.X, b.Y.Min - vec.Y), new (mid.X, b.Y.Max + vec.Y),
         new (b.X.Min - vec.X, mid.Y), new (b.X.Max + vec.X, mid.Y)]);

      Lux.Text2D ("Polys", new (mid.X - 10, mid.Y - 10), ETextAlign.TopRight, Vec2S.Zero);
      Lux.Text2D ("Union", new (mid.X + 10, mid.Y - 10), ETextAlign.TopLeft, Vec2S.Zero);
      Lux.Text2D ("Intersection", new (mid.X - 10, mid.Y + 10), ETextAlign.BotRight, Vec2S.Zero);
      Lux.Text2D ("Subtraction", new (mid.X + 10, mid.Y + 10), ETextAlign.BotLeft, Vec2S.Zero);
   }

   static Vector2 GetOffset (EPane pane, Bound2 bound) {
      double dx = bound.Width * 0.02, dy = bound.Height * 0.02;
      (double sx, double sy) = (Lux.Viewport.X / bound.Width, Lux.Viewport.Y / bound.Height);
      if (sy < sx) {
         dx += (Lux.Viewport.X / sy - bound.Width) / 2;
      } else {
         dy += (Lux.Viewport.Y / sx - bound.Height) / 2;
      }
      Vector2 vec = new (dx, dy);
      if (pane >= 0) {
         (dx, dy) = vec / 2;
         (dx, dy) = pane switch {
            EPane.Union => (dx, -dy),
            EPane.Intersection => (-dx, dy),
            EPane.Subtraction => (dx, dy),
            EPane.Polys => (-dx, -dy),
            _ => throw new NotImplementedException (),
         };
         int n = (int)pane;
         (sx, sy) = ((n & 1) > 0 ? 1.0 : 0, (n & 2) > 0 ? 1.0 : 0);
         vec = new Vector2 (dx + sx * bound.Width / 2, dy + sy * bound.Height / 2);
      }
      return vec;
   }

   // Implementation -----------------------------------------------------------
   class PolyVN (List<Poly> polys, EPane pane) : VNode {
      readonly EPane mPane = pane;

      public override void SetAttributes () {
         Lux.LineWidth = 2f;
         Lux.Color = mPane > 0 ? new (96, 96, 192) : Color4.Gray (96);
         Lux.LineType = ELineType.Continuous;
      }

      public override void Draw () => Lux.Polys (mPolys.AsSpan ());

      public override VNode? GetChild (int n) => null;

      readonly List<Poly> mPolys = polys;
   }
}
#endregion
