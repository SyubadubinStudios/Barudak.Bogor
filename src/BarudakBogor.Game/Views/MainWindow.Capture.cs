using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Game.Views;

/// <summary>
/// Screenshot tour for the documentation: <c>BarudakBogor --capture docs/images</c> plays a
/// scripted run through the game, saves a picture of the window at each stop and exits. The
/// shots come from the real game loop, so the docs can never show something the game cannot do.
/// </summary>
public partial class MainWindow
{
    private sealed record Shot(string Name, float Seconds, Action Setup);

    private readonly Queue<Shot> _shots = new();
    private string? _captureDirectory;
    private Shot? _shot;
    private float _shotTimer;
    private string? _benchFile;
    private int _benchFrames;
    private float _benchSeconds;
    private double _benchCpuMs;
    private double _benchGameMs;
    private double _benchViewMs;

    /// <summary>Drives the angkot along the ring road in heavy rain at each quality level and writes the frame rates.</summary>
    private void StartBench(string file)
    {
        _benchFile = Path.GetFullPath(file);
        _captureDirectory = Path.GetDirectoryName(_benchFile)!;
        File.WriteAllText(_benchFile, $"Barudak Bogor frame rate, {Bounds.Width:0} x {Bounds.Height:0} window{Environment.NewLine}");
        foreach (Quality quality in Enum.GetValues<Quality>())
        {
            _shots.Enqueue(new Shot("bench-" + quality, 12f, () =>
            {
                _settings.Quality = quality;
                _character = CharacterId.Ujang;
                StartPlaying(null);
                ClearToasts();
                GameSession s = _session!;
                s.Clock.Set(1, 16f);
                s.Weather.Locked = true;
                s.Weather.Set(WeatherKind.HujanDeras, 3_600f);
                s.Weather.Settle();
                s.EnterVehicle(s.Vehicles.First(v => v.Spec.Kind == VehicleKind.Angkot));
                _camera!.Snap(s);
                _scriptedInput = new InputState { Move = new Vector2(0f, 0.7f) };
                _benchFrames = 0;
                _benchSeconds = 0f;
                _benchCpuMs = 0;
                _benchGameMs = 0;
                _benchViewMs = 0;
            }));
        }
    }

    private void StartCapture(string directory)
    {
        _captureDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_captureDirectory);
        _settings.Quality = Quality.Tinggi;
        ApplyQuality();

        void Scene(float hours, WeatherKind weather)
        {
            GameSession s = _session!;
            s.Clock.Set(s.Clock.Day, hours);
            s.Weather.Locked = true;
            s.Weather.Set(weather, 3_600f);
            s.Weather.Settle();
        }

        void Stand(PoiId place, Vector2 offset, float yaw, float cameraYaw, float pitch = 0.2f)
        {
            GameSession s = _session!;
            Vector2 at = s.Map[place].RoadPoint + offset;
            s.Player.Teleport(s.Map, s.Map.ResolveCircle(at, 0.5f, out _), yaw);
            Relocate(s);
            _camera!.Yaw = cameraYaw;
            _camera.Pitch = pitch;
        }

        void SkipDialog()
        {
            GameSession s = _session!;
            for (int i = 0; i < 30 && s.Dialog is not null; i++)
            {
                s.Update(0.02f, new InputState { Action = true });
            }
        }

