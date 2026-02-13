// ────── ╔╗                                                                                    LUX
// ╔═╦╦═╦╦╬╣ RenderState.cs
// ║║║║╬║╔╣║ RenderState — GPU-backend-agnostic render state manager (replaces GLState)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class RenderState --------------------------------------------------------------------------
/// <summary>RenderState manages current GPU rendering state through the IGPU abstraction</summary>
/// This replaces the old GLState class. In WebGPU, blend/depth/stencil state is baked
/// into the pipeline object, so setting a program resolves to an EPipeline and calls
/// IGPU.SetPipeline. State properties (Blending, DepthTest, etc.) are tracked for
/// diagnostics but do not issue GPU calls — the pipeline handles everything.
///
/// Unlike GLState (which was static), RenderState is an instance class that holds
/// a reference to the IGPU backend. A static accessor (RenderState.It) provides
/// access during the transition period.
class RenderState {
   // Constructor --------------------------------------------------------------
   /// <summary>Create a RenderState attached to a particular GPU backend</summary>
   public RenderState (IGPU gpu) => mGPU = gpu;

   // Properties ---------------------------------------------------------------
   /// <summary>The current RenderState instance</summary>
   public static RenderState It => sIt!;
   static RenderState? sIt;

   /// <summary>Initialize the global RenderState instance with a GPU backend</summary>
   public static void Init (IGPU gpu) => sIt = new RenderState (gpu);

   /// <summary>The underlying GPU backend</summary>
   public IGPU GPU => mGPU;

   /// <summary>Is blending enabled (tracked state, baked into pipeline)</summary>
   public bool Blending { get => mBlending; set => mBlending = value; }
   bool mBlending;

   /// <summary>Is depth testing enabled (tracked state, baked into pipeline)</summary>
   public bool DepthTest { get => mDepthTest; set => mDepthTest = value; }
   bool mDepthTest;

   /// <summary>Is polygon-offset-fill enabled (tracked state, baked into pipeline)</summary>
   public bool PolygonOffsetFill { get => mPolygonOffsetFill; set => mPolygonOffsetFill = value; }
   bool mPolygonOffsetFill;

   /// <summary>Stencil behavior of the current pipeline (tracked, baked into pipeline)</summary>
   public EStencilBehavior StencilBehavior {
      get => mStencilBehavior;
      set => mStencilBehavior = value;
   }
   EStencilBehavior mStencilBehavior;

   /// <summary>The current shader program</summary>
   /// Setting a program resolves the EPipeline and calls IGPU.SetPipeline.
   /// Blend, depth, stencil, and polygon-offset state are tracked from the
   /// program's configuration for diagnostics.
   public ShaderImp? Program {
      get => mProgram;
      set {
         if (mProgram == value) return;
         mProgram = value;
         if (value != null) {
            mPgmChanges++;
            Blending = value.Blending;
            DepthTest = value.DepthTest;
            PolygonOffsetFill = value.PolygonOffset;
            StencilBehavior = value.StencilBehavior;
            mGPU.SetPipeline ((int)ResolvePipeline (value));
         }
      }
   }
   ShaderImp? mProgram;
   internal int mPgmChanges;

   /// <summary>The current vertex buffer binding (replaces OpenGL VAO concept)</summary>
   /// WebGPU does not have VAOs. This tracked int allows callers to detect
   /// redundant bindings during the transition. Callers will migrate to
   /// IGPU.SetVertexBuffer in Task 2.3.
   public int VertexBinding {
      get => mVertexBinding;
      set {
         if (mVertexBinding == value) return;
         mVertexBinding = value;
         if (value != 0) mBindChanges++;
      }
   }
   int mVertexBinding;
   internal int mBindChanges;

   /// <summary>The current typeface being used for text rendering</summary>
   public TypeFace? TypeFace {
      set {
         if (mTypeFaceId == value?.UID) return;
         mTypeFaceId = value?.UID ?? 0;
         // Font texture binding is handled by the text shader draw path,
         // not by global state (WebGPU uses explicit bind groups per draw)
      }
   }
   int mTypeFaceId;

   // Methods ------------------------------------------------------------------
   /// <summary>Reset all state at the start of every frame</summary>
   public void StartFrame (Vec2S size, Color4 bgrdColor) {
      mGPU.SetViewport (0, 0, size.X, size.Y);
      mBlending = false;
      mDepthTest = false;
      mStencilBehavior = EStencilBehavior.None;
      mPolygonOffsetFill = false;
      mProgram = null;
      mPgmChanges = 0; mBindChanges = 0;
      mVertexBinding = 0;
      mTypeFaceId = 0;
      mGPU.Clear (bgrdColor);
   }

   /// <summary>Resolve the correct EPipeline for a given ShaderImp</summary>
   /// Each ShaderImp stores its Pipeline enum directly (resolved at load time),
   /// so this is now a simple field access.
   public EPipeline ResolvePipeline (ShaderImp pgm)
      => pgm.Pipeline;

   // Private data -------------------------------------------------------------
   readonly IGPU mGPU;
}
#endregion

#region static class GLState (backward compatibility) ----------------------------------------------
/// <summary>Static shim that forwards to RenderState.It during migration</summary>
/// Existing Lux code references GLState.Program, GLState.TypeFace, etc.
/// This shim forwards those calls to the instance-based RenderState so
/// callers can be migrated incrementally. Remove once all callers use
/// RenderState directly (Task 2.5).
static class GLState {
   // Properties ---------------------------------------------------------------
   /// <summary>Is blending enabled</summary>
   public static bool Blending { set => RenderState.It.Blending = value; }

   /// <summary>Is depth-testing enabled</summary>
   public static bool DepthTest { set => RenderState.It.DepthTest = value; }

   /// <summary>Is polygon-offset-fill enabled</summary>
   public static bool PolygonOffsetFill { set => RenderState.It.PolygonOffsetFill = value; }

   /// <summary>The current shader program</summary>
   public static ShaderImp? Program { set => RenderState.It.Program = value; }

   /// <summary>The stencil behavior</summary>
   public static EStencilBehavior StencilBehavior { set => RenderState.It.StencilBehavior = value; }

   /// <summary>The current typeface</summary>
   public static TypeFace? TypeFace { set => RenderState.It.TypeFace = value; }

   /// <summary>Vertex binding tracker (replaces OpenGL VAO)</summary>
   public static int VAO {
      get => RenderState.It.VertexBinding;
      set => RenderState.It.VertexBinding = value;
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Reset state at the start of every frame</summary>
   public static void StartFrame (Vec2S size, Color4 bgrdColor)
      => RenderState.It.StartFrame (size, bgrdColor);

   /// <summary>Number of pipeline changes this frame</summary>
   internal static int mPgmChanges => RenderState.It.mPgmChanges;

   /// <summary>Number of vertex binding changes this frame</summary>
   internal static int mVAOChanges => RenderState.It.mBindChanges;
}
#endregion
