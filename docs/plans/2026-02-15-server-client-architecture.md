# Server-Client Architecture Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Transform Nori into two reusable libraries — `Nori.Server` (.NET, geometric logic) and `Nori.Client` (TypeScript, WebGPU rendering) — connected via WebSocket, with two Tauri V2 demo apps.

**Architecture:** The server library owns all geometric entities (Dwg2, Model3, Poly, Mesh3, etc.), scene state, and interaction logic. It exposes a typed API that hosts integrate into their own server process. A WebSocket protocol serializes scene state as renderable primitives (lines, points, meshes, text) plus metadata (colors, transforms, layers). The client library is a framework-agnostic TypeScript package that takes a `<canvas>` + server URL, connects via WebSocket, and renders everything with WebGPU using the existing WGSL shaders.

**Tech Stack:**
- Server: .NET 10.0, System.Net.WebSockets, MessagePack for binary serialization
- Client: TypeScript, WebGPU API (native browser), no framework dependency
- Demos: Tauri V2, React (frontend only), .NET sidecar (server)
- Build: pnpm workspace (client + demos), dotnet (server + core)

---

## Phase 1: Clean Up — Remove Platform-Dependent Code

### Task 1.1: Remove Platform-Specific Demo Projects

**Files:**
- Delete: `Demos/macOS/` (entire directory)
- Delete: `Demos/Windows/` (entire directory)
- Delete: `Demos/Linux/` (entire directory)
- Delete: `Demos/iOS/` (entire directory)
- Delete: `Demos/Android/` (entire directory)
- Delete: `Demos/Web/` (entire directory)
- Delete: `Demos/ConShell/` (entire directory)
- Delete: `Demos/BenchShell/` (entire directory)
- Keep: `Demos/Shared/` (reference for scene implementations)
- Modify: `Nori.slnx` — remove all deleted project references

**Step 1: Delete demo directories**

```bash
rm -rf Demos/macOS Demos/Windows Demos/Linux Demos/iOS Demos/Android Demos/Web Demos/ConShell Demos/BenchShell
```

**Step 2: Update Nori.slnx — remove deleted projects**

Remove these lines from the `<Folder Name="/Demos/">` section:
```xml
<Project Path="Demos/macOS/Demos.macOS.csproj" />
<Project Path="Demos/Linux/Demos.Linux.csproj" />
<Project Path="Demos/Web/Demos.Web.csproj" />
<Project Path="Demos/Windows/Demos.Windows.csproj" />
<Project Path="Demos/iOS/Demos.iOS.csproj" />
<Project Path="Demos/Android/Demos.Android.csproj" />
<Project Path="Demos/ConShell/ConShell.csproj" />
<Project Path="Demos/BenchShell/BenchShell.csproj" />
```

Keep:
```xml
<Project Path="Demos/Shared/Demos.Shared.csproj" />
```

**Step 3: Verify build**

```bash
dotnet build Nori.slnx
```
Expected: Build succeeds (Demos/Shared still references Core, Lux, Platform).

**Step 4: Commit**

```bash
git add -A && git commit -m "chore: remove platform-specific demo projects"
```

---

### Task 1.2: Remove Platform Implementations (Keep Abstractions)

**Files:**
- Delete: `Platform.Desktop/` (entire directory — SDL2 implementation)
- Delete: `Platform.Windows/` (entire directory)
- Delete: `Platform.iOS/` (entire directory)
- Delete: `Platform.Android/` (entire directory)
- Delete: `Platform.Web/` (entire directory)
- Delete: `GPU.Web/` (entire directory — Blazor WebGPU interop)
- Keep: `Platform/` (ISurface, IPlatform, IInput, IFontLoader — abstractions)
- Keep: `GPU/` (GPUDevice, GPUSurface, shaders — will be referenced by server for pipeline knowledge)
- Modify: `Nori.slnx` — remove deleted project references

**Step 1: Delete platform implementation directories**

```bash
rm -rf Platform.Desktop Platform.Windows Platform.iOS Platform.Android Platform.Web GPU.Web
```

