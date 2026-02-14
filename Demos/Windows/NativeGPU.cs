// ────── ╔╗                                                                          DEMOS.WINDOWS
// ╔═╦╦═╦╦╬╣ NativeGPU.cs
// ║║║║╬║╔╣║ IGPU implementation using native WebGPU via Silk.NET (desktop adapter)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Silk.NET.WebGPU;
using Silk.NET.Core.Native;
using System.Collections.Generic;
using WBufUsage = Silk.NET.WebGPU.BufferUsage;
namespace Nori;

#region class NativeGPU -------------------------------------------------------------------------------
/// <summary>Desktop IGPU adapter bridging Lux renderer to native WebGPU via Silk.NET</summary>
/// This class adapts the low-level GPU project wrappers (GPUDevice, GPUSurface, GPUBuffer,
/// GPUCommandEncoder, GPUTexture, PipelineFactory) into the IGPU interface that Lux expects.
/// Each frame is recorded into a command encoder and submitted on Present().
unsafe class NativeGPU : IGPU, IDisposable {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a NativeGPU from an existing GPUDevice and GPUSurface</summary>
   public NativeGPU (GPUDevice device, GPUSurface surface) {
      mDevice = device;
      mSurface = surface;
      mPipelineFactory = PipelineFactory.Create (device);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>The pipeline factory for accessing pre-compiled pipelines</summary>
   public PipelineFactory Pipelines => mPipelineFactory;

   // IGPU implementation -------------------------------------------------------
   /// <summary>Set the viewport rectangle (in pixels)</summary>
   public void SetViewport (int x, int y, int w, int h) {
      mViewport = (x, y, w, h);
      // Reconfigure surface if size changed
      if (w > 0 && h > 0 && ((uint)w != mSurface.Width || (uint)h != mSurface.Height))
         mSurface.Configure ((uint)w, (uint)h);
   }

   /// <summary>Clear the current render target to the specified color</summary>
   public void Clear (Color4 color) {
      mClearColor = color;
      // Begin a new frame: acquire texture, start command encoder and render pass
      BeginFrame ();
   }

   /// <summary>Present the current frame to the display</summary>
   public void Present () {
      EndFrame ();
      mSurface.Present ();
   }

   /// <summary>Create a GPU buffer of the given size and return its handle</summary>
   public int CreateBuffer (int size, bool isIndex) {
      int handle = mNextHandle++;
      WBufUsage usage = isIndex
         ? WBufUsage.Index | WBufUsage.CopyDst
         : WBufUsage.Vertex | WBufUsage.CopyDst;
      GPUBuffer buf = GPUBuffer.Create (mDevice, (ulong)size, usage);
      mBuffers[handle] = buf;
      return handle;
   }

   /// <summary>Upload data to a previously created buffer</summary>
   public void UploadBuffer (int handle, nint data, int size) {
      if (!mBuffers.TryGetValue (handle, out GPUBuffer? buf)) return;
      ReadOnlySpan<byte> span = new ((void*)data, size);
      buf.Write (span);
   }

   /// <summary>Delete a buffer and free its GPU memory</summary>
   public void DeleteBuffer (int handle) {
      if (mBuffers.Remove (handle, out GPUBuffer? buf)) buf.Dispose ();
   }

   /// <summary>Set the active render pipeline</summary>
   public void SetPipeline (int pipelineIndex) {
      if (mEncoder == null) return;
      EPipeline ep = (EPipeline)pipelineIndex;
      GPUPipeline pipe = mPipelineFactory.Get (ep);
      mDevice.Api.RenderPassEncoderSetPipeline (mEncoder.RenderPass, pipe.Handle);
   }

   /// <summary>Bind a vertex buffer for subsequent draw calls</summary>
   public void SetVertexBuffer (int bufferHandle, int offset) {
      if (mEncoder == null || mEncoder.RenderPass == null) return;
      if (!mBuffers.TryGetValue (bufferHandle, out GPUBuffer? buf)) return;
      mDevice.Api.RenderPassEncoderSetVertexBuffer (
         mEncoder.RenderPass, 0, buf.Handle, (ulong)offset, buf.Size - (ulong)offset);
   }

   /// <summary>Bind an index buffer for subsequent draw calls</summary>
   public void SetIndexBuffer (int bufferHandle, int offset) {
      if (mEncoder == null || mEncoder.RenderPass == null) return;
      if (!mBuffers.TryGetValue (bufferHandle, out GPUBuffer? buf)) return;
      mDevice.Api.RenderPassEncoderSetIndexBuffer (
         mEncoder.RenderPass, buf.Handle, IndexFormat.Uint16, (ulong)offset, buf.Size - (ulong)offset);
   }

   /// <summary>Bind a group of uniform data for the active pipeline</summary>
   public void SetBindGroup (int group, nint data, int size) {
      if (mEncoder == null || mEncoder.RenderPass == null) return;
      // Create a transient uniform buffer for this bind group
      GPUBuffer uniformBuf = GPUBuffer.Create (mDevice, (ulong)size,
         WBufUsage.Uniform | WBufUsage.CopyDst);
      ReadOnlySpan<byte> span = new ((void*)data, size);
      uniformBuf.Write (span);

      // Create a bind group with the uniform buffer bound at binding 0
      BindGroupLayout* layout = mPipelineFactory.UniformLayout;
      BindGroupEntry entry = new () {
         Binding = 0,
         Buffer = uniformBuf.Handle,
         Offset = 0,
         Size = (ulong)size
      };
      BindGroupDescriptor bgDesc = new () {
         Layout = layout,
         EntryCount = 1,
         Entries = &entry
      };
      BindGroup* bg = mDevice.Api.DeviceCreateBindGroup (mDevice.Device, &bgDesc);
      mDevice.Api.RenderPassEncoderSetBindGroup (mEncoder.RenderPass, (uint)group, bg, 0, null);

      // Track for cleanup
      mFrameBindGroups.Add ((nint)bg);
      mFrameUniformBuffers.Add (uniformBuf);
   }

   /// <summary>Draw non-indexed primitives</summary>
   public void Draw (int vertexCount, int instanceCount, int firstVertex) {
      if (mEncoder == null || mEncoder.RenderPass == null) return;
      mDevice.Api.RenderPassEncoderDraw (
         mEncoder.RenderPass, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, 0);
   }

   /// <summary>Draw indexed primitives</summary>
   public void DrawIndexed (int indexCount, int instanceCount, int firstIndex, int baseVertex) {
      if (mEncoder == null || mEncoder.RenderPass == null) return;
      mDevice.Api.RenderPassEncoderDrawIndexed (
         mEncoder.RenderPass, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, 0);
   }

   /// <summary>Create a 2D RGBA texture from pixel data and return its handle</summary>
   public int CreateTexture (int width, int height, byte[] data) {
      int handle = mNextHandle++;
      GPUTexture tex = GPUTexture.Create (mDevice, (uint)width, (uint)height,
         TextureFormat.Rgba8Unorm);
      tex.Write (data, (uint)(width * 4));
      tex.CreateSampler ();
      mTextures[handle] = tex;
      return handle;
   }

   /// <summary>Bind a texture to a slot for sampling in shaders</summary>
   public void BindTexture (int handle, int slot) {
      if (!mTextures.TryGetValue (handle, out GPUTexture? tex)) return;
      mBoundTexture = tex;
   }

   /// <summary>Delete a texture and free its GPU memory</summary>
   public void DeleteTexture (int handle) {
      if (mTextures.Remove (handle, out GPUTexture? tex)) tex.Dispose ();
   }

   /// <summary>Create an offscreen framebuffer with color and depth attachments</summary>
   public int CreateFramebuffer (int width, int height) {
      int handle = mNextHandle++;
      GPUTexture color = GPUTexture.Create (mDevice, (uint)width, (uint)height,
         mDevice.SurfaceFormat,
         TextureUsage.RenderAttachment | TextureUsage.CopySrc);
      GPUTexture depth = GPUTexture.CreateDepth (mDevice, (uint)width, (uint)height);
      mFramebuffers[handle] = (color, depth);
      return handle;
   }

   /// <summary>Bind an offscreen framebuffer as the render target</summary>
   public void BindFramebuffer (int handle) {
      if (mFramebuffers.TryGetValue (handle, out (GPUTexture color, GPUTexture depth) fb))
         mActiveFB = fb;
   }

   /// <summary>Bind the default (screen) framebuffer as the render target</summary>
   public void BindDefaultFramebuffer () => mActiveFB = default;

   /// <summary>Read pixels from the current framebuffer as RGBA bytes</summary>
   public byte[] ReadPixels (int x, int y, int width, int height) {
      // <<TODO>> Implement pixel readback via buffer mapping
      return new byte[width * height * 4];
   }

   /// <summary>Delete an offscreen framebuffer and its attachments</summary>
   public void DeleteFramebuffer (int handle) {
      if (mFramebuffers.Remove (handle, out (GPUTexture color, GPUTexture depth) fb)) {
         fb.color.Dispose ();
         fb.depth.Dispose ();
      }
   }

   /// <summary>Release all GPU resources</summary>
   public void Dispose () {
      foreach (GPUBuffer buf in mBuffers.Values) buf.Dispose ();
      mBuffers.Clear ();
      foreach (GPUTexture tex in mTextures.Values) tex.Dispose ();
      mTextures.Clear ();
      foreach ((GPUTexture color, GPUTexture depth) fb in mFramebuffers.Values) {
         fb.color.Dispose (); fb.depth.Dispose ();
      }
      mFramebuffers.Clear ();
      mDepthTexture?.Dispose ();
      mPipelineFactory.Dispose ();
      mSurface.Dispose ();
      mDevice.Dispose ();
   }

   // Implementation -----------------------------------------------------------
   // Begins a new frame by acquiring the surface texture, creating a command
   // encoder, and starting a render pass with clear color.
   void BeginFrame () {
      // End any existing frame first
      if (mEncoder != null) EndFrame ();

      // Acquire the surface texture view for this frame
      mSurfaceView = mSurface.GetCurrentTextureView ();
      if (mSurfaceView == null) return;

      // Ensure depth texture matches viewport size
      int w = mViewport.w, h = mViewport.h;
      if (w <= 0 || h <= 0) return;
      if (mDepthTexture == null || mDepthTexture.Width != (uint)w || mDepthTexture.Height != (uint)h) {
         mDepthTexture?.Dispose ();
         mDepthTexture = GPUTexture.CreateDepth (mDevice, (uint)w, (uint)h);
      }

      // Start command encoder and render pass
      mEncoder = GPUCommandEncoder.Begin (mDevice);

      // Determine the color view (offscreen FB or screen surface)
      TextureView* colorView = mActiveFB.color != null
         ? mActiveFB.color.View : mSurfaceView;
      TextureView* depthView = mActiveFB.depth != null
         ? mActiveFB.depth.View : (mDepthTexture != null ? mDepthTexture.View : null);

      mEncoder.BeginRenderPass (colorView, depthView,
         mClearColor.R / 255.0, mClearColor.G / 255.0,
         mClearColor.B / 255.0, mClearColor.A / 255.0);

      // Set viewport
      mEncoder.SetViewport (mViewport.x, mViewport.y, mViewport.w, mViewport.h);
   }

   // Ends the current frame by finishing the render pass and submitting commands.
   void EndFrame () {
      if (mEncoder == null) return;
      mEncoder.EndRenderPass ();
      mEncoder.Submit ();
      mEncoder.Dispose ();
      mEncoder = null;

      // Release the surface texture view
      if (mSurfaceView != null) {
         mDevice.Api.TextureViewRelease (mSurfaceView);
         mSurfaceView = null;
      }

      // Cleanup transient bind groups and uniform buffers
      foreach (nint bg in mFrameBindGroups)
         mDevice.Api.BindGroupRelease ((BindGroup*)bg);
      mFrameBindGroups.Clear ();
      foreach (GPUBuffer buf in mFrameUniformBuffers)
         buf.Dispose ();
      mFrameUniformBuffers.Clear ();
   }

   // Private data -------------------------------------------------------------
   GPUDevice mDevice;
   GPUSurface mSurface;
   PipelineFactory mPipelineFactory;
   GPUCommandEncoder? mEncoder;
   TextureView* mSurfaceView;
   GPUTexture? mDepthTexture;
   GPUTexture? mBoundTexture;
   Color4 mClearColor;
   (int x, int y, int w, int h) mViewport;
   (GPUTexture color, GPUTexture depth) mActiveFB;
   int mNextHandle = 1;
   Dictionary<int, GPUBuffer> mBuffers = [];
   Dictionary<int, GPUTexture> mTextures = [];
   Dictionary<int, (GPUTexture color, GPUTexture depth)> mFramebuffers = [];
   List<nint> mFrameBindGroups = [];
   List<GPUBuffer> mFrameUniformBuffers = [];
}
#endregion
