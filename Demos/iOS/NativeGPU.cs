// ────── ╔╗                                                                              DEMOS.IOS
// ╔═╦╦═╦╦╬╣ NativeGPU.cs
// ║║║║╬║╔╣║ IGPU implementation bridging the low-level GPU project to the Lux renderer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class NativeGPU -------------------------------------------------------------------------------
/// <summary>IGPU adapter that bridges GPUDevice, GPUSurface, and PipelineFactory to Lux</summary>
/// The GPU project provides raw WebGPU wrappers (GPUDevice, GPUSurface, GPUBuffer, etc.)
/// while Lux renders through the IGPU abstraction. This class bridges the two by managing
/// a render pass per frame and translating IGPU calls into WebGPU commands.
unsafe class NativeGPU : IGPU, IDisposable {
   // Methods ------------------------------------------------------------------
   /// <summary>Create a NativeGPU from an ISurface native handle</summary>
   public static NativeGPU Create (IntPtr nativeHandle) {
      NativeGPU gpu = new ();
      gpu.Init (nativeHandle);
      return gpu;
   }

   /// <summary>Release all GPU resources</summary>
   public void Dispose () {
      mDepthTexture?.Dispose ();
      mPipelines?.Dispose ();
      mSurface?.Dispose ();
      mDevice?.Dispose ();
      GC.SuppressFinalize (this);
   }

   // IGPU: Viewport and presentation ------------------------------------------
   /// <summary>Set the viewport rectangle (in pixels)</summary>
   public void SetViewport (int x, int y, int w, int h) {
      mViewport = (x, y, w, h);
      EnsureRenderPass ();
      mEncoder!.SetViewport (x, y, w, h);
   }

   /// <summary>Clear the current render target to the specified color</summary>
   public void Clear (Color4 color) {
      mClearColor = color;
      // End any existing pass so the next EnsureRenderPass picks up the new clear color
      EndCurrentPass ();
   }

   /// <summary>Present the current frame to the display</summary>
   public void Present () {
      EndCurrentPass ();
      if (mEncoder != null) {
         mEncoder.Submit ();
         mEncoder.Dispose ();
         mEncoder = null;
      }
      mSurface!.Present ();
      mCurrentColorView = null;
   }

   // IGPU: Buffer operations --------------------------------------------------
   /// <summary>Create a GPU buffer and return its handle</summary>
   public int CreateBuffer (int size, bool isIndex) {
      BufferUsage usage = isIndex
         ? BufferUsage.Index | BufferUsage.CopyDst
         : BufferUsage.Vertex | BufferUsage.CopyDst;
      GPUBuffer buf = GPUBuffer.Create (mDevice!, (ulong)size, usage);
      int handle = mNextHandle++;
      mBuffers[handle] = buf;
      return handle;
   }

   /// <summary>Upload data to a buffer</summary>
   public void UploadBuffer (int handle, nint data, int size) {
      if (mBuffers.TryGetValue (handle, out GPUBuffer? buf))
         buf.Write (new ReadOnlySpan<byte> ((void*)data, size));
   }

   /// <summary>Delete a buffer</summary>
   public void DeleteBuffer (int handle) {
      if (mBuffers.Remove (handle, out GPUBuffer? buf))
         buf.Dispose ();
   }

   // IGPU: Pipeline operations ------------------------------------------------
   /// <summary>Set the active render pipeline</summary>
   public void SetPipeline (int pipelineIndex) {
      EnsureRenderPass ();
      GPUPipeline pipe = mPipelines!.Get ((EPipeline)pipelineIndex);
      mDevice!.Api.RenderPassEncoderSetPipeline (mEncoder!.RenderPass, pipe.Handle);
   }

   /// <summary>Bind a vertex buffer</summary>
   public void SetVertexBuffer (int bufferHandle, int offset) {
      EnsureRenderPass ();
      if (mBuffers.TryGetValue (bufferHandle, out GPUBuffer? buf))
         mDevice!.Api.RenderPassEncoderSetVertexBuffer (
            mEncoder!.RenderPass, 0, buf.Handle, (ulong)offset, buf.Size - (ulong)offset);
   }

