// pipeline-factory.ts -- Pre-compiles all WebGPU render pipelines at initialization
// Ports PipelineFactory.cs to the browser WebGPU API.

// Import shader sources as raw strings. These are loaded at build time
// or fetched at runtime. For now we embed them inline via a loader map.
import { shaderSources } from './shader-loader.js';

// ---------------------------------------------------------------------------
// EPipeline enum -- matches the C# EPipeline exactly
// ---------------------------------------------------------------------------
export enum Pipeline {
  Line2D = 0,
  Line3D = 1,
  Bezier2D = 2,
  DashLine2D = 3,
  Point2D = 4,
  Point3D = 5,
  Triangle2D = 6,
  Quad2D = 7,
  BlackLine = 8,
  GlassLine = 9,
  Gourad = 10,
  Phong = 11,
  PhongPink = 12,
  Pick = 13,
  Glass = 14,
  FlatFacet = 15,
  TextPx = 16,
  Text2D = 17,
  Text3D = 18,
  TriFanStencil = 19,
  TriFanCover = 20,
}

// ---------------------------------------------------------------------------
// Vertex layout helpers -- mirror the C# GPUVertexLayout factories
// ---------------------------------------------------------------------------

/** 2D line instance: two vec2<f32> endpoints (p0, p1), 16 bytes per instance */
function instanceLayout2DLine(): GPUVertexBufferLayout {
  return {
    arrayStride: 16,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x2', offset: 0 },
      { shaderLocation: 1, format: 'float32x2', offset: 8 },
    ],
  };
}

/** 3D line instance: two vec3<f32> endpoints (p0, p1), 24 bytes per instance */
function instanceLayout3DLine(): GPUVertexBufferLayout {
  return {
    arrayStride: 24,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x3', offset: 0 },
      { shaderLocation: 1, format: 'float32x3', offset: 12 },
    ],
  };
}

/** 2D point instance: one vec2<f32> position, 8 bytes per instance */
function instanceLayout2DPoint(): GPUVertexBufferLayout {
  return {
    arrayStride: 8,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x2', offset: 0 },
    ],
  };
}

/** 3D point instance: one vec3<f32> position, 12 bytes per instance */
function instanceLayout3DPoint(): GPUVertexBufferLayout {
  return {
    arrayStride: 12,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x3', offset: 0 },
    ],
  };
}

/** 2D direct vertex: one vec2<f32> position, 8 bytes per vertex */
function vertexLayout2D(): GPUVertexBufferLayout {
  return {
    arrayStride: 8,
    stepMode: 'vertex',
    attributes: [
      { shaderLocation: 0, format: 'float32x2', offset: 0 },
    ],
  };
}

/** 3D facet vertex: vec3<f32> position + vec3<f32> normal, 24 bytes per vertex */
function vertexLayout3DFacet(): GPUVertexBufferLayout {
  return {
    arrayStride: 24,
    stepMode: 'vertex',
    attributes: [
      { shaderLocation: 0, format: 'float32x3', offset: 0 },
      { shaderLocation: 1, format: 'float32x3', offset: 12 },
    ],
  };
}

/** TextPx instance: vec4<i32> char_box + i32 tex_offset, 20 bytes per instance */
function instanceLayoutTextPx(): GPUVertexBufferLayout {
  return {
    arrayStride: 20,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'sint32x4', offset: 0 },
      { shaderLocation: 1, format: 'sint32', offset: 16 },
    ],
  };
}

/** Text2D instance: vec2<f32> pos + vec4<i32> char_box + i32 tex_offset, 28 bytes */
function instanceLayoutText2D(): GPUVertexBufferLayout {
  return {
    arrayStride: 28,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x2', offset: 0 },
      { shaderLocation: 1, format: 'sint32x4', offset: 8 },
      { shaderLocation: 2, format: 'sint32', offset: 24 },
    ],
  };
}

/** Text3D instance: vec3<f32> pos + vec4<i32> char_box + i32 tex_offset, 32 bytes */
function instanceLayoutText3D(): GPUVertexBufferLayout {
  return {
    arrayStride: 32,
    stepMode: 'instance',
    attributes: [
      { shaderLocation: 0, format: 'float32x3', offset: 0 },
      { shaderLocation: 1, format: 'sint32x4', offset: 12 },
      { shaderLocation: 2, format: 'sint32', offset: 28 },
    ],
  };
}

// ---------------------------------------------------------------------------
// Blend state used for anti-aliased primitives (lines, points, text)
// ---------------------------------------------------------------------------
const ALPHA_BLEND: GPUBlendState = {
  color: {
    srcFactor: 'src-alpha',
    dstFactor: 'one-minus-src-alpha',
    operation: 'add',
  },
  alpha: {
    srcFactor: 'one',
    dstFactor: 'one-minus-src-alpha',
    operation: 'add',
  },
};

