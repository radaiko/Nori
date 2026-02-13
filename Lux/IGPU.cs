// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ IGPU.cs
// ║║║║╬║╔╣║ GPU abstraction interface — isolates Lux from the underlying graphics API
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface IGPU -------------------------------------------------------------------------------
/// <summary>Abstracts all GPU operations that Lux needs, decoupling it from OpenGL or WebGPU</summary>
/// Lux uses this interface for all rendering. Implementations exist for OpenGL (via WGL)
/// and WebGPU (via Nori.GPU). The interface mirrors the operations Lux actually performs:
/// state management, shader compilation, buffer management, draw calls, textures,
/// framebuffers, and pixel readback.
public interface IGPU {
   // State management ----------------------------------------------------------
   /// <summary>Set the viewport rectangle (in pixels)</summary>
   void Viewport (int x, int y, int width, int height);

   /// <summary>Set the clear color</summary>
   void ClearColor (float r, float g, float b, float a);

   /// <summary>Clear the specified buffers (color, depth, stencil)</summary>
   void Clear (bool color, bool depth, bool stencil);

   /// <summary>Enable or disable a GPU capability (blending, depth test, stencil test, etc.)</summary>
   void Enable (EGPUCap cap, bool on);

   /// <summary>Set the blend function</summary>
   void BlendFunc (EGPUBlendFactor src, EGPUBlendFactor dst);

   /// <summary>Set the polygon offset parameters</summary>
   void PolygonOffset (float factor, float units);

   /// <summary>Set the stencil operation for the specified face</summary>
   void StencilOp (EGPUStencilOp sfail, EGPUStencilOp dpfail, EGPUStencilOp dppass);

   /// <summary>Set the stencil function for the specified face</summary>
   void StencilFunc (EGPUStencilFunc func, int refVal, uint mask);

   /// <summary>Set the primitive restart index</summary>
   void PrimitiveRestartIndex (uint index);

   /// <summary>Set the number of vertices per tessellation patch</summary>
   void PatchVertices (int count);

   /// <summary>Block until all GPU commands have completed</summary>
   void Finish ();

   // Shader operations ---------------------------------------------------------
   /// <summary>Create a shader program and return its handle</summary>
   int CreateProgram ();

   /// <summary>Compile a shader of the given type from source and return its handle</summary>
   int CompileShader (EGPUShaderType type, string source);

   /// <summary>Attach a compiled shader to a program</summary>
   void AttachShader (int program, int shader);

   /// <summary>Link a shader program</summary>
   void LinkProgram (int program);

   /// <summary>Get the link status of a program (true if successful)</summary>
   bool GetProgramLinkStatus (int program);

   /// <summary>Get the info log for a program (link errors/warnings)</summary>
   string GetProgramInfoLog (int program);

   /// <summary>Get the compile status of a shader (true if successful)</summary>
   bool GetShaderCompileStatus (int shader);

   /// <summary>Get the info log for a shader (compile errors/warnings)</summary>
   string GetShaderInfoLog (int shader);

   /// <summary>Get the number of active uniforms in a program</summary>
   int GetActiveUniformCount (int program);

   /// <summary>Get information about the nth active uniform</summary>
   void GetActiveUniform (int program, int index, out int size, out int type, out string name, out int location);

   /// <summary>Set the active shader program</summary>
   void UseProgram (int program);

   /// <summary>Set a float uniform</summary>
   void SetUniform (int location, float value);

   /// <summary>Set an int uniform</summary>
   void SetUniform1i (int location, int value);

   /// <summary>Set a Vec2F uniform</summary>
   void SetUniform (int location, float x, float y);

   /// <summary>Set a Vec4F uniform</summary>
   void SetUniform (int location, float x, float y, float z, float w);

   /// <summary>Set a Mat4F uniform (4x4 matrix, column-major)</summary>
   unsafe void SetUniformMatrix4 (int location, bool transpose, float* value);

   // Buffer operations ---------------------------------------------------------
   /// <summary>Generate a new buffer and return its handle</summary>
   int GenBuffer ();

   /// <summary>Bind a buffer to the specified target</summary>
   void BindBuffer (EGPUBufferTarget target, int buffer);

   /// <summary>Upload data to the currently bound buffer</summary>
   void BufferData (EGPUBufferTarget target, int size, nint data, EGPUBufferUsage usage);

   /// <summary>Map a range of the currently bound buffer for writing</summary>
   nint MapBufferRange (EGPUBufferTarget target, int offset, int length, EGPUMapAccess access);

   /// <summary>Unmap the currently mapped buffer</summary>
   void UnmapBuffer (EGPUBufferTarget target);

   /// <summary>Delete a buffer</summary>
   void DeleteBuffer (int buffer);

   // Vertex array operations ---------------------------------------------------
   /// <summary>Generate a new vertex array object (VAO) and return its handle</summary>
   int GenVertexArray ();

   /// <summary>Bind a vertex array object</summary>
   void BindVertexArray (int vao);

   /// <summary>Delete a vertex array object</summary>
   void DeleteVertexArray (int vao);

   /// <summary>Define a floating-point vertex attribute</summary>
   void VertexAttribPointer (int index, int dims, int type, bool normalized, int stride, int offset);

   /// <summary>Define an integer vertex attribute</summary>
   void VertexAttribIPointer (int index, int dims, int type, int stride, int offset);

   /// <summary>Enable a vertex attribute array</summary>
   void EnableVertexAttribArray (int index);

   /// <summary>Disable a vertex attribute array</summary>
   void DisableVertexAttribArray (int index);