**Step 2: Update Nori.slnx — remove the `/Platform/` folder entries for deleted projects**

Remove these from the `<Folder Name="/Platform/">` section:
```xml
<Project Path="Platform.Windows/Nori.Platform.Windows.csproj" />
<Project Path="Platform.Desktop/Nori.Platform.Desktop.csproj" />
<Project Path="Platform.Web/Nori.Platform.Web.csproj" />
<Project Path="Platform.iOS/Nori.Platform.iOS.csproj" />
<Project Path="Platform.Android/Nori.Platform.Android.csproj" />
<Project Path="GPU.Web/Nori.GPU.Web.csproj" />
```

**Step 3: Update Test project if it references any deleted projects**

Check `Test/Nori.Test.csproj` for references to deleted projects and remove them.

**Step 4: Verify build**

```bash
dotnet build Nori.slnx
```
Expected: Build succeeds. Core, Lux, GPU, Platform (abstractions), Demos/Shared, Test, Tools all still build.

**Step 5: Commit**

```bash
git add -A && git commit -m "chore: remove platform implementations, keep abstractions"
```

---

### Task 1.3: Clean Up Orphaned Files

**Files:**
- Delete from repo root: `*.png` screenshots, `console-output.log`, `firebase-debug.log`, `.playwright-mcp/`

**Step 1: Remove orphaned files**

```bash
rm -f nori-*.png console-output.log firebase-debug.log
rm -rf .playwright-mcp
```

**Step 2: Commit**

```bash
git add -A && git commit -m "chore: clean up orphaned files"
```

---

## Phase 2: Protocol Design — Define the Wire Format

### Task 2.1: Design and Implement the Protocol Schema

The protocol must efficiently transmit scene state from server to client. The design follows these principles:

1. **Renderable primitives** cross the wire — not raw entities, not GPU commands
2. **Binary serialization** via MessagePack for performance
3. **Delta updates** — only changed state is sent after initial sync
4. **Bidirectional** — server pushes scene state, client sends interaction events

**Files:**
- Create: `Nori.Server/Protocol/Messages.cs` — All message type definitions
- Create: `Nori.Client/src/protocol/messages.ts` — TypeScript mirror of message types

**Step 1: Create the Nori.Server project and protocol messages**

Create `Nori.Server/Nori.Server.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutDir>..\Bin</OutDir>
    <AssemblyName>Nori.Server</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MessagePack" Version="3.1.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Core\Nori.Core.csproj" />
    <ProjectReference Include="..\Lux\Nori.Lux.csproj" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Tools\Generate\Nori.Gen.csproj"
      ReferenceOutputAssembly="false" OutputItemType="Analyzer" />
  </ItemGroup>
</Project>
```

Create `Nori.Server/Protocol/Messages.cs` defining all wire message types:

```csharp
// Message envelope: every message has a Type discriminator + payload
// Server → Client messages:
//   SceneInit      — full scene snapshot (sent on connect)
//   SceneDelta     — incremental update (entity add/remove/modify)
//   EntityData     — renderable primitive data for one entity
//
// Client → Server messages:
//   ViewState      — camera position, zoom, viewport size
//   Interaction    — click, hover, selection events
//   Command        — application-level commands
```

The key message types and their payloads:

| Message | Direction | Payload |
|---------|-----------|---------|
| `SceneInit` | S→C | Scene type (2D/3D), background color, bounds, full entity list |
| `EntityAdd` | S→C | Entity ID, type, render data (vertices, indices, colors, transforms) |
| `EntityRemove` | S→C | Entity ID |
| `EntityUpdate` | S→C | Entity ID, changed fields only |
| `ViewState` | C→S | Viewport size, zoom, pan, rotation (3D) |
| `Pick` | C→S | Pixel position for hit-testing |
| `PickResult` | S→C | Entity ID (or null) at picked position |
| `Interaction` | C→S | Event type (click/hover/drag), position, modifiers |

Render data format per entity — the server pre-tessellates geometry into renderable primitives:

