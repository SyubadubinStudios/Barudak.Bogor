using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.World;
using Vector = Avalonia.Vector;

namespace BarudakBogor.Game.Views;

/// <summary>
/// The map, drawn straight from the road graph. As the round mini map it follows the player;
/// with <see cref="Full"/> set it is the pause-screen map: wheel to zoom, drag to pan, click a
/// place to select it.
/// </summary>
public sealed class MapControl : Control
{
    private static readonly IBrush Ground = new SolidColorBrush(Color.Parse("#162025"));
    private static readonly IBrush Park = new SolidColorBrush(Color.Parse("#1D4A30"));
    private static readonly IBrush Town = new SolidColorBrush(Color.Parse("#232E34"));
    private static readonly IBrush WaterBrush = new SolidColorBrush(Color.Parse("#2E7F8C"));
    private static readonly IBrush Cream = new SolidColorBrush(Color.Parse("#F6ECCB"));
    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#F0B63A"));
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#12181C"));
    private static readonly IPen RoadPen = new Pen(new SolidColorBrush(Color.Parse("#7C8A8C")), 2.2, lineCap: PenLineCap.Round);
    private static readonly IPen ArterialPen = new Pen(new SolidColorBrush(Color.Parse("#C9C2A5")), 3.2, lineCap: PenLineCap.Round);
    private static readonly IPen TollPen = new Pen(new SolidColorBrush(Color.Parse("#F0B63A")), 3.4, lineCap: PenLineCap.Round);
    private static readonly IPen GangPen = new Pen(new SolidColorBrush(Color.Parse("#566266")), 1.2);
    private static readonly IPen ThinRoadPen = new Pen(new SolidColorBrush(Color.Parse("#8E9A97")), 1.1);
    private static readonly IPen ThinArterialPen = new Pen(new SolidColorBrush(Color.Parse("#D6CFAF")), 1.8);
    private static readonly IPen OutlinePen = new Pen(Dark, 2);
    private static readonly IBrush[] MarkerBrushes =
    [
        Gold,
        new SolidColorBrush(Color.Parse("#7CF29C")),
        new SolidColorBrush(Color.Parse("#4FB6E8")),
        new SolidColorBrush(Color.Parse("#F08A3C")),
        Cream,
        new SolidColorBrush(Color.Parse("#E86FB0")),
    ];

    private static readonly Typeface LabelFace = new(new FontFamily("avares://BarudakBogor/Assets/Fonts#Barlow Condensed"), FontStyle.Normal, FontWeight.SemiBold);

    private double _zoom = 1;
    private Vector2 _center;
    private Point _dragFrom;
    private bool _dragging;
    private bool _moved;

    public GameSession? Session { get; set; }

    /// <summary>Pause-screen mode: the whole map, interactive.</summary>
    public bool Full { get; set; }

    public PoiId? Selected { get; set; }

    /// <summary>Raised when a place is clicked on the full map.</summary>
    public event Action<PoiId>? PlaceSelected;

    /// <summary>Centres the full map on the player at a zoom that shows the surrounding districts.</summary>
    public void FocusOnPlayer()
    {
        if (Session is { } session)
        {
            _center = session.Player.Ground;
            _zoom = 5;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Session is not { } session)
        {
            return;
        }

        Rect bounds = new(Bounds.Size);
        context.FillRectangle(Ground, bounds);
        if (Full)
        {
            RenderFull(context, session, bounds);
        }
        else
        {
            RenderMini(context, session, bounds);
        }
    }

    // ------------------------------------------------------------------ mini map

