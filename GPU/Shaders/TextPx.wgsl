// TextPx.wgsl — Pixel-space text rendering via instanced quads
// Replaces: TextPx.vert + Text2D.geom + Text.frag
// Used by: TextPx
//
// Strategy: Each character glyph is an instance providing a bounding box (Vec4S)
// and a texture offset (int). The vertex shader (6 vertices per instance) expands
// the box into a screen-space quad. The fragment shader samples the font texture
// to determine glyph alpha.
//
// The original GLSL pipeline:
//   TextPx.vert: Computes box corners in pixel coords, packs into gl_Position.xyzw
//   Text2D.geom: Expands the packed box into a quad with tex coords
//   Text.frag: Samples the rectangle font texture by computing 1D offset
//
// In WGSL we do all of this in a single vertex shader + fragment shader.

struct Uniforms {
    vp_scale: vec2<f32>,
    _pad: vec2<f32>,
    draw_color: vec4<f32>,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var font_texture: texture_2d<f32>;
@group(0) @binding(2) var font_sampler: sampler;

struct VertexInput {
    // Per-instance: character bounding box (x0, y0, x1, y1) as shorts
    @location(0) char_box: vec4<i32>,
    // Per-instance: offset into the font texture data
    @location(1) tex_offset: i32,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) @interpolate(flat) cell_size: vec2<i32>,
    @location(1) @interpolate(flat) tex_offset: i32,
    @location(2) tex_coord: vec2<f32>,
};

const QUAD_IDX = array<u32, 6>(0u, 1u, 2u, 2u, 1u, 3u);

@vertex
fn vs_main(@builtin(vertex_index) vid: u32, input: VertexInput) -> VertexOutput {
    let corner = QUAD_IDX[vid];
    let cell_size = vec2<i32>(input.char_box.z - input.char_box.x,
                              input.char_box.w - input.char_box.y);

    // Compute clip-space box corners from pixel coordinates
    // Original GLSL: gl_Position = vec4(CharBoxN.xy * VPScale, CharBoxN.zw * VPScale) - vec4(1,1,1,1)
    let box_xy = vec2<f32>(f32(input.char_box.x), f32(input.char_box.y)) * uniforms.vp_scale - vec2<f32>(1.0);
    let box_zw = vec2<f32>(f32(input.char_box.z), f32(input.char_box.w)) * uniforms.vp_scale - vec2<f32>(1.0);

    // Corner positions and texture coordinates
    // 0: (x0, y0) tex(0, h)  1: (x1, y0) tex(w, h)  2: (x0, y1) tex(0, 0)  3: (x1, y1) tex(w, 0)
    var pos: vec2<f32>;
    var tc: vec2<f32>;
    let w = f32(cell_size.x);
    let h = f32(cell_size.y);

    switch corner {
        case 0u: { pos = vec2<f32>(box_xy.x, box_xy.y); tc = vec2<f32>(0.0, h); }
        case 1u: { pos = vec2<f32>(box_zw.x, box_xy.y); tc = vec2<f32>(w, h); }
        case 2u: { pos = vec2<f32>(box_xy.x, box_zw.y); tc = vec2<f32>(0.0, 0.0); }
        case 3u: { pos = vec2<f32>(box_zw.x, box_zw.y); tc = vec2<f32>(w, 0.0); }
        default: { pos = vec2<f32>(0.0); tc = vec2<f32>(0.0); }
    }

    var out: VertexOutput;
    out.position = vec4<f32>(pos, 0.0, 1.0);
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
