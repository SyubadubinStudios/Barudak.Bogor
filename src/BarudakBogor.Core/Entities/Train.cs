using System.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Entities;

public enum TrainState
{
    /// <summary>Standing at the platform with the doors open.</summary>
    AtStation,
    /// <summary>Running north, away from Bogor.</summary>
    Outbound,
    /// <summary>Out of sight up the line.</summary>
    Away,
    /// <summary>Coming back to the terminus.</summary>
    Inbound,
}

/// <summary>
/// The commuter train. It shuttles between the platform at Stasiun Bogor and a point far up
/// the line, and it always stops for a player on the track: nobody gets hurt in Bogor.
/// </summary>
public sealed class Train(Corridor track, IReadOnlyList<LevelCrossing> crossings)
{
    public const int Cars = 4;
    public const float CarLength = 20f;

    /// <summary>Half the width of a car body.</summary>
    public const float HalfWidth = 1.5f;

    private const float StationTail = 14f;
    private const float Cruise = 19f;
    private const float Acceleration = 0.9f;
    private const float Braking = 1f;
    private const float EmergencyBraking = 3f;

    private readonly float _farTail = MathF.Min(3_400f, track.Length - (Cars * CarLength) - 40f);
    private readonly bool[] _announced = new bool[crossings.Count];
    private float _timer = 20f;
    private float _honk;

    public Corridor Track { get; } = track;

    public TrainState State { get; private set; } = TrainState.AtStation;

    /// <summary>Distance along the track of the train's southern end.</summary>
    public float Tail { get; private set; } = StationTail;

    public float Length => Cars * CarLength;

    /// <summary>Signed speed in m/s: positive away from Bogor.</summary>
    public float Speed { get; private set; }

    /// <summary>True while the driver is braking for someone on the track.</summary>
    public bool Blocked { get; private set; }

    /// <summary>Raised when the horn sounds, with the position of the leading end.</summary>
    public event Action<Vector2>? Honked;

    /// <summary>Puts the train back at the platform with its doors open, as at the start of a day.</summary>
    public void Reset()
    {
        State = TrainState.AtStation;
        Tail = StationTail;
        Speed = 0f;
        Blocked = false;
        _timer = 20f;
    }

    /// <summary>Position, heading and rail level of the middle of car <paramref name="index"/>.</summary>
    public Vector2 CarPose(int index, out Vector2 direction, out float level) =>
        Track.Sample(Tail + ((index + 0.5f) * CarLength), out direction, out level);

    /// <summary>True while the barrier of a level crossing is down: the train is near and moving, or standing on it.</summary>
    public bool Closes(in LevelCrossing crossing)
    {
        if (State is TrainState.AtStation or TrainState.Away && MathF.Abs(Speed) < 0.1f)
        {
            return crossing.Along > Tail - 4f && crossing.Along < Tail + Length + 4f;
        }

        return crossing.Along > Tail - 150f && crossing.Along < Tail + Length + 150f;
    }

    /// <param name="dt">Step in seconds.</param>
    /// <param name="person">Where the player is.</param>
    /// <param name="radius">How much room the player (or their vehicle) takes up.</param>
    public void Update(float dt, Vector2 person, float radius)
    {
        _honk = MathF.Max(0f, _honk - dt);
        float direction = State switch { TrainState.Outbound => 1f, TrainState.Inbound => -1f, _ => 0f };
        float speed = MathF.Abs(Speed);

        switch (State)
        {
            case TrainState.AtStation:
            case TrainState.Away:
                Speed = 0f;
                Blocked = false;
                _timer -= dt;
                if (_timer <= 0f)
                {
                    State = State == TrainState.AtStation ? TrainState.Outbound : TrainState.Inbound;
                    Array.Clear(_announced);
                    Honk();
                }

                return;
        }

        // How far the leading end may still run: to the end of this leg, or to whoever is on the track.
        float lead = direction > 0f ? Tail + Length : Tail;
        float room = direction > 0f ? _farTail - Tail : Tail - StationTail;
        float brake = Braking;
        Blocked = false;
        if (Track.Closest(person, HalfWidth + radius + 0.8f, out _, out float at, out _))
        {
            float gap = ((at - lead) * direction) - radius - 6f;
            float stopping = (speed * speed / (2f * Braking)) + 25f;
            if ((at - lead) * direction > -1f && gap < stopping)
            {
                Blocked = true;
                brake = EmergencyBraking;
                room = MathF.Min(room, MathF.Max(0f, gap));
                if (_honk <= 0f)
                {
                    Honk();
                    _honk = 3f;
                }
            }
        }

        // The fastest the train may go and still stop in the room it has.
        float allowed = MathF.Min(Cruise, MathF.Sqrt(2f * brake * MathF.Max(0f, room)));
        speed = allowed < speed ? MathF.Max(allowed, speed - (EmergencyBraking * 1.5f * dt)) : MathF.Min(allowed, speed + (Acceleration * dt));
        if (Blocked && room <= 0.05f)
        {
            speed = 0f;
        }

        float step = MathF.Min(speed * dt, MathF.Max(0f, room));
        Tail += step * direction;
        Speed = speed * direction;

        for (int i = 0; i < crossings.Count; i++)
        {
            if (!_announced[i] && MathF.Abs(crossings[i].Along - lead) < 170f && (crossings[i].Along - lead) * direction > 0f)
            {
                _announced[i] = true;
                Honk();
            }
        }

        bool arrived = direction > 0f ? Tail >= _farTail - 0.3f : Tail <= StationTail + 0.3f;
        if (arrived && speed < 0.6f)
        {
            Tail = direction > 0f ? _farTail : StationTail;
            Speed = 0f;
            State = direction > 0f ? TrainState.Away : TrainState.AtStation;
            _timer = direction > 0f ? 25f : 30f;
        }
    }

    private void Honk() => Honked?.Invoke(Track.Sample(State == TrainState.Inbound ? Tail : Tail + Length, out _, out _));
}
