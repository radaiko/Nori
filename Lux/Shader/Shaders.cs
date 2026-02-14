// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ Shaders.cs
// ║║║║╬║╔╣║ Concrete Shader classes, using bind groups for WGSL-aligned uniform buffers
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class Bezier2DShader -----------------------------------------------------------------------
/// <summary>A specialization of Seg2DShader, used to draw curved segs (using beziers)</summary>
[Singleton]
partial class Bezier2DShader () : Seg2DShader (ShaderImp.Bezier2D);
#endregion

#region class BlackLineShader ----------------------------------------------------------------------
/// <summary>Variant of StencilLineShader that draws solid black lines in 3D (anti-aliased)</summary>
[Singleton]
partial class BlackLineShader () : StencilLineShader (ShaderImp.BlackLine);
#endregion

#region class DashLine2DShader ---------------------------------------------------------------------
/// <summary>Shader used to draw lines with a dash pattern (dashed / dotted / centerline etc)</summary>
[Singleton]
partial class DashLine2DShader : Shader<Vec2F, DashLine2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public DashLine2DShader () : base (ShaderImp.DashLine2D) { }

   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      float fLType = ((int)a.LineType + 0.5f) / 10.0f;
      DashLine2DUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         LineWidth = a.LineWidth * Lux.DPIScale,
         LTScale = a.LTScale * Lux.DPIScale,
         DrawColor = (Vec4F)a.Color,
         LineType = fLType,
         _Pad0 = 0, _Pad1 = 0, _Pad2 = 0
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (DashLine2DUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm.CompareTo (b.IDXfm); if (n != 0) return n;
      n = a.LineType.CompareTo (b.LineType); if (n != 0) return n;
      n = (int)(a.Color.Value - b.Color.Value); if (n != 0) return n;
      n = a.LTScale.CompareTo (b.LTScale); if (n != 0) return n;
      return a.LineWidth.CompareTo (b.LineWidth);
   }

   protected override void SetConstantsImp ()
      => mVPScale = Lux.VPScale;

   protected override Settings SnapUniformsImp ()
      => new (Lux.IDXfm, Lux.LineWidth, Lux.LineType, Lux.LTScale, Lux.Color);

   // Nested types -------------------------------------------------------------
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, float LineWidth, ELineType LineType, float LTScale, Color4 Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class FacetShader --------------------------------------------------------------------------
/// <summary>Base class for various types of 3D shader (Flat / Gourad / Phong)</summary>
abstract class FacetShader : Shader<Mesh3.Node, FacetShader.Settings> {
   // Constructors -------------------------------------------------------------
   protected FacetShader (ShaderImp imp) : base (imp) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      FacetUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         NormalXfm = Lux.Scene!.Xfms[a.IDXfm].NormalXfm,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (FacetUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () { }
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color);

   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, Color4 Color);
}
#endregion

#region class FlatFacetShader ----------------------------------------------------------------------
/// <summary>3D shader using flat shading (no interpolation)</summary>
[Singleton]
partial class FlatFacetShader () : FacetShader (ShaderImp.FlatFacet);
#endregion

#region class GlassShader --------------------------------------------------------------------------
/// <summary>3D shader that simulates translucency using stippling</summary>
[Singleton]
partial class GlassShader () : FacetShader (ShaderImp.Glass);
#endregion

#region class GlassLineShader ----------------------------------------------------------------------
/// <summary>Variant of StencilLineShader that draws stippled lines (50% transparency)</summary>
[Singleton]
partial class GlassLineShader () : StencilLineShader (ShaderImp.GlassLine);
#endregion

#region class GouradShader -------------------------------------------------------------------------
/// <summary>3D shader using the Gourad shader model (color interpolation)</summary>
[Singleton]
partial class GouradShader () : FacetShader (ShaderImp.Gourad);
#endregion

