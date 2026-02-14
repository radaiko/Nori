// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Blazor WASM entry point — initializes Nori platform, GPU and Lux for browser demos
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Net.Http;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nori;

// Start the Blazor WASM host so the .NET runtime is available for JSImport/JSExport
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault (args);
WebAssemblyHost host = builder.Build ();

// Import JS modules so their functions are available to JSImport declarations.
// "nori-platform" maps to Platform.Web's nori-platform.js (served as static web asset).
// "nori-demos" maps to this project's nori-demos.js helper module.
// "nori" maps to GPU.Web's nori-gpu.js WebGPU implementation.
await JSHost.ImportAsync ("nori-platform",
   "../_content/Nori.Platform.Web/nori-platform.js");
await JSHost.ImportAsync ("nori-demos",
   "../nori-demos.js");
await JSHost.ImportAsync ("nori",
   "../_content/Nori.GPU.Web/nori-gpu.js");

// Pre-fetch data assets into the WASM virtual filesystem before initializing Nori.
// This sets NORIROOT so all file paths resolve to /data/... in the vfs.
Environment.SetEnvironmentVariable ("NORIROOT", WebAssetLoader.VfsRoot);
HttpClient http = new () { BaseAddress = new Uri (builder.HostEnvironment.BaseAddress) };
await WebAssetLoader.LoadAsync (http);

// Initialize the Nori core library (now with vfs assets available)
Lib.Init ();
Lux2.Init ();

// Set up the web platform and rendering surface
WebPlatform platform = new ("noriCanvas");
ISurface surface = platform.CreateSurface ("Nori Demos", 1280, 800);

// Initialize the WebGPU backend
await WebGPU.Init ("noriCanvas");
WebGPU gpu = new ();

// Initialize the Lux rendering engine with the GPU backend and surface
Lux.Init (gpu, surface);
TraceVN.TextColor = Color4.Yellow;
SceneManipulator manipulator = new (platform.Input);

// Once Lux reports ready, initialize the demo application
Lux.OnReady.Subscribe (_ => DemoApp.Init ());

// Start the animation frame loop — WebPlatform uses requestAnimationFrame
// so this returns immediately and renders via JS callbacks.
// Lux.Tick checks the dirty flag and renders when needed.
platform.Run (dt => Lux.Tick ());

await host.RunAsync ();
