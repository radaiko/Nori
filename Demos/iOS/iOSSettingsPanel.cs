// ────── ╔╗                                                                              DEMOS.IOS
// ╔═╦╦═╦╦╬╣ iOSSettingsPanel.cs
// ║║║║╬║╔╣║ iOS implementation of ISettingsPanel using UIKit controls
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using UIKit;
using Foundation;
namespace Nori;

#region class iOSSettingsPanel -----------------------------------------------------------------------
/// <summary>iOS implementation of ISettingsPanel using a UIStackView container</summary>
class iOSSettingsPanel : ISettingsPanel {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a settings panel backed by the given UIStackView</summary>
   public iOSSettingsPanel (UIStackView container) => mContainer = container;

   // Methods ------------------------------------------------------------------
   /// <summary>Add a label to the panel</summary>
   public void AddLabel (string text) {
      UILabel label = new () {
         Text = text,
         Font = UIFont.BoldSystemFontOfSize (14),
         TextColor = UIColor.White
      };
      mContainer.AddArrangedSubview (label);
   }

   /// <summary>Add a slider control with name, range, initial value and change callback</summary>
   public void AddSlider (string name, double min, double max, double value, Action<double> onChange) {
      UILabel label = new () {
         Text = $"{name}: {value:F1}",
         Font = UIFont.SystemFontOfSize (12),
         TextColor = UIColor.LightGray
      };
      UISlider slider = new () {
         MinValue = (float)min,
         MaxValue = (float)max,
         Value = (float)value
      };
      slider.ValueChanged += (_, _) => {
         label.Text = $"{name}: {slider.Value:F1}";
         onChange (slider.Value);
      };
      mContainer.AddArrangedSubview (label);
      mContainer.AddArrangedSubview (slider);
   }

   /// <summary>Add a button with text and click callback</summary>
   public void AddButton (string text, Action onClick) {
      UIButton button = new (UIButtonType.System);
      button.SetTitle (text, UIControlState.Normal);
      button.TouchUpInside += (_, _) => onClick ();
      mContainer.AddArrangedSubview (button);
   }

   /// <summary>Add a list box with items, selected index and selection change callback</summary>
   public void AddListBox (string[] items, int selectedIndex, Action<int> onSelectionChanged) {
      PickerModel model = new (items);
      UIPickerView picker = new () { Model = model };
      picker.Select (selectedIndex, 0, false);
      model.ItemSelected += index => onSelectionChanged (index);
      mContainer.AddArrangedSubview (picker);
   }

   /// <summary>Clear all controls from the panel</summary>
   public void Clear () {
      foreach (UIView view in mContainer.ArrangedSubviews)
         mContainer.RemoveArrangedSubview (view);
      foreach (UIView view in mContainer.Subviews)
         view.RemoveFromSuperview ();
   }

   // Private data -------------------------------------------------------------
   UIStackView mContainer;
}
#endregion

#region class PickerModel ----------------------------------------------------------------------------
/// <summary>UIPickerViewModel providing items for a UIPickerView used in iOSSettingsPanel</summary>
class PickerModel : UIPickerViewModel {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a picker model with the given string items</summary>
   public PickerModel (string[] items) => mItems = items;

   // Events -------------------------------------------------------------------
   /// <summary>Fired when an item is selected, passing the selected index</summary>
   public event Action<int>? ItemSelected;

   // Overrides ----------------------------------------------------------------
   public override nint GetComponentCount (UIPickerView pickerView) => 1;

   public override nint GetRowsInComponent (UIPickerView pickerView, nint component)
      => mItems.Length;

   public override string GetTitle (UIPickerView pickerView, nint row, nint component)
      => mItems[(int)row];

   public override void Selected (UIPickerView pickerView, nint row, nint component)
      => ItemSelected?.Invoke ((int)row);

   // Private data -------------------------------------------------------------
   string[] mItems;
}
#endregion
