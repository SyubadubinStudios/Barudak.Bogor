using System.Numerics;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using BarudakBogor.World;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Sky, sun, fog, rain and lightning: everything that makes Bogor look like Bogor. Reads the
/// clock and the weather from the session each frame and drives the scene environment.
/// </summary>
public sealed class Atmosphere
{
    private const int MaxDrops = 1_800;
    private const float RainBox = 22f;
    private const int LampLights = 6;

    private readonly Scene _scene;
    private readonly Node _sun;
    private readonly Node _fill;
    private readonly Node _rain;
    private readonly Node[] _lamps = new Node[LampLights];
    private readonly Vector3[] _drops = new Vector3[MaxDrops];
    private readonly Matrix4x4[] _dropMatrices = new Matrix4x4[MaxDrops];
    private readonly List<(Vector2 Position, float Yaw)> _nearLamps = [];
    private float _lampTimer;
    private int _activeDrops = -1;

    public Atmosphere(Scene scene)
    {
        _scene = scene;
        _sun = scene.AddLight(Light.Directional(Vector3.One, 3f) with { CastShadow = true, ShadowNormalBias = 2.5f }, name: "sun");
        _fill = scene.AddLight(Light.Hemisphere(new Vector3(0.6f, 0.7f, 0.85f), new Vector3(0.25f, 0.24f, 0.2f), 0.5f), name: "sky-fill");

        Material streak = scene.CreateMaterial(MaterialOptions.Basic(new Vector4(0.72f, 0.8f, 0.9f, 0.26f)) with
        {
            AlphaMode = AlphaMode.Blend,
            DepthWrite = false,
            CullMode = CullMode.None,
        });
        _rain = scene.AddMesh(scene.CreateBoxGeometry(0.009f, 0.7f, 0.009f), streak, name: "rain");
        _rain.CastShadow = false;
        _rain.ReceiveShadow = false;
        _rain.Visible = false;
        Rng rng = new(404);
        for (int i = 0; i < MaxDrops; i++)
        {
            _drops[i] = new Vector3(rng.Range(0f, RainBox), rng.Range(0f, RainBox), rng.Range(0f, RainBox));
        }

        for (int i = 0; i < LampLights; i++)
        {
            _lamps[i] = scene.AddLight(Light.Point(new Vector3(1f, 0.74f, 0.42f), 60f, 26f) with { Enabled = false }, name: "lamp");
        }
    }

    /// <summary>Scales the number of rain streaks (quality setting).</summary>
    public float RainDensity { get; set; } = 1f;

    /// <summary>Current fog amount 0..1 at the camera, for the HUD and audio.</summary>
    public float Fog { get; private set; }

    /// <summary>How strongly windows and lamps glow: 0 by day, 1 at night.</summary>
    public float NightGlow { get; private set; }

    public void Update(GameSession session, Vector3 cameraPosition, float dt)
    {
        float hours = session.Clock.Hours;
        float daylight = session.Clock.Daylight;
        float clouds = session.Weather.Clouds;
        float rain = session.Weather.Rain;
        float flash = session.Weather.Flash;
        Fog = session.Weather.FogAt(cameraPosition.Y, hours);

        // The sun crosses from east (+X) to west and keeps going under the world; the moon rides
        // opposite it. The sky pass draws both discs, the stars and the cloud sheet from this one
        // direction, so the light in the scene must come from the same place.
        float dayAngle = (hours - 6f) / 12f * MathF.PI;
        bool moon = daylight < 0.02f;
        Vector3 skySun = Vector3.Normalize(new Vector3(MathF.Cos(dayAngle), MathF.Sin(dayAngle), 0.35f));
        Vector3 toSun = moon
            ? Vector3.Normalize(new Vector3(-skySun.X, MathF.Max(0.3f, -skySun.Y), -skySun.Z))
            : Vector3.Normalize(new Vector3(skySun.X, MathF.Max(0.12f, skySun.Y), skySun.Z));
        float low = 1f - MathUtil.Saturate(toSun.Y * 2.2f);
        Vector3 sunColor = Vector3.Lerp(new Vector3(1f, 0.96f, 0.9f), new Vector3(1f, 0.6f, 0.34f), moon ? 0f : low);
        float overcast = 1f - (0.82f * clouds);
        float sunPower = moon ? 0.22f : 3.4f * daylight * overcast;
        if (moon)
        {
            sunColor = new Vector3(0.55f, 0.65f, 0.95f);
        }

        _sun.Light = Light.Directional(sunColor, sunPower + (flash * 5f)) with
        {
            CastShadow = sunPower > 0.5f,
            ShadowNormalBias = 2.5f,
            ShadowStrength = MathUtil.Saturate(overcast * 1.1f),
        };
        _sun.Position = cameraPosition + (toSun * 400f);
        _sun.LookAt(cameraPosition);

        float ambient = MathUtil.Lerp(0.1f, 0.5f, daylight) * (0.75f + (0.5f * clouds)) + (flash * 1.2f);
        Vector3 skyTint = Vector3.Lerp(new Vector3(0.25f, 0.32f, 0.55f), Vector3.Lerp(new Vector3(0.6f, 0.72f, 0.9f), new Vector3(0.66f, 0.7f, 0.74f), clouds), daylight);
        _fill.Light = Light.Hemisphere(skyTint, new Vector3(0.26f, 0.25f, 0.2f) * MathUtil.Lerp(0.4f, 1f, daylight), ambient);

        // Fog takes the colour of the horizon so distant chunks melt into the sky.
        Vector3 dayFog = Vector3.Lerp(new Vector3(0.62f, 0.74f, 0.86f), new Vector3(0.58f, 0.62f, 0.66f), MathUtil.Saturate((clouds * 0.9f) + (Fog * 0.4f)));
        Vector3 duskFog = new(0.72f, 0.5f, 0.38f);
        Vector3 nightFog = new(0.022f, 0.035f, 0.085f);
        Vector3 fogColor = Vector3.Lerp(nightFog, Vector3.Lerp(dayFog, duskFog, moon ? 0f : low * (1f - clouds) * 0.7f), daylight);
        fogColor += new Vector3(0.5f, 0.55f, 0.7f) * flash * 0.5f;
        float density = MathUtil.Lerp(0.0006f, 0.019f, Fog * Fog) + (session.Weather.Mist * 0.012f) + (rain * 0.0012f);

        _scene.Environment = _scene.Environment with
        {
            Sky = SkyMode.Procedural,
            SunDirection = skySun,
            // The sky darkens itself when the sun is down; rain cloud and mist dim what is left.
            SkyIntensity = (1f - (0.35f * clouds)) * (1f - (0.5f * Fog * Fog)) + (flash * 0.8f),
            SkyHaze = MathUtil.Saturate(0.3f + (0.5f * clouds) + (0.4f * Fog)),
            // Even a fine day in Bogor has cloud building somewhere.
            SkyClouds = MathF.Max(clouds, 0.3f),
            Background = new Vector4(fogColor, 1f),
            FogColor = fogColor,
            FogDensity = density,
            AmbientColor = skyTint,
            AmbientIntensity = ambient * 0.35f,
        };

        // Lights come on at dusk; a dark storm switches a few on by day as well.
        NightGlow = MathF.Max(1f - MathUtil.SmoothStep(0.15f, 0.6f, daylight), 0.3f * MathUtil.SmoothStep(0.7f, 1f, clouds));
        UpdateRain(session, cameraPosition, dt);
        UpdateLamps(session, cameraPosition, dt);
    }

