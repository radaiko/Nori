// shader-loader.ts -- Embeds all WGSL shader sources as strings
// These are the same shaders used by the C# desktop renderer.
// In a bundled environment (Vite, webpack), these could be loaded via
// ?raw imports. For portability, they are embedded inline here.

/** Returns a map of shader name to WGSL source code */
export function shaderSources(): Record<string, string> {
  return {
    Line2D: LINE2D,
    Line3D: LINE3D,
    Bezier2D: BEZIER2D,
    DashLine2D: DASHLINE2D,
    Point2D: POINT2D,
    Point3D: POINT3D,
    Flat2D: FLAT2D,
    Gourad: GOURAD,
    Phong: PHONG,
    PhongPink: PHONGPINK,
    Pick: PICK,
    Glass: GLASS,
    FlatFacet: FLATFACET,
    GlassLine: GLASSLINE,
    TextPx: TEXTPX,
    Text2D: TEXT2D,
    Text3D: TEXT3D,
    GBufferNormal: GBUFFER_NORMAL,
    CADGooch: CAD_GOOCH,
    EdgeComposite: EDGE_COMPOSITE,
  };
}

// ---------------------------------------------------------------------------
// Embedded WGSL shader sources
// ---------------------------------------------------------------------------

const LINE2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    line_width: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) p0: vec2<f32>,
    @location(1) p1: vec2<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) dist: f32,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 0.0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 0.0, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale;
    let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.0625;
    let is_end = (corner & 1u) != 0u;
    let is_neg = (corner & 2u) != 0u;
    let base_pt = select(px0, px1, is_end);
    let dir_sign = select(-1.0, 1.0, is_end);
    let perp_sign = select(1.0, -1.0, is_neg);
    let pos_px = base_pt + perp * perp_sign + dir * dir_sign;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, 0.0, 1.0);
    out.dist = width * 2.9 * select(1.0, -1.0, is_neg);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let d = abs(in.dist) / uniforms.line_width;
    let a = exp2(-2.0 * d * d);
    return vec4<f32>(uniforms.draw_color.rgb, a * uniforms.draw_color.a);
}
`;

const LINE3D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    line_width: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) p0: vec3<f32>,
    @location(1) p1: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) dist: f32,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale;
    let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.25;
    let is_end = (corner & 1u) != 0u;
    let is_neg = (corner & 2u) != 0u;
    let base_pt = select(px0, px1, is_end);
    let base_z = select(clip0.z, clip1.z, is_end);
    let dir_sign = select(-1.0, 1.0, is_end);
    let perp_sign = select(1.0, -1.0, is_neg);
    let pos_px = base_pt + perp * perp_sign + dir * dir_sign;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, base_z, 1.0);
    out.dist = width * 2.9 * select(1.0, -1.0, is_neg);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let d = abs(in.dist) / uniforms.line_width;
    let a = exp2(-2.0 * d * d);
    return vec4<f32>(uniforms.draw_color.rgb, a * uniforms.draw_color.a);
}
`;

const BEZIER2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    line_width: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) p0: vec2<f32>,
    @location(1) p1: vec2<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) dist: f32,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 0.0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 0.0, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale;
    let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.0625;
    let is_end = (corner & 1u) != 0u;
    let is_neg = (corner & 2u) != 0u;
    let base_pt = select(px0, px1, is_end);
    let dir_sign = select(-1.0, 1.0, is_end);
    let perp_sign = select(1.0, -1.0, is_neg);
    let pos_px = base_pt + perp * perp_sign + dir * dir_sign;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, 0.0, 1.0);
    out.dist = width * 2.9 * select(1.0, -1.0, is_neg);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let d = abs(in.dist) / uniforms.line_width;
    let a = exp2(-2.0 * d * d);
    return vec4<f32>(uniforms.draw_color.rgb, a * uniforms.draw_color.a);
}
`;

const DASHLINE2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    line_width: f32,
    lt_scale: f32,
    draw_color: vec4<f32>,
    line_type: f32,
    _pad0: f32,
    _pad1: f32,
    _pad2: f32,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var ltype_texture: texture_2d<f32>;
@group(0) @binding(2) var ltype_sampler: sampler;

struct VertexInput {
    @location(0) p0: vec2<f32>,
    @location(1) p1: vec2<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) dist: f32,
    @location(1) tex_coord: f32,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 0.0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 0.0, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale;
    let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    let raw_dir = px1 - px0;
    let len = length(raw_dir);
    var dir = normalize(raw_dir) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.0625;
    let is_end = (corner & 1u) != 0u;
    let is_neg = (corner & 2u) != 0u;
    let base_pt = select(px0, px1, is_end);
    let dir_sign = select(-1.0, 1.0, is_end);
    let perp_sign = select(1.0, -1.0, is_neg);
    let pos_px = base_pt + perp * perp_sign + dir * dir_sign;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, 0.0, 1.0);
    out.dist = width * 2.9 * select(1.0, -1.0, is_neg);
    out.tex_coord = select(0.0, len / uniforms.lt_scale, is_end);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let d = abs(in.dist) / uniforms.line_width;
    let a1 = exp2(-2.0 * d * d);
    let a2 = textureSample(ltype_texture, ltype_sampler, vec2<f32>(in.tex_coord, uniforms.line_type)).r;
    return vec4<f32>(uniforms.draw_color.rgb, a1 * a2);
}
`;

