using System.Numerics;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.World;

namespace BarudakBogor.Missions;

public enum MissionState
{
    Locked,
    Available,
    Active,
    Completed,
}

/// <summary>
/// A story mission written as a linear script of steps (talk, go somewhere, do something).
/// Subclasses describe the script in <see cref="Script"/>; the base class runs it, keeps the
/// objective text and timer for the HUD, and cleans up whatever the mission spawned.
/// </summary>
public abstract class Mission
{
    private sealed record StepDef(L Objective, Func<Vector2?>? Target, Action? Enter, Func<float, bool> Done, Func<bool>? Condition, float Radius);

    private readonly List<StepDef> _steps = [];
    private readonly List<Action> _cleanup = [];
    private int _index = -1;

    public abstract string Id { get; }

    public abstract L Title { get; }

    public abstract L Summary { get; }

    /// <summary>Where the mission is picked up.</summary>
    public abstract PoiId StartPlace { get; }

    public abstract long RewardMoney { get; }

    public virtual Career RewardCareer => Career.Warga;

    public virtual int RewardReputation => 15;

    /// <summary>Model of the character (or thing) that hands out the mission, shown at its start place.</summary>
    public virtual string? GiverModel => null;

    /// <summary>Where the start marker stands.</summary>
    public Vector2 StartSpot { get; internal set; }

    /// <summary>Where the mission giver stands, a step back from the marker.</summary>
    public Vector2 GiverPosition { get; internal set; }

    /// <summary>Heading of the mission giver: facing the marker.</summary>
    public float GiverYaw { get; internal set; }

    /// <summary>True for a side mission: optional, and not counted toward finishing the story.</summary>
    public virtual bool IsSide => false;

    /// <summary>Mission that must be finished first, or null.</summary>
    public virtual string? Requires => "prolog";

    public MissionState State { get; internal set; } = MissionState.Locked;

    public L Objective { get; private set; }

    /// <summary>Seconds left on the mission clock, when the current phase is timed.</summary>
    public float? TimeLeft { get; protected set; }

    protected GameSession S { get; private set; } = null!;

    /// <summary>Reason shown when the timer runs out.</summary>
    protected virtual L TimeoutReason => new("Waktu habis!", "Out of time!");

    internal void Begin(GameSession session)
    {
        S = session;
        _steps.Clear();
        _cleanup.Clear();
        _index = -1;
        TimeLeft = null;
        Script();
        Advance();
    }

    internal void Update(float dt)
    {
        if (_index < 0 || _index >= _steps.Count)
        {
            return;
        }

        if (TimeLeft is { } left)
        {
            TimeLeft = left - dt;
            if (left - dt <= 0f)
            {
                S.Missions.Fail(TimeoutReason);
                return;
            }
        }

        Tick(dt);
        if (State != MissionState.Active)
        {
            return;
        }

        StepDef step = _steps[_index];
        if (step.Target?.Invoke() is { } target)
        {
            S.Markers.Add(new Marker(MarkerKind.Objective, target, step.Objective, step.Radius));
        }

        if (step.Done(dt))
        {
            Advance();
        }
    }

    internal void Cleanup()
    {
        foreach (Action action in _cleanup)
        {
            action();
        }

        _cleanup.Clear();
        TimeLeft = null;
        _index = -1;
    }

    /// <summary>Builds the step list. Called every time the mission starts.</summary>
    protected abstract void Script();

    /// <summary>Per-frame hook while the mission runs (pursuers, fail conditions).</summary>
    protected virtual void Tick(float dt)
    {
    }

    /// <summary>Registers something to undo when the mission ends, however it ends.</summary>
    protected void OnEnd(Action action) => _cleanup.Add(action);

    protected void Step(L objective, Func<Vector2?>? target, Action? enter, Func<float, bool> done, Func<bool>? when = null, float radius = 4f) =>
        _steps.Add(new StepDef(objective, target, enter, done, when, radius));