| Primitive | Data |
|-----------|------|
| Lines2D | `Vec2F[]` pairs |
| Lines3D | `Vec3F[]` pairs |
| Beziers2D | `Vec2F[]` (4-point groups) |
| Points2D | `Vec2F[]` |
| Points3D | `Vec3F[]` |
| Mesh3D | `float[] vertices`, `int[] indices`, `int[] wireIndices` |
| Text | string, position, alignment, font info |
| Fill | `Vec2F[] vertices`, `int[] indices`, `Bound2` |

Each entity's render data includes: color, line width, line type, point size, shade mode, transform matrix.

**Step 2: Implement C# message types with MessagePack attributes**

See detailed implementation in Task 2.1 implementation below.

**Step 3: Run build to verify**

```bash
dotnet build Nori.Server/Nori.Server.csproj
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: define server-client protocol message types"
```

---

### Task 2.2: Implement Scene Serializer (Server-Side)

The server needs to convert Nori's scene graph (VNode tree with Lux draw calls) into the wire protocol format. This is the critical bridge between the existing rendering pipeline and the network protocol.

**Strategy:** Create a `SceneSerializer` that walks the VNode tree the same way `VNode.Render()` does, but instead of issuing GPU calls, it captures the draw commands into protocol messages.

**Files:**
- Create: `Nori.Server/Protocol/SceneSerializer.cs`
- Create: `Nori.Server/Protocol/RenderCapture.cs` — captures Lux.Draw* calls into protocol primitives

**Step 1: Implement RenderCapture — an IGPU implementation that captures draw commands**

`RenderCapture` implements `IGPU` but instead of sending commands to a GPU, it records them as protocol messages. This is the "virtual GPU" approach:

```csharp
namespace Nori;

/// <summary>Captures Lux draw calls into protocol render data instead of sending to GPU</summary>
class RenderCapture : IGPU {
   // Instead of rendering, we accumulate EntityRenderData for each VNode
   // The existing Lux pipeline (VNode.Render → SetAttributes → Draw → Shader → RBatch → IGPU)
   // flows through unchanged — we just capture at the IGPU level
   ...
}
```

**Step 2: Implement SceneSerializer**

```csharp
namespace Nori;

/// <summary>Serializes a Scene into protocol messages for transmission to clients</summary>
public class SceneSerializer {
   /// <summary>Serialize the entire scene into a SceneInit message</summary>
   public SceneInitMsg Serialize (Scene scene) { ... }

   /// <summary>Compute delta between previous and current state</summary>
   public SceneDeltaMsg? ComputeDelta () { ... }
}
```

**Step 3: Write tests**

```bash
dotnet run --project Test/Nori.Test.csproj
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: implement scene serializer and render capture"
```

---

## Phase 3: Nori.Server — The .NET Library

### Task 3.1: Core Server Class — NoriServer

The main entry point for consumers integrating Nori.Server into their host application.

**Files:**
- Create: `Nori.Server/NoriServer.cs` — public API surface
- Create: `Nori.Server/NoriSession.cs` — per-client session management
- Create: `Nori.Server/NoriServerConfig.cs` — configuration options

**Step 1: Define the public API**

```csharp
namespace Nori;

/// <summary>Configuration for a NoriServer instance</summary>
public class NoriServerConfig {
   /// <summary>WebSocket endpoint path (default: "/nori")</summary>
   public string Path { get; set; } = "/nori";
   /// <summary>Maximum number of concurrent client sessions</summary>
   public int MaxSessions { get; set; } = 16;
}

/// <summary>Nori rendering server — integrable into any .NET host application</summary>
/// Usage:
///   var server = new NoriServer(config);
///   server.Scene = myScene;         // Set the active scene
///   app.UseWebSockets();
///   app.Map("/nori", server.HandleWebSocket);  // Wire into ASP.NET pipeline
///
/// Or for standalone:
///   await server.StartAsync("ws://localhost:5100/nori");
public class NoriServer {
   public NoriServer (NoriServerConfig? config = null);

   /// <summary>The active scene being served to clients</summary>
   public Scene? Scene { get; set; }

   /// <summary>Handle an incoming WebSocket connection (for ASP.NET integration)</summary>
   public Task HandleWebSocket (HttpContext context);

   /// <summary>Start a standalone WebSocket server (for console app / sidecar usage)</summary>
   public Task StartAsync (string url, CancellationToken ct = default);

   /// <summary>Stop the server</summary>
   public Task StopAsync ();

   /// <summary>Event raised when a client connects</summary>
   public event Action<NoriSession>? OnClientConnected;

   /// <summary>Event raised when a client sends an interaction</summary>
   public event Action<NoriSession, InteractionMsg>? OnInteraction;

   /// <summary>Number of active client sessions</summary>
   public int SessionCount { get; }
}
```

