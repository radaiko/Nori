// ────── ╔╗                                                                                GPU.WEB
// ╔═╦╦═╦╦╬╣ nori-gpu.js
// ║║║║╬║╔╣║ Browser WebGPU implementation — decodes command buffers from C# WASM interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────

// Command opcodes (must match WebGPU.cs constants)
const OP_SET_VIEWPORT = 1;
const OP_CLEAR = 2;
const OP_PRESENT = 3;
const OP_CREATE_BUFFER = 4;
const OP_UPLOAD_BUFFER = 5;
const OP_DELETE_BUFFER = 6;
const OP_SET_PIPELINE = 7;
const OP_SET_VERTEX_BUFFER = 8;
const OP_SET_INDEX_BUFFER = 9;
const OP_SET_BIND_GROUP = 10;
const OP_DRAW = 11;
const OP_DRAW_INDEXED = 12;
const OP_CREATE_TEXTURE = 13;
const OP_BIND_TEXTURE = 14;
const OP_DELETE_TEXTURE = 15;
const OP_CREATE_FB = 16;
const OP_BIND_FB = 17;
const OP_BIND_DEFAULT_FB = 18;
const OP_READ_PIXELS = 19;
const OP_DELETE_FB = 20;

export const noriGpu = {
   // --- State ---------------------------------------------------------------
   device: null,
   context: null,
   canvasFormat: null,
   queue: null,

   // Resource maps keyed by integer handle
   buffers: new Map (),
   textures: new Map (),
   textureViews: new Map (),
   samplers: new Map (),
   framebuffers: new Map (),
   pipelines: [],

   // Current render state
   currentEncoder: null,
   currentPass: null,
   currentFB: null,
   currentPipeline: null,
   boundTextures: new Map (),
   viewport: { x: 0, y: 0, w: 0, h: 0 },

   // --- Initialization ------------------------------------------------------

   async init (canvasId) {
      if (!navigator.gpu)
         throw new Error ("WebGPU is not supported in this browser");

      const adapter = await navigator.gpu.requestAdapter ({
         powerPreference: "high-performance"
      });
      if (!adapter)
         throw new Error ("Failed to get WebGPU adapter");

      this.device = await adapter.requestDevice ();
      this.queue = this.device.queue;

      const canvas = document.getElementById (canvasId);
      if (!canvas)
         throw new Error (`Canvas element '${canvasId}' not found`);

      this.context = canvas.getContext ("webgpu");
      this.canvasFormat = navigator.gpu.getPreferredCanvasFormat ();
      this.context.configure ({
         device: this.device,
         format: this.canvasFormat,
         alphaMode: "premultiplied"
      });

      // <<TODO>> Create default render pipelines from WGSL shader sources
      this._initPipelines ();
   },

   // --- Command buffer execution --------------------------------------------

   executeCommands (commands, length) {
      const view = new DataView (commands.buffer, commands.byteOffset, length);
      let pos = 0;

      while (pos < length) {
         const op = commands[pos++];
         switch (op) {
            case OP_SET_VIEWPORT: {
               const x = view.getInt32 (pos, true); pos += 4;
               const y = view.getInt32 (pos, true); pos += 4;
               const w = view.getInt32 (pos, true); pos += 4;
               const h = view.getInt32 (pos, true); pos += 4;
               this.viewport = { x, y, w, h };
               if (this.currentPass)
                  this.currentPass.setViewport (x, y, w, h, 0.0, 1.0);
               break;
            }
            case OP_CLEAR: {
               const r = view.getFloat32 (pos, true); pos += 4;
               const g = view.getFloat32 (pos, true); pos += 4;
               const b = view.getFloat32 (pos, true); pos += 4;
               const a = view.getFloat32 (pos, true); pos += 4;
               this._beginRenderPass (r, g, b, a, "clear");
               break;
            }
            case OP_PRESENT: {
               this._endRenderPass ();
               this._submitCommands ();
               break;
            }
            case OP_CREATE_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const size = view.getInt32 (pos, true); pos += 4;
               const isIndex = commands[pos++] !== 0;
               this._createBuffer (handle, size, isIndex);
               break;
            }
            case OP_DELETE_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteBuffer (handle);
               break;
            }
            case OP_SET_PIPELINE: {
               const index = view.getInt32 (pos, true); pos += 4;
               this._setPipeline (index);
               break;
            }
            case OP_SET_VERTEX_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const offset = view.getInt32 (pos, true); pos += 4;
               this._setVertexBuffer (handle, offset);
               break;
            }
            case OP_SET_INDEX_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const offset = view.getInt32 (pos, true); pos += 4;
               this._setIndexBuffer (handle, offset);
               break;
            }
            case OP_DRAW: {
               const vertexCount = view.getInt32 (pos, true); pos += 4;
               const firstVertex = view.getInt32 (pos, true); pos += 4;
               this._draw (vertexCount, firstVertex);
               break;
            }
            case OP_DRAW_INDEXED: {
               const indexCount = view.getInt32 (pos, true); pos += 4;
               const firstIndex = view.getInt32 (pos, true); pos += 4;
               const baseVertex = view.getInt32 (pos, true); pos += 4;
               this._drawIndexed (indexCount, firstIndex, baseVertex);
               break;
            }
            case OP_BIND_TEXTURE: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const slot = view.getInt32 (pos, true); pos += 4;
               this._bindTexture (handle, slot);
               break;
            }
            case OP_DELETE_TEXTURE: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteTexture (handle);
               break;
            }
            case OP_CREATE_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const w = view.getInt32 (pos, true); pos += 4;
               const h = view.getInt32 (pos, true); pos += 4;
               this._createFramebuffer (handle, w, h);
               break;
            }
            case OP_BIND_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._bindFramebuffer (handle);
               break;
            }
            case OP_BIND_DEFAULT_FB: {
               this._bindDefaultFramebuffer ();
               break;
            }
            case OP_DELETE_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteFramebuffer (handle);
               break;
            }
            default:
               console.error (`noriGpu: unknown opcode ${op} at position ${pos - 1}`);
               return;
         }
      }
   },

   // --- Large-payload interop calls -----------------------------------------

   uploadBuffer (handle, data, size) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu.uploadBuffer: unknown handle ${handle}`);
         return;
      }
      this.queue.writeBuffer (buffer, 0, data, 0, size);
   },

   setBindGroup (group, data, size) {
      // Create or update a uniform buffer for this bind group, then bind it
      // to the current render pass
      // <<TODO>> Implement bind group uniform buffer management
      // For now, store the data so it can be bound when a draw call occurs
      const key = `bindgroup_${group}`;
      let buf = this._uniformBuffers.get (key);
      if (!buf || buf.size < size) {
         if (buf) buf.destroy ();
         buf = this.device.createBuffer ({
            size: Math.max (size, 256),
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            label: `uniform-group-${group}`
         });
         this._uniformBuffers.set (key, buf);
      }
      this.queue.writeBuffer (buf, 0, data, 0, size);

      // <<TODO>> Create and set the actual bind group on the render pass
      // This requires knowing the pipeline's bind group layout
   },

   createTexture (handle, width, height, data, size) {
      const texture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: "rgba8unorm",
         usage: GPUTextureUsage.TEXTURE_BINDING |
                GPUTextureUsage.COPY_DST |
                GPUTextureUsage.RENDER_ATTACHMENT,
         label: `texture-${handle}`
      });
      this.textures.set (handle, texture);
      this.textureViews.set (handle, texture.createView ());

      // Upload pixel data
      this.queue.writeTexture (
         { texture },
         data,
         { bytesPerRow: width * 4, rowsPerImage: height },
         { width, height, depthOrArrayLayers: 1 }
      );

      // Create a default sampler for this texture
      const sampler = this.device.createSampler ({
         magFilter: "linear",
         minFilter: "linear",
         mipmapFilter: "linear",
         addressModeU: "clamp-to-edge",
         addressModeV: "clamp-to-edge"
      });
      this.samplers.set (handle, sampler);
   },

   readPixels (x, y, width, height) {
      // <<TODO>> Implement pixel readback
      // This is challenging in WebGPU because mapAsync is asynchronous.
      // For synchronous WASM interop, we need to use SharedArrayBuffer
      // or Atomics.wait to block until the map completes. For now, return
      // a zeroed array as a placeholder.
      //
      // Full implementation would:
      // 1. Create a staging buffer with MAP_READ | COPY_DST usage
      // 2. Use commandEncoder.copyTextureToBuffer to copy pixels
      // 3. Submit and map the staging buffer
      // 4. Block on Atomics.wait until mapAsync resolves
      // 5. Copy the mapped data and return it
      const byteLength = width * height * 4;
      return new Uint8Array (byteLength);
   },

   // --- Internal helpers ----------------------------------------------------

   _uniformBuffers: new Map (),

   _initPipelines () {
      // <<TODO>> Create render pipelines from WGSL shaders
      // Pipeline indices correspond to the pipelineIndex parameter in
      // SetPipeline. Each pipeline encapsulates:
      //   - Vertex and fragment shader modules
      //   - Vertex buffer layout
      //   - Blend state (alpha blending, additive, etc.)
      //   - Depth/stencil state
      //   - Primitive topology and cull mode
      //   - Bind group layouts
      this.pipelines = [];
   },

   _createBuffer (handle, size, isIndex) {
      const usage = isIndex
         ? (GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST)
         : (GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST);
      const buffer = this.device.createBuffer ({
         size: Math.max (size, 4),   // WebGPU requires size > 0
         usage,
         label: `buffer-${handle}`
      });
      this.buffers.set (handle, buffer);
   },

   _deleteBuffer (handle) {
      const buffer = this.buffers.get (handle);
      if (buffer) {
         buffer.destroy ();
         this.buffers.delete (handle);
      }
   },

   _setPipeline (index) {
      if (index < 0 || index >= this.pipelines.length) {
         console.error (`noriGpu: pipeline index ${index} out of range`);
         return;
      }
      this.currentPipeline = this.pipelines[index];
      if (this.currentPass)
         this.currentPass.setPipeline (this.currentPipeline);
   },

   _setVertexBuffer (handle, offset) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu: unknown buffer handle ${handle}`);
         return;
      }
      if (this.currentPass)
         this.currentPass.setVertexBuffer (0, buffer, offset);
   },

   _setIndexBuffer (handle, offset) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu: unknown buffer handle ${handle}`);
         return;
      }
      if (this.currentPass)
         this.currentPass.setIndexBuffer (buffer, "uint16", offset);
   },

   _draw (vertexCount, firstVertex) {
      if (this.currentPass)
         this.currentPass.draw (vertexCount, 1, firstVertex, 0);
   },

   _drawIndexed (indexCount, firstIndex, baseVertex) {
      if (this.currentPass)
         this.currentPass.drawIndexed (indexCount, 1, firstIndex, baseVertex, 0);
   },

   _bindTexture (handle, slot) {
      this.boundTextures.set (slot, handle);
      // <<TODO>> Texture binding requires creating a bind group that
      // references the texture view and sampler, then calling
      // currentPass.setBindGroup(). The exact bind group index depends
      // on the pipeline layout.
   },

   _deleteTexture (handle) {
      const texture = this.textures.get (handle);
      if (texture) {
         texture.destroy ();
         this.textures.delete (handle);
         this.textureViews.delete (handle);
         this.samplers.delete (handle);
      }
   },

   _createFramebuffer (handle, width, height) {
      // A "framebuffer" in WebGPU is just a set of texture attachments
      // that we render into instead of the canvas
      const colorTexture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: this.canvasFormat,
         usage: GPUTextureUsage.RENDER_ATTACHMENT |
                GPUTextureUsage.TEXTURE_BINDING |
                GPUTextureUsage.COPY_SRC,
         label: `fb-color-${handle}`
      });
      const depthTexture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: "depth24plus-stencil8",
         usage: GPUTextureUsage.RENDER_ATTACHMENT,
         label: `fb-depth-${handle}`
      });
      this.framebuffers.set (handle, {
         color: colorTexture,
         colorView: colorTexture.createView (),
         depth: depthTexture,
         depthView: depthTexture.createView (),
         width, height
      });
   },

   _bindFramebuffer (handle) {
      this._endRenderPass ();
      this.currentFB = this.framebuffers.get (handle) || null;
   },

   _bindDefaultFramebuffer () {
      this._endRenderPass ();
      this.currentFB = null;
   },

   _deleteFramebuffer (handle) {
      const fb = this.framebuffers.get (handle);
      if (fb) {
         fb.color.destroy ();
         fb.depth.destroy ();
         this.framebuffers.delete (handle);
      }
   },

   _beginRenderPass (r, g, b, a, label) {
      // End any existing pass before starting a new one
      this._endRenderPass ();

      if (!this.currentEncoder)
         this.currentEncoder = this.device.createCommandEncoder ();

      // Determine the color attachment — offscreen FB or canvas
      let colorView, depthView;
      if (this.currentFB) {
         colorView = this.currentFB.colorView;
         depthView = this.currentFB.depthView;
      } else {
         colorView = this.context.getCurrentTexture ().createView ();
         depthView = null;
      }

      const colorAttachment = {
         view: colorView,
         clearValue: { r, g, b, a },
         loadOp: "clear",
         storeOp: "store"
      };

      const passDescriptor = {
         colorAttachments: [colorAttachment],
         label: label || "render-pass"
      };

      if (depthView) {
         passDescriptor.depthStencilAttachment = {
            view: depthView,
            depthClearValue: 1.0,
            depthLoadOp: "clear",
            depthStoreOp: "store",
            stencilClearValue: 0,
            stencilLoadOp: "clear",
            stencilStoreOp: "store"
         };
      }

      this.currentPass = this.currentEncoder.beginRenderPass (passDescriptor);

      // Apply viewport if one was set
      const vp = this.viewport;
      if (vp.w > 0 && vp.h > 0)
         this.currentPass.setViewport (vp.x, vp.y, vp.w, vp.h, 0.0, 1.0);

      // Re-apply current pipeline if one is active
      if (this.currentPipeline)
         this.currentPass.setPipeline (this.currentPipeline);
   },

   _endRenderPass () {
      if (this.currentPass) {
         this.currentPass.end ();
         this.currentPass = null;
      }
   },

   _submitCommands () {
      if (this.currentEncoder) {
         const commandBuffer = this.currentEncoder.finish ();
         this.queue.submit ([commandBuffer]);
         this.currentEncoder = null;
      }
   }
};
