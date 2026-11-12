using System.Numerics;
using BarudakBogor.Localization;

namespace BarudakBogor.Gameplay;

/// <summary>Things the presentation layer reacts to (sound, toasts, effects). Drained once per frame.</summary>
public enum GameEventKind
{
    Toast,
    Money,
    MissionStarted,
    MissionCompleted,
    MissionFailed,
    ObjectiveChanged,
    Lightning,
    Thunder,
    Blackout,
    Horn,
    Crash,
    Splash,
    FishCaught,
    EnterVehicle,
    ExitVehicle,
    Dodge,
    Photo,
    BeatHit,
    BeatMiss,
    Pickup,
    Ending,
    Saved,
}

public readonly record struct GameEvent(GameEventKind Kind, L Text, Vector3 Position = default, float Value = 0f);

public enum MarkerKind
{
    MissionStart,
    Objective,
    Activity,
    Shop,
    Home,
    Passenger,
}

/// <summary>A beacon in the world and a blip on the map.</summary>
public sealed record Marker(MarkerKind Kind, Vector2 Position, L Label, float Radius = 3f);

/// <summary>Something the player can trigger by standing near it and pressing Interact.</summary>
public sealed class Interactable(Vector2 position, float radius, L prompt, Action action)
{
    public Vector2 Position { get; set; } = position;

    public float Radius { get; } = radius;

    public L Prompt { get; } = prompt;

    public Action Action { get; } = action;

    /// <summary>Optional gate, e.g. "only on foot" or "only when no mission is running".</summary>
    public Func<bool>? Available { get; init; }

    /// <summary>Marker shown for this spot, if any.</summary>
    public Marker? Marker { get; init; }
}

/// <summary>A line of conversation, optionally ending in a choice.</summary>
public sealed class Dialog(L speaker, L text, IReadOnlyList<L>? choices = null, Action<int>? onChoice = null)
{
    public L Speaker { get; } = speaker;

    public L Text { get; } = text;

    public IReadOnlyList<L> Choices { get; } = choices ?? [];

    public Action<int>? OnChoice { get; } = onChoice;
}

/// <summary>Everything the player pressed this frame, already independent of the device.</summary>
public struct InputState
{
    /// <summary>X: strafe / steer (positive = left), Y: forward / throttle.</summary>
    public Vector2 Move;

    /// <summary>Yaw of the camera, so "forward" on foot means away from the camera.</summary>
    public float CameraYaw;

    public bool Run;
    public bool Jump;
    public bool Handbrake;
    public bool Horn;

    /// <summary>Interact was pressed this frame (enter vehicle, talk, start activity).</summary>
    public bool Interact;

    /// <summary>The action key was pressed this frame (cast, hook, advance dialogue).</summary>
    public bool Action;

    /// <summary>The action key is held (reel in).</summary>
    public bool ActionHeld;

    /// <summary>Leave the current activity or mission prompt.</summary>
    public bool Cancel;

    /// <summary>Lane keys pressed this frame, bit i = lane i (rhythm games, dialogue choices).</summary>
    public int Lanes;
}

/// <summary>The five ways the story can end, one per career.</summary>
public enum Career
{
    Angkot,
    Bandrek,
    Kuliner,
    Mancing,
    Warga,
}

/// <summary>Money, reputation and counters: the part of the session that is saved.</summary>
public sealed class Progress
{
    public const int ReputationGoal = 100;

    private readonly int[] _reputation = new int[5];

    public long Money { get; private set; } = 50_000;

    public HashSet<string> CompletedMissions { get; } = [];

    public HashSet<Career> Endings { get; } = [];

    public int FishCaught { get; set; }

    public float BiggestFishKg { get; set; }

    public int PassengersCarried { get; set; }

    public int OjolOrders { get; set; }

    public int PhotosTaken { get; set; }

    public int BestPhotoScore { get; set; }

    public float DistanceDriven { get; set; }

    public event Action<long, long>? MoneyChanged;

    public event Action<Career>? EndingReached;

    public int Reputation(Career career) => _reputation[(int)career];

    public void SetReputation(Career career, int value) => _reputation[(int)career] = Math.Max(0, value);

    public void SetMoney(long amount) => Money = amount;

    public void Earn(long amount)
    {
        if (amount == 0)
        {
            return;
        }

        Money += amount;
        MoneyChanged?.Invoke(Money, amount);
    }

    public bool TrySpend(long amount)
    {
        if (amount > Money)
        {
            return false;
        }

        Earn(-amount);
        return true;
    }

    public void AddReputation(Career career, int amount)
    {
        int index = (int)career;
        _reputation[index] = Math.Max(0, _reputation[index] + amount);
        if (_reputation[index] >= ReputationGoal && Endings.Add(career))
        {
            EndingReached?.Invoke(career);
        }
    }

    public static L CareerTitle(Career career) => career switch
    {
        Career.Angkot => new("Raja Angkot", "Angkot King"),
        Career.Bandrek => new("Pengusaha Bandrek", "Bandrek Tycoon"),
        Career.Kuliner => new("Influencer Kuliner", "Food Influencer"),
        Career.Mancing => new("Pemancing Legendaris", "Legendary Angler"),
        _ => new("Walikota", "Mayor"),
    };

    public static L CareerHint(Career career) => career switch
    {
        Career.Angkot => new("Narik angkot dan antar penumpang.", "Drive the angkot and carry passengers."),
        Career.Bandrek => new("Jual bandrek dan besarkan usahamu.", "Sell bandrek and grow the business."),
        Career.Kuliner => new("Potret Bogor dan antar pesanan makanan.", "Photograph Bogor and deliver food orders."),
        Career.Mancing => new("Mancing ikan besar dan menangkan turnamen.", "Catch big fish and win the tournament."),
        _ => new("Tolong warga dan selesaikan misi cerita.", "Help the people and finish story missions."),
    };
}
