// ────── ╔╗                                                                                GPU.WEB
// ╔═╦╦═╦╦╬╣ nori-gpu.js
// ║║║║╬║╔╣║ Browser WebGPU implementation — decodes command buffers from C# WASM interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────

// Command opcodes (must match WebGPU.cs constants)
const OP_SET_VIEWPORT = 1;
const OP_CLEAR = 2;
const OP_PRESENT = 3;
const OP_CREATE_BUFFER = 4;
const OP_UPLOAD_BUFFER = 5;
const OP_DELETE_BUFFER = 6;
const OP_SET_PIPELINE = 7;
const OP_SET_VERTEX_BUFFER = 8;
const OP_SET_INDEX_BUFFER = 9;
const OP_SET_BIND_GROUP = 10;
const OP_DRAW = 11;
const OP_DRAW_INDEXED = 12;
const OP_CREATE_TEXTURE = 13;
const OP_BIND_TEXTURE = 14;
const OP_DELETE_TEXTURE = 15;
const OP_CREATE_FB = 16;
const OP_BIND_FB = 17;
const OP_BIND_DEFAULT_FB = 18;
const OP_READ_PIXELS = 19;
const OP_DELETE_FB = 20;

// --- WGSL shader sources ----------------------------------------------------

const SHADER_FLAT2D = `
// Flat2D.wgsl — Simple 2D flat-colored rendering (triangles / quads / tri-fans)
struct Uniforms {
    xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
struct VertexOutput { @builtin(position) position: vec4<f32>, };
@vertex
fn vs_main(@location(0) pos: vec2<f32>) -> VertexOutput {
    var out: VertexOutput;
    out.position = uniforms.xfm * vec4<f32>(pos, 0.0, 1.0);
    return out;
}
@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> { return uniforms.draw_color; }
`;

const SHADER_LINE2D = `
// Line2D.wgsl — 2D anti-aliased line rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 0.0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 0.0, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale; let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.0625;
    let is_end = (corner & 1u) != 0u; let is_neg = (corner & 2u) != 0u;
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

const SHADER_LINE3D = `
// Line3D.wgsl — 3D anti-aliased line rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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

const SHADER_BEZIER2D = `
// Bezier2D.wgsl — Pre-tessellated Bezier curve rendering (same as Line2D)
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let clip0 = uniforms.xfm * vec4<f32>(input.p0, 0.0, 1.0);
    let clip1 = uniforms.xfm * vec4<f32>(input.p1, 0.0, 1.0);
    let inv_scale = 1.0 / uniforms.vp_scale;
    let px0 = clip0.xy * inv_scale; let px1 = clip1.xy * inv_scale;
    let width = uniforms.line_width / 2.0;
    var dir = normalize(px1 - px0) * width;
    let perp = vec2<f32>(dir.y, -dir.x);
    dir *= 0.0625;
    let is_end = (corner & 1u) != 0u; let is_neg = (corner & 2u) != 0u;
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

const SHADER_DASHLINE2D = `
// DashLine2D.wgsl — 2D dashed/dotted line rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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

const SHADER_POINT2D = `
// Point2D.wgsl — 2D point rendering via instanced quads
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    point_size: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
struct VertexInput { @location(0) pos: vec2<f32>, };
struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) st_coord: vec2<f32>,
};
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
const CORNER_SIGNS = array<vec2<f32>, 4>(
    vec2<f32>(1.0, 1.0), vec2<f32>(1.0, -1.0),
    vec2<f32>(-1.0, 1.0), vec2<f32>(-1.0, -1.0),
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

const SHADER_POINT3D = `
// Point3D.wgsl — 3D point rendering via instanced quads
struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    point_size: f32,
    _pad0: f32,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
struct VertexInput { @location(0) pos: vec3<f32>, };
struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) st_coord: vec2<f32>,
};
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
const CORNER_SIGNS = array<vec2<f32>, 4>(
    vec2<f32>(1.0, 1.0), vec2<f32>(1.0, -1.0),
    vec2<f32>(-1.0, 1.0), vec2<f32>(-1.0, -1.0),
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

const SHADER_GLASSLINE = `
// GlassLine.wgsl — 3D stippled line rendering (checkerboard transparency)
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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

const SHADER_GOURAD = `
// Gourad.wgsl — Gouraud shading with Lambert diffuse (per-vertex lighting)
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
const LIGHT_DIR = vec3<f32>(0.0, 0.0, 1.0);
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
    let tnorm = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 0.0)).xyz);
    let diffuse = abs(dot(LIGHT_DIR, tnorm));
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

