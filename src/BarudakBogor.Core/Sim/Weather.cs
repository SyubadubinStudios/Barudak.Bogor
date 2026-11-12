using System.Numerics;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Sim;

/// <summary>In-game time of day. One real second is one game minute by default (a 24 minute day).</summary>
public sealed class GameClock
{
    /// <summary>Game minutes that pass per real second.</summary>
    public float MinutesPerSecond { get; set; } = 1f;

    /// <summary>Day counter, starting at 1.</summary>
    public int Day { get; private set; } = 1;

    /// <summary>Time of day in hours, [0, 24).</summary>
    public float Hours { get; private set; } = 15.5f;

    public int Hour => (int)Hours;

    public int Minute => (int)((Hours - Hour) * 60f);

    public bool IsNight => Hours is < 5.5f or >= 18.5f;

    /// <summary>The part of the day: morning, midday, afternoon or night.</summary>
    public DayPhase Phase => Hours switch
    {
        >= 5f and < 11f => DayPhase.Pagi,
        >= 11f and < 15f => DayPhase.Siang,
        >= 15f and < 18.5f => DayPhase.Sore,
        _ => DayPhase.Malam,
    };

    public static L PhaseName(DayPhase phase) => phase switch
    {
        DayPhase.Pagi => new("Pagi", "Morning"),
        DayPhase.Siang => new("Siang", "Midday"),
        DayPhase.Sore => new("Sore", "Afternoon"),
        _ => new("Malam", "Night"),
    };

    /// <summary>0 at night, 1 in full daylight, with smooth dawn and dusk.</summary>
    public float Daylight =>
        MathUtil.SmoothStep(5f, 7f, Hours) * (1f - MathUtil.SmoothStep(17.3f, 19f, Hours));

    public void Set(int day, float hours)
    {
        Day = Math.Max(1, day);
        Hours = ((hours % 24f) + 24f) % 24f;
    }

    public void Update(float dt)
    {
        Hours += dt * MinutesPerSecond / 60f;
        while (Hours >= 24f)
        {
            Hours -= 24f;
            Day++;
        }
    }

    public override string ToString() => $"{Hour:00}:{Minute:00}";
}

public enum DayPhase
{
    Pagi,
    Siang,
    Sore,
    Malam,
}

/// <summary>The weather states of the design document, from clear to thunderstorm.</summary>
public enum WeatherKind
{
    Cerah,
    Berawan,
    Gerimis,
    HujanSedang,
    HujanDeras,
    BadaiPetir,
}

/// <summary>A neighbourhood without power after a lightning strike.</summary>
public sealed class Blackout(Vector2 center, float radius, float remaining)
{
    public Vector2 Center { get; } = center;

    public float Radius { get; } = radius;

    /// <summary>Real seconds until the power returns.</summary>
    public float Remaining { get; internal set; } = remaining;
}

/// <summary>
/// Bogor is the "Rain City": the weather is a Markov chain biased toward afternoon rain, with
/// smooth intensities for rendering, mountain fog for Puncak, lightning and local blackouts.
/// </summary>
public sealed class WeatherSystem(uint seed)
{
    private readonly List<Blackout> _blackouts = [];
    private readonly List<(float Delay, float Volume)> _thunder = [];
    private Rng _rng = new(seed);
    private float _untilChange = 240f;
    private float _untilStrike = 8f;
    private float _mistTarget;

    public WeatherKind Kind { get; private set; } = WeatherKind.Berawan;

    /// <summary>Rain amount 0..1, eased toward the current state's target.</summary>
    public float Rain { get; private set; }

    /// <summary>Cloud cover 0..1.</summary>
    public float Clouds { get; private set; } = 0.5f;

    /// <summary>How wet the ground is, 0..1: rises in rain, dries slowly. Lowers tyre grip.</summary>
    public float Wetness { get; private set; }

