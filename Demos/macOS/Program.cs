// ────── ╔╗                                                                            DEMOS.MACOS
// ╔═╦╦═╦╦╬╣ Program.cs
// ║║║║╬║╔╣║ Entry point for the macOS SDL2 demo application hosting cross-platform scenes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
using System.Reactive.Linq;

Lib.Init ();

// Create SDL platform and window surface
using SDLPlatform platform = new ();
ISurface surface = platform.CreateSurface ("Nori Demos \u2014 macOS", 1280, 800);

// Create GPU backend and initialize the Lux renderer
using NativeGPU gpu = NativeGPU.Create (surface.NativeHandle);
Lux.Init (gpu, surface);

// Track current demo index
int currentDemo = 0;

// Print available demos to the console
Console.WriteLine ("Nori Demos \u2014 macOS (SDL2 + WebGPU)");
Console.WriteLine (new string ('-', 40));
for (int i = 0; i < DemoRegistry.Scenes.Length; i++)
   Console.WriteLine ($"  [{i,2}] {DemoRegistry.Scenes[i].Name}");
Console.WriteLine ();
Console.WriteLine ("Keys: Left/Right arrows to cycle, 0-9 to select directly");
Console.WriteLine ();

// When Lux is ready, set up scene manipulation and load the first demo
Lux.OnReady.Subscribe (_ => {
   SceneManipulator manipulator = new (platform.Input);
   SwitchDemo (0);
});

// Subscribe to keyboard input for demo switching
platform.Input.Keys.Where (k => k.State == EKeyState.Pressed).Subscribe (k => {
   int count = DemoRegistry.Scenes.Length;
   if (k.Key == EKey.Right)
      SwitchDemo ((currentDemo + 1) % count);
   else if (k.Key == EKey.Left)
      SwitchDemo ((currentDemo - 1 + count) % count);
   else if (k.Key >= EKey.D0 && k.Key <= EKey.D9) {
      int index = k.Key - EKey.D0;
      if (index < count) SwitchDemo (index);
   }
});

// Run the SDL event loop
platform.Run (dt => { });

// Switch to a demo scene by index
void SwitchDemo (int index) {
   currentDemo = index;
   (string name, Func<Scene> factory) = DemoRegistry.Scenes[index];
   Scene scene = factory ();
   Lux.UIScene = scene;
   Console.WriteLine ($"Demo [{index,2}]: {name}");
}
