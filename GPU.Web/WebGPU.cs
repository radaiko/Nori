// ────── ╔╗                                                                                GPU.WEB
// ╔═╦╦═╦╦╬╣ WebGPU.cs
// ║║║║╬║╔╣║ IGPU implementation via browser WebGPU API using JS interop command batching
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class WebGPU -------------------------------------------------------------------------------
/// <summary>IGPU implementation that batches commands into a binary buffer and flushes to JS</summary>
/// Rather than calling into JavaScript for every GPU operation, this class encodes each
/// command as a byte opcode followed by its parameters into a flat command buffer. On
/// Present() the entire buffer is flushed to the JS side via a single JSImport call,
/// minimizing the WASM-to-JS interop overhead which is the primary bottleneck.
/// GPU resources (buffers, textures, framebuffers) are identified by integer handles
/// that are assigned on the C# side and mirrored in JavaScript.
public class WebGPU : IGPU {
   // Command opcodes -----------------------------------------------------------
   const byte OP_SET_VIEWPORT = 1;
   const byte OP_CLEAR = 2;
   const byte OP_PRESENT = 3;
   const byte OP_CREATE_BUFFER = 4;
   const byte OP_UPLOAD_BUFFER = 5;
   const byte OP_DELETE_BUFFER = 6;
   const byte OP_SET_PIPELINE = 7;
   const byte OP_SET_VERTEX_BUFFER = 8;
   const byte OP_SET_INDEX_BUFFER = 9;
   const byte OP_SET_BIND_GROUP = 10;
   const byte OP_DRAW = 11;
   const byte OP_DRAW_INDEXED = 12;
   const byte OP_CREATE_TEXTURE = 13;
   const byte OP_BIND_TEXTURE = 14;
   const byte OP_DELETE_TEXTURE = 15;
   const byte OP_CREATE_FB = 16;
   const byte OP_BIND_FB = 17;
   const byte OP_BIND_DEFAULT_FB = 18;
   const byte OP_READ_PIXELS = 19;
   const byte OP_DELETE_FB = 20;

   // Viewport and presentation ------------------------------------------------
   /// <summary>Set the viewport rectangle (in pixels)</summary>
   public void SetViewport (int x, int y, int w, int h) {
      EnsureSpace (17);
      WriteByte (OP_SET_VIEWPORT);
      WriteInt (x); WriteInt (y); WriteInt (w); WriteInt (h);
   }

   /// <summary>Clear the current render target to the specified color</summary>
   public void Clear (Color4 color) {
      EnsureSpace (17);
      WriteByte (OP_CLEAR);
      WriteFloat (color.R / 255f); WriteFloat (color.G / 255f);
      WriteFloat (color.B / 255f); WriteFloat (color.A / 255f);
   }

   /// <summary>Present the current frame to the display</summary>
   public void Present () {
      EnsureSpace (1);
      WriteByte (OP_PRESENT);
      FlushCommands ();
   }

   // Buffer operations --------------------------------------------------------
   /// <summary>Create a GPU buffer of the given size and return its handle</summary>
   public int CreateBuffer (int size, bool isIndex) {
      int handle = mNextHandle++;
      EnsureSpace (10);
      WriteByte (OP_CREATE_BUFFER);
      WriteInt (handle);
      WriteInt (size);
      WriteByte (isIndex ? (byte)1 : (byte)0);
      // Flush immediately — the JS side must create the resource before
      // any subsequent UploadBuffer call references this handle
      FlushCommands ();
      return handle;
   }

   /// <summary>Upload data to a previously created buffer</summary>
   public void UploadBuffer (int handle, nint data, int size) {
      // Buffer uploads are large and variable-length; flush the current
      // command batch first, then send the upload as a dedicated call so
      // we can pass the data pointer efficiently via a typed array view
      FlushCommands ();
      unsafe {
         byte[] managed = new byte[size];
         System.Runtime.InteropServices.Marshal.Copy (data, managed, 0, size);
         NoriWebGPU.UploadBuffer (handle, managed, size);
      }
   }

   /// <summary>Delete a buffer and free its GPU memory</summary>
   public void DeleteBuffer (int handle) {
      EnsureSpace (5);
      WriteByte (OP_DELETE_BUFFER);
      WriteInt (handle);
   }

   // Pipeline operations ------------------------------------------------------
   /// <summary>Set the active render pipeline</summary>
   public void SetPipeline (int pipelineIndex) {
      EnsureSpace (5);
      WriteByte (OP_SET_PIPELINE);
      WriteInt (pipelineIndex);
   }

   /// <summary>Bind a vertex buffer for subsequent draw calls</summary>
   public void SetVertexBuffer (int bufferHandle, int offset) {
      EnsureSpace (9);
      WriteByte (OP_SET_VERTEX_BUFFER);
      WriteInt (bufferHandle);
      WriteInt (offset);
   }

   /// <summary>Bind an index buffer for subsequent draw calls</summary>
   public void SetIndexBuffer (int bufferHandle, int offset) {
      EnsureSpace (9);
      WriteByte (OP_SET_INDEX_BUFFER);
      WriteInt (bufferHandle);
      WriteInt (offset);
   }

