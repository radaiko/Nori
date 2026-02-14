// ────── ╔╗                                                                                      LUX
// ╔═╦╦═╦╦╬╣ Lux.cs
// ║║║║╬║╔╣║ The Lux class: public interface to the Lux rendering engine
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Subjects;
namespace Nori;

#region class Lux ----------------------------------------------------------------------------------
/// <summary>The public interface to the Lux renderer</summary>
public static partial class Lux {
   // Properties ---------------------------------------------------------------
   /// <summary>If set, back faces are colored pink (useful for debugging) when using the Phong shader</summary>
   public static bool BackFacesPink;

   /// <summary>Subscribe to this to get a FPS (frames-per-second) report each second</summary>
   public static IObservable<int> FPS => mFPS;
   static readonly Subject<int> mFPS = new ();

   /// <summary>Subscribe to this to get statistics after each frame is rendered</summary>
   public static IObservable<Stats> Info => mInfo;
   static readonly Subject<Stats> mInfo = new ();

   /// <summary>If set, we are redering a frame for 'picking'</summary>
   public static bool IsPicking => mIsPicking;
   static bool mIsPicking;

   /// <summary>Subscribe to this to know when Lux is ready (event raised only once)</summary>
   public static IObservable<int> OnReady => mOnReady;
   internal static ReplaySubject<int> mOnReady = new (1);

   /// <summary>The platform surface used for cursor visibility and redraw requests</summary>
   public static ISurface? Surface { get => mSurface; set => mSurface = value; }
   static ISurface? mSurface;

   /// <summary>Sets whether the cursor is visible or not when it is over the panel</summary>
   /// If this is set to false, then the current scene must 'paint' a cursor that follows
   /// the mouse movement
   public static bool CursorVisible { set { if (mSurface != null) mSurface.CursorVisible = value; } }

   /// <summary>The current scene that is bound to the visible viewport</summary>
   public static Scene? UIScene {
      get => mUIScene;
      set {
         mUIScene?.Detach ();
         BackFacesPink = false;
         mUIScene = value; mUIScene?.Attach (); mViewBound.OnNext (0); Redraw ();
         if (mSurface != null) mSurface.CursorVisible = mUIScene?.CursorVisible ?? true;
      }
   }
   static Scene? mUIScene;

   /// <summary>How many world units does one pixel correspond to (for the current scene)</summary>
   public static double PixelScale {
      get {
         if (mUIScene == null || mViewport.X == 0) return 1;
         Matrix3 xfm = mUIScene.Xfms[0].InvXfm;
         double dx = 2.0 / mViewport.X;   //
         Point3 pa = Point3.Zero * xfm, pb = new Point3 (dx, 0, 0) * xfm;
         return pa.DistTo (pb);
      }
   }

   /// <summary>Subscribe to this to know when the 'View-Bound' changes (view is zoomed, panned or rotated)</summary>
   public static IObservable<int> ViewBound => mViewBound;
   internal static Subject<int> mViewBound = new ();

   /// <summary>The viewport size (in pixels) of the Lux rendering panel</summary>
   public static Vec2S Viewport => mViewport;
   static Vec2S mViewport;

   // Methods ------------------------------------------------------------------
   /// <summary>Initialize Lux with a GPU backend and surface</summary>
   public static void Init (IGPU gpu, ISurface surface) {
      RenderState.Init (gpu);
      mSurface = surface;
      surface.Ready.Subscribe (_ => { mReady = true; mOnReady.OnNext (0); });
      surface.Resized.Subscribe (size => Render (UIScene, size, ETarget.Screen, DIBitmap.EFormat.Unknown));
   }

   public static void DumpStats () {
      Debug.Print ("Buffers:");
      foreach (RetainBuffer buf in RetainBuffer.All.GetSnapshot ()) Debug.Print (buf.ToString ());
   }

   /// <summary>Called when entities are redrawn, or when the transform changes</summary>
   /// At these times, the pick buffer must be flushed so we don't pick on a stale
   /// pick buffer
   public static void FlushPickBuffer () => mPickBufferValid = false;
   static bool mPickBufferValid;

