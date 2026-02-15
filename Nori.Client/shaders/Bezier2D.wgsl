// Bezier2D.wgsl — Pre-tessellated Bezier curve rendering (same as Line2D)
// Replaces: World2D.vert + Bezier.tctrl + Bezier.teval + Line2D.geom + Line.frag
// Used by: Bezier2D
//
// Strategy: WebGPU does not support tessellation shaders. Bezier curves are
// pre-tessellated on the CPU using Poly.Flatten() to produce line segments.
// These line segments are then rendered using the same instanced quad approach
// as Line2D. This shader is identical to Line2D.wgsl — it exists as a separate
// file for pipeline mapping clarity and in case future divergence is needed.

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
