// ────── ╔╗                                                                                    GPU
// ╔═╦╦═╦╦╬╣ GPUPipeline.cs
// ║║║║╬║╔╣║ Wraps WebGPU shader module and render pipeline creation
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region struct GPUStencilConfig ----------------------------------------------------------------------
/// <summary>Configures stencil operations for a render pipeline</summary>
public struct GPUStencilConfig {
   /// <summary>Stencil comparison function for front faces</summary>
   public CompareFunction FrontCompare;
   /// <summary>Operation when stencil test passes for front faces</summary>
   public StencilOperation FrontPassOp;
   /// <summary>Operation when stencil test fails for front faces</summary>
   public StencilOperation FrontFailOp;
   /// <summary>Stencil comparison function for back faces</summary>
   public CompareFunction BackCompare;
   /// <summary>Operation when stencil test passes for back faces</summary>
   public StencilOperation BackPassOp;
   /// <summary>Operation when stencil test fails for back faces</summary>
   public StencilOperation BackFailOp;
   /// <summary>Bitmask for stencil read operations</summary>
   public uint ReadMask;
   /// <summary>Bitmask for stencil write operations</summary>
   public uint WriteMask;
}
#endregion

#region struct GPUVertexLayout ------------------------------------------------------------------------
/// <summary>Describes the layout of vertex attributes for a pipeline</summary>
public struct GPUVertexLayout {
   /// <summary>Stride in bytes between consecutive vertices</summary>
   public ulong Stride;
   /// <summary>Per-vertex or per-instance stepping</summary>
   public VertexStepMode StepMode;
   /// <summary>Attribute descriptors (location, format, offset)</summary>
   public GPUVertexAttr[] Attributes;
}
#endregion

#region struct GPUVertexAttr ---------------------------------------------------------------------------
/// <summary>Describes a single vertex attribute within a vertex layout</summary>
public struct GPUVertexAttr {
   /// <summary>Shader location index</summary>
   public uint Location;
   /// <summary>Vertex data format</summary>
   public VertexFormat Format;
   /// <summary>Byte offset within the vertex</summary>
   public ulong Offset;
}
#endregion

#region class GPUShaderModule -------------------------------------------------------------------------
/// <summary>Wraps a WebGPU shader module created from WGSL source</summary>
public unsafe class GPUShaderModule : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU shader module handle</summary>
   public ShaderModule* Handle => mModule;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a shader module from WGSL source code</summary>
   public static GPUShaderModule Create (GPUDevice gpu, string wgslSource, string? label = null) {
      GPUShaderModule sm = new () { mGPU = gpu };
      ShaderModuleWGSLDescriptor wgslDesc = new () {
         Code = (byte*)SilkMarshal.StringToPtr (wgslSource),
         Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor }
      };
      ShaderModuleDescriptor desc = new () {
         NextInChain = (ChainedStruct*)(&wgslDesc)
      };
      if (label != null) desc.Label = (byte*)SilkMarshal.StringToPtr (label);
      sm.mModule = gpu.Api.DeviceCreateShaderModule (gpu.Device, &desc);
      SilkMarshal.FreeString ((nint)wgslDesc.Code);
      if (label != null) SilkMarshal.FreeString ((nint)desc.Label);
      if (sm.mModule == null) throw new Exception ("Failed to create WebGPU shader module");
      return sm;
   }

   /// <summary>Release the shader module</summary>
   public void Dispose () {
      if (mModule != null) { mGPU.Api.ShaderModuleRelease (mModule); mModule = null; }
      GC.SuppressFinalize (this);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   ShaderModule* mModule;
}
#endregion

#region class GPUPipeline -----------------------------------------------------------------------------
/// <summary>Wraps a WebGPU render pipeline</summary>
public unsafe class GPUPipeline : IDisposable {
   // Properties ---------------------------------------------------------------
   /// <summary>The underlying WebGPU render pipeline handle</summary>
   public RenderPipeline* Handle => mPipeline;

   /// <summary>The pipeline layout (if created explicitly)</summary>
   public PipelineLayout* Layout => mLayout;

