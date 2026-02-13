// Point3D.wgsl — 3D point rendering via instanced quads
// Replaces: World3D.vert + Point3D.geom + Point.frag
// Used by: Point3D
//
// Same as Point2D but operates on 3D positions and preserves depth.

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

const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

const CORNER_SIGNS = array<vec2<f32>, 4>(
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
