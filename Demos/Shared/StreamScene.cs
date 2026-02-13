// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ StreamScene.cs
// ║║║║╬║╔╣║ Demo scene for streaming VNode rendering (continuous redraw)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Threading;
namespace Nori;

#region class StreamDemoScene ----------------------------------------------------------------------
class StreamDemoScene : Scene2 {
   public StreamDemoScene () {
      BgrdColor = Color4.Gray (216);
      Bound = new (0, 0, 500, 300);

      StreamLines lines1 = new ([new (30, 30), new (100, 30), new (50, 50), new (200, 200), new (400, 250), new (400, 30)], Color4.DarkGreen);
      StreamLines lines2 = new ([new (40, 40), new (440, 40), new (440, 40), new (250, 280)], Color4.Red);
      StreamQuads quads1 = new ([new (10, 10), new (50, 10), new (70, 50), new (20, 60), new (200, 30), new (150, 70), new (120, 50), new (180, 30)], Color4.Yellow);
      StreamQuads quads2 = new ([new (350, 100), new (430, 110), new (420, 130), new (350, 120)], new Color4 (128, 200, 128));
      StreamQuads quads3 = new ([new (10, 100), new (20, 100), new (40, 200), new (20, 200)], Color4.Yellow);
      StreamUnderlay under = new ();
      StreamOverlay over = new ();

      Root = new GroupVN ([under, lines1, lines2, quads1, quads2, over, quads3]);
   }
}
#endregion

#region class StreamUnderlay -----------------------------------------------------------------------
class StreamUnderlay : VNode {
   public StreamUnderlay () {
      mTimer = new Timer (_ => Lib.Post (() => { mPts.Clear (); Redraw (); }), null, Timeout.Infinite, 25);
      Streaming = true;
   }
   List<Vec2F> mPts = [];
   Timer mTimer;

   public override void OnAttach () => mTimer.Change (0, 25);

   public override void OnDetach () => mTimer.Change (Timeout.Infinite, Timeout.Infinite);

   public override void SetAttributes () {
      Lux.ZLevel = 8; Lux.LineWidth = 5f; Lux.Color = Color4.Gray (192);
   }
   Random mRand = new ();

   public override void Draw () {
      if (mPts.Count == 0) {
         for (int i = 0; i < 100; i++) {
            int x = mRand.Next (10, 490), y1 = mRand.Next (10, 290), y2 = mRand.Next (10, 290);
            mPts.Add (new (x, y1)); mPts.Add (new (x, y2));
         }
      }
      Lux.Lines (mPts.AsSpan ());
   }
}
#endregion

#region class StreamLines --------------------------------------------------------------------------
class StreamLines : VNode {
   public StreamLines (List<Vec2F> pts, Color4 color) => (mPts, mColor) = (pts, color);
   Color4 mColor;
   List<Vec2F> mPts;

   public override void SetAttributes () { Lux.ZLevel = 10; Lux.Color = mColor; }
   public override void Draw () => Lux.Lines (mPts.AsSpan ());
}
#endregion

#region class StreamQuads --------------------------------------------------------------------------
class StreamQuads : VNode {
   public StreamQuads (List<Vec2F> pts, Color4 color)
      => (mPts, mColor, Streaming) = (pts, color, false);
   Color4 mColor;
   List<Vec2F> mPts;

   public override void SetAttributes () { Lux.ZLevel = 8; Lux.Color = mColor; }
   public override void Draw () => Lux.Quads (mPts.AsSpan ());
}
#endregion

#region class StreamOverlay ------------------------------------------------------------------------
class StreamOverlay : VNode {
   public StreamOverlay () {
      mFace = new TypeFace ($"{Lib.DevRoot}/Wad/GL/Fonts/RobotoMono-Regular.ttf", 36);
      Streaming = true;
   }
   TypeFace mFace;

   /// <summary>Update the tracked point and redraw</summary>
   public void UpdatePoint (Point2 pt) {
      mPt = pt;
      Redraw ();
   }
   Point2 mPt = new (-1000, 0);

   public override void SetAttributes () {
      Lux.Color = Color4.DarkGreen; Lux.ZLevel = 20;
      Lux.TypeFace = mFace;
   }

   public override void Draw () {
      string s = $"{(mPt.X * 10).Round (0)},{(mPt.Y * 10).Round (0)}";
      Lux.Text2D (s, (Vec2F)mPt, ETextAlign.MidCenter, new Vec2S (0, 0));
   }
}
#endregion