**Step 2: Implement NoriSession**

```csharp
namespace Nori;

/// <summary>Represents a connected client session</summary>
public class NoriSession {
   /// <summary>Unique session identifier</summary>
   public string Id { get; }

   /// <summary>Send a scene update to this client</summary>
   internal Task SendAsync (byte[] data, CancellationToken ct);

   /// <summary>The client's current view state (viewport, zoom, camera)</summary>
   public ViewStateMsg? ViewState { get; }
}
```

**Step 3: Implement WebSocket message loop**

The server:
1. On connect → serialize full scene → send `SceneInit`
2. Scene changes → compute delta → send `SceneDelta` to all sessions
3. Receive client messages → dispatch to event handlers

**Step 4: Add to solution**

Update `Nori.slnx`:
```xml
<Project Path="Nori.Server/Nori.Server.csproj" />
```

**Step 5: Build and test**

```bash
dotnet build Nori.slnx
```

**Step 6: Commit**

```bash
git add -A && git commit -m "feat: implement NoriServer with WebSocket session management"
```

---

### Task 3.2: Scene Change Detection and Push

The server must detect when the scene changes (entity added/removed/modified, transform changed) and push deltas to connected clients.

**Files:**
- Modify: `Nori.Server/NoriServer.cs` — add scene observation
- Modify: `Nori.Server/Protocol/SceneSerializer.cs` — add delta computation

**Step 1: Hook into Nori's reactive system**

Nori entities implement `IObservable<EProp>` and collections use `AList<T>` with `ListChange`. Subscribe to these to detect changes:

```csharp
// Watch entity property changes
entity.Subscribe(prop => OnEntityChanged(entity, prop));

// Watch collection changes
dwg.Entities.Subscribe(change => OnEntitiesChanged(change));
```

**Step 2: Implement change batching**

Batch changes within a frame (16ms window) to avoid flooding clients:
```csharp
// Collect changes during the frame
// On frame end → compute delta → broadcast to all sessions
```

**Step 3: Test with a Dwg2 scene**

Create a test that adds/removes entities and verifies delta messages are generated correctly.

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: scene change detection and delta broadcasting"
```

---

## Phase 4: Nori.Client — TypeScript WebGPU Renderer

### Task 4.1: Project Setup

**Files:**
- Create: `Nori.Client/package.json`
- Create: `Nori.Client/tsconfig.json`
- Create: `Nori.Client/src/index.ts` — public API surface

**Step 1: Initialize the package**

```bash
mkdir -p Nori.Client/src/{engine,protocol}
cd Nori.Client
pnpm init
pnpm add -D typescript @webgpu/types
```

`package.json`:
```json
{
  "name": "@nori/renderer",
  "version": "0.1.0",
  "type": "module",
  "main": "dist/index.js",
  "types": "dist/index.d.ts",
  "files": ["dist/", "shaders/"],
  "scripts": {
    "build": "tsc",
    "dev": "tsc --watch"
  }
}
```

**Step 2: Define the public API**

`src/index.ts`:
```typescript
export interface NoriRendererConfig {
  /** Canvas element to render into */
  canvas: HTMLCanvasElement;
  /** WebSocket URL of the Nori server */
  serverUrl: string;
  /** Background color (CSS format) */
  backgroundColor?: string;
}

export class NoriRenderer {
  constructor(config: NoriRendererConfig);

  /** Connect to the server and start rendering */
  connect(): Promise<void>;

