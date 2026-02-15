// Flat2D.wgsl — Simple 2D flat-colored rendering (triangles / quads / tri-fans)
// Replaces: World2D.vert + Flat.frag
// Used by: Triangle2D, Quad2D, TriFanStencil, TriFanCover

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