    private void RenderMini(DrawingContext context, GameSession session, Rect bounds)
    {
        const float range = 250f;
        Vector2 focus = session.Player.Ground;
        double scale = bounds.Width / (range * 2);
        Point ToScreen(Vector2 p) => new(bounds.Width * 0.5 + ((p.X - focus.X) * scale), bounds.Height * 0.5 + ((p.Y - focus.Y) * scale));

        WorldMap map = session.Map;
        foreach (District district in map.Districts)
        {
            if (Vector2.Distance(district.Center, focus) < district.Radius + (range * 1.5f))
            {
                double r = district.Radius * scale;
                context.DrawEllipse(district.Use == LandUse.Taman ? Park : Town, null, ToScreen(district.Center), r, r);
            }
        }

        // The park sits inside the city disc, so paint it again on top.
        foreach (District district in map.Districts)
        {
            if (district.Use == LandUse.Taman && Vector2.Distance(district.Center, focus) < district.Radius + (range * 1.5f))
            {
                double r = district.Radius * scale;
                context.DrawEllipse(Park, null, ToScreen(district.Center), r, r);
            }
        }

        foreach (Pond pond in map.Ponds)
        {
            double r = pond.Radius * scale;
            context.DrawEllipse(WaterBrush, null, ToScreen(pond.Center), r, r);
        }

        Vector2 reach = new(range * 1.1f);
        map.Roads.ForEachSegment(focus - reach, focus + reach, (edge, segment) =>
        {
            IPen pen = edge.Kind switch
            {
                RoadKind.Gang => GangPen,
                RoadKind.Street => RoadPen,
                RoadKind.Toll => TollPen,
                _ => ArterialPen,
            };
            context.DrawLine(pen, ToScreen(edge.Points[segment]), ToScreen(edge.Points[segment + 1]));
        });

        double half = (bounds.Width * 0.5) - 12;
        Point middle = bounds.Center;
        foreach (Marker marker in session.Markers)
        {
            // Markers beyond the rim stick to it, pointing the way.
            Point p = ToScreen(marker.Position);
            Vector offset = p - middle;
            double length = Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));
            bool outside = length > half;
            if (outside)
            {
                p = middle + (offset * (half / length));
            }

