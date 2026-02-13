// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUBuffer.cs
// ║║║║╬║╔╣║ Wraps WebGPU buffer creation, data upload, mapping, and release
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using WBuffer = Silk.NET.WebGPU.Buffer;

#region class GPUBuffer -------------------------------------------------------------------------------
/// <summary>Wraps a WebGPU buffer for vertex, index, or uniform data</summary>
public unsafe class GPUBuffer : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU buffer handle</summary>
   public WBuffer* Handle => mBuffer;

   /// <summary>The size of this buffer in bytes</summary>
   public ulong Size => mSize;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a GPU buffer with the given size and usage flags</summary>
   public static GPUBuffer Create (GPUDevice gpu, ulong size, BufferUsage usage, string? label = null) {
      GPUBuffer buf = new () { mGPU = gpu, mSize = size };
      BufferDescriptor desc = new () {
         Size = size,
         Usage = usage,
         MappedAtCreation = false
      };
      if (label != null) desc.Label = (byte*)SilkMarshal.StringToPtr (label);
      buf.mBuffer = gpu.Api.DeviceCreateBuffer (gpu.Device, &desc);
      if (label != null) SilkMarshal.FreeString ((nint)desc.Label);
      if (buf.mBuffer == null) throw new Exception ("Failed to create WebGPU buffer");
      return buf;
   }

   /// <summary>Create a buffer initialized with mapped data, then unmap it</summary>
   public static GPUBuffer CreateWithData (GPUDevice gpu, ReadOnlySpan<byte> data, BufferUsage usage, string? label = null) {
      GPUBuffer buf = new () { mGPU = gpu, mSize = (ulong)data.Length };
      BufferDescriptor desc = new () {
         Size = (ulong)data.Length,
         Usage = usage,
         MappedAtCreation = true
      };
      if (label != null) desc.Label = (byte*)SilkMarshal.StringToPtr (label);
      buf.mBuffer = gpu.Api.DeviceCreateBuffer (gpu.Device, &desc);
      if (label != null) SilkMarshal.FreeString ((nint)desc.Label);
      if (buf.mBuffer == null) throw new Exception ("Failed to create WebGPU buffer");
      // Copy data into the mapped region
      void* mapped = gpu.Api.BufferGetMappedRange (buf.mBuffer, 0, (nuint)data.Length);
      data.CopyTo (new Span<byte> (mapped, data.Length));
      gpu.Api.BufferUnmap (buf.mBuffer);
      return buf;
   }

   /// <summary>Write data to this buffer via the queue</summary>
   public void Write (ReadOnlySpan<byte> data, ulong offset = 0) {
      fixed (byte* ptr = data)
         mGPU.Api.QueueWriteBuffer (mGPU.Queue, mBuffer, offset, ptr, (nuint)data.Length);
   }

   /// <summary>Write typed data to this buffer via the queue</summary>
   public void Write<T> (ReadOnlySpan<T> data, ulong offset = 0) where T : unmanaged {
      fixed (T* ptr = data)
         mGPU.Api.QueueWriteBuffer (mGPU.Queue, mBuffer, offset, ptr, (nuint)(data.Length * sizeof (T)));
   }

   /// <summary>Map the buffer for reading (async via callback)</summary>
   public void MapAsync (MapMode mode, nuint offset, nuint size, Action callback) {
      mGPU.Api.BufferMapAsync (mBuffer, mode, offset, size,
         new PfnBufferMapCallback ((s, _) => { if (s == BufferMapAsyncStatus.Success) callback (); }),
         null);
   }

   /// <summary>Get a pointer to the mapped range</summary>
   public void* GetMappedRange (nuint offset, nuint size)
      => mGPU.Api.BufferGetMappedRange (mBuffer, offset, size);

   /// <summary>Unmap the buffer</summary>
   public void Unmap () => mGPU.Api.BufferUnmap (mBuffer);

   /// <summary>Release the buffer</summary>
   public void Dispose () {
      if (mBuffer != null) { mGPU.Api.BufferRelease (mBuffer); mBuffer = null; }
      GC.SuppressFinalize (this);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   WBuffer* mBuffer;
   ulong mSize;
}
#endregion