  /** Disconnect from the server */
  disconnect(): void;

  /** Current connection state */
  readonly connected: boolean;

  /** Camera controls */
  zoom(factor: number, center?: { x: number; y: number }): void;
  pan(dx: number, dy: number): void;
  resetView(): void;

  /** For 3D scenes: orbit camera */
  orbit(xRot: number, zRot: number): void;

  /** Event callbacks */
  onEntityPicked?: (entityId: number | null, position: { x: number; y: number; z: number }) => void;
  onConnected?: () => void;
  onDisconnected?: () => void;
  onSceneLoaded?: () => void;

  /** Clean up resources */
  dispose(): void;
}
```

**Step 3: Commit**

```bash
git add -A && git commit -m "feat: initialize Nori.Client TypeScript package with public API"
```

---

### Task 4.2: WebGPU Rendering Engine

Port the rendering pipeline from C#/Silk.NET to TypeScript/WebGPU browser API. The WGSL shaders are already cross-platform — they work in both native WGPU and browser WebGPU.

**Files:**
- Create: `Nori.Client/src/engine/gpu-device.ts` — WebGPU device initialization
- Create: `Nori.Client/src/engine/pipeline-factory.ts` — pipeline compilation (port from `GPU/PipelineFactory.cs`)
- Create: `Nori.Client/src/engine/renderer.ts` — frame rendering loop
- Create: `Nori.Client/src/engine/scene-graph.ts` — client-side scene representation
- Create: `Nori.Client/src/engine/buffers.ts` — vertex/index buffer management
- Copy: `GPU/Shaders/*.wgsl` → `Nori.Client/shaders/` (all 18 shaders, unmodified)

**Step 1: Copy WGSL shaders**

```bash
cp -r GPU/Shaders/*.wgsl Nori.Client/shaders/
```

The shaders are already valid WGSL and work in browser WebGPU. No modifications needed.

**Step 2: Implement gpu-device.ts**

Port from `GPU/GPUDevice.cs` — request adapter, create device, configure canvas context:

```typescript
export class GPUDeviceManager {
  device!: GPUDevice;
  context!: GPUCanvasContext;
  format!: GPUTextureFormat;

  async init(canvas: HTMLCanvasElement): Promise<void> {
    const adapter = await navigator.gpu.requestAdapter();
    this.device = await adapter!.requestDevice();
    this.context = canvas.getContext('webgpu')!;
    this.format = navigator.gpu.getPreferredCanvasFormat();
    this.context.configure({ device: this.device, format: this.format });
  }
}
```

**Step 3: Implement pipeline-factory.ts**

Port from `GPU/PipelineFactory.cs` — compile all 21 pipelines from WGSL shaders. Each pipeline maps to an `EPipeline` enum value with specific blend/depth/stencil state baked in.

**Step 4: Implement renderer.ts**

The rendering loop:
1. Each frame, iterate over all renderable entities received from server
2. For each entity, bind the appropriate pipeline, upload vertex data, set uniforms, draw
3. Sort by Z-level (same as `RBatch.IssueAll()` in Lux)

**Step 5: Implement scene-graph.ts**

Client-side scene representation — stores entities received from server, manages transforms, handles camera state:

```typescript
export class ClientScene {
  readonly entities: Map<number, RenderableEntity>;
  readonly sceneType: '2d' | '3d';
  bounds: { min: number[]; max: number[] };
  backgroundColor: [number, number, number, number];

  // Camera state (managed client-side)
  zoom: number;
  pan: [number, number];
  viewpoint: [number, number]; // For 3D: xRot, zRot

  /** Compute the projection matrix (port of Scene2/Scene3.ComputeXfms) */
  computeTransform(viewport: [number, number]): Float32Array;
}
```

**Step 6: Verify rendering with a hardcoded test scene**

Create a simple test HTML page that initializes the renderer with test data (bypassing the WebSocket connection) to verify the pipeline works.

**Step 7: Commit**

```bash
git add -A && git commit -m "feat: implement WebGPU rendering engine with pipeline factory"
```

---

### Task 4.3: WebSocket Protocol Client

**Files:**
- Create: `Nori.Client/src/protocol/connection.ts` — WebSocket management with reconnection
- Create: `Nori.Client/src/protocol/messages.ts` — TypeScript message types (mirror of C# Messages.cs)
- Create: `Nori.Client/src/protocol/deserializer.ts` — MessagePack deserialization

**Step 1: Add msgpack dependency**

```bash
cd Nori.Client && pnpm add @msgpack/msgpack
```

**Step 2: Implement message types**

Mirror the C# message types in TypeScript:

```typescript
export enum MessageType {
  SceneInit = 1,
  EntityAdd = 2,
  EntityRemove = 3,
  EntityUpdate = 4,
  ViewState = 10,
  Pick = 11,
  PickResult = 12,
  Interaction = 13,
}

