// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ PipelineFactory.cs
// ║║║║╬║╔╣║ Pre-compiles all WebGPU render pipelines at initialization time
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

#region enum EPipeline ----------------------------------------------------------------------------------
/// <summary>Identifies a pre-compiled WebGPU render pipeline</summary>
public enum EPipeline {
   /// <summary>2D anti-aliased line via instanced quads</summary>
   Line2D,
   /// <summary>3D anti-aliased line via instanced quads</summary>
   Line3D,
   /// <summary>2D Bezier curve (CPU pre-tessellated) via instanced quads</summary>
   Bezier2D,
   /// <summary>2D dashed line with line-type texture</summary>
   DashLine2D,
   /// <summary>2D point via instanced quads</summary>
   Point2D,
   /// <summary>3D point via instanced quads</summary>
   Point3D,
   /// <summary>2D filled triangles (flat color)</summary>
   Triangle2D,
   /// <summary>2D filled quads (CPU-expanded to triangles, flat color)</summary>
   Quad2D,
   /// <summary>3D wireframe (same shader as Line3D)</summary>
   BlackLine,
   /// <summary>3D stippled line</summary>
   GlassLine,
   /// <summary>3D Gouraud shading (per-vertex lighting)</summary>
   Gourad,
   /// <summary>3D Phong shading (per-fragment lighting)</summary>
   Phong,
   /// <summary>3D Phong with pink back-face highlighting</summary>
   PhongPink,
   /// <summary>3D pick buffer (entity ID as color)</summary>
   Pick,
   /// <summary>3D stippled surface (glass effect)</summary>
   Glass,
   /// <summary>3D flat-facet shading (provoking vertex normal)</summary>
   FlatFacet,
   /// <summary>Pixel-space text rendering</summary>
   TextPx,
   /// <summary>2D world-space text rendering</summary>
   Text2D,
   /// <summary>3D world-space text rendering</summary>
   Text3D,
   /// <summary>Stencil write pass for triangle-fan fill (invert stencil bit)</summary>
   TriFanStencil,
   /// <summary>Stencil test pass for triangle-fan fill (draw where stencil set)</summary>
   TriFanCover,
}
#endregion