const POINT2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    point_size: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) pos: vec2<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) st_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
var<private> CORNER_SIGNS: array<vec2<f32>, 4> = array<vec2<f32>, 4>(
    vec2<f32>(1.0, 1.0),
    vec2<f32>(1.0, -1.0),
    vec2<f32>(-1.0, 1.0),
    vec2<f32>(-1.0, -1.0),
);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip = uniforms.xfm * vec4<f32>(input.pos, 0.0, 1.0);
    let px = clip.xy / uniforms.vp_scale;
    let h = uniforms.point_size / 2.0;
    let signs = CORNER_SIGNS[corner];
    let pos_px = px + signs * h;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, 0.0, 1.0);
    out.st_coord = signs * h;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let radius = uniforms.point_size / 2.0;
    let dist = length(in.st_coord);
    let a = 1.0 - smoothstep(radius - 1.0, radius, dist);
    return vec4<f32>(uniforms.draw_color.rgb, a);
}
`;

const POINT3D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    point_size: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) pos: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) st_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
var<private> CORNER_SIGNS: array<vec2<f32>, 4> = array<vec2<f32>, 4>(
    vec2<f32>(1.0, 1.0),
    vec2<f32>(1.0, -1.0),
    vec2<f32>(-1.0, 1.0),
    vec2<f32>(-1.0, -1.0),
);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip = uniforms.xfm * vec4<f32>(input.pos, 1.0);
    let px = clip.xy / uniforms.vp_scale;
    let h = uniforms.point_size / 2.0;
    let signs = CORNER_SIGNS[corner];
    let pos_px = px + signs * h;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, clip.z, 1.0);
    out.st_coord = signs * h;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let radius = uniforms.point_size / 2.0;
    let dist = length(in.st_coord);
    let a = 1.0 - smoothstep(radius - 1.0, radius, dist);
    return vec4<f32>(uniforms.draw_color.rgb, a);
}
`;

const FLAT2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
};

@vertex
fn vs_main(@location(0) pos: vec2<f32>) -> VertexOutput {
    var out: VertexOutput;
    out.position = uniforms.xfm * vec4<f32>(pos, 0.0, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return uniforms.draw_color;
}
`;

const GOURAD = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) light_intensity: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let diffuse = abs(dot(light_dir, normalize(input.normal)));
    var out: VertexOutput;
    out.light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.light_intensity.rgb, uniforms.draw_color.a);
}
`;

const PHONG = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.normal = input.normal;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let tnorm = normalize(in.normal);
    let diffuse = abs(dot(light_dir, tnorm));
    let light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    return vec4<f32>(light_intensity.rgb, uniforms.draw_color.a);
}
`;

const PHONGPINK = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);
const PINK = vec4<f32>(1.0, 0.0, 1.0, 1.0);

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
};

struct FragInput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
    @builtin(front_facing) front_facing: bool,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.normal = input.normal;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: FragInput) -> @location(0) vec4<f32> {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let tnorm = normalize(in.normal);
    let diffuse = abs(dot(light_dir, tnorm));
    let color = select(PINK, uniforms.draw_color, in.front_facing);
    let light_intensity = color * diffuse + AMBIENT_COLOR;
    return vec4<f32>(light_intensity.rgb, uniforms.draw_color.a);
}
`;

const PICK = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return uniforms.draw_color;
}
`;

const GLASS = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) light_intensity: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let diffuse = abs(dot(light_dir, normalize(input.normal)));
    var out: VertexOutput;
    out.light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let coord = vec2<i32>(in.position.xy - vec2<f32>(0.5));
    if fract(f32(coord.x + coord.y) / 2.0) < 0.5 {
        discard;
    }
    return vec4<f32>(in.light_intensity.rgb, uniforms.draw_color.a);
}
`;