   /// <summary>Render a Scene to an image (for example, to generate a thumbnail)</summary>
   public static DIBitmap RenderToImage (Scene scene, Vec2S size, DIBitmap.EFormat fmt) {
      if (size.X % 4 != 0) throw new ArgumentException ("Lux.RenderToImage: image width must be a multiple of 4");
      if (scene != Lux.UIScene) scene.Attach ();
      DIBitmap dib = (DIBitmap)Render (scene, size, ETarget.Image, fmt)!;
      if (scene != Lux.UIScene) scene.Detach ();
      return dib;
   }

   /// <summary>This does a 'pick' operation on the current UIScene</summary>
   /// This effectively returns the VNode that lies underneat the current mouse position.
   public static VNode? Pick (Vec2S pos) {
      // If we're doign any simulation, return null
      if (sRenderCompletes.Count > 0 || mRendering || !mReady || mUIScene == null) return null;
      if (!mPickBufferValid) {
         mPickBufferValid = true;
         (byte[], float[]) tup = ((byte[], float[]))Render (mUIScene, mViewport, ETarget.Pick, DIBitmap.EFormat.Unknown)!;
         mPickPixel = tup.Item1; mPickDepth = tup.Item2;
      }
      int index = (mViewport.Y - pos.Y - 1) * mViewport.X + pos.X;
      if (index < 0 || index >= mPickDepth.Length) return null;
      float fDepth = mPickDepth[index];

      // Now, abandon the LSB 2 bits of r, g and b leaving only 6 bits each (this is to
      // avoid round off errors in low-bit depth color buffers
      index *= 4;
      int b = mPickPixel[index] >> 2, g = mPickPixel[index + 1] >> 2, r = mPickPixel[index + 2] >> 2;
      int vnodeId = r + (g << 6) + (b << 12);
      VNode? node = VNode.SafeGet (vnodeId);
      if (node != null) PickPos = mUIScene.Unproject (pos, fDepth);
      return node;
   }

   public static Point3 PickPos;

   public static bool Ready => mReady;
   static bool mReady;

   /// <summary>Converts a pixel coordinate to world coordinates</summary>
   public static Point3 PixelToWorld (Vec2S pix) {
      if (mUIScene == null) return new (pix.X, pix.Y, 0);
      // Convert pixel coordinate to OpenGL clip space coordinates.
      Vec2S vp = mViewport;
      Point3 clip = new (2.0 * pix.X / vp.X - 1, 1.0 - 2.0 * pix.Y / vp.Y, 0);
      clip *= mUIScene.Xfms[0].InvXfm;
      int d = PixelScale switch { > 1 => 0, > 0.1 => 1, > 0.01 => 2, > 0.001 => 3, _ => 4 };
      clip = new (Math.Round (clip.X, d), Math.Round (clip.Y, d), Math.Round (clip.Z, d));
      return clip;
   }

   /// <summary>Stub for the Render method that is called when each frame has to be painted</summary>
   internal static object? Render (Scene? scene, Vec2S viewport, ETarget target, DIBitmap.EFormat fmt) {
      mcFrames++; mcFPSFrames++;
      mIsPicking = target == ETarget.Pick;
      if (mRendering) throw new InvalidOperationException ();
      mRendering = true;
      BeginRender (viewport, target);
      StartFrame (viewport);
      Color4 bgrdColor = mIsPicking ? Color4.White : (scene?.BgrdColor ?? Color4.Gray (96));
      GLState.StartFrame (viewport, bgrdColor);
      RBatch.StartFrame ();
      Shader.StartFrame ();
      scene?.Render (viewport);
      object? obj = EndRender (target, fmt);
      if (target == ETarget.Screen) RenderState.It.GPU.Present ();

