// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Tauri sidecar — launches a NoriServer with a demo drawing
// ╚╩═╩═╩╝╚╝ ──────────────────────────────────────────────────────────
using Nori;

// Parse port from command-line args (Tauri passes it)
int port = args.Length > 0 && int.TryParse (args[0], out int p) ? p : 5100;
string url = $"http://localhost:{port}/";

Console.WriteLine ($"Nori Server starting on {url}");

// Create server and load a demo scene
NoriServer server = new ();

// Build a demo drawing using actual Nori entity constructors
Dwg2 dwg = new ();
Layer2 layer = dwg.CurrentLayer;

// A rectangle
dwg.Add (Poly.Rectangle (0, 0, 200, 100));

// A polyline with curves (SVG-like path syntax)
dwg.Add (Poly.Parse ("M0,0 H200 V100 Q150,150,1 H0Z"));

// A circle
dwg.Add (Poly.Circle (new Point2 (150, 50), 30));

// Some points
dwg.Add (new Point2 (50, 50));
dwg.Add (new Point2 (100, 50));
dwg.Add (new Point2 (150, 50));

// A cross-shaped block
List<Ent2> bSet = [];
bSet.Add (new E2Poly (layer, Poly.Parse ("M-1,-1 V-3 H1 V-1 H3 V1 H1 V3 H-1 V1 H-3 V-1Z")));
bSet.Add (new E2Point (layer, Point2.Zero));
bSet.Add (new E2Poly (layer, Poly.Circle (Point2.Zero, 2)));
Block2 block = new ("Cross", Point2.Zero, bSet);
dwg.Add (block);
dwg.Add (new E2Insert (dwg, layer, "Cross", new Point2 (30, 70), 0, 5, 5));

// A text label
Style2 style = new ("Std", "Simplex", 0, 1, 0);
dwg.Add (style);
dwg.Add (new E2Text (layer, style, "Nori Demo", new Point2 (50, 10), 8, 0, 0, 1, ETextAlign.BaseLeft));

server.Dwg = dwg;

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