#region class PipelineFactory ----------------------------------------------------------------------------
/// <summary>Pre-compiles all WebGPU render pipelines at initialization time</summary>
/// In WebGPU, pipelines are immutable and pre-compiled (unlike OpenGL where state
/// is set dynamically). This factory loads all WGSL shaders from embedded resources,
/// creates bind group layouts, and builds every pipeline the renderer needs.
public unsafe class PipelineFactory : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The uniform-only bind group layout (Group 0: uniform buffer at binding 0)</summary>
   public BindGroupLayout* UniformLayout => mUniformLayout;

   /// <summary>The textured bind group layout (Group 0: uniform + texture + sampler)</summary>
   public BindGroupLayout* TexturedLayout => mTexturedLayout;

   // Methods ------------------------------------------------------------------
   /// <summary>Create and compile all render pipelines for the given device</summary>
   public static PipelineFactory Create (GPUDevice gpu) {
      PipelineFactory factory = new () { mGPU = gpu };
      factory.Init ();
      return factory;
   }

   /// <summary>Retrieve a pre-compiled render pipeline by type</summary>
   public GPUPipeline Get (EPipeline pipeline) => mPipelines[pipeline];

   /// <summary>Release all pipelines, shader modules, and bind group layouts</summary>
   public void Dispose () {
      foreach (GPUPipeline pipe in mPipelines.Values) pipe.Dispose ();
      mPipelines.Clear ();
      foreach (GPUShaderModule sm in mShaders.Values) sm.Dispose ();
      mShaders.Clear ();
      if (mUniformLayout != null) { mGPU.Api.BindGroupLayoutRelease (mUniformLayout); mUniformLayout = null; }
      if (mTexturedLayout != null) { mGPU.Api.BindGroupLayoutRelease (mTexturedLayout); mTexturedLayout = null; }
      GC.SuppressFinalize (this);
   }

   // Implementation -----------------------------------------------------------
   void Init () {
      LoadShaders ();
      CreateBindGroupLayouts ();
      CreateAllPipelines ();
   }

   // Loads all WGSL shader sources from embedded resources and compiles them
   // into GPUShaderModule objects. Each .wgsl file becomes a module keyed by
   // its filename without extension (e.g., "Line2D", "Flat2D").
   void LoadShaders () {
      Assembly asm = typeof (PipelineFactory).Assembly;
      string[] names = asm.GetManifestResourceNames ();
      foreach (string name in names) {
         if (!name.EndsWith (".wgsl")) continue;
         // Resource name format: Nori.GPU.Shaders.Line2D.wgsl (or similar)
         string key = Path.GetFileNameWithoutExtension (name);
         using Stream stream = asm.GetManifestResourceStream (name)!;
         using StreamReader reader = new (stream);
         string source = reader.ReadToEnd ();
         mShaders[key] = GPUShaderModule.Create (mGPU, source, label: key);
      }
   }

   // Creates the two bind group layouts used across all pipelines:
   //   - Uniform layout: binding(0) = uniform buffer
   //   - Textured layout: binding(0) = uniform buffer, binding(1) = texture, binding(2) = sampler
   void CreateBindGroupLayouts () {
      // Uniform-only layout (used by most pipelines)
      BindGroupLayoutEntry uniformEntry = new () {
         Binding = 0,
         Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
         Buffer = new BufferBindingLayout {
            Type = BufferBindingType.Uniform,
            HasDynamicOffset = false,
            MinBindingSize = 0
         }
      };
      BindGroupLayoutDescriptor uniformDesc = new () {
         EntryCount = 1,
         Entries = &uniformEntry
      };
      mUniformLayout = mGPU.Api.DeviceCreateBindGroupLayout (mGPU.Device, &uniformDesc);

      // Textured layout (used by DashLine2D, TextPx, Text2D, Text3D)
      BindGroupLayoutEntry* texEntries = stackalloc BindGroupLayoutEntry[3];
      texEntries[0] = new BindGroupLayoutEntry {
         Binding = 0,
         Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
         Buffer = new BufferBindingLayout {
            Type = BufferBindingType.Uniform,
            HasDynamicOffset = false,
            MinBindingSize = 0
         }
      };
      texEntries[1] = new BindGroupLayoutEntry {
         Binding = 1,
         Visibility = ShaderStage.Fragment,
         Texture = new TextureBindingLayout {
            SampleType = TextureSampleType.Float,
            ViewDimension = TextureViewDimension.Dimension2D,
            Multisampled = false
         }
      };
      texEntries[2] = new BindGroupLayoutEntry {
         Binding = 2,
         Visibility = ShaderStage.Fragment,
         Sampler = new SamplerBindingLayout {
            Type = SamplerBindingType.Filtering
         }
      };
      BindGroupLayoutDescriptor texDesc = new () {
         EntryCount = 3,
         Entries = texEntries
      };
      mTexturedLayout = mGPU.Api.DeviceCreateBindGroupLayout (mGPU.Device, &texDesc);
   }

   // Creates all 21 render pipelines from Index.txt using the appropriate
   // vertex layouts, blend states, depth/stencil states, and shader modules.
   void CreateAllPipelines () {
      TextureFormat fmt = mGPU.SurfaceFormat;

      // --- 2D instanced line pipelines (Line2D, Bezier2D, DashLine2D) ---
      GPUVertexLayout[] line2DLayout = [InstanceLayout2DLine ()];
      Build (EPipeline.Line2D, "Line2D", line2DLayout, fmt, blend: true);
      Build (EPipeline.Bezier2D, "Bezier2D", line2DLayout, fmt, blend: true);
      Build (EPipeline.DashLine2D, "DashLine2D", line2DLayout, fmt, blend: true, textured: true);

      // --- 3D instanced line pipelines (Line3D, BlackLine, GlassLine) ---
      GPUVertexLayout[] line3DLayout = [InstanceLayout3DLine ()];
      Build (EPipeline.Line3D, "Line3D", line3DLayout, fmt, blend: true, depth: true);
      Build (EPipeline.BlackLine, "Line3D", line3DLayout, fmt, blend: true, depth: true);
      Build (EPipeline.GlassLine, "GlassLine", line3DLayout, fmt, blend: true, depth: true);

      // --- 2D instanced point pipeline ---
      GPUVertexLayout[] pt2DLayout = [InstanceLayout2DPoint ()];
      Build (EPipeline.Point2D, "Point2D", pt2DLayout, fmt, blend: true);

      // --- 3D instanced point pipeline ---
      GPUVertexLayout[] pt3DLayout = [InstanceLayout3DPoint ()];
      Build (EPipeline.Point3D, "Point3D", pt3DLayout, fmt, blend: true);

      // --- 2D direct triangle pipelines (Triangle2D, Quad2D) ---
      GPUVertexLayout[] flat2DLayout = [VertexLayout2D ()];
      Build (EPipeline.Triangle2D, "Flat2D", flat2DLayout, fmt);
      Build (EPipeline.Quad2D, "Flat2D", flat2DLayout, fmt);

      // --- 3D facet pipelines (Gourad, Phong, PhongPink, Pick, Glass, FlatFacet) ---
      GPUVertexLayout[] facet3DLayout = [VertexLayout3DFacet ()];
      Build (EPipeline.Gourad, "Gourad", facet3DLayout, fmt, depth: true);
      Build (EPipeline.Phong, "Phong", facet3DLayout, fmt, depth: true);
      Build (EPipeline.PhongPink, "PhongPink", facet3DLayout, fmt, depth: true);
      Build (EPipeline.Pick, "Pick", facet3DLayout, fmt, depth: true);
      Build (EPipeline.Glass, "Glass", facet3DLayout, fmt, depth: true);
      Build (EPipeline.FlatFacet, "FlatFacet", facet3DLayout, fmt, depth: true);

      // --- Instanced text pipelines ---
      GPUVertexLayout[] textPxLayout = [InstanceLayoutTextPx ()];
      Build (EPipeline.TextPx, "TextPx", textPxLayout, fmt, blend: true, textured: true);

      GPUVertexLayout[] text2DLayout = [InstanceLayoutText2D ()];
      Build (EPipeline.Text2D, "Text2D", text2DLayout, fmt, blend: true, textured: true);

      GPUVertexLayout[] text3DLayout = [InstanceLayoutText3D ()];
      Build (EPipeline.Text3D, "Text3D", text3DLayout, fmt, blend: true, depth: true, textured: true);

      // --- Stencil pipelines (TriFanStencil, TriFanCover) ---
      GPUStencilConfig stencilWrite = new () {
         FrontCompare = CompareFunction.Always,
         FrontPassOp = StencilOperation.Invert,
         FrontFailOp = StencilOperation.Keep,
         BackCompare = CompareFunction.Always,
         BackPassOp = StencilOperation.Invert,
         BackFailOp = StencilOperation.Keep,
         ReadMask = 0xFF,
         WriteMask = 0xFF
      };
      GPUStencilConfig stencilTest = new () {
         FrontCompare = CompareFunction.NotEqual,
         FrontPassOp = StencilOperation.Zero,
         FrontFailOp = StencilOperation.Keep,
         BackCompare = CompareFunction.NotEqual,
         BackPassOp = StencilOperation.Zero,
         BackFailOp = StencilOperation.Keep,
         ReadMask = 0xFF,
         WriteMask = 0xFF
      };
      BuildStencil (EPipeline.TriFanStencil, "Flat2D", flat2DLayout, fmt, stencilWrite,
         colorWrite: ColorWriteMask.None);
      BuildStencil (EPipeline.TriFanCover, "Flat2D", flat2DLayout, fmt, stencilTest,
         colorWrite: ColorWriteMask.All);
   }

   // Builds a standard pipeline with optional blend, depth, and texture bindings.
   void Build (EPipeline id, string shaderName, GPUVertexLayout[] layouts,
      TextureFormat fmt, bool blend = false, bool depth = false, bool textured = false) {

      GPUShaderModule shader = mShaders[shaderName];
      BindGroupLayout*[] bgl = textured
         ? [mTexturedLayout] : [mUniformLayout];
      mPipelines[id] = GPUPipeline.Create (
         mGPU, shader, "vs_main", shader, "fs_main",
         layouts, fmt,
         enableBlend: blend,
         enableDepth: depth,
         bindGroupLayouts: bgl,
         label: id.ToString ());
   }

   // Builds a stencil pipeline with custom stencil config and color write mask.
   void BuildStencil (EPipeline id, string shaderName, GPUVertexLayout[] layouts,
      TextureFormat fmt, GPUStencilConfig stencilCfg, ColorWriteMask colorWrite) {

      GPUShaderModule shader = mShaders[shaderName];
      BindGroupLayout*[] bgl = [mUniformLayout];
      mPipelines[id] = GPUPipeline.Create (
         mGPU, shader, "vs_main", shader, "fs_main",
         layouts, fmt,
         enableBlend: false,
         enableDepth: false,
         bindGroupLayouts: bgl,
         label: id.ToString (),
         stencil: stencilCfg,
         colorWriteMask: colorWrite);
   }

   // --- Vertex layout helpers ------------------------------------------------

   // 2D line instance: two vec2<f32> endpoints (p0, p1), step per instance
   static GPUVertexLayout InstanceLayout2DLine ()
      => new () {
         Stride = 16, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x2, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Float32x2, Offset = 8 }
         ]
      };

   // 3D line instance: two vec3<f32> endpoints (p0, p1), step per instance
   static GPUVertexLayout InstanceLayout3DLine ()
      => new () {
         Stride = 24, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x3, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Float32x3, Offset = 12 }
         ]
      };

   // 2D point instance: one vec2<f32> position, step per instance
   static GPUVertexLayout InstanceLayout2DPoint ()
      => new () {
         Stride = 8, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x2, Offset = 0 }
         ]
      };

   // 3D point instance: one vec3<f32> position, step per instance
   static GPUVertexLayout InstanceLayout3DPoint ()
      => new () {
         Stride = 12, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x3, Offset = 0 }
         ]
      };

   // 2D direct vertex: one vec2<f32> position, step per vertex
   static GPUVertexLayout VertexLayout2D ()
      => new () {
         Stride = 8, StepMode = VertexStepMode.Vertex,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x2, Offset = 0 }
         ]
      };

   // 3D facet vertex: vec3<f32> position + vec3<f32> normal, step per vertex
   static GPUVertexLayout VertexLayout3DFacet ()
      => new () {
         Stride = 24, StepMode = VertexStepMode.Vertex,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x3, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Float32x3, Offset = 12 }
         ]
      };

   // TextPx instance: vec4<i32> char_box + i32 tex_offset, step per instance
   // The Vec4S (4 shorts = 8 bytes) is expanded to vec4<i32> (16 bytes) on upload,
   // and the int tex_offset is 4 bytes → total stride 20 bytes.
   static GPUVertexLayout InstanceLayoutTextPx ()
      => new () {
         Stride = 20, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Sint32x4, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Sint32, Offset = 16 }
         ]
      };

   // Text2D instance: vec2<f32> pos + vec4<i32> char_box + i32 tex_offset
   // 8 + 16 + 4 = 28 bytes, step per instance
   static GPUVertexLayout InstanceLayoutText2D ()
      => new () {
         Stride = 28, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x2, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Sint32x4, Offset = 8 },
            new () { Location = 2, Format = VertexFormat.Sint32, Offset = 24 }
         ]
      };

   // Text3D instance: vec3<f32> pos + vec4<i32> char_box + i32 tex_offset
   // 12 + 16 + 4 = 32 bytes, step per instance
   static GPUVertexLayout InstanceLayoutText3D ()
      => new () {
         Stride = 32, StepMode = VertexStepMode.Instance,
         Attributes = [
            new () { Location = 0, Format = VertexFormat.Float32x3, Offset = 0 },
            new () { Location = 1, Format = VertexFormat.Sint32x4, Offset = 12 },
            new () { Location = 2, Format = VertexFormat.Sint32, Offset = 28 }
         ]
      };

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   Dictionary<EPipeline, GPUPipeline> mPipelines = [];
   Dictionary<string, GPUShaderModule> mShaders = [];
   BindGroupLayout* mUniformLayout;
   BindGroupLayout* mTexturedLayout;
}
#endregion