            double size = outside ? 4.5 : 6;
            context.DrawEllipse(MarkerBrushes[(int)marker.Kind], OutlinePen, p, size, size);
        }

        DrawPlayer(context, middle, session.Player.Yaw, 8);
    }

    // ------------------------------------------------------------------ full map

    private double FullScale(Rect bounds) => Math.Min(bounds.Width, bounds.Height) / (Terrain.HalfSize * 2) * _zoom;

    private Point FullToScreen(Vector2 p, Rect bounds)
    {
        double scale = FullScale(bounds);
        return new Point((bounds.Width * 0.5) + ((p.X - _center.X) * scale), (bounds.Height * 0.5) + ((p.Y - _center.Y) * scale));
    }

    private void RenderFull(DrawingContext context, GameSession session, Rect bounds)
    {
        using DrawingContext.PushedState clip = context.PushClip(bounds);
        WorldMap map = session.Map;
        double scale = FullScale(bounds);

        foreach (District district in map.Districts)
        {
            double r = district.Radius * scale;
            context.DrawEllipse(district.Use == LandUse.Taman ? Park : Town, null, FullToScreen(district.Center, bounds), r, r);
        }

        foreach (District district in map.Districts)
        {
            if (district.Use == LandUse.Taman)
            {
                double r = district.Radius * scale;
                context.DrawEllipse(Park, null, FullToScreen(district.Center, bounds), r, r);
            }
        }

        foreach (Pond pond in map.Ponds)
        {
            double r = Math.Max(2, pond.Radius * scale);
            context.DrawEllipse(WaterBrush, null, FullToScreen(pond.Center, bounds), r, r);
        }

        bool close = _zoom > 3.5;
        foreach (RoadEdge edge in map.Roads.Edges)
        {
            bool major = edge.Kind is RoadKind.Arterial or RoadKind.Toll or RoadKind.Mountain;
            if (!major && !close)
            {
                continue;
            }

            if (edge.Kind == RoadKind.Gang && _zoom < 9)
            {
                continue;
            }

            Point a = FullToScreen(edge.Points[0], bounds);
            Point b = FullToScreen(edge.Points[^1], bounds);
            if ((a.X < -200 && b.X < -200) || (a.Y < -200 && b.Y < -200) || (a.X > bounds.Width + 200 && b.X > bounds.Width + 200) || (a.Y > bounds.Height + 200 && b.Y > bounds.Height + 200))
            {
                continue;
            }

            IPen pen = edge.Kind == RoadKind.Toll ? TollPen : major ? ThinArterialPen : ThinRoadPen;
            // Long roads keep their bends; short streets are a single stroke.
            int step = Math.Max(1, edge.Points.Length / (major ? 48 : 4));
            Point previous = a;
            for (int i = step; i < edge.Points.Length; i += step)
            {
                Point next = FullToScreen(edge.Points[i], bounds);
                context.DrawLine(pen, previous, next);
                previous = next;
            }

            context.DrawLine(pen, previous, b);
        }

        foreach (District district in map.Districts)
        {
            if (district.Radius * scale > 16)
            {
                FormattedText name = Text(Loc.Name(district.NameKey).Text.ToUpperInvariant(), 13, Cream);
                Point at = FullToScreen(district.Center, bounds);
                context.DrawText(name, new Point(at.X - (name.Width * 0.5), at.Y - (district.Radius * scale) - 16));
            }
        }

        foreach (Poi poi in map.Pois)
        {
            Point p = FullToScreen(poi.Position, bounds);
            bool selected = poi.Id == Selected;
            context.DrawEllipse(selected ? Gold : Cream, OutlinePen, p, selected ? 7 : 4, selected ? 7 : 4);
            if (selected || _zoom > 6)
            {
                context.DrawText(Text(Loc.Name(poi.NameKey).Text, 14, selected ? Gold : Cream), new Point(p.X + 9, p.Y - 9));
            }
        }

        foreach (Marker marker in session.Markers)
        {
            Point p = FullToScreen(marker.Position, bounds);
            context.DrawRectangle(MarkerBrushes[(int)marker.Kind], OutlinePen, new Rect(p.X - 5, p.Y - 5, 10, 10), 2, 2);
        }

        DrawPlayer(context, FullToScreen(session.Player.Ground, bounds), session.Player.Yaw, 9);
    }

    private static void DrawPlayer(DrawingContext context, Point at, float yaw, double size)
    {
        // Screen x is east and screen y is south, the same axes as the world's X and Z.
        Vector forward = new(Math.Sin(yaw), Math.Cos(yaw));
        Vector side = new(forward.Y, -forward.X);
        StreamGeometry arrow = new();
        using (StreamGeometryContext geometry = arrow.Open())
        {
            geometry.BeginFigure(at + (forward * size * 1.3), true);
            geometry.LineTo(at - (forward * size * 0.8) + (side * size * 0.8));
            geometry.LineTo(at - (forward * size * 0.3));
            geometry.LineTo(at - (forward * size * 0.8) - (side * size * 0.8));
            geometry.EndFigure(true);
        }

        context.DrawGeometry(Cream, OutlinePen, arrow);
    }

    private static FormattedText Text(string text, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, size, brush);

    // ------------------------------------------------------------------ interaction

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!Full)
        {
            return;
        }

        Rect bounds = new(Bounds.Size);
        Point cursor = e.GetPosition(this);
        Vector2 before = ToWorld(cursor, bounds);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.3 : 1 / 1.3), 1, 40);
        // Keep the point under the cursor where it is.
        Vector2 after = ToWorld(cursor, bounds);
        _center += before - after;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Full)
        {
            _dragging = true;
            _moved = false;
            _dragFrom = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        Point now = e.GetPosition(this);
        Vector delta = now - _dragFrom;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 3)
        {
            _moved = true;
        }

        double scale = FullScale(new Rect(Bounds.Size));
        _center -= new Vector2((float)(delta.X / scale), (float)(delta.Y / scale));
        _dragFrom = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        if (_moved || Session is not { } session)
        {
            return;
        }

        Rect bounds = new(Bounds.Size);
        Point click = e.GetPosition(this);
        Poi? best = null;
        double bestDistance = 18;
        foreach (Poi poi in session.Map.Pois)
        {
            Point p = FullToScreen(poi.Position, bounds);
            double distance = Math.Sqrt(Math.Pow(p.X - click.X, 2) + Math.Pow(p.Y - click.Y, 2));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = poi;
            }
        }

        if (best is not null)
        {
            Selected = best.Id;
            PlaceSelected?.Invoke(best.Id);
            InvalidateVisual();
        }
    }

    private Vector2 ToWorld(Point screen, Rect bounds)
    {
        double scale = FullScale(bounds);
        return _center + new Vector2((float)((screen.X - (bounds.Width * 0.5)) / scale), (float)((screen.Y - (bounds.Height * 0.5)) / scale));
    }
}
