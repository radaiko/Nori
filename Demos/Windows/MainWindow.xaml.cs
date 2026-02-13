// ────── ╔╗                                                                          DEMOS.WINDOWS
// ╔═╦╦═╦╦╬╣ MainWindow.xaml.cs
// ║║║║╬║╔╣║ Main window hosting all cross-platform demo scenes via WinPlatform and Lux
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using Silk.NET.WebGPU;
using Silk.NET.Core.Native;
namespace Nori;

#region class MainWindow ------------------------------------------------------------------------------
/// <summary>WPF main window for the Demos.Windows application</summary>
public partial class MainWindow : Window {
   // Constructors -------------------------------------------------------------
   public MainWindow () {
      Lib.Init ();
      Lux2.Init ();
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
      InitializeComponent ();

      // Create the WinSurface and WinInput directly (avoiding WinPlatform's own Window)
      mSurface = new WinSurface ();
      mInput = new WinInput ();
      mInput.SetPanel (mSurface);

      // Host the WinSurface inside the content area via WindowsFormsHost
      mContent.Child = new WindowsFormsHost { Child = mSurface, Focusable = false };

      // Initialize GPU once the surface handle is ready
      mSurface.Ready.Subscribe (_ => InitGPU ());
   }

   // Implementation -----------------------------------------------------------
   unsafe void InitGPU () {
      if (mGPUInitialized) return;
      mGPUInitialized = true;

      // Create a WebGPU surface from the native HWND for adapter/device selection
      IntPtr hwnd = mSurface.NativeHandle;
      Silk.NET.WebGPU.WebGPU api = Silk.NET.WebGPU.WebGPU.GetApi ();
      InstanceDescriptor instDesc = new ();
      Instance* instance = api.CreateInstance (&instDesc);

      SurfaceDescriptorFromWindowsHWND hwndDesc = new () {
         Hwnd = (void*)hwnd,
         Hinstance = (void*)System.Diagnostics.Process.GetCurrentProcess ().Handle,
         Chain = new ChainedStruct { SType = SType.SurfaceDescriptorFromWindowsHwnd }
      };
      SurfaceDescriptor surfDesc = new () {
         NextInChain = (ChainedStruct*)(&hwndDesc)
      };
      Silk.NET.WebGPU.Surface* wgpuSurface = api.InstanceCreateSurface (instance, &surfDesc);

      // Create GPUDevice using the WebGPU surface for adapter selection
      mGPUDevice = GPUDevice.Create (wgpuSurface);

      // Wrap the existing surface for presentation
      GPUSurface gpuSurface = GPUSurface.Wrap (mGPUDevice, wgpuSurface);
      Vec2S sz = ((ISurface)mSurface).Size;
      if (sz.X > 0 && sz.Y > 0)
         gpuSurface.Configure ((uint)sz.X, (uint)sz.Y);

      // Create the IGPU adapter and initialize Lux
      mGPU = new NativeGPU (mGPUDevice, gpuSurface);
      Lux.Init (mGPU, mSurface);
      Lux.OnReady.Subscribe (OnLuxReady);
   }

   void OnLuxReady (int _) {
      PresentationSource? source = PresentationSource.FromVisual (this);
      if (source != null) Lux.DPIScale = (float)source.CompositionTarget.TransformToDevice.M11;
      TraceVN.TextColor = Color4.Yellow;
      mManip = new SceneManipulator (mInput);
   }

   void OnDemo (object sender, RoutedEventArgs e) {
      if (sender is not Button btn || btn.Tag is not string tagStr) return;
      if (!int.TryParse (tagStr, out int index)) return;
      if (index < 0 || index >= DemoRegistry.Scenes.Length) return;

      Scene scene = DemoRegistry.Scenes[index].Factory ();
      mSettings.Children.Clear ();
      Lux.UIScene = scene;

      // Use reflection to call CreateUI on RobotScene since it is internal to Demos.Shared
      MethodInfo? createUI = scene.GetType ().GetMethod ("CreateUI",
         BindingFlags.Public | BindingFlags.Instance, null, [typeof (ISettingsPanel)], null);
      if (createUI != null)
         createUI.Invoke (scene, [mSettingsPanel ??= new WpfSettingsPanel (mSettings)]);

      // Enable back-face highlighting for STEP and T3X scenes
      string typeName = scene.GetType ().Name;
      if (typeName is "STPScene" or "T3XDemoScene") Lux.BackFacesPink = true;
   }

   // Private data -------------------------------------------------------------
   WinSurface mSurface;
   WinInput mInput;
   NativeGPU? mGPU;
   GPUDevice? mGPUDevice;
   SceneManipulator? mManip;
   WpfSettingsPanel? mSettingsPanel;
   bool mGPUInitialized;
}
#endregion
