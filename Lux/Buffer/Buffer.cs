// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ Buffer.cs
// ║║║║╬║╔╣║ Implements RetainBuffer (GPU buffer wrapper), StreamBuffer (CPU-staged streaming)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Ptr = nint;

#region class RetainBuffer -------------------------------------------------------------------------
/// <summary>A wrapper around GPU vertex/index buffers, used for 'retained mode' drawing</summary>
/// We can store vertex data in a RetainBuffer, if we intend to keep that data constant and
/// reuse it over multiple frames. The other alternative is StreamBuffer, that is used to
/// send data to the GPU that is only going to be used for drawing once. Both have broadly
/// equivalent functionality, and it is more an optimization issue of which one you use over
/// the other
class RetainBuffer : IIndexed {
   // Properties ---------------------------------------------------------------
   /// <summary>The list of all RetainBuffers</summary>
   public static IdxHeap<RetainBuffer> All = new ();

   /// <summary>IIndexed implementation of Idx</summary>
   public int Idx { get; set; }

   /// <summary>The reference count for this buffer (how many RBatch objects are pointing to it)</summary>
   public int References {
      get => mReferences;
      set {
         mReferences = value;
         if (value < 0) throw new InvalidOperationException ("Negative reference count for RetainBuffer");
         if (value == 0) Release ();
      }
   }
   int mReferences;

   /// <summary>IGPU handle for the vertex buffer (allocated by PushToGPU)</summary>
   public int VertexBuffer => mVertexBuffer;
   int mVertexBuffer;

   /// <summary>The vertex specification for this buffer (layout of each vertex in it)</summary>
   public EVertexSpec VSpec {
      get => mSpec;
      set => mcbVertex = Attrib.GetSize (mSpec = value);
   }
   int mcbVertex;
   EVertexSpec mSpec;

   // Methods ------------------------------------------------------------------
   /// <summary>Add raw data into a RetainBuffer</summary>
   /// <param name="pSrc">Pointer to the data to add</param>
   /// <param name="cb">Count, in bytes, of the data</param>
   /// <returns>The index at which the first byte of data was added</returns>
   public unsafe int AddData (void* pSrc, int cb) {
      int n = mUsed;
      if (mUsed + cb > mData.Length)
         Array.Resize (ref mData, Math.Max (mUsed + cb, mData.Length * 2));
      fixed (void* pDst = &mData[mUsed])
         Buffer.MemoryCopy (pSrc, pDst, mData.Length, cb);
      mUsed += cb;
      return n;
   }

   /// <summary>Adds element indices into the Buffer, if we are using indexed-mode drawing</summary>
   public int AddIndices (ReadOnlySpan<int> seq) {
      int n = mIndexUsed, c = seq.Length;
      if (mIndexUsed + c > mIndex.Length)
         Array.Resize (ref mIndex, Math.Max (mIndexUsed + c, mIndex.Length * 2));
      seq.CopyTo (mIndex.AsSpan (mIndexUsed));
      mIndexUsed += c;
      return n;
   }

   /// <summary>Draws data from the vertex buffer using a simple Draw call (non-indexed)</summary>
   public void Draw (int offset, int count) {
      PushToGPU ();
      IGPU gpu = RenderState.It.GPU;
      gpu.SetVertexBuffer (mVertexBuffer, 0);
      gpu.Draw (count, offset / mcbVertex);
   }

   /// <summary>Draws data from the vertex/index buffers using indexed drawing</summary>
   public void Draw (int offset, int ioffset, int icount) {
      PushToGPU ();
      IGPU gpu = RenderState.It.GPU;
      gpu.SetVertexBuffer (mVertexBuffer, 0);
      gpu.SetIndexBuffer (mIndexBuffer, 0);
      gpu.DrawIndexed (icount, ioffset, offset / mcbVertex);
   }

   /// <summary>Gets a currently open RetainBuffer corresponding to a given vertex-spec</summary>
   /// An open retain-buffer is one that has not yet been pushed to the GPU, and is open
   /// for adding additional vertices into
   public static RetainBuffer Get (EVertexSpec spec) {
      RetainBuffer? rb = mBySpec[(int)spec];
      if (rb == null) (rb = mBySpec[(int)spec] = All.Alloc ()).VSpec = spec;
      return rb;
   }
   static readonly RetainBuffer?[] mBySpec = new RetainBuffer?[(int)EVertexSpec._Last];

