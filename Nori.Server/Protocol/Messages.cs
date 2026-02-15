// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Messages.cs
// ║║║║╬║╔╣║ Protocol message types for server-client communication
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using MessagePack;

namespace Nori;

// Enums ══════════════════════════════════════════════════════════════════════════

/// <summary>Discriminator for all server-to-client wire messages</summary>
public enum EMsgType : byte {
   SceneInit = 1,
   EntityAdd = 2,
   EntityRemove = 3,
   EntityUpdate = 4,
   PickResult = 5,
}

/// <summary>Scene type discriminator</summary>
public enum ESceneType : byte { Scene2D = 1, Scene3D = 2 }

/// <summary>Render primitive type discriminator</summary>
public enum EPrimType : byte {
   Lines2D = 1, Lines3D = 2, Beziers2D = 3,
   Points2D = 4, Points3D = 5,
   Mesh3D = 6,
   Text2D = 7, Text3D = 8, TextPx = 9,
   Fill2D = 10, Triangles2D = 11, Quads2D = 12,
}

/// <summary>Discriminator for client-to-server messages</summary>
public enum EClientMsgType : byte {
   ViewState = 10,
   Pick = 11,
   Interaction = 12,
   Command = 13,
}

/// <summary>Interaction event type</summary>
public enum EInteractionType : byte {
   Click = 1, DoubleClick = 2, Hover = 3,
   DragStart = 4, DragMove = 5, DragEnd = 6,
}

// Server → Client messages ═══════════════════════════════════════════════════════

/// <summary>Full scene snapshot sent on client connect</summary>
[MessagePackObject]
public class SceneInitMsg {
   [Key (0)] public ESceneType SceneType { get; set; }
   [Key (1)] public byte[] BgColor { get; set; } = [];    // RGBA bytes
   [Key (2)] public float[] Bounds { get; set; } = [];     // [minX,minY,maxX,maxY] or [minX,minY,minZ,maxX,maxY,maxZ]
   [Key (3)] public float[] Transforms { get; set; } = []; // Flattened Mat4F array (16 floats per transform)
   [Key (4)] public EntityDataMsg[] Entities { get; set; } = [];
}

/// <summary>One entity's complete render data</summary>
[MessagePackObject]
public class EntityDataMsg {
   [Key (0)] public int Id { get; set; }
   [Key (1)] public RenderPrimitive[] Primitives { get; set; } = [];
}

/// <summary>A single renderable primitive with its draw attributes</summary>
/// Vertex data is flattened floats whose interpretation depends on Type:
/// Lines2D/Points2D/Beziers2D/Fill2D/Triangles2D/Quads2D: pairs of (x,y)
/// Lines3D/Points3D: triples of (x,y,z)
/// Mesh3D: interleaved position+normal (x,y,z,nx,ny,nz per vertex)
/// Text2D: single (x,y) position
/// Text3D: single (x,y,z) position
/// TextPx: single (x,y) pixel position
[MessagePackObject]
public class RenderPrimitive {
   [Key (0)] public EPrimType Type { get; set; }
   [Key (1)] public float[] Data { get; set; } = [];                     // Vertex data (floats)
   [Key (2)] public int[]? Indices { get; set; }                         // Index data (optional, for Mesh3D triangles)
   [Key (3)] public byte[] Color { get; set; } = [255, 255, 255, 255];   // RGBA
   [Key (4)] public float LineWidth { get; set; } = 2f;
   [Key (5)] public byte LineType { get; set; }                          // ELineType cast to byte
   [Key (6)] public float LTScale { get; set; } = 30f;
   [Key (7)] public float PointSize { get; set; } = 4f;
   [Key (8)] public int TransformIndex { get; set; }
   [Key (9)] public int ZLevel { get; set; }
   [Key (10)] public byte ShadeMode { get; set; }                        // EShadeMode cast to byte
   // Text primitives
   [Key (11)] public string? Text { get; set; }
   [Key (12)] public byte TextAlign { get; set; }                        // ETextAlign cast to byte
   // Mesh primitives (wire edge indices separate from triangle indices)
   [Key (13)] public int[]? WireIndices { get; set; }
   // Fill primitives
   [Key (14)] public float[]? BoundData { get; set; }                    // [minX, minY, maxX, maxY]
}

/// <summary>Entity added to scene</summary>
[MessagePackObject]
public class EntityAddMsg {
   [Key (0)] public EntityDataMsg Entity { get; set; } = new ();
}

/// <summary>Entity removed from scene</summary>
[MessagePackObject]
public class EntityRemoveMsg {
   [Key (0)] public int EntityId { get; set; }
}

/// <summary>Entity updated (full replacement of render data)</summary>
[MessagePackObject]
public class EntityUpdateMsg {
   [Key (0)] public EntityDataMsg Entity { get; set; } = new ();
}

/// <summary>Result of a pick operation</summary>
[MessagePackObject]
public class PickResultMsg {
   [Key (0)] public int EntityId { get; set; }       // -1 if nothing picked
   [Key (1)] public float[] Position { get; set; } = []; // World position [x,y] or [x,y,z]
}

// Client → Server messages ═══════════════════════════════════════════════════════

/// <summary>Client's current view state (viewport, zoom, pan, rotation)</summary>
[MessagePackObject]
public class ViewStateMsg {
   [Key (0)] public int ViewportW { get; set; }
   [Key (1)] public int ViewportH { get; set; }
   [Key (2)] public double Zoom { get; set; } = 1;
   [Key (3)] public float PanX { get; set; }
   [Key (4)] public float PanY { get; set; }
   [Key (5)] public double XRot { get; set; }   // For 3D scenes
   [Key (6)] public double ZRot { get; set; }   // For 3D scenes
}

/// <summary>Pick request from client (screen coordinates)</summary>
[MessagePackObject]
public class PickMsg {
   [Key (0)] public int X { get; set; }
   [Key (1)] public int Y { get; set; }
}

/// <summary>Interaction event from client</summary>
[MessagePackObject]
public class InteractionMsg {
   [Key (0)] public EInteractionType Type { get; set; }
   [Key (1)] public float X { get; set; }         // World coordinates
   [Key (2)] public float Y { get; set; }
   [Key (3)] public float Z { get; set; }
   [Key (4)] public int Modifiers { get; set; }    // Shift=1, Ctrl=2, Alt=4
}

/// <summary>Command from client (e.g. switch demo, toggle option)</summary>
[MessagePackObject]
public class CommandMsg {
   [Key (0)] public string Name { get; set; } = "";
   [Key (1)] public string Arg { get; set; } = "";
}