    /// <summary>
    /// Ground mist 0..1 that sometimes rolls in with the rain, even in the lowlands. It thickens
    /// and lifts slowly, and clears once the rain has stopped.
    /// </summary>
    public float Mist { get; private set; }

    /// <summary>Wind strength 0..1; slants the rain.</summary>
    public float Wind { get; private set; }

    /// <summary>Brightness of the current lightning flash, 1 at the strike and decaying fast.</summary>
    public float Flash { get; private set; }

    public IReadOnlyList<Blackout> Blackouts => _blackouts;

    /// <summary>When true the chain does not advance by itself (missions pin the weather).</summary>
    public bool Locked { get; set; }

    /// <summary>Raised when lightning strikes: world position of the strike.</summary>
    public event Action<Vector2>? LightningStruck;

    /// <summary>Raised when the thunder of an earlier strike arrives: volume 0..1.</summary>
    public event Action<float>? Thunder;

    /// <summary>Raised when a blackout starts.</summary>
    public event Action<Blackout>? BlackoutStarted;

    public static float RainTarget(WeatherKind kind) => kind switch
    {
        WeatherKind.Gerimis => 0.22f,
        WeatherKind.HujanSedang => 0.55f,
        WeatherKind.HujanDeras => 0.85f,
        WeatherKind.BadaiPetir => 1f,
        _ => 0f,
    };

    public static float CloudTarget(WeatherKind kind) => kind switch
    {
        WeatherKind.Cerah => 0.15f,
        WeatherKind.Berawan => 0.6f,
        WeatherKind.Gerimis => 0.8f,
        _ => 1f,
    };

    /// <summary>Switches the weather now; intensities still ease in.</summary>
    public void Set(WeatherKind kind, float holdSeconds = 240f)
    {
        Kind = kind;
        _untilChange = holdSeconds;
        _untilStrike = MathF.Min(_untilStrike, 6f);
    }

    /// <summary>Jumps intensities to the current state (used when loading and for screenshots).</summary>
    public void Settle()
    {
        Rain = RainTarget(Kind);
        Clouds = CloudTarget(Kind);
        Wetness = Rain > 0f ? 1f : 0f;
        Mist = _mistTarget;
    }

    /// <summary>Calls the mist in (or sends it away) regardless of the dice; 0 clears it.</summary>
    public void SetMist(float amount) => _mistTarget = MathUtil.Saturate(amount);

