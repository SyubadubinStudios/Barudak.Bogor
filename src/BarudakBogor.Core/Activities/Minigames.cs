using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;

namespace BarudakBogor.Activities;

/// <summary>
/// A modal mini game: while one runs the player stands still and input goes to the activity.
/// The UI inspects the concrete type to draw the right panel.
/// </summary>
public abstract class Activity
{
    public abstract L Title { get; }

    /// <summary>One line telling the player what to press right now.</summary>
    public abstract L Hint { get; }

    public bool Finished { get; protected set; }

    public abstract void Update(GameSession session, float dt, in InputState input);

    /// <summary>Called when the player backs out early.</summary>
    public virtual void Cancel(GameSession session) => Finished = true;
}

public sealed record FishSpecies(L Name, float MinKg, float MaxKg, float Strength, float Weight, long PricePerKg)
{
    public static readonly FishSpecies[] All =
    [
        new(new("Ikan Mujair", "Tilapia"), 0.2f, 0.9f, 0.7f, 30f, 28_000),
        new(new("Ikan Nila", "Nile Tilapia"), 0.3f, 1.2f, 0.8f, 26f, 32_000),
        new(new("Ikan Mas", "Common Carp"), 0.5f, 2.6f, 1f, 20f, 40_000),
        new(new("Ikan Lele", "Catfish"), 0.4f, 2.2f, 1.1f, 16f, 30_000),
        new(new("Ikan Gurame", "Giant Gourami"), 0.8f, 3.5f, 1.3f, 7f, 65_000),
        new(new("Sendal Jepit", "A Flip-Flop"), 0.1f, 0.2f, 0.3f, 4f, 0),
        new(new("Lele Segede Bedug", "The Mosque-Drum Catfish"), 9f, 14f, 2.1f, 1f, 90_000),
    ];
}

public enum FishingState
{
    Ready,
    Waiting,
    Bite,
    Reeling,
    Landed,
    Escaped,
}

/// <summary>
/// Fishing: cast, wait for the float to dip, strike in time, then reel while keeping the line
/// tension inside the safe band. Drizzle makes fish bite sooner, as any Bogor angler will tell you.
/// </summary>
public sealed class FishingGame(uint seed, float skillBonus, float rain) : Activity
{
    /// <summary>Tension band in which reeling makes progress.</summary>
    public const float SafeMin = 0.25f;
    public const float SafeMax = 0.82f;

    private Rng _rng = new(seed);
    private float _timer;
    private float _surge;
    private float _slack;

    public FishingState State { get; private set; } = FishingState.Ready;

    /// <summary>Line tension 0..1; above 1 the line snaps.</summary>
    public float Tension { get; private set; }

    /// <summary>How far the fish has been reeled in, 0..1.</summary>
    public float Reeled { get; private set; }

    public FishSpecies? Fish { get; private set; }

    public float WeightKg { get; private set; }

    /// <summary>Catches landed in this sitting.</summary>
    public int Count { get; private set; }

    public float TotalKg { get; private set; }

    /// <summary>Set by the tournament mission: sitting ends by itself after this many seconds.</summary>
    public float? TimeLeft { get; set; }

    /// <summary>Raised for every landed catch.</summary>
    public event Action<FishSpecies, float>? Caught;

    public override L Title => new("Mancing", "Fishing");

    public override L Hint => State switch
    {
        FishingState.Ready => new("[Spasi] Lempar kail   [Esc] Udahan", "[Space] Cast   [Esc] Leave"),
        FishingState.Waiting => new("Sabar... tungguin pelampungnya", "Patience... watch the float"),
        FishingState.Bite => new("DISAMBER! [Spasi] Sentak!", "BITE! [Space] Strike!"),
        FishingState.Reeling => new("Tahan [Spasi] buat narik, jaga tegangan di zona hijau", "Hold [Space] to reel, keep tension in the green"),
        FishingState.Landed => new("Dapet! [Spasi] Lempar lagi", "Got it! [Space] Cast again"),
        _ => new("Lepas... [Spasi] Coba lagi", "It got away... [Space] Try again"),
    };