#region class Line2DShader -------------------------------------------------------------------------
/// <summary>A specialization of Seg2DShader, used to draw linear segs</summary>
[Singleton]
partial class Line2DShader () : Seg2DShader (ShaderImp.Line2D);
#endregion

#region class Line3DShader -------------------------------------------------------------------------
/// <summary>Draw lines in 3D space</summary>
[Singleton]
partial class Line3DShader : Shader<Vec3F, Seg2DShader.Settings> {
   Line3DShader () : base (ShaderImp.Line3D) { }

   protected override unsafe void ApplyUniformsImp (ref readonly Seg2DShader.Settings a) {
      IGPU gpu = RenderState.It.GPU;
      LineUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         LineWidth = a.LineWidth * Lux.DPIScale,
         _Pad0 = 0,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (LineUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Seg2DShader.Settings a, ref readonly Seg2DShader.Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => mVPScale = Lux.VPScale;
   protected override Seg2DShader.Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Lux.Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class PhongShader --------------------------------------------------------------------------
/// <summary>3D shader using the Phong shading model (normal vector interpolation)</summary>
[Singleton]
partial class PhongShader () : FacetShader (ShaderImp.Phong);
#endregion

#region class PhongPinkShader ----------------------------------------------------------------------
/// <summary>Phong shader that colors back-faces in Pink (useful for debugging)</summary>
[Singleton]
partial class PhongPinkShader () : FacetShader (ShaderImp.PhongPink);
#endregion

#region class PickShader ---------------------------------------------------------------------------
/// <summary>3D shader used during picking - replaces actual colors with VNode Ids</summary>
[Singleton]
partial class PickShader () : FacetShader (ShaderImp.Pick) {
   /// <summary>Direct uniform application for pick mode (bypasses SnapUniforms)</summary>
   public unsafe void ApplyUniforms (int idXfm, Color4 color) {
      IGPU gpu = RenderState.It.GPU;
      FacetUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[idXfm].Xfm,
         NormalXfm = Lux.Scene.Xfms[idXfm].NormalXfm,
         DrawColor = (Vec4F)color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (FacetUniforms));
   }
}
#endregion

#region class Point2DShader ------------------------------------------------------------------------
/// <summary>Shader used to draw points</summary>
[Singleton]
partial class Point2DShader : Shader<Vec2F, Point2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Point2DShader () : base (ShaderImp.Point2D) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      PointUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         PointSize = a.PointSize * Lux.DPIScale,
         _Pad0 = 0,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (PointUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.PointSize.CompareTo (b.PointSize); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => mVPScale = Lux.VPScale;
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.PointSize, Lux.Color);

   // Nested types -------------------------------------------------------------
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, float PointSize, Color4 Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class Point3DShader ------------------------------------------------------------------------
/// <summary>Shader used to draw points in 3D</summary>
[Singleton]
partial class Point3DShader : Shader<Vec3F, Point3DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Point3DShader () : base (ShaderImp.Point3D) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      PointUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         PointSize = a.PointSize * Lux.DPIScale,
         _Pad0 = 0,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (PointUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.PointSize.CompareTo (b.PointSize); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => mVPScale = Lux.VPScale;
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.PointSize, Lux.Color);

   // Nested types -------------------------------------------------------------
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, float PointSize, Color4 Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class Quad2DShader -------------------------------------------------------------------------
/// <summary>Shader to draw simple quads in 2D (specified in world space, no anti-aliasing)</summary>
[Singleton]
partial class Quad2DShader () : TriQuad2DShader (ShaderImp.Quad2D);
#endregion

#region class Seg2DShader --------------------------------------------------------------------------
/// <summary>Base class for the Line2DShader and Bezier2DShader</summary>
class Seg2DShader : Shader<Vec2F, Seg2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Seg2DShader (ShaderImp shader) : base (shader) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      LineUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         LineWidth = a.LineWidth * Lux.DPIScale,
         _Pad0 = 0,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (LineUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => mVPScale = Lux.VPScale;
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Lux.Color);

   // Nested types -------------------------------------------------------------
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, float LineWidth, Color4 Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class StencilLineShader --------------------------------------------------------------------
/// <summary>Shader used to draw the black stencil lines for a mesh</summary>
abstract class StencilLineShader : Shader<Mesh3.Node, StencilLineShader.Settings> {
   // Constructor --------------------------------------------------------------
   protected StencilLineShader (ShaderImp imp) : base (imp) { }

   // Methods ------------------------------------------------------------------
   /// <summary>Expands wire index pairs into consecutive node pairs for instanced line rendering</summary>
   /// The instanced line pipeline expects consecutive pairs of nodes (p0, p1) in the vertex
   /// buffer, where each pair represents one line segment instance. Wire indices are pairs of
   /// indices into the node array. We dereference these to produce the flat node pairs needed.
   public void DrawWires (Mesh3.Node[] nodes, int[] wires) {
      int nEdges = wires.Length / 2;
      if (nEdges == 0) return;
      if (mExpanded.Length < nEdges * 2)
         mExpanded = new Mesh3.Node[nEdges * 2];
      for (int i = 0; i < nEdges; i++) {
         mExpanded[i * 2] = nodes[wires[i * 2]];
         mExpanded[i * 2 + 1] = nodes[wires[i * 2 + 1]];
      }
      Draw (mExpanded.AsSpan (0, nEdges * 2));
   }
   static Mesh3.Node[] mExpanded = [];

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      LineUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         LineWidth = a.LineWidth * Lux.DPIScale,
         _Pad0 = 0,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (LineUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      n = a.LineWidth.CompareTo (b.LineWidth); if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () => mVPScale = Lux.VPScale;
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.LineWidth, Lux.StencilColor);

   // Nested types -------------------------------------------------------------
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, float LineWidth, Color4 Color);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class TextPxShader -------------------------------------------------------------------------
/// <summary>Draws text defined in pixel coordinates</summary>
[Singleton]
partial class TextPxShader : Shader<TextPxShader.Args, TextPxShader.Settings> {
   // Constructor --------------------------------------------------------------
   public TextPxShader () : base (ShaderImp.TextPx) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      GLState.TypeFace = a.Face;
      IGPU gpu = RenderState.It.GPU;
      TextPxUniforms ub = new () {
         VPScale = mVPScale,
         _Pad = default,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (TextPxUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp ()
      => mVPScale = Lux.VPScale;

   protected override Settings SnapUniformsImp () => new (Lux.Color, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec4S Cell, int TexOffset);
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (Color4 Color, TypeFace Face);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class Text2DShader -------------------------------------------------------------------------
/// <summary>Draws the text defined in world coordinates</summary>
[Singleton]
partial class Text2DShader : Shader<Text2DShader.Args, Text2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Text2DShader () : base (ShaderImp.Text2D) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      GLState.TypeFace = a.Face;
      IGPU gpu = RenderState.It.GPU;
      TextXfmUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         _Pad = default,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (TextXfmUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp ()
      => mVPScale = Lux.VPScale;

   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec2F Pos, Vec4S Cell, int TexOffset);
   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, Color4 Color, TypeFace Face);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class Text3DShader -------------------------------------------------------------------------
/// <summary>Draws text defined in 3D world coordinates</summary>
[Singleton]
partial class Text3DShader : Shader<Text3DShader.Args, Text2DShader.Settings> {
   // Constructor --------------------------------------------------------------
   public Text3DShader () : base (ShaderImp.Text3D) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Text2DShader.Settings a) {
      GLState.TypeFace = a.Face;
      IGPU gpu = RenderState.It.GPU;
      TextXfmUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         VPScale = mVPScale,
         _Pad = default,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (TextXfmUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Text2DShader.Settings a, ref readonly Text2DShader.Settings b) {
      int n = a.Face.UID - b.Face.UID; if (n != 0) return n;
      n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp ()
      => mVPScale = Lux.VPScale;

   protected override Text2DShader.Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color, Lux.TypeFace ?? TypeFace.Default);

   // Nested types -------------------------------------------------------------
   [StructLayout (LayoutKind.Sequential)]
   public readonly record struct Args (Vec3F Pos, Vec4S Cell, int TexOffset);

   // Private data -------------------------------------------------------------
   Vec2F mVPScale;
}
#endregion

#region class Triangle2DShader ---------------------------------------------------------------------
/// <summary>Shader to draw simple triangles in 2D (specified in world space, no anti-aliasing)</summary>
[Singleton]
partial class Triangle2DShader () : TriQuad2DShader (ShaderImp.Triangle2D);
#endregion

#region class TriFanStencilShader ------------------------------------------------------------------
/// <summary>Shader used to implement stage 1 of the stencil-then-cover algorithm</summary>
/// This is a simple algorithm that uses the stencil buffer to fill the interior of a set of
/// closed paths. The paths are defined as contours, each consisting of a number of line segments.
/// The orientation (winding) of these contours is not important, nor are there any constraints on
/// whether they can self-intersect or cross each other.
///
/// This animation https://www.ekioh.com/devblog/gpu-filling-vector-paths/ shows very clearly
/// how this algorithm works. Basically we pick an arbitrary point on the scene and draw a triangle
/// from that point using each 'segment' on the path as a base. For all the pixels lying within
/// that triangle, we invert bit 0 of the stencil buffer. When we are finished, all the points lying
/// within a contour (that is, an odd number of 'crossings' from the arbitrary point) will have
/// their stencil bit 0 set, and the rest will not.
///
/// This is done by the TriFanStencilShader. It requires as input a triangle list (converted from
/// fan format by the CPU). The TriFanCoverShader then uses this stencil to fill in the pixels
/// where stencil bit 0 is set. In WebGPU, the stencil behavior is baked into the pipeline.
[Singleton]
partial class TriFanStencilShader () : TriQuad2DShader (ShaderImp.TriFanStencil);
#endregion

#region class TriFanCoverShader --------------------------------------------------------------------
/// <summary>Shader used to implement stage 2 of the stencil-then-cover algorithm</summary>
/// This works after the TriFanStencilShader has updated the stencil buffer. This shader uses
/// bit 0 of the stencil buffer, and wherever that is set, it simply applies the Lux.Color to that
/// pixel. For this to work, the shader needs as input a triangle list that fully covers the
/// paths in question. The implementation in Lux.FillPoly uses the bounding box of the set of
/// paths and creates triangles that apply paint into this bounding box.
[Singleton]
partial class TriFanCoverShader () : TriQuad2DShader (ShaderImp.TriFanCover);
#endregion

#region class TriQuad2DShader ----------------------------------------------------------------------
/// <summary>TriQuad2DShader is the base class for Triangle2DShader and Quad2DShader</summary>
abstract class TriQuad2DShader : Shader<Vec2F, TriQuad2DShader.Settings> {
   // Constructors -------------------------------------------------------------
   protected TriQuad2DShader (ShaderImp imp) : base (imp) { }

   // Overrides ----------------------------------------------------------------
   protected override unsafe void ApplyUniformsImp (ref readonly Settings a) {
      IGPU gpu = RenderState.It.GPU;
      Flat2DUniforms ub = new () {
         Xfm = Lux.Scene!.Xfms[a.IDXfm].Xfm,
         DrawColor = (Vec4F)a.Color
      };
      gpu.SetBindGroup (0, (nint)(&ub), sizeof (Flat2DUniforms));
   }

   protected override int OrderUniformsImp (ref readonly Settings a, ref readonly Settings b) {
      int n = a.IDXfm - b.IDXfm; if (n != 0) return n;
      return (int)(a.Color.Value - b.Color.Value);
   }

   protected override void SetConstantsImp () { }
   protected override Settings SnapUniformsImp () => new (Lux.IDXfm, Lux.Color);

   /// <summary>Batch settings captured at draw time</summary>
   public readonly record struct Settings (int IDXfm, Color4 Color);
}
#endregion

#region WGSL-aligned uniform buffer structs --------------------------------------------------------
/// <summary>Uniform buffer for Flat2D pipelines (Triangle2D, Quad2D, TriFanStencil, TriFanCover)</summary>
/// WGSL layout: xfm(64) + draw_color(16) = 80 bytes
[StructLayout (LayoutKind.Explicit, Size = 80)]
struct Flat2DUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Vec4F DrawColor;
}

/// <summary>Uniform buffer for line pipelines (Line2D, Bezier2D, Line3D, BlackLine, GlassLine)</summary>
/// WGSL layout: xfm(64) + vp_scale(8) + line_width(4) + _pad0(4) + draw_color(16) = 96 bytes
[StructLayout (LayoutKind.Explicit, Size = 96)]
struct LineUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Vec2F VPScale;
   [FieldOffset (72)] public float LineWidth;
   [FieldOffset (76)] public float _Pad0;
   [FieldOffset (80)] public Vec4F DrawColor;
}

/// <summary>Uniform buffer for DashLine2D pipeline</summary>
/// WGSL layout: xfm(64) + vp_scale(8) + line_width(4) + lt_scale(4) + draw_color(16) +
///              line_type(4) + _pad0..2(12) = 112 bytes
[StructLayout (LayoutKind.Explicit, Size = 112)]
struct DashLine2DUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Vec2F VPScale;
   [FieldOffset (72)] public float LineWidth;
   [FieldOffset (76)] public float LTScale;
   [FieldOffset (80)] public Vec4F DrawColor;
   [FieldOffset (96)] public float LineType;
   [FieldOffset (100)] public float _Pad0;
   [FieldOffset (104)] public float _Pad1;
   [FieldOffset (108)] public float _Pad2;
}

/// <summary>Uniform buffer for point pipelines (Point2D, Point3D)</summary>
/// WGSL layout: xfm(64) + vp_scale(8) + point_size(4) + _pad0(4) + draw_color(16) = 96 bytes
[StructLayout (LayoutKind.Explicit, Size = 96)]
struct PointUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Vec2F VPScale;
   [FieldOffset (72)] public float PointSize;
   [FieldOffset (76)] public float _Pad0;
   [FieldOffset (80)] public Vec4F DrawColor;
}