      // Various post-processing after frame render
      // Issue stats, and keep 'continuous render' loop going
      mInfo.OnNext (sStats);
      DateTime frameTS = DateTime.Now;
      mLastFrameTime = (DateTime.Now - sLastFrametime).TotalSeconds;
      if (sRenderCompletes.Count > 0 && target == ETarget.Screen) {
         Lib.Post (NextFrame);
         double elapsed = (frameTS - mFPSReportTS).TotalSeconds;
         if (elapsed >= 1.0) {
            // Every 1 second, issue an FPS (frames-per-second) report
            int fps = (int)(mcFPSFrames / elapsed + 0.5);
            mFPS.OnNext (fps);
            (mcFPSFrames, mFPSReportTS) = (0, frameTS);
         }
      }
      sLastFrametime = frameTS;
      mRendering = mIsPicking = false;
      return obj;

      // Helpers ...........................................
      static void NextFrame () {
         for (int i = sRenderCompletes.Count - 1; i >= 0; i--)
            sRenderCompletes[i] (mLastFrameTime);
         Redraw ();
      }
   }
   static int mcFrames;             // Frames rendered totally
   static double mLastFrameTime;    // How many seconds did the last frame take to render
   static DateTime mFPSReportTS;    // When did we last issue an FPS report
   static int mcFPSFrames;          // Frames rendered since that time
   static bool mRendering;          // Currently rendering a frame

   static void BeginRender (Vec2S viewport, ETarget target) {
      IGPU gpu = RenderState.It.GPU;
      if (target is ETarget.Image or ETarget.Pick) {
         mFBViewport = viewport;
         if (viewport.X > mFBSize.X || viewport.Y > mFBSize.Y) {
            if (mFrameBufferHandle != 0) gpu.DeleteFramebuffer (mFrameBufferHandle);
            mFrameBufferHandle = gpu.CreateFramebuffer (viewport.X, viewport.Y);
            mFBSize = viewport;
         }
         gpu.BindFramebuffer (mFrameBufferHandle);
      } else
         gpu.BindDefaultFramebuffer ();
   }
   static Vec2S mFBViewport;            // Viewport size, when rendering to a frame-buffer
   static int mFrameBufferHandle;       // IGPU framebuffer handle for image rendering
   static Vec2S mFBSize;                // The size of the frame-buffer
   static float[] mPickDepth = [];      // The depth buffer, obtained during a Pick render
   // This buffer contains the raw pixel-data obtained from a pick operation.
   // Since the models are drawn in 'false-color' mode during a pick operation, this buffer
   // effectively contains indices into the VModels list. Some finagling is required, such
   // as discarding the least signifcant bits of each color component etc (see the code in
   // Lux.Pick which reads and interprets these buffers)
   static byte[] mPickPixel = [];

   static object? EndRender (ETarget target, DIBitmap.EFormat fmt) {
      IGPU gpu = RenderState.It.GPU;
      switch (target) {
         case ETarget.Image:
            int x = mFBViewport.X, y = mFBViewport.Y;
            byte[] rgba = gpu.ReadPixels (0, 0, x, y);
            if (fmt == DIBitmap.EFormat.RGBA8)
               return new DIBitmap (x, y, fmt, rgba);
            int bpp = fmt.BytesPerPixel ();
            byte[] data = new byte[bpp * x * y];
            ConvertPixels (rgba, data, x * y, fmt);
            return new DIBitmap (x, y, fmt, data);
         case ETarget.Pick:
            int px = mFBViewport.X, py = mFBViewport.Y;
            int size = px * py;
            if (size > mPickDepth.Length)
               (mPickPixel, mPickDepth) = (new byte[size * 4], new float[size]);
            byte[] pixels = gpu.ReadPixels (0, 0, px, py);
            Array.Copy (pixels, mPickPixel, Math.Min (pixels.Length, mPickPixel.Length));
            // Depth reading: IGPU.ReadPixels returns color only.
            // For pick, depth is secondary — picking works by color ID.
            Array.Clear (mPickDepth);
            return (mPickPixel, mPickDepth);
      }
      return null;
   }

