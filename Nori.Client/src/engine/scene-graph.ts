// scene-graph.ts -- Client-side scene representation and camera state
// Ports Scene2.ComputeXfms and Scene3.ComputeXfms from C#.

import { Pipeline } from './pipeline-factory.js';

// ---------------------------------------------------------------------------
// Primitive type enum -- matches EPrimType on the server
// ---------------------------------------------------------------------------
export enum PrimType {
  Lines2D = 0,
  Lines3D = 1,
  Beziers2D = 2,
  Points2D = 3,
  Points3D = 4,
  Mesh3D = 5,
  Text2D = 6,
  Text3D = 7,
  TextPx = 8,
  Fill2D = 9,
  Triangles2D = 10,
  Quads2D = 11,
}

// ---------------------------------------------------------------------------
// RenderPrimitive -- a single drawable batch from the server
// ---------------------------------------------------------------------------
export interface RenderPrimitive {
  type: PrimType;
  data: Float32Array;
  indices?: Uint32Array;
  color: [number, number, number, number]; // RGBA 0-255
  lineWidth: number;
  lineType: number;
  ltScale: number;
  pointSize: number;
  transformIndex: number;
  zLevel: number;
  shadeMode: number;
  text?: string;
  textAlign?: number;
  wireIndices?: Uint32Array;
  boundData?: Float32Array;
}

// ---------------------------------------------------------------------------
// RenderEntity -- a collection of primitives with a unique ID
// ---------------------------------------------------------------------------
export interface RenderEntity {
  id: number;
  primitives: RenderPrimitive[];
}

// ---------------------------------------------------------------------------
// SceneType
// ---------------------------------------------------------------------------
export type SceneType = '2d' | '3d';

// ---------------------------------------------------------------------------
// ClientScene -- stores entities, camera state, computes projection matrices
// ---------------------------------------------------------------------------
export class ClientScene {
  sceneType: SceneType = '2d';
  bgColor: [number, number, number, number] = [128, 128, 128, 255];

  // Bounds: for 2D [x0, y0, x1, y1], for 3D [x0, y0, z0, x1, y1, z1]
  bounds: number[] = [0, 0, 10, 10];

  entities: Map<number, RenderEntity> = new Map();

  // Camera state
  zoom: number = 1;
  panX: number = 0;
  panY: number = 0;
  xRot: number = -60;  // 3D only (degrees)
  zRot: number = 45;   // 3D only (degrees)

  /** Compute the combined world+projection 4x4 matrix (column-major Float32Array) */
  computeProjectionMatrix(viewportW: number, viewportH: number): Float32Array {
    if (this.sceneType === '2d') {
      return this.compute2DProjection(viewportW, viewportH);
    } else {
      return this.compute3DProjection(viewportW, viewportH);
    }
  }

  /** Compute the normal transform for 3D lighting (4x4 column-major) */
  computeNormalMatrix(viewportW: number, viewportH: number): Float32Array {
    if (this.sceneType === '2d') {
      return mat4Identity();
    }
    // For 3D: compute the rotation part of worldXfm, then pack camera dir
    const xRad = this.xRot * Math.PI / 180;
    const zRad = this.zRot * Math.PI / 180;
    const rot = quaternionFromAxisRotations(xRad, 0, zRad);
    const rotMatrix = mat4FromQuaternion(rot);

    // Pack camera direction (in model space) into row 3 of the normal matrix.
    // Camera forward in view space is (0,0,1). In model space: (R.M13, R.M23, R.M33).
    // The rotation matrix is stored column-major, so:
    //   M13 = rotMatrix[2], M23 = rotMatrix[6], M33 = rotMatrix[10]
    const normalXfm = new Float32Array(16);
    // Copy rotation 3x3 into first 3 columns
    normalXfm[0]  = rotMatrix[0];  normalXfm[1]  = rotMatrix[1];  normalXfm[2]  = rotMatrix[2];  normalXfm[3]  = 0;
    normalXfm[4]  = rotMatrix[4];  normalXfm[5]  = rotMatrix[5];  normalXfm[6]  = rotMatrix[6];  normalXfm[7]  = 0;
    normalXfm[8]  = rotMatrix[8];  normalXfm[9]  = rotMatrix[9];  normalXfm[10] = rotMatrix[10]; normalXfm[11] = 0;
    // Column 3 (row 3 of row-major) = camera dir in model space
    normalXfm[12] = rotMatrix[2];  // M13
    normalXfm[13] = rotMatrix[6];  // M23
    normalXfm[14] = rotMatrix[10]; // M33
    normalXfm[15] = 0;
    return normalXfm;
  }

