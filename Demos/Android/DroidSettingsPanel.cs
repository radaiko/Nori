// ────── ╔╗                                                                          DEMOS.ANDROID
// ╔═╦╦═╦╦╬╣ DroidSettingsPanel.cs
// ║║║║╬║╔╣║ Android implementation of ISettingsPanel using native Android views
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Android.Content;
using Android.Views;
using Android.Widget;
namespace Nori;

#region class DroidSettingsPanel ---------------------------------------------------------------------
/// <summary>Android implementation of ISettingsPanel using a LinearLayout container</summary>
class DroidSettingsPanel : ISettingsPanel {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a settings panel backed by the given LinearLayout and context</summary>
   public DroidSettingsPanel (LinearLayout container, Context context) {
      mContainer = container;
      mContext = context;
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Add a label to the panel</summary>
   public void AddLabel (string text) {
      TextView label = new (mContext) { Text = text };
      label.SetTextColor (Android.Graphics.Color.White);
      label.SetTypeface (null, Android.Graphics.TypefaceStyle.Bold);
      label.SetTextSize (Android.Util.ComplexUnitType.Sp, 14);
      LinearLayout.LayoutParams lp = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      lp.SetMargins (8, 16, 8, 4);
      mContainer.AddView (label, lp);
   }

   /// <summary>Add a slider control with name, range, initial value and change callback</summary>
   public void AddSlider (string name, double min, double max, double value, Action<double> onChange) {
      TextView label = new (mContext) { Text = $"{name}: {value:F1}" };
      label.SetTextColor (Android.Graphics.Color.LightGray);
      label.SetTextSize (Android.Util.ComplexUnitType.Sp, 12);

      SeekBar seekBar = new (mContext) { Max = 1000 };
      int progress = (int)((value - min) / (max - min) * 1000);
      seekBar.Progress = Math.Clamp (progress, 0, 1000);

      seekBar.ProgressChanged += (_, e) => {
         double v = min + (max - min) * e.Progress / 1000.0;
         label.Text = $"{name}: {v:F1}";
         onChange (v);
      };

      LinearLayout.LayoutParams lp = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      lp.SetMargins (8, 4, 8, 4);
      mContainer.AddView (label, lp);
      mContainer.AddView (seekBar, lp);
   }

   /// <summary>Add a button with text and click callback</summary>
   public void AddButton (string text, Action onClick) {
      Button button = new (mContext) { Text = text };
      button.Click += (_, _) => onClick ();
      LinearLayout.LayoutParams lp = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      lp.SetMargins (8, 4, 8, 4);
      mContainer.AddView (button, lp);
   }

   /// <summary>Add a list box with items, selected index and selection change callback</summary>
   public void AddListBox (string[] items, int selectedIndex, Action<int> onSelectionChanged) {
      Spinner spinner = new (mContext);
      ArrayAdapter<string> adapter = new (mContext,
         Android.Resource.Layout.SimpleSpinnerItem, items);
      adapter.SetDropDownViewResource (Android.Resource.Layout.SimpleSpinnerDropDownItem);
      spinner.Adapter = adapter;
      spinner.SetSelection (selectedIndex);
      spinner.ItemSelected += (_, e) => onSelectionChanged (e.Position);
      LinearLayout.LayoutParams lp = new (
         ViewGroup.LayoutParams.MatchParent,
         ViewGroup.LayoutParams.WrapContent);
      lp.SetMargins (8, 4, 8, 4);
      mContainer.AddView (spinner, lp);
   }

   /// <summary>Clear all controls from the panel</summary>
   public void Clear () => mContainer.RemoveAllViews ();

   // Private data -------------------------------------------------------------
   LinearLayout mContainer;
   Context mContext;
}
#endregion
