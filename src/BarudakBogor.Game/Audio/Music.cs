using BarudakBogor.Numerics;

namespace BarudakBogor.Game.Audio;

/// <summary>
/// Composes the background music in code. Two looping pieces share the five-note degung
/// (pelog) scale of Sundanese gamelan, so one can fade into the other without a clash:
/// "Angkot Jalan" is a bright dangdut-flavoured groove for the day, "Hujan Sore" a slow
/// gamelan piece for night and rain. Nothing is sampled or downloaded.
/// </summary>
public static class Music
{
    public const int Rate = 22_050;

    // Degung scale on D: da, mi, na, ti, la (semitones above the root).
    private static readonly int[] Scale = [0, 1, 5, 7, 8];
    private const float Root = 146.83f;

    private static float Pitch(int degree)
    {
        int octave = (int)MathF.Floor(degree / 5f);
        int step = degree - (octave * 5);
        return Root * MathF.Pow(2f, octave + (Scale[step] / 12f));
    }

    /// <summary>The daytime piece: 16 bars at 108 beats per minute.</summary>
    public static float[] Day() => Compose(108f, 16, lively: true, seed: 11);

    /// <summary>The night and rain piece: 8 slow bars at 66 beats per minute.</summary>
    public static float[] Night() => Compose(66f, 8, lively: false, seed: 29);