        _shots.Enqueue(new Shot("title", 5f, () => { }));
        _shots.Enqueue(new Shot("kredit", 9f, ShowCredits));
        _shots.Enqueue(new Shot("launch", 0.6f, () =>
        {
            HideCredits();
            LoadingScreen.Opacity = 1;
            LoadingScreen.IsVisible = true;
        }));
        _shots.Enqueue(new Shot("tugu-kujang", 5f, () =>
        {
            LoadingScreen.IsVisible = false;
            _character = CharacterId.Ujang;
            StartPlaying(null);
            ClearToasts();
            Scene(9.5f, WeatherKind.Cerah);
            Poi monument = _session!.Map[PoiId.TuguKujang];
            Vector2 away = Vector2.Normalize(monument.RoadPoint - monument.Position);
            Stand(PoiId.TuguKujang, away * 16f, MathUtil.YawOf(-away), MathUtil.YawOf(-away), 0.02f);
        }));
        _shots.Enqueue(new Shot("dialog-prolog", 1.5f, () =>
        {
            GameSession s = _session!;
            s.Missions.Start(s.Missions.Find("prolog"));
            SkipDialog();
            Vehicle own = s.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot).OrderBy(v => Vector2.Distance(v.Ground, s.Player.Ground)).First();
            s.EnterVehicle(own);
            s.Update(0.02f, default);
            _camera!.Snap(s);
        }));
        _shots.Enqueue(new Shot("pemberi-misi", 4f, () =>
        {
            // Mang Udin waiting at Pasar Anyar with his start marker.
            GameSession s = _session!;
            SkipDialog();
            s.Missions.Abandon();
            s.ExitVehicle(force: true);
            ClearToasts();
            s.Progress.CompletedMissions.Add("prolog");
            s.Missions.Refresh();
            Scene(9.2f, WeatherKind.Cerah);
            Missions.Mission cilok = s.Missions.Find("cilok");
            Vector2 back = MathUtil.Forward(cilok.GiverYaw);
            s.Player.Teleport(s.Map, cilok.GiverPosition + (back * 3.2f), MathUtil.YawOf(-back));
            Relocate(s);
            _camera!.Yaw = MathUtil.YawOf(-back) + 0.35f;
            _camera.Pitch = 0.08f;
        }));
        _shots.Enqueue(new Shot("mulai-lagi", 0.2f, () =>
        {
            GameSession s = _session!;
            s.Progress.CompletedMissions.Clear();
            s.Missions.Refresh();
            Poi monument = s.Map[PoiId.TuguKujang];
            s.Player.Teleport(s.Map, monument.RoadPoint, 0f);
            Relocate(s);
            s.Missions.Start(s.Missions.Find("prolog"));
        }));
        _shots.Enqueue(new Shot("kejar-kejaran", 7f, () =>
        {
            GameSession s = _session!;
            SkipDialog();
            Vehicle angkot = s.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot).OrderBy(v => Vector2.Distance(v.Ground, s.Player.Ground)).First();
            s.EnterVehicle(angkot);
            s.Update(0.02f, default);
            SkipDialog();
            _camera!.Snap(s);
            _scriptedInput = new InputState { Move = new Vector2(0f, 1f) };
        }));
        _shots.Enqueue(new Shot("pasar-anyar", 9f, () =>
        {
            _scriptedInput = null;
            GameSession s = _session!;
            s.Missions.Abandon();
            ClearToasts();
            s.ExitVehicle(force: true);
            Scene(10.5f, WeatherKind.Gerimis);
            Stand(PoiId.PasarAnyar, new Vector2(3f, -14f), 0.4f, 0.5f, 0.16f);
        }));
        _shots.Enqueue(new Shot("ramai-kota", 14f, () =>
        {
            // Midday on the ring road: the city at its busiest.
            _scriptedInput = null;
            Scene(12.2f, WeatherKind.Cerah);
            Poi monument = _session!.Map[PoiId.TuguKujang];
            Vector2 away = Vector2.Normalize(monument.RoadPoint - monument.Position);
            Vector2 along = MathUtil.Left(away);
            Stand(PoiId.TuguKujang, (away * 5f) + (along * 26f), MathUtil.YawOf(-along), MathUtil.YawOf(-along), 0.22f);
        }));
        _shots.Enqueue(new Shot("pengendara", 1.2f, () =>
        {
            // Line up a ridden motorbike, a delman and a becak in front of the camera.
            GameSession s = _session!;
            Poi monument = s.Map[PoiId.TuguKujang];
            Vector2 away = Vector2.Normalize(monument.RoadPoint - monument.Position);
            Vector2 along = MathUtil.Left(away);
            Vector2 centre = s.Player.Ground - (along * 9f);
            VehicleKind[] kinds = [VehicleKind.Motor, VehicleKind.Delman, VehicleKind.Becak];
            for (int i = 0; i < kinds.Length; i++)
            {
                Vehicle v = s.SpawnVehicle(kinds[i], centre + (away * ((i - 1) * 3.4f)), MathUtil.YawOf(along) + 0.5f);
                v.Occupied = true;
            }

            _camera!.Yaw = MathUtil.YawOf(-along);
            _camera.Pitch = 0.16f;
        }));
        _shots.Enqueue(new Shot("kabut-hujan", 10f, () =>
        {
            // The same streets when the afternoon rain brings mist with it.
            Scene(16.8f, WeatherKind.HujanSedang);
            _session!.Weather.SetMist(0.72f);
            _session.Weather.Settle();
            Stand(PoiId.PasarAnyar, new Vector2(3f, -14f), 0.4f, 0.5f, 0.14f);
        }));
        _shots.Enqueue(new Shot("gang-selot", 6f, () =>
        {
            _session!.Weather.SetMist(0f);
            Scene(16.2f, WeatherKind.Berawan);
            Stand(PoiId.GangSelot, new Vector2(0f, 6f), 2.6f, 2.9f, 0.3f);
        }));
        _shots.Enqueue(new Shot("mancing", 0.5f, () =>
        {
            GameSession s = _session!;
            Scene(7.2f, WeatherKind.Gerimis);
            Poi pond = s.Map[PoiId.Empang];
            s.Player.Teleport(s.Map, pond.Position, MathUtil.YawOf(s.Map.Ponds[0].Center - pond.Position));
            Relocate(s);
            _camera!.Yaw = s.Player.Yaw + 0.6f;
            _camera.Pitch = 0.3f;
            FishingGame game = new(8, 0.3f, 0.3f);
            s.StartActivity(game);
            // Cast, then wait for the bite so the gauge has something to show.
            for (int i = 0; i < 2_000 && game.State != FishingState.Reeling; i++)
            {
                s.Update(0.02f, new InputState { Action = game.State is FishingState.Ready or FishingState.Bite or FishingState.Escaped });
            }

            _scriptedInput = new InputState { ActionHeld = true };
        }));
        _shots.Enqueue(new Shot("karaoke", 6.5f, () =>
        {
            _scriptedInput = new InputState { Cancel = true };
            GameSession s = _session!;
            s.Update(0.02f, new InputState { Cancel = true });
            _scriptedInput = null;
            Scene(20.5f, WeatherKind.Cerah);
            Poi stage = s.Map[PoiId.Karaoke];
            Vector2 kerb = Vector2.Normalize(stage.Position - stage.RoadPoint);
            Stand(PoiId.Karaoke, (kerb * 5f) - (MathUtil.Left(kerb) * 10f), MathUtil.YawOf(kerb), MathUtil.YawOf(kerb) + 0.5f, 0.18f);
            s.StartActivity(RhythmGame.Karaoke(3, (_, _) => { }));
        }));
        _shots.Enqueue(new Shot("langit-malam", 5f, () =>
        {
            // A clear night: stars, the moon and drifting cloud over the old town.
            GameSession s = _session!;
            s.Update(0.02f, new InputState { Cancel = true });
            ClearToasts();
            Scene(22.5f, WeatherKind.Cerah);
            Poi monument = s.Map[PoiId.TuguKujang];
            Vector2 away = Vector2.Normalize(monument.RoadPoint - monument.Position);
            Stand(PoiId.TuguKujang, away * 14f, MathUtil.YawOf(-away), MathUtil.YawOf(-away), -0.2f);
        }));
        _shots.Enqueue(new Shot("kabut-puncak", 6f, () =>
        {
            GameSession s = _session!;
            s.Update(0.02f, new InputState { Cancel = true });
            ClearToasts();
            Scene(17.4f, WeatherKind.Gerimis);
            Poi villa = s.Map[PoiId.VillaMisterius];
            Vector2 away = Vector2.Normalize(villa.RoadPoint - villa.Position);
            Stand(PoiId.VillaMisterius, away * 4f, MathUtil.YawOf(-away), MathUtil.YawOf(-away), 0.1f);
        }));
        _shots.Enqueue(new Shot("malam-hujan", 7f, () =>
        {
            GameSession s = _session!;
            Scene(19.6f, WeatherKind.HujanDeras);
            Stand(PoiId.Karaoke, new Vector2(4f, 0f), 0f, 0f);
            Vehicle motor = s.SpawnVehicleAt(VehicleKind.Motor, PoiId.Karaoke, 18f);
            s.EnterVehicle(motor);
            s.Ojol.Toggle(s);
            ClearToasts();
            _camera!.Snap(s);
            _scriptedInput = new InputState { Move = new Vector2(0f, 0.55f) };
        }));
        _shots.Enqueue(new Shot("peta", 1.2f, () =>
        {
            _scriptedInput = null;
            Pause(PauseTab.Map);
            BigMap.Selected = PoiId.PuncakPass;
            SelectPlace(PoiId.PuncakPass);
        }));
        _shots.Enqueue(new Shot("misi", 0.8f, () =>
        {
            _tab = PauseTab.Missions;
            RefreshPause();
        }));
        _shots.Enqueue(new Shot("karier", 0.8f, () =>
        {
            _tab = PauseTab.Career;
            RefreshPause();
        }));
    }

    private void UpdateCapture(float dt, float realSeconds)
    {
        if (_captureDirectory is null)
        {
            return;
        }

        if (_shot is null)
        {
            if (!_shots.TryDequeue(out _shot))
            {
                _captureDirectory = null;
                _screen = Screen.Title;
                Close();
                return;
            }

            _shot.Setup();
            _shotTimer = _shot.Seconds;
            return;
        }

        _shotTimer -= dt > 0f ? dt : 0.016f;
        // The first seconds after a quality switch include renderer start-up; leave them out.
        if (_benchFile is not null && _shotTimer < _shot.Seconds - 3f)
        {
            // Real elapsed time: the simulation step is capped at 50 ms, which would hide anything below 20 fps.
            _benchFrames++;
            _benchSeconds += realSeconds;
            _benchCpuMs += Viewport.Stats.CpuTimeMs;
            _benchGameMs += _gameMs;
            _benchViewMs += _viewMs;
        }

        if (_shotTimer > 0f || (_world!.IsBusy && _benchFile is null))
        {
            return;
        }

        if (_benchFile is not null)
        {
            ThreeNet.FrameStats stats = Viewport.Stats;
            File.AppendAllText(_benchFile,
                $"{_shot.Name[6..],-7} {_benchFrames / MathF.Max(_benchSeconds, 0.01f),5:0.0} fps  {_benchSeconds * 1000f / Math.Max(_benchFrames, 1),5:0.0} ms/frame  work {_benchViewMs / Math.Max(_benchFrames, 1),4:0.0} ms (game {_benchGameMs / Math.Max(_benchFrames, 1),3:0.0})  renderer cpu {_benchCpuMs / Math.Max(_benchFrames, 1),4:0.0} ms  {stats.DrawCalls,4} draws  {stats.Triangles / 1000,4} k tris  render {Viewport.Renderer?.Width} x {Viewport.Renderer?.Height}  {Viewport.Renderer?.AdapterName}{Environment.NewLine}");
            _shot = null;
            return;
        }

        PixelSize size = new((int)Bounds.Width, (int)Bounds.Height);
        using RenderTargetBitmap bitmap = new(size, new Avalonia.Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(Path.Combine(_captureDirectory, _shot.Name + ".png"));
        _shot = null;
    }
}
