// protocol/deserializer.ts -- Convert MessagePack-decoded arrays into typed message objects.
// MessagePack with [Key(n)] attributes serializes objects as arrays where index = key number.

import { decode } from '@msgpack/msgpack';
import type {
  MessageEnvelope,
  SceneInitMsg,
  EntityDataMsg,
  RenderPrimitiveMsg,
  EntityAddMsg,
  EntityRemoveMsg,
  EntityUpdateMsg,
  PickResultMsg,
} from './messages.js';

// ═══════════════════════════════════════════════════════════════════════════════
// Envelope
// ═══════════════════════════════════════════════════════════════════════════════

/** Deserialize a wire envelope: [type(byte), payload(bytes)] */
export function deserializeEnvelope(data: Uint8Array): MessageEnvelope {
  const arr = decode(data) as [number, Uint8Array];
  return { type: arr[0], payload: arr[1] };
}

// ═══════════════════════════════════════════════════════════════════════════════
// Server → Client messages
// ═══════════════════════════════════════════════════════════════════════════════

/** Deserialize a SceneInit payload: [sceneType, bgColor, bounds, transforms, entities] */
export function deserializeSceneInit(payload: Uint8Array): SceneInitMsg {
  const arr = decode(payload) as unknown[];
  return {
    sceneType: arr[0] as number,
    bgColor: toUint8Array(arr[1]),
    bounds: toNumberArray(arr[2]),
    transforms: toNumberArray(arr[3]),
    entities: (arr[4] as unknown[][]).map(deserializeEntityData),
  };
}

/** Deserialize an EntityAdd payload: [entity] */
export function deserializeEntityAdd(payload: Uint8Array): EntityAddMsg {
  const arr = decode(payload) as unknown[];
  return {
    entity: deserializeEntityData(arr[0] as unknown[]),
  };
}

/** Deserialize an EntityRemove payload: [entityId] */
export function deserializeEntityRemove(payload: Uint8Array): EntityRemoveMsg {
  const arr = decode(payload) as unknown[];
  return {
    entityId: arr[0] as number,
  };
}

/** Deserialize an EntityUpdate payload: [entity] */
export function deserializeEntityUpdate(payload: Uint8Array): EntityUpdateMsg {
  const arr = decode(payload) as unknown[];
  return {
    entity: deserializeEntityData(arr[0] as unknown[]),
  };
}

/** Deserialize a PickResult payload: [entityId, position] */
export function deserializePickResult(payload: Uint8Array): PickResultMsg {
  const arr = decode(payload) as unknown[];
  return {
    entityId: arr[0] as number,
    position: toNumberArray(arr[1]),
  };
}

// ═══════════════════════════════════════════════════════════════════════════════
// Inner message structures
// ═══════════════════════════════════════════════════════════════════════════════

/** Deserialize an EntityDataMsg from a decoded array: [id, primitives[]] */
function deserializeEntityData(arr: unknown[]): EntityDataMsg {
  return {
    id: arr[0] as number,
    primitives: (arr[1] as unknown[][]).map(deserializePrimitive),
  };
}

/** Deserialize a RenderPrimitiveMsg from a decoded array: keys [0..14] */
function deserializePrimitive(arr: unknown[]): RenderPrimitiveMsg {
  return {
    type: arr[0] as number,
    data: toNumberArray(arr[1]),
    indices: arr[2] != null ? toIntArray(arr[2]) : null,
    color: toUint8Array(arr[3]),
    lineWidth: arr[4] as number,
    lineType: arr[5] as number,
    ltScale: arr[6] as number,
    pointSize: arr[7] as number,
    transformIndex: arr[8] as number,
    zLevel: arr[9] as number,
    shadeMode: arr[10] as number,
    text: arr[11] as string | null,
    textAlign: arr[12] as number,
    wireIndices: arr[13] != null ? toIntArray(arr[13]) : null,
    boundData: arr[14] != null ? toNumberArray(arr[14]) : null,
  };
}

// ═══════════════════════════════════════════════════════════════════════════════
// Helpers — MessagePack can decode numbers as typed arrays or plain arrays,
// so we normalize to standard JS arrays for consistent downstream handling.
// ═══════════════════════════════════════════════════════════════════════════════

/** Convert a decoded value to a number[] (handles Float32Array, Float64Array, plain arrays) */
function toNumberArray(val: unknown): number[] {
  if (val == null) return [];
  if (val instanceof Float32Array || val instanceof Float64Array) return Array.from(val);
  if (Array.isArray(val)) return val as number[];
  return [];
}

/** Convert a decoded value to a number[] of integers (handles Int32Array, Uint32Array, plain arrays) */
function toIntArray(val: unknown): number[] {
  if (val == null) return [];
  if (val instanceof Int32Array || val instanceof Uint32Array) return Array.from(val);
  if (Array.isArray(val)) return val as number[];
  return [];
}

/** Convert a decoded value to Uint8Array (handles plain arrays and typed arrays) */
function toUint8Array(val: unknown): Uint8Array {
  if (val == null) return new Uint8Array(0);
  if (val instanceof Uint8Array) return val;
  if (Array.isArray(val)) return new Uint8Array(val);
  return new Uint8Array(0);
}