const FLATFACET = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) light_intensity: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let diffuse = abs(dot(light_dir, normalize(input.normal)));
    var out: VertexOutput;
    out.light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.light_intensity.rgb, uniforms.draw_color.a);
}
`;

const GLASSLINE = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    line_width: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) p0: vec3<f32>,
    @location(1) p1: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) dist: f32,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale;
    let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.25;
    let is_end = (corner & 1u) != 0u;
    let is_neg = (corner & 2u) != 0u;
    let base_pt = select(px0, px1, is_end);
    let base_z = select(clip0.z, clip1.z, is_end);
    let dir_sign = select(-1.0, 1.0, is_end);
    let perp_sign = select(1.0, -1.0, is_neg);
    let pos_px = base_pt + perp * perp_sign + dir * dir_sign;
    var out: VertexOutput;
    out.position = vec4<f32>(uniforms.vp_scale * pos_px, base_z, 1.0);
    out.dist = width * 2.9 * select(1.0, -1.0, is_neg);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let coord = vec2<i32>(in.position.xy - vec2<f32>(0.5));
    if fract(f32(coord.x + coord.y) / 2.0) < 0.5 {
        discard;
    }
    let d = abs(in.dist) / uniforms.line_width;
    let a = exp2(-2.0 * d * d);
    return vec4<f32>(uniforms.draw_color.rgb, a);
}
`;

