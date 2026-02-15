// gpu-device.ts -- WebGPU device initialization and lifecycle management
// Wraps the browser WebGPU API: adapter, device, canvas context, and depth texture.

export const MSAA_SAMPLES = 4;

export class GPUDeviceManager {
  device!: GPUDevice;
  context!: GPUCanvasContext;
  format!: GPUTextureFormat;
  canvas!: HTMLCanvasElement;
  depthTexture!: GPUTexture;
  depthView!: GPUTextureView;

  // G-Buffer textures for CAD edge-detection pass
  normalTexture!: GPUTexture;
  normalView!: GPUTextureView;
  gBufferDepthTexture!: GPUTexture;
  gBufferDepthView!: GPUTextureView;

  // MSAA textures (4x) for smooth edges
  msaaColorTexture!: GPUTexture;
  msaaColorView!: GPUTextureView;
  msaaDepthTexture!: GPUTexture;
  msaaDepthView!: GPUTextureView;
  msaaNormalTexture!: GPUTexture;
  msaaNormalView!: GPUTextureView;
  msaaGBufDepthTexture!: GPUTexture;
  msaaGBufDepthView!: GPUTextureView;

  /** Initialize WebGPU: request adapter, device, configure canvas context */
  async init(canvas: HTMLCanvasElement): Promise<void> {
    if (!navigator.gpu) throw new Error('WebGPU not supported in this browser');
    const adapter = await navigator.gpu.requestAdapter({ powerPreference: 'high-performance' });
    if (!adapter) throw new Error('No WebGPU adapter found');
    this.device = await adapter.requestDevice();
    this.device.lost.then((info) => {
      console.error(`WebGPU device lost (${info.reason}): ${info.message}`);
    });
    this.canvas = canvas;
    this.context = canvas.getContext('webgpu')!;
    if (!this.context) throw new Error('Failed to get WebGPU canvas context');
    this.format = navigator.gpu.getPreferredCanvasFormat();
    this.context.configure({
      device: this.device,
      format: this.format,
      alphaMode: 'opaque',
    });
    this.createDepthTexture();
  }

  /** (Re-)create all render textures (single-sample + MSAA) to match canvas size */
  createDepthTexture(): void {
    // Destroy previous textures
    this.depthTexture?.destroy();
    this.normalTexture?.destroy();
    this.gBufferDepthTexture?.destroy();
    this.msaaColorTexture?.destroy();
    this.msaaDepthTexture?.destroy();
    this.msaaNormalTexture?.destroy();
    this.msaaGBufDepthTexture?.destroy();

    const width = Math.max(1, this.canvas.width);
    const height = Math.max(1, this.canvas.height);
    const sc = MSAA_SAMPLES;

    // --- Single-sample textures ---

    // Main depth-stencil (used by main pass without MSAA, e.g. 2D primitives)
    this.depthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'depth-stencil',
    });
    this.depthView = this.depthTexture.createView({ label: 'depth-stencil-view' });

    // G-Buffer normal+depth resolve target (readable by edge composite)
    this.normalTexture = this.device.createTexture({
      size: { width, height },
      format: 'rgba16float',
      usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
      label: 'gbuffer-normal-resolve',
    });
    this.normalView = this.normalTexture.createView({ label: 'gbuffer-normal-view' });

    // --- MSAA textures (4x) ---

    // MSAA color for main pass (resolves to canvas texture)
    this.msaaColorTexture = this.device.createTexture({
      size: { width, height },
      format: this.format,
      sampleCount: sc,
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'msaa-color',
    });
    this.msaaColorView = this.msaaColorTexture.createView({ label: 'msaa-color-view' });

    // MSAA depth for main pass
    this.msaaDepthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      sampleCount: sc,
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'msaa-depth',
    });
    this.msaaDepthView = this.msaaDepthTexture.createView({ label: 'msaa-depth-view' });

    // MSAA normal for G-Buffer pass (resolves to normalTexture)
    this.msaaNormalTexture = this.device.createTexture({
      size: { width, height },
      format: 'rgba16float',
      sampleCount: sc,
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'msaa-gbuffer-normal',
    });
    this.msaaNormalView = this.msaaNormalTexture.createView({ label: 'msaa-gbuffer-normal-view' });

    // MSAA depth for G-Buffer pass
    this.msaaGBufDepthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      sampleCount: sc,
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'msaa-gbuffer-depth',
    });
    this.msaaGBufDepthView = this.msaaGBufDepthTexture.createView({ label: 'msaa-gbuffer-depth-view' });
  }

  /** Width of the canvas backing store in pixels */
  get width(): number { return this.canvas.width; }

  /** Height of the canvas backing store in pixels */
  get height(): number { return this.canvas.height; }

  /** Release all GPU resources */
  dispose(): void {
    this.depthTexture?.destroy();
    this.normalTexture?.destroy();
    this.gBufferDepthTexture?.destroy();
    this.msaaColorTexture?.destroy();
    this.msaaDepthTexture?.destroy();
    this.msaaNormalTexture?.destroy();
    this.msaaGBufDepthTexture?.destroy();
    this.device?.destroy();
  }
}
