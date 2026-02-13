// Pick.wgsl — Picking shader (renders object IDs as colors for hit testing)
// Replaces: FlatFacet.vert + Flat.frag
// Used by: Pick
//
// The vertex shader transforms position + normal (same layout as FlatFacet),
// but the fragment shader simply outputs the draw_color uniform which encodes
// the object ID for picking.

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