  /**
   * Port of Scene2.ComputeXfms:
   *   xfm = Matrix3.Map(bound.InflatedF(1/zoom), viewport) * Translation(panX, panY, 0)
   *
   * Matrix3.Map: translates midpoint to origin, scales to clip space [-1,1],
   * adjusting for aspect ratio.
   */
  private compute2DProjection(w: number, h: number): Float32Array {
    const b = this.bounds; // [x0, y0, x1, y1]

    // InflatedF(1/zoom): scale the bound about its midpoint by 1/zoom
    const factor = 1 / this.zoom;
    const midX = (b[0] + b[2]) / 2;
    const midY = (b[1] + b[3]) / 2;
    const halfW = Math.max(((b[2] - b[0]) / 2) * factor, 1);
    const halfH = Math.max(((b[3] - b[1]) / 2) * factor, 1);

    // Map: adjusts dx,dy for aspect ratio
    let dx = halfW;
    let dy = halfH;
    const aspect = Math.max(w, 1) / Math.max(h, 1);
    if (dx / dy > aspect) {
      dy = dx / aspect;
    } else {
      dx = dy * aspect;
    }

    // The Matrix3.Map produces: Translation(-mid) * Scaling(1/dx, 1/dy, 1)
    // Then we multiply by Translation(panX, panY, 0)
    // Combined in a single 4x4 (column-major):
    const sx = 1 / dx;
    const sy = 1 / dy;
    const tx = -midX * sx + this.panX;
    const ty = -midY * sy + this.panY;

    // Column-major 4x4
    return new Float32Array([
      sx,  0,   0,  0,
      0,   sy,  0,  0,
      0,   0,   1,  0,
      tx,  ty,  0,  1,
    ]);
  }

  /**
   * Port of Scene3.ComputeXfms:
   *   worldXfm = Translation(-mid) * Rotation(quaternion)
   *   projectionXfm = Orthographic(frustum) * Translation(pan)
   *   combined = worldXfm * projectionXfm
   */
  private compute3DProjection(w: number, h: number): Float32Array {
    const b = this.bounds; // [x0, y0, z0, x1, y1, z1]
    const midX = (b[0] + b[3]) / 2;
    const midY = (b[1] + b[4]) / 2;
    const midZ = (b[2] + b[5]) / 2;

    // Rotation quaternion from turntable angles
    const xRad = this.xRot * Math.PI / 180;
    const zRad = this.zRot * Math.PI / 180;
    const q = quaternionFromAxisRotations(xRad, 0, zRad);

    // worldXfm = Translation(-mid) * Rotation(q)
    const transMid = mat4Translation(-midX, -midY, -midZ);
    const rotMat = mat4FromQuaternion(q);
    const worldXfm = mat4Multiply(transMid, rotMat);

    // Compute frustum for orthographic projection
    const diagX = b[3] - b[0];
    const diagY = b[4] - b[1];
    const diagZ = b[5] - b[2];
    const diagonal = Math.sqrt(diagX * diagX + diagY * diagY + diagZ * diagZ);
    const radius = diagonal / 2;

    const aspect = Math.max(w, 1) / Math.max(h, 1);
    let dx = radius / this.zoom;
    let dy = radius / this.zoom;
    if (aspect > 1) {
      dx = aspect * dy;
    } else {
      dy = dx / aspect;
    }

    // Orthographic: maps frustum to clip space
    // X,Y -> [-1,1], Z -> [0,1] (WebGPU convention, larger Z -> 0 near, smaller -> 1 far)
    const ortho = mat4Orthographic(-dx, dx, -dy, dy, -radius, radius);
    const transPan = mat4Translation(this.panX, this.panY, 0);
    const projectionXfm = mat4Multiply(ortho, transPan);

    // Combined: worldXfm * projectionXfm
    return mat4Multiply(worldXfm, projectionXfm);
  }

