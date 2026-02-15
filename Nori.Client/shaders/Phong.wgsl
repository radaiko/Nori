// Phong.wgsl — Phong shading (per-fragment lighting, normal interpolation)
// Replaces: Phong.vert + Phong.frag
// Used by: Phong

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