   // Methods ------------------------------------------------------------------
   /// <summary>Create a render pipeline with the given configuration</summary>
   public static GPUPipeline Create (
      GPUDevice gpu,
      GPUShaderModule vertexShader, string vsEntry,
      GPUShaderModule fragmentShader, string fsEntry,
      GPUVertexLayout[] vertexLayouts,
      TextureFormat colorFormat,
      bool enableBlend = false,
      bool enableDepth = false,
      TextureFormat depthFormat = TextureFormat.Depth24PlusStencil8,
      PrimitiveTopology topology = PrimitiveTopology.TriangleList,
      BindGroupLayout*[]? bindGroupLayouts = null,
      string? label = null,
      GPUStencilConfig? stencil = null,
      ColorWriteMask colorWriteMask = ColorWriteMask.All) {

      GPUPipeline pipe = new () { mGPU = gpu };

      // Build pipeline layout if bind group layouts are provided
      if (bindGroupLayouts != null && bindGroupLayouts.Length > 0) {
         fixed (BindGroupLayout** pLayouts = bindGroupLayouts) {
            PipelineLayoutDescriptor layoutDesc = new () {
               BindGroupLayoutCount = (uint)bindGroupLayouts.Length,
               BindGroupLayouts = pLayouts
            };
            pipe.mLayout = gpu.Api.DeviceCreatePipelineLayout (gpu.Device, &layoutDesc);
         }
      }

      // Build vertex buffer layouts with attributes
      VertexBufferLayout* vbLayouts = stackalloc VertexBufferLayout[vertexLayouts.Length];
      // We need pinned arrays for attributes
      VertexAttribute[][] attrArrays = new VertexAttribute[vertexLayouts.Length][];
      for (int i = 0; i < vertexLayouts.Length; i++) {
         GPUVertexLayout vl = vertexLayouts[i];
         attrArrays[i] = new VertexAttribute[vl.Attributes.Length];
         for (int j = 0; j < vl.Attributes.Length; j++) {
            attrArrays[i][j] = new VertexAttribute {
               ShaderLocation = vl.Attributes[j].Location,
               Format = vl.Attributes[j].Format,
               Offset = vl.Attributes[j].Offset
            };
         }
      }

      // Pin all attribute arrays and set up vertex buffer layouts
      GCHandle[] pins = new GCHandle[vertexLayouts.Length];
      try {
         for (int i = 0; i < vertexLayouts.Length; i++) {
            pins[i] = GCHandle.Alloc (attrArrays[i], GCHandleType.Pinned);
            vbLayouts[i] = new VertexBufferLayout {
               ArrayStride = vertexLayouts[i].Stride,
               StepMode = vertexLayouts[i].StepMode,
               AttributeCount = (uint)attrArrays[i].Length,
               Attributes = (VertexAttribute*)pins[i].AddrOfPinnedObject ()
            };
         }

         // Vertex state
         byte* vsEntryPtr = (byte*)SilkMarshal.StringToPtr (vsEntry);
         VertexState vertexState = new () {
            Module = vertexShader.Handle,
            EntryPoint = vsEntryPtr,
            BufferCount = (uint)vertexLayouts.Length,
            Buffers = vbLayouts
         };

         // Fragment state
         byte* fsEntryPtr = (byte*)SilkMarshal.StringToPtr (fsEntry);
         BlendState blendState = new () {
            Color = new BlendComponent {
               SrcFactor = BlendFactor.SrcAlpha,
               DstFactor = BlendFactor.OneMinusSrcAlpha,
               Operation = BlendOperation.Add
            },
            Alpha = new BlendComponent {
               SrcFactor = BlendFactor.One,
               DstFactor = BlendFactor.OneMinusSrcAlpha,
               Operation = BlendOperation.Add
            }
         };
         ColorTargetState colorTarget = new () {
            Format = colorFormat,
            WriteMask = colorWriteMask,
            Blend = enableBlend ? &blendState : null
         };
         FragmentState fragmentState = new () {
            Module = fragmentShader.Handle,
            EntryPoint = fsEntryPtr,
            TargetCount = 1,
            Targets = &colorTarget
         };

         // Depth stencil state
         bool useDepthStencil = enableDepth || stencil.HasValue;
         GPUStencilConfig sc = stencil ?? default;
         DepthStencilState depthStencil = new () {
            Format = depthFormat,
            DepthWriteEnabled = enableDepth,
            DepthCompare = enableDepth ? CompareFunction.Less : CompareFunction.Always,
            StencilFront = new StencilFaceState {
               Compare = stencil.HasValue ? sc.FrontCompare : CompareFunction.Always,
               FailOp = stencil.HasValue ? sc.FrontFailOp : StencilOperation.Keep,
               DepthFailOp = StencilOperation.Keep,
               PassOp = stencil.HasValue ? sc.FrontPassOp : StencilOperation.Keep
            },
            StencilBack = new StencilFaceState {
               Compare = stencil.HasValue ? sc.BackCompare : CompareFunction.Always,
               FailOp = stencil.HasValue ? sc.BackFailOp : StencilOperation.Keep,
               DepthFailOp = StencilOperation.Keep,
               PassOp = stencil.HasValue ? sc.BackPassOp : StencilOperation.Keep
            },
            StencilReadMask = stencil.HasValue ? sc.ReadMask : 0xFFu,
            StencilWriteMask = stencil.HasValue ? sc.WriteMask : 0xFFu
         };

         // Assemble render pipeline descriptor
         RenderPipelineDescriptor pipeDesc = new () {
            Layout = pipe.mLayout,
            Vertex = vertexState,
            Fragment = &fragmentState,
            Primitive = new PrimitiveState {
               Topology = topology,
               StripIndexFormat = IndexFormat.Undefined,
               FrontFace = FrontFace.Ccw,
               CullMode = CullMode.None
            },
            DepthStencil = useDepthStencil ? &depthStencil : null,
            Multisample = new MultisampleState {
               Count = 1,
               Mask = ~0u,
               AlphaToCoverageEnabled = false
            }
         };
         if (label != null) pipeDesc.Label = (byte*)SilkMarshal.StringToPtr (label);

         pipe.mPipeline = gpu.Api.DeviceCreateRenderPipeline (gpu.Device, &pipeDesc);

         SilkMarshal.FreeString ((nint)vsEntryPtr);
         SilkMarshal.FreeString ((nint)fsEntryPtr);
         if (label != null) SilkMarshal.FreeString ((nint)pipeDesc.Label);
      } finally {
         for (int i = 0; i < pins.Length; i++)
            if (pins[i].IsAllocated) pins[i].Free ();
      }

      if (pipe.mPipeline == null) throw new Exception ("Failed to create WebGPU render pipeline");
      return pipe;
   }

   /// <summary>Release the pipeline and layout</summary>
   public void Dispose () {
      if (mPipeline != null) { mGPU.Api.RenderPipelineRelease (mPipeline); mPipeline = null; }
      if (mLayout != null) { mGPU.Api.PipelineLayoutRelease (mLayout); mLayout = null; }
      GC.SuppressFinalize (this);
   }

   // Private data -------------------------------------------------------------
   GPUDevice mGPU = null!;
   RenderPipeline* mPipeline;
   PipelineLayout* mLayout;
}
#endregion
