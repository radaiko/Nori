// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Tauri sidecar — launches a NoriServer with demo scene routing
// ╚╩═╩═╩╝╚╝ ──────────────────────────────────────────────────────────────
using Nori;

// Initialize Nori.Core (registers WAD/stream locators for font data, etc.)
// DevRoot must point to the repo root (where Wad/ and TData/ live)
Environment.SetEnvironmentVariable ("NORIROOT", Path.GetFullPath (Path.Combine (AppContext.BaseDirectory, "../../../../../../")));
Lib.Init ();

// Parse args: [demoName] [port]
string demoName = args.Length > 0 ? args[0] : "dwg";
int port = args.Length > 1 && int.TryParse (args[1], out int p) ? p : 5100;
string url = $"http://localhost:{port}/";

Console.WriteLine ($"Nori Server starting on {url}");

NoriServer server = new ();
SceneSerializer serializer = new ();

// Convex hull interactive state
string activeDemo = "";
List<Point2> hullPts = [];
Random hullRng = new ();
Point2 hullLast = Point2.Zero;
const int HullMax = 300;
Bound2 hullBound = new Bound2 (-700, -500, 700, 500).InflatedF (0.8);

LoadDemo (demoName);

server.OnCommand += (session, cmd) => {
   if (cmd.Name == "demo") {
      Console.WriteLine ($"Switching to demo: {cmd.Arg}");
      LoadDemo (cmd.Arg);
   }
};

server.OnInteraction += (session, msg) => {
   if (activeDemo != "convexhull") return;

   if (msg.Type == EInteractionType.Hover) {
      Point2 pt = new (msg.X, msg.Y);
      if (hullLast.DistTo (pt) < 10) return;
      hullPts.Add (hullLast = pt);
      for (int i = 0; i < 10; i++)
         if (hullPts.Count > HullMax) hullPts.RemoveAt (0);
      BroadcastHullUpdate ();
   } else if (msg.Type == EInteractionType.Click) {
      AddRandomHullPts (5 * HullMax);
      BroadcastHullUpdate ();
   }
};

server.OnClientConnected += session =>
   Console.WriteLine ($"Client connected: {session.Id}");
server.OnClientDisconnected += session =>
   Console.WriteLine ($"Client disconnected: {session.Id}");

CancellationTokenSource cts = new ();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel (); };

try {
   await server.StartAsync (url, cts.Token);
} catch (OperationCanceledException) {
   Console.WriteLine ("Server shutting down...");
}

server.Dispose ();

// ═══════════════════════════════════════════════════════════════════════════════
// Helpers
// ═══════════════════════════════════════════════════════════════════════════════

void LoadDemo (string name) {
   activeDemo = name;
   server.Dwg = null;
   server.Model = null;
   if (name == "convexhull") {
      hullPts.Clear ();
      AddRandomHullPts (10 * HullMax);
      server.SetCustomScene (BuildHullScene ());
   } else {
      server.SetCustomScene (DemoScenes.Build (name));
   }
}

void AddRandomHullPts (int count) {
   for (int i = 0; i < count; i++) {
      double x = hullRng.NextDouble () * hullBound.X.Length + hullBound.X.Min;
      double y = hullRng.NextDouble () * hullBound.Y.Length + hullBound.Y.Min;
      hullPts.Add (new (x, y));
   }
}

SceneInitMsg BuildHullScene () {
   List<Point2> hull = ConvexHull.Compute (hullPts);
   return new SceneInitMsg {
      SceneType = ESceneType.Scene2D,
      BgColor = [40, 40, 40, 255],
      Bounds = [-700, -500, 700, 500],
      Transforms = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1],
      Entities = [
         new EntityDataMsg { Id = 0, Primitives = [BuildPointsPrim ()] },
         new EntityDataMsg { Id = 1, Primitives = [BuildHullPrim (hull)] },
      ],
   };
}

RenderPrimitive BuildPointsPrim () {
   List<float> data = [];
   foreach (Point2 pt in hullPts) { data.Add ((float)pt.X); data.Add ((float)pt.Y); }
   return new RenderPrimitive {
      Type = EPrimType.Points2D,
      Data = data.ToArray (),
      Color = [255, 255, 255, 255],
      PointSize = 6f,
   };
}

RenderPrimitive BuildHullPrim (List<Point2> hull) {
   List<float> data = [];
   for (int i = 0; i < hull.Count; i++) {
      Point2 a = hull[i], b = hull[(i + 1) % hull.Count];
      data.Add ((float)a.X); data.Add ((float)a.Y);
      data.Add ((float)b.X); data.Add ((float)b.Y);
   }
   return new RenderPrimitive {
      Type = EPrimType.Lines2D,
      Data = data.ToArray (),
      Color = [255, 255, 255, 255],
      LineWidth = 1.5f,
   };
}

void BroadcastHullUpdate () {
   List<Point2> hull = ConvexHull.Compute (hullPts);
   byte[] ptBytes = serializer.ToBytes (new EntityUpdateMsg {
      Entity = new EntityDataMsg { Id = 0, Primitives = [BuildPointsPrim ()] },
   });
   byte[] hullBytes = serializer.ToBytes (new EntityUpdateMsg {
      Entity = new EntityDataMsg { Id = 1, Primitives = [BuildHullPrim (hull)] },
   });
   _ = server.BroadcastAsync (ptBytes);
   _ = server.BroadcastAsync (hullBytes);
}