export interface SceneInitMsg {
  type: MessageType.SceneInit;
  sceneType: '2d' | '3d';
  bgColor: [number, number, number, number];
  bounds: number[];
  entities: EntityDataMsg[];
}
// ... etc
```

**Step 3: Implement WebSocket connection**

```typescript
export class NoriConnection {
  constructor(url: string);
  connect(): Promise<void>;
  disconnect(): void;
  send(msg: ClientMessage): void;
  onMessage?: (msg: ServerMessage) => void;
  onDisconnect?: () => void;
}
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: implement WebSocket protocol client with MessagePack"
```

---

### Task 4.4: User Interaction (Input Handling)

**Files:**
- Create: `Nori.Client/src/engine/input.ts` — mouse/keyboard/touch event handling

**Step 1: Implement input handler**

Handle:
- Mouse wheel → zoom (same formula as `Scene.Zoom()`)
- Mouse drag → pan (2D) or orbit (3D)
- Click → pick request to server
- Hover → optional hover feedback

The client manages camera state locally (no round-trip for zoom/pan). Only interaction events that need server-side logic (pick, selection) are sent to the server.

**Step 2: Commit**

```bash
git add -A && git commit -m "feat: implement client-side input handling for zoom/pan/orbit"
```

---

## Phase 5: Demo 1 — Single Executable (Tauri V2)

### Task 5.1: Tauri V2 Project Setup

**Files:**
- Create: `Demos/Single/` — Tauri V2 project with React frontend
- Create: `Demos/Single/src-tauri/` — Tauri Rust backend
- Create: `Demos/Single/src/` — React frontend using `@nori/renderer`

**Step 1: Create the Tauri project**

```bash
cd Demos && pnpm create tauri-app Single --template react-ts --manager pnpm
```

**Step 2: Configure .NET sidecar**

In `src-tauri/tauri.conf.json`, configure the Nori.Server .NET process as a sidecar:

```json
{
  "bundle": {
    "externalBin": ["binaries/nori-server"]
  }
}
```

Create a minimal .NET console app that hosts NoriServer:

Create `Demos/Single/sidecar/Program.cs`:
```csharp
using Nori;

NoriServer server = new (new NoriServerConfig { Path = "/nori" });
// Load a demo scene
server.Scene = DemoScenes.CreateDefault ();
await server.StartAsync ("ws://127.0.0.1:5100/nori");
```

Create `Demos/Single/sidecar/Sidecar.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Nori.Server\Nori.Server.csproj" />
    <ProjectReference Include="..\..\..\Demos\Shared\Demos.Shared.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Wire up React frontend**

`src/App.tsx`:
```tsx
import { useEffect, useRef } from 'react';
import { NoriRenderer } from '@nori/renderer';

function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const renderer = new NoriRenderer({
      canvas: canvasRef.current!,
      serverUrl: 'ws://127.0.0.1:5100/nori',
    });
    renderer.connect();
    return () => renderer.dispose();
  }, []);

  return <canvas ref={canvasRef} style={{ width: '100%', height: '100%' }} />;
}
```

**Step 4: Add Tauri sidecar launch logic**

In the Tauri Rust backend, launch the .NET sidecar on app start and kill it on exit.

**Step 5: Test locally**

```bash
cd Demos/Single && pnpm tauri dev
```

