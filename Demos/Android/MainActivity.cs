// ────── ╔╗                                                                          DEMOS.ANDROID
// ╔═╦╦═╦╦╬╣ MainActivity.cs
// ║║║║╬║╔╣║ Android Activity entry point hosting cross-platform demo scenes via DroidPlatform
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
namespace Nori;

#region class MainActivity --------------------------------------------------------------------------
/// <summary>Main Android activity hosting the GPU surface, demo spinner, and settings panel</summary>
[Activity (Label = "Nori Demos", MainLauncher = true)]
class MainActivity : Activity {
   // Overrides ----------------------------------------------------------------
   protected override void OnCreate (Bundle? savedInstanceState) {
      base.OnCreate (savedInstanceState);
      Lib.Init ();
      Lux2.Init ();
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());

      // Build the root layout: vertical LinearLayout with spinner at top,
      // GPU surface in the middle, and settings at the bottom
      LinearLayout root = new (this) { Orientation = Orientation.Vertical };
      root.SetBackgroundColor (Android.Graphics.Color.Black);

      // Demo picker spinner at the top
      mSpinner = new Spinner (this);
      string[] names = new string[DemoRegistry.Scenes.Length];
      for (int i = 0; i < names.Length; i++)
         names[i] = DemoRegistry.Scenes[i].Name;
      ArrayAdapter<string> adapter = new (this,
         Android.Resource.Layout.SimpleSpinnerItem, names);
      adapter.SetDropDownViewResource (Android.Resource.Layout.SimpleSpinnerDropDownItem);
      mSpinner.Adapter = adapter;
      LinearLayout.LayoutParams spinnerLP = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      root.AddView (mSpinner, spinnerLP);

      // Create the platform and surface — the surface occupies the middle area
      mPlatform = new DroidPlatform (this);

      // Container for the GPU surface
      FrameLayout surfaceContainer = new (this);
      LinearLayout.LayoutParams surfaceLP = new (
         ViewGroup.LayoutParams.MatchParent, 0, 1.0f);
      root.AddView (surfaceContainer, surfaceLP);

      // Settings panel at the bottom (scrollable)
      ScrollView settingsScroll = new (this);
      mSettingsContainer = new LinearLayout (this) { Orientation = Orientation.Vertical };
      mSettingsContainer.SetBackgroundColor (Android.Graphics.Color.Argb (160, 0, 0, 0));
      settingsScroll.AddView (mSettingsContainer, new ViewGroup.LayoutParams (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent));
      LinearLayout.LayoutParams settingsLP = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      root.AddView (settingsScroll, settingsLP);

      SetContentView (root);

      // Create the renderable surface inside the container
      ISurface surface = mPlatform.CreateSurface ("Nori Demos", 0, 0);

      // The DroidPlatform sets the content view to the surface, but we need it
      // inside our container. Reparent the surface view.
      View? surfaceView = FindViewById (Android.Resource.Id.Content);
      if (surfaceView is ViewGroup vg && vg.ChildCount > 0) {
         View? child = vg.GetChildAt (vg.ChildCount - 1);
         if (child != null) {
            vg.RemoveView (child);
            surfaceContainer.AddView (child, new FrameLayout.LayoutParams (
               ViewGroup.LayoutParams.MatchParent,
               ViewGroup.LayoutParams.MatchParent));
         }
      }

      // Wire up demo selection
      mSpinner.ItemSelected += (_, e) => SwitchDemo (e.Position);

      // Create the GPU backend and initialize Lux
      surface.Ready.Subscribe (_ => {
         NativeGPU gpu = NativeGPU.Create (surface.NativeHandle);
         mGPU = gpu;
         Lux.Init (gpu, surface);
         Lux.OnReady.Subscribe (_ => {
            mManip = new SceneManipulator (mPlatform.Input);
            SwitchDemo (0);
         });
      });

      // Run the platform render loop
      mPlatform.Run (dt => { });
   }

   // Implementation -----------------------------------------------------------
   void SwitchDemo (int index) {
      mCurrentDemo = index;
      (string name, Func<Scene> factory) = DemoRegistry.Scenes[index];
      Scene scene = factory ();

      // Clear settings and set the scene
      mSettingsPanel ??= new DroidSettingsPanel (mSettingsContainer!, this);
      mSettingsPanel.Clear ();
      Lux.UIScene = scene;

      // Use reflection to call CreateUI if available (e.g. RobotScene)
      MethodInfo? createUI = scene.GetType ().GetMethod ("CreateUI",
         BindingFlags.Public | BindingFlags.Instance, null, [typeof (ISettingsPanel)], null);
      if (createUI != null)
         createUI.Invoke (scene, [mSettingsPanel]);

      // Enable back-face highlighting for STEP and T3X scenes
      string typeName = scene.GetType ().Name;
      if (typeName is "STPScene" or "T3XDemoScene") Lux.BackFacesPink = true;
   }

   // Private data -------------------------------------------------------------
   DroidPlatform? mPlatform;
   NativeGPU? mGPU;
   SceneManipulator? mManip;
   Spinner? mSpinner;
   LinearLayout? mSettingsContainer;
   DroidSettingsPanel? mSettingsPanel;
   int mCurrentDemo;
}
#endregion
