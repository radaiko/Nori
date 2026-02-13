// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ ShaderImp.cs
// ║║║║╬║╔╣║ ShaderImp is the low level metadata holder for a pre-compiled WebGPU pipeline
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Runtime.CompilerServices;
namespace Nori;

#region class ShaderImp ----------------------------------------------------------------------------
/// <summary>Metadata holder referencing a pre-compiled WebGPU render pipeline</summary>
/// In the WebGPU model, all render state (blend, depth, stencil, polygon offset) is
/// baked into an immutable pipeline object at creation time. ShaderImp stores the
/// pipeline identifier and associated metadata (sort code, vertex spec, etc.) but
/// does not compile or link any shaders itself — that is done by PipelineFactory.
class ShaderImp {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a ShaderImp with the given metadata</summary>
   ShaderImp (string name, int sort, EPipeline pipeline, EVertexSpec vspec, bool blend, bool depthTest, bool polyOffset, EStencilBehavior stencil)
      => (Name, SortCode, Pipeline, VSpec, Blending, DepthTest, PolygonOffset, StencilBehavior)
         = (name, sort, pipeline, vspec, blend, depthTest, polyOffset, stencil);

   // Properties ---------------------------------------------------------------
   /// <summary>Enable blending when this pipeline is used</summary>
   public readonly bool Blending;
   /// <summary>Enable depth-testing when this pipeline is used</summary>
   public readonly bool DepthTest;
   /// <summary>The name of this shader pipeline</summary>
   public readonly string Name;
   /// <summary>The pre-compiled WebGPU pipeline this shader references</summary>
   public readonly EPipeline Pipeline;
   /// <summary>Enable polygon-offset-fill when this pipeline is used</summary>
   public readonly bool PolygonOffset;
   /// <summary>The sorting code for this (determines order in which batches are dispatched)</summary>
   public readonly int SortCode;
   /// <summary>What is the special 'stencil-buffer' behavior of this pipeline</summary>
   public readonly EStencilBehavior StencilBehavior;
   /// <summary>The vertex-specification for this shader</summary>
   public readonly EVertexSpec VSpec;

   // Standard shaders ---------------------------------------------------------
   public static ShaderImp Bezier2D => mBezier2D ??= Load ();
   public static ShaderImp Line2D => mLine2D ??= Load ();
   public static ShaderImp Line3D => mLine3D ??= Load ();
   public static ShaderImp DashLine2D => mDashLine2D ??= Load ();
   public static ShaderImp Point2D => mPoint2D ??= Load ();
   public static ShaderImp Point3D => mPoint3D ??= Load ();
   public static ShaderImp Triangle2D => mTriangle2D ??= Load ();
   public static ShaderImp Quad2D => mQuad2D ??= Load ();
   static ShaderImp? mLine2D, mLine3D, mBezier2D, mPoint2D, mPoint3D, mTriangle2D, mQuad2D, mDashLine2D;

   public static ShaderImp BlackLine => mBlackLine ??= Load ();
   public static ShaderImp GlassLine => mGlassLine ??= Load ();
   public static ShaderImp Gourad => mGourad ??= Load ();
   public static ShaderImp Phong => mPhong ??= Load ();
   public static ShaderImp PhongPink => mPhongPink ??= Load ();
   public static ShaderImp Pick => mPick ??= Load ();
   public static ShaderImp Glass => mGlass ??= Load ();
   public static ShaderImp FlatFacet => mFlatFacet ??= Load ();
   static ShaderImp? mBlackLine, mGlassLine, mGourad, mPhong, mPhongPink, mPick, mFlatFacet, mGlass;

   public static ShaderImp TriFanStencil => mTriFanStencil ??= Load ();
   public static ShaderImp TriFanCover => mTriFanCover ??= Load ();
   static ShaderImp? mTriFanStencil, mTriFanCover;

   public static ShaderImp TextPx => mTextPx ??= Load ();
   public static ShaderImp Text2D => mText2D ??= Load ();
   public static ShaderImp Text3D => mText3D ??= Load ();
   static ShaderImp? mTextPx, mText2D, mText3D;

   // Implementation -----------------------------------------------------------
   // Maps a shader name to the corresponding EPipeline enum value
   static EPipeline ResolvePipeline (string name)
      => name switch {
         "Line2D" => EPipeline.Line2D,
         "Line3D" => EPipeline.Line3D,
         "Bezier2D" => EPipeline.Bezier2D,
         "DashLine2D" => EPipeline.DashLine2D,
         "Point2D" => EPipeline.Point2D,
         "Point3D" => EPipeline.Point3D,
         "Triangle2D" => EPipeline.Triangle2D,
         "Quad2D" => EPipeline.Quad2D,
         "BlackLine" => EPipeline.BlackLine,
         "GlassLine" => EPipeline.GlassLine,
         "Gourad" => EPipeline.Gourad,
         "Phong" => EPipeline.Phong,
         "PhongPink" => EPipeline.PhongPink,
         "Pick" => EPipeline.Pick,
         "Glass" => EPipeline.Glass,
         "FlatFacet" => EPipeline.FlatFacet,
         "TextPx" => EPipeline.TextPx,
         "Text2D" => EPipeline.Text2D,
         "Text3D" => EPipeline.Text3D,
         "TriFanStencil" => EPipeline.TriFanStencil,
         "TriFanCover" => EPipeline.TriFanCover,
         _ => throw new BadCaseException (name)
      };