**Step 6: Commit**

```bash
git add -A && git commit -m "feat: Demo 1 — Single executable Tauri V2 app with embedded server"
```

---

### Task 5.2: Demo UI — React Scene Browser

**Files:**
- Create: `Demos/Single/src/components/` — shared React UI components

Build a demo UI with:
- Scene selector (dropdown to switch between demo scenes from `Demos/Shared/`)
- Viewport controls (zoom to extents, toggle 2D/3D)
- Entity info panel (shows picked entity details)
- FPS counter
- Connection status indicator

This UI is shared between Demo 1 and Demo 2 (extracted as a shared package later or just copied).

**Step 1: Implement scene selector component**

**Step 2: Implement viewport controls**

**Step 3: Implement entity info panel**

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: Demo UI — React scene browser with controls"
```

---

## Phase 6: Demo 2 — Networked (Separate Server + Client)

### Task 6.1: Standalone Server Executable

**Files:**
- Create: `Demos/Networked/Server/Program.cs`
- Create: `Demos/Networked/Server/Server.csproj`

**Step 1: Create minimal server console app**

```csharp
using Nori;

string url = args.Length > 0 ? args[0] : "ws://0.0.0.0:5100/nori";
Console.WriteLine ($"Nori Server starting on {url}");

NoriServer server = new ();
server.Scene = DemoScenes.CreateDefault ();
server.OnClientConnected += session =>
   Console.WriteLine ($"Client connected: {session.Id}");

await server.StartAsync (url);
```

This demonstrates how simple it is to integrate `Nori.Server` into any .NET application — just reference the NuGet package and call `StartAsync`.

**Step 2: Build and test**

```bash
dotnet run --project Demos/Networked/Server/Server.csproj
```

**Step 3: Commit**

```bash
git add -A && git commit -m "feat: Demo 2 — standalone Nori server console app"
```

---

### Task 6.2: Networked Client (Tauri V2)

**Files:**
- Create: `Demos/Networked/Client/` — Tauri V2 project (same React UI as Demo 1)

**Step 1: Create Tauri project (no sidecar)**

Same React frontend as Demo 1, but:
- No .NET sidecar
- Server URL is configurable (defaults to `ws://localhost:5100/nori`)
- Settings panel to change server URL

**Step 2: Add server URL configuration UI**

Add a connection dialog that lets users enter the server address.

**Step 3: Test end-to-end**

Terminal 1:
```bash
dotnet run --project Demos/Networked/Server/Server.csproj
```

Terminal 2:
```bash
cd Demos/Networked/Client && pnpm tauri dev
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: Demo 2 — networked Tauri V2 client with configurable server URL"
```

---

### Task 6.3: Update Solution File

**Files:**
- Modify: `Nori.slnx` — add all new projects

**Step 1: Update Nori.slnx**

Final solution structure:
```xml
<Solution>
  <Project Path="Core/Nori.Core.csproj" />
  <Project Path="Platform/Nori.Platform.csproj" />
  <Project Path="GPU/Nori.GPU.csproj" />
  <Project Path="Lux/Nori.Lux.csproj" />
  <Project Path="Nori.Server/Nori.Server.csproj" />
  <Folder Name="/Demos/">
    <Project Path="Demos/Shared/Demos.Shared.csproj" />
    <Project Path="Demos/Single/sidecar/Sidecar.csproj" />
    <Project Path="Demos/Networked/Server/Server.csproj" />
  </Folder>
  <Folder Name="/Test/">
    <Project Path="Test/Nori.Test.csproj" />
  </Folder>
  <Folder Name="/Tools/">
    <Project Path="Tools/Console/Nori.Con.csproj" />
    <Project Path="Tools/Cover/Nori.Cover.csproj" />
    <Project Path="Tools/Doc/Nori.Doc.csproj" />
    <Project Path="Tools/Generate/Nori.Gen.csproj" />
  </Folder>
</Solution>
```

**Step 2: Verify full build**

```bash
dotnet build Nori.slnx
```

**Step 3: Commit**

```bash
git add -A && git commit -m "chore: update solution file with server-client architecture"
```

