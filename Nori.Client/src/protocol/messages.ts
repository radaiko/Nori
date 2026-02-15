// protocol/messages.ts -- TypeScript message types mirroring C# Nori.Server.Protocol.Messages
// MessagePack with [Key(n)] attributes serializes as arrays where index = key number.

// ═══════════════════════════════════════════════════════════════════════════════
// Server → Client message types (EMsgType)
// ═══════════════════════════════════════════════════════════════════════════════
export const MsgType = {
  SceneInit: 1,
  EntityAdd: 2,
  EntityRemove: 3,
  EntityUpdate: 4,
  PickResult: 5,
} as const;

// ═══════════════════════════════════════════════════════════════════════════════
// Client → Server message types (EClientMsgType)
// ═══════════════════════════════════════════════════════════════════════════════
export const ClientMsgType = {
  ViewState: 10,
  Pick: 11,
  Interaction: 12,
  Command: 13,
} as const;

// ═══════════════════════════════════════════════════════════════════════════════
// Interaction types (EInteractionType)
// ═══════════════════════════════════════════════════════════════════════════════
export const InteractionType = {
  Click: 1,
  DoubleClick: 2,
  Hover: 3,
  DragStart: 4,
  DragMove: 5,
  DragEnd: 6,
} as const;

// ═══════════════════════════════════════════════════════════════════════════════
// Scene type (ESceneType)
// ═══════════════════════════════════════════════════════════════════════════════
export const SceneType = {
  Scene2D: 1,
  Scene3D: 2,
} as const;

// ═══════════════════════════════════════════════════════════════════════════════
// Server → Client message interfaces
// ═══════════════════════════════════════════════════════════════════════════════

/** Wire envelope: [type, payload] — wraps all messages with a type discriminator */
export interface MessageEnvelope {
  type: number;
  payload: Uint8Array;
}

/** SceneInitMsg: keys [0..4] — full scene snapshot sent on client connect */
export interface SceneInitMsg {
  sceneType: number;        // ESceneType
  bgColor: Uint8Array;      // RGBA bytes
  bounds: number[];          // 2D: [x0,y0,x1,y1], 3D: [x0,y0,z0,x1,y1,z1]
  transforms: number[];     // Flattened Mat4F array (16 floats per transform)
  entities: EntityDataMsg[];
}

/** EntityDataMsg: keys [0..1] — one entity's complete render data */
export interface EntityDataMsg {
  id: number;
  primitives: RenderPrimitiveMsg[];
}

/** RenderPrimitiveMsg: keys [0..14] — a single renderable primitive with draw attributes */
export interface RenderPrimitiveMsg {
  type: number;                                      // EPrimType (server enum, starts at 1)
  data: Float32Array | Float64Array | number[];      // Vertex data (floats)
  indices: Uint32Array | Int32Array | number[] | null; // Index data (optional, for Mesh3D triangles)
  color: Uint8Array;                                 // RGBA bytes
  lineWidth: number;
  lineType: number;                                  // ELineType
  ltScale: number;
  pointSize: number;
  transformIndex: number;
  zLevel: number;
  shadeMode: number;                                 // EShadeMode
  text: string | null;
  textAlign: number;                                 // ETextAlign
  wireIndices: Uint32Array | Int32Array | number[] | null; // Wire edge indices (separate from triangle indices)
  boundData: Float32Array | Float64Array | number[] | null; // Fill bounds [minX, minY, maxX, maxY]
}

/** EntityAddMsg: key [0] — entity added to scene */
export interface EntityAddMsg {
  entity: EntityDataMsg;
}

/** EntityRemoveMsg: key [0] — entity removed from scene */
export interface EntityRemoveMsg {
  entityId: number;
}

/** EntityUpdateMsg: key [0] — entity updated (full replacement of render data) */
export interface EntityUpdateMsg {
  entity: EntityDataMsg;
}

/** PickResultMsg: keys [0..1] — result of a pick operation */
export interface PickResultMsg {
  entityId: number;    // -1 if nothing picked
  position: number[];  // World position [x,y] or [x,y,z]
}

// ═══════════════════════════════════════════════════════════════════════════════
// Client → Server message interfaces
// ═══════════════════════════════════════════════════════════════════════════════

/** ViewStateMsg: keys [0..6] — client's current view state */
export interface ViewStateMsg {
  viewportW: number;
  viewportH: number;
  zoom: number;
  panX: number;
  panY: number;
  xRot: number;
  zRot: number;
}

/** PickRequestMsg: keys [0..1] — pick request from client (screen coordinates) */
export interface PickRequestMsg {
  x: number;
  y: number;
}

/** InteractionRequestMsg: keys [0..4] — interaction event from client */
export interface InteractionRequestMsg {
  type: number;       // EInteractionType
  x: number;          // World coordinates
  y: number;
  z: number;
  modifiers: number;  // Shift=1, Ctrl=2, Alt=4
}