   /// <summary>Bind a group of uniform data for the active pipeline</summary>
   public void SetBindGroup (int group, nint data, int size) {
      // Like buffer uploads, bind group data is variable-length. We flush
      // the current batch and send the data separately.
      FlushCommands ();
      unsafe {
         byte[] managed = new byte[size];
         System.Runtime.InteropServices.Marshal.Copy (data, managed, 0, size);
         NoriWebGPU.SetBindGroup (group, managed, size);
      }
   }

   /// <summary>Draw non-indexed primitives</summary>
   public void Draw (int vertexCount, int instanceCount, int firstVertex) {
      EnsureSpace (13);
      WriteByte (OP_DRAW);
      WriteInt (vertexCount);
      WriteInt (instanceCount);
      WriteInt (firstVertex);
   }

   /// <summary>Draw indexed primitives</summary>
   public void DrawIndexed (int indexCount, int instanceCount, int firstIndex, int baseVertex) {
      EnsureSpace (17);
      WriteByte (OP_DRAW_INDEXED);
      WriteInt (indexCount);
      WriteInt (instanceCount);
      WriteInt (firstIndex);
      WriteInt (baseVertex);
   }

   // Texture operations -------------------------------------------------------
   /// <summary>Create a 2D RGBA texture from pixel data and return its handle</summary>
   public int CreateTexture (int width, int height, byte[] data) {
      int handle = mNextHandle++;
      // Texture creation with pixel data is a large payload — use a
      // dedicated interop call rather than embedding in the command buffer
      FlushCommands ();
      NoriWebGPU.CreateTexture (handle, width, height, data, data.Length);
      return handle;
   }

   /// <summary>Bind a texture to a slot for sampling in shaders</summary>
   public void BindTexture (int handle, int slot) {
      EnsureSpace (9);
      WriteByte (OP_BIND_TEXTURE);
      WriteInt (handle);
      WriteInt (slot);
   }

   /// <summary>Delete a texture and free its GPU memory</summary>
   public void DeleteTexture (int handle) {
      EnsureSpace (5);
      WriteByte (OP_DELETE_TEXTURE);
      WriteInt (handle);
   }

   // Framebuffer operations ---------------------------------------------------
   /// <summary>Create an offscreen framebuffer with color and depth attachments</summary>
   public int CreateFramebuffer (int width, int height) {
      int handle = mNextHandle++;
      EnsureSpace (13);
      WriteByte (OP_CREATE_FB);
      WriteInt (handle);
      WriteInt (width);
      WriteInt (height);
      // Flush so the framebuffer exists before any subsequent bind
      FlushCommands ();
      return handle;
   }

   /// <summary>Bind an offscreen framebuffer as the render target</summary>
   public void BindFramebuffer (int handle) {
      EnsureSpace (5);
      WriteByte (OP_BIND_FB);
      WriteInt (handle);
   }

   /// <summary>Bind the default (screen) framebuffer as the render target</summary>
   public void BindDefaultFramebuffer () {
      EnsureSpace (1);
      WriteByte (OP_BIND_DEFAULT_FB);
   }

   /// <summary>Read pixels from the current framebuffer as RGBA bytes</summary>
   public byte[] ReadPixels (int x, int y, int width, int height) {
      // ReadPixels requires a synchronous return from JS, so we flush
      // pending commands first and then make a dedicated interop call
      FlushCommands ();
      return NoriWebGPU.ReadPixels (x, y, width, height);
   }

   /// <summary>Delete an offscreen framebuffer and its attachments</summary>
   public void DeleteFramebuffer (int handle) {
      EnsureSpace (5);
      WriteByte (OP_DELETE_FB);
      WriteInt (handle);
   }

   // Initialization -----------------------------------------------------------
   /// <summary>Initialize the WebGPU device and canvas context for the given canvas element</summary>
   public static Task Init (string canvasId) => NoriWebGPU.Init (canvasId);

   // Implementation -----------------------------------------------------------
   // Ensures there are at least 'bytes' bytes of space left in the command
   // buffer. If not, flushes the current batch to JS first.
   void EnsureSpace (int bytes) {
      if (mCmdPos + bytes > mCmdBuf.Length)
         FlushCommands ();
   }

   // Flushes the accumulated command buffer to the JS side for execution.
   // Resets the write position to zero after the flush.
   void FlushCommands () {
      if (mCmdPos == 0) return;
      NoriWebGPU.ExecuteCommands (mCmdBuf, mCmdPos);
      mCmdPos = 0;
   }

   // Writes a single byte to the command buffer at the current position.
   void WriteByte (byte value)
      => mCmdBuf[mCmdPos++] = value;

   // Writes a 32-bit integer to the command buffer in little-endian order.
   void WriteInt (int value) {
      mCmdBuf[mCmdPos++] = (byte)(value & 0xFF);
      mCmdBuf[mCmdPos++] = (byte)((value >> 8) & 0xFF);
      mCmdBuf[mCmdPos++] = (byte)((value >> 16) & 0xFF);
      mCmdBuf[mCmdPos++] = (byte)((value >> 24) & 0xFF);
   }

   // Writes a 32-bit float to the command buffer in little-endian order.
   void WriteFloat (float value) {
      int bits = BitConverter.SingleToInt32Bits (value);
      WriteInt (bits);
   }

   // Private data -------------------------------------------------------------
   byte[] mCmdBuf = new byte[64 * 1024];  // 64KB command buffer
   int mCmdPos;
   int mNextHandle = 1;
}
#endregion