const SHADER_PHONG = `
// Phong.wgsl — Lambert diffuse shading (per-fragment lighting, normal interpolation)
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
const LIGHT_DIR = vec3<f32>(0.0, 0.0, 1.0);
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
    out.normal = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 0.0)).xyz);
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}
@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let tnorm = normalize(in.normal);
    let diffuse = abs(dot(LIGHT_DIR, tnorm));
    let light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    return vec4<f32>(light_intensity.rgb, uniforms.draw_color.a);
}
`;

const SHADER_PHONGPINK = `
// PhongPink.wgsl — Lambert diffuse with back-face coloring in pink (debugging)
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
const LIGHT_DIR = vec3<f32>(0.0, 0.0, 1.0);
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
    out.normal = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 0.0)).xyz);
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}
@fragment
fn fs_main(in: FragInput) -> @location(0) vec4<f32> {
    let tnorm = normalize(in.normal);
    let diffuse = abs(dot(LIGHT_DIR, tnorm));
    let color = select(PINK, uniforms.draw_color, in.front_facing);
    let light_intensity = color * diffuse + AMBIENT_COLOR;
    return vec4<f32>(light_intensity.rgb, uniforms.draw_color.a);
}
`;

const SHADER_PICK = `
// Pick.wgsl — Picking shader (renders object IDs as colors for hit testing)
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
struct VertexOutput { @builtin(position) position: vec4<f32>, };
@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}
@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> { return uniforms.draw_color; }
`;

const SHADER_GLASS = `
// Glass.wgsl — Lambert diffuse with checkerboard stipple (translucency)
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
const LIGHT_DIR = vec3<f32>(0.0, 0.0, 1.0);
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
    let tnorm = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 0.0)).xyz);
    let diffuse = abs(dot(LIGHT_DIR, tnorm));
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

const SHADER_FLATFACET = `
// FlatFacet.wgsl — Flat-faceted Lambert shading (per-face lighting, no interpolation)
struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};
@group(0) @binding(0) var<uniform> uniforms: Uniforms;
const LIGHT_DIR = vec3<f32>(0.0, 0.0, 1.0);
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
    let tnorm = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 0.0)).xyz);
    let diffuse = abs(dot(LIGHT_DIR, tnorm));
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

const SHADER_TEXTPX = `
// TextPx.wgsl — Pixel-space text rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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
    if r < 0.001 { discard; }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

const SHADER_TEXT2D = `
// Text2D.wgsl — World-space 2D text rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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
    if r < 0.001 { discard; }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

const SHADER_TEXT3D = `
// Text3D.wgsl — World-space 3D text rendering via instanced quads
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
const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);
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
    if r < 0.001 { discard; }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
`;

// --- Vertex layout definitions -----------------------------------------------

const LAYOUT_INSTANCE2D_LINE = {
   arrayStride: 16,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x2" },
      { shaderLocation: 1, offset: 8, format: "float32x2" },
   ],
};

const LAYOUT_INSTANCE3D_LINE = {
   arrayStride: 24,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x3" },
      { shaderLocation: 1, offset: 12, format: "float32x3" },
   ],
};

const LAYOUT_INSTANCE2D_POINT = {
   arrayStride: 8,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x2" },
   ],
};

const LAYOUT_INSTANCE3D_POINT = {
   arrayStride: 12,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x3" },
   ],
};

const LAYOUT_VERTEX2D = {
   arrayStride: 8,
   stepMode: "vertex",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x2" },
   ],
};

const LAYOUT_VERTEX3D_FACET = {
   arrayStride: 20,
   stepMode: "vertex",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x3" },
      { shaderLocation: 1, offset: 12, format: "float16x4" },
   ],
};

const LAYOUT_INSTANCE_BLACKLINE = {
   arrayStride: 40,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x3" },
      { shaderLocation: 1, offset: 20, format: "float32x3" },
   ],
};

const LAYOUT_INSTANCE_TEXTPX = {
   arrayStride: 12,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "sint16x4" },
      { shaderLocation: 1, offset: 8, format: "sint32" },
   ],
};

