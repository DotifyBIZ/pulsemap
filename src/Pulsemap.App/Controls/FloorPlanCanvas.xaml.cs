using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Windows.UI;

namespace Pulsemap.App.Controls;

/// <summary>
/// Renders a floor's walls, test points, access points, and an optional coverage heatmap on a
/// meters-to-pixels canvas, and turns pointer clicks into add-wall/add-test-point/delete requests
/// depending on <see cref="Tool"/>. Owns only rendering and gesture recognition — every mutation
/// is decided and persisted by whoever handles these events (see WorkspaceViewModel).
/// </summary>
public sealed partial class FloorPlanCanvas : UserControl
{
    private const double PixelsPerMeter = 40;
    private const double PaddingMeters = 2;
    private const double MinSpanMeters = 5;
    private const double HeatmapCellMeters = 0.5;
    private const double TestPointRadiusPx = 7;
    private const double AccessPointRadiusPx = 10;
    private const double WallStrokeThicknessPx = 4;
    private const double HeatmapOpacity = 0.55;

    private Bounds _bounds = new(0, 0, 20, 15);
    private Point2D? _wallAnchor;

    public FloorPlanCanvas()
    {
        InitializeComponent();
    }

    public WorkspaceTool Tool { get; set; } = WorkspaceTool.Select;

    public event EventHandler<Point2D>? TestPointRequested;

    public event EventHandler<(Point2D Start, Point2D End)>? WallRequested;

    public event EventHandler<Point2D>? DeleteRequested;

    public void Render(Floor floor, IReadOnlyList<CoverageSample> heatmap)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(heatmap);

        _bounds = ComputeBounds(floor);
        RootGrid.Width = _bounds.WidthMeters * PixelsPerMeter;
        RootGrid.Height = _bounds.HeightMeters * PixelsPerMeter;

        HeatmapLayer.Children.Clear();
        WallsLayer.Children.Clear();
        MarkersLayer.Children.Clear();

        foreach (var sample in heatmap)
        {
            HeatmapLayer.Children.Add(BuildHeatmapCell(sample));
        }

        foreach (var wall in floor.Walls)
        {
            WallsLayer.Children.Add(BuildWallLine(wall));
        }

        foreach (var testPoint in floor.TestPoints)
        {
            MarkersLayer.Children.Add(BuildTestPointMarker(testPoint));
        }

        foreach (var accessPoint in floor.AccessPoints)
        {
            MarkersLayer.Children.Add(BuildAccessPointMarker(accessPoint));
        }
    }

    private Rectangle BuildHeatmapCell(CoverageSample sample)
    {
        double sizePx = HeatmapCellMeters * PixelsPerMeter;
        var (px, py) = ToPixels(sample.Position);
        var cell = new Rectangle
        {
            Width = sizePx,
            Height = sizePx,
            Fill = new SolidColorBrush(HeatmapColor(sample.ValueDbm)),
            Opacity = HeatmapOpacity,
        };
        Canvas.SetLeft(cell, px - (sizePx / 2));
        Canvas.SetTop(cell, py - (sizePx / 2));
        return cell;
    }

    private Line BuildWallLine(Wall wall)
    {
        var (x1, y1) = ToPixels(wall.Start);
        var (x2, y2) = ToPixels(wall.End);
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = new SolidColorBrush(Colors.DimGray),
            StrokeThickness = WallStrokeThicknessPx,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    private Ellipse BuildTestPointMarker(TestPoint testPoint)
    {
        var (px, py) = ToPixels(testPoint.Position);
        double diameter = TestPointRadiusPx * 2;
        var marker = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(testPoint.Measurements.Count > 0 ? Colors.SeaGreen : Colors.Gray),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
        };
        Canvas.SetLeft(marker, px - TestPointRadiusPx);
        Canvas.SetTop(marker, py - TestPointRadiusPx);
        return marker;
    }

    private Ellipse BuildAccessPointMarker(AccessPoint accessPoint)
    {
        var (px, py) = ToPixels(accessPoint.Position);
        double diameter = AccessPointRadiusPx * 2;
        var marker = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(Colors.OrangeRed),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
        };
        Canvas.SetLeft(marker, px - AccessPointRadiusPx);
        Canvas.SetTop(marker, py - AccessPointRadiusPx);
        return marker;
    }

    private static Color HeatmapColor(double signalDbm) => signalDbm switch
    {
        >= -50 => Colors.Green,
        >= -60 => Colors.YellowGreen,
        >= -67 => Colors.Gold,
        >= -75 => Colors.Orange,
        _ => Colors.Red,
    };

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(RootGrid).Position;
        var meters = ToMeters(position);

        switch (Tool)
        {
            case WorkspaceTool.AddTestPoint:
                TestPointRequested?.Invoke(this, meters);
                break;

            case WorkspaceTool.DrawWall:
                if (_wallAnchor is { } anchor)
                {
                    WallRequested?.Invoke(this, (anchor, meters));
                    _wallAnchor = null;
                }
                else
                {
                    _wallAnchor = meters;
                }

                break;

            case WorkspaceTool.DeleteElement:
                DeleteRequested?.Invoke(this, meters);
                break;

            case WorkspaceTool.Select:
            default:
                break;
        }
    }

    private (double X, double Y) ToPixels(Point2D meters) =>
        ((meters.X - _bounds.MinX) * PixelsPerMeter, (meters.Y - _bounds.MinY) * PixelsPerMeter);

    private Point2D ToMeters(Windows.Foundation.Point pixels) =>
        new(_bounds.MinX + (pixels.X / PixelsPerMeter), _bounds.MinY + (pixels.Y / PixelsPerMeter));

    private static Bounds ComputeBounds(Floor floor)
    {
        var xs = new List<double>();
        var ys = new List<double>();

        foreach (var wall in floor.Walls)
        {
            xs.Add(wall.Start.X);
            xs.Add(wall.End.X);
            ys.Add(wall.Start.Y);
            ys.Add(wall.End.Y);
        }

        foreach (var testPoint in floor.TestPoints)
        {
            xs.Add(testPoint.Position.X);
            ys.Add(testPoint.Position.Y);
        }

        foreach (var accessPoint in floor.AccessPoints)
        {
            xs.Add(accessPoint.Position.X);
            ys.Add(accessPoint.Position.Y);
        }

        if (xs.Count == 0)
        {
            return new Bounds(0, 0, 20, 15);
        }

        double minX = xs.Min() - PaddingMeters;
        double minY = ys.Min() - PaddingMeters;
        double width = Math.Max((xs.Max() - xs.Min()) + (PaddingMeters * 2), MinSpanMeters);
        double height = Math.Max((ys.Max() - ys.Min()) + (PaddingMeters * 2), MinSpanMeters);
        return new Bounds(minX, minY, width, height);
    }

    private readonly record struct Bounds(double MinX, double MinY, double WidthMeters, double HeightMeters);
}