   /// <summary>Bind an index buffer</summary>
   public void SetIndexBuffer (int bufferHandle, int offset) {
      EnsureRenderPass ();
      if (mBuffers.TryGetValue (bufferHandle, out GPUBuffer? buf))
         mDevice!.Api.RenderPassEncoderSetIndexBuffer (
            mEncoder!.RenderPass, buf.Handle, IndexFormat.Uint16, (ulong)offset, buf.Size - (ulong)offset);
   }

   /// <summary>Bind a group of uniform data</summary>
   public void SetBindGroup (int group, nint data, int size) {
      EnsureRenderPass ();
      // Create a temporary uniform buffer, upload data, create bind group, then bind it
      GPUBuffer uniformBuf = GPUBuffer.Create (mDevice!, (ulong)size,
         BufferUsage.Uniform | BufferUsage.CopyDst);
      uniformBuf.Write (new ReadOnlySpan<byte> ((void*)data, size));

      BindGroupLayout* layout = mPipelines!.UniformLayout;
      BindGroupEntry entry = new () {
         Binding = 0, Buffer = uniformBuf.Handle,
         Offset = 0, Size = (ulong)size
      };
      BindGroupDescriptor desc = new () {
         Layout = layout, EntryCount = 1, Entries = &entry
      };
      BindGroup* bg = mDevice!.Api.DeviceCreateBindGroup (mDevice.Device, &desc);
      mDevice.Api.RenderPassEncoderSetBindGroup (mEncoder!.RenderPass, (uint)group, bg, 0, null);

      // Queue cleanup for end of frame
      mTempBuffers.Add (uniformBuf);
      mTempBindGroups.Add ((nint)bg);
   }

