using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using ThreeNet;

namespace BarudakBogor.Game.Audio;

/// <summary>
/// All sound is synthesised at start-up: rain, engine, horn, thunder and interface blips.
/// If the machine has no audio device the game simply runs silent.
/// </summary>
public sealed class GameAudio : IDisposable
{
    private const int Rate = 44_100;

    private readonly AudioEngine? _engine;
    private readonly AudioClip? _horn;
    private readonly AudioClip? _thunder;
    private readonly AudioClip? _blip;
    private readonly AudioClip? _coin;
    private readonly AudioClip? _crash;
    private readonly AudioClip? _splash;
    private readonly AudioClip? _miss;
    private readonly AudioClip? _fanfare;
    private readonly SoundInstance? _rain;
    private readonly SoundInstance? _motor;
    private readonly SoundInstance? _musicDay;
    private readonly SoundInstance? _musicNight;
    private readonly SoundInstance? _radio;
    private float _radioGain;
    private float _dayGain;
    private float _nightGain;
    private float _volume = 0.8f;
    private bool _muted;

    public GameAudio()
    {
        try
        {
            _engine = AudioEngine.Open();
            _horn = Clip(0.45f, t => (Square(t * 392f) + Square(t * 494f)) * 0.16f * Envelope(t, 0.45f, 0.01f, 0.08f));
            _thunder = Noise(3.2f, 0.035f, (t, n) => n * 1.6f * MathF.Exp(-t * 1.25f) * (0.7f + (0.3f * MathF.Sin(t * 9f))));
            _blip = Clip(0.16f, t => MathF.Sin(t * MathUtil.Tau * (740f + (t * 2_400f))) * 0.3f * Envelope(t, 0.16f, 0.005f, 0.1f));
            _coin = Clip(0.32f, t => MathF.Sin(t * MathUtil.Tau * (t < 0.09f ? 988f : 1_319f)) * 0.26f * Envelope(t, 0.32f, 0.004f, 0.2f));
            _miss = Clip(0.2f, t => Square(t * (190f - (t * 300f))) * 0.14f * Envelope(t, 0.2f, 0.005f, 0.1f));
            _crash = Noise(0.5f, 0.2f, (t, n) => n * 1.1f * MathF.Exp(-t * 9f));
            _splash = Noise(0.6f, 0.5f, (t, n) => n * 0.5f * MathF.Exp(-t * 6f) * MathUtil.Saturate(t * 30f));
            _fanfare = Clip(1.1f, Fanfare);

            // Loops are built so their ends meet: whole numbers of cycles, or noise cross-faded onto itself.
            AudioClip rain = Noise(2.5f, 0.55f, (_, n) => n * 0.5f, loop: true);
            AudioClip motor = Clip(1f, t => ((Saw(t * 55f) * 0.5f) + (Saw(t * 110f) * 0.25f) + (Square(t * 27f) * 0.2f)) * 0.3f);
            _rain = _engine.Play(rain, SoundOptions.Flat(0f, loop: true));
            _motor = _engine.Play(motor, SoundOptions.Flat(0f, loop: true));
            _musicDay = _engine.Play(_engine.CreateClip(Audio.Music.Day(), 1, Audio.Music.Rate), SoundOptions.Flat(0f, loop: true));
            _musicNight = _engine.Play(_engine.CreateClip(Audio.Music.Night(), 1, Audio.Music.Rate), SoundOptions.Flat(0f, loop: true));
            _radio = _engine.Play(_engine.CreateClip(Audio.Music.Dangdut(), 1, Audio.Music.Rate), SoundOptions.Flat(0f, loop: true));
            Apply();
        }
        catch (Exception exception) when (exception is ThreeNetException or DllNotFoundException or EntryPointNotFoundException)
        {
            Unavailable = exception.Message;
            _engine = null;
        }
    }

