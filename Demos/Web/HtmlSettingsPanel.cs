// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ HtmlSettingsPanel.cs
// ║║║║╬║╔╣║ ISettingsPanel implementation that creates HTML elements via JS interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class HtmlSettingsPanel ----------------------------------------------------------------------
/// <summary>ISettingsPanel implementation that creates HTML elements via JS interop</summary>
/// Each control is created in the #settings div via NoriWebDemos JSImport calls.
/// Callbacks are registered in a static dictionary keyed by integer IDs, and
/// invoked from JS via DemoApp's JSExport methods.
public class HtmlSettingsPanel : ISettingsPanel {
   // Methods ------------------------------------------------------------------
   /// <summary>Add a label to the settings panel</summary>
   public void AddLabel (string text)
      => NoriWebDemos.AddLabel (text);

   /// <summary>Add a slider with name, range, initial value and change callback</summary>
   public void AddSlider (string name, double min, double max, double value, Action<double> onChange) {
      int id = sNextId++;
      sSliderCallbacks[id] = onChange;
      NoriWebDemos.AddSlider (name, min, max, value, id);
   }

   /// <summary>Add a button with text and click callback</summary>
   public void AddButton (string text, Action onClick) {
      int id = sNextId++;
      sButtonCallbacks[id] = onClick;
      NoriWebDemos.AddButton (text, id);
   }

   /// <summary>Add a list box with items, selected index and selection change callback</summary>
   public void AddListBox (string[] items, int selectedIndex, Action<int> onSelectionChanged) {
      int id = sNextId++;
      sListBoxCallbacks[id] = onSelectionChanged;
      NoriWebDemos.AddListBox (items, selectedIndex, id);
   }

   /// <summary>Clear all controls from the settings panel</summary>
   public void Clear () {
      sSliderCallbacks.Clear ();
      sButtonCallbacks.Clear ();
      sListBoxCallbacks.Clear ();
      sNextId = 1;
      NoriWebDemos.ClearSettings ();
   }

   // Implementation -----------------------------------------------------------
   // Invoke a registered slider callback by its ID
   internal static void InvokeSlider (int id, double value) {
      if (sSliderCallbacks.TryGetValue (id, out Action<double>? cb)) cb (value);
   }

   // Invoke a registered button callback by its ID
   internal static void InvokeButton (int id) {
      if (sButtonCallbacks.TryGetValue (id, out Action? cb)) cb ();
   }

   // Invoke a registered list box callback by its ID
   internal static void InvokeListBox (int id, int index) {
      if (sListBoxCallbacks.TryGetValue (id, out Action<int>? cb)) cb (index);
   }

   // Private data -------------------------------------------------------------
   static int sNextId = 1;
   static Dictionary<int, Action<double>> sSliderCallbacks = new ();
   static Dictionary<int, Action> sButtonCallbacks = new ();
   static Dictionary<int, Action<int>> sListBoxCallbacks = new ();
}
#endregion
