using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Missions;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Game.Views;

public partial class MainWindow
{
    private enum PauseTab
    {
        Map,
        Missions,
        Career,
        Settings,
        Help,
    }

    private PauseTab _tab = PauseTab.Map;
    private SaveData? _save;

    private void WireMenus()
    {
        ContinueButton.Click += (_, _) => StartPlaying(_save);
        NewGameButton.Click += (_, _) => StartPlaying(null);
        LanguageButton.Click += (_, _) => ToggleLanguage();
        TitleSettingsButton.Click += (_, _) => CycleQuality();
        QuitButton.Click += (_, _) => Close();
        CreditsButton.Click += (_, _) => ShowCredits();
        CreditsCloseButton.Click += (_, _) => HideCredits();
        ResumeButton.Click += (_, _) => Resume();
        SaveButton.Click += (_, _) =>
        {
            SaveGame(quiet: true);
            SaveButton.Content = _session?.Missions.Active is null ? Ui.Saved.Text : Ui.SaveBlocked.Text;
        };
        ToTitleButton.Click += (_, _) =>
        {
            SaveGame(quiet: true);
            ShowTitle();
        };
        BigMap.PlaceSelected += SelectPlace;
        FastTravelButton.Click += (_, _) =>
        {
            if (BigMap.Selected is { } place && _session is { } session && session.FastTravel(place))
            {
                Relocate(session);
                Resume();
            }
            else if (_session is not null)
            {
                // The reason (mission running, not enough money) arrives as a toast; show it on the panel too.
                _events.Clear();
                _session.DrainEvents(_events);
                MapDetail.Text = _events.LastOrDefault(e => e.Kind == GameEventKind.Toast).Text.Text;
            }
        };
    }

    // ------------------------------------------------------------------ title

    private void ShowTitle()
    {
        _audio?.Click();
        _screen = Screen.Title;
        _save = SaveSystem.Load();
        // A throwaway session gives the title its mood: an afternoon shower over Tugu Kujang.
        StartSession(null);
        _session!.Clock.Set(1, 17.1f);
        _session.Weather.Locked = true;
        _session.Weather.Set(WeatherKind.HujanSedang);
        _session.Weather.Settle();
        _session.Population.Density = 0.8f;
        _world!.LoadNow(_session.Player.Ground);

        PauseScreen.IsVisible = false;
        Hud.IsVisible = false;
        DialogPanel.IsVisible = false;
        ActivityPanel.IsVisible = false;
        TitleScreen.IsVisible = true;
        if (_save is not null)
        {
            _character = _save.Character;
        }

        RefreshTitle();
    }

