using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Sim;

namespace BarudakBogor.Gameplay;

/// <summary>Everything that survives between sessions. Plain data so it serialises without reflection.</summary>
public sealed class SaveData
{
    public int Version { get; set; } = 1;

    public CharacterId Character { get; set; }

    public float X { get; set; }

    public float Z { get; set; }

    public float Yaw { get; set; }

    public long Money { get; set; }

    public int[] Reputation { get; set; } = new int[5];

    public List<string> CompletedMissions { get; set; } = [];

    public List<Career> Endings { get; set; } = [];

    public int Day { get; set; } = 1;

    public float Hours { get; set; }

    public WeatherKind Weather { get; set; }

    public int FishCaught { get; set; }

    public float BiggestFishKg { get; set; }

    public int PassengersCarried { get; set; }

    public int OjolOrders { get; set; }

    public int PhotosTaken { get; set; }

    public int BestPhotoScore { get; set; }

    public float DistanceDriven { get; set; }

    public BusinessTier BusinessTier { get; set; }

    public int BandrekStock { get; set; }

    public long CupsSold { get; set; }

    public long PendingTakings { get; set; }

    public DateTime SavedAt { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

/// <summary>Captures a session into <see cref="SaveData"/>, restores it, and reads/writes the save file.</summary>
public static class SaveSystem
{
    /// <summary>Default save location: %AppData%/BarudakBogor/save.json (or the platform equivalent).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BarudakBogor", "save.json");

    public static SaveData Capture(GameSession session)
    {
        Progress progress = session.Progress;
        Vector2 position = session.Player.Ground;
        return new SaveData
        {
            Character = session.Player.Profile.Id,
            X = position.X,
            Z = position.Y,
            Yaw = session.Player.Yaw,
            Money = progress.Money,
            Reputation = [.. Enum.GetValues<Career>().Select(progress.Reputation)],
            CompletedMissions = [.. progress.CompletedMissions],
            Endings = [.. progress.Endings],
            Day = session.Clock.Day,
            Hours = session.Clock.Hours,
            Weather = session.Weather.Kind,
            FishCaught = progress.FishCaught,
            BiggestFishKg = progress.BiggestFishKg,
            PassengersCarried = progress.PassengersCarried,
            OjolOrders = progress.OjolOrders,
            PhotosTaken = progress.PhotosTaken,
            BestPhotoScore = progress.BestPhotoScore,
            DistanceDriven = progress.DistanceDriven,
            BusinessTier = session.Business.Tier,
            BandrekStock = session.Business.Stock,
            CupsSold = session.Business.CupsSold,
            PendingTakings = session.Business.Pending,
            SavedAt = DateTime.Now,
        };
    }

    /// <summary>Applies saved progress to a freshly created session.</summary>
    public static void Apply(GameSession session, SaveData data)
    {
        Progress progress = session.Progress;
        session.Player.Profile = CharacterProfile.Of(data.Character);
        session.Player.Teleport(session.Map, new Vector2(data.X, data.Z), data.Yaw);
        progress.SetMoney(data.Money);
        for (int i = 0; i < Math.Min(5, data.Reputation.Length); i++)
        {
            progress.SetReputation((Career)i, data.Reputation[i]);
        }

        progress.CompletedMissions.Clear();
        progress.CompletedMissions.UnionWith(data.CompletedMissions);
        progress.Endings.Clear();
        progress.Endings.UnionWith(data.Endings);
        progress.FishCaught = data.FishCaught;
        progress.BiggestFishKg = data.BiggestFishKg;
        progress.PassengersCarried = data.PassengersCarried;
        progress.OjolOrders = data.OjolOrders;
        progress.PhotosTaken = data.PhotosTaken;
        progress.BestPhotoScore = data.BestPhotoScore;
        progress.DistanceDriven = data.DistanceDriven;
        session.Clock.Set(data.Day, data.Hours);
        session.SyncPhase();
        session.Weather.Set(data.Weather);
        session.Weather.Settle();
        session.Business.Tier = data.BusinessTier;
        session.Business.Stock = data.BandrekStock;
        session.Business.CupsSold = data.CupsSold;
        session.Business.Pending = data.PendingTakings;
        session.Missions.Refresh();
    }

    public static string ToJson(SaveData data) => JsonSerializer.Serialize(data, SaveJsonContext.Default.SaveData);

    public static SaveData? FromJson(string json) => JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveData);

    public static void Save(GameSession session, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write to a temporary file first so a crash can never leave half a save behind.
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, ToJson(Capture(session)));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Reads the save file; null when there is none or it cannot be read.</summary>
    public static SaveData? Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return FromJson(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
