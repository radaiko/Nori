// FlatFacet.wgsl — Flat-faceted shading (per-face lighting, no interpolation)
// Replaces: FlatFacet.vert + FlatFacet.geom + FlatFacet.frag
// Used by: FlatFacet
//
// Strategy: The GLSL geometry shader averaged the 3 vertex normals to get a
// per-face normal. In WGSL we use @interpolate(flat) on the light intensity
// computed in the vertex shader. The provoking vertex's normal is used for
// the entire face. For better accuracy, the CPU should provide face normals
// or we compute lighting per-vertex and let flat interpolation pick one.
// This is the standard approach when geometry shaders are unavailable.

struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const LIGHT_POS = vec3<f32>(0.0, 0.0, 1.0);
const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);
const SPECULAR_COLOR = vec4<f32>(1.0, 1.0, 1.0, 1.0);
const SPECULAR_EXP: f32 = 100.0;

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
    let tnorm = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 1.0)).xyz);
    let dotp = abs(dot(LIGHT_POS, tnorm));
    let amb_diffuse = uniforms.draw_color * 0.9 * dotp + AMBIENT_COLOR;
    let specular = SPECULAR_COLOR * pow(abs(dot(tnorm, vec3<f32>(0.0, 0.0, 1.0))), SPECULAR_EXP);

    var out: VertexOutput;
    out.light_intensity = amb_diffuse + specular;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.light_intensity.rgb, uniforms.draw_color.a);
}
