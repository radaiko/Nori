// GlassLine.wgsl — 3D stippled line rendering (checkerboard transparency)
// Replaces: World3D.vert + Line3D.geom + GlassLine.frag
// Used by: GlassLine
//
// Same as Line3D but the fragment shader applies a checkerboard discard pattern
// to simulate 50% transparency (stipple effect).

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
    // Checkerboard stipple pattern: discard every other pixel
    let coord = vec2<i32>(in.position.xy - vec2<f32>(0.5));
    if fract(f32(coord.x + coord.y) / 2.0) < 0.5 {
        discard;
    }
    let d = abs(in.dist) / uniforms.line_width;
    let a = exp2(-2.0 * d * d);
    return vec4<f32>(uniforms.draw_color.rgb, a);
}