   // Loads metadata for a particular shader from Shader/Index.txt.
   // Unlike the OpenGL version, this does NOT compile any shaders — it only
   // reads metadata and resolves the EPipeline. Actual pipeline compilation
   // is handled by PipelineFactory at startup.
   static ShaderImp Load ([CallerMemberName] string name = "") {
      sIndex ??= Lib.ReadLines ("nori:GL/Shader/Index.txt");
      // Each line in the index.txt contains these:
      // 0:Name  1:SortCode  2:Mode  3:VSpec  4:Blending  5:DepthTest  6:PolygonOffset  7:StencilBehavior  8:Programs
      foreach (string line in sIndex) {
         string[] w = line.Split (' ', StringSplitOptions.RemoveEmptyEntries);
         if (w.Length >= 9 && w[0] == name) {
            int sort = int.Parse (w[1]);
            // w[2] is Mode (OpenGL primitive topology) — ignored, WebGPU uses TriangleList for everything
            EVertexSpec vspec = Enum.Parse<EVertexSpec> (w[3], true);
            bool blending = w[4] == "1", depthtest = w[5] == "1", offset = w[6] == "1";
            EStencilBehavior stencil = Enum.Parse<EStencilBehavior> (w[7], true);
            EPipeline pipeline = ResolvePipeline (name);
            return new (name, sort, pipeline, vspec, blending, depthtest, offset, stencil);
         }
      }
      throw new NotImplementedException ($"Shader {name} not found in Shader/Index.txt");
   }
   static string[]? sIndex;

   public override string ToString ()
      => $"Shader {Name} (Pipeline: {Pipeline})";
}
#endregion

#region struct Attrib ------------------------------------------------------------------------------
/// <summary>Attrib represents one attribute in a vertex buffer</summary>
/// Attrib is still used for metadata purposes — knowing vertex stride, component layout,
/// and sizes — even though GL-specific attribute setup is no longer done here.
readonly record struct Attrib (int Dims, int Size, bool Integral) {
   public static Attrib AVec2f = new (2, 8, false);
   public static Attrib AInt = new (1, 4, true);
   public static Attrib AShort = new (1, 2, true);
   public static Attrib AFloat = new (1, 4, false);
   public static Attrib AVec3f = new (3, 12, false);
   public static Attrib AVec4f = new (4, 16, false);
   public static Attrib AVec3h = new (3, 6, false);
   public static Attrib AVec4s = new (4, 8, true);

   public static Attrib[] GetFor (EVertexSpec spec) =>
      spec switch {
         EVertexSpec.Vec2F => [AVec2f],
         EVertexSpec.Vec3F => [AVec3f],
         EVertexSpec.Vec3F_Vec3H => [AVec3f, AVec3h],
         EVertexSpec.Vec4S_Int => [AVec4s, AInt],
         EVertexSpec.Vec2F_Vec4S_Int => [AVec2f, AVec4s, AInt],
         EVertexSpec.Vec3F_Vec4S_Int => [AVec3f, AVec4s, AInt],
         _ => throw new BadCaseException (spec)
      };

   public static int GetSize (EVertexSpec spec) =>
      spec switch {
         EVertexSpec.Vec2F => 8,
         EVertexSpec.Vec3F => 12,
         EVertexSpec.Vec3F_Vec3H => 20,
         EVertexSpec.Vec4S_Int => 12,
         EVertexSpec.Vec2F_Vec4S_Int => 20,
         EVertexSpec.Vec3F_Vec4S_Int => 24,
         _ => throw new BadCaseException (spec)
      };
}
#endregion

#region enum EVertexSpec ---------------------------------------------------------------------------
/// <summary>The various vertex specifications used by shader pipelines</summary>
enum EVertexSpec { Vec2F, Vec3F, Vec3F_Vec3H, Vec4S_Int, Vec2F_Vec4S_Int, Vec3F_Vec4S_Int, _Last }
#endregion

#region enum EStencilBehavior ----------------------------------------------------------------------
/// <summary>Does this shader have any special behavior related to the stencil-buffer?</summary>
/// See the TriFanStencil and TriFanCover shaders for more details on this
enum EStencilBehavior { None, Stencil, Cover }
#endregion