   // Implementation -----------------------------------------------------------
   // Release the buffers after use.
   // Buffers are released after all the RBatch objects pointing into them are
   // released (when this.References goes down to zero)
   public void Release () {
      IGPU gpu = RenderState.It.GPU;
      if (mVertexBuffer != 0) gpu.DeleteBuffer (mVertexBuffer);
      if (mIndexBuffer != 0) gpu.DeleteBuffer (mIndexBuffer);
      mVertexBuffer = 0; mIndexBuffer = 0;
      All.Release (Idx);
   }

   // Called to transmit the data to the GPU.
   // The first time this is called, it allocates GPU buffers, copies the data
   // into them and uploads to the GPU. Subsequent calls are a no-op since the
   // data is already on the GPU
   unsafe void PushToGPU () {
      if (mVertexBuffer != 0) return;
      IGPU gpu = RenderState.It.GPU;

      // Create and upload vertex buffer
      mVertexBuffer = gpu.CreateBuffer (mUsed, false);
      fixed (void* p = &mData[0])
         gpu.UploadBuffer (mVertexBuffer, (Ptr)p, mUsed);
      PushedVerts = mUsed;

      // Create and upload index buffer
      if (mIndexUsed > 0) {
         mIndexBuffer = gpu.CreateBuffer (mIndexUsed * 4, true);
         fixed (void* p = &mIndex[0])
            gpu.UploadBuffer (mIndexBuffer, (Ptr)p, mIndexUsed * 4);
      }

      mData = null!; mIndex = null!; mBySpec[(int)VSpec] = null;
      mUsed = mIndexUsed = 0;
      PushID = ++mNextPushID;
   }
   public int PushID, PushedVerts;
   static int mNextPushID;

   public override string ToString ()
      => $"RBuffer Idx:{Idx}, Spec:{VSpec}, Push:{PushedVerts} bytes @ {PushID}";

   // Private data -------------------------------------------------------------
   byte[] mData = new byte[1024];   // Raw data storage
   int mUsed;                       // How many bytes of that have we used
   int[] mIndex = new int[128];     // Indices storage
   int mIndexUsed;                  // How many elements of the Indices array are used

   int mIndexBuffer;                // IGPU handle for the index buffer
}
#endregion

#region class StreamBuffer -------------------------------------------------------------------------
/// <summary>StreamBuffer implements CPU-staged streaming to the GPU via IGPU</summary>
/// The original OpenGL version used glMapBufferRange with UNSYNCHRONIZED for lock-free
/// streaming. WebGPU does not expose that pattern, so instead we maintain a CPU-side
/// staging buffer and create+upload a fresh GPU buffer for each draw call. The IGPU
/// backend is responsible for efficient buffer pooling/recycling.
///
/// In short, this is what we do:
/// - We maintain a CPU-side staging byte array
/// - Each time we want to draw, we copy vertex data into the staging buffer
/// - We create a transient GPU buffer, upload the data, bind it, and issue the draw
/// - The transient buffer is deleted immediately after use (the backend may defer
///   the actual deletion until the GPU is done with it)
class StreamBuffer {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a StreamBuffer</summary>
   public StreamBuffer () { }

   /// <summary>The singleton StreamBuffer instance</summary>
   public static StreamBuffer It => mIt ??= new ();
   static StreamBuffer? mIt;

   // Methods ------------------------------------------------------------------
   /// <summary>Copy data into a staging buffer and issue a Draw call via IGPU</summary>
   /// <param name="pSrc">The source buffer from where the 'vertex definitions' are picked</param>
   /// <param name="nVerts">The number of 'vertices'</param>
   /// <param name="cbVertex">The size of each vertex, in bytes</param>
   internal unsafe void Draw (void* pSrc, int nVerts, int cbVertex) {
      IGPU gpu = RenderState.It.GPU;
      int cbData = cbVertex * nVerts;

      // Create a transient GPU vertex buffer and upload the data
      int hBuffer = gpu.CreateBuffer (cbData, false);
      gpu.UploadBuffer (hBuffer, (Ptr)pSrc, cbData);

      // Bind and draw
      gpu.SetVertexBuffer (hBuffer, 0);
      gpu.Draw (nVerts, 0);

      // Release the transient buffer (the backend may defer actual deletion)
      gpu.DeleteBuffer (hBuffer);
   }
}
#endregion
