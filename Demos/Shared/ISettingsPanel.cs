// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ ISettingsPanel.cs
// ║║║║╬║╔╣║ Cross-platform interface for building settings UI panels
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region interface ISettingsPanel --------------------------------------------------------------------
/// <summary>Cross-platform interface for building settings UI panels</summary>
public interface ISettingsPanel {
   /// <summary>Add a label to the panel</summary>
   void AddLabel (string text);
   /// <summary>Add a slider control with name, range, initial value and change callback</summary>
   void AddSlider (string name, double min, double max, double value, Action<double> onChange);
   /// <summary>Add a button with text and click callback</summary>
   void AddButton (string text, Action onClick);
   /// <summary>Add a list box with items, selected index and selection change callback</summary>
   void AddListBox (string[] items, int selectedIndex, Action<int> onSelectionChanged);
   /// <summary>Clear all controls from the panel</summary>
   void Clear ();
}
#endregion