    /// <summary>Why there is no sound, or null when audio works.</summary>
    public string? Unavailable { get; }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            Apply();
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            Apply();
        }
    }

    private void Apply()
    {
        if (_engine is not null)
        {
            _engine.MasterGain = _muted ? 0f : _volume;
        }
    }

    /// <summary>Continuous sounds follow the simulation every frame.</summary>
    /// <summary>Background music on or off (the sound effects stay).</summary>
    public bool Music { get; set; } = true;

    public void Update(GameSession session, bool playing)
    {
        if (_engine is null)
        {
            return;
        }

        // The bright piece plays by day in fair weather; night and rain bring the slow gamelan.
        // Both always run, so changing over is just a slow cross-fade.
        bool calm = !playing || session.Clock.IsNight || session.Weather.Rain > 0.35f;
        float level = Music ? (session.Activity is null ? 0.5f : 0.22f) : 0f;
        // In an angkot the driver's radio takes over: dangdut, a little too loud.
        bool radio = playing && Music && session.Player.Vehicle is { Spec.Kind: VehicleKind.Angkot } && session.Activity is null;
        _radioGain += ((radio ? 0.5f : 0f) - _radioGain) * 0.03f;
        float radioGain = _radioGain;
        _radio?.Update(o => o with { Gain = radioGain });
        level *= 1f - MathUtil.Saturate(_radioGain * 2f);
        _dayGain += (((calm ? 0f : level)) - _dayGain) * 0.012f;
        _nightGain += (((calm ? level : 0f)) - _nightGain) * 0.012f;
        float day = _dayGain;
        float night = _nightGain;
        _musicDay?.Update(o => o with { Gain = day });
        _musicNight?.Update(o => o with { Gain = night });

        float rain = playing ? session.Weather.Rain : session.Weather.Rain * 0.5f;
        _rain?.Update(o => o with { Gain = rain * 0.75f, Pitch = 0.9f + (rain * 0.25f) });
        Vehicle? vehicle = playing && session.Player.Vehicle is { Spec.Motorised: true } v ? v : null;
        float revs = vehicle?.Revs ?? 0f;
        float size = vehicle?.Spec.Kind == VehicleKind.Motor ? 1.5f : 1f;
        _motor?.Update(o => o with { Gain = vehicle is null ? 0f : 0.16f + (revs * 0.2f), Pitch = (0.7f + (revs * 1.5f)) * size });
    }

    public void Handle(in GameEvent gameEvent)
    {
        switch (gameEvent.Kind)
        {
            case GameEventKind.Thunder:
                Play(_thunder, 0.4f + (gameEvent.Value * 0.6f));
                break;
            case GameEventKind.Horn:
                Play(_horn, 0.5f);
                break;
            case GameEventKind.Crash:
                Play(_crash, MathUtil.Saturate(gameEvent.Value / 14f));
                break;
            case GameEventKind.Splash:
                Play(_splash, 0.6f);
                break;
            case GameEventKind.Money when gameEvent.Value > 0f:
            case GameEventKind.FishCaught:
                Play(_coin, 0.6f);
                break;
            case GameEventKind.MissionCompleted or GameEventKind.Ending:
                Play(_fanfare, 0.9f);
                break;
            case GameEventKind.MissionFailed or GameEventKind.BeatMiss:
                Play(_miss, 0.6f);
                break;
            case GameEventKind.BeatHit or GameEventKind.Pickup or GameEventKind.ObjectiveChanged or GameEventKind.Photo
                or GameEventKind.EnterVehicle or GameEventKind.MissionStarted:
                Play(_blip, 0.5f);
                break;
        }
    }

    /// <summary>Interface click.</summary>
    public void Click() => Play(_blip, 0.35f);

    /// <summary>The horn of the vehicle the player drives.</summary>
    public void Horn() => Play(_horn, 0.7f);

    private void Play(AudioClip? clip, float gain)
    {
        if (clip is not null && gain > 0.01f)
        {
            _engine!.Play(clip, SoundOptions.Flat(gain));
        }
    }

    /// <summary>A rising pelog-flavoured phrase, a small nod to Sundanese gamelan.</summary>
    private static float Fanfare(float t)
    {
        ReadOnlySpan<float> notes = [392f, 415f, 523f, 587f, 784f];
        int index = Math.Min((int)(t / 0.18f), notes.Length - 1);
        float local = t - (index * 0.18f);
        float tone = MathF.Sin(t * MathUtil.Tau * notes[index]) + (0.4f * MathF.Sin(t * MathUtil.Tau * notes[index] * 2.76f));
        return tone * 0.2f * MathF.Exp(-local * (index == notes.Length - 1 ? 3f : 7f));
    }

    private AudioClip Clip(float seconds, Func<float, float> wave)
    {
        float[] samples = new float[(int)(seconds * Rate)];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = wave(i / (float)Rate);
        }

        return _engine!.CreateClip(samples, 1, Rate);
    }

    /// <summary>Filtered noise; <paramref name="colour"/> is the low-pass amount (small = rumble, large = hiss).</summary>
    private AudioClip Noise(float seconds, float colour, Func<float, float, float> shape, bool loop = false)
    {
        int count = (int)(seconds * Rate);
        float[] samples = new float[count];
        Rng rng = new(1234);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            low += (((rng.NextFloat() * 2f) - 1f) - low) * colour;
            samples[i] = shape(i / (float)Rate, low * (1f + ((1f - colour) * 4f)));
        }

        if (loop)
        {
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                samples[i] = (samples[i] * t) + (samples[count - fade + i] * (1f - t));
            }

            Array.Resize(ref samples, count - fade);
        }

        return _engine!.CreateClip(samples, 1, Rate);
    }

    private static float Square(float cycles) => (cycles - MathF.Floor(cycles)) < 0.5f ? 1f : -1f;

    private static float Saw(float cycles) => ((cycles - MathF.Floor(cycles)) * 2f) - 1f;

    private static float Envelope(float t, float length, float attack, float release) =>
        MathUtil.Saturate(t / attack) * MathUtil.Saturate((length - t) / release);

    public void Dispose() => _engine?.Dispose();
}
