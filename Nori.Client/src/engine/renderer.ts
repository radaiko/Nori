// renderer.ts -- Frame rendering loop using WebGPU
// Sorts primitives by zLevel, binds appropriate pipelines, uploads uniforms, draws.

import { GPUDeviceManager } from './gpu-device.js';
import { PipelineFactory, Pipeline } from './pipeline-factory.js';
import { BufferManager } from './buffers.js';
import { ClientScene, RenderPrimitive, PrimType } from './scene-graph.js';
import type { NoriConnection } from '../protocol/connection.js';

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

// Post-process uniforms: vec2 texelSize (8) + f32 thresholdNormal (4) + f32 thresholdDepth (4) = 16
const UNIFORM_SIZE_POST = 16;

// ---------------------------------------------------------------------------
// Renderer
// ---------------------------------------------------------------------------
export class Renderer {
  private gpu: GPUDeviceManager;
  private pipelines: PipelineFactory;
  private buffers: BufferManager;
  private scene: ClientScene;
  private running: boolean = false;
  private dirty: boolean = true;
  private lastCanvasW: number = 0;
  private lastCanvasH: number = 0;

  // Re-usable uniform buffer (sized to max uniform size)
  private uniformBuf: GPUBuffer | null = null;

  // Edge composite resources (created once, recreated on resize)
  private edgeSampler: GPUSampler | null = null;
  private edgeBindGroup: GPUBindGroup | null = null;
  private edgeUniformBuf: GPUBuffer | null = null;
  private edgeTexW: number = 0;
  private edgeTexH: number = 0;

  // DashLine2D linetype texture resources
  private ltypeTexture: GPUTexture | null = null;
  private ltypeTextureView: GPUTextureView | null = null;
  private ltypeSampler: GPUSampler | null = null;

  // Connection for per-frame message flushing
  private connection: NoriConnection | null = null;

  // FPS tracking
  private frameCount: number = 0;
  private lastFpsTime: number = 0;
  onFps: ((fps: number) => void) | null = null;

  constructor(gpu: GPUDeviceManager, pipelines: PipelineFactory, scene: ClientScene) {
    this.gpu = gpu;
    this.pipelines = pipelines;
    this.buffers = new BufferManager(gpu.device);
    this.scene = scene;
    this.createLinetypeTexture(gpu.device);
  }

  /** Create a 256×16 linetype pattern texture for dashed line rendering */
  private createLinetypeTexture(device: GPUDevice): void {
    const W = 256, H = 16;
    const data = new Uint8Array(W * H);
    // ELineType: 0=Continuous, 1=Dot, 2=Dash, 3=DashDot, 4=DashDotDot,
    //            5=Center, 6=Border, 7=Hidden, 8=Dash2, 9=Phantom
    const patterns: number[][] = [
      [],                                            // 0: Continuous (all solid)
      [4, 4],                                        // 1: Dot
      [24, 12],                                      // 2: Dash
      [24, 8, 4, 8],                                 // 3: DashDot
      [24, 6, 4, 6, 4, 6],                           // 4: DashDotDot
      [32, 8, 8, 8],                                 // 5: Center
      [24, 6, 4, 6, 4, 6],                           // 6: Border
      [12, 8],                                       // 7: Hidden
      [16, 8],                                       // 8: Dash2
      [32, 6, 4, 6, 4, 6],                           // 9: Phantom
    ];
    for (let row = 0; row < H; row++) {
      const pat = row < patterns.length ? patterns[row] : [];
      if (pat.length === 0) {
        // Solid line — fill entire row with 255
        for (let x = 0; x < W; x++) data[row * W + x] = 255;
      } else {
        // Generate repeating dash pattern
        const total = pat.reduce((a, b) => a + b, 0);
        for (let x = 0; x < W; x++) {
          const t = (x / W) * total;
          let acc = 0;
          let visible = true;
          for (let i = 0; i < pat.length; i++) {
            acc += pat[i];
            if (t < acc) { visible = i % 2 === 0; break; }
          }
          data[row * W + x] = visible ? 255 : 0;
        }
      }
    }
    this.ltypeTexture = device.createTexture({
      size: { width: W, height: H },
      format: 'r8unorm',
      usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
      label: 'linetype-texture',
    });
    device.queue.writeTexture(
      { texture: this.ltypeTexture },
      data, { bytesPerRow: W }, { width: W, height: H },
    );
    this.ltypeTextureView = this.ltypeTexture.createView({ label: 'linetype-view' });
    this.ltypeSampler = device.createSampler({
      magFilter: 'linear', minFilter: 'linear',
      addressModeU: 'repeat', addressModeV: 'clamp-to-edge',
      label: 'linetype-sampler',
    });
  }

