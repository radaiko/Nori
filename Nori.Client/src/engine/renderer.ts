// renderer.ts -- Frame rendering loop using WebGPU
// Sorts primitives by zLevel, binds appropriate pipelines, uploads uniforms, draws.

import { GPUDeviceManager } from './gpu-device.js';
import { PipelineFactory, Pipeline } from './pipeline-factory.js';
import { BufferManager } from './buffers.js';
import { ClientScene, RenderPrimitive, PrimType } from './scene-graph.js';

// ---------------------------------------------------------------------------
// Uniform buffer sizes for each pipeline family
// ---------------------------------------------------------------------------

// Line2D/Bezier2D/Line3D/Point2D/Point3D/GlassLine uniforms:
//   mat4x4 (64) + vec2 vp_scale (8) + f32 lineWidth/pointSize (4) + pad (4) + vec4 color (16) = 96
const UNIFORM_SIZE_LINE = 96;

// DashLine2D uniforms:
//   mat4x4 (64) + vec2 vp_scale (8) + f32 lineWidth (4) + f32 ltScale (4)
//   + vec4 color (16) + f32 lineType (4) + pad*3 (12) = 112
const UNIFORM_SIZE_DASHLINE = 112;

// Flat2D uniforms: mat4x4 (64) + vec4 color (16) = 80
const UNIFORM_SIZE_FLAT = 80;

// 3D facet uniforms: mat4x4 xfm (64) + mat4x4 normalXfm (64) + vec4 color (16) = 144
const UNIFORM_SIZE_FACET = 144;

// ---------------------------------------------------------------------------
// Renderer
// ---------------------------------------------------------------------------
export class Renderer {
  private gpu: GPUDeviceManager;
  private pipelines: PipelineFactory;
  private buffers: BufferManager;
  private scene: ClientScene;
  private animFrameId: number = 0;
  private dirty: boolean = true;
  private lastCanvasW: number = 0;
  private lastCanvasH: number = 0;

  // Re-usable uniform buffer (sized to max uniform size)
  private uniformBuf: GPUBuffer | null = null;

  // FPS tracking
  private frameCount: number = 0;
  private lastFpsTime: number = 0;
  onFps: ((fps: number) => void) | null = null;

  constructor(gpu: GPUDeviceManager, pipelines: PipelineFactory, scene: ClientScene) {
    this.gpu = gpu;
    this.pipelines = pipelines;
    this.buffers = new BufferManager(gpu.device);
    this.scene = scene;
  }

  /** Start the render loop */
  start(): void {
    this.lastFpsTime = performance.now();
    const loop = () => {
      this.checkResize();
      if (this.dirty) {
        this.renderFrame();
        this.dirty = false;
        this.frameCount++;
      }
      // FPS reporting
      const now = performance.now();
      if (now - this.lastFpsTime >= 1000) {
        this.onFps?.(this.frameCount);
        this.frameCount = 0;
        this.lastFpsTime = now;
      }
      this.animFrameId = requestAnimationFrame(loop);
    };
    this.animFrameId = requestAnimationFrame(loop);
  }

  /** Stop the render loop */
  stop(): void {
    cancelAnimationFrame(this.animFrameId);
    this.animFrameId = 0;
  }

  /** Mark the scene as needing a redraw */
  invalidate(): void {
    this.dirty = true;
  }

  /** Release GPU resources owned by the renderer */
  dispose(): void {
    this.stop();
    this.uniformBuf?.destroy();
    this.uniformBuf = null;
  }

  // -------------------------------------------------------------------------
  // Internals
  // -------------------------------------------------------------------------

  /** Check if canvas was resized; if so, recreate depth texture */
  private checkResize(): void {
    const w = this.gpu.canvas.width;
    const h = this.gpu.canvas.height;
    if (w !== this.lastCanvasW || h !== this.lastCanvasH) {
      this.lastCanvasW = w;
      this.lastCanvasH = h;
      this.gpu.createDepthTexture();
      this.dirty = true;
    }
  }

