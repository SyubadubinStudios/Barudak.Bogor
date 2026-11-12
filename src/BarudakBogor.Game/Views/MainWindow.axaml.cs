using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Game.Audio;
using BarudakBogor.Game.Rendering;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;
using ThreeNet;
using ThreeNet.Avalonia;
using Key = Avalonia.Input.Key;

namespace BarudakBogor.Game.Views;

/// <summary>
/// The game window. It owns the frame loop: read input, step the <see cref="GameSession"/>,
/// then let the renderers and the HUD show the result. All game rules live in the core
/// library; this class only translates between the player, the simulation and the screen.
/// </summary>
public partial class MainWindow : Window
{
    private enum Screen
    {
        Loading,
        Title,
        Playing,
        Paused,
    }

    private readonly Settings _settings = Settings.Load();
    private readonly HashSet<Key> _down = [];
    private readonly HashSet<Key> _pressed = [];
    private readonly List<GameEvent> _events = [];
    private Screen _screen = Screen.Loading;
    private WorldMap? _map;
    private GameSession? _session;
    private Scene? _scene;
    private WorldRenderer? _world;
    private EntityRenderer? _entities;
    private Atmosphere? _atmosphere;
    private CameraRig? _camera;
    private GameAudio? _audio;
    private CharacterId _character = CharacterId.Ujang;
    private Point _lastPointer;
    private bool _looking;
    private double _titleTime;
    private float _hudTimer;
    private float _fps;
    private int _frames;
    private float _fpsTimer;
    private InputState? _scriptedInput;
    private double _gameMs;
    private bool _closed;
    private double _viewMs;

    public MainWindow()
    {
        InitializeComponent();
        Loc.Language = _settings.Language;

        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        InputLayer.PointerPressed += OnLookStart;
        InputLayer.PointerMoved += OnLookMove;
        InputLayer.PointerReleased += (_, e) =>
        {
            _looking = false;
            e.Pointer.Capture(null);
        };
        // Releasing every key avoids a stuck throttle when the window loses focus.
        Deactivated += (_, _) => _down.Clear();
        Closed += (_, _) => Shutdown();

        Viewport.EnableInteraction = false;
        // The view's own timer ticks on the 15.6 ms Windows timer grid, which rounds every frame up
        // to a multiple of it (a 20 ms frame becomes 31 ms). Frames are pumped from the display's
        // refresh instead; see PumpFrame.
        Viewport.IsRendering = false;
        Viewport.Frame += OnFrame;
        Viewport.RenderFailed += (_, message) => LoadingText.Text = message;

        WireMenus();
        Opened += async (_, _) => await LoadWorldAsync();
    }

    private bool Playing => _screen == Screen.Playing;

    // ------------------------------------------------------------------ start-up

    private async Task LoadWorldAsync()
    {
        LoadingText.Text = Ui.Loading.Text;
        LoadingTagline.Text = Loc.Tagline.Text;
        LoadingCredit.Text = Loc.CreditLine.Text.ToUpperInvariant();
        using (Stream icon = Avalonia.Platform.AssetLoader.Open(new Uri("avares://BarudakBogor/Assets/Art/icon_256.png")))
        {
            Icon = new WindowIcon(icon);
        }

        // The livery stripe sweeps across the bar until the city is ready.
        DispatcherTimer stripe = new() { Interval = TimeSpan.FromMilliseconds(16) };
        double sweep = -150;
        stripe.Tick += (_, _) =>
        {
            sweep = sweep > 300 ? -150 : sweep + 4.5;
            LoadingStripe.Margin = new Thickness(sweep, 0, 0, 0);
        };
        stripe.Start();
        // Let the launch art reach the screen before the heavy lifting starts.
        await Task.Delay(350);
        _map = await Task.Run(() => WorldMap.Generate());

        ThreeNetRuntime.Initialize();
        _scene = new Scene();
        ModelLibrary models = new(_scene);
        _world = new WorldRenderer(_scene, _map, models);
        _entities = new EntityRenderer(_scene, models);
        _atmosphere = new Atmosphere(_scene);
        _camera = new CameraRig(_scene);
        _audio = new GameAudio { Volume = _settings.Volume, Muted = _settings.Muted, Music = _settings.Music };

        StartSession(null);
        ApplyQuality();
        _world.LoadNow(_session!.Player.Ground);

        Viewport.Scene = _scene;
        RequestAnimationFrame(PumpFrame);
        Viewport.Camera = _camera.Node;
        ShowTitle();
        // Cross-fade from the painted Bogor to the living one.
        await Task.Delay(Program.CaptureDirectory is null && Program.BenchFile is null ? 900 : 50);
        LoadingScreen.Opacity = 0;
        LoadingScreen.IsHitTestVisible = false;
        await Task.Delay(750);
        stripe.Stop();
        LoadingScreen.IsVisible = false;
        if (Program.BenchFile is { } report)
        {
            StartBench(report);
        }
        else if (Program.CaptureDirectory is { } directory)
        {
            StartCapture(directory);
        }
    }