    public override void Update(GameSession session, float dt, in InputState input)
    {
        if (TimeLeft is { } left)
        {
            TimeLeft = left - dt;
            if (left - dt <= 0f)
            {
                Finished = true;
                return;
            }
        }

        if (input.Cancel)
        {
            Finished = true;
            return;
        }

        switch (State)
        {
            case FishingState.Ready:
            case FishingState.Landed:
            case FishingState.Escaped:
                if (input.Action)
                {
                    State = FishingState.Waiting;
                    // Rain and skill both shorten the wait.
                    _timer = _rng.Range(2.5f, 8f) / (1f + skillBonus + (rain * 0.6f));
                    session.Emit(new GameEvent(GameEventKind.Splash, L.Empty, session.Player.Position));
                }

                break;

            case FishingState.Waiting:
                _timer -= dt;
                if (input.Action)
                {
                    // Striking at nothing scares the fish.
                    _timer += 1.5f;
                }

                if (_timer <= 0f)
                {
                    State = FishingState.Bite;
                    _timer = 0.9f + (0.5f * skillBonus);
                    PickFish();
                }

                break;

            case FishingState.Bite:
                _timer -= dt;
                if (input.Action)
                {
                    State = FishingState.Reeling;
                    Tension = 0.45f;
                    Reeled = 0f;
                    _slack = 0f;
                }
                else if (_timer <= 0f)
                {
                    State = FishingState.Escaped;
                }

                break;

            case FishingState.Reeling:
                Reel(session, dt, input.ActionHeld);
                break;
        }
    }

    private void PickFish()
    {
        float total = 0f;
        foreach (FishSpecies species in FishSpecies.All)
        {
            total += species.Weight;
        }

        float roll = _rng.NextFloat() * total;
        Fish = FishSpecies.All[0];
        foreach (FishSpecies species in FishSpecies.All)
        {
            roll -= species.Weight;
            if (roll <= 0f)
            {
                Fish = species;
                break;
            }
        }

        // Skew toward small fish; the big ones are rare.
        float t = _rng.NextFloat();
        WeightKg = MathUtil.Lerp(Fish.MinKg, Fish.MaxKg, t * t);
    }

    private void Reel(GameSession session, float dt, bool held)
    {
        FishSpecies fish = Fish!;
        float strength = fish.Strength * (0.8f + (WeightKg / fish.MaxKg * 0.4f));

        // The fish makes sudden runs.
        _surge = MathF.Max(0f, _surge - dt);
        if (_surge <= 0f && _rng.Chance(0.6f * dt))
        {
            _surge = _rng.Range(0.5f, 1.2f);
        }

        float pull = (_surge > 0f ? 0.55f : 0.08f) * strength;
        Tension += ((held ? 0.62f : -0.75f) + pull) * dt;
        Tension = MathF.Max(0f, Tension);

        if (held && Tension is >= SafeMin and <= SafeMax)
        {
            Reeled += dt * (0.32f + (0.2f * skillBonus)) / strength;
        }
        else if (!held)
        {
            Reeled = MathF.Max(0f, Reeled - (dt * 0.05f * strength));
        }

        _slack = Tension < 0.1f ? _slack + dt : 0f;
        if (Tension >= 1f || _slack > 2.5f)
        {
            State = FishingState.Escaped;
            session.Emit(new GameEvent(GameEventKind.Splash, L.Empty, session.Player.Position));
            return;
        }

        if (Reeled >= 1f)
        {
            State = FishingState.Landed;
            Count++;
            TotalKg += WeightKg;
            Caught?.Invoke(fish, WeightKg);
            session.OnFishCaught(fish, WeightKg);
        }
    }
}

/// <summary>A note of a rhythm chart.</summary>
public readonly record struct Beat(float Time, int Lane);

public enum Judgement
{
    None,
    Hit,
    Perfect,
    Miss,
}

/// <summary>
/// Shared timing game behind kampung badminton (two lanes, return the shuttle) and karaoke
/// (four lanes, sing on the beat): press the lane key when the note reaches the line.
/// </summary>
public sealed class RhythmGame : Activity
{
    private readonly Beat[] _beats;
    private readonly bool[] _resolved;
    private readonly float _window;
    private readonly int _missLimit;
    private readonly L _title;
    private readonly Action<GameSession, RhythmGame> _onFinished;
    private int _first;

    public RhythmGame(L title, int lanes, Beat[] beats, float window, int missLimit, float approach, Action<GameSession, RhythmGame> onFinished)
    {
        _title = title;
        Lanes = lanes;
        _beats = beats;
        _resolved = new bool[beats.Length];
        _window = window;
        _missLimit = missLimit;
        Approach = approach;
        _onFinished = onFinished;
    }

    public int Lanes { get; }

    /// <summary>Seconds a note is visible before it must be hit.</summary>
    public float Approach { get; }

    public float Time { get; private set; } = -2f;

    public int Score { get; private set; }

    public int Combo { get; private set; }

    public int BestCombo { get; private set; }

    public int Hits { get; private set; }

    public int Misses { get; private set; }

    public int Total => _beats.Length;

    public Judgement Last { get; private set; }

    /// <summary>Seconds since <see cref="Last"/> was set, for fading the feedback.</summary>
    public float LastAge { get; private set; }

    public bool Won => Finished && (_missLimit <= 0 || Misses < _missLimit) && Hits * 2 >= Total;