   // Convert RGBA pixel data to RGB8 or Gray8 format
   static void ConvertPixels (byte[] rgba, byte[] dst, int pixelCount, DIBitmap.EFormat fmt) {
      if (fmt == DIBitmap.EFormat.RGB8) {
         for (int i = 0, s = 0, d = 0; i < pixelCount; i++, s += 4, d += 3) {
            dst[d] = rgba[s]; dst[d + 1] = rgba[s + 1]; dst[d + 2] = rgba[s + 2];
         }
      } else if (fmt == DIBitmap.EFormat.Gray8) {
         for (int i = 0, s = 0; i < pixelCount; i++, s += 4)
            dst[i] = rgba[s];
      }
   }

   /// <summary>Prompts the Lux system to redraw the screen (asynchronous)</summary>
   public static void Redraw () { mNeedRedraw = true; mSurface?.Invalidate (); }

   /// <summary>Called from the platform's animation-frame callback to render when dirty</summary>
   /// On web (requestAnimationFrame), the platform calls Tick() each frame. On desktop, the
   /// WPF CompositionTarget.Rendering event handles rendering directly, so Tick() is unused.
   public static void Tick () {
      if (!mReady || mRendering || mSurface == null || !mNeedRedraw) return;
      mNeedRedraw = false;
      Render (UIScene, mSurface.Size, ETarget.Screen, DIBitmap.EFormat.Unknown);
   }
   static bool mNeedRedraw;

   /// <summary>This is called to initiate 'continuous rendering'</summary>
   /// This function takes a 'callback' that will be invoked after each frame is rendered. Once
   /// this is started, Lux renders frames continuously, attempting to render at the monitor
   /// refresh rate (60 fps) if the hardware is fast enough. If Lux.VSync is turned off, then
   /// it renders at the maximum possible rate (regardless of monitor refresh rate).
   ///
   /// The 'elapsed-time' since the last time the callback was called (in seconds) is passed as
   /// a parameter to the callback, which can use this parameter to adjust the positions
   /// of objects in the scene. Thus, it is possible to create simulation where the simulation
   /// speed is not dependent on the number of frames we render per second.
   ///
   /// It is possible to call StartContinuousRender any number of times, attaching different
   /// callbacks. The continuous-render goes on as long as at least one such callback is attached,
   /// and after each frame is rendered, all these callbacks are invoked. Once all these callbacks
   /// retire (by calling StopContinuousRender), we stop the render pump, and subsequent renders
   /// happen only on-demand (when the VNode tree changes, or the window size changes etc)
   public static void StartContinuousRender (Action<double> renderComplete) {
      sRenderCompletes.Add (renderComplete);
      if (sRenderCompletes.Count == 1) {
         // If this is the first render-complete function, start the backup timer running.
         // We need this backup timer because the RenderComplete event is not always dependable.
         // Normally, if we are running at 60 fps, we should hit the render-complete each 16.66 ms,
         // and the timer would never fire.
         if (sTimer == null)
            sTimer = new Timer (_ => Lib.Post (Redraw), null, 0, 40);
         else
            sTimer.Change (0, 40);
         // Issue one redraw to prime things off
         sLastFrametime = DateTime.Now;
         Redraw ();
      }
   }
   static DateTime sLastFrametime;
   static readonly List<Action<double>> sRenderCompletes = [];
   static Timer? sTimer;

   /// <summary>This detaches a callback from the continous-render loop</summary>
   /// This is the opposite of StartContinuousRender above. Once all the callbacks have
   /// retired, we stop the loop.
   public static void StopContinuousRender (Action<double> renderComplete) {
      sRenderCompletes.Remove (renderComplete);
      if (sRenderCompletes.Count == 0 && sTimer != null)
         sTimer.Change (Timeout.Infinite, Timeout.Infinite);
   }

   // Internal properties ------------------------------------------------------
   /// <summary>Bumped up whenever any Lux draw property is changed (used for shader optimizations)</summary>
   internal static int Rung;

   /// <summary>The scene that is currently being rendered (set only during a Render() call)</summary>
   internal static Scene? Scene;

