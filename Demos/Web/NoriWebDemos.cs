// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ NoriWebDemos.cs
// ║║║║╬║╔╣║ JSImport declarations for demo sidebar and settings panel interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class NoriWebDemos ---------------------------------------------------------------------------
/// <summary>JS interop bindings to the nori-demos.js module</summary>
internal static partial class NoriWebDemos {
   // Sidebar ------------------------------------------------------------------
   /// <summary>Populate the sidebar with demo buttons from a list of names</summary>
   [JSImport ("noriDemos.populateSidebar", "nori-demos")]
   internal static partial void PopulateSidebar (string[] names);

   /// <summary>Highlight the active demo button by index</summary>
   [JSImport ("noriDemos.setActiveDemo", "nori-demos")]
   internal static partial void SetActiveDemo (int index);

   // Settings panel -----------------------------------------------------------
   /// <summary>Remove all children from the settings panel</summary>
   [JSImport ("noriDemos.clearSettings", "nori-demos")]
   internal static partial void ClearSettings ();

   /// <summary>Add a label element to the settings panel</summary>
   [JSImport ("noriDemos.addLabel", "nori-demos")]
   internal static partial void AddLabel (string text);

   /// <summary>Add a slider control with a JS callback ID for value changes</summary>
   [JSImport ("noriDemos.addSlider", "nori-demos")]
   internal static partial void AddSlider (string name, double min, double max, double value, int callbackId);

   /// <summary>Add a button with a JS callback ID for clicks</summary>
   [JSImport ("noriDemos.addButton", "nori-demos")]
   internal static partial void AddButton (string text, int callbackId);

   /// <summary>Add a list box with items and a JS callback ID for selection changes</summary>
   [JSImport ("noriDemos.addListBox", "nori-demos")]
   internal static partial void AddListBox (string[] items, int selectedIndex, int callbackId);
}
#endregion