    private void UpdateRain(GameSession session, Vector3 cameraPosition, float dt)
    {
        float rain = session.Weather.Rain;
        int count = (int)(MaxDrops * MathUtil.Saturate(rain * 1.15f) * RainDensity);
        if (count <= 0)
        {
            if (_activeDrops != 0)
            {
                _activeDrops = 0;
                _rain.Visible = false;
            }

            return;
        }

        float wind = session.Weather.Wind;
        float fall = 15f + (rain * 9f);
        Vector3 velocity = new(wind * 5f, -fall, wind * 2f);
        Matrix4x4 lean = Matrix4x4.CreateRotationZ(MathF.Atan2(velocity.X, fall)) * Matrix4x4.CreateRotationX(-MathF.Atan2(velocity.Z, fall));
        Vector3 corner = cameraPosition - new Vector3(RainBox * 0.5f, RainBox * 0.4f, RainBox * 0.5f);
        Vector3 step = velocity * dt;
        for (int i = 0; i < count; i++)
        {
            // Each drop lives in a box that wraps around the camera, so rain never runs out.
            Vector3 p = _drops[i] + step;
            _drops[i] = p;
            Vector3 world = new(
                corner.X + Wrap(p.X - corner.X),
                corner.Y + Wrap(p.Y - corner.Y),
                corner.Z + Wrap(p.Z - corner.Z));
            Matrix4x4 m = lean;
            m.Translation = world;
            _dropMatrices[i] = m;
        }

        _rain.SetInstances(_dropMatrices.AsSpan(0, count));
        _rain.Visible = true;
        _activeDrops = count;
    }

    private static float Wrap(float value)
    {
        value %= RainBox;
        return value < 0f ? value + RainBox : value;
    }

    private void UpdateLamps(GameSession session, Vector3 cameraPosition, float dt)
    {
        // Only a handful of real lights: the lamps nearest the camera. The rest glow via emissive.
        _lampTimer -= dt;
        if (_lampTimer > 0f)
        {
            return;
        }

        _lampTimer = 0.4f;
        _nearLamps.Clear();
        Vector2 eye = cameraPosition.Xz();
        if (NightGlow > 0.3f)
        {
            session.Map.ForEachProp(eye - new Vector2(90f), eye + new Vector2(90f), prop =>
            {
                if (prop.Model == "lampu_jalan" && !session.Weather.IsBlackedOut(prop.Position))
                {
                    _nearLamps.Add((prop.Position, prop.Yaw));
                }
            });
            _nearLamps.Sort((a, b) => Vector2.DistanceSquared(a.Position, eye).CompareTo(Vector2.DistanceSquared(b.Position, eye)));
        }

        for (int i = 0; i < LampLights; i++)
        {
            bool on = i < _nearLamps.Count;
            Light light = Light.Point(new Vector3(1f, 0.74f, 0.42f), 70f * NightGlow, 28f) with { Enabled = on };
            _lamps[i].Light = light;
            if (on)
            {
                (Vector2 position, float yaw) = _nearLamps[i];
                Vector2 head = position + (MathUtil.Forward(yaw) * 2.2f);
                _lamps[i].Position = new Vector3(head.X, session.Map.Terrain.HeightAt(position) + 6.6f, head.Y);
            }
        }
    }
}