  /** Set the connection for per-frame message batching */
  setConnection(conn: NoriConnection | null): void {
    this.connection = conn;
  }

  /** Start the render loop (vsync-driven via requestAnimationFrame) */
  start(): void {
    this.lastFpsTime = performance.now();
    this.running = true;
    const loop = () => {
      if (!this.running) return;
      // Flush all queued WebSocket messages before rendering so multiple
      // updates between frames are batched into a single redraw.
      this.connection?.flush();
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
      requestAnimationFrame(loop);
    };
    requestAnimationFrame(loop);
  }

  /** Stop the render loop */
  stop(): void {
    this.running = false;
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
    this.edgeUniformBuf?.destroy();
    this.edgeUniformBuf = null;
    this.edgeBindGroup = null;
    this.edgeSampler = null;
  }

  // -------------------------------------------------------------------------
  // Internals
  // -------------------------------------------------------------------------

  /** Check if canvas was resized; if so, recreate depth texture and edge resources */
  private checkResize(): void {
    const w = this.gpu.canvas.width;
    const h = this.gpu.canvas.height;
    if (w !== this.lastCanvasW || h !== this.lastCanvasH) {
      this.lastCanvasW = w;
      this.lastCanvasH = h;
      this.gpu.createDepthTexture();
      this.rebuildEdgeBindGroup();
      this.dirty = true;
    }
  }