const LAYOUT_INSTANCE_TEXT2D = {
   arrayStride: 20,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x2" },
      { shaderLocation: 1, offset: 8, format: "sint16x4" },
      { shaderLocation: 2, offset: 16, format: "sint32" },
   ],
};

const LAYOUT_INSTANCE_TEXT3D = {
   arrayStride: 24,
   stepMode: "instance",
   attributes: [
      { shaderLocation: 0, offset: 0, format: "float32x3" },
      { shaderLocation: 1, offset: 12, format: "sint16x4" },
      { shaderLocation: 2, offset: 20, format: "sint32" },
   ],
};

// --- Blend state constants ---------------------------------------------------

const BLEND_ALPHA = {
   color: { srcFactor: "src-alpha", dstFactor: "one-minus-src-alpha", operation: "add" },
   alpha: { srcFactor: "one", dstFactor: "one-minus-src-alpha", operation: "add" },
};

// --- Depth/stencil format constant -------------------------------------------

const DEPTH_STENCIL_FORMAT = "depth24plus-stencil8";

// =============================================================================

export const noriGpu = {
   // --- State ---------------------------------------------------------------
   device: null,
   context: null,
   canvasFormat: null,
   queue: null,
   canvas: null,

   // Resource maps keyed by integer handle
   buffers: new Map (),
   textures: new Map (),
   textureViews: new Map (),
   samplers: new Map (),
   framebuffers: new Map (),
   pipelines: [],
   _pipelineInfo: [],

   // Current render state
   currentEncoder: null,
   currentPass: null,
   currentFB: null,
   currentPipeline: null,
   currentPipelineIndex: -1,
   boundTextures: new Map (),
   viewport: { x: 0, y: 0, w: 0, h: 0 },

   // Uniform buffer pool and bind group layouts
   _uniformBuffers: new Map (),
   _uniformOnlyLayout: null,
   _texturedLayout: null,

   // Default framebuffer depth/stencil
   _defaultDepth: null,
   _defaultDepthView: null,
   _defaultDepthW: 0,
   _defaultDepthH: 0,

   // --- Initialization ------------------------------------------------------

   async init (canvasId) {
      if (!navigator.gpu)
         throw new Error ("WebGPU is not supported in this browser");

      const adapter = await navigator.gpu.requestAdapter ({
         powerPreference: "high-performance"
      });
      if (!adapter)
         throw new Error ("Failed to get WebGPU adapter");

      this.device = await adapter.requestDevice ();
      this.queue = this.device.queue;

      this.canvas = document.getElementById (canvasId);
      if (!this.canvas)
         throw new Error (`Canvas element '${canvasId}' not found`);

      this.context = this.canvas.getContext ("webgpu");
      this.canvasFormat = navigator.gpu.getPreferredCanvasFormat ();
      this.context.configure ({
         device: this.device,
         format: this.canvasFormat,
         alphaMode: "premultiplied"
      });

      // Create bind group layouts
      this._uniformOnlyLayout = this.device.createBindGroupLayout ({
         label: "uniform-only-layout",
         entries: [
            { binding: 0, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
              buffer: { type: "uniform" } },
         ],
      });

      this._texturedLayout = this.device.createBindGroupLayout ({
         label: "textured-layout",
         entries: [
            { binding: 0, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
              buffer: { type: "uniform" } },
            { binding: 1, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
              texture: { sampleType: "float" } },
            { binding: 2, visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
              sampler: { type: "filtering" } },
         ],
      });

      // Create a 1x1 white dummy texture + sampler for textured pipelines
      // when no real texture is bound yet
      this._dummyTex = this.device.createTexture ({
         size: { width: 1, height: 1 },
         format: "rgba8unorm",
         usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
         label: "dummy-1x1"
      });
      this.queue.writeTexture (
         { texture: this._dummyTex },
         new Uint8Array ([255, 255, 255, 255]),
         { bytesPerRow: 4 },
         { width: 1, height: 1 }
      );
      this._dummyView = this._dummyTex.createView ();
      this._dummySampler = this.device.createSampler ({ label: "dummy-sampler" });

      // Create initial depth-stencil for default framebuffer
      this._ensureDepthStencil (this.canvas.width, this.canvas.height);

      // Create all render pipelines
      this._initPipelines ();
   },

   // --- Command buffer execution --------------------------------------------

   executeCommands (commands, length) {
      const view = new DataView (commands.buffer, commands.byteOffset, length);
      let pos = 0;

      while (pos < length) {
         const op = commands[pos++];
         switch (op) {
            case OP_SET_VIEWPORT: {
               const x = view.getInt32 (pos, true); pos += 4;
               const y = view.getInt32 (pos, true); pos += 4;
               const w = view.getInt32 (pos, true); pos += 4;
               const h = view.getInt32 (pos, true); pos += 4;
               this.viewport = { x, y, w, h };
               if (this.currentPass)
                  this.currentPass.setViewport (x, y, w, h, 0.0, 1.0);
               break;
            }
            case OP_CLEAR: {
               const r = view.getFloat32 (pos, true); pos += 4;
               const g = view.getFloat32 (pos, true); pos += 4;
               const b = view.getFloat32 (pos, true); pos += 4;
               const a = view.getFloat32 (pos, true); pos += 4;
               this._beginRenderPass (r, g, b, a, "clear");
               break;
            }
            case OP_PRESENT: {
               this._endRenderPass ();
               this._submitCommands ();
               break;
            }
            case OP_CREATE_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const size = view.getInt32 (pos, true); pos += 4;
               const isIndex = commands[pos++] !== 0;
               this._createBuffer (handle, size, isIndex);
               break;
            }
            case OP_DELETE_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteBuffer (handle);
               break;
            }
            case OP_SET_PIPELINE: {
               const index = view.getInt32 (pos, true); pos += 4;
               this._setPipeline (index);
               break;
            }
            case OP_SET_VERTEX_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const offset = view.getInt32 (pos, true); pos += 4;
               this._setVertexBuffer (handle, offset);
               break;
            }
            case OP_SET_INDEX_BUFFER: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const offset = view.getInt32 (pos, true); pos += 4;
               this._setIndexBuffer (handle, offset);
               break;
            }
            case OP_DRAW: {
               const vertexCount = view.getInt32 (pos, true); pos += 4;
               const instanceCount = view.getInt32 (pos, true); pos += 4;
               const firstVertex = view.getInt32 (pos, true); pos += 4;
               this._draw (vertexCount, instanceCount, firstVertex);
               break;
            }
            case OP_DRAW_INDEXED: {
               const indexCount = view.getInt32 (pos, true); pos += 4;
               const instanceCount = view.getInt32 (pos, true); pos += 4;
               const firstIndex = view.getInt32 (pos, true); pos += 4;
               const baseVertex = view.getInt32 (pos, true); pos += 4;
               this._drawIndexed (indexCount, instanceCount, firstIndex, baseVertex);
               break;
            }
            case OP_BIND_TEXTURE: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const slot = view.getInt32 (pos, true); pos += 4;
               this._bindTexture (handle, slot);
               break;
            }
            case OP_DELETE_TEXTURE: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteTexture (handle);
               break;
            }
            case OP_CREATE_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               const w = view.getInt32 (pos, true); pos += 4;
               const h = view.getInt32 (pos, true); pos += 4;
               this._createFramebuffer (handle, w, h);
               break;
            }
            case OP_BIND_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._bindFramebuffer (handle);
               break;
            }
            case OP_BIND_DEFAULT_FB: {
               this._bindDefaultFramebuffer ();
               break;
            }
            case OP_DELETE_FB: {
               const handle = view.getInt32 (pos, true); pos += 4;
               this._deleteFramebuffer (handle);
               break;
            }
            default:
               console.error (`noriGpu: unknown opcode ${op} at position ${pos - 1}`);
               return;
         }
      }
   },

   // --- Large-payload interop calls -----------------------------------------

   uploadBuffer (handle, data, size) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu.uploadBuffer: unknown handle ${handle}`);
         return;
      }
      this.queue.writeBuffer (buffer, 0, data, 0, size);
   },

   setBindGroup (group, data, size) {
      // Create or reuse a uniform buffer for this bind group slot
      const key = `bindgroup_${group}`;
      let buf = this._uniformBuffers.get (key);
      if (!buf || buf.size < size) {
         if (buf) buf.destroy ();
         buf = this.device.createBuffer ({
            size: Math.max (size, 256),
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            label: `uniform-group-${group}`
         });
         this._uniformBuffers.set (key, buf);
      }
      this.queue.writeBuffer (buf, 0, data, 0, size);

      // Create the bind group using the appropriate layout for the current pipeline
      if (this.currentPipelineIndex < 0 || !this.currentPass) return;
      const info = this._pipelineInfo[this.currentPipelineIndex];
      if (!info) return;

      let bindGroup;
      if (info.textured) {
         // Textured pipeline: include texture view and sampler if bound
         const texHandle = this.boundTextures.get (0);
         const texView = texHandle !== undefined ? this.textureViews.get (texHandle) : null;
         const sampler = texHandle !== undefined ? this.samplers.get (texHandle) : null;
         if (texView && sampler) {
            bindGroup = this.device.createBindGroup ({
               layout: this._texturedLayout,
               entries: [
                  { binding: 0, resource: { buffer: buf } },
                  { binding: 1, resource: texView },
                  { binding: 2, resource: sampler },
               ],
               label: `bindgroup-textured-${group}`
            });
         } else {
            // Texture not yet bound; use dummy 1x1 white texture
            bindGroup = this.device.createBindGroup ({
               layout: this._texturedLayout,
               entries: [
                  { binding: 0, resource: { buffer: buf } },
                  { binding: 1, resource: this._dummyView },
                  { binding: 2, resource: this._dummySampler },
               ],
               label: `bindgroup-textured-dummy-${group}`
            });
         }
      } else {
         bindGroup = this.device.createBindGroup ({
            layout: this._uniformOnlyLayout,
            entries: [
               { binding: 0, resource: { buffer: buf } },
            ],
            label: `bindgroup-uniform-${group}`
         });
      }

      this.currentPass.setBindGroup (group, bindGroup);
   },

   createTexture (handle, width, height, data, size) {
      const texture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: "rgba8unorm",
         usage: GPUTextureUsage.TEXTURE_BINDING |
                GPUTextureUsage.COPY_DST |
                GPUTextureUsage.RENDER_ATTACHMENT,
         label: `texture-${handle}`
      });
      this.textures.set (handle, texture);
      this.textureViews.set (handle, texture.createView ());

      // Upload pixel data
      this.queue.writeTexture (
         { texture },
         data,
         { bytesPerRow: width * 4, rowsPerImage: height },
         { width, height, depthOrArrayLayers: 1 }
      );

      // Create a default sampler for this texture
      const sampler = this.device.createSampler ({
         magFilter: "linear",
         minFilter: "linear",
         mipmapFilter: "linear",
         addressModeU: "clamp-to-edge",
         addressModeV: "clamp-to-edge"
      });
      this.samplers.set (handle, sampler);
   },

   readPixels (x, y, width, height) {
      // Synchronous readback is not possible in WebGPU without SharedArrayBuffer.
      // Full implementation would:
      // 1. Create a staging buffer with MAP_READ | COPY_DST usage
      // 2. Use commandEncoder.copyTextureToBuffer to copy pixels
      // 3. Submit and map the staging buffer
      // 4. Block on Atomics.wait until mapAsync resolves
      // 5. Copy the mapped data and return it
      const byteLength = width * height * 4;
      return new Uint8Array (byteLength);
   },

   // --- Internal: pipeline initialization ------------------------------------

   _initPipelines () {
      this.pipelines = [];
      this._pipelineInfo = [];

      // EPipeline enum:
      //  0=Line2D,  1=Line3D,  2=Bezier2D,  3=DashLine2D,  4=Point2D,
      //  5=Point3D, 6=Triangle2D, 7=Quad2D,  8=BlackLine,   9=GlassLine,
      // 10=Gourad, 11=Phong,  12=PhongPink, 13=Pick, 14=Glass, 15=FlatFacet,
      // 16=TextPx, 17=Text2D, 18=Text3D,
      // 19=TriFanStencil, 20=TriFanCover

      // 0: Line2D — Instance2DLine, blend, no depth, uniform-only
      this._addPipeline (SHADER_LINE2D, LAYOUT_INSTANCE2D_LINE,
         { blend: true, depth: false, textured: false, label: "Line2D" });

      // 1: Line3D — Instance3DLine, blend, depth, uniform-only
      this._addPipeline (SHADER_LINE3D, LAYOUT_INSTANCE3D_LINE,
         { blend: true, depth: true, textured: false, label: "Line3D" });

      // 2: Bezier2D — Instance2DLine, blend, no depth, uniform-only
      this._addPipeline (SHADER_BEZIER2D, LAYOUT_INSTANCE2D_LINE,
         { blend: true, depth: false, textured: false, label: "Bezier2D" });

      // 3: DashLine2D — Instance2DLine, blend, no depth, textured
      this._addPipeline (SHADER_DASHLINE2D, LAYOUT_INSTANCE2D_LINE,
         { blend: true, depth: false, textured: true, label: "DashLine2D" });

      // 4: Point2D — Instance2DPoint, blend, no depth, uniform-only
      this._addPipeline (SHADER_POINT2D, LAYOUT_INSTANCE2D_POINT,
         { blend: true, depth: false, textured: false, label: "Point2D" });

      // 5: Point3D — Instance3DPoint, blend, no depth, uniform-only
      this._addPipeline (SHADER_POINT3D, LAYOUT_INSTANCE3D_POINT,
         { blend: true, depth: false, textured: false, label: "Point3D" });

      // 6: Triangle2D — Vertex2D, no blend, no depth, uniform-only
      this._addPipeline (SHADER_FLAT2D, LAYOUT_VERTEX2D,
         { blend: false, depth: false, textured: false, label: "Triangle2D" });

      // 7: Quad2D — Vertex2D, no blend, no depth, uniform-only
      this._addPipeline (SHADER_FLAT2D, LAYOUT_VERTEX2D,
         { blend: false, depth: false, textured: false, label: "Quad2D" });

      // 8: BlackLine — Vec3F_Vec3H instanced lines, blend, depth, uniform-only
      this._addPipeline (SHADER_LINE3D, LAYOUT_INSTANCE_BLACKLINE,
         { blend: true, depth: true, textured: false, label: "BlackLine" });

      // 9: GlassLine — Vec3F_Vec3H instanced lines, blend, depth, uniform-only
      this._addPipeline (SHADER_GLASSLINE, LAYOUT_INSTANCE_BLACKLINE,
         { blend: true, depth: true, textured: false, label: "GlassLine" });

      // 10: Gourad — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_GOURAD, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "Gourad" });

      // 11: Phong — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_PHONG, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "Phong" });

      // 12: PhongPink — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_PHONGPINK, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "PhongPink" });

      // 13: Pick — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_PICK, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "Pick" });

      // 14: Glass — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_GLASS, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "Glass" });

      // 15: FlatFacet — Vertex3DFacet, no blend, depth, polyOffset, uniform-only
      this._addPipeline (SHADER_FLATFACET, LAYOUT_VERTEX3D_FACET,
         { blend: false, depth: true, textured: false, polyOffset: true, label: "FlatFacet" });

      // 16: TextPx — InstanceTextPx, blend, no depth, textured
      this._addPipeline (SHADER_TEXTPX, LAYOUT_INSTANCE_TEXTPX,
         { blend: true, depth: false, textured: true, label: "TextPx" });

      // 17: Text2D — InstanceText2D, blend, no depth, textured
      this._addPipeline (SHADER_TEXT2D, LAYOUT_INSTANCE_TEXT2D,
         { blend: true, depth: false, textured: true, label: "Text2D" });

      // 18: Text3D — InstanceText3D, blend, depth, textured
      this._addPipeline (SHADER_TEXT3D, LAYOUT_INSTANCE_TEXT3D,
         { blend: true, depth: true, textured: true, label: "Text3D" });

      // 19: TriFanStencil — Vertex2D, no blend, no depth, stencilWrite
      this._addPipeline (SHADER_FLAT2D, LAYOUT_VERTEX2D,
         { blend: false, depth: false, textured: false, stencil: "write", label: "TriFanStencil" });

      // 20: TriFanCover — Vertex2D, no blend, no depth, stencilTest
      this._addPipeline (SHADER_FLAT2D, LAYOUT_VERTEX2D,
         { blend: false, depth: false, textured: false, stencil: "test", label: "TriFanCover" });
   },

   _addPipeline (shaderSource, vertexLayout, opts) {
      const shaderModule = this.device.createShaderModule ({
         code: shaderSource,
         label: `shader-${opts.label}`
      });

      const bgLayout = opts.textured ? this._texturedLayout : this._uniformOnlyLayout;
      const pipelineLayout = this.device.createPipelineLayout ({
         bindGroupLayouts: [bgLayout],
         label: `layout-${opts.label}`
      });

      // Color target
      const colorTarget = {
         format: this.canvasFormat,
      };
      if (opts.blend) {
         colorTarget.blend = BLEND_ALPHA;
      }
      // For stencil-write (TriFanStencil), disable color writes
      if (opts.stencil === "write") {
         colorTarget.writeMask = 0;
      }

      const descriptor = {
         label: opts.label,
         layout: pipelineLayout,
         vertex: {
            module: shaderModule,
            entryPoint: "vs_main",
            buffers: [vertexLayout],
         },
         fragment: {
            module: shaderModule,
            entryPoint: "fs_main",
            targets: [colorTarget],
         },
         primitive: {
            topology: "triangle-list",
            cullMode: "none",
         },
      };

      // Depth/stencil state — always required because the render pass always
      // has a depth-stencil attachment; pipelines must match the pass layout.
      {
         const depthStencil = {
            format: DEPTH_STENCIL_FORMAT,
         };

         if (opts.depth) {
            depthStencil.depthCompare = "less-equal";
            depthStencil.depthWriteEnabled = true;
         } else {
            depthStencil.depthCompare = "always";
            depthStencil.depthWriteEnabled = false;
         }

         if (opts.polyOffset) {
            depthStencil.depthBias = 1;
            depthStencil.depthBiasSlopeScale = 1;
         }

         if (opts.stencil === "write") {
            // TriFanStencil: invert stencil on pass, no color write
            const stencilState = {
               compare: "always",
               passOp: "invert",
               failOp: "keep",
               depthFailOp: "keep",
            };
            depthStencil.stencilFront = stencilState;
            depthStencil.stencilBack = stencilState;
            depthStencil.stencilReadMask = 0xFF;
            depthStencil.stencilWriteMask = 0xFF;
         } else if (opts.stencil === "test") {
            // TriFanCover: test stencil != 0, zero on pass
            const stencilState = {
               compare: "not-equal",
               passOp: "zero",
               failOp: "keep",
               depthFailOp: "keep",
            };
            depthStencil.stencilFront = stencilState;
            depthStencil.stencilBack = stencilState;
            depthStencil.stencilReadMask = 0xFF;
            depthStencil.stencilWriteMask = 0xFF;
         }

         descriptor.depthStencil = depthStencil;
      }

      const pipeline = this.device.createRenderPipeline (descriptor);
      this.pipelines.push (pipeline);
      this._pipelineInfo.push ({
         textured: !!opts.textured,
         hasDepth: !!opts.depth,
         hasStencil: !!opts.stencil,
         label: opts.label,
      });
   },

   // --- Internal: buffer management ------------------------------------------

   _createBuffer (handle, size, isIndex) {
      const usage = isIndex
         ? (GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST)
         : (GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST);
      const buffer = this.device.createBuffer ({
         size: Math.max (size, 4),
         usage,
         label: `buffer-${handle}`
      });
      this.buffers.set (handle, buffer);
   },

   _deleteBuffer (handle) {
      const buffer = this.buffers.get (handle);
      if (buffer) {
         buffer.destroy ();
         this.buffers.delete (handle);
      }
   },

   // --- Internal: pipeline and draw ------------------------------------------

   _setPipeline (index) {
      if (index < 0 || index >= this.pipelines.length) {
         console.error (`noriGpu: pipeline index ${index} out of range`);
         return;
      }
      this.currentPipeline = this.pipelines[index];
      this.currentPipelineIndex = index;
      if (this.currentPass)
         this.currentPass.setPipeline (this.currentPipeline);
   },

   _setVertexBuffer (handle, offset) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu: unknown buffer handle ${handle}`);
         return;
      }
      if (this.currentPass)
         this.currentPass.setVertexBuffer (0, buffer, offset);
   },

   _setIndexBuffer (handle, offset) {
      const buffer = this.buffers.get (handle);
      if (!buffer) {
         console.error (`noriGpu: unknown buffer handle ${handle}`);
         return;
      }
      if (this.currentPass)
         this.currentPass.setIndexBuffer (buffer, "uint32", offset);
   },

   _draw (vertexCount, instanceCount, firstVertex) {
      if (this.currentPass)
         this.currentPass.draw (vertexCount, instanceCount, firstVertex, 0);
   },

   _drawIndexed (indexCount, instanceCount, firstIndex, baseVertex) {
      if (this.currentPass)
         this.currentPass.drawIndexed (indexCount, instanceCount, firstIndex, baseVertex, 0);
   },

   // --- Internal: texture management -----------------------------------------

   _bindTexture (handle, slot) {
      this.boundTextures.set (slot, handle);
   },

   _deleteTexture (handle) {
      const texture = this.textures.get (handle);
      if (texture) {
         texture.destroy ();
         this.textures.delete (handle);
         this.textureViews.delete (handle);
         this.samplers.delete (handle);
      }
   },

   // --- Internal: framebuffer management -------------------------------------

   _createFramebuffer (handle, width, height) {
      const colorTexture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: this.canvasFormat,
         usage: GPUTextureUsage.RENDER_ATTACHMENT |
                GPUTextureUsage.TEXTURE_BINDING |
                GPUTextureUsage.COPY_SRC,
         label: `fb-color-${handle}`
      });
      const depthTexture = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: DEPTH_STENCIL_FORMAT,
         usage: GPUTextureUsage.RENDER_ATTACHMENT,
         label: `fb-depth-${handle}`
      });
      this.framebuffers.set (handle, {
         color: colorTexture,
         colorView: colorTexture.createView (),
         depth: depthTexture,
         depthView: depthTexture.createView (),
         width, height
      });
   },

   _bindFramebuffer (handle) {
      this._endRenderPass ();
      this.currentFB = this.framebuffers.get (handle) || null;
   },

   _bindDefaultFramebuffer () {
      this._endRenderPass ();
      this.currentFB = null;
   },

   _deleteFramebuffer (handle) {
      const fb = this.framebuffers.get (handle);
      if (fb) {
         fb.color.destroy ();
         fb.depth.destroy ();
         this.framebuffers.delete (handle);
      }
   },

   // --- Internal: render pass management -------------------------------------

   _ensureDepthStencil (width, height) {
      if (width < 1) width = 1;
      if (height < 1) height = 1;
      if (this._defaultDepth && this._defaultDepthW === width && this._defaultDepthH === height)
         return;

      // Destroy old texture if it exists
      if (this._defaultDepth)
         this._defaultDepth.destroy ();

      this._defaultDepth = this.device.createTexture ({
         size: { width, height, depthOrArrayLayers: 1 },
         format: DEPTH_STENCIL_FORMAT,
         usage: GPUTextureUsage.RENDER_ATTACHMENT,
         label: "default-depth-stencil"
      });
      this._defaultDepthView = this._defaultDepth.createView ();
      this._defaultDepthW = width;
      this._defaultDepthH = height;
   },

   _beginRenderPass (r, g, b, a, label) {
      // End any existing pass before starting a new one
      this._endRenderPass ();

      if (!this.currentEncoder)
         this.currentEncoder = this.device.createCommandEncoder ();

      // Determine the color and depth attachments
      let colorView, depthView;
      if (this.currentFB) {
         colorView = this.currentFB.colorView;
         depthView = this.currentFB.depthView;
      } else {
         colorView = this.context.getCurrentTexture ().createView ();
         // Ensure default depth-stencil matches canvas size
         this._ensureDepthStencil (this.canvas.width, this.canvas.height);
         depthView = this._defaultDepthView;
      }

      const colorAttachment = {
         view: colorView,
         clearValue: { r, g, b, a },
         loadOp: "clear",
         storeOp: "store"
      };

      const passDescriptor = {
         colorAttachments: [colorAttachment],
         label: label || "render-pass"
      };

      // Always attach depth-stencil (required by pipelines that use it)
      if (depthView) {
         passDescriptor.depthStencilAttachment = {
            view: depthView,
            depthClearValue: 1.0,
            depthLoadOp: "clear",
            depthStoreOp: "store",
            stencilClearValue: 0,
            stencilLoadOp: "clear",
            stencilStoreOp: "store"
         };
      }

      this.currentPass = this.currentEncoder.beginRenderPass (passDescriptor);

      // Apply viewport if one was set
      const vp = this.viewport;
      if (vp.w > 0 && vp.h > 0)
         this.currentPass.setViewport (vp.x, vp.y, vp.w, vp.h, 0.0, 1.0);

      // Re-apply current pipeline if one is active
      if (this.currentPipeline)
         this.currentPass.setPipeline (this.currentPipeline);
   },

   _endRenderPass () {
      if (this.currentPass) {
         this.currentPass.end ();
         this.currentPass = null;
      }
   },

   _submitCommands () {
      if (this.currentEncoder) {
         const commandBuffer = this.currentEncoder.finish ();
         this.queue.submit ([commandBuffer]);
         this.currentEncoder = null;
      }
   }
};
