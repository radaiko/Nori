// gpu-device.ts -- WebGPU device initialization and lifecycle management
// Wraps the browser WebGPU API: adapter, device, canvas context, and depth texture.

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

  /** (Re-)create depth, normal, and G-Buffer depth textures to match canvas size */
  createDepthTexture(): void {
    if (this.depthTexture) this.depthTexture.destroy();
    if (this.normalTexture) this.normalTexture.destroy();
    if (this.gBufferDepthTexture) this.gBufferDepthTexture.destroy();
    const width = Math.max(1, this.canvas.width);
    const height = Math.max(1, this.canvas.height);

    // Main depth-stencil texture (used by main pass)
    this.depthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'depth-stencil',
    });
    this.depthView = this.depthTexture.createView({ label: 'depth-stencil-view' });

    // G-Buffer normal+depth texture (rgba16float: rgb=view-space normal, a=linear depth)
    this.normalTexture = this.device.createTexture({
      size: { width, height },
      format: 'rgba16float',
      usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
      label: 'gbuffer-normal',
    });
    this.normalView = this.normalTexture.createView({ label: 'gbuffer-normal-view' });

    // Separate depth-stencil for G-Buffer pass (so it doesn't interfere with main pass)
    this.gBufferDepthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'gbuffer-depth-stencil',
    });
    this.gBufferDepthView = this.gBufferDepthTexture.createView({ label: 'gbuffer-depth-view' });
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
    this.device?.destroy();
  }
}
