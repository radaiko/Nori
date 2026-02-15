// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Tauri sidecar — launches a NoriServer with demo scene routing
// ╚╩═╩═╩╝╚╝ ──────────────────────────────────────────────────────────────
using Nori;

// Parse args: [demoName] [port]
string demoName = args.Length > 0 ? args[0] : "dwg";
int port = args.Length > 1 && int.TryParse (args[1], out int p) ? p : 5100;
string url = $"http://localhost:{port}/";

Console.WriteLine ($"Nori Server starting on {url}");

NoriServer server = new ();
LoadDemo (server, demoName);

server.OnCommand += (session, cmd) => {
   if (cmd.Name == "demo") {
      Console.WriteLine ($"Switching to demo: {cmd.Arg}");
      LoadDemo (server, cmd.Arg);
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

static void LoadDemo (NoriServer server, string name) {
   server.Dwg = null;
   server.Model = null;
   SceneInitMsg scene = DemoScenes.Build (name);
   server.SetCustomScene (scene);
}
