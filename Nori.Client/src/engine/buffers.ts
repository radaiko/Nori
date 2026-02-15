// buffers.ts -- GPU buffer creation and management utilities

export class BufferManager {
  private device: GPUDevice;

  constructor(device: GPUDevice) {
    this.device = device;
  }

  /** Create a vertex buffer from Float32Array data */
  createVertexBuffer(data: Float32Array, label?: string): GPUBuffer {
    const buffer = this.device.createBuffer({
      size: data.byteLength,
      usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
      label: label ?? 'vertex-buffer',
    });
    this.device.queue.writeBuffer(buffer, 0, data.buffer, data.byteOffset, data.byteLength);
    return buffer;
  }

  /** Create an index buffer from Uint16Array or Uint32Array data */
  createIndexBuffer(data: Uint16Array | Uint32Array, label?: string): GPUBuffer {
    const buffer = this.device.createBuffer({
      size: data.byteLength,
      usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
      label: label ?? 'index-buffer',
    });
    this.device.queue.writeBuffer(buffer, 0, data.buffer, data.byteOffset, data.byteLength);
    return buffer;
  }

  /** Create a uniform buffer of the specified byte size */
  createUniformBuffer(size: number, label?: string): GPUBuffer {
    // Uniform buffers must be at least 16-byte aligned
    const alignedSize = Math.ceil(size / 16) * 16;
    return this.device.createBuffer({
      size: alignedSize,
      usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
      label: label ?? 'uniform-buffer',
    });
  }

  /** Write data into a uniform buffer at the specified offset */
  writeUniform(buffer: GPUBuffer, data: ArrayBuffer, offset: number = 0): void {
    this.device.queue.writeBuffer(buffer, offset, data, 0, data.byteLength);
  }
}
