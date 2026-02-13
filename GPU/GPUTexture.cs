// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUTexture.cs
// ║║║║╬║╔╣║ Wraps WebGPU texture creation, data upload, view, and sampler
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GPUTexture ------------------------------------------------------------------------------
/// <summary>Wraps a WebGPU texture with its view and optional sampler</summary>
public unsafe class GPUTexture : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU texture handle</summary>
   public Texture* Handle => mTexture;

   /// <summary>The default texture view</summary>
   public TextureView* View => mView;

   /// <summary>The texture sampler (null if not created)</summary>
   public Sampler* Sampler => mSampler;

   /// <summary>Texture width in pixels</summary>
   public uint Width => mWidth;

   /// <summary>Texture height in pixels</summary>
   public uint Height => mHeight;

   /// <summary>Texture format</summary>
   public TextureFormat Format => mFormat;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a 2D texture with the given dimensions and format</summary>
   public static GPUTexture Create (GPUDevice gpu, uint width, uint height, TextureFormat format,
      TextureUsage usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
      string? label = null) {

      GPUTexture tex = new () {
         mGPU = gpu, mWidth = width, mHeight = height, mFormat = format
      };
      TextureDescriptor desc = new () {
         Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
         MipLevelCount = 1,
         SampleCount = 1,
         Dimension = TextureDimension.Dimension2D,
         Format = format,
         Usage = usage
      };
      if (label != null) desc.Label = (byte*)SilkMarshal.StringToPtr (label);
      tex.mTexture = gpu.Api.DeviceCreateTexture (gpu.Device, &desc);
      if (label != null) SilkMarshal.FreeString ((nint)desc.Label);
      if (tex.mTexture == null) throw new Exception ("Failed to create WebGPU texture");

      // Create default view
      TextureViewDescriptor viewDesc = new () {
         Format = format,
         Dimension = TextureViewDimension.Dimension2D,
         MipLevelCount = 1,
         ArrayLayerCount = 1,
         BaseMipLevel = 0,
         BaseArrayLayer = 0,
         Aspect = TextureAspect.All
      };
      tex.mView = gpu.Api.TextureCreateView (tex.mTexture, &viewDesc);
      return tex;
   }

   /// <summary>Create a depth-stencil texture for render pass attachments</summary>
   public static GPUTexture CreateDepth (GPUDevice gpu, uint width, uint height,
      TextureFormat format = TextureFormat.Depth24PlusStencil8) {

      return Create (gpu, width, height, format,
         TextureUsage.RenderAttachment, label: "depth-stencil");
   }

   /// <summary>Write pixel data to this texture</summary>
   public void Write (ReadOnlySpan<byte> data, uint bytesPerRow) {
      ImageCopyTexture dst = new () {
         Texture = mTexture,
         MipLevel = 0,
         Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
         Aspect = TextureAspect.All
      };
      TextureDataLayout layout = new () {
         Offset = 0,
         BytesPerRow = bytesPerRow,
         RowsPerImage = mHeight
      };
      Extent3D size = new () {
         Width = mWidth,
         Height = mHeight,
         DepthOrArrayLayers = 1
      };
      fixed (byte* ptr = data)
         mGPU.Api.QueueWriteTexture (mGPU.Queue, &dst, ptr, (nuint)data.Length, &layout, &size);
   }

   /// <summary>Create a sampler for this texture</summary>
   public void CreateSampler (
      FilterMode minFilter = FilterMode.Linear,
      FilterMode magFilter = FilterMode.Linear,
      MipmapFilterMode mipmapFilter = MipmapFilterMode.Linear,
      AddressMode addressU = AddressMode.ClampToEdge,
      AddressMode addressV = AddressMode.ClampToEdge) {

      if (mSampler != null) { mGPU.Api.SamplerRelease (mSampler); mSampler = null; }
      SamplerDescriptor desc = new () {
         AddressModeU = addressU,
         AddressModeV = addressV,
         AddressModeW = AddressMode.ClampToEdge,
         MagFilter = magFilter,
         MinFilter = minFilter,
         MipmapFilter = mipmapFilter,
         LodMinClamp = 0f,
         LodMaxClamp = 1f,
         MaxAnisotropy = 1
      };
      mSampler = mGPU.Api.DeviceCreateSampler (mGPU.Device, &desc);
   }

   /// <summary>Release the texture, view, and sampler</summary>
   public void Dispose () {
      if (mSampler != null) { mGPU.Api.SamplerRelease (mSampler); mSampler = null; }
      if (mView != null) { mGPU.Api.TextureViewRelease (mView); mView = null; }
      if (mTexture != null) { mGPU.Api.TextureRelease (mTexture); mTexture = null; }
      GC.SuppressFinalize (this);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   Texture* mTexture;
   TextureView* mView;
   Sampler* mSampler;
   uint mWidth, mHeight;
   TextureFormat mFormat;
}
#endregion
