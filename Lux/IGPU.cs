// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ IGPU.cs
// ║║║║╬║╔╣║ GPU abstraction interface — backend-agnostic API for OpenGL and WebGPU
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IGPU -----------------------------------------------------------------------------
/// <summary>Abstracts all GPU operations that Lux needs, decoupling it from any specific graphics API</summary>
/// Lux uses this interface for all rendering. Implementations exist for OpenGL (via WGL)
/// and WebGPU (via Nori.GPU). The design follows WebGPU concepts: pipelines encapsulate
/// all render state, uniforms are passed via bind groups, and there are no API-specific
/// concepts like VAOs, individual uniform setters, or global enable/disable caps.
public interface IGPU {
   // Viewport and presentation ------------------------------------------------
   /// <summary>Set the viewport rectangle (in pixels)</summary>
   void SetViewport (int x, int y, int w, int h);

   /// <summary>Clear the current render target to the specified color</summary>
   void Clear (Color4 color);

   /// <summary>Present the current frame to the display</summary>
   void Present ();

   // Buffer operations --------------------------------------------------------
   /// <summary>Create a GPU buffer of the given size and return its handle</summary>
   /// <param name="size">Size in bytes</param>
   /// <param name="isIndex">True for an index buffer, false for a vertex buffer</param>
   int CreateBuffer (int size, bool isIndex);

   /// <summary>Upload data to a previously created buffer</summary>
   /// <param name="handle">Buffer handle returned by CreateBuffer</param>
   /// <param name="data">Pointer to the source data</param>
   /// <param name="size">Number of bytes to upload</param>
   void UploadBuffer (int handle, nint data, int size);

   /// <summary>Delete a buffer and free its GPU memory</summary>
   void DeleteBuffer (int handle);

   // Pipeline operations ------------------------------------------------------
   /// <summary>Set the active render pipeline (blend, depth, stencil state baked in)</summary>
   /// <param name="pipelineIndex">Index into the set of pre-created pipelines</param>
   void SetPipeline (int pipelineIndex);

   /// <summary>Bind a vertex buffer for subsequent draw calls</summary>
   void SetVertexBuffer (int bufferHandle, int offset);

   /// <summary>Bind an index buffer for subsequent draw calls</summary>
   void SetIndexBuffer (int bufferHandle, int offset);

   /// <summary>Bind a group of uniform data for the active pipeline</summary>
   /// <param name="group">Bind group index (0, 1, 2, ...)</param>
   /// <param name="data">Pointer to the uniform data</param>
   /// <param name="size">Size of the uniform data in bytes</param>
   void SetBindGroup (int group, nint data, int size);

   /// <summary>Draw non-indexed primitives</summary>
   void Draw (int vertexCount, int instanceCount, int firstVertex);

   /// <summary>Draw indexed primitives</summary>
   void DrawIndexed (int indexCount, int instanceCount, int firstIndex, int baseVertex);

   // Texture operations -------------------------------------------------------
   /// <summary>Create a 2D RGBA texture from pixel data and return its handle</summary>
   int CreateTexture (int width, int height, byte[] data);

   /// <summary>Bind a texture to a slot for sampling in shaders</summary>
   void BindTexture (int handle, int slot);

   /// <summary>Delete a texture and free its GPU memory</summary>
   void DeleteTexture (int handle);

   // Framebuffer operations ---------------------------------------------------
   /// <summary>Create an offscreen framebuffer with color and depth attachments</summary>
   int CreateFramebuffer (int width, int height);

   /// <summary>Bind an offscreen framebuffer as the render target</summary>
   void BindFramebuffer (int handle);

   /// <summary>Bind the default (screen) framebuffer as the render target</summary>
   void BindDefaultFramebuffer ();

   /// <summary>Read pixels from the current framebuffer as RGBA bytes</summary>
   byte[] ReadPixels (int x, int y, int width, int height);

   /// <summary>Delete an offscreen framebuffer and its attachments</summary>
   void DeleteFramebuffer (int handle);
}
#endregion