    private void RefreshTitle()
    {
        TitleEyebrow.Text = Ui.Eyebrow.Text;
        TaglineText.Text = Loc.Tagline.Text;
        ContinueButton.Content = Ui.Continue.Text;
        ContinueButton.IsVisible = _save is not null;
        NewGameButton.Content = Ui.NewGame.Text;
        NewGameButton.Classes.Set("go", _save is null);
        LanguageButton.Content = Ui.Language.Text;
        TitleSettingsButton.Content = Ui.Quality.With(Ui.QualityName(_settings.Quality).Text).Text;
        QuitButton.Content = Ui.Quit.Text;
        CreditsButton.Content = Ui.Credits.Text;
        RosterPortrait.Source = Portrait(_character);
        RosterLabel.Text = Ui.Roster.Text;
        CreditText.Text = Loc.CreditLine.Text.ToUpperInvariant();
        VersionText.Text = "V0.1 · .NET 10 · THREE.NET";
        TitleNote.Text = (_save is { } save
            ? Ui.HasSave.With(CharacterProfile.Of(save.Character).Name, save.Day, Loc.Rupiah(save.Money)).Text + "\n" + Ui.NewGameWarning.Text
            : Ui.NoSave.Text).ToUpperInvariant();

        RosterList.Children.Clear();
        foreach (CharacterProfile profile in CharacterProfile.Roster)
        {
            CharacterId id = profile.Id;
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new TextBlock { Classes = { "display" }, Text = profile.Name.ToUpperInvariant(), FontSize = 19, Width = 104 });
            TextBlock role = new() { Text = profile.Role.Text, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(role, 1);
            row.Children.Add(role);
            Button button = new() { Classes = { "plate", "small" }, Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Classes.Set("selected", id == _character);
            button.Click += (_, _) =>
            {
                _character = id;
                _audio?.Click();
                RefreshTitle();
            };
            RosterList.Children.Add(button);
        }

        CharacterProfile chosen = CharacterProfile.Of(_character);
        RosterBio.Text = chosen.Bio.Text;
        RosterPerk.Text = chosen.Perk.Text;
    }

    private void ToggleLanguage()
    {
        _settings.Language = _settings.Language == Language.Indonesia ? Language.English : Language.Indonesia;
        Loc.Language = _settings.Language;
        _settings.Save();
        _audio?.Click();
        if (_screen == Screen.Title)
        {
            RefreshTitle();
        }
        else
        {
            RefreshPause();
        }
    }

    private void CycleQuality()
    {
        _settings.Quality = (Quality)(((int)_settings.Quality + 1) % 3);
        _settings.Save();
        ApplyQuality();
        _audio?.Click();
        if (_screen == Screen.Title)
        {
            RefreshTitle();
        }
        else
        {
            RefreshPause();
        }
    }

    private void StartPlaying(SaveData? save)
    {
        _audio?.Click();
        StartSession(save);
        ApplyQuality();
        GameSession session = _session!;
        Relocate(session);
        TitleScreen.IsVisible = false;
        Hud.IsVisible = true;
        _screen = Screen.Playing;
        _hudTimer = 0f;
        _shownDialog = null;
        _shownActivity = null;
        Viewport.Focus();
        if (save is null)
        {
            Toast(Ui.StartHint.Text);
        }
    }

    // ------------------------------------------------------------------ credits

    private readonly Dictionary<CharacterId, Avalonia.Media.Imaging.Bitmap> _portraits = [];
    private bool _credits;
    private double _creditsTime;

    /// <summary>The painted portrait of a playable character (loaded once).</summary>
    private Avalonia.Media.Imaging.Bitmap Portrait(CharacterId id)
    {
        if (!_portraits.TryGetValue(id, out Avalonia.Media.Imaging.Bitmap? bitmap))
        {
            Uri uri = new($"avares://BarudakBogor/Assets/Art/portrait_{CharacterProfile.Of(id).Name.ToLowerInvariant()}.jpg");
            using Stream stream = Avalonia.Platform.AssetLoader.Open(uri);
            _portraits[id] = bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
        }

        return bitmap;
    }

    private void ShowCredits()
    {
        _audio?.Click();
        CreditsCloseButton.Content = Ui.CreditsClose.Text;
        CreditsRoll.Children.Clear();
        CreditsRoll.Children.Add(new TextBlock { Classes = { "display" }, Text = "BARUDAK", FontSize = 58, LineHeight = 54 });
        CreditsRoll.Children.Add(new TextBlock { Classes = { "display" }, Text = "BOGOR", FontSize = 58, LineHeight = 54, Foreground = Brush("Kujang") });
        CreditsRoll.Children.Add(new TextBlock { Text = Loc.Tagline.Text, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 10, 0, 20) });
        foreach ((L role, L names) in Ui.CreditRoll)
        {
            CreditsRoll.Children.Add(new TextBlock { Classes = { "role" }, Text = role.Text });
            CreditsRoll.Children.Add(new TextBlock { Classes = { "name" }, Text = names.Text });
        }

        CreditsRoll.Children.Add(new TextBlock { Classes = { "role" }, Text = "© 2026" });
        CreditsRoll.Children.Add(new TextBlock { Classes = { "name" }, Text = Loc.CreditLine.Text, FontSize = 19 });
        _credits = true;
        _creditsTime = 0;
        CreditsScreen.IsVisible = true;
        UpdateCredits(0f);
    }

