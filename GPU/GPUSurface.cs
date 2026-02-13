// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUSurface.cs
// ║║║║╬║╔╣║ Wraps WebGPU surface configuration, texture acquisition, and presentation
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GPUSurface ------------------------------------------------------------------------------
/// <summary>Wraps a WebGPU surface for swap chain presentation</summary>
public unsafe class GPUSurface : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU surface handle</summary>
   public Surface* Handle => mSurface;

   /// <summary>The current surface width in pixels</summary>
   public uint Width => mWidth;

   /// <summary>The current surface height in pixels</summary>
   public uint Height => mHeight;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a surface from a native window handle</summary>
   public static GPUSurface Create (GPUDevice gpu, IntPtr nativeHandle) {
      GPUSurface surf = new () { mGPU = gpu };
      surf.mSurface = CreateNativeSurface (gpu, nativeHandle);
      if (surf.mSurface == null) throw new Exception ("Failed to create WebGPU surface");
      return surf;
   }

   /// <summary>Wrap an existing WebGPU surface (e.g. created by a windowing library)</summary>
   public static GPUSurface Wrap (GPUDevice gpu, Surface* surface) {
      GPUSurface surf = new () { mGPU = gpu, mSurface = surface, mOwned = false };
      return surf;
   }

   /// <summary>Configure the surface for rendering</summary>
   public void Configure (uint width, uint height, PresentMode presentMode = PresentMode.Fifo) {
      mWidth = width;
      mHeight = height;
      SurfaceConfiguration config = new () {
         Device = mGPU.Device,
         Format = mGPU.SurfaceFormat,
         Usage = TextureUsage.RenderAttachment,
         PresentMode = presentMode,
         Width = width,
         Height = height
      };
      mGPU.Api.SurfaceConfigure (mSurface, &config);
   }

   /// <summary>Get the current texture view for rendering</summary>
   public TextureView* GetCurrentTextureView () {
      SurfaceTexture surfTex = new ();
      mGPU.Api.SurfaceGetCurrentTexture (mSurface, &surfTex);
      if (surfTex.Status != SurfaceGetCurrentTextureStatus.Success || surfTex.Texture == null)
         return null;
      TextureViewDescriptor viewDesc = new () {
         Format = mGPU.SurfaceFormat,
         Dimension = TextureViewDimension.Dimension2D,
         MipLevelCount = 1,
         ArrayLayerCount = 1,
         BaseMipLevel = 0,
         BaseArrayLayer = 0,
         Aspect = TextureAspect.All
      };
      return mGPU.Api.TextureCreateView (surfTex.Texture, &viewDesc);
   }

   /// <summary>Present the current frame to the display</summary>
   public void Present ()
      => mGPU.Api.SurfacePresent (mSurface);

   /// <summary>Reconfigure the surface after a resize</summary>
   public void Resize (uint width, uint height, PresentMode presentMode = PresentMode.Fifo)
      => Configure (width, height, presentMode);

   /// <summary>Release the surface</summary>
   public void Dispose () {
      if (mSurface != null && mOwned) {
         mGPU.Api.SurfaceRelease (mSurface);
      }
      mSurface = null;
      GC.SuppressFinalize (this);
   }

   // Implementation -----------------------------------------------------------
   // Creates a WebGPU surface from a native window handle using platform-specific
   // surface descriptors. Falls back per platform: Windows HWND, macOS Metal layer,
   // or X11/Wayland on Linux.
   static Surface* CreateNativeSurface (GPUDevice gpu, IntPtr nativeHandle) {
      if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows))
         return CreateWindowsSurface (gpu, nativeHandle);
      else if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX))
         return CreateMacOSSurface (gpu, nativeHandle);
      else if (RuntimeInformation.IsOSPlatform (OSPlatform.Linux))
         return CreateX11Surface (gpu, nativeHandle);
      throw new PlatformNotSupportedException ("WebGPU surface creation not supported on this platform");
   }

   static Surface* CreateWindowsSurface (GPUDevice gpu, IntPtr hwnd) {
      SurfaceDescriptorFromWindowsHWND hwndDesc = new () {
         Hwnd = (void*)hwnd,
         Hinstance = (void*)System.Diagnostics.Process.GetCurrentProcess ().Handle,
         Chain = new ChainedStruct { SType = SType.SurfaceDescriptorFromWindowsHwnd }
      };
      SurfaceDescriptor surfDesc = new () {
         NextInChain = (ChainedStruct*)(&hwndDesc)
      };
      return gpu.Api.InstanceCreateSurface (gpu.Instance, &surfDesc);
   }

   static Surface* CreateMacOSSurface (GPUDevice gpu, IntPtr metalLayer) {
      SurfaceDescriptorFromMetalLayer metalDesc = new () {
         Layer = (void*)metalLayer,
         Chain = new ChainedStruct { SType = SType.SurfaceDescriptorFromMetalLayer }
      };
      SurfaceDescriptor surfDesc = new () {
         NextInChain = (ChainedStruct*)(&metalDesc)
      };
      return gpu.Api.InstanceCreateSurface (gpu.Instance, &surfDesc);
   }

   static Surface* CreateX11Surface (GPUDevice gpu, IntPtr window) {
      SurfaceDescriptorFromXlibWindow xlibDesc = new () {
         Window = (uint)(long)window,
         Display = (void*)IntPtr.Zero,  // <<TODO>> Obtain X11 display handle
         Chain = new ChainedStruct { SType = SType.SurfaceDescriptorFromXlibWindow }
      };
      SurfaceDescriptor surfDesc = new () {
         NextInChain = (ChainedStruct*)(&xlibDesc)
      };
      return gpu.Api.InstanceCreateSurface (gpu.Instance, &surfDesc);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   Surface* mSurface;
   uint mWidth, mHeight;
   bool mOwned = true;
}
#endregion
