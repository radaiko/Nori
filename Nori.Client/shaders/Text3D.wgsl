// Text3D.wgsl — World-space 3D text rendering via instanced quads
// Replaces: Text3D.vert + Text3D.geom + Text.frag
// Used by: Text3D
//
// Strategy: Like Text2D but operates on 3D anchor positions and preserves depth.
// Each character is an instance with a 3D world-space anchor, character box, and
// texture offset. The vertex shader projects to clip space, snaps to pixel grid,
// expands the quad, and preserves the Z depth.

struct Uniforms {
    xfm: mat4x4<f32>,
    vp_scale: vec2<f32>,
    _pad: vec2<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var font_texture: texture_2d<f32>;
@group(0) @binding(2) var font_sampler: sampler;

struct VertexInput {
    @location(0) pos: vec3<f32>,
    @location(1) char_box: vec4<i32>,
    @location(2) tex_offset: i32,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) cell_size: vec2<i32>,
    @location(1) @interpolate(flat) tex_offset: i32,
    @location(2) tex_coord: vec2<f32>,
};

var<private> QUAD_IDX: array<u32, 6> = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let cell_size = vec2<i32>(input.char_box.z - input.char_box.x,
                              input.char_box.w - input.char_box.y);

    // Transform 3D anchor to clip space
    let clip = uniforms.xfm * vec4<f32>(input.pos, 1.0);
    let clip_z = clip.z;

    // Convert to pixel coordinates and snap to pixel grid
    var xyref = floor(clip.xy / uniforms.vp_scale);
    xyref = xyref + vec2<f32>(0.01, 0.01);

    // Compute box corners in pixel space
    let xy0 = xyref + vec2<f32>(f32(input.char_box.x), f32(input.char_box.y));
    let xy1 = xyref + vec2<f32>(f32(input.char_box.z), f32(input.char_box.w));

    // Convert back to clip space
    let clip0 = xy0 * uniforms.vp_scale;
    let clip1 = xy1 * uniforms.vp_scale;

    let w = f32(cell_size.x);
    let h = f32(cell_size.y);

    var pos: vec2<f32>;
    var tc: vec2<f32>;
    switch corner {
        case 0u: { pos = vec2<f32>(clip0.x, clip0.y); tc = vec2<f32>(0.0, h); }
        case 1u: { pos = vec2<f32>(clip1.x, clip0.y); tc = vec2<f32>(w, h); }
        case 2u: { pos = vec2<f32>(clip0.x, clip1.y); tc = vec2<f32>(0.0, 0.0); }
        case 3u: { pos = vec2<f32>(clip1.x, clip1.y); tc = vec2<f32>(w, 0.0); }
        default: { pos = vec2<f32>(0.0); tc = vec2<f32>(0.0); }
    }

    var out: VertexOutput;
    out.position = vec4<f32>(pos, clip_z, 1.0);
    out.cell_size = cell_size;
    out.tex_offset = input.tex_offset;
    out.tex_coord = tc;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let x = i32(in.tex_coord.x);
    let y = i32(in.tex_coord.y);
    let offset = y * in.cell_size.x + x + in.tex_offset;
    let st = vec2<i32>(offset % 8192, offset / 8192);
    let r = textureLoad(font_texture, st, 0).r;
    if r < 0.001 {
        discard;
    }
    return vec4<f32>(uniforms.draw_color.rgb, r);
}
