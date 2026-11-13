using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Missions;

namespace BarudakBogor.Game.Views;

public partial class MainWindow
{
    private readonly List<(Border Panel, float Remaining)> _toasts = [];
    private float _bannerTimer;
    private float _moneyDeltaTimer;
    private Dialog? _shownDialog;
    private Activity? _shownActivity;

    private static IBrush Brush(string key) => (IBrush)(Application.FindResource(key) ?? Brushes.White);

    private static Avalonia.Application Application => Avalonia.Application.Current!;

    private void UpdateHud(GameSession session, float dt)
    {
        UpdateToasts(dt);
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= dt;
            Banner.IsVisible = _bannerTimer > 0f;
        }

        if (_moneyDeltaTimer > 0f)
        {
            _moneyDeltaTimer -= dt;
            MoneyDelta.Opacity = Math.Clamp(_moneyDeltaTimer / 0.6f, 0f, 1f);
        }

        UpdateDialog(session);
        UpdateActivity(session);

        // Text changes a dozen times a second at most; laying it out every frame would be waste.
        _hudTimer -= dt;
        if (_hudTimer > 0f)
        {
            return;
        }

        _hudTimer = 0.08f;
        Player player = session.Player;
        LocationText.Text = session.LocationName.Text;
        string day = Loc.Language == Language.Indonesia ? "HARI" : "DAY";
        ClockText.Text = $"{day} {session.Clock.Day} · {session.Clock} · {Sim.GameClock.PhaseName(session.Clock.Phase).Text.ToUpperInvariant()}";
        MoneyText.Text = Loc.Rupiah(session.Progress.Money);
        string weather = Ui.Weather(session.Weather.Kind).Text;
        if (session.Weather.Mist > 0.3f)
        {
            weather += " · " + Ui.Misty.Text;
        }

        WeatherText.Text = session.Weather.IsBlackedOut(player.Ground) ? $"{weather} · {Ui.Blackout.Text}" : weather;

        Mission? mission = session.Missions.Active;
        ObjectivePanel.IsVisible = mission is not null && !mission.Objective.IsEmpty;
        if (mission is not null)
        {
            int number = 1;
            foreach (Mission candidate in session.Missions.All)
            {
                if (candidate == mission)
                {
                    break;
                }

                number++;
            }

            TrayekLabel.Text = Ui.MissionLabel.Text;
            TrayekNumber.Text = number.ToString("00");
            MissionTitle.Text = mission.Title.Text.ToUpperInvariant();
            ObjectiveText.Text = mission.Objective.Text;
            MissionTimer.Text = mission.TimeLeft is { } left ? FormatTime(left) : string.Empty;
            MissionTimer.Foreground = mission.TimeLeft is < 30f ? Brush("Brake") : Brush("Cream");
            ObjectiveDistance.Text = NearestObjective(session) is { } distance
                ? (distance >= 1_000f ? Ui.Kilometres.With(distance / 1_000f) : Ui.Metres.With(distance)).Text
                : string.Empty;
        }
        else
        {
            TrayekLabel.Text = Ui.StoryLabel.Text;
            TrayekNumber.Text = $"{session.Missions.CompletedCount}/{session.Missions.StoryCount}";
        }

        UpdateJobPanel(session);

        bool driving = player.Vehicle is { Spec.IsCart: false };
        SpeedPanel.IsVisible = driving;
        StaminaPanel.IsVisible = !driving;
        if (driving)
        {
            SpeedText.Text = ((int)(MathF.Abs(player.Vehicle!.Speed) * 3.6f)).ToString();
            VehicleName.Text = player.Vehicle.Spec.Name.Text.ToUpperInvariant();
            SpeedUnit.Text = Ui.SpeedUnit.Text;
        }
        else
        {
            StaminaLabel.Text = Ui.Stamina.Text;
            StaminaBar.Value = player.Stamina;
        }

        bool free = session.Dialog is null && session.Activity is null;
        L? prompt = session.Focus?.Prompt ?? session.VehiclePrompt;
        PromptPanel.IsVisible = free && prompt is not null;
        if (prompt is { } text)
        {
            PromptText.Text = text.Text;
        }

        StatsText.IsVisible = _settings.ShowStats;
        if (_settings.ShowStats)
        {
            ThreeNet.FrameStats stats = Viewport.Stats;
            StatsText.Text = $"{_fps:0} FPS · {stats.DrawCalls} DRAW · {stats.Triangles / 1000} K TRI · {_world!.LoadedChunks} CHUNK · {Viewport.Renderer?.Width} x {Viewport.Renderer?.Height} ({_viewMs:0} MS)";
        }

