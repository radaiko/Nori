// ────── ╔╗                                                                       PLATFORM.WINDOWS
// ╔═╦╦═╦╦╬╣ WinSurface.cs
// ║║║║╬║╔╣║ Windows Forms UserControl implementing ISurface for WebGPU rendering
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using static System.Windows.Forms.ControlStyles;
using FCursor = System.Windows.Forms.Cursor;
namespace Nori;

#region class WinSurface ---------------------------------------------------------------------------
/// <summary>Windows Forms UserControl that provides a native HWND for WebGPU surface creation</summary>
class WinSurface : UserControl, ISurface {
   // Constructors -------------------------------------------------------------
   /// <summary>Create a new WinSurface with appropriate style bits for GPU rendering</summary>
   public WinSurface () {
      (DoubleBuffered, Name, AutoScaleMode) = (false, "WinSurface", AutoScaleMode.None);
      foreach (ControlStyles style in new[] { Opaque, UserPaint, AllPaintingInWmPaint }) SetStyle (style, true);
      foreach (ControlStyles style in new[] { OptimizedDoubleBuffer, Selectable }) SetStyle (style, false);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Surface size in pixels</summary>
   Vec2S ISurface.Size => new (Width, Height);

   /// <summary>DPI scale factor (1.0 for standard, 2.0 for HiDPI)</summary>
   public double DPIScale => DeviceDpi / 96.0;

   /// <summary>Native surface handle for WebGPU surface creation</summary>
   public IntPtr NativeHandle => Handle;

   /// <summary>Show or hide the cursor over the surface</summary>
   bool ISurface.CursorVisible {
      set => Cursor = value ? Cursors.Default : EmptyCursor;
   }

   /// <summary>Fired when the surface is resized</summary>
   public IObservable<Vec2S> Resized => mResized;

   /// <summary>Fired when the surface is ready for rendering</summary>
   public IObservable<int> Ready => mReady;

   // Overrides ----------------------------------------------------------------
   // Override CreateParams to specify custom class-style bits:
   // 1. CS_OWNDC — the control has its own private device context
   // 2. CS_HREDRAW | CS_VREDRAW — full redraw on resize
   protected override CreateParams CreateParams {
      get {
         CreateParams cp = base.CreateParams;
         const int CS_VREDRAW = 0x1, CS_HREDRAW = 0x2, CS_OWNDC = 0x20;
         cp.ClassStyle |= CS_HREDRAW | CS_VREDRAW | CS_OWNDC;
         return cp;
      }
   }

   // Push a Ready notification when the handle is created
   protected override void OnHandleCreated (EventArgs e) {
      base.OnHandleCreated (e);
      mReady.OnNext (0);
   }

   // Push a Resized notification when the control is resized
   protected override void OnResize (EventArgs e) {
      base.OnResize (e);
      mResized.OnNext (new Vec2S (Width, Height));
   }

   // Request a redraw of the surface
   void ISurface.Invalidate () => Invalidate ();

   // Implementation -----------------------------------------------------------
   // Lazily construct an invisible cursor for hiding the mouse
   static FCursor EmptyCursor {
      get {
         if (sEmptyCursor == null)
            using (System.IO.Stream stm = Lib.OpenRead ("nori:Cursor/Empty.cur"))
               sEmptyCursor = new FCursor (stm);
         return sEmptyCursor;
      }
   }

   // Private data -------------------------------------------------------------
   Subject<Vec2S> mResized = new ();
   Subject<int> mReady = new ();
   static FCursor? sEmptyCursor;
}
#endregion