    /// <summary>Runs an action and moves on.</summary>
    protected void Do(Action action, Func<bool>? when = null) => Step(L.Empty, null, action, _ => true, when);

    /// <summary>Shows lines of dialogue and waits until the player has read them.</summary>
    protected void Talk(L speaker, L line) => TalkLines(null, speaker, [line]);

    protected void Talk(L speaker, L first, L second) => TalkLines(null, speaker, [first, second]);

    protected void Talk(L speaker, L first, L second, L third) => TalkLines(null, speaker, [first, second, third]);

    /// <summary>Dialogue that only plays when <paramref name="when"/> holds.</summary>
    protected void TalkWhen(Func<bool> when, L speaker, L line) => TalkLines(when, speaker, [line]);

    private void TalkLines(Func<bool>? when, L speaker, L[] lines) =>
        Step(L.Empty, null, () =>
        {
            foreach (L line in lines)
            {
                S.Say(speaker, line);
            }
        }, _ => S.Dialog is null, when);

    /// <summary>Asks a question; the step ends once an option is picked.</summary>
    protected void Ask(L speaker, L text, L[] options, Action<int> onChoice)
    {
        bool answered = false;
        Step(L.Empty, null, () =>
        {
            answered = false;
            S.ShowDialog(new Dialog(speaker, text, options, choice =>
            {
                onChoice(choice);
                answered = true;
            }));
        }, _ => answered);
    }

    /// <summary>Waits until the player is within <paramref name="radius"/> of a point (and <paramref name="also"/> holds).</summary>
    protected void Reach(L objective, Func<Vector2> target, float radius = 6f, Func<bool>? also = null, Func<bool>? when = null) =>
        Step(objective, () => target(), null,
            _ => Vector2.Distance(S.Player.Ground, target()) < radius && (also?.Invoke() ?? true), when, radius);

    /// <summary>Waits for a condition, with an optional marker.</summary>
    protected void Until(L objective, Func<bool> condition, Func<Vector2?>? target = null, Func<bool>? when = null) =>
        Step(objective, target, null, _ => condition(), when);

    /// <summary>A road point near <paramref name="place"/> shifted by an offset, for scripted locations.</summary>
    protected Vector2 RoadNear(PoiId place, Vector2 offset)
    {
        Vector2 wish = S.Map[place].RoadPoint + offset;
        return S.Map.Roads.Nearest(wish, 400f, e => e.Kind != RoadKind.Toll)?.Point ?? wish;
    }

    private void Advance()
    {
        while (true)
        {
            _index++;
            if (_index >= _steps.Count)
            {
                S.Missions.Complete();
                return;
            }

            StepDef step = _steps[_index];
            if (step.Condition is { } condition && !condition())
            {
                continue;
            }

            if (!step.Objective.IsEmpty)
            {
                Objective = step.Objective;
                S.Emit(new GameEvent(GameEventKind.ObjectiveChanged, step.Objective));
            }

            step.Enter?.Invoke();
            if (State != MissionState.Active)
            {
                return;
            }

            // Instant steps (Do) chain without waiting a frame.
            if (step.Target is null && step.Objective.IsEmpty && step.Done(0f))
            {
                continue;
            }

            return;
        }
    }
}

/// <summary>Owns the mission list, offers missions at their start places and tracks the active one.</summary>
public sealed class MissionManager
{
    private readonly GameSession _session;
    private readonly List<Mission> _missions;