  /** Rebuild the edge composite bind group after resize (textures changed) */
  private rebuildEdgeBindGroup(): void {
    const device = this.gpu.device;
    const w = this.gpu.width;
    const h = this.gpu.height;

    if (!this.edgeSampler) {
      this.edgeSampler = device.createSampler({
        magFilter: 'linear',
        minFilter: 'linear',
        label: 'edge-sampler',
      });
    }

    this.edgeUniformBuf?.destroy();
    const postData = new Float32Array(UNIFORM_SIZE_POST / 4);
    postData[0] = 1.0 / w;   // texel_size.x
    postData[1] = 1.0 / h;   // texel_size.y
    postData[2] = 0.25;      // edge_threshold_normal (feature edges from normal discontinuities)
    postData[3] = 0.01;      // edge_threshold_depth (unused — silhouette uses bg neighbor check)
    this.edgeUniformBuf = this.buffers.createUniformBuffer(UNIFORM_SIZE_POST, 'edge-uniform');
    device.queue.writeBuffer(this.edgeUniformBuf, 0, postData.buffer, postData.byteOffset, postData.byteLength);

    this.edgeBindGroup = device.createBindGroup({
      layout: this.pipelines.postProcessLayout,
      entries: [
        { binding: 0, resource: { buffer: this.edgeUniformBuf } },
        { binding: 1, resource: this.gpu.normalView },
        { binding: 2, resource: this.edgeSampler },
      ],
    });
    this.edgeTexW = w;
    this.edgeTexH = h;
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

    // Compute transforms
    const projMatrix = this.scene.computeProjectionMatrix(w, h);
    const normalMatrix = this.scene.computeNormalMatrix(w, h);
    const vpScaleX = 2 / w;
    const vpScaleY = 2 / h;

    // Sort primitives and check if any are 3D meshes
    const primitives = this.scene.getAllPrimitivesSorted();
    const has3D = primitives.some(p => p.type === PrimType.Mesh3D);
    const hasGBufferPipeline = this.pipelines.has(Pipeline.GBufferNormal);

    const encoder = device.createCommandEncoder({ label: 'frame' });

    // --- Pass 1: G-Buffer with MSAA (mesh scenes only) ---
    if (has3D && hasGBufferPipeline) {
      const gBufferPass = encoder.beginRenderPass({
        label: 'gbuffer-pass',
        colorAttachments: [{
          view: this.gpu.msaaNormalView,          // MSAA render target
          resolveTarget: this.gpu.normalView,     // resolve to single-sample for edge composite
          clearValue: { r: 0.5, g: 0.5, b: 1.0, a: 1.0 },
          loadOp: 'clear',
          storeOp: 'discard',  // MSAA intermediate discarded after resolve
        }],
        depthStencilAttachment: {
          view: this.gpu.msaaGBufDepthView,       // MSAA depth
          depthClearValue: 1.0,
          depthLoadOp: 'clear',
          depthStoreOp: 'discard',
          stencilClearValue: 0,
          stencilLoadOp: 'clear',
          stencilStoreOp: 'discard',
        },
      });

      const gBufferPipeline = this.pipelines.get(Pipeline.GBufferNormal);
      for (const prim of primitives) {
        if (prim.type === PrimType.Mesh3D) {
          this.drawMesh3D(gBufferPass, gBufferPipeline, prim, projMatrix, normalMatrix, primColor(prim), device);
        }
      }

      gBufferPass.end();
    }

    // --- Pass 2: Main scene pass ---
    if (has3D && this.pipelines.has(Pipeline.CADGooch)) {
      // Pass 2a: MSAA pass for 3D meshes (CADGooch pipeline uses 4x MSAA)
      const msaaPass = encoder.beginRenderPass({
        label: 'msaa-main-pass',
        colorAttachments: [{
          view: this.gpu.msaaColorView,      // MSAA render target
          resolveTarget: colorView,           // resolve to canvas
          clearValue: clearColor,
          loadOp: 'clear',
          storeOp: 'discard',                // MSAA intermediate discarded after resolve
        }],
        depthStencilAttachment: {
          view: this.gpu.msaaDepthView,       // MSAA depth
          depthClearValue: 1.0,
          depthLoadOp: 'clear',
          depthStoreOp: 'discard',
          stencilClearValue: 0,
          stencilLoadOp: 'clear',
          stencilStoreOp: 'discard',
        },
      });

      const cadPipeline = this.pipelines.get(Pipeline.CADGooch);
      for (const prim of primitives) {
        if (prim.type === PrimType.Mesh3D) {
          this.drawMesh3D(msaaPass, cadPipeline, prim, projMatrix, normalMatrix, primColor(prim), device, [vpScaleX, vpScaleY]);
        }
      }
      msaaPass.end();

      // Pass 2b: Non-MSAA overlay for everything except 3D meshes
      const hasNonMesh = primitives.some(p => p.type !== PrimType.Mesh3D);
      if (hasNonMesh) {
        const overlayPass = encoder.beginRenderPass({
          label: 'overlay-pass',
          colorAttachments: [{
            view: colorView,
            loadOp: 'load',     // preserve resolved MSAA result
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

        for (const prim of primitives) {
          if (prim.type !== PrimType.Mesh3D) {
            this.drawPrimitive(overlayPass, prim, projMatrix, normalMatrix, vpScaleX, vpScaleY, device, false);
          }
        }
        overlayPass.end();
      }
    } else {
      // Non-3D scenes: single pass (no MSAA needed)
      const mainPass = encoder.beginRenderPass({
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

      for (const prim of primitives) {
        this.drawPrimitive(mainPass, prim, projMatrix, normalMatrix, vpScaleX, vpScaleY, device, has3D);
      }
      mainPass.end();
    }

    // Edge composite pass disabled — wireframe lines drawn in MSAA pass match WPF approach

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
    has3D: boolean = false,
  ): void {
    if (!prim.data || prim.data.length === 0) return;

    let pipelineId = this.scene.getPipeline(prim);
    if (pipelineId === null) return;

    // For 3D mesh scenes, override shade-mode pipeline with CADGooch
    if (has3D && prim.type === PrimType.Mesh3D && this.pipelines.has(Pipeline.CADGooch)) {
      pipelineId = Pipeline.CADGooch;
    }

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
      case Pipeline.DashLine2D:
        this.drawInstanced2DDashLine(pass, pipeline, prim, projMatrix, vpScaleX, vpScaleY, color, device);
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
        this.drawMesh3D(pass, pipeline, prim, projMatrix, normalMatrix, color, device, [vpScaleX, vpScaleY]);
        break;
      case Pipeline.CADGooch:
        this.drawMesh3D(pass, pipeline, prim, projMatrix, normalMatrix, color, device, [vpScaleX, vpScaleY]);
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

  private drawInstanced2DDashLine(
    pass: GPURenderPassEncoder,
    pipeline: GPURenderPipeline,
    prim: RenderPrimitive,
    projMatrix: Float32Array,
    vpScaleX: number, vpScaleY: number,
    color: Float32Array,
    device: GPUDevice,
  ): void {
    if (!this.ltypeTextureView || !this.ltypeSampler) return;

    // DashLine2D uniform: mat4x4 xfm + vec2 vp_scale + f32 lineWidth + f32 ltScale
    //   + vec4 color + f32 lineType + pad*3 = 112 bytes
    const uniformData = new Float32Array(UNIFORM_SIZE_DASHLINE / 4);
    uniformData.set(projMatrix, 0);           // offset 0: mat4x4 (16 floats)
    uniformData[16] = vpScaleX;               // offset 64: vp_scale.x
    uniformData[17] = vpScaleY;               // offset 68: vp_scale.y
    uniformData[18] = Math.max(prim.lineWidth, 1); // offset 72: line_width
    uniformData[19] = Math.max(prim.ltScale || 20, 1); // offset 76: lt_scale
    uniformData.set(color, 20);               // offset 80: draw_color (4 floats)
    // lineType is the row in the texture (0-9), normalized to [0,1] for sampling
    uniformData[24] = ((prim.lineType || 0) + 0.5) / 16; // offset 96: line_type (normalized Y)
    uniformData[25] = 0;                      // pad
    uniformData[26] = 0;                      // pad
    uniformData[27] = 0;                      // pad

    const instanceCount = prim.data.length / 4;
    if (instanceCount < 1) return;

    const uniformBuf = this.createTempUniform(device, uniformData);
    const vertexBuf = this.buffers.createVertexBuffer(prim.data, 'dashline2d-instances');
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.texturedLayout,
      entries: [
        { binding: 0, resource: { buffer: uniformBuf } },
        { binding: 1, resource: this.ltypeTextureView },
        { binding: 2, resource: this.ltypeSampler },
      ],
    });

    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuf);
    pass.draw(6, instanceCount);

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
    vpScale?: [number, number],
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

      // Draw wireframe edges if present (only when vpScale is provided — skip for G-Buffer)
      if (vpScale && prim.wireIndices && prim.wireIndices.length > 0) {
        this.drawWireEdges(pass, prim, projMatrix, vpScale, device);
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
    uniformData[18] = 2 * (globalThis.devicePixelRatio ?? 1);  // WPF default is 2 logical px * DPIScale
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
    if (!prim.indices || prim.indices.length === 0) return;

    const color = primColor(prim);
    const uniformData = new Float32Array(UNIFORM_SIZE_FLAT / 4);
    uniformData.set(projMatrix, 0);
    uniformData.set(color, 16);

    // Expand triangle fan indices (with -1 delimiters) into a triangle list.
    // Format: [hub, v0, v1, v2, ..., vN, v0, -1, hub, ...] where each fan
    // produces triangles (hub,v0,v1), (hub,v1,v2), etc.
    const indices = prim.indices;
    const verts = prim.data; // x,y pairs
    const tris: number[] = [];
    let fanStart = 0;
    for (let i = 0; i < indices.length; i++) {
      const idx = indices[i];
      if (idx === 0xFFFFFFFF || (idx | 0) === -1) { // -1 delimiter (unsigned or signed)
        // Emit triangles for this fan: indices[fanStart] is hub,
        // subsequent indices are the ring vertices
        const hub = indices[fanStart];
        for (let j = fanStart + 2; j < i; j++) {
          const a = indices[j - 1], b = indices[j];
          tris.push(verts[hub * 2], verts[hub * 2 + 1]);
          tris.push(verts[a * 2], verts[a * 2 + 1]);
          tris.push(verts[b * 2], verts[b * 2 + 1]);
        }
        fanStart = i + 1;
      }
    }

    if (tris.length === 0) return;
    const stencilData = new Float32Array(tris);
    const stencilBuf = this.buffers.createVertexBuffer(stencilData, 'fill2d-stencil');

    const uniformBuf = this.createTempUniform(device, uniformData);
    const bindGroup = device.createBindGroup({
      layout: this.pipelines.uniformLayout,
      entries: [{ binding: 0, resource: { buffer: uniformBuf } }],
    });

    // Pass 1: Write stencil (invert stencil bit for each triangle, no color output)
    pass.setPipeline(this.pipelines.get(Pipeline.TriFanStencil));
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, stencilBuf);
    pass.setStencilReference(1);
    pass.draw(stencilData.length / 2);

    // Pass 2: Draw a quad covering the bounding box where stencil is set
    const bd = prim.boundData;
    let coverData: Float32Array;
    if (bd && bd.length >= 4) {
      const [minX, minY, maxX, maxY] = bd;
      coverData = new Float32Array([
        minX, minY, maxX, minY, maxX, maxY,
        minX, minY, maxX, maxY, minX, maxY,
      ]);
    } else {
      // Fallback: compute bounds from vertices
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
      for (let i = 0; i < verts.length; i += 2) {
        if (verts[i] < minX) minX = verts[i];
        if (verts[i] > maxX) maxX = verts[i];
        if (verts[i + 1] < minY) minY = verts[i + 1];
        if (verts[i + 1] > maxY) maxY = verts[i + 1];
      }
      coverData = new Float32Array([
        minX, minY, maxX, minY, maxX, maxY,
        minX, minY, maxX, maxY, minX, maxY,
      ]);
    }
    const coverBuf = this.buffers.createVertexBuffer(coverData, 'fill2d-cover');

    pass.setPipeline(this.pipelines.get(Pipeline.TriFanCover));
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, coverBuf);
    pass.setStencilReference(0);
    pass.draw(6);

    this.deferDestroy(uniformBuf, stencilBuf, coverBuf);
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