  /** Render one complete frame */
  private renderFrame(): void {
    const device = this.gpu.device;
    const w = this.gpu.width;
    const h = this.gpu.height;
    if (w === 0 || h === 0) return;

    // Get current texture from canvas context
    let colorTexture: GPUTexture;
    try {
      colorTexture = this.gpu.context.getCurrentTexture();
    } catch {
      return; // Context may be lost
    }
    const colorView = colorTexture.createView();

    // Background color from scene (convert 0-255 to 0-1)
    const bg = this.scene.bgColor;
    const clearColor: GPUColor = {
      r: bg[0] / 255,
      g: bg[1] / 255,
      b: bg[2] / 255,
      a: bg[3] / 255,
    };

    const encoder = device.createCommandEncoder({ label: 'frame' });

    const renderPass = encoder.beginRenderPass({
      label: 'main-pass',
      colorAttachments: [{
        view: colorView,
        clearValue: clearColor,
        loadOp: 'clear',
        storeOp: 'store',
      }],
      depthStencilAttachment: {
        view: this.gpu.depthView,
        depthClearValue: 1.0,
        depthLoadOp: 'clear',
        depthStoreOp: 'store',
        stencilClearValue: 0,
        stencilLoadOp: 'clear',
        stencilStoreOp: 'store',
      },
    });

    // Compute transforms
    const projMatrix = this.scene.computeProjectionMatrix(w, h);
    const normalMatrix = this.scene.computeNormalMatrix(w, h);
    const vpScaleX = 2 / w;
    const vpScaleY = 2 / h;

    // Sort primitives
    const primitives = this.scene.getAllPrimitivesSorted();

    // Draw each primitive
    for (const prim of primitives) {
      this.drawPrimitive(renderPass, prim, projMatrix, normalMatrix, vpScaleX, vpScaleY, device);
    }

    renderPass.end();
    device.queue.submit([encoder.finish()]);
  }

  /** Draw a single primitive with the appropriate pipeline */
  private drawPrimitive(
    pass: GPURenderPassEncoder,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    normalMatrix: Float32Array,
    vpScaleX: number,
    vpScaleY: number,
    device: GPUDevice,
  ): void {
    if (!prim.data || prim.data.length === 0) return;

    const pipelineId = this.scene.getPipeline(prim);
    if (pipelineId === null) return;
    if (!this.pipelines.has(pipelineId)) return;

    // Handle Fill2D specially with two-pass stencil rendering
    if (prim.type === PrimType.Fill2D) {
      this.drawFill2D(pass, prim, projMatrix, device);
      return;
    }

    // Skip text pipelines for now (require font texture)
    if (prim.type === PrimType.Text2D || prim.type === PrimType.Text3D || prim.type === PrimType.TextPx) {
      return;
    }

    const pipeline = this.pipelines.get(pipelineId);
    const color = primColor(prim);

    // Determine the uniform data and rendering parameters based on pipeline type
    switch (pipelineId) {
      case Pipeline.Line2D:
      case Pipeline.Bezier2D:
        this.drawInstanced2DLine(pass, pipeline, prim, projMatrix, vpScaleX, vpScaleY, color, device);
        break;
      case Pipeline.Line3D:
      case Pipeline.BlackLine:
      case Pipeline.GlassLine:
        this.drawInstanced3DLine(pass, pipeline, prim, projMatrix, vpScaleX, vpScaleY, color, device);
        break;
      case Pipeline.Point2D:
        this.drawInstanced2DPoint(pass, pipeline, prim, projMatrix, vpScaleX, vpScaleY, color, device);
        break;
      case Pipeline.Point3D:
        this.drawInstanced3DPoint(pass, pipeline, prim, projMatrix, vpScaleX, vpScaleY, color, device);
        break;
      case Pipeline.Triangle2D:
      case Pipeline.Quad2D:
        this.drawFlat2D(pass, pipeline, prim, projMatrix, color, device);
        break;
      case Pipeline.Gourad:
      case Pipeline.Phong:
      case Pipeline.PhongPink:
      case Pipeline.Pick:
      case Pipeline.Glass:
      case Pipeline.FlatFacet:
        this.drawMesh3D(pass, pipeline, prim, projMatrix, normalMatrix, color, device);
        break;
      default:
        break;
    }
  }

  // -------------------------------------------------------------------------
  // Draw helpers for each pipeline family
  // -------------------------------------------------------------------------

