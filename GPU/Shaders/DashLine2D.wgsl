// DashLine2D.wgsl — 2D dashed/dotted line rendering via instanced quads
// Replaces: World2D.vert + DashLine2D.geom + DashLine.frag
// Used by: DashLine2D
//
// Strategy: Like Line2D, but also computes a texture coordinate along the line
// length for dash pattern lookup. The fragment shader samples a 1D line-type
// texture to determine visibility.

struct Uniforms {
    xfm: mat4x4<f32>,         // offset 0,  size 64
    vp_scale: vec2<f32>,       // offset 64, size 8
    line_width: f32,           // offset 72, size 4
    lt_scale: f32,             // offset 76, size 4
    draw_color: vec4<f32>,     // offset 80, size 16 (aligned to 16)
    line_type: f32,            // offset 96, size 4
    _pad0: f32,                // offset 100, size 4
    _pad1: f32,                // offset 104, size 4
    _pad2: f32,                // offset 108, size 4
};                             // total: 112 bytes (aligned to 16)

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
