using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Missions;
using BarudakBogor.World;

namespace BarudakBogor.Tests;

/// <summary>The flash drive chapters and the side missions, played by a bot that goes where the objective marker is.</summary>
[Collection("world")]
public sealed class NewMissionTests(WorldFixture fixture)
{
    private const float Step = 1f / 30f;

    private readonly WorldMap _map = fixture.Map;

    private GameSession Unlocked(params string[] done)
    {
        GameSession session = new(_map, CharacterId.Ujang, seed: 5);
        foreach (string id in done)
        {
            session.Progress.CompletedMissions.Add(id);
        }

        session.Missions.Refresh();
        return session;
    }

    /// <summary>Reads dialogue (always picking the second option) and jumps to each objective until the mission ends.</summary>
    private void Play(GameSession session, Mission mission)
    {
        for (int i = 0; i < 4_000 && session.Missions.Active == mission; i++)
        {
            if (session.Dialog is not null)
            {
                session.Update(Step, new InputState { Action = true, Lanes = 0b10 });
                continue;
            }

            if (session.Markers.FirstOrDefault(m => m.Kind == MarkerKind.Objective) is { } marker)
            {
                if (session.Player.Vehicle is { } vehicle)
                {
                    vehicle.Place(_map, marker.Position, vehicle.Yaw);
                }
                else
                {
                    session.Player.Teleport(_map, marker.Position, 0f);
                }
            }

            session.Update(Step, default);
        }
    }

    [Fact]
    public void FlashDriveStoryRunsToItsEnd()
    {
        GameSession session = Unlocked("prolog", "cilok");
        Mission flashdisk = session.Missions.Find("flashdisk");
        Mission meeting = session.Missions.Find("mufakat");
        Assert.Equal(MissionState.Available, flashdisk.State);
        Assert.Equal(MissionState.Locked, meeting.State);

        Assert.True(session.Missions.Start(flashdisk));
        Play(session, flashdisk);
        Assert.Equal(MissionState.Completed, flashdisk.State);
        Assert.DoesNotContain(session.Vehicles, v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied);
        Assert.Equal(MissionState.Available, meeting.State);

        long before = session.Progress.Money;
        Assert.True(session.Missions.Start(meeting));
        Play(session, meeting);
        Assert.Equal(MissionState.Completed, meeting.State);
        Assert.True(session.Progress.Money >= before + 1_000_000);
        Assert.False(session.Weather.Locked);
    }

    [Fact]
    public void GettingBoxedInAtTheGardensFailsTheFlashDriveMission()
    {
        GameSession session = Unlocked("prolog", "cilok");
        Mission flashdisk = session.Missions.Find("flashdisk");
        session.Missions.Start(flashdisk);
        // Play up to the moment the black cars appear, then stand still.
        for (int i = 0; i < 4_000 && !session.Vehicles.Any(v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied); i++)
        {
            if (session.Dialog is not null)
            {
                session.Update(Step, new InputState { Action = true, Lanes = 0b10 });
            }
            else
            {
                if (session.Markers.FirstOrDefault(m => m.Kind == MarkerKind.Objective) is { } marker)
                {
                    session.Player.Teleport(_map, marker.Position, 0f);
                }

                session.Update(Step, default);
            }
        }

        Assert.Contains(session.Vehicles, v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied);
        for (int i = 0; i < 30 * 40 && session.Missions.Active is not null; i++)
        {
            session.Update(Step, default);
        }

        Assert.Null(session.Missions.Active);
        Assert.Equal(MissionState.Available, flashdisk.State);
        Assert.DoesNotContain(session.Vehicles, v => v.Spec.Kind == VehicleKind.MobilKeluarga && v.Occupied);
    }

    [Fact]
    public void SideMissionsCanBeFinishedAndDoNotCountAsStory()
    {
        GameSession session = Unlocked("prolog");
        int story = session.Missions.CompletedCount;
        Assert.Equal(10, session.Missions.StoryCount);

        // Parcels: on the courier's motorbike.
        Mission parcels = session.Missions.Find("paket");
        int vehicles = session.Vehicles.Count;
        Assert.True(session.Missions.Start(parcels));
        for (int i = 0; i < 60 && session.Vehicles.Count == vehicles; i++)
        {
            session.Update(Step, new InputState { Action = true });
        }

        session.EnterVehicle(session.Vehicles[^1]);
        Play(session, parcels);
        Assert.Equal(MissionState.Completed, parcels.State);
        Assert.Equal(vehicles, session.Vehicles.Count);
        session.ExitVehicle(force: true);

        // The deer: on foot.
        Mission deer = session.Missions.Find("rusa");
        Assert.True(session.Missions.Start(deer));
        Play(session, deer);
        Assert.Equal(MissionState.Completed, deer.State);

        // The horse cart: placed gently from bend to bend.
        Mission cart = session.Missions.Find("delman");
        Vehicle delman = session.Vehicles.First(v => v.Spec.Kind == VehicleKind.Delman);
        session.Player.Teleport(_map, cart.StartSpot, 0f);
        Assert.True(session.Missions.Start(cart));
        for (int i = 0; i < 60 && session.Dialog is not null; i++)
        {
            session.Update(Step, new InputState { Action = true });
        }

        session.EnterVehicle(delman);
        Play(session, cart);
        Assert.Equal(MissionState.Completed, cart.State);

        Assert.Equal(story, session.Missions.CompletedCount);
    }

    [Fact]
    public void RushingTheHorseCartSpillsTheIceCream()
    {
        GameSession session = Unlocked("prolog");
        Mission cart = session.Missions.Find("delman");
        Vehicle delman = session.Vehicles.First(v => v.Spec.Kind == VehicleKind.Delman);
        session.Player.Teleport(_map, cart.StartSpot, 0f);
        session.Missions.Start(cart);
        for (int i = 0; i < 60 && session.Dialog is not null; i++)
        {
            session.Update(Step, new InputState { Action = true });
        }

        // A long straight road, so nothing but the driver decides the pace.
        RoadEdge straight = _map.Roads.Edges.First(e => e.Kind == RoadKind.Toll);
        Vector2 start = straight.Sample(straight.Length * 0.3f, out Vector2 along);
        delman.Place(_map, start, BarudakBogor.Numerics.MathUtil.YawOf(along));
        session.EnterVehicle(delman);
        float fastest = 0f;
        for (int i = 0; i < 30 * 40 && session.Missions.Active is not null; i++)
        {
            session.Update(Step, new InputState { Move = new Vector2(0f, 1f) });
            fastest = MathF.Max(fastest, delman.Speed);
        }

        Assert.True(fastest > 5f, $"the cart only reached {fastest} m/s");

        Assert.Null(session.Missions.Active);
        Assert.Equal(MissionState.Available, cart.State);
    }
}