  /** Add or update an entity in the scene */
  addEntity(entity: RenderEntity): void {
    this.entities.set(entity.id, entity);
  }

  /** Remove an entity from the scene */
  removeEntity(id: number): void {
    this.entities.delete(id);
  }

  /** Replace an entity's primitives */
  updateEntity(entity: RenderEntity): void {
    this.entities.set(entity.id, entity);
  }

  /** Clear all entities */
  clear(): void {
    this.entities.clear();
  }

  /** Collect all primitives across all entities, sorted by zLevel */
  getAllPrimitivesSorted(): RenderPrimitive[] {
    const allPrims: RenderPrimitive[] = [];
    for (const entity of this.entities.values()) {
      for (const prim of entity.primitives) {
        allPrims.push(prim);
      }
    }
    allPrims.sort((a, b) => a.zLevel - b.zLevel);
    return allPrims;
  }

  /** Map a primitive type to the appropriate pipeline */
  getPipeline(prim: RenderPrimitive): Pipeline | null {
    switch (prim.type) {
      case PrimType.Lines2D:
        return prim.lineType > 0 ? Pipeline.DashLine2D : Pipeline.Line2D;
      case PrimType.Lines3D:
        return Pipeline.Line3D;
      case PrimType.Beziers2D:
        return Pipeline.Bezier2D;
      case PrimType.Points2D:
        return Pipeline.Point2D;
      case PrimType.Points3D:
        return Pipeline.Point3D;
      case PrimType.Triangles2D:
        return Pipeline.Triangle2D;
      case PrimType.Quads2D:
        return Pipeline.Quad2D;
      case PrimType.Fill2D:
        return Pipeline.TriFanStencil; // Uses stencil two-pass
      case PrimType.Mesh3D:
        return pipelineForShadeMode(prim.shadeMode);
      case PrimType.Text2D:
        return Pipeline.Text2D;
      case PrimType.Text3D:
        return Pipeline.Text3D;
      case PrimType.TextPx:
        return Pipeline.TextPx;
      default:
        return null;
    }
  }
}

// ---------------------------------------------------------------------------
// Shade mode -> pipeline mapping (matches C# EShadeMode)
// ---------------------------------------------------------------------------
function pipelineForShadeMode(mode: number): Pipeline {
  switch (mode) {
    case 0: return Pipeline.Gourad;      // EShadeMode.Gourad
    case 1: return Pipeline.Phong;       // EShadeMode.Phong
    case 2: return Pipeline.PhongPink;   // EShadeMode.PhongPink
    case 3: return Pipeline.FlatFacet;   // EShadeMode.FlatFacet
    case 4: return Pipeline.Glass;       // EShadeMode.Glass
    default: return Pipeline.Phong;
  }
}

// ---------------------------------------------------------------------------
// Matrix math utilities (column-major 4x4 matrices as Float32Array(16))
// ---------------------------------------------------------------------------

function mat4Identity(): Float32Array {
  return new Float32Array([
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1,
  ]);
}

function mat4Translation(dx: number, dy: number, dz: number): Float32Array {
  return new Float32Array([
    1,  0,  0,  0,
    0,  1,  0,  0,
    0,  0,  1,  0,
    dx, dy, dz, 1,
  ]);
}