    public bool IsBlackedOut(Vector2 p)
    {
        foreach (Blackout blackout in _blackouts)
        {
            if (Vector2.DistanceSquared(p, blackout.Center) < blackout.Radius * blackout.Radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fog amount 0..1 at a place: a light haze everywhere when it rains, and the thick
    /// "Kabut Puncak" above 700 m, heaviest at dawn, dusk and in rain.
    /// </summary>
    public float FogAt(float altitude, float hours)
    {
        float haze = MathF.Max(0.08f + (Rain * 0.3f), Mist);
        float high = MathUtil.SmoothStep(650f, 1_050f, altitude);
        float dawnDusk = MathF.Max(1f - MathUtil.SmoothStep(6f, 9.5f, hours), MathUtil.SmoothStep(15.5f, 18f, hours));
        float mountain = high * (0.35f + (0.45f * dawnDusk) + (0.3f * Clouds));
        return MathUtil.Saturate(MathF.Max(haze, mountain));
    }

    public void Update(float dt, GameClock clock, Vector2 focus)
    {
        if (!Locked)
        {
            _untilChange -= dt;
            if (_untilChange <= 0f)
            {
                Kind = Next(Kind, clock.Hours);
                _untilChange = _rng.Range(150f, 420f);
                // Not every shower brings mist: about two in five do, more often in the cool hours.
                bool cool = clock.Hours is < 9f or >= 16.5f;
                _mistTarget = Kind >= WeatherKind.Gerimis && _rng.Chance(cool ? 0.55f : 0.3f) ? _rng.Range(0.4f, 0.78f) : 0f;
            }
        }

        float rainTarget = RainTarget(Kind);
        Rain += (rainTarget - Rain) * MathUtil.Damp(rainTarget > Rain ? 0.35f : 0.2f, dt);
        Clouds += (CloudTarget(Kind) - Clouds) * MathUtil.Damp(0.25f, dt);
        Wind += ((Kind >= WeatherKind.HujanDeras ? 0.8f : Rain * 0.5f) - Wind) * MathUtil.Damp(0.3f, dt);
        Wetness = MathUtil.Saturate(Wetness + ((Rain > 0.05f ? Rain * 0.12f : -0.012f) * dt));
        if (Kind < WeatherKind.Gerimis)
        {
            _mistTarget = 0f;
        }

        Mist += (_mistTarget - Mist) * MathUtil.Damp(_mistTarget > Mist ? 0.06f : 0.035f, dt);
        Flash = MathF.Max(0f, Flash - (dt * 3.2f));

        if (Kind >= WeatherKind.HujanDeras)
        {
            _untilStrike -= dt;
            if (_untilStrike <= 0f)
            {
                Strike(focus);
                _untilStrike = Kind == WeatherKind.BadaiPetir ? _rng.Range(4f, 13f) : _rng.Range(22f, 55f);
            }
        }

        for (int i = _thunder.Count - 1; i >= 0; i--)
        {
            (float delay, float volume) = _thunder[i];
            delay -= dt;
            if (delay <= 0f)
            {
                _thunder.RemoveAt(i);
                Thunder?.Invoke(volume);
            }
            else
            {
                _thunder[i] = (delay, volume);
            }
        }

        for (int i = _blackouts.Count - 1; i >= 0; i--)
        {
            _blackouts[i].Remaining -= dt;
            if (_blackouts[i].Remaining <= 0f)
            {
                _blackouts.RemoveAt(i);
            }
        }
    }

    /// <summary>Forces a strike near <paramref name="focus"/>; <paramref name="blackout"/> forces the power cut too.</summary>
    public void Strike(Vector2 focus, bool? blackout = null)
    {
        float angle = _rng.Range(0f, MathUtil.Tau);
        float distance = _rng.Range(120f, 1_500f);
        Vector2 position = focus + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
        Flash = 1f;
        // Sound travels ~343 m/s: far strikes rumble late and quietly.
        _thunder.Add((distance / 343f, MathUtil.Saturate(1.15f - (distance / 1_700f))));
        LightningStruck?.Invoke(position);

        float chance = Kind == WeatherKind.BadaiPetir ? 0.22f : 0.08f;
        if ((blackout ?? _rng.Chance(chance)) && _blackouts.Count < 3)
        {
            Blackout cut = new(blackout == true ? focus : position, _rng.Range(350f, 600f), _rng.Range(60f, 150f));
            _blackouts.Add(cut);
            BlackoutStarted?.Invoke(cut);
        }
    }

    private WeatherKind Next(WeatherKind current, float hours)
    {
        // Afternoons are when Bogor earns its nickname.
        float wetBias = hours is >= 13f and < 19.5f ? 2.6f : hours is >= 19.5f or < 4f ? 1.2f : 0.55f;
        Span<float> weights = stackalloc float[6];
        for (int i = 0; i < weights.Length; i++)
        {
            int step = Math.Abs(i - (int)current);
            // Weather mostly drifts to a neighbouring state rather than jumping.
            float w = step switch { 0 => 0.6f, 1 => 3f, 2 => 1f, _ => 0.15f };
            weights[i] = w * (i >= (int)WeatherKind.Gerimis ? wetBias : 1f);
        }

        float total = 0f;
        foreach (float w in weights)
        {
            total += w;
        }

        float roll = _rng.NextFloat() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                return (WeatherKind)i;
            }
        }

        return current;
    }
}