   // Internal methods ---------------------------------------------------------
   /// <summary>Called when we start rendering a VNode (and it's subtree)</summary>
   /// The corresponding EndNode is called after the entire subtree under
   /// this VNode is completed rendering. Because of this, there could be multiple
   /// open 'BeginNode' calls whose EndNode is pending
   internal static void BeginNode (VNode node) {
      mNodeStack.Push ((mVNode, mChanged));
      (mVNode, mChanged) = (node, ELuxAttr.None);
   }
   static readonly Stack<(VNode?, ELuxAttr)> mNodeStack = [];

   /// <summary>Called when a node is finished drawing</summary>
   internal static void EndNode () {
      if (PopAttr (mChanged)) Rung++;
      (mVNode, mChanged) = mNodeStack.Pop ();
   }

   /// <summary>Used internally to reset some set of attributes to the previous values</summary>
   /// This is called after a node (and it's subtree) are drawn, so that we can reset
   /// all attributes like Color, LineType etc to their previous values.
   internal static bool PopAttr (ELuxAttr flags) {
      flags &= mChanged;
      if (flags != ELuxAttr.None) {
         if ((flags & ELuxAttr.Color) != 0) mColor = mColors.Pop ();
         if ((flags & ELuxAttr.LineType) != 0) mLineType = mLineTypes.Pop ();
         if ((flags & ELuxAttr.LineWidth) != 0) mLineWidth = mLineWidths.Pop ();
         if ((flags & ELuxAttr.LTScale) != 0) mLTScale = mLTScales.Pop ();
         if ((flags & ELuxAttr.PointSize) != 0) mPointSize = mPointSizes.Pop ();
         if ((flags & ELuxAttr.TypeFace) != 0) mTypeface = mTypefaces.Pop ();
         if ((flags & ELuxAttr.Xfm) != 0) mIDXfm = mIDXfms.Pop ();
         if ((flags & ELuxAttr.ZLevel) != 0) mZLevel = mZLevels.Pop ();
         mChanged &= ~flags;
         return true;
      }
      return false;
   }

   // Implementation -----------------------------------------------------------
   static bool Get (ELuxAttr flags, ELuxAttr bit) => (flags & bit) != 0;
   static bool Set (ELuxAttr attr) {
      if ((mChanged & attr) != 0) return false;
      mChanged |= attr; return true;
   }
   static ELuxAttr mChanged;

   /// <summary>This is called at the start of every frame to reset to known</summary>
   static void StartFrame (Vec2S viewport) {
      mcFillPaths = 0;
      mViewport = viewport;
      VPScale = new Vec2F (2.0 / viewport.X, 2.0 / viewport.Y);
      mColors.Clear (); mColor = Color4.White;
      mLineWidths.Clear (); mLineWidth = 2;     // Multiplied by DPIScale before it is used
      mPointSizes.Clear (); mPointSize = 4;     // Multiplied by DPIScale before it is used
      mLineTypes.Clear (); mLineType = ELineType.Continuous;
      mLTScales.Clear (); mLTScale = 30f;
      mTypefaces.Clear (); mTypeface = null;
      mIDXfms.Clear (); mIDXfm = 0;
      mZLevels.Clear (); mZLevel = 0;
      mChanged = ELuxAttr.None;
      Rung++;
   }

   // Nested types -------------------------------------------------------------
   /// <summary>Stats provides information on number of draw calls, verts drawn, pgm-changes made etc</summary>
   public class Stats {
      /// <summary>The current frame number</summary>
      public int NFrame => mcFrames;
      /// <summary>How many times is a program change happening, per frame</summary>
      public int PgmChanges => GLState.mPgmChanges;
      /// <summary>How many times is a new VAO bound, per frame</summary>
      public int VAOChanges => GLState.mVAOChanges;
      /// <summary>How many times are we applying new uniforms per frame</summary>
      public int ApplyUniforms => Shader.mApplyUniforms;
      /// <summary>How many draw calls per frame</summary>
      public int DrawCalls => RBatch.mDrawCalls;
      /// <summary>Number of vertices drawn</summary>
      public int VertsDrawn => RBatch.mVertsDrawn;
   }
   static readonly Stats sStats = new ();
}
#endregion