    /// <summary>
    /// The angkot's radio: a dangdut number in A minor at 126 beats per minute, with the
    /// "dang-dut" of the kendang, a walking bass, a mandolin tremolo and a suling on top.
    /// Thinned out afterwards to sound like a small speaker above the windscreen.
    /// </summary>
    public static float[] Dangdut()
    {
        const float tempo = 126f;
        const int bars = 16;
        float beat = 60f / tempo;
        float[] mix = new float[(int)(bars * 4 * beat * Rate)];
        Rng rng = new(47);
        // Harmonic minor on A; the chords go i - i - iv - V, the backbone of a thousand dangdut songs.
        int[] minor = [0, 2, 3, 5, 7, 8, 11];
        float Note(int degree)
        {
            int octave = (int)MathF.Floor(degree / 7f);
            return 220f * MathF.Pow(2f, octave + (minor[degree - (octave * 7)] / 12f));
        }

        int[] roots = [0, 0, 3, 4];
        int melody = 4;
        for (int bar = 0; bar < bars; bar++)
        {
            float start = bar * 4 * beat;
            int root = roots[bar % 4];

            // Bass: root, fifth on the push, octave to lead into the next bar.
            Pluck(mix, start, beat * 1.4f, Note(root - 14), 0.34f);
            Pluck(mix, start + (beat * 1.5f), beat, Note(root - 10), 0.26f);
            Pluck(mix, start + (beat * 2.5f), beat * 0.6f, Note(root - 14), 0.26f);
            Pluck(mix, start + (beat * 3f), beat * 0.9f, Note(root - 7), 0.3f);

            // Kendang: "dut" bends up on one, "dang" lands on four, "tak" fills the gaps.
            Drum(mix, start, 70f, 0.42f, bend: true);
            Tak(mix, start + (beat * 1f), 0.2f);
            Tak(mix, start + (beat * 1.5f), 0.14f);
            Tak(mix, start + (beat * 2f), 0.2f);
            Drum(mix, start + (beat * 2.5f), 150f, 0.2f, bend: false);
            Drum(mix, start + (beat * 3f), 96f, 0.4f, bend: false);
            Tak(mix, start + (beat * 3.5f), 0.16f);
            for (int i = 0; i < 8; i++)
            {
                Shaker(mix, start + (i * beat * 0.5f), i % 2 == 1 ? 0.07f : 0.03f);
            }

            // Mandolin: the chord in broken, trembling plucks on the off-beats.
            for (int i = 0; i < 4; i++)
            {
                int tone = root + (i % 2 == 0 ? 2 : 4);
                for (int k = 0; k < 3; k++)
                {
                    Pluck(mix, start + (i * beat) + (beat * 0.5f) + (k * beat * 0.125f), beat * 0.3f, Note(tone), 0.07f);
                }
            }

            // Suling: sings every other phrase, mostly by step, landing on a chord tone at the bar.
            if (bar / 4 % 2 == 0 || bar % 4 >= 2)
            {
                for (int half = 0; half < 4; half++)
                {
                    int move = rng.Range(0, 6) switch { 0 => -2, 1 or 2 => -1, 3 or 4 => 1, _ => 2 };
                    melody = half == 0 ? root + 7 + (rng.Chance(0.5f) ? 2 : 4) - 7 + 7 : Math.Clamp(melody + move, 5, 13);
                    float length = rng.Chance(0.3f) ? beat * 0.5f : beat;
                    // A quick grace note from the step below: the cengkok of dangdut singing.
                    Flute(mix, start + (half * beat), beat * 0.12f, Note(melody - 1), 0.08f);
                    Flute(mix, start + (half * beat) + (beat * 0.1f), length * 0.9f, Note(melody), 0.13f);
                    if (length < beat * 0.8f)
                    {
                        Flute(mix, start + (half * beat) + (beat * 0.5f), beat * 0.45f, Note(melody + 1), 0.11f);
                    }
                }
            }
        }

        // A small radio has no bass and a hard edge: take the lows out, then clip gently.
        float low = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < mix.Length; i++)
            {
                low += (mix[i] - low) * 0.035f;
                mix[i] = MathF.Tanh((mix[i] - (low * 0.85f)) * 1.6f) * 0.62f;
            }
        }

        return mix;
    }

    private static float[] Compose(float tempo, int bars, bool lively, uint seed)
    {
        float beat = 60f / tempo;
        int length = (int)(bars * 4 * beat * Rate);
        float[] mix = new float[length];
        Rng rng = new(seed);

        // One four-bar phrase, answered, varied and returned to: A A' B A.
        int[] phraseA = Phrase(ref rng, 2);
        int[] phraseB = Phrase(ref rng, 4);
        for (int bar = 0; bar < bars; bar++)
        {
            float start = bar * 4 * beat;
            int section = bar / 4 % 4;
            int[] phrase = section == 2 ? phraseB : phraseA;
            int inBar = bar % 4;

            // Gong marks the top of each phrase, as it marks the cycle in gamelan.
            if (inBar == 0)
            {
                Gong(mix, start, 0.5f);
            }

            // Bass: root and fifth, the "dang-dut" walk when lively.
            int bassDegree = inBar is 0 or 3 ? -5 : inBar == 1 ? -2 : -4;
            for (int b = 0; b < 4; b++)
            {
                if (lively || b % 2 == 0)
                {
                    Pluck(mix, start + (b * beat), beat * (lively ? 0.9f : 1.8f), Pitch(bassDegree + (lively && b == 2 ? 3 : 0)) * 0.5f, lively ? 0.3f : 0.2f);
                }
            }

            // Bonang: a rippling figure of metal bars around the phrase note.
            int steps = lively ? 8 : 4;
            for (int i = 0; i < steps; i++)
            {
                int note = phrase[(inBar * 2) + (i * 2 / steps)] + (i % 2 == 0 ? 0 : i % 4 == 1 ? 2 : -1);
                Metal(mix, start + (i * 4 * beat / steps), beat * (lively ? 1.2f : 2.6f), Pitch(note + 5), lively ? 0.09f : 0.12f);
            }

            // Suling: the bamboo flute carries the tune, resting on the second pass.
            if (section != 1 || lively)
            {
                for (int half = 0; half < 2; half++)
                {
                    int note = phrase[(inBar * 2) + half] + 5 + (section == 3 && half == 1 && inBar == 3 ? -5 : 0);
                    float duration = lively && rng.Chance(0.35f) ? beat : beat * 2f;
                    Flute(mix, start + (half * 2 * beat), duration * 0.95f, Pitch(note), lively ? 0.11f : 0.14f);
                    if (duration < beat * 1.5f)
                    {
                        Flute(mix, start + (half * 2 * beat) + beat, beat * 0.9f, Pitch(note + (rng.Chance(0.5f) ? 1 : -1)), 0.1f);
                    }
                }
            }

            if (lively)
            {
                // Kendang: the low "dut" bends up, the high "tak" answers; a fill closes each phrase.
                for (int b = 0; b < 4; b++)
                {
                    float t = start + (b * beat);
                    Drum(mix, t, b == 0 ? 78f : 96f, 0.34f, bend: b == 0);
                    Tak(mix, t + (beat * 0.5f), 0.16f);
                    if (b is 1 or 3)
                    {
                        Tak(mix, t + (beat * 0.75f), 0.1f);
                    }

                    Shaker(mix, t, 0.035f);
                    Shaker(mix, t + (beat * 0.5f), 0.05f);
                }

                if (inBar == 3)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Drum(mix, start + (3 * beat) + (i * beat * 0.25f), 110f + (i * 22f), 0.26f, bend: false);
                    }
                }
            }
        }

        // The loop wraps: let tails that ran off the end ring on over the start.
        return mix;
    }

    private static int[] Phrase(ref Rng rng, int start)
    {
        // Eight notes, moving mostly by step so it sings, ending where it can rest.
        int[] notes = new int[8];
        int note = start;
        for (int i = 0; i < notes.Length; i++)
        {
            notes[i] = note;
            int move = rng.Range(0, 5) switch { 0 => -2, 1 => -1, 2 => 1, 3 => 1, _ => 2 };
            note = Math.Clamp(note + move, -1, 7);
        }

        notes[^1] = start is 2 ? 0 : 2;
        return notes;
    }

    private static void Add(float[] mix, float start, float duration, Func<float, float> voice)
    {
        int first = (int)(start * Rate);
        int count = (int)(duration * Rate);
        for (int i = 0; i < count; i++)
        {
            // Notes that run past the end wrap round, which is what makes the loop seamless.
            mix[(first + i) % mix.Length] += voice(i / (float)Rate);
        }
    }

    private static void Pluck(float[] mix, float start, float duration, float frequency, float gain) =>
        Add(mix, start, duration, t =>
            (MathF.Sin(t * MathUtil.Tau * frequency) + (0.35f * MathF.Sin(t * MathUtil.Tau * frequency * 2f)))
            * gain * MathF.Exp(-t * 3.2f) * MathUtil.Saturate(t * 300f) * MathUtil.Saturate((duration - t) * 40f));

    private static void Metal(float[] mix, float start, float duration, float frequency, float gain) =>
        Add(mix, start, duration, t =>
            (MathF.Sin(t * MathUtil.Tau * frequency)
             + (0.5f * MathF.Sin(t * MathUtil.Tau * frequency * 2.76f) * MathF.Exp(-t * 6f))
             + (0.25f * MathF.Sin(t * MathUtil.Tau * frequency * 5.4f) * MathF.Exp(-t * 12f)))
            * gain * MathF.Exp(-t * 2.4f) * MathUtil.Saturate(t * 600f) * MathUtil.Saturate((duration - t) * 20f));

    private static void Flute(float[] mix, float start, float duration, float frequency, float gain)
    {
        uint breath = (uint)(start * 1000f) + 7u;
        Add(mix, start, duration, t =>
        {
            float vibrato = 1f + (0.006f * MathF.Sin(t * MathUtil.Tau * 5.2f) * MathUtil.Saturate(t * 3f));
            float tone = MathF.Sin(t * MathUtil.Tau * frequency * vibrato) + (0.22f * MathF.Sin(t * MathUtil.Tau * frequency * 2f * vibrato));
            float air = (Hash.Unit((int)(t * Rate), 0, breath) - 0.5f) * 0.25f;
            float envelope = MathUtil.Saturate(t * 14f) * MathUtil.Saturate((duration - t) * 9f);
            return (tone + air) * gain * envelope;
        });
    }

    private static void Gong(float[] mix, float start, float gain) =>
        Add(mix, start, 5f, t =>
            (MathF.Sin(t * MathUtil.Tau * Root * 0.5f) + (0.5f * MathF.Sin(t * MathUtil.Tau * Root * 0.5f * 1.51f)))
            * gain * 0.4f * MathF.Exp(-t * 0.9f) * MathUtil.Saturate(t * 80f) * (1f + (0.2f * MathF.Sin(t * 9f))));

    private static void Drum(float[] mix, float start, float frequency, float gain, bool bend) =>
        Add(mix, start, 0.3f, t =>
        {
            float pitch = bend ? frequency * (1f + (t * 1.6f)) : frequency * (1f + (0.6f * MathF.Exp(-t * 30f)));
            return MathF.Sin(t * MathUtil.Tau * pitch) * gain * MathF.Exp(-t * 13f) * MathUtil.Saturate(t * 500f);
        });

    private static void Tak(float[] mix, float start, float gain)
    {
        uint seed = (uint)(start * 977f) + 3u;
        Add(mix, start, 0.09f, t =>
            ((Hash.Unit((int)(t * Rate), 1, seed) - 0.5f) + (0.6f * MathF.Sin(t * MathUtil.Tau * 420f))) * gain * MathF.Exp(-t * 55f));
    }

    private static void Shaker(float[] mix, float start, float gain)
    {
        uint seed = (uint)(start * 611f) + 5u;
        Add(mix, start, 0.06f, t => (Hash.Unit((int)(t * Rate), 2, seed) - 0.5f) * gain * MathF.Exp(-t * 60f));
    }
}