/** Orthographic projection: X,Y -> [-1,1], Z -> [0,1] (WebGPU convention) */
function mat4Orthographic(
  left: number, right: number,
  bottom: number, top: number,
  near: number, far: number,
): Float32Array {
  // Port of Matrix3.Orthographic(Bound3):
  //   dx = 1/(right-left), dy = 1/(top-bottom), dz = 1/(far-near)
  //   midX = (left+right)/2, midY = (bottom+top)/2, midZ = (near+far)/2
  //   M = [2*dx, 0, 0, 0,  0, 2*dy, 0, 0,  0, 0, -dz, 0,  -2*dx*midX, -2*dy*midY, dz*midZ+0.5, 1]
  const dx = 1 / (right - left);
  const dy = 1 / (top - bottom);
  const dz = 1 / (far - near);
  const midX = (left + right) / 2;
  const midY = (bottom + top) / 2;
  const midZ = (near + far) / 2;

  return new Float32Array([
    2 * dx,          0,               0,                0,
    0,               2 * dy,          0,                0,
    0,               0,               -dz,              0,
    -2 * dx * midX,  -2 * dy * midY,  dz * midZ + 0.5, 1,
  ]);
}

/** Multiply two column-major 4x4 matrices: result = A * B */
function mat4Multiply(a: Float32Array, b: Float32Array): Float32Array {
  const out = new Float32Array(16);
  for (let col = 0; col < 4; col++) {
    for (let row = 0; row < 4; row++) {
      let sum = 0;
      for (let k = 0; k < 4; k++) {
        sum += a[k * 4 + row] * b[col * 4 + k];
      }
      out[col * 4 + row] = sum;
    }
  }
  return out;
}

/** Construct a rotation matrix from a unit quaternion [x, y, z, w] (column-major) */
function mat4FromQuaternion(q: [number, number, number, number]): Float32Array {
  const [x, y, z, w] = q;
  // Port of Matrix3.Rotation(Quaternion):
  //   row0 = [1-2yy-2zz, 2xy+2wz, 2xz-2wy]
  //   row1 = [2xy-2wz, 1-2xx-2zz, 2yz+2wx]
  //   row2 = [2xz+2wy, 2yz-2wx, 1-2xx-2yy]
  // In column-major, column c, row r = element at index c*4+r
  return new Float32Array([
    1 - 2*y*y - 2*z*z,  2*x*y + 2*w*z,      2*x*z - 2*w*y,      0,
    2*x*y - 2*w*z,      1 - 2*x*x - 2*z*z,  2*y*z + 2*w*x,      0,
    2*x*z + 2*w*y,      2*y*z - 2*w*x,      1 - 2*x*x - 2*y*y,  0,
    0,                   0,                   0,                   1,
  ]);
}

/**
 * Construct a quaternion from axis rotations: first X, then Y, then Z.
 * Returns [x, y, z, w]. Port of Quaternion.FromAxisRotations.
 */
function quaternionFromAxisRotations(xRot: number, yRot: number, zRot: number): [number, number, number, number] {
  const qx = quaternionFromAxisAngle([1, 0, 0], xRot);
  const qy = quaternionFromAxisAngle([0, 1, 0], yRot);
  const qz = quaternionFromAxisAngle([0, 0, 1], zRot);
  return quaternionMultiply(quaternionMultiply(qx, qy), qz);
}

/** Construct a quaternion from an axis and angle (radians). Returns [x, y, z, w]. */
function quaternionFromAxisAngle(axis: [number, number, number], angle: number): [number, number, number, number] {
  if (Math.abs(angle) < 1e-12) return [0, 0, 0, 1]; // identity
  const halfAngle = angle / 2;
  const s = Math.sin(halfAngle);
  const len = Math.sqrt(axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2]);
  const nx = axis[0] / len, ny = axis[1] / len, nz = axis[2] / len;
  return [nx * s, ny * s, nz * s, Math.cos(halfAngle)];
}

/** Multiply two quaternions: result = a * b. Returns [x, y, z, w]. */
function quaternionMultiply(
  a: [number, number, number, number],
  b: [number, number, number, number],
): [number, number, number, number] {
  const [ax, ay, az, aw] = a;
  const [bx, by, bz, bw] = b;
  return [
    aw * bx + ax * bw + ay * bz - az * by,
    aw * by - ax * bz + ay * bw + az * bx,
    aw * bz + ax * by - ay * bx + az * bw,
    aw * bw - ax * bx - ay * by - az * bz,
  ];
}