        MiniMap.InvalidateVisual();
    }

    private void UpdateJobPanel(GameSession session)
    {
        if (session.Ojol.OnDuty)
        {
            JobPanel.IsVisible = true;
            JobTitle.Text = Ui.Ojol.With(session.Ojol.Objective.Text).Text.ToUpperInvariant();
            JobText.Text = session.Ojol.HasOrder
                ? Ui.OjolFare.With(Loc.Rupiah(session.Ojol.Fare), (int)session.Ojol.TimeLeft).Text
                : string.Empty;
            JobText.IsVisible = session.Ojol.HasOrder;
        }
        else if (session.Touring.Active)
        {
            JobPanel.IsVisible = true;
            JobTitle.Text = Ui.Touring.With(FormatTime(session.Touring.Elapsed)).Text;
            JobText.Text = session.Touring.Objective.Text;
            JobText.IsVisible = true;
        }
        else if (session.Business.Selling)
        {
            JobPanel.IsVisible = true;
            JobTitle.Text = Ui.Selling.Text;
            JobText.Text = Ui.SellingDetail.With(session.Business.Stock, session.Business.CupsSold).Text;
            JobText.IsVisible = true;
        }
        else
        {
            JobPanel.IsVisible = false;
        }
    }

    private static float? NearestObjective(GameSession session)
    {
        float? best = null;
        foreach (Marker marker in session.Markers)
        {
            if (marker.Kind == MarkerKind.Objective)
            {
                float distance = Vector2.Distance(marker.Position, session.Player.Ground);
                best = best is { } current ? MathF.Min(current, distance) : distance;
            }
        }

        return best;
    }

    private static string FormatTime(float seconds) => $"{(int)(seconds / 60f):00}:{(int)(seconds % 60f):00}";

    // ------------------------------------------------------------------ dialogue and mini games

    private void UpdateDialog(GameSession session)
    {
        Dialog? dialog = session.Dialog;
        if (ReferenceEquals(dialog, _shownDialog))
        {
            return;
        }

        _shownDialog = dialog;
        DialogPanel.IsVisible = dialog is not null;
        if (dialog is null)
        {
            return;
        }

        DialogSpeaker.Text = dialog.Speaker.Text.ToUpperInvariant();
        // When the player's own character speaks, their portrait leads the panel.
        bool own = dialog.Speaker.Id == session.Player.Profile.Name;
        DialogPortraitFrame.IsVisible = own;
        if (own)
        {
            DialogPortrait.Source = Portrait(session.Player.Profile.Id);
        }

        DialogText.Text = dialog.Text.Text;
        DialogChoices.Children.Clear();
        for (int i = 0; i < dialog.Choices.Count; i++)
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new Border
            {
                Background = Brush("Cream"),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(9, 2, 9, 0),
                Child = new TextBlock { Classes = { "display" }, Text = (i + 1).ToString(), FontSize = 16, Foreground = Brush("Asphalt") },
            });
            row.Children.Add(new TextBlock { Text = dialog.Choices[i].Text, FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            DialogChoices.Children.Add(row);
        }

        DialogHint.Text = (dialog.Choices.Count > 0 ? Ui.DialogChoose : Ui.DialogNext).Text;
    }

    private void UpdateActivity(GameSession session)
    {
        Activity? activity = session.Activity;
        if (!ReferenceEquals(activity, _shownActivity))
        {
            _shownActivity = activity;
            ActivityView.Activity = activity;
            ActivityPanel.IsVisible = activity is not null;
        }

        if (activity is null)
        {
            return;
        }

        ActivityTitle.Text = activity.Title.Text.ToUpperInvariant();
        ActivityHint.Text = activity.Hint.Text;
        ActivityScore.Text = activity switch
        {
            FishingGame fishing when fishing.TimeLeft is { } left => $"{fishing.TotalKg:0.0} KG · {FormatTime(left)}",
            FishingGame fishing => $"{fishing.Count} · {fishing.TotalKg:0.0} KG",
            RhythmGame rhythm => rhythm.Score.ToString(),
            _ => string.Empty,
        };
        ActivityView.InvalidateVisual();
    }

    // ------------------------------------------------------------------ toasts and banners

    private void Toast(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Border panel = new()
        {
            Classes = { "panel" },
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 720,
            Child = new TextBlock { Text = text, FontSize = 19, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
        };
        ToastStack.Children.Add(panel);
        _toasts.Add((panel, 5.2f));
        while (_toasts.Count > 4)
        {
            ToastStack.Children.Remove(_toasts[0].Panel);
            _toasts.RemoveAt(0);
        }
    }

    private void UpdateToasts(float dt)
    {
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            (Border panel, float remaining) = _toasts[i];
            remaining -= dt;
            if (remaining <= 0f)
            {
                ToastStack.Children.Remove(panel);
                _toasts.RemoveAt(i);
            }
            else
            {
                panel.Opacity = Math.Clamp(remaining / 0.5f, 0f, 1f);
                _toasts[i] = (panel, remaining);
            }
        }
    }

    private void ClearToasts()
    {
        ToastStack.Children.Clear();
        _toasts.Clear();
        Banner.IsVisible = false;
        _bannerTimer = 0f;
    }

    private void ShowBanner(string label, string title, string detail, bool good, float seconds = 4.2f)
    {
        BannerLabel.Text = label;
        BannerTitle.Text = title.ToUpperInvariant();
        BannerDetail.Text = detail;
        BannerDetail.IsVisible = detail.Length > 0;
        Banner.Background = Brush(good ? "Angkot" : "Brake");
        Banner.BorderBrush = good ? Brush("AngkotDeep") : new SolidColorBrush(Color.Parse("#A32A1B"));
        Banner.IsVisible = true;
        _bannerTimer = seconds;
    }

    private void ShowMoneyDelta(long amount)
    {
        if (amount == 0)
        {
            return;
        }

        MoneyDelta.Text = (amount > 0 ? "+" : "-") + Loc.Rupiah(Math.Abs(amount));
        MoneyDelta.Foreground = Brush(amount > 0 ? "Angkot" : "Brake");
        MoneyDelta.Opacity = 1;
        _moneyDeltaTimer = 2.4f;
    }
}
