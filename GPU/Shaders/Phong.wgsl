// Phong.wgsl — Phong shading (per-fragment lighting, normal interpolation)
// Replaces: Phong.vert + Phong.frag
// Used by: Phong

struct Uniforms {
    xfm: mat4x4<f32>,
    normal_xfm: mat4x4<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

const LIGHT_POS = vec3<f32>(0.0, 0.0, 1.0);
const AMBIENT_COLOR = vec4<f32>(0.1, 0.1, 0.1, 1.0);
const SPECULAR_COLOR = vec4<f32>(1.0, 1.0, 1.0, 1.0);
const SPECULAR_EXP: f32 = 64.0;

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
    out.normal = normalize((uniforms.normal_xfm * vec4<f32>(input.normal, 1.0)).xyz);
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let tnorm = normalize(in.normal);
    let dotp = abs(dot(LIGHT_POS, tnorm));
    let amb_diffuse = uniforms.draw_color * 0.9 * dotp + AMBIENT_COLOR;
    let specular = SPECULAR_COLOR * pow(abs(dot(tnorm, vec3<f32>(0.0, 0.0, 1.0))), SPECULAR_EXP);
    let light_intensity = amb_diffuse + specular;
    return vec4<f32>(light_intensity.rgb, uniforms.draw_color.a);
}