const TEXTPX = /* wgsl */`
struct Uniforms {
    vp_scale: vec2<f32>,
    _pad: vec2<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var font_texture: texture_2d<f32>;
@group(0) @binding(2) var font_sampler: sampler;

struct VertexInput {
    @location(0) char_box: vec4<i32>,
    @location(1) tex_offset: i32,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) cell_size: vec2<i32>,
    @location(1) @interpolate(flat) tex_offset: i32,
    @location(2) tex_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let cell_size = vec2<i32>(input.char_box.z - input.char_box.x,
                              input.char_box.w - input.char_box.y);
    let box_xy = vec2<f32>(f32(input.char_box.x), f32(input.char_box.y)) * uniforms.vp_scale - vec2<f32>(1.0);
    let box_zw = vec2<f32>(f32(input.char_box.z), f32(input.char_box.w)) * uniforms.vp_scale - vec2<f32>(1.0);
    var pos: vec2<f32>;
    var tc: vec2<f32>;
    let w = f32(cell_size.x);
    let h = f32(cell_size.y);
    switch corner {
        case 0u: { pos = vec2<f32>(box_xy.x, box_xy.y); tc = vec2<f32>(0.0, h); }
        case 1u: { pos = vec2<f32>(box_zw.x, box_xy.y); tc = vec2<f32>(w, h); }
        case 2u: { pos = vec2<f32>(box_xy.x, box_zw.y); tc = vec2<f32>(0.0, 0.0); }
        case 3u: { pos = vec2<f32>(box_zw.x, box_zw.y); tc = vec2<f32>(w, 0.0); }
        default: { pos = vec2<f32>(0.0); tc = vec2<f32>(0.0); }
    }
    var out: VertexOutput;
    out.position = vec4<f32>(pos, 0.0, 1.0);
    out.cell_size = cell_size;
    out.tex_offset = input.tex_offset;
    out.tex_coord = tc;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let x = i32(in.tex_coord.x);
    let y = i32(in.tex_coord.y);
    let offset = y * in.cell_size.x + x + in.tex_offset;
    let st = vec2<i32>(offset % 8192, offset / 8192);
    let r = textureLoad(font_texture, st, 0).r;
    if r < 0.001 {
        discard;
    }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

const TEXT2D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    _pad: vec2<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var font_texture: texture_2d<f32>;
@group(0) @binding(2) var font_sampler: sampler;

struct VertexInput {
    @location(0) pos: vec2<f32>,
    @location(1) char_box: vec4<i32>,
    @location(2) tex_offset: i32,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) cell_size: vec2<i32>,
    @location(1) @interpolate(flat) tex_offset: i32,
    @location(2) tex_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let cell_size = vec2<i32>(input.char_box.z - input.char_box.x,
                              input.char_box.w - input.char_box.y);
    let clip = uniforms.xfm * vec4<f32>(input.pos, 0.0, 1.0);
    var xyref = floor(clip.xy / uniforms.vp_scale);
    xyref = xyref + vec2<f32>(0.01, 0.01);
    let xy0 = xyref + vec2<f32>(f32(input.char_box.x), f32(input.char_box.y));
    let xy1 = xyref + vec2<f32>(f32(input.char_box.z), f32(input.char_box.w));
    let clip0 = xy0 * uniforms.vp_scale;
    let clip1 = xy1 * uniforms.vp_scale;
    let w = f32(cell_size.x);
    let h = f32(cell_size.y);
    var pos: vec2<f32>;
    var tc: vec2<f32>;
    switch corner {
        case 0u: { pos = vec2<f32>(clip0.x, clip0.y); tc = vec2<f32>(0.0, h); }
        case 1u: { pos = vec2<f32>(clip1.x, clip0.y); tc = vec2<f32>(w, h); }
        case 2u: { pos = vec2<f32>(clip0.x, clip1.y); tc = vec2<f32>(0.0, 0.0); }
        case 3u: { pos = vec2<f32>(clip1.x, clip1.y); tc = vec2<f32>(w, 0.0); }
        default: { pos = vec2<f32>(0.0); tc = vec2<f32>(0.0); }
    }
    var out: VertexOutput;
    out.position = vec4<f32>(pos, 0.0, 1.0);
    out.cell_size = cell_size;
    out.tex_offset = input.tex_offset;
    out.tex_coord = tc;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let x = i32(in.tex_coord.x);
    let y = i32(in.tex_coord.y);
    let offset = y * in.cell_size.x + x + in.tex_offset;
    let st = vec2<i32>(offset % 8192, offset / 8192);
    let r = textureLoad(font_texture, st, 0).r;
    if r < 0.001 {
        discard;
    }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

const TEXT3D = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    _pad: vec2<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var font_texture: texture_2d<f32>;
@group(0) @binding(2) var font_sampler: sampler;

struct VertexInput {
    @location(0) pos: vec3<f32>,
    @location(1) char_box: vec4<i32>,
    @location(2) tex_offset: i32,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) cell_size: vec2<i32>,
    @location(1) @interpolate(flat) tex_offset: i32,
    @location(2) tex_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let cell_size = vec2<i32>(input.char_box.z - input.char_box.x,
                              input.char_box.w - input.char_box.y);
    let clip = uniforms.xfm * vec4<f32>(input.pos, 1.0);
    let clip_z = clip.z;
    var xyref = floor(clip.xy / uniforms.vp_scale);
    xyref = xyref + vec2<f32>(0.01, 0.01);
    let xy0 = xyref + vec2<f32>(f32(input.char_box.x), f32(input.char_box.y));
    let xy1 = xyref + vec2<f32>(f32(input.char_box.z), f32(input.char_box.w));
    let clip0 = xy0 * uniforms.vp_scale;
    let clip1 = xy1 * uniforms.vp_scale;
    let w = f32(cell_size.x);
    let h = f32(cell_size.y);
    var pos: vec2<f32>;
    var tc: vec2<f32>;
    switch corner {
        case 0u: { pos = vec2<f32>(clip0.x, clip0.y); tc = vec2<f32>(0.0, h); }
        case 1u: { pos = vec2<f32>(clip1.x, clip0.y); tc = vec2<f32>(w, h); }
        case 2u: { pos = vec2<f32>(clip0.x, clip1.y); tc = vec2<f32>(0.0, 0.0); }
        case 3u: { pos = vec2<f32>(clip1.x, clip1.y); tc = vec2<f32>(w, 0.0); }
        default: { pos = vec2<f32>(0.0); tc = vec2<f32>(0.0); }
    }
    var out: VertexOutput;
    out.position = vec4<f32>(pos, clip_z, 1.0);
    out.cell_size = cell_size;
    out.tex_offset = input.tex_offset;
    out.tex_coord = tc;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let x = i32(in.tex_coord.x);
    let y = i32(in.tex_coord.y);
    let offset = y * in.cell_size.x + x + in.tex_offset;
    let st = vec2<i32>(offset % 8192, offset / 8192);
    let r = textureLoad(font_texture, st, 0).r;
    if r < 0.001 {
        discard;
    }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

// ---------------------------------------------------------------------------
// CAD pipeline shaders: G-Buffer, Gooch shading, Edge composite
// ---------------------------------------------------------------------------

const GBUFFER_NORMAL = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
    @location(1) depth: f32,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    // Transform normal to view space using upper-left 3x3 of normal_xfm
    out.normal = normalize(mat3x3<f32>(
        uniforms.normal_xfm[0].xyz,
        uniforms.normal_xfm[1].xyz,
        uniforms.normal_xfm[2].xyz
    ) * input.normal);
    out.depth = out.position.z; // clip-space Z, already [0,1] in WebGPU
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let n = normalize(in.normal);
    return vec4<f32>(n * 0.5 + 0.5, in.depth);
}
`;

