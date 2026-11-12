using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BarudakBogor.Activities;

namespace BarudakBogor.Game.Views;

/// <summary>Draws the playfield of the running mini game: the fishing line gauge or the rhythm lanes.</summary>
public sealed class ActivityControl : Control
{
    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#26F6ECCB"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F6ECCB"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#F0B63A"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#1E9E57"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E5432E"));
    private static readonly IBrush Teal = new SolidColorBrush(Color.Parse("#5FC4C9"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#12181C"));
    private static readonly IPen LinePen = new Pen(Cream, 3);
    private static readonly IBrush[] LaneBrushes = [Teal, Gold, Green, new SolidColorBrush(Color.Parse("#E86FB0"))];
    private static readonly Typeface Display = new(new FontFamily("avares://BarudakBogor/Assets/Fonts#Bungee"));
    private static readonly Typeface Body = new(new FontFamily("avares://BarudakBogor/Assets/Fonts#Barlow Condensed"), FontStyle.Normal, FontWeight.SemiBold);

    private readonly List<(int Lane, float TimeToHit)> _beats = [];

    public Activity? Activity { get; set; }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        switch (Activity)
        {
            case FishingGame fishing:
                RenderFishing(context, fishing, bounds);
                break;
            case RhythmGame rhythm:
                RenderRhythm(context, rhythm, bounds);
                break;
        }
    }

    private static void RenderFishing(DrawingContext context, FishingGame game, Rect bounds)
    {
        double width = bounds.Width;

        // The float: it sits on the water line and dips when a fish takes the bait.
        double water = 46;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1F4F58")), new Rect(0, water, width, 30), 4);
        double bob = game.State switch
        {
            FishingState.Bite => 12,
            FishingState.Reeling => 6 + (Math.Sin(game.Tension * 30) * 3),
            FishingState.Waiting => 0,
            _ => -200,
        };
        double floatX = width * (0.75 - (game.Reeled * 0.55));
        context.DrawEllipse(game.State == FishingState.Bite ? Red : Gold, null, new Point(floatX, water + bob), 7, 9);
        context.DrawLine(new Pen(Cream, 1), new Point(30, 4), new Point(floatX, water + bob - 8));

        string status = game.State switch
        {
            FishingState.Landed when game.Fish is { } fish => $"{fish.Name.Text} · {game.WeightKg:0.0} kg",
            FishingState.Escaped => "…",
            _ => string.Empty,
        };
        if (status.Length > 0)
        {
            FormattedText text = new(status, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Display, 20, Gold);
            context.DrawText(text, new Point((width - text.Width) * 0.5, 8));
        }

        // Tension gauge with the safe band marked in livery green.
        double top = 98;
        context.FillRectangle(Track, new Rect(0, top, width, 22), 4);
        context.FillRectangle(Green, new Rect(width * FishingGame.SafeMin, top, width * (FishingGame.SafeMax - FishingGame.SafeMin), 22), 4);
        context.FillRectangle(Red, new Rect(width * 0.94, top, width * 0.06, 22), 4);
        double needle = width * Math.Clamp(game.Tension, 0f, 1f);
        context.FillRectangle(Cream, new Rect(needle - 3, top - 6, 6, 34), 2);

        // Reel progress.
        context.FillRectangle(Track, new Rect(0, top + 44, width, 10), 5);
        context.FillRectangle(Gold, new Rect(0, top + 44, width * Math.Clamp(game.Reeled, 0f, 1f), 10), 5);
    }

    private void RenderRhythm(DrawingContext context, RhythmGame game, Rect bounds)
    {
        int lanes = game.Lanes;
        double laneWidth = Math.Min(110, bounds.Width / lanes);
        double left = (bounds.Width - (laneWidth * lanes)) * 0.5;
        double line = bounds.Height - 30;
        string[] keys = lanes == 2 ? ["A", "D"] : ["A", "S", "D", "F"];

        for (int lane = 0; lane < lanes; lane++)
        {
            Rect column = new(left + (lane * laneWidth) + 4, 0, laneWidth - 8, bounds.Height);
            context.FillRectangle(Track, column, 4);
            FormattedText key = new(keys[lane], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Display, 17, Cream);
            context.DrawText(key, new Point(column.Center.X - (key.Width * 0.5), line + 6));
        }

        context.DrawLine(LinePen, new Point(left, line), new Point(left + (laneWidth * lanes), line));

        game.VisibleBeats(_beats);
        foreach ((int lane, float timeToHit) in _beats)
        {
            double y = line - (timeToHit / game.Approach * line);
            double x = left + (lane * laneWidth) + (laneWidth * 0.5);
            if (lanes == 2)
            {
                // A shuttlecock: cork and skirt.
                context.DrawEllipse(Cream, null, new Point(x, y), 13, 9);
                context.DrawEllipse(LaneBrushes[lane], null, new Point(x, y + 5), 7, 6);
            }
            else
            {
                context.FillRectangle(LaneBrushes[lane], new Rect(x - (laneWidth * 0.5) + 10, y - 8, laneWidth - 20, 16), 8);
            }
        }

        if (game.Last != Judgement.None && game.LastAge < 0.5f)
        {
            (string word, IBrush brush) = game.Last switch
            {
                Judgement.Perfect => ("MANTAP!", Gold),
                Judgement.Hit => ("PAS", Cream),
                _ => ("LEPAS", Red),
            };
            FormattedText text = new(word, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Display, 26, brush);
            using (context.PushOpacity(1 - (game.LastAge / 0.5)))
            {
                context.DrawText(text, new Point((bounds.Width - text.Width) * 0.5, 10 - (game.LastAge * 12)));
            }
        }

        if (game.Combo > 2)
        {
            FormattedText combo = new($"x{game.Combo}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Body, 22, Gold);
            context.DrawText(combo, new Point(bounds.Width - combo.Width - 4, 6));
        }
    }
}