    public MissionManager(GameSession session)
    {
        _session = session;
        _missions =
        [
            new PrologMission(), new CilokMission(), new NarikMission(), new BinaInsaniMission(),
            new StmMission(), new EmpangMission(), new BandrekMission(), new HororMission(),
            new FlashdiskMission(), new MufakatMission(),
            new PaketMission(), new RusaMission(), new DelmanMission(),
        ];

        foreach (Mission mission in _missions)
        {
            Mission captured = mission;
            Poi poi = session.Map[mission.StartPlace];
            // The marker sits at the kerb so it can be reached on foot or by car.
            Vector2 spot = poi.RoadPoint + ((poi.Position - poi.RoadPoint) * 0.6f) + (StartOffset(mission) * 1f);
            Vector2 inward = poi.Position - poi.RoadPoint;
            inward = inward.LengthSquared() > 0.01f ? Vector2.Normalize(inward) : Vector2.UnitY;
            mission.StartSpot = spot;
            mission.GiverPosition = session.Map.ResolveCircle(spot + (inward * 2.2f), 0.4f, out _);
            mission.GiverYaw = MathF.Atan2(-inward.X, -inward.Y);
            session.Interactables.Add(new Interactable(spot, 5f, new L("Mulai misi: {0}", "Start mission: {0}").With(mission.Title.Text), () => Start(captured))
            {
                Available = () => Active is null && captured.State == MissionState.Available,
                Marker = new Marker(MarkerKind.MissionStart, spot, mission.Title),
            });
        }

        Refresh();
    }

    public IReadOnlyList<Mission> All => _missions;

    public Mission? Active { get; private set; }

    /// <summary>Number of story missions finished.</summary>
    public int CompletedCount => _missions.Count(m => !m.IsSide && m.State == MissionState.Completed);

    /// <summary>Number of story missions there are (side missions not counted).</summary>
    public int StoryCount => _missions.Count(m => !m.IsSide);

    public Mission Find(string id) => _missions.First(m => m.Id == id);

    /// <summary>Recomputes which missions are available from the completed set.</summary>
    public void Refresh()
    {
        HashSet<string> done = _session.Progress.CompletedMissions;
        foreach (Mission mission in _missions)
        {
            if (mission == Active)
            {
                continue;
            }

            mission.State = done.Contains(mission.Id)
                ? MissionState.Completed
                : mission.Requires is null || done.Contains(mission.Requires) ? MissionState.Available : MissionState.Locked;
        }
    }

    public bool Start(Mission mission)
    {
        if (Active is not null || mission.State != MissionState.Available)
        {
            return false;
        }

        Active = mission;
        mission.State = MissionState.Active;
        _session.Emit(new GameEvent(GameEventKind.MissionStarted, mission.Title));
        mission.Begin(_session);
        return true;
    }

    public void Update(float dt) => Active?.Update(dt);

    /// <summary>Gives up the active mission; it can be started again.</summary>
    public void Abandon() => Fail(new("Misi dibatalkan.", "Mission abandoned."));

    internal void Fail(L reason)
    {
        if (Active is not { } mission)
        {
            return;
        }

        mission.Cleanup();
        Active = null;
        Refresh();
        _session.Emit(new GameEvent(GameEventKind.MissionFailed, reason));
    }

    internal void Complete()
    {
        if (Active is not { } mission)
        {
            return;
        }

        mission.Cleanup();
        Active = null;
        _session.Progress.CompletedMissions.Add(mission.Id);
        Refresh();
        long reward = (long)(mission.RewardMoney * (1f + _session.Player.Profile.StudyBonus));
        _session.Reward(reward, mission.RewardCareer, mission.RewardReputation);
        _session.Emit(new GameEvent(GameEventKind.MissionCompleted, mission.Title, default, reward));
        if (!mission.IsSide && CompletedCount == StoryCount)
        {
            // Finishing the whole story is what makes a mayor.
            _session.Progress.AddReputation(Career.Warga, Progress.ReputationGoal);
        }
    }

    /// <summary>Spreads start markers of missions that share a place.</summary>
    private Vector2 StartOffset(Mission mission)
    {
        int sameBefore = _missions.TakeWhile(m => m != mission).Count(m => m.StartPlace == mission.StartPlace);
        return new Vector2(sameBefore * 7f, 0f);
    }
}
