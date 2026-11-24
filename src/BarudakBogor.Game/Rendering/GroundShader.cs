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

            // Layers: broad patches, clumps a stride across, fine grit, and asphalt aggregate.
            let broad = bb_noise(p * 0.065);
            let clump = bb_noise(p * 0.52);
            let grain = bb_noise(p * 4.8);
            let micro = bb_noise(p * 14.0);
            let green = clamp((surface.albedo.g - max(surface.albedo.r, surface.albedo.b)) * 6.0, 0.0, 1.0);
            let ground_tone = (0.85 + broad * 0.22) * (0.89 + clump * 0.22) * (0.93 + grain * 0.14);

            // Road surface: subtle aggregate variation + weathered tar cracks.
            let crack_field = abs(bb_noise(p * 0.35 + vec2<f32>(3.7, 9.1)) - 0.5);
            let crack = smoothstep(0.018, 0.0, crack_field) * 0.18;
            let road_tone = (0.92 + broad * 0.1 + grain * 0.08 + micro * 0.05) * (1.0 - crack);

            out.albedo = surface.albedo * mix(ground_tone, road_tone, road);
            // Grass grows in richer tropical tufts; bare earth varies subtly.
            out.albedo = mix(out.albedo, out.albedo * vec3<f32>(0.78, 1.1, 0.74), green * clump * 0.55 * (1.0 - road));

            // Keep dry surfaces genuinely matte so SSR never mirrors on dry grass or dry concrete.
            let dry_rough = mix(0.95, 0.92, road);
            let wet_rough = mix(0.68, 0.26, road);
            out.roughness = clamp(mix(dry_rough, wet_rough, wet) + (grain - 0.5) * 0.09, 0.05, 1.0);

            // Damp ground and asphalt darken realistically when wet.
            out.albedo = out.albedo * (1.0 - wet * mix(0.24, 0.32, road));

            // Puddles gather in the low patches of a second, slower field, on level ground only.
            let hollow = bb_noise(p * 0.19 + vec2<f32>(17.0, 5.0)) * 0.7 + bb_noise(p * 0.61) * 0.3;
            let level = clamp((context.world_normal.y - 0.985) * 80.0, 0.0, 1.0);
            let edge = 1.0 - wet * mix(0.38, 0.48, road);
            let puddle = smoothstep(edge, edge + 0.06, hollow) * level * smoothstep(0.06, 0.18, wet);
            if (puddle > 0.001) {
                var normal = vec3<f32>(0.0, 1.0, 0.0);
                if (rain > 0.05) {
                    // Raindrop rings: each cell of a grid drops one ring that widens and fades.
                    let cell = floor(p * 1.7);
                    let local = fract(p * 1.7) - 0.5;
                    let phase = fract(context.time * 1.45 + bb_hash(cell) * 7.0);
                    let centre = (vec2<f32>(bb_hash(cell + 3.1), bb_hash(cell + 7.7)) - 0.5) * 0.5;
                    let d = length(local - centre);
                    let ring = sin((d - phase * 0.45) * 62.0) * smoothstep(0.08, 0.0, abs(d - phase * 0.45)) * (1.0 - phase);
                    let away = normalize(local - centre + vec2<f32>(0.0001, 0.0));
                    normal = normalize(vec3<f32>(away.x * ring * rain * 0.65, 1.0, away.y * ring * rain * 0.65));
                }

                out.albedo = mix(out.albedo, out.albedo * 0.36, puddle);
                out.roughness = mix(out.roughness, 0.03, puddle);
                out.normal = normalize(mix(out.normal, normal, puddle));
            }

            return out;
        }
        """;
}