    /// <summary>0..1 share of notes hit.</summary>
    public float Accuracy => Total == 0 ? 0f : Hits / (float)Total;

    public override L Title => _title;

    public override L Hint => Lanes == 2
        ? new("[A] kiri  [D] kanan — pukul pas kok sampai garis", "[A] left  [D] right — swing as the shuttle reaches the line")
        : new("[A] [S] [D] [F] — tekan pas not sampai garis", "[A] [S] [D] [F] — press as the note reaches the line");

    /// <summary>Visits notes currently on screen: (lane, seconds until it must be hit).</summary>
    public void VisibleBeats(List<(int Lane, float TimeToHit)> into)
    {
        into.Clear();
        for (int i = _first; i < _beats.Length; i++)
        {
            float until = _beats[i].Time - Time;
            if (until > Approach)
            {
                break;
            }

            if (!_resolved[i])
            {
                into.Add((_beats[i].Lane, until));
            }
        }
    }

    public override void Update(GameSession session, float dt, in InputState input)
    {
        if (input.Cancel)
        {
            Finished = true;
            return;
        }

        Time += dt;
        LastAge += dt;

        for (int lane = 0; lane < Lanes; lane++)
        {
            if ((input.Lanes & (1 << lane)) == 0)
            {
                continue;
            }

            // Closest unresolved note in this lane within the window.
            int best = -1;
            float bestError = _window;
            for (int i = _first; i < _beats.Length && _beats[i].Time - Time <= _window; i++)
            {
                float error = MathF.Abs(_beats[i].Time - Time);
                if (!_resolved[i] && _beats[i].Lane == lane && error <= bestError)
                {
                    best = i;
                    bestError = error;
                }
            }

            if (best >= 0)
            {
                _resolved[best] = true;
                bool perfect = bestError < _window * 0.4f;
                Hits++;
                Combo++;
                BestCombo = Math.Max(BestCombo, Combo);
                Score += (perfect ? 150 : 100) + (Math.Min(Combo, 20) * 5);
                Judge(session, perfect ? Judgement.Perfect : Judgement.Hit);
            }
            else
            {
                Combo = 0;
                Judge(session, Judgement.Miss);
            }
        }

        // Notes that slipped past the line.
        while (_first < _beats.Length && (_resolved[_first] || _beats[_first].Time < Time - _window))
        {
            if (!_resolved[_first])
            {
                _resolved[_first] = true;
                Misses++;
                Combo = 0;
                Judge(session, Judgement.Miss);
            }

            _first++;
        }

        bool lost = _missLimit > 0 && Misses >= _missLimit;
        if (lost || (_first >= _beats.Length && Time > (_beats.Length > 0 ? _beats[^1].Time : 0f) + 1f))
        {
            Finished = true;
            _onFinished(session, this);
        }
    }

    private void Judge(GameSession session, Judgement judgement)
    {
        Last = judgement;
        LastAge = 0f;
        session.Emit(new GameEvent(judgement == Judgement.Miss ? GameEventKind.BeatMiss : GameEventKind.BeatHit, L.Empty, default, Combo));
    }

    /// <summary>Kampung badminton: a rally that speeds up; three dropped shuttles and the set is lost.</summary>
    public static RhythmGame Badminton(uint seed, Action<GameSession, RhythmGame> onFinished)
    {
        Rng rng = new(seed);
        List<Beat> beats = [];
        float time = 1.5f;
        for (int i = 0; i < 30; i++)
        {
            beats.Add(new Beat(time, rng.Range(0, 2)));
            // The rally gets faster as it goes on.
            time += MathF.Max(0.55f, 1.25f - (i * 0.025f)) + rng.Range(0f, 0.2f);
        }

        return new RhythmGame(new("Badminton Kampung", "Kampung Badminton"), 2, [.. beats], 0.22f, 3, 1.4f, onFinished);
    }

    /// <summary>Karaoke: a 40 second dangdut chart on four lanes.</summary>
    public static RhythmGame Karaoke(uint seed, Action<GameSession, RhythmGame> onFinished)
    {
        Rng rng = new(seed);
        List<Beat> beats = [];
        const float beat = 60f / 112f;
        int lane = 0;
        for (float time = 2f; time < 40f; time += beat)
        {
            // Mostly on the beat, with off-beat pairs in the "reff".
            lane = (lane + rng.Range(1, 4)) % 4;
            beats.Add(new Beat(time, lane));
            if (time > 16f && rng.Chance(0.35f))
            {
                beats.Add(new Beat(time + (beat * 0.5f), (lane + 1) % 4));
            }
        }

        return new RhythmGame(new("Karaoke Dangdut", "Dangdut Karaoke"), 4, [.. beats], 0.18f, 0, 1.6f, onFinished);
    }
}