// ---------------------------------------------------------------------------
// PipelineFactory
// ---------------------------------------------------------------------------
export class PipelineFactory {
  private pipelines: Map<Pipeline, GPURenderPipeline> = new Map();
  private shaderModules: Map<string, GPUShaderModule> = new Map();
  uniformLayout!: GPUBindGroupLayout;
  texturedLayout!: GPUBindGroupLayout;

  /** Initialize all pipelines for the given device and canvas format */
  async init(device: GPUDevice, format: GPUTextureFormat): Promise<void> {
    this.loadShaders(device);
    this.createBindGroupLayouts(device);
    this.createAllPipelines(device, format);
  }

  /** Retrieve a pre-compiled render pipeline by type */
  get(pipeline: Pipeline): GPURenderPipeline {
    const p = this.pipelines.get(pipeline);
    if (!p) throw new Error(`Pipeline ${Pipeline[pipeline]} not compiled`);
    return p;
  }

  /** Check if a pipeline exists */
  has(pipeline: Pipeline): boolean {
    return this.pipelines.has(pipeline);
  }

  // -------------------------------------------------------------------------
  // Internals
  // -------------------------------------------------------------------------

  private loadShaders(device: GPUDevice): void {
    const sources = shaderSources();
    for (const [name, code] of Object.entries(sources)) {
      this.shaderModules.set(name, device.createShaderModule({
        code,
        label: name,
      }));
    }
  }

  private createBindGroupLayouts(device: GPUDevice): void {
    // Uniform-only layout: binding 0 = uniform buffer (vertex+fragment)
    this.uniformLayout = device.createBindGroupLayout({
      label: 'uniform-layout',
      entries: [{
        binding: 0,
        visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
        buffer: { type: 'uniform' },
      }],
    });

    // Textured layout: binding 0 = uniform, binding 1 = texture, binding 2 = sampler
    this.texturedLayout = device.createBindGroupLayout({
      label: 'textured-layout',
      entries: [
        {
          binding: 0,
          visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
          buffer: { type: 'uniform' },
        },
        {
          binding: 1,
          visibility: GPUShaderStage.FRAGMENT,
          texture: { sampleType: 'float', viewDimension: '2d', multisampled: false },
        },
        {
          binding: 2,
          visibility: GPUShaderStage.FRAGMENT,
          sampler: { type: 'filtering' },
        },
      ],
    });
  }

  private createAllPipelines(device: GPUDevice, format: GPUTextureFormat): void {
    const line2DLayout = [instanceLayout2DLine()];
    this.build(device, format, Pipeline.Line2D, 'Line2D', line2DLayout, { blend: true });
    this.build(device, format, Pipeline.Bezier2D, 'Bezier2D', line2DLayout, { blend: true });
    this.build(device, format, Pipeline.DashLine2D, 'DashLine2D', line2DLayout, { blend: true, textured: true });

    const line3DLayout = [instanceLayout3DLine()];
    this.build(device, format, Pipeline.Line3D, 'Line3D', line3DLayout, { blend: true, depth: true });
    this.build(device, format, Pipeline.BlackLine, 'Line3D', line3DLayout, { blend: true, depth: true });
    this.build(device, format, Pipeline.GlassLine, 'GlassLine', line3DLayout, { blend: true, depth: true });

    const pt2DLayout = [instanceLayout2DPoint()];
    this.build(device, format, Pipeline.Point2D, 'Point2D', pt2DLayout, { blend: true });

    const pt3DLayout = [instanceLayout3DPoint()];
    this.build(device, format, Pipeline.Point3D, 'Point3D', pt3DLayout, { blend: true });

    const flat2DLayout = [vertexLayout2D()];
    this.build(device, format, Pipeline.Triangle2D, 'Flat2D', flat2DLayout, {});
    this.build(device, format, Pipeline.Quad2D, 'Flat2D', flat2DLayout, {});

    const facet3DLayout = [vertexLayout3DFacet()];
    this.build(device, format, Pipeline.Gourad, 'Gourad', facet3DLayout, { depth: true });
    this.build(device, format, Pipeline.Phong, 'Phong', facet3DLayout, { depth: true });
    this.build(device, format, Pipeline.PhongPink, 'PhongPink', facet3DLayout, { depth: true });
    this.build(device, format, Pipeline.Pick, 'Pick', facet3DLayout, { depth: true });
    this.build(device, format, Pipeline.Glass, 'Glass', facet3DLayout, { depth: true });
    this.build(device, format, Pipeline.FlatFacet, 'FlatFacet', facet3DLayout, { depth: true });

    const textPxLayout = [instanceLayoutTextPx()];
    this.build(device, format, Pipeline.TextPx, 'TextPx', textPxLayout, { blend: true, textured: true });

    const text2DLayout = [instanceLayoutText2D()];
    this.build(device, format, Pipeline.Text2D, 'Text2D', text2DLayout, { blend: true, textured: true });

    const text3DLayout = [instanceLayoutText3D()];
    this.build(device, format, Pipeline.Text3D, 'Text3D', text3DLayout, { blend: true, depth: true, textured: true });

    // Stencil pipelines for tri-fan fill
    this.buildStencil(device, format, Pipeline.TriFanStencil, 'Flat2D', flat2DLayout, {
      compare: 'always',
      passOp: 'invert',
      failOp: 'keep',
      colorWrite: 0x0,   // no color output
    });
    this.buildStencil(device, format, Pipeline.TriFanCover, 'Flat2D', flat2DLayout, {
      compare: 'not-equal',
      passOp: 'zero',
      failOp: 'keep',
      colorWrite: GPUColorWrite.ALL,
    });
  }

