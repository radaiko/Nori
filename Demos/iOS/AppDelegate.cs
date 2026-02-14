// ────── ╔╗                                                                              DEMOS.IOS
// ╔═╦╦═╦╦╬╣ AppDelegate.cs
// ║║║║╬║╔╣║ UIKit entry point for the iOS demo application hosting cross-platform scenes
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
using System.Reflection;
using UIKit;
using Foundation;
using CoreGraphics;
namespace Nori;

#region class AppDelegate ---------------------------------------------------------------------------
/// <summary>UIApplicationDelegate entry point for the iOS demo application</summary>
[Register ("AppDelegate")]
class AppDelegate : UIApplicationDelegate {
   // Overrides ----------------------------------------------------------------
   public override UIWindow? Window { get; set; }

   public override bool FinishedLaunching (UIApplication application, NSDictionary? launchOptions) {
      Lib.Init ();
      Lux2.Init ();
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());

      Window = new UIWindow (UIScreen.MainScreen.Bounds);
      DemoViewController vc = new ();
      Window.RootViewController = vc;
      Window.MakeKeyAndVisible ();
      return true;
   }
}
#endregion

#region class DemoViewController --------------------------------------------------------------------
/// <summary>Main view controller hosting the GPU surface, demo picker, and settings panel</summary>
class DemoViewController : UIViewController {
   // Overrides ----------------------------------------------------------------
   public override void ViewDidLoad () {
      base.ViewDidLoad ();
      View!.BackgroundColor = UIColor.Black;

      // Create the iOS platform and renderable surface
      mPlatform = new iOSPlatform ();
      ISurface surface = mPlatform.CreateSurface ("Nori Demos", (int)View.Bounds.Width, (int)View.Bounds.Height);

      // Add the platform's view controller view as a child
      if (mPlatform.ViewController is { } platformVC) {
         AddChildViewController (platformVC);
         platformVC.View!.Frame = View.Bounds;
         platformVC.View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
         View.AddSubview (platformVC.View);
         platformVC.DidMoveToParentViewController (this);
      }

      // Create the demo picker (segmented control) at the top
      string[] names = new string[DemoRegistry.Scenes.Length];
      for (int i = 0; i < names.Length; i++)
         names[i] = DemoRegistry.Scenes[i].Name;
      mPicker = new UISegmentedControl (names) {
         SelectedSegment = 0,
         BackgroundColor = UIColor.FromRGBA (0, 0, 0, 160),
         TintColor = UIColor.White
      };
      mPicker.TranslatesAutoresizingMaskIntoConstraints = false;
      mPicker.ApportionsSegmentWidthsByContent = true;

      // Wrap the segmented control in a scroll view for many demos
      UIScrollView pickerScroll = new () {
         ShowsHorizontalScrollIndicator = true,
         ShowsVerticalScrollIndicator = false,
         TranslatesAutoresizingMaskIntoConstraints = false,
         BackgroundColor = UIColor.FromRGBA (0, 0, 0, 120)
      };
      pickerScroll.AddSubview (mPicker);
      View.AddSubview (pickerScroll);

      // Create the settings panel (vertical stack view) at the bottom
      mSettingsStack = new UIStackView {
         Axis = UILayoutConstraintAxis.Vertical,
         Spacing = 4,
         TranslatesAutoresizingMaskIntoConstraints = false,
         LayoutMarginsRelativeArrangement = true,
         LayoutMargins = new UIEdgeInsets (8, 8, 8, 8)
      };
      UIScrollView settingsScroll = new () {
         TranslatesAutoresizingMaskIntoConstraints = false,
         BackgroundColor = UIColor.FromRGBA (0, 0, 0, 160)
      };
      settingsScroll.AddSubview (mSettingsStack);
      View.AddSubview (settingsScroll);

      // Layout constraints
      #pragma warning disable CA1422 // KeyWindow is fine for our iOS 15.0 min target
      nfloat safeTop = UIApplication.SharedApplication.KeyWindow?.SafeAreaInsets.Top ?? 44;
      #pragma warning restore CA1422
      NSLayoutConstraint.ActivateConstraints ([
         // Picker scroll view at top
         pickerScroll.TopAnchor.ConstraintEqualTo (View.TopAnchor, safeTop),
         pickerScroll.LeadingAnchor.ConstraintEqualTo (View.LeadingAnchor),
         pickerScroll.TrailingAnchor.ConstraintEqualTo (View.TrailingAnchor),
         pickerScroll.HeightAnchor.ConstraintEqualTo (44),

         // Picker inside scroll
         mPicker.TopAnchor.ConstraintEqualTo (pickerScroll.TopAnchor),
         mPicker.LeadingAnchor.ConstraintEqualTo (pickerScroll.LeadingAnchor, 8),
         mPicker.TrailingAnchor.ConstraintEqualTo (pickerScroll.TrailingAnchor, -8),
         mPicker.HeightAnchor.ConstraintEqualTo (pickerScroll.HeightAnchor),

         // Settings scroll at bottom
         settingsScroll.BottomAnchor.ConstraintEqualTo (View.BottomAnchor),
         settingsScroll.LeadingAnchor.ConstraintEqualTo (View.LeadingAnchor),
         settingsScroll.TrailingAnchor.ConstraintEqualTo (View.TrailingAnchor),
         settingsScroll.HeightAnchor.ConstraintLessThanOrEqualTo (View.HeightAnchor, 0.3f),

         // Settings stack inside scroll
         mSettingsStack.TopAnchor.ConstraintEqualTo (settingsScroll.TopAnchor),
         mSettingsStack.LeadingAnchor.ConstraintEqualTo (settingsScroll.LeadingAnchor),
         mSettingsStack.TrailingAnchor.ConstraintEqualTo (settingsScroll.TrailingAnchor),
         mSettingsStack.BottomAnchor.ConstraintEqualTo (settingsScroll.BottomAnchor),
         mSettingsStack.WidthAnchor.ConstraintEqualTo (settingsScroll.WidthAnchor),
      ]);

      mPicker.ValueChanged += (_, _) => SwitchDemo ((int)mPicker.SelectedSegment);

      // Create the GPU backend and initialize Lux
      NativeGPU gpu = NativeGPU.Create (surface.NativeHandle);
      mGPU = gpu;
      Lux.Init (gpu, surface);
      Lux.OnReady.Subscribe (_ => {
         mManip = new SceneManipulator (mPlatform.Input);
         SwitchDemo (0);
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
      mSettingsPanel ??= new iOSSettingsPanel (mSettingsStack!);
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
   iOSPlatform? mPlatform;
   NativeGPU? mGPU;
   SceneManipulator? mManip;
   UISegmentedControl? mPicker;
   UIStackView? mSettingsStack;
   iOSSettingsPanel? mSettingsPanel;
   int mCurrentDemo;
}
#endregion
