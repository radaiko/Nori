// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUDevice.cs
// ║║║║╬║╔╣║ Wraps WebGPU instance, adapter, device, and queue lifecycle
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class GPUDevice -------------------------------------------------------------------------------
/// <summary>Manages the WebGPU lifecycle: instance, adapter, device, and queue</summary>
public unsafe class GPUDevice : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The Silk.NET WebGPU API entry point</summary>
   public WebGPU Api => mApi;

   /// <summary>The WebGPU instance</summary>
   public Instance* Instance => mInstance;

   /// <summary>The WebGPU adapter (physical GPU)</summary>
   public Adapter* Adapter => mAdapter;

   /// <summary>The WebGPU logical device</summary>
   public Device* Device => mDevice;

   /// <summary>The default command queue</summary>
   public Queue* Queue => mQueue;

   /// <summary>The preferred surface texture format</summary>
   public TextureFormat SurfaceFormat => mSurfaceFormat;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a GPUDevice from a native surface handle (HWND, NSWindow, X11 Window)</summary>
   public static GPUDevice Create (Surface* surface) {
      GPUDevice gpu = new ();
      gpu.Init (surface);
      return gpu;
   }

   /// <summary>Create a GPUDevice with only the WebGPU API and instance (no adapter/device yet)</summary>
   public static GPUDevice CreateInstanceOnly () {
      GPUDevice gpu = new ();
      gpu.mApi = WebGPU.GetApi ();
      InstanceDescriptor instanceDesc = new ();
      gpu.mInstance = gpu.mApi.CreateInstance (&instanceDesc);
      if (gpu.mInstance == null) throw new Exception ("Failed to create WebGPU instance");
      return gpu;
   }

   /// <summary>Complete device initialization with an existing surface (adapter, device, queue)</summary>
   public void InitDevice (Surface* surface) {
      // Request adapter (synchronous via callback)
      RequestAdapterOptions adapterOpts = new () {
         CompatibleSurface = surface,
         PowerPreference = PowerPreference.HighPerformance
      };
      Adapter* adapter = null;
      mApi.InstanceRequestAdapter (
         mInstance, in adapterOpts,
         new PfnRequestAdapterCallback ((_, a, _, _) => adapter = a),
         null
      );
      mAdapter = adapter;
      if (mAdapter == null) throw new Exception ("Failed to request WebGPU adapter");

      // Query surface capabilities for preferred format
      SurfaceCapabilities caps = new ();
      mApi.SurfaceGetCapabilities (surface, mAdapter, &caps);
      mSurfaceFormat = caps.Formats != null ? caps.Formats[0] : TextureFormat.Bgra8Unorm;

      // Request device (synchronous via callback)
      DeviceDescriptor deviceDesc = new () {
         DeviceLostCallback = new PfnDeviceLostCallback (OnDeviceLost)
      };
      Device* device = null;
      mApi.AdapterRequestDevice (
         mAdapter, in deviceDesc,
         new PfnRequestDeviceCallback ((_, d, _, _) => device = d),
         null
      );
      mDevice = device;
      if (mDevice == null) throw new Exception ("Failed to request WebGPU device");

      // Set up uncaptured error callback
      mApi.DeviceSetUncapturedErrorCallback (
         mDevice,
         new PfnErrorCallback (OnUncapturedError),
         null
      );

      // Get the default queue
      mQueue = mApi.DeviceGetQueue (mDevice);
   }

   /// <summary>Release all WebGPU resources</summary>
   public void Dispose () {
      if (mDevice != null) { mApi.DeviceRelease (mDevice); mDevice = null; }
      if (mAdapter != null) { mApi.AdapterRelease (mAdapter); mAdapter = null; }
      if (mInstance != null) { mApi.InstanceRelease (mInstance); mInstance = null; }
      mQueue = null;
      GC.SuppressFinalize (this);
   }

   // Implementation -----------------------------------------------------------
   void Init (Surface* surface) {
      mApi = WebGPU.GetApi ();

      // Create WebGPU instance
      InstanceDescriptor instanceDesc = new ();
      mInstance = mApi.CreateInstance (&instanceDesc);
      if (mInstance == null) throw new Exception ("Failed to create WebGPU instance");

      // Request adapter (synchronous via callback)
      RequestAdapterOptions adapterOpts = new () {
         CompatibleSurface = surface,
         PowerPreference = PowerPreference.HighPerformance
      };
      Adapter* adapter = null;
      mApi.InstanceRequestAdapter (
         mInstance, in adapterOpts,
         new PfnRequestAdapterCallback ((_, a, _, _) => adapter = a),
         null
      );
      mAdapter = adapter;
      if (mAdapter == null) throw new Exception ("Failed to request WebGPU adapter");

      // Query surface capabilities for preferred format
      SurfaceCapabilities caps = new ();
      mApi.SurfaceGetCapabilities (surface, mAdapter, &caps);
      mSurfaceFormat = caps.Formats != null ? caps.Formats[0] : TextureFormat.Bgra8Unorm;

      // Request device (synchronous via callback)
      DeviceDescriptor deviceDesc = new () {
         DeviceLostCallback = new PfnDeviceLostCallback (OnDeviceLost)
      };
      Device* device = null;
      mApi.AdapterRequestDevice (
         mAdapter, in deviceDesc,
         new PfnRequestDeviceCallback ((_, d, _, _) => device = d),
         null
      );
      mDevice = device;
      if (mDevice == null) throw new Exception ("Failed to request WebGPU device");

      // Set up uncaptured error callback
      mApi.DeviceSetUncapturedErrorCallback (
         mDevice,
         new PfnErrorCallback (OnUncapturedError),
         null
      );

      // Get the default queue
      mQueue = mApi.DeviceGetQueue (mDevice);
   }

   static void OnDeviceLost (DeviceLostReason reason, byte* message, void* userData) {
      string msg = SilkMarshal.PtrToString ((nint)message) ?? "Unknown";
      Console.Error.WriteLine ($"WebGPU device lost ({reason}): {msg}");
   }

   static void OnUncapturedError (ErrorType type, byte* message, void* userData) {
      string msg = SilkMarshal.PtrToString ((nint)message) ?? "Unknown";
      Console.Error.WriteLine ($"WebGPU error ({type}): {msg}");
   }

   // Private data -------------------------------------------------------------
   WebGPU mApi = null!;
   Instance* mInstance;
   Adapter* mAdapter;
   Device* mDevice;
   Queue* mQueue;
   TextureFormat mSurfaceFormat;
}
#endregion
