// ────── ╔╗                                                                          DEMOS.WINDOWS
// ╔═╦╦═╦╦╬╣ WpfSettingsPanel.cs
// ║║║║╬║╔╣║ WPF implementation of ISettingsPanel for building settings UI panels
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
namespace Nori;

#region class WpfSettingsPanel ------------------------------------------------------------------------
/// <summary>WPF implementation of ISettingsPanel using a StackPanel container</summary>
class WpfSettingsPanel : ISettingsPanel {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a settings panel backed by the given WPF StackPanel</summary>
   public WpfSettingsPanel (StackPanel panel) => mPanel = panel;

   // Methods ------------------------------------------------------------------
   /// <summary>Add a label to the panel</summary>
   public void AddLabel (string text) {
      TextBlock label = new () {
         Text = text,
         Margin = new Thickness (4, 8, 4, 2),
         FontWeight = FontWeights.Bold
      };
      mPanel.Children.Add (label);
   }

   /// <summary>Add a slider control with name, range, initial value and change callback</summary>
   public void AddSlider (string name, double min, double max, double value, Action<double> onChange) {
      StackPanel container = new () { Margin = new Thickness (4, 2, 4, 2) };
      TextBlock label = new () { Text = $"{name}: {value:F1}" };
      Slider slider = new () {
         Minimum = min, Maximum = max, Value = value,
         TickFrequency = (max - min) / 100,
         IsSnapToTickEnabled = false
      };
      slider.ValueChanged += (_, e) => {
         label.Text = $"{name}: {e.NewValue:F1}";
         onChange (e.NewValue);
      };
      container.Children.Add (label);
      container.Children.Add (slider);
      mPanel.Children.Add (container);
   }

   /// <summary>Add a button with text and click callback</summary>
   public void AddButton (string text, Action onClick) {
      Button button = new () {
         Content = text,
         Margin = new Thickness (4, 2, 4, 2),
         Padding = new Thickness (7, 1, 7, 1)
      };
      button.Click += (_, _) => onClick ();
      mPanel.Children.Add (button);
   }

   /// <summary>Add a list box with items, selected index and selection change callback</summary>
   public void AddListBox (string[] items, int selectedIndex, Action<int> onSelectionChanged) {
      ListBox listBox = new () {
         Margin = new Thickness (4, 2, 4, 2),
         Height = 150
      };
      foreach (string item in items) listBox.Items.Add (item);
      listBox.SelectedIndex = selectedIndex;
      listBox.SelectionChanged += (_, _) => {
         if (listBox.SelectedIndex >= 0) onSelectionChanged (listBox.SelectedIndex);
      };
      mPanel.Children.Add (listBox);
   }

   /// <summary>Clear all controls from the panel</summary>
   public void Clear () => mPanel.Children.Clear ();

   // Private data -------------------------------------------------------------
   StackPanel mPanel;
}
#endregion