const CAD_GOOCH = /* wgsl */`
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

// Gooch bias colors (added to scaled object color)
const COOL_BIAS = vec3<f32>(0.02, 0.02, 0.08);  // subtle blue in shadows
const WARM_BIAS = vec3<f32>(0.06, 0.03, 0.0);   // subtle warm in highlights
const AMBIENT = vec3<f32>(0.08, 0.08, 0.08);    // ambient floor

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.normal = input.normal;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let n = normalize(in.normal);
    let NdotL = abs(dot(light_dir, n)); // two-sided lighting (matches Phong)
    let t = NdotL;

    // Gooch: object color preserved, with subtle cool/warm bias
    let k_cool = COOL_BIAS + 0.45 * uniforms.draw_color.rgb;
    let k_warm = WARM_BIAS + 0.85 * uniforms.draw_color.rgb;
    let color = mix(k_cool, k_warm, t) + AMBIENT;
    return vec4<f32>(color, uniforms.draw_color.a);
}
`;

const EDGE_COMPOSITE = /* wgsl */`
struct PostUniforms {
    texel_size: vec2<f32>,
    edge_threshold_normal: f32,
    edge_threshold_depth: f32,
};

@group(0) @binding(0) var<uniform> uniforms: PostUniforms;
@group(0) @binding(1) var gbuf_texture: texture_2d<f32>;
@group(0) @binding(2) var gbuf_sampler: sampler;

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) uv: vec2<f32>,
};

@vertex
fn vs_main(@builtin(vertex_index) vid: u32) -> VertexOutput {
    // Full-screen triangle: 3 vertices cover entire screen
    let uv = vec2<f32>(f32((vid << 1u) & 2u), f32(vid & 2u));
    var out: VertexOutput;
    out.position = vec4<f32>(uv * 2.0 - 1.0, 0.0, 1.0);
    out.uv = vec2<f32>(uv.x, 1.0 - uv.y); // flip Y for texture coords
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let ts = uniforms.texel_size;

    // Sample center and 3x3 neighborhood (all in uniform control flow)
    let center = textureSample(gbuf_texture, gbuf_sampler, in.uv);
    let tl = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>(-ts.x,  ts.y));
    let tc = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>( 0.0,   ts.y));
    let tr = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>( ts.x,  ts.y));
    let ml = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>(-ts.x,  0.0));
    let mr = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>( ts.x,  0.0));
    let bl = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>(-ts.x, -ts.y));
    let bc = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>( 0.0,  -ts.y));
    let br = textureSample(gbuf_texture, gbuf_sampler, in.uv + vec2<f32>( ts.x, -ts.y));

    // Background mask: if center pixel is background (depth ≈ 1.0), no edge
    let is_geom = select(0.0, 1.0, center.a < 0.99);

    // Sobel on normals (rgb)
    let sx_n = -tl.rgb - 2.0 * ml.rgb - bl.rgb + tr.rgb + 2.0 * mr.rgb + br.rgb;
    let sy_n = -tl.rgb - 2.0 * tc.rgb - tr.rgb + bl.rgb + 2.0 * bc.rgb + br.rgb;
    let normal_edge = length(sx_n) + length(sy_n);

    // Sobel on depth (alpha channel) — clamp background neighbors to center depth
    // to avoid false silhouette edges from geometry-to-background transitions
    let cd = center.a;
    let d_tl = select(tl.a, cd, tl.a >= 0.99);
    let d_tc = select(tc.a, cd, tc.a >= 0.99);
    let d_tr = select(tr.a, cd, tr.a >= 0.99);
    let d_ml = select(ml.a, cd, ml.a >= 0.99);
    let d_mr = select(mr.a, cd, mr.a >= 0.99);
    let d_bl = select(bl.a, cd, bl.a >= 0.99);
    let d_bc = select(bc.a, cd, bc.a >= 0.99);
    let d_br = select(br.a, cd, br.a >= 0.99);

    let sx_d = -d_tl - 2.0 * d_ml - d_bl + d_tr + 2.0 * d_mr + d_br;
    let sy_d = -d_tl - 2.0 * d_tc - d_tr + d_bl + 2.0 * d_bc + d_br;
    let depth_edge = abs(sx_d) + abs(sy_d);

    // Combine edge strengths with thresholds, masked by geometry
    // Use tight smoothstep for thin, crisp edges (narrow transition band)
    let n_strength = smoothstep(uniforms.edge_threshold_normal * 0.85, uniforms.edge_threshold_normal, normal_edge);
    let d_strength = smoothstep(uniforms.edge_threshold_depth * 0.85, uniforms.edge_threshold_depth, depth_edge);
    let alpha = max(n_strength, d_strength) * is_geom;

    return vec4<f32>(0.0, 0.0, 0.0, alpha); // black edges, blended over scene
}
`;