    /// <summary>Creates a fresh session, optionally restoring a save into it.</summary>
    private void StartSession(SaveData? save)
    {
        _session = new GameSession(_map!, save?.Character ?? _character, seed: (uint)Environment.TickCount);
        if (save is not null)
        {
            SaveSystem.Apply(_session, save);
        }

        MiniMap.Session = _session;
        BigMap.Session = _session;
        ClearToasts();
    }

    /// <summary>Renders one frame per display refresh for as long as the window lives.</summary>
    private void PumpFrame(TimeSpan time)
    {
        if (_closed)
        {
            return;
        }

        // Ask for the next refresh before doing the work, so a frame that finishes in time is not
        // made to wait an extra refresh for its successor to be scheduled.
        RequestAnimationFrame(PumpFrame);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        Viewport.RenderFrame();
        _viewMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void Shutdown()
    {
        _closed = true;
        if (_session is not null && _screen is Screen.Playing or Screen.Paused)
        {
            SaveGame(quiet: true);
        }

        _settings.Save();
        _world?.Dispose();
        _audio?.Dispose();
    }

    private void ApplyQuality()
    {
        (double scale, RendererOptions options, int radius, float rain, float crowd) = _settings.Quality switch
        {
            Quality.Hemat => (0.55, RendererOptions.Default with { MsaaSamples = 1, Fxaa = true, Shadows = false, Bloom = false }, 2, 0.5f, 0.6f),
            Quality.Tinggi => (1.0, RendererOptions.Default with
            {
                MsaaSamples = 4,
                Shadows = true,
                ShadowCascades = 3,
                ShadowMapSize = 2048,
                ShadowDistance = 150f,
                Bloom = true,
                Ssao = true,
                SsaoRadius = 0.8f,
                SsaoIntensity = 1.2f,
            }, 3, 1f, 1f),
            _ => (0.72, RendererOptions.Default with
            {
                MsaaSamples = 1,
                Fxaa = true,
                Shadows = true,
                ShadowCascades = 2,
                ShadowMapSize = 1024,
                ShadowDistance = 90f,
                Bloom = true,
            }, 3, 0.8f, 0.85f),
        };

        Viewport.RenderScale = scale;
        Viewport.RendererOptions = options with
        {
            BgraOutput = true,
            VSync = false,
            ToneMapping = ToneMapping.Aces,
            Exposure = 1.05f,
            BloomIntensity = 0.35f,
            BloomThreshold = 1.1f,
            ShadowSoftness = 1,
        };
        if (_world is not null)
        {
            _world.Radius = radius;
            _world.DetailRadius = radius;
        }

        if (_atmosphere is not null)
        {
            _atmosphere.RainDensity = rain;
        }

        if (_session is not null)
        {
            _session.Population.Density = crowd;
        }
    }

    // ------------------------------------------------------------------ frame

    private void OnFrame(object? sender, FrameEventArgs e)
    {
        if (_session is not { } session || _camera is not { } camera)
        {
            return;
        }

        float dt = MathF.Min(e.DeltaSeconds, 0.05f);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateCapture(dt, e.DeltaSeconds);
        if (_closed)
        {
            // The capture tour closed the window from inside this frame; everything is torn down.
            return;
        }

        switch (_screen)
        {
            case Screen.Title:
                if (_credits)
                {
                    UpdateCredits(dt);
                }

                // Attract mode: the world keeps living while the camera circles Tugu Kujang.
                _titleTime += dt;
                session.Update(dt, default);
                Poi monument = session.Map[PoiId.TuguKujang];
                float angle = (float)(_titleTime * 0.06) + 2.4f;
                Vector2 orbit = monument.Position + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 62f);
                float ground = session.Map.Terrain.HeightAt(monument.Position);
                camera.Node.Position = orbit.ToWorld(ground + 13f);
                camera.Node.LookAt(monument.Position.ToWorld(ground + 11f));
                camera.Node.Camera = Camera.Perspective(0.95f, 0.25f, 9_000f);
                Present(session, camera.Node.Position, dt);
                break;

            case Screen.Playing:
                InputState input = _scriptedInput ?? ReadInput(session, camera);
                HandleHotkeys(session, camera);
                session.Update(dt, input);
                camera.Update(session, dt);
                Present(session, camera.Position, dt);
                HandleEvents(session, camera);
                UpdateHud(session, dt);
                break;

            case Screen.Paused:
                Present(session, camera.Position, 0f);
                break;
        }

        _pressed.Clear();
        _gameMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _frames++;
        _fpsTimer += e.DeltaSeconds;
        if (_fpsTimer >= 0.5f)
        {
            _fps = _frames / _fpsTimer;
            _frames = 0;
            _fpsTimer = 0f;
        }
    }