  private drawInstanced2DLine(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScaleX: number, vpScaleY: number,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    // Uniform: mat4x4 xfm + vec2 vp_scale + f32 line_width + pad + vec4 color
    const uniformData = new Float32Array(UNIFORM_SIZE_LINE / 4);
    uniformData.set(projMatrix, 0);           // offset 0: mat4x4 (16 floats)
    uniformData[16] = vpScaleX;               // offset 64: vp_scale.x
    uniformData[17] = vpScaleY;               // offset 68: vp_scale.y
    uniformData[18] = Math.max(prim.lineWidth, 1); // offset 72: line_width
    uniformData[19] = 0;                      // offset 76: pad
    uniformData.set(color, 20);               // offset 80: draw_color (4 floats)

    // Instance count = data length / 4 floats per instance (p0.xy, p1.xy)
    const instanceCount = prim.data.length / 4;
    if (instanceCount < 1) return;

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'line2d-instances');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, instanceCount);   // 6 vertices per quad instance

    // Schedule cleanup (buffers live until end of frame submission)
    this.deferDestroy(uniformBuf, vertexBuf);
  }

  private drawInstanced3DLine(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScaleX: number, vpScaleY: number,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    const uniformData = new Float32Array(UNIFORM_SIZE_LINE / 4);
    uniformData.set(projMatrix, 0);
    uniformData[16] = vpScaleX;
    uniformData[17] = vpScaleY;
    uniformData[18] = Math.max(prim.lineWidth, 1);
    uniformData[19] = 0;
    uniformData.set(color, 20);

    // Instance count = data length / 6 floats per instance (p0.xyz, p1.xyz)
    const instanceCount = prim.data.length / 6;
    if (instanceCount < 1) return;

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'line3d-instances');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, instanceCount);

    this.deferDestroy(uniformBuf, vertexBuf);
  }

  private drawInstanced2DPoint(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScaleX: number, vpScaleY: number,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    const uniformData = new Float32Array(UNIFORM_SIZE_LINE / 4);
    uniformData.set(projMatrix, 0);
    uniformData[16] = vpScaleX;
    uniformData[17] = vpScaleY;
    uniformData[18] = Math.max(prim.pointSize, 2);  // point_size
    uniformData[19] = 0;
    uniformData.set(color, 20);

    // Instance count = data length / 2 floats per instance (pos.xy)
    const instanceCount = prim.data.length / 2;
    if (instanceCount < 1) return;

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'point2d-instances');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, instanceCount);

    this.deferDestroy(uniformBuf, vertexBuf);
  }

  private drawInstanced3DPoint(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScaleX: number, vpScaleY: number,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    const uniformData = new Float32Array(UNIFORM_SIZE_LINE / 4);
    uniformData.set(projMatrix, 0);
    uniformData[16] = vpScaleX;
    uniformData[17] = vpScaleY;
    uniformData[18] = Math.max(prim.pointSize, 2);
    uniformData[19] = 0;
    uniformData.set(color, 20);

    const instanceCount = prim.data.length / 3;
    if (instanceCount < 1) return;

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'point3d-instances');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, instanceCount);

    this.deferDestroy(uniformBuf, vertexBuf);
  }

  private drawFlat2D(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    // Flat2D uniform: mat4x4 xfm + vec4 color = 80 bytes
    const uniformData = new Float32Array(UNIFORM_SIZE_FLAT / 4);
    uniformData.set(projMatrix, 0);
    uniformData.set(color, 16);

    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'flat2d-verts');
    const uniformBuf = this.createTempUniform(device, uniformData);
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);

    if (prim.indices && prim.indices.length > 0) {
      const indexBuf = this.buffers.createIndexBuffer(prim.indices, 'flat2d-idx');
      pass.setIndexBuffer(indexBuf, 'uint32');
      pass.drawIndexed(prim.indices.length);
      this.deferDestroy(uniformBuf, vertexBuf, indexBuf);
    } else {
      // Direct vertex draw: data.length / 2 floats per vertex
      const vertexCount = prim.data.length / 2;
      pass.draw(vertexCount);
      this.deferDestroy(uniformBuf, vertexBuf);
    }
  }

  private drawMesh3D(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    normalMatrix: Float32Array,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    // Facet uniform: mat4x4 xfm + mat4x4 normalXfm + vec4 color = 144 bytes
    const uniformData = new Float32Array(UNIFORM_SIZE_FACET / 4);
    uniformData.set(projMatrix, 0);
    uniformData.set(normalMatrix, 16);
    uniformData.set(color, 32);

    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'mesh3d-verts');
    const uniformBuf = this.createTempUniform(device, uniformData);
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);

    if (prim.indices && prim.indices.length > 0) {
      const indexBuf = this.buffers.createIndexBuffer(prim.indices, 'mesh3d-idx');
      pass.setIndexBuffer(indexBuf, 'uint32');
      pass.drawIndexed(prim.indices.length);

      // Also draw wireframe edges if present
      if (prim.wireIndices && prim.wireIndices.length > 0) {
        this.drawWireEdges(pass, prim, projMatrix, vpScaleFromUniforms(uniformData), device);
      }
      this.deferDestroy(uniformBuf, vertexBuf, indexBuf);
    } else {
      const vertexCount = prim.data.length / 6; // 6 floats per vertex (pos + normal)
      pass.draw(vertexCount);
      this.deferDestroy(uniformBuf, vertexBuf);
    }
  }

  /** Draw wireframe overlay lines for a 3D mesh (BlackLine pipeline) */
  private drawWireEdges(
    pass: GPURenderPassEncoder,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScale: [number, number],
    device: GPUDevice,
  ): void {
    if (!prim.wireIndices || prim.wireIndices.length === 0) return;
    if (!this.pipelines.has(Pipeline.BlackLine)) return;

    // Build line segment data from wireIndices
    // wireIndices are pairs of vertex indices into prim.data (stride 6: pos + normal)
    const lineCount = prim.wireIndices.length / 2;
    const lineData = new Float32Array(lineCount * 6); // p0.xyz + p1.xyz per line
    for (let i = 0; i < lineCount; i++) {
      const i0 = prim.wireIndices[i * 2];
      const i1 = prim.wireIndices[i * 2 + 1];
      lineData[i * 6 + 0] = prim.data[i0 * 6 + 0]; // p0.x
      lineData[i * 6 + 1] = prim.data[i0 * 6 + 1]; // p0.y
      lineData[i * 6 + 2] = prim.data[i0 * 6 + 2]; // p0.z
      lineData[i * 6 + 3] = prim.data[i1 * 6 + 0]; // p1.x
      lineData[i * 6 + 4] = prim.data[i1 * 6 + 1]; // p1.y
      lineData[i * 6 + 5] = prim.data[i1 * 6 + 2]; // p1.z
    }

    const blackColor = new Float32Array([0, 0, 0, 1]);
    const uniformData = new Float32Array(UNIFORM_SIZE_LINE / 4);
    uniformData.set(projMatrix, 0);
    uniformData[16] = vpScale[0];
    uniformData[17] = vpScale[1];
    uniformData[18] = 1;  // 1px line width for wire edges
    uniformData[19] = 0;
    uniformData.set(blackColor, 20);

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(lineData, 'wire-edges');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    const pipeline = this.pipelines.get(Pipeline.BlackLine);
    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, lineCount);

    this.deferDestroy(uniformBuf, vertexBuf);
  }

  /** Two-pass stencil rendering for Fill2D (triangle-fan fills) */
  private drawFill2D(
    pass: GPURenderPassEncoder,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    device: GPUDevice,
  ): void {
    if (!this.pipelines.has(Pipeline.TriFanStencil) || !this.pipelines.has(Pipeline.TriFanCover)) return;

    const color = primColor(prim);
    const uniformData = new Float32Array(UNIFORM_SIZE_FLAT / 4);
    uniformData.set(projMatrix, 0);
    uniformData.set(color, 16);

    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'fill2d-verts');
    const uniformBuf = this.createTempUniform(device, uniformData);
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    const vertexCount = prim.data.length / 2;

    // Pass 1: Write stencil (invert stencil bit, no color output)
    pass.setPipeline(this.pipelines.get(Pipeline.TriFanStencil));
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.setStencilReference(1);
    pass.draw(vertexCount);

    // Pass 2: Draw where stencil is set (clear stencil, write color)
    pass.setPipeline(this.pipelines.get(Pipeline.TriFanCover));
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.setStencilReference(1);
    pass.draw(vertexCount);

    this.deferDestroy(uniformBuf, vertexBuf);
  }

  // -------------------------------------------------------------------------
  // Utility
  // -------------------------------------------------------------------------

  /** Create a temporary uniform buffer and write data to it */
  private createTempUniform(device: GPUDevice, data: Float32Array): GPUBuffer {
    const buf = this.buffers.createUniformBuffer(data.byteLength, 'temp-uniform');
    device.queue.writeBuffer(buf, 0, data.buffer, data.byteOffset, data.byteLength);
    return buf;
  }

  /** Defer buffer destruction until after frame submission */
  private pendingDestroys: GPUBuffer[] = [];

  private deferDestroy(...bufs: GPUBuffer[]): void {
    this.pendingDestroys.push(...bufs);
    // Use queueMicrotask to destroy after the current command submission
    if (this.pendingDestroys.length === bufs.length) {
      queueMicrotask(() => {
        for (const b of this.pendingDestroys) b.destroy();
        this.pendingDestroys.length = 0;
      });
    }
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Convert RGBA bytes [0-255] to normalized float color [0-1] */
function primColor(prim: RenderPrimitive): Float32Array {
  return new Float32Array([
    prim.color[0] / 255,
    prim.color[1] / 255,
    prim.color[2] / 255,
    prim.color[3] / 255,
  ]);
}

/** Extract vpScale from a line/point uniform data array */
function vpScaleFromUniforms(uniformData: Float32Array): [number, number] {
  return [uniformData[16], uniformData[17]];
}
