// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ DemoApp.cs
// ║║║║╬║╔╣║ Central demo application controller — manages scene switching and settings callbacks
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class DemoApp --------------------------------------------------------------------------------
/// <summary>Central demo application controller that manages scene switching and settings</summary>
/// Exposes JSExport methods called by nori-demos.js when the user interacts with
/// the sidebar buttons or settings panel controls. On each demo selection, the
/// corresponding Scene is instantiated from DemoRegistry and set as Lux.UIScene.
public static partial class DemoApp {
   // Methods ------------------------------------------------------------------
   /// <summary>Initialize the demo application after Lux is ready</summary>
   public static void Init () {
      // Populate the sidebar with demo names
      string[] names = new string[DemoRegistry.Scenes.Length];
      for (int i = 0; i < names.Length; i++)
         names[i] = DemoRegistry.Scenes[i].Name;
      NoriWebDemos.PopulateSidebar (names);
      sPanel = new HtmlSettingsPanel ();
      // Select the first demo by default
      SelectDemo (0);
   }

   // JSExport callbacks -------------------------------------------------------
   /// <summary>Called from JS when a demo sidebar button is clicked</summary>
   [JSExport]
   public static void OnDemoSelected (int index)
      => SelectDemo (index);

   /// <summary>Called from JS when a slider value changes</summary>
   [JSExport]
   public static void OnSliderChanged (int callbackId, double value)
      => HtmlSettingsPanel.InvokeSlider (callbackId, value);

   /// <summary>Called from JS when a button is clicked</summary>
   [JSExport]
   public static void OnButtonClicked (int callbackId)
      => HtmlSettingsPanel.InvokeButton (callbackId);

   /// <summary>Called from JS when a list box selection changes</summary>
   [JSExport]
   public static void OnListBoxChanged (int callbackId, int index)
      => HtmlSettingsPanel.InvokeListBox (callbackId, index);

   // Implementation -----------------------------------------------------------
   static void SelectDemo (int index) {
      if (index < 0 || index >= DemoRegistry.Scenes.Length) return;
      sPanel?.Clear ();
      try {
         Scene scene = DemoRegistry.Scenes[index].Factory ();
         Lux.UIScene = scene;
         NoriWebDemos.SetActiveDemo (index);
         // Check if the scene has a CreateUI(ISettingsPanel) method via reflection
         // (scene types are internal in Demos.Shared, so we cannot cast directly)
         System.Reflection.MethodInfo? createUI = scene.GetType ().GetMethod (
            "CreateUI", [typeof (ISettingsPanel)]);
         if (createUI != null && sPanel != null)
            createUI.Invoke (scene, [sPanel]);
         // Similarly check for scenes that want BackFacesPink
         string name = DemoRegistry.Scenes[index].Name;
         if (name is "Load STEP" or "Load T3X File") Lux.BackFacesPink = true;
      } catch (Exception ex) {
         Console.WriteLine ($"Demo failed: {ex.Message}");
         NoriWebDemos.SetActiveDemo (index);
      }
   }

   // Private data -------------------------------------------------------------
   static HtmlSettingsPanel? sPanel;
}
#endregion