    private void HideCredits()
    {
        _credits = false;
        CreditsScreen.IsVisible = false;
    }

    /// <summary>Rolls the names up and lets the painting drift down from the mountain to the city.</summary>
    private void UpdateCredits(float dt)
    {
        _creditsTime += dt;
        double height = CreditsScreen.Bounds.Height;
        double width = CreditsScreen.Bounds.Width;
        if (height <= 0)
        {
            return;
        }

        double roll = CreditsRoll.Bounds.Height;
        double travel = height + roll;
        double position = (_creditsTime * 46) % (travel + 60);
        Canvas.SetTop(CreditsRoll, height - position);

        // The art is as wide as the window and taller than it; pan by how far the roll has come.
        double artHeight = width * 1376 / 768;
        CreditsArt.Width = width;
        CreditsArt.Height = artHeight;
        double progress = Math.Clamp(position / travel, 0, 1);
        Canvas.SetTop(CreditsArt, -(artHeight - height) * progress);
    }

    // ------------------------------------------------------------------ pause

    private void Pause(PauseTab tab)
    {
        if (_session is null)
        {
            return;
        }

        _screen = Screen.Paused;
        _session.Paused = true;
        _down.Clear();
        _tab = tab;
        Hud.IsVisible = false;
        DialogPanel.IsVisible = false;
        ActivityPanel.IsVisible = false;
        BigMap.Selected = null;
        BigMap.FocusOnPlayer();
        PauseScreen.IsVisible = true;
        RefreshPause();
    }

    private void Resume()
    {
        if (_session is null)
        {
            return;
        }

        _session.Paused = false;
        _screen = Screen.Playing;
        PauseScreen.IsVisible = false;
        Hud.IsVisible = true;
        // Dialogue and mini game panels come back on the next HUD pass.
        _shownDialog = null;
        _shownActivity = null;
        Viewport.Focus();
    }

    private void RefreshPause()
    {
        ResumeButton.Content = Ui.Resume.Text;
        SaveButton.Content = Ui.Save.Text;
        ToTitleButton.Content = Ui.ToTitle.Text;
        PauseCredit.Text = Loc.CreditLine.Text.ToUpperInvariant();

        TabBar.Children.Clear();
        (PauseTab Tab, L Name)[] tabs =
        [
            (PauseTab.Map, Ui.TabMap), (PauseTab.Missions, Ui.TabMissions), (PauseTab.Career, Ui.TabCareer),
            (PauseTab.Settings, Ui.TabSettings), (PauseTab.Help, Ui.TabHelp),
        ];
        foreach ((PauseTab tab, L name) in tabs)
        {
            Button button = new() { Classes = { "plate", "tab" }, Content = name.Text };
            button.Classes.Set("selected", tab == _tab);
            button.Click += (_, _) =>
            {
                _tab = tab;
                _audio?.Click();
                RefreshPause();
            };
            TabBar.Children.Add(button);
        }

        TabMap.IsVisible = _tab == PauseTab.Map;
        TabList.IsVisible = _tab != PauseTab.Map;
        TabContent.Children.Clear();
        GameSession session = _session!;
        switch (_tab)
        {
            case PauseTab.Map:
                MapHelp.Text = Ui.MapHelp.Text;
                if (BigMap.Selected is { } place)
                {
                    SelectPlace(place);
                }
                else
                {
                    MapTitle.Text = Ui.MapPrompt.Text;
                    MapDetail.Text = Ui.MapPromptDetail.Text;
                    FastTravelButton.IsVisible = false;
                }

                BigMap.InvalidateVisual();
                break;
            case PauseTab.Missions:
                BuildMissions(session);
                break;
            case PauseTab.Career:
                BuildCareer(session);
                break;
            case PauseTab.Settings:
                BuildSettings();
                break;
            default:
                BuildHelp();
                break;
        }
    }