    /// <summary>Pushes the simulation state to the scene.</summary>
    private void Present(GameSession session, Vector3 eye, float dt)
    {
        _world!.Update(session.Player.Ground);
        _atmosphere!.Update(session, eye, dt);
        bool dark = session.Weather.IsBlackedOut(session.Player.Ground);
        _world.SetConditions(session.Weather.Wetness, dark ? 0.04f : _atmosphere.NightGlow);
        _entities!.Update(session, eye, dt, _atmosphere.NightGlow);
        _audio!.Update(session, Playing);
    }

    private void HandleEvents(GameSession session, CameraRig camera)
    {
        _events.Clear();
        session.DrainEvents(_events);
        foreach (GameEvent gameEvent in _events)
        {
            _audio!.Handle(gameEvent);
            switch (gameEvent.Kind)
            {
                case GameEventKind.Toast:
                    Toast(gameEvent.Text.Text);
                    break;
                case GameEventKind.Money:
                    ShowMoneyDelta((long)gameEvent.Value);
                    break;
                case GameEventKind.MissionStarted:
                    ShowBanner(Ui.MissionStart.Text, gameEvent.Text.Text, string.Empty, good: true);
                    break;
                case GameEventKind.MissionCompleted:
                    ShowBanner(Ui.MissionDone.Text, gameEvent.Text.Text, "+" + Loc.Rupiah((long)gameEvent.Value), good: true);
                    SaveGame(quiet: true);
                    break;
                case GameEventKind.MissionFailed:
                    ShowBanner(Ui.MissionFailed.Text, gameEvent.Text.Text, string.Empty, good: false);
                    break;
                case GameEventKind.Ending:
                    ShowBanner(Ui.Ending.Text, Progress.CareerTitle((Career)(int)gameEvent.Value).Text, Ui.EndingDetail.Text, good: true, seconds: 8f);
                    break;
                case GameEventKind.Lightning:
                    _entities!.Lightning(gameEvent.Position);
                    break;
                case GameEventKind.Crash:
                    camera.Shake(MathUtil.Saturate(gameEvent.Value / 12f));
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ input

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (_down.Add(e.Key))
        {
            _pressed.Add(e.Key);
        }

        if (e.Key == Key.Escape)
        {
            if (_credits)
            {
                HideCredits();
            }
            else if (_screen == Screen.Paused)
            {
                Resume();
            }
            else if (Playing && _session is { Activity: null })
            {
                Pause(PauseTab.Map);
            }
        }
        else if (e.Key == Key.M && Playing && _session is { Activity: null, Dialog: null })
        {
            Pause(PauseTab.Map);
        }
        else if (e.Key == Key.F1 && Playing)
        {
            Pause(PauseTab.Help);
        }

        // Keep Space and Enter from clicking whichever button had focus last.
        if (Playing && e.Key is Key.Space or Key.Enter or Key.Tab)
        {
            e.Handled = true;
        }
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e) => _down.Remove(e.Key);

    private void OnLookStart(object? sender, PointerPressedEventArgs e)
    {
        if (Playing)
        {
            _looking = true;
            _lastPointer = e.GetPosition(this);
            e.Pointer.Capture(InputLayer);
            Viewport.Focus();
        }
    }

    private void OnLookMove(object? sender, PointerEventArgs e)
    {
        if (!_looking || _camera is null)
        {
            return;
        }

        Point now = e.GetPosition(this);
        Avalonia.Vector delta = now - _lastPointer;
        _lastPointer = now;
        // Dragging right turns the view right, which is toward negative yaw.
        _camera.Orbit((float)(-delta.X * 0.0055), (float)(delta.Y * 0.004));
    }

    private bool Down(Key key) => _down.Contains(key);

    private bool Pressed(Key key) => _pressed.Contains(key);

    private InputState ReadInput(GameSession session, CameraRig camera)
    {
        InputState input = new()
        {
            CameraYaw = camera.Yaw,
            Run = Down(Key.LeftShift) || Down(Key.RightShift),
            Jump = Pressed(Key.Space),
            Handbrake = Down(Key.Space),
            Horn = Down(Key.H),
            Interact = Pressed(Key.E),
            Action = Pressed(Key.Space) || Pressed(Key.Enter),
            ActionHeld = Down(Key.Space),
            Cancel = Pressed(Key.Escape) || Pressed(Key.Back),
        };

        float forward = (Down(Key.W) || Down(Key.Up) ? 1f : 0f) - (Down(Key.S) || Down(Key.Down) ? 1f : 0f);
        float left = (Down(Key.A) || Down(Key.Left) ? 1f : 0f) - (Down(Key.D) || Down(Key.Right) ? 1f : 0f);
        input.Move = new Vector2(left, forward);

        if (session.Dialog is { Choices.Count: > 0 })
        {
            ReadOnlySpan<Key> digits = [Key.D1, Key.D2, Key.D3, Key.D4];
            ReadOnlySpan<Key> pad = [Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4];
            for (int i = 0; i < digits.Length; i++)
            {
                if (Pressed(digits[i]) || Pressed(pad[i]))
                {
                    input.Lanes |= 1 << i;
                }
            }
        }
        else if (session.Activity is RhythmGame { Lanes: 2 })
        {
            input.Lanes = (Pressed(Key.A) || Pressed(Key.Left) ? 1 : 0) | (Pressed(Key.D) || Pressed(Key.Right) ? 2 : 0);
        }
        else if (session.Activity is RhythmGame)
        {
            input.Lanes = (Pressed(Key.A) ? 1 : 0) | (Pressed(Key.S) ? 2 : 0) | (Pressed(Key.D) ? 4 : 0) | (Pressed(Key.F) ? 8 : 0);
        }

        return input;
    }

    private void HandleHotkeys(GameSession session, CameraRig camera)
    {
        if (session.Dialog is not null || session.Activity is not null)
        {
            return;
        }

        if (Pressed(Key.H) && session.Player.Vehicle is { Spec.Motorised: true })
        {
            _audio!.Horn();
        }

        if (Pressed(Key.O))
        {
            session.Ojol.Toggle(session);
        }

        if (Pressed(Key.P))
        {
            TakePhoto(session, camera);
        }

        if (Pressed(Key.F5))
        {
            SaveGame(quiet: false);
        }

        if (Pressed(Key.F3))
        {
            _settings.ShowStats = !_settings.ShowStats;
        }
    }

    private void TakePhoto(GameSession session, CameraRig camera)
    {
        if (session.TakePhoto(camera.Position, camera.Forward) is not { } photo)
        {
            return;
        }

        string tags = string.Join(" · ", photo.Tags.Select(tag => tag.Text));
        Toast(photo.Payment > 0
            ? Ui.PhotoPaid.With(photo.Score, tags, Loc.Rupiah(photo.Payment)).Text
            : Ui.PhotoPlain.With(photo.Score, tags).Text);

        // Keep the picture: Pictures/BarudakBogor.
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "BarudakBogor");
            Directory.CreateDirectory(folder);
            Viewport.CaptureFrame()?.Save(Path.Combine(folder, $"bogor-{DateTime.Now:yyyyMMdd-HHmmss}.png"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Scoring the photo matters more than keeping the file.
        }
    }

    // ------------------------------------------------------------------ saving

    private void SaveGame(bool quiet)
    {
        if (_session is not { } session || session.Missions.Active is not null)
        {
            if (!quiet)
            {
                Toast(Ui.SaveBlocked.Text);
            }

            return;
        }

        try
        {
            SaveSystem.Save(session);
            if (!quiet)
            {
                Toast(Ui.Saved.Text);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Toast(Ui.SaveFailed.With(exception.Message).Text);
        }
    }

    /// <summary>Moves the player somewhere new and makes sure the world is there before the next frame.</summary>
    private void Relocate(GameSession session)
    {
        session.Population.Clear();
        _world!.LoadNow(session.Player.Ground);
        _camera!.Snap(session);
    }
}