  private build(
    device: GPUDevice,
    format: GPUTextureFormat,
    id: Pipeline,
    shaderName: string,
    layouts: GPUVertexBufferLayout[],
    opts: { blend?: boolean; depth?: boolean; textured?: boolean },
  ): void {
    const shader = this.shaderModules.get(shaderName);
    if (!shader) {
      console.warn(`Shader "${shaderName}" not found, skipping pipeline ${Pipeline[id]}`);
      return;
    }
    const bindGroupLayout = opts.textured ? this.texturedLayout : this.uniformLayout;
    const pipelineLayout = device.createPipelineLayout({
      label: `${Pipeline[id]}-layout`,
      bindGroupLayouts: [bindGroupLayout],
    });

    const colorTarget: GPUColorTargetState = {
      format,
      ...(opts.blend ? { blend: ALPHA_BLEND } : {}),
      writeMask: GPUColorWrite.ALL,
    };

    // All pipelines must declare depth/stencil format to match the render pass.
    // 3D pipelines write depth; 2D pipelines pass through without depth testing.
    const depthStencil: GPUDepthStencilState = opts.depth
      ? {
        format: 'depth24plus-stencil8',
        depthWriteEnabled: true,
        depthCompare: 'less-equal',
      }
      : {
        format: 'depth24plus-stencil8',
        depthWriteEnabled: false,
        depthCompare: 'always',
      };

    const pipeline = device.createRenderPipeline({
      label: Pipeline[id],
      layout: pipelineLayout,
      vertex: {
        module: shader,
        entryPoint: 'vs_main',
        buffers: layouts,
      },
      fragment: {
        module: shader,
        entryPoint: 'fs_main',
        targets: [colorTarget],
      },
      primitive: {
        topology: 'triangle-list',
        cullMode: 'none',
      },
      depthStencil,
    });
    this.pipelines.set(id, pipeline);
  }

  private buildStencil(
    device: GPUDevice,
    format: GPUTextureFormat,
    id: Pipeline,
    shaderName: string,
    layouts: GPUVertexBufferLayout[],
    opts: {
      compare: GPUCompareFunction;
      passOp: GPUStencilOperation;
      failOp: GPUStencilOperation;
      colorWrite: GPUColorWriteFlags;
    },
  ): void {
    const shader = this.shaderModules.get(shaderName);
    if (!shader) return;
    const pipelineLayout = device.createPipelineLayout({
      label: `${Pipeline[id]}-layout`,
      bindGroupLayouts: [this.uniformLayout],
    });

    const stencilFace: GPUStencilFaceState = {
      compare: opts.compare,
      passOp: opts.passOp,
      failOp: opts.failOp,
      depthFailOp: 'keep',
    };

    const pipeline = device.createRenderPipeline({
      label: Pipeline[id],
      layout: pipelineLayout,
      vertex: {
        module: shader,
        entryPoint: 'vs_main',
        buffers: layouts,
      },
      fragment: {
        module: shader,
        entryPoint: 'fs_main',
        targets: [{
          format,
          writeMask: opts.colorWrite,
        }],
      },
      primitive: {
        topology: 'triangle-list',
        cullMode: 'none',
      },
      depthStencil: {
        format: 'depth24plus-stencil8',
        depthWriteEnabled: false,
        depthCompare: 'always',
        stencilFront: stencilFace,
        stencilBack: stencilFace,
        stencilReadMask: 0xFF,
        stencilWriteMask: 0xFF,
      },
    });
    this.pipelines.set(id, pipeline);
  }
}
