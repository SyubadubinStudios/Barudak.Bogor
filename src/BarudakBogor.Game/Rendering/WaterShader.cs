namespace BarudakBogor.Game.Rendering;

/// <summary>
/// The surface shader of the river and the ponds: moving ripples, deep water darker than the
/// shallows, foam where the current meets the bank, a surface that turns mirror-like at a
/// glancing angle and clear when looked straight down into, and rings when it rains.
/// </summary>
/// <remarks>
/// Material slots: custom0.x rain 0..1, custom0.y 1 for a pond and 0 for a river, custom0.z how
/// muddy the water is 0..1, custom0.w pond radius; custom1.xy pond centre (world x, z).
/// A river is drawn as a ribbon whose uv.x runs bank to bank and uv.y downstream (one unit per
/// 24 m), which is what lets the ripples travel with the current.
/// </remarks>
internal static class WaterShader
{
    public const string Source = """
        fn bw_hash(p: vec2<f32>) -> f32 {
            let q = fract(p * vec2<f32>(0.1031, 0.1030));
            let r = q + dot(q, q.yx + 33.33);
            return fract((r.x + r.y) * r.x);
        }

        fn bw_noise(p: vec2<f32>) -> f32 {
            let i = floor(p);
            let f = fract(p);
            let u = f * f * (3.0 - 2.0 * f);
            return mix(mix(bw_hash(i), bw_hash(i + vec2<f32>(1.0, 0.0)), u.x),
                       mix(bw_hash(i + vec2<f32>(0.0, 1.0)), bw_hash(i + vec2<f32>(1.0, 1.0)), u.x), u.y);
        }

        // Three trains of ripples, each smaller and quicker than the last, all running downstream (-y).
        fn bw_height(p: vec2<f32>, t: f32) -> f32 {
            return bw_noise(p * 0.9 + vec2<f32>(0.0, t * 1.1)) * 0.6
                 + bw_noise(p * 2.3 + vec2<f32>(t * 0.4, t * 1.9)) * 0.3
                 + bw_noise(p * 5.1 + vec2<f32>(-t * 0.7, t * 2.6)) * 0.1;
        }

        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let rain = context.custom0.x;
            let pond = context.custom0.y;
            let mud = context.custom0.z;

            // p: position on the water in metres; edge: 0 in mid-water, 1 at the bank.
            var p = vec2<f32>(context.uv.x * 32.0, context.uv.y * 24.0);
            var edge = clamp(abs(context.uv.x - 0.5) * 2.0 / 0.81, 0.0, 1.0);
            var t = context.time;
            var chop = 1.0;
            if (pond > 0.5) {
                p = context.world_position.xz;
                edge = clamp(length(context.world_position.xz - context.custom1.xy) / max(context.custom0.w, 1.0), 0.0, 1.0);
                // Still water only shivers in the breeze.
                t = context.time * 0.22;
                chop = 0.45;
            }

            let e = 0.18;
            let h = bw_height(p, t);
            let hx = bw_height(p + vec2<f32>(e, 0.0), t);
            let hz = bw_height(p + vec2<f32>(0.0, e), t);
            var normal = normalize(vec3<f32>(-(hx - h) * chop, 1.0, -(hz - h) * chop));

            if (rain > 0.05) {
                let cell = floor(context.world_position.xz * 1.4);
                let local = fract(context.world_position.xz * 1.4) - 0.5;
                let phase = fract(context.time * 1.3 + bw_hash(cell) * 7.0);
                let centre = (vec2<f32>(bw_hash(cell + 3.1), bw_hash(cell + 7.7)) - 0.5) * 0.5;
                let d = length(local - centre);
                let ring = sin((d - phase * 0.45) * 60.0) * smoothstep(0.08, 0.0, abs(d - phase * 0.45)) * (1.0 - phase);
                let away = normalize(local - centre + vec2<f32>(0.0001, 0.0));
                normal = normalize(normal + vec3<f32>(away.x, 0.0, away.y) * ring * rain * 0.7);
            }

            // Colour by depth: clear green water, or the brown of a river carrying half a hillside.
            let shallow = smoothstep(0.45, 1.0, edge);
            let deep_colour = mix(vec3<f32>(0.02, 0.10, 0.11), vec3<f32>(0.10, 0.11, 0.06), mud);
            let shallow_colour = mix(vec3<f32>(0.12, 0.27, 0.22), vec3<f32>(0.33, 0.28, 0.16), mud);
            var colour = mix(deep_colour, shallow_colour, shallow);

            // Foam: a broken line along the banks of a river, and flecks on the crests in mid-stream.
            var foam = 0.0;
            if (pond < 0.5) {
                let lace = bw_noise(vec2<f32>(p.x * 0.9, p.y * 0.3 + t * 1.4));
                foam = smoothstep(0.55, 0.8, lace) * smoothstep(0.72, 0.97, edge) * 0.8;
                foam = foam + smoothstep(0.8, 0.9, h) * 0.18 * (1.0 - shallow);
            } else {
                // A pond only gathers a thin scum of duckweed against its bank.
                colour = mix(colour, vec3<f32>(0.2, 0.36, 0.12), smoothstep(0.86, 1.0, edge) * bw_noise(p * 0.8) * 0.8);
            }
            foam = clamp(foam, 0.0, 1.0);
            colour = mix(colour, vec3<f32>(0.86, 0.88, 0.84), foam);

            // Looked straight down into, water is clear; at a glancing angle it is a mirror.
            let facing = clamp(dot(normal, normalize(context.view_direction)), 0.0, 1.0);
            let fresnel = pow(1.0 - facing, 3.0);
            var alpha = mix(mix(0.7, 0.9, mud), 0.98, fresnel);
            alpha = max(alpha, foam);
            // The very edge thins out, so the water meets the bank softly.
            alpha = alpha * (1.0 - smoothstep(0.965, 1.0, edge) * 0.75);

            out.albedo = colour;
            out.alpha = alpha;
            out.normal = normal;
            out.metallic = 0.0;
            out.roughness = mix(0.1, 0.6, foam);
            return out;
        }
        """;
}