/// <summary>Uniform buffer for 3D facet pipelines (Gourad, Phong, PhongPink, Pick, Glass, FlatFacet)</summary>
/// WGSL layout: xfm(64) + normal_xfm(64) + draw_color(16) = 144 bytes
[StructLayout (LayoutKind.Explicit, Size = 144)]
struct FacetUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Mat4F NormalXfm;
   [FieldOffset (128)] public Vec4F DrawColor;
}

/// <summary>Uniform buffer for TextPx pipeline</summary>
/// WGSL layout: vp_scale(8) + _pad(8) + draw_color(16) = 32 bytes
[StructLayout (LayoutKind.Explicit, Size = 32)]
struct TextPxUniforms {
   [FieldOffset (0)] public Vec2F VPScale;
   [FieldOffset (8)] public Vec2F _Pad;
   [FieldOffset (16)] public Vec4F DrawColor;
}

/// <summary>Uniform buffer for Text2D and Text3D pipelines</summary>
/// WGSL layout: xfm(64) + vp_scale(8) + _pad(8) + draw_color(16) = 96 bytes
[StructLayout (LayoutKind.Explicit, Size = 96)]
struct TextXfmUniforms {
   [FieldOffset (0)] public Mat4F Xfm;
   [FieldOffset (64)] public Vec2F VPScale;
   [FieldOffset (72)] public Vec2F _Pad;
   [FieldOffset (80)] public Vec4F DrawColor;
}
#endregion
