// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUCommandEncoder.cs
// ║║║║╬║╔╣║ Wraps WebGPU command recording, render passes, and submission
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GPUCommandEncoder -----------------------------------------------------------------------
/// <summary>Records GPU commands and submits them for execution</summary>
public unsafe class GPUCommandEncoder : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU command encoder handle</summary>
   public CommandEncoder* Handle => mEncoder;

   /// <summary>The active render pass encoder (null when no pass is active)</summary>
   public RenderPassEncoder* RenderPass => mRenderPass;

   // Methods ------------------------------------------------------------------
   /// <summary>Begin recording commands</summary>
   public static GPUCommandEncoder Begin (GPUDevice gpu) {
      GPUCommandEncoder enc = new () { mGPU = gpu };
      CommandEncoderDescriptor desc = new ();
      enc.mEncoder = gpu.Api.DeviceCreateCommandEncoder (gpu.Device, &desc);
      if (enc.mEncoder == null) throw new Exception ("Failed to create WebGPU command encoder");
      return enc;
   }

   /// <summary>Begin a render pass with a color attachment and optional depth attachment</summary>
   public void BeginRenderPass (TextureView* colorView, TextureView* depthView = null,
      double r = 0, double g = 0, double b = 0, double a = 1,
      LoadOp colorLoadOp = LoadOp.Clear, StoreOp colorStoreOp = StoreOp.Store) {

      RenderPassColorAttachment colorAttach = new () {
         View = colorView,
         ResolveTarget = null,
         LoadOp = colorLoadOp,
         StoreOp = colorStoreOp,
         ClearValue = new Color { R = r, G = g, B = b, A = a }
      };

      RenderPassDescriptor passDesc = new () {
         ColorAttachmentCount = 1,
         ColorAttachments = &colorAttach
      };

      // Optional depth-stencil attachment
      RenderPassDepthStencilAttachment depthAttach;
      if (depthView != null) {
         depthAttach = new RenderPassDepthStencilAttachment {
            View = depthView,
            DepthLoadOp = LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            DepthClearValue = 1.0f,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store,
            StencilClearValue = 0
         };
         passDesc.DepthStencilAttachment = &depthAttach;
      }

      mRenderPass = mGPU.Api.CommandEncoderBeginRenderPass (mEncoder, &passDesc);
   }

   /// <summary>Set the active render pipeline for the current pass</summary>
   public void SetPipeline (GPUPipeline pipeline)
      => mGPU.Api.RenderPassEncoderSetPipeline (mRenderPass, pipeline.Handle);

   /// <summary>Set a vertex buffer for the current pass</summary>
   public void SetVertexBuffer (uint slot, GPUBuffer buffer, ulong offset = 0, ulong size = 0) {
      ulong sz = size == 0 ? buffer.Size : size;
      mGPU.Api.RenderPassEncoderSetVertexBuffer (mRenderPass, slot, buffer.Handle, offset, sz);
   }

   /// <summary>Set the index buffer for the current pass</summary>
   public void SetIndexBuffer (GPUBuffer buffer, IndexFormat format = IndexFormat.Uint16,
      ulong offset = 0, ulong size = 0) {
      ulong sz = size == 0 ? buffer.Size : size;
      mGPU.Api.RenderPassEncoderSetIndexBuffer (mRenderPass, buffer.Handle, format, offset, sz);
   }

   /// <summary>Set a bind group for the current pass</summary>
   public void SetBindGroup (uint index, BindGroup* bindGroup, uint dynamicOffsetCount = 0, uint* dynamicOffsets = null)
      => mGPU.Api.RenderPassEncoderSetBindGroup (mRenderPass, index, bindGroup, dynamicOffsetCount, dynamicOffsets);

   /// <summary>Draw non-indexed primitives</summary>
   public void Draw (uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
      => mGPU.Api.RenderPassEncoderDraw (mRenderPass, vertexCount, instanceCount, firstVertex, firstInstance);

   /// <summary>Draw indexed primitives</summary>
   public void DrawIndexed (uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
      => mGPU.Api.RenderPassEncoderDrawIndexed (mRenderPass, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

   /// <summary>Set the viewport for the current render pass</summary>
   public void SetViewport (float x, float y, float width, float height, float minDepth = 0f, float maxDepth = 1f)
      => mGPU.Api.RenderPassEncoderSetViewport (mRenderPass, x, y, width, height, minDepth, maxDepth);

   /// <summary>Set the scissor rectangle for the current render pass</summary>
   public void SetScissorRect (uint x, uint y, uint width, uint height)
      => mGPU.Api.RenderPassEncoderSetScissorRect (mRenderPass, x, y, width, height);

   /// <summary>End the current render pass</summary>
   public void EndRenderPass () {
      if (mRenderPass != null) {
         mGPU.Api.RenderPassEncoderEnd (mRenderPass);
         mRenderPass = null;
      }
   }

   /// <summary>Finish recording and return the command buffer</summary>
   public CommandBuffer* Finish () {
      CommandBufferDescriptor cbDesc = new ();
      CommandBuffer* cmdBuf = mGPU.Api.CommandEncoderFinish (mEncoder, &cbDesc);
      mEncoder = null;
      return cmdBuf;
   }

   /// <summary>Finish and immediately submit to the queue</summary>
   public void Submit () {
      CommandBuffer* cmdBuf = Finish ();
      mGPU.Api.QueueSubmit (mGPU.Queue, 1, &cmdBuf);
   }

   /// <summary>Release the encoder (if not already finished)</summary>
   public void Dispose () {
      if (mRenderPass != null) { mGPU.Api.RenderPassEncoderEnd (mRenderPass); mRenderPass = null; }
      if (mEncoder != null) { mGPU.Api.CommandEncoderRelease (mEncoder); mEncoder = null; }
      GC.SuppressFinalize (this);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   CommandEncoder* mEncoder;
   RenderPassEncoder* mRenderPass;
}
#endregion
