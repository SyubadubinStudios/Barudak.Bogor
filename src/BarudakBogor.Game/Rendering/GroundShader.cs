namespace BarudakBogor.Game.Rendering;

/// <summary>
/// The surface shader shared by the ground and the roads. It layers world-space detail over
/// the chunk's painted colour (so the ground is never one flat tone up close) and, when it has
/// rained, opens puddles in the hollows: dark, mirror-smooth water that the screen-space
/// reflections pick up, ringed by raindrops while the rain lasts.
/// </summary>
/// <remarks>
/// Material slots: custom0.x wetness 0..1, custom0.y 1 for a road and 0 for the ground,
/// custom0.z rain 0..1.
/// </remarks>
internal static class GroundShader
{
    public const string Source = """
        fn bb_hash(p: vec2<f32>) -> f32 {
            let q = fract(p * vec2<f32>(0.1031, 0.1030));
            let r = q + dot(q, q.yx + 33.33);
            return fract((r.x + r.y) * r.x);
        }

        fn bb_noise(p: vec2<f32>) -> f32 {
            let i = floor(p);
            let f = fract(p);
            let u = f * f * (3.0 - 2.0 * f);
            return mix(mix(bb_hash(i), bb_hash(i + vec2<f32>(1.0, 0.0)), u.x),
                       mix(bb_hash(i + vec2<f32>(0.0, 1.0)), bb_hash(i + vec2<f32>(1.0, 1.0)), u.x), u.y);
        }

        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let p = context.world_position.xz;
            let wet = context.custom0.x;
            let road = context.custom0.y;
            let rain = context.custom0.z;

            // Layers: broad patches, clumps a stride across, and grain underfoot.
            let broad = bb_noise(p * 0.07);
            let clump = bb_noise(p * 0.55);
            let grain = bb_noise(p * 4.5);
            let green = clamp((surface.albedo.g - max(surface.albedo.r, surface.albedo.b)) * 6.0, 0.0, 1.0);
            let ground_tone = (0.86 + broad * 0.2) * (0.9 + clump * 0.2) * (0.94 + grain * 0.12);
            let road_tone = 0.95 + grain * 0.1;
            out.albedo = surface.albedo * mix(ground_tone, road_tone, road);
            // Grass grows in darker tufts; bare earth just varies.
            out.albedo = mix(out.albedo, out.albedo * vec3<f32>(0.82, 1.08, 0.78), green * clump * 0.5 * (1.0 - road));
            out.roughness = clamp(surface.roughness + (grain - 0.5) * 0.12, 0.04, 1.0);

            // Damp ground darkens before any water stands on it.
            out.albedo = out.albedo * (1.0 - wet * 0.22 * (1.0 - road));

            // Puddles gather in the low patches of a second, slower field, on level ground only.
            let hollow = bb_noise(p * 0.19 + vec2<f32>(17.0, 5.0)) * 0.7 + bb_noise(p * 0.61) * 0.3;
            let level = clamp((context.world_normal.y - 0.985) * 80.0, 0.0, 1.0);
            let edge = 1.0 - wet * mix(0.42, 0.5, road);
            let puddle = smoothstep(edge, edge + 0.07, hollow) * level * step(0.05, wet);
            if (puddle > 0.001) {
                var normal = vec3<f32>(0.0, 1.0, 0.0);
                if (rain > 0.05) {
                    // Raindrop rings: each cell of a grid drops one ring that widens and fades.
                    let cell = floor(p * 1.6);
                    let local = fract(p * 1.6) - 0.5;
                    let phase = fract(context.time * 1.3 + bb_hash(cell) * 7.0);
                    let centre = (vec2<f32>(bb_hash(cell + 3.1), bb_hash(cell + 7.7)) - 0.5) * 0.5;
                    let d = length(local - centre);
                    let ring = sin((d - phase * 0.45) * 60.0) * smoothstep(0.08, 0.0, abs(d - phase * 0.45)) * (1.0 - phase);
                    let away = normalize(local - centre + vec2<f32>(0.0001, 0.0));
                    normal = normalize(vec3<f32>(away.x * ring * rain * 0.6, 1.0, away.y * ring * rain * 0.6));
                }

                out.albedo = mix(out.albedo, out.albedo * 0.38, puddle);
                out.roughness = mix(out.roughness, 0.03, puddle);
                out.normal = normalize(mix(out.normal, normal, puddle));
            }

            return out;
        }
        """;
}