---

## Final Directory Structure

```
Nori/
├── Core/                    # Geometry, math, entities, I/O (unchanged)
├── Lux/                     # Rendering abstractions, scene graph (unchanged)
├── GPU/                     # WebGPU bindings via Silk.NET (unchanged, used by server for reference)
│   └── Shaders/*.wgsl       # Shared shaders (copied to Nori.Client)
├── Platform/                # ISurface, IPlatform, IInput (abstractions only)
├── Nori.Server/             # .NET library — integrable into any host
│   ├── Nori.Server.csproj
│   ├── NoriServer.cs        # Public API: StartAsync, Scene, HandleWebSocket
│   ├── NoriSession.cs       # Per-client session
│   ├── NoriServerConfig.cs  # Configuration
│   └── Protocol/
│       ├── Messages.cs      # Wire message types (MessagePack)
│       ├── SceneSerializer.cs
│       └── RenderCapture.cs
├── Nori.Client/             # TypeScript npm package — framework-agnostic
│   ├── package.json         # @nori/renderer
│   ├── shaders/             # WGSL shaders (copied from GPU/Shaders/)
│   └── src/
│       ├── index.ts          # Public API: NoriRenderer class
│       ├── engine/
│       │   ├── gpu-device.ts
│       │   ├── pipeline-factory.ts
│       │   ├── renderer.ts
│       │   ├── scene-graph.ts
│       │   ├── buffers.ts
│       │   └── input.ts
│       └── protocol/
│           ├── connection.ts
│           ├── messages.ts
│           └── deserializer.ts
├── Demos/
│   ├── Shared/              # Demo scene implementations (reference)
│   ├── Single/              # Tauri V2 — embedded server + client
│   │   ├── src-tauri/       # Rust backend (launches .NET sidecar)
│   │   ├── sidecar/         # .NET console app hosting NoriServer
│   │   └── src/             # React UI using @nori/renderer
│   └── Networked/
│       ├── Server/          # Standalone .NET console app
│       └── Client/          # Tauri V2 client (no sidecar)
├── Test/                    # Test project (unchanged)
└── Tools/                   # Gen, Doc, Cover, Console (unchanged)
```

## Execution Order and Dependencies

```
Task 1.1 ─→ Task 1.2 ─→ Task 1.3          (Phase 1: sequential cleanup)
                              │
                              ├─→ Task 2.1 ─→ Task 2.2    (Phase 2: protocol)
                              │                   │
                              │                   ├─→ Task 3.1 ─→ Task 3.2    (Phase 3: server)
                              │                   │
                              │                   └─→ Task 4.1 ─→ Task 4.2 ─→ Task 4.3 ─→ Task 4.4  (Phase 4: client)
                              │                                                               │
                              │                   ┌───────────────────────────────────────────┘
                              │                   │
                              └───────────────────├─→ Task 5.1 ─→ Task 5.2    (Phase 5: Demo 1)
                                                  │
                                                  └─→ Task 6.1 ─→ Task 6.2 ─→ Task 6.3  (Phase 6: Demo 2)
```

## Key Risks and Mitigations

1. **Render fidelity**: The TypeScript renderer must match the C# renderer output. Mitigation: use the exact same WGSL shaders; port the uniform computation logic faithfully from `Scene2/Scene3.ComputeXfms` and each shader's `SnapUniforms`.

2. **Protocol performance**: Scene with many entities could produce large initial payloads. Mitigation: binary MessagePack, delta updates, optional geometry LOD.

3. **Text rendering**: The C# side uses FreeType for glyph rasterization into texture atlases. The TypeScript side needs a browser-native equivalent. Mitigation: use Canvas 2D to rasterize font glyphs into a texture atlas at startup, matching the existing TextPx/Text2D/Text3D shader input format.

4. **Pick support**: Currently pick uses GPU readback (render with false colors, read pixels). In the server-client model, pick requests go to the server which can use CPU-based ray casting against the scene. Mitigation: implement server-side picking using bounding box intersection (Bound2/Bound3 already available on all entities).
