// gpu-device.ts -- WebGPU device initialization and lifecycle management
// Wraps the browser WebGPU API: adapter, device, canvas context, and depth texture.

export class GPUDeviceManager {
  device!: GPUDevice;
  context!: GPUCanvasContext;
  format!: GPUTextureFormat;
  canvas!: HTMLCanvasElement;
  depthTexture!: GPUTexture;
  depthView!: GPUTextureView;

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

  /** (Re-)create the depth+stencil texture to match current canvas size */
  createDepthTexture(): void {
    if (this.depthTexture) this.depthTexture.destroy();
    const width = Math.max(1, this.canvas.width);
    const height = Math.max(1, this.canvas.height);
    this.depthTexture = this.device.createTexture({
      size: { width, height },
      format: 'depth24plus-stencil8',
      usage: GPUTextureUsage.RENDER_ATTACHMENT,
      label: 'depth-stencil',
    });
    this.depthView = this.depthTexture.createView({ label: 'depth-stencil-view' });
  }

  /** Width of the canvas backing store in pixels */
  get width(): number { return this.canvas.width; }

  /** Height of the canvas backing store in pixels */
  get height(): number { return this.canvas.height; }

  /** Release all GPU resources */
  dispose(): void {
    this.depthTexture?.destroy();
    this.device?.destroy();
  }
}
