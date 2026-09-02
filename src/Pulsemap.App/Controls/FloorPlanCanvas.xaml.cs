using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Services;
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
    private const double WalkTargetRadiusPx = 14;
    private const double WallStrokeThicknessPx = 4;
    private const double HeatmapOpacity = 0.55;

    private readonly FloorPlanImageCache _imageCache;

    private Bounds _bounds = new(0, 0, 20, 15);
    private Point2D? _wallAnchor;
    private byte[]? _renderedBackgroundImageData;
    private double _backgroundWidthMeters;
    private double _backgroundHeightMeters;
    private int _renderVersion;

    public FloorPlanCanvas()
    {
        InitializeComponent();
        _imageCache = App.Services.GetRequiredService<FloorPlanImageCache>();
    }

    public WorkspaceTool Tool { get; set; } = WorkspaceTool.Select;

    public event EventHandler<Point2D>? TestPointRequested;

    public event EventHandler<(Point2D Start, Point2D End)>? WallRequested;

    public event EventHandler<Point2D>? DeleteRequested;

    public event EventHandler<Point2D>? WallSelectRequested;

    /// <summary>
    /// Renders walls/points/heatmap immediately, plus (for an image-style floor plan) the
    /// background image/PDF once it's decoded — decoding only happens the first time a given
    /// <see cref="ImagePlanSource.ImageData"/> is seen, cached thereafter by array reference. If a
    /// newer <see cref="RenderAsync"/> call starts while this one is still decoding, this call
    /// abandons itself rather than overwriting the newer render's result.
    /// </summary>
    public async Task RenderAsync(
        Floor floor,
        IReadOnlyList<CoverageSample> heatmap,
        Point2D? walkTarget = null,
        IReadOnlyCollection<Wall>? selectedWalls = null,
        IReadOnlyList<Point2D>? remainingWalkPoints = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(heatmap);

        int myRenderVersion = ++_renderVersion;

        if (floor.PlanSource is ImagePlanSource imagePlan)
        {
            if (!ReferenceEquals(imagePlan.ImageData, _renderedBackgroundImageData))
            {
                string? cachePath = await _imageCache.GetOrCreateAsync(imagePlan, cancellationToken);
                if (myRenderVersion != _renderVersion)
                {
                    return;
                }

                if (cachePath is not null)
                {
                    var bitmap = new BitmapImage();
                    var (pixelWidth, pixelHeight) = await LoadAndMeasureAsync(bitmap, cachePath);
                    BackgroundLayer.Source = bitmap;
                    _backgroundWidthMeters = pixelWidth / imagePlan.PixelsPerMeter;
                    _backgroundHeightMeters = pixelHeight / imagePlan.PixelsPerMeter;
                }
                else
                {
                    BackgroundLayer.Source = null;
                    _backgroundWidthMeters = 0;
                    _backgroundHeightMeters = 0;
                }

                _renderedBackgroundImageData = imagePlan.ImageData;
            }
        }
        else if (_renderedBackgroundImageData is not null)
        {
            BackgroundLayer.Source = null;
            _renderedBackgroundImageData = null;
            _backgroundWidthMeters = 0;
            _backgroundHeightMeters = 0;
        }

        RenderCore(floor, heatmap, walkTarget, selectedWalls, remainingWalkPoints);
    }

    private static Task<(int PixelWidth, int PixelHeight)> LoadAndMeasureAsync(BitmapImage bitmap, string filePath)
    {
        var completion = new TaskCompletionSource<(int, int)>();

        void OnOpened(object sender, RoutedEventArgs e)
        {
            bitmap.ImageOpened -= OnOpened;
            bitmap.ImageFailed -= OnFailed;
            completion.TrySetResult((bitmap.PixelWidth, bitmap.PixelHeight));
        }

        void OnFailed(object sender, ExceptionRoutedEventArgs e)
        {
            bitmap.ImageOpened -= OnOpened;
            bitmap.ImageFailed -= OnFailed;
            completion.TrySetResult((0, 0));
        }

        bitmap.ImageOpened += OnOpened;
        bitmap.ImageFailed += OnFailed;
        bitmap.UriSource = new Uri(filePath);

        return completion.Task;
    }

    private void RenderCore(
        Floor floor,
        IReadOnlyList<CoverageSample> heatmap,
        Point2D? walkTarget,
        IReadOnlyCollection<Wall>? selectedWalls,
        IReadOnlyList<Point2D>? remainingWalkPoints)
    {
        _bounds = ComputeBounds(floor);
        RootGrid.Width = _bounds.WidthMeters * PixelsPerMeter;
        RootGrid.Height = _bounds.HeightMeters * PixelsPerMeter;

        // The background image's own local (0,0) anchors to the floor's meter-space origin —
        // there's no calibration/offset UI today, so this is the only sane default.
        var (originX, originY) = ToPixels(new Point2D(0, 0));
        BackgroundLayer.Margin = new Thickness(originX, originY, 0, 0);
        BackgroundLayer.Width = _backgroundWidthMeters * PixelsPerMeter;
        BackgroundLayer.Height = _backgroundHeightMeters * PixelsPerMeter;

        HeatmapLayer.Children.Clear();
        WallsLayer.Children.Clear();
        MarkersLayer.Children.Clear();

        foreach (var sample in heatmap)
        {
            HeatmapLayer.Children.Add(BuildHeatmapCell(sample));
        }

        foreach (var wall in floor.Walls)
        {
            WallsLayer.Children.Add(BuildWallLine(wall, selectedWalls?.Contains(wall) == true));
        }

        foreach (var testPoint in floor.TestPoints)
        {
            MarkersLayer.Children.Add(BuildTestPointMarker(testPoint));
        }

        foreach (var accessPoint in floor.AccessPoints)
        {
            MarkersLayer.Children.Add(BuildAccessPointMarker(accessPoint));
        }

        foreach (var upcoming in remainingWalkPoints ?? [])
        {
            MarkersLayer.Children.Add(BuildRemainingWalkPointMarker(upcoming));
        }

        if (walkTarget is { } target)
        {
            MarkersLayer.Children.Add(BuildWalkTargetMarker(target));
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

    private Line BuildWallLine(Wall wall, bool isSelected)
    {
        var (x1, y1) = ToPixels(wall.Start);
        var (x2, y2) = ToPixels(wall.End);
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = new SolidColorBrush(isSelected ? Colors.DodgerBlue : Colors.DimGray),
            StrokeThickness = isSelected ? WallStrokeThicknessPx + 2 : WallStrokeThicknessPx,
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

    private Ellipse BuildWalkTargetMarker(Point2D position)
    {
        var (px, py) = ToPixels(position);
        double diameter = WalkTargetRadiusPx * 2;
        var marker = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Stroke = new SolidColorBrush(Colors.MediumPurple),
            StrokeThickness = 3,
            StrokeDashArray = [4, 2],
        };
        Canvas.SetLeft(marker, px - WalkTargetRadiusPx);
        Canvas.SetTop(marker, py - WalkTargetRadiusPx);
        return marker;
    }

    private Ellipse BuildRemainingWalkPointMarker(Point2D position)
    {
        var (px, py) = ToPixels(position);
        double diameter = WalkTargetRadiusPx;
        var marker = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Stroke = new SolidColorBrush(Colors.MediumPurple),
            StrokeThickness = 1.5,
            Opacity = 0.5,
        };
        Canvas.SetLeft(marker, px - (diameter / 2));
        Canvas.SetTop(marker, py - (diameter / 2));
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
                WallSelectRequested?.Invoke(this, meters);
                break;

            default:
                break;
        }
    }

    private (double X, double Y) ToPixels(Point2D meters) =>
        ((meters.X - _bounds.MinX) * PixelsPerMeter, (meters.Y - _bounds.MinY) * PixelsPerMeter);

    private Point2D ToMeters(Windows.Foundation.Point pixels) =>
        new(_bounds.MinX + (pixels.X / PixelsPerMeter), _bounds.MinY + (pixels.Y / PixelsPerMeter));

    private Bounds ComputeBounds(Floor floor)
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

        // Background image anchors at meter-space (0,0) — include its far corner so the canvas
        // sizes to fit the whole image even before any walls are drawn on top of it.
        if (_backgroundWidthMeters > 0 && _backgroundHeightMeters > 0)
        {
            xs.Add(0);
            xs.Add(_backgroundWidthMeters);
            ys.Add(0);
            ys.Add(_backgroundHeightMeters);
        }

        // An outdoor area has no walls to size the canvas from — same explicit-bounds fallback
        // FloorGrid uses for its candidate grid.
        if (floor.IsOutdoor && floor.OutdoorBoundsMin is { } outdoorMin && floor.OutdoorBoundsMax is { } outdoorMax)
        {
            xs.Add(outdoorMin.X);
            xs.Add(outdoorMax.X);
            ys.Add(outdoorMin.Y);
            ys.Add(outdoorMax.Y);
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