    private void SelectPlace(PoiId place)
    {
        if (_session is not { } session)
        {
            return;
        }

        Poi poi = session.Map[place];
        MapTitle.Text = Loc.Name(poi.NameKey).Text;
        float km = Vector2.Distance(poi.Position, session.Player.Ground) / 1_000f;
        string district = Loc.Name(session.Map.DistrictAt(poi.Position)?.NameKey ?? "district.rural").Text;
        MapDetail.Text = $"{district}\n{Ui.Distance.With(km).Text}";
        FastTravelButton.Content = Ui.FastTravel.With(Loc.Rupiah(session.FastTravelFare(place))).Text;
        FastTravelButton.IsVisible = true;
    }

    private Border Card(Control content, string accent = "AsphaltSoft") => new()
    {
        Classes = { "panel" },
        BorderBrush = Brush(accent),
        BorderThickness = new Avalonia.Thickness(4, 0, 0, 0),
        Padding = new Avalonia.Thickness(16, 12),
        Child = content,
    };

    private static TextBlock Heading(string text, string brush = "Cream") => new()
    {
        Classes = { "display" },
        Text = text,
        FontSize = 19,
        Foreground = (IBrush)Application.FindResource(brush)!,
    };

    private static TextBlock Paragraph(string text) => new() { Classes = { "body" }, Text = text, FontSize = 18, LineHeight = 23 };

    private static TextBlock Caption(string text) => new() { Classes = { "label" }, Text = text.ToUpperInvariant(), TextWrapping = TextWrapping.Wrap };

    private void BuildMissions(GameSession session)
    {
        int number = 0;
        foreach (Mission mission in session.Missions.All)
        {
            number++;
            (string status, string accent) = mission.State switch
            {
                MissionState.Completed => (Ui.Done.Text, "Angkot"),
                MissionState.Active => (Ui.Active.Text, "Kujang"),
                MissionState.Available => (Ui.Available.With(Loc.Name(session.Map[mission.StartPlace].NameKey).Text).Text, "Rain"),
                _ => (Ui.Locked.With(session.Missions.Find(mission.Requires!).Title.Text).Text, "AsphaltSoft"),
            };

            Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            grid.Children.Add(new TextBlock { Classes = { "display" }, Text = number.ToString("00"), FontSize = 30, Width = 62, Foreground = Brush(accent), VerticalAlignment = VerticalAlignment.Center });
            StackPanel text = new() { Spacing = 2 };
            text.Children.Add(Heading(mission.Title.Text.ToUpperInvariant()));
            text.Children.Add(Paragraph(mission.Summary.Text));
            text.Children.Add(Caption($"{status} · {Ui.Reward.With(Loc.Rupiah(mission.RewardMoney)).Text}"));
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            if (mission.State == MissionState.Active)
            {
                Button abandon = new() { Classes = { "plate", "small" }, Content = Ui.Abandon.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(14, 0, 0, 0) };
                abandon.Click += (_, _) =>
                {
                    session.Missions.Abandon();
                    RefreshPause();
                };
                Grid.SetColumn(abandon, 2);
                grid.Children.Add(abandon);
            }

            Border card = Card(grid, accent);
            card.Opacity = mission.State == MissionState.Locked ? 0.55 : 1;
            TabContent.Children.Add(card);
        }
    }

