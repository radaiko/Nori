// Gourad.wgsl — Gouraud shading (per-vertex lighting, color interpolation)
// Replaces: Gourad.vert + Gourad.frag
// Used by: Gourad

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
    @location(0) light_intensity: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    let light_dir = normalize(uniforms.normal_xfm[3].xyz);
    let diffuse = abs(dot(light_dir, normalize(input.normal)));
    var out: VertexOutput;
    out.light_intensity = uniforms.draw_color * diffuse + AMBIENT_COLOR;
    out.position = uniforms.xfm * vec4<f32>(input.position, 1.0);
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.light_intensity.rgb, uniforms.draw_color.a);
}