   // Draw calls ----------------------------------------------------------------
   /// <summary>Draw primitives from array data</summary>
   void DrawArrays (int mode, int first, int count);

   /// <summary>Draw indexed primitives with a base vertex offset</summary>
   void DrawElementsBaseVertex (int mode, int count, int indexType, int indexOffset, int baseVertex);

   // Texture operations --------------------------------------------------------
   /// <summary>Generate a new texture and return its handle</summary>
   int GenTexture ();

   /// <summary>Bind a texture to the specified target</summary>
   void BindTexture (int target, int texture);

   /// <summary>Delete a texture</summary>
   void DeleteTexture (int texture);

   /// <summary>Set the active texture unit</summary>
   void ActiveTexture (int unit);

   /// <summary>Set pixel store alignment parameters</summary>
   void PixelStore (int param, int value);

   /// <summary>Upload a 2D texture image</summary>
   void TexImage2D (int target, int internalFormat, int width, int height, int pixelFormat, int pixelType, Array data);

   /// <summary>Set a texture parameter (int value)</summary>
   void TexParameter (int target, int param, int value);

   // Framebuffer operations ----------------------------------------------------
   /// <summary>Generate a new framebuffer and return its handle</summary>
   int GenFrameBuffer ();

   /// <summary>Bind a framebuffer</summary>
   void BindFrameBuffer (int target, int framebuffer);

   /// <summary>Generate a new renderbuffer and return its handle</summary>
   int GenRenderBuffer ();

   /// <summary>Bind a renderbuffer</summary>
   void BindRenderBuffer (int target, int renderbuffer);

   /// <summary>Allocate storage for a renderbuffer</summary>
   void RenderBufferStorage (int format, int width, int height);

   /// <summary>Attach a renderbuffer to the current framebuffer</summary>
   void FrameBufferRenderBuffer (int target, int attachment, int renderbuffer);

   /// <summary>Check the completeness status of a framebuffer</summary>
   int CheckFrameBufferStatus (int target);

   // Pixel readback ------------------------------------------------------------
   /// <summary>Read pixels from the framebuffer into a byte array</summary>
   void ReadPixels (int x, int y, int width, int height, int format, int type, byte[] data);

   /// <summary>Read pixels from the framebuffer into a float array (for depth)</summary>
   void ReadPixels (int x, int y, int width, int height, int format, int type, float[] data);
}
#endregion

#region enum EGPUCap ---------------------------------------------------------------------------------
/// <summary>GPU capabilities that can be enabled or disabled</summary>
public enum EGPUCap {
   /// <summary>Alpha blending</summary>
   Blend,
   /// <summary>Depth testing</summary>
   DepthTest,
   /// <summary>Stencil testing</summary>
   StencilTest,
   /// <summary>Polygon offset for filled primitives</summary>
   PolygonOffsetFill,
   /// <summary>Primitive restart (for strip/fan primitives)</summary>
   PrimitiveRestart
}
#endregion

#region enum EGPUShaderType --------------------------------------------------------------------------
/// <summary>Types of shader stages</summary>
public enum EGPUShaderType {
   /// <summary>Vertex shader</summary>
   Vertex,
   /// <summary>Fragment (pixel) shader</summary>
   Fragment,
   /// <summary>Geometry shader</summary>
   Geometry,
   /// <summary>Tessellation control shader</summary>
   TessControl,
   /// <summary>Tessellation evaluation shader</summary>
   TessEvaluation
}
#endregion

#region enum EGPUBufferTarget ------------------------------------------------------------------------
/// <summary>Buffer binding targets</summary>
public enum EGPUBufferTarget {
   /// <summary>Vertex attribute data</summary>
   Array,
   /// <summary>Element index data</summary>
   ElementArray
}
#endregion

#region enum EGPUBufferUsage -------------------------------------------------------------------------
/// <summary>Hints for buffer data usage patterns</summary>
public enum EGPUBufferUsage {
   /// <summary>Data set once, drawn many times</summary>
   StaticDraw,
   /// <summary>Data set once, drawn at most a few times</summary>
   StreamDraw
}
#endregion

#region enum EGPUMapAccess ---------------------------------------------------------------------------
/// <summary>Flags for buffer mapping access</summary>
[Flags]
public enum EGPUMapAccess {
   /// <summary>Map for writing</summary>
   Write = 1,
   /// <summary>Do not synchronize (caller promises not to overwrite in-flight data)</summary>
   Unsynchronized = 2
}
#endregion

#region enum EGPUBlendFactor --------------------------------------------------------------------------
/// <summary>Blend factor source/destination values</summary>
public enum EGPUBlendFactor {
   /// <summary>Factor is zero</summary>
   Zero,
   /// <summary>Factor is one</summary>
   One,
   /// <summary>Factor is source alpha</summary>
   SrcAlpha,
   /// <summary>Factor is (1 - source alpha)</summary>
   OneMinusSrcAlpha
}
#endregion

#region enum EGPUStencilOp ---------------------------------------------------------------------------
/// <summary>Stencil buffer update operations</summary>
public enum EGPUStencilOp {
   /// <summary>Keep the current value</summary>
   Keep,
   /// <summary>Set the stencil value to zero</summary>
   Zero,
   /// <summary>Bitwise invert the current stencil value</summary>
   Invert
}
#endregion

#region enum EGPUStencilFunc -------------------------------------------------------------------------
/// <summary>Stencil comparison functions</summary>
public enum EGPUStencilFunc {
   /// <summary>Never passes</summary>
   Never,
   /// <summary>Passes if (ref & mask) == (stencil & mask)</summary>
   Equal,
   /// <summary>Always passes</summary>
   Always
}
#endregion