    private void BuildCareer(GameSession session)
    {
        Progress progress = session.Progress;
        TabContent.Children.Add(Paragraph(Ui.CareerIntro.Text));
        foreach (Career career in Enum.GetValues<Career>())
        {
            bool reached = progress.Endings.Contains(career);
            StackPanel stack = new() { Spacing = 6 };
            Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            top.Children.Add(Heading(Progress.CareerTitle(career).Text.ToUpperInvariant(), reached ? "Kujang" : "Cream"));
            TextBlock score = new() { Classes = { "display" }, Text = $"{Math.Min(progress.Reputation(career), Progress.ReputationGoal)}/{Progress.ReputationGoal}", FontSize = 17 };
            Grid.SetColumn(score, 1);
            top.Children.Add(score);
            stack.Children.Add(top);
            stack.Children.Add(new ProgressBar { Classes = { "gauge" }, Minimum = 0, Maximum = Progress.ReputationGoal, Value = Math.Min(progress.Reputation(career), Progress.ReputationGoal) });
            stack.Children.Add(Caption(Progress.CareerHint(career).Text));
            TabContent.Children.Add(Card(stack, reached ? "Kujang" : "AsphaltSoft"));
        }

        StackPanel records = new() { Spacing = 4 };
        records.Children.Add(Caption(Ui.StatsTitle.Text));
        records.Children.Add(Paragraph(Ui.StatLines.With(
            progress.FishCaught, progress.BiggestFishKg, progress.PassengersCarried, progress.OjolOrders, progress.PhotosTaken,
            progress.BestPhotoScore, progress.DistanceDriven / 1_000f, session.Business.CupsSold).Text));
        records.Children.Add(Paragraph($"{BandrekBusiness.TierName(session.Business.Tier).Text} · {session.Player.Profile.Name}: {session.Player.Profile.Perk.Text}"));
        TabContent.Children.Add(Card(records));
    }

    private void BuildSettings()
    {
        void Option(string text, Action action)
        {
            Button button = new() { Classes = { "plate" }, Content = text, Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += (_, _) =>
            {
                action();
                _settings.Save();
                _audio?.Click();
                RefreshPause();
            };
            TabContent.Children.Add(button);
        }

        Option(Ui.Language.Text, () =>
        {
            _settings.Language = _settings.Language == Language.Indonesia ? Language.English : Language.Indonesia;
            Loc.Language = _settings.Language;
        });
        Option(Ui.Quality.With(Ui.QualityName(_settings.Quality).Text).Text, () =>
        {
            _settings.Quality = (Quality)(((int)_settings.Quality + 1) % 3);
            ApplyQuality();
        });
        TabContent.Children.Add(Caption(Ui.QualityNote.Text));
        Option(Ui.Sound.With((_settings.Muted ? Ui.Off : Ui.On).Text).Text, () =>
        {
            _settings.Muted = !_settings.Muted;
            _audio!.Muted = _settings.Muted;
        });
        Option(Ui.Volume.With((int)MathF.Round(_settings.Volume * 100f)).Text, () =>
        {
            // Steps of 20%, wrapping from 100 back to 20.
            _settings.Volume = _settings.Volume >= 0.99f ? 0.2f : MathF.Min(1f, _settings.Volume + 0.2f);
            _audio!.Volume = _settings.Volume;
        });
        Option(Ui.Music.With((_settings.Music ? Ui.On : Ui.Off).Text).Text, () =>
        {
            _settings.Music = !_settings.Music;
            _audio!.Music = _settings.Music;
        });
        Option(Ui.Stats.With((_settings.ShowStats ? Ui.On : Ui.Off).Text).Text, () => _settings.ShowStats = !_settings.ShowStats);
        if (_audio?.Unavailable is { } reason)
        {
            TabContent.Children.Add(Caption(Ui.NoAudio.With(reason).Text));
        }
    }

    private void BuildHelp()
    {
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("230,*") };
        int row = 0;
        foreach ((string keys, L action) in Ui.Controls)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock key = new() { Classes = { "display" }, Text = keys, FontSize = 15, Foreground = Brush("Kujang"), Margin = new Avalonia.Thickness(0, 5) };
            TextBlock what = new() { Text = action.Text, FontSize = 19, FontWeight = FontWeight.SemiBold, Margin = new Avalonia.Thickness(0, 4) };
            Grid.SetRow(key, row);
            Grid.SetRow(what, row);
            Grid.SetColumn(what, 1);
            grid.Children.Add(key);
            grid.Children.Add(what);
            row++;
        }

        TabContent.Children.Add(Card(grid, "Kujang"));
        StackPanel about = new() { Spacing = 4 };
        about.Children.Add(Heading("BARUDAK BOGOR"));
        about.Children.Add(Paragraph(Loc.Tagline.Text));
        about.Children.Add(Caption(Loc.CreditLine.Text));
        TabContent.Children.Add(Card(about, "Angkot"));
    }
}
