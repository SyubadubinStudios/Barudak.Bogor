using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

/// <summary>The town's daily rhythm: who is out when, umbrellas, small talk.</summary>
[Collection("world")]
public sealed class LifeTests(WorldFixture fixture)
{
    private const float Step = 1f / 30f;

    private readonly WorldMap _map = fixture.Map;

    private GameSession SessionAt(float hours, WeatherKind weather)
    {
        GameSession session = new(_map, CharacterId.Ujang, seed: 11);
        session.Clock.Set(1, hours);
        session.SyncPhase();
        session.Weather.Locked = true;
        session.Weather.Set(weather, 3_600f);
        session.Weather.Settle();
        return session;
    }

    private static void Run(GameSession session, float seconds)
    {
        for (float t = 0f; t < seconds; t += Step)
        {
            session.Update(Step, default);
        }
    }

    [Fact]
    public void StreetsFollowTheClock()
    {
        GameSession midday = SessionAt(12.6f, WeatherKind.Cerah);
        Run(midday, 12f);
        GameSession small = SessionAt(2.5f, WeatherKind.Cerah);
        Run(small, 12f);
        int busy = midday.Population.Pedestrians.Count;
        int quiet = small.Population.Pedestrians.Count;
        Assert.True(busy >= 20, $"only {busy} people out at midday");
        Assert.True(quiet * 3 < busy, $"{quiet} people out at 2.30 am against {busy} at midday");

        // Nobody's children are out in the small hours, and at ten they are in class.
        Assert.DoesNotContain(small.Population.Pedestrians, p => p.Kind is NpcKind.AnakSd or NpcKind.AnakSekolah);
        GameSession lesson = SessionAt(10f, WeatherKind.Cerah);
        Run(lesson, 12f);
        int children = lesson.Population.Pedestrians.Count(p => p.Kind is NpcKind.AnakSd or NpcKind.AnakSekolah);
        Assert.True(children * 8 <= lesson.Population.Pedestrians.Count, $"{children} children out during lessons");

        // The going-home bell fills the pavement with them.
        GameSession bell = SessionAt(13f, WeatherKind.Cerah);
        Run(bell, 12f);
        int after = bell.Population.Pedestrians.Count(p => p.Kind is NpcKind.AnakSd or NpcKind.AnakSekolah);
        Assert.True(after >= 5 && after > children, $"{after} children out after school against {children} during lessons");
    }

    [Fact]
    public void ScheduleCoversTheWholeDay()
    {
        // Someone must be allowed out at every hour, or the spawner would have nobody to place.
        for (float hours = 0f; hours < 24f; hours += 0.5f)
        {
            Assert.True(Enum.GetValues<NpcKind>().Any(kind => Population.ScheduleFactor(kind, hours) >= 1f), $"nobody is out at {hours}");
            Assert.InRange(Population.CrowdFactor(hours), 0.1f, 1f);
        }
    }

    [Fact]
    public void UmbrellasGoUpInTheRainAndDownAfterwards()
    {
        GameSession session = SessionAt(12f, WeatherKind.HujanDeras);
        Run(session, 12f);
        int people = session.Population.Pedestrians.Count;
        int umbrellas = session.Population.Pedestrians.Count(p => p.Umbrella);
        Assert.True(people >= 8, $"only {people} people out in the rain");
        Assert.True(umbrellas >= people / 4 && umbrellas < people, $"{umbrellas} umbrellas among {people} people");

        session.Weather.Set(WeatherKind.Cerah, 3_600f);
        session.Weather.Settle();
        Run(session, 1f);
        Assert.DoesNotContain(session.Population.Pedestrians, p => p.Umbrella);
    }

    [Fact]
    public void PassersBySpeakToAPlayerOnFoot()
    {
        GameSession session = SessionAt(9f, WeatherKind.Cerah);
        List<GameEvent> events = [];
        Run(session, 90f);
        session.DrainEvents(events);
        GameEvent[] chatter = [.. events.Where(e => e.Kind == GameEventKind.Chatter)];
        Assert.NotEmpty(chatter);
        Assert.All(chatter, e => Assert.False(string.IsNullOrWhiteSpace(e.Text.Id) || string.IsNullOrWhiteSpace(e.Text.En)));
        // Not a running commentary: a few lines in a minute and a half at most.
        Assert.True(chatter.Length <= 5, $"{chatter.Length} lines in 90 s");
    }
}