   /// <summary>Draw non-indexed primitives</summary>
   public void Draw (int vertexCount, int instanceCount, int firstVertex) {
      EnsureRenderPass ();
      mDevice!.Api.RenderPassEncoderDraw (mEncoder!.RenderPass,
         (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, 0);
   }

   /// <summary>Draw indexed primitives</summary>
   public void DrawIndexed (int indexCount, int instanceCount, int firstIndex, int baseVertex) {
      EnsureRenderPass ();
      mDevice!.Api.RenderPassEncoderDrawIndexed (mEncoder!.RenderPass,
         (uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, 0);
   }

   // IGPU: Texture operations -------------------------------------------------
   /// <summary>Create a 2D RGBA texture from pixel data</summary>
   public int CreateTexture (int width, int height, byte[] data) {
      GPUTexture tex = GPUTexture.Create (mDevice!, (uint)width, (uint)height,
         TextureFormat.Rgba8Unorm);
      tex.Write (data, (uint)(width * 4));
      tex.CreateSampler ();
      int handle = mNextHandle++;
      mTextures[handle] = tex;
      return handle;
   }

   /// <summary>Bind a texture to a slot</summary>
   public void BindTexture (int handle, int slot) {
      // Texture binding is handled via bind groups in WebGPU, stored for later use
      if (mTextures.TryGetValue (handle, out GPUTexture? _))
         mBoundTexture = handle;
   }

   /// <summary>Delete a texture</summary>
   public void DeleteTexture (int handle) {
      if (mTextures.Remove (handle, out GPUTexture? tex))
         tex.Dispose ();
   }

   // IGPU: Framebuffer operations ---------------------------------------------
   /// <summary>Create an offscreen framebuffer</summary>
   public int CreateFramebuffer (int width, int height) {
      GPUTexture color = GPUTexture.Create (mDevice!, (uint)width, (uint)height,
         TextureFormat.Rgba8Unorm,
         TextureUsage.RenderAttachment | TextureUsage.CopySrc);
      GPUTexture depth = GPUTexture.CreateDepth (mDevice!, (uint)width, (uint)height);
      int handle = mNextHandle++;
      mFramebuffers[handle] = (color, depth);
      return handle;
   }

   /// <summary>Bind an offscreen framebuffer</summary>
   public void BindFramebuffer (int handle) {
      EndCurrentPass ();
      mActiveFramebuffer = handle;
   }

   /// <summary>Bind the default (screen) framebuffer</summary>
   public void BindDefaultFramebuffer () {
      EndCurrentPass ();
      mActiveFramebuffer = -1;
   }

   /// <summary>Read pixels from the current framebuffer as RGBA bytes</summary>
   public byte[] ReadPixels (int x, int y, int width, int height) {
      // <<TODO>> Implement readback via staging buffer and MapAsync
      return new byte[width * height * 4];
   }

   /// <summary>Delete an offscreen framebuffer</summary>
   public void DeleteFramebuffer (int handle) {
      if (mFramebuffers.Remove (handle, out (GPUTexture color, GPUTexture depth) fb)) {
         fb.color.Dispose ();
         fb.depth.Dispose ();
      }
   }

   // Implementation -----------------------------------------------------------
   void Init (IntPtr nativeHandle) {
      // Create a temporary GPUDevice to bootstrap, then create the surface
      mDevice = new GPUDevice ();
      // We need the surface first to create the device (adapter needs compatible surface),
      // but GPUDevice.Create takes a Surface*. Use GPUSurface to create the raw surface
      // from the native handle, which requires a GPUDevice. The solution: create a
      // bootstrap device first, create the surface, then reinitialize.
      // For now, use the two-phase approach from GPUSurface.Create.
      GPUSurface tempSurface = GPUSurface.Create (mDevice, nativeHandle);
      // Now properly create the device with the surface
      mDevice.Dispose ();
      mDevice = GPUDevice.Create (tempSurface.Handle);
      mSurface = GPUSurface.Wrap (mDevice, tempSurface.Handle);

      // Create all pipelines
      mPipelines = PipelineFactory.Create (mDevice);
   }

   // Ensure we have an active render pass for the current frame
   void EnsureRenderPass () {
      if (mEncoder != null && mEncoder.RenderPass != null) return;
      if (mEncoder == null) mEncoder = GPUCommandEncoder.Begin (mDevice!);

      TextureView* colorView;
      TextureView* depthView = null;
      if (mActiveFramebuffer >= 0 && mFramebuffers.TryGetValue (mActiveFramebuffer,
         out (GPUTexture color, GPUTexture depth) fb)) {
         colorView = fb.color.View;
         depthView = fb.depth.View;
      } else {
         // Screen rendering — get swap chain texture
         colorView = mSurface!.GetCurrentTextureView ();
         mCurrentColorView = colorView;
         // Ensure we have a depth texture matching the surface size
         EnsureDepthTexture (mSurface.Width, mSurface.Height);
         depthView = mDepthTexture!.View;
      }

      double r = mClearColor.R / 255.0, g = mClearColor.G / 255.0;
      double b = mClearColor.B / 255.0, a = mClearColor.A / 255.0;
      mEncoder.BeginRenderPass (colorView, depthView, r, g, b, a);

      // Apply stored viewport
      if (mViewport.w > 0)
         mEncoder.SetViewport (mViewport.x, mViewport.y, mViewport.w, mViewport.h);
   }

   void EndCurrentPass () {
      if (mEncoder != null && mEncoder.RenderPass != null)
         mEncoder.EndRenderPass ();
      // Clean up temporary uniform buffers and bind groups from this pass
      foreach (GPUBuffer buf in mTempBuffers) buf.Dispose ();
      mTempBuffers.Clear ();
      foreach (nint bg in mTempBindGroups)
         mDevice!.Api.BindGroupRelease ((BindGroup*)bg);
      mTempBindGroups.Clear ();
   }

   void EnsureDepthTexture (uint w, uint h) {
      if (mDepthTexture != null && mDepthTexture.Width >= w && mDepthTexture.Height >= h) return;
      mDepthTexture?.Dispose ();
      mDepthTexture = GPUTexture.CreateDepth (mDevice!, w, h);
   }

   // Private data -------------------------------------------------------------
   GPUDevice? mDevice;
   GPUSurface? mSurface;
   PipelineFactory? mPipelines;
   GPUTexture? mDepthTexture;
   GPUCommandEncoder? mEncoder;
   TextureView* mCurrentColorView;
   Color4 mClearColor = Color4.Black;
   (int x, int y, int w, int h) mViewport;
   int mActiveFramebuffer = -1;
   int mBoundTexture;
   int mNextHandle = 1;
   Dictionary<int, GPUBuffer> mBuffers = [];
   Dictionary<int, GPUTexture> mTextures = [];
   Dictionary<int, (GPUTexture color, GPUTexture depth)> mFramebuffers = [];
   List<GPUBuffer> mTempBuffers = [];
   List<nint> mTempBindGroups = [];
}
#endregion
