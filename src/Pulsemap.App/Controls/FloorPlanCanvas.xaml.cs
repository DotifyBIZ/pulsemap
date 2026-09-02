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
    private const double OutdoorResizeHandleRadiusPx = 8;
    private const double OutdoorHitToleranceMeters = 0.5; // matches WorkspaceViewModel's own DeleteHitToleranceMeters
    private const double MinOutdoorSizeMeters = 2;

    private readonly FloorPlanImageCache _imageCache;

    private Bounds _bounds = new(0, 0, 20, 15);
    private Point2D? _wallAnchor;
    private byte[]? _renderedBackgroundImageData;
    private double _backgroundWidthMeters;
    private double _backgroundHeightMeters;
    private int _renderVersion;

    // Outdoor-bounds drag state — resize/move only ever needs one anchored corner (Min) and one
    // dragged corner (Max) rather than four independent handles, the smallest interaction that
    // still covers "resize" and "reposition" without a full multi-handle editor.
    private OutdoorDragMode _outdoorDragMode = OutdoorDragMode.None;
    private Point2D _dragStartPointerMeters;
    private Point2D _dragStartMin;
    private Point2D _dragStartMax;
    private Point2D? _previewBoundsMin;
    private Point2D? _previewBoundsMax;
    private Floor? _lastFloor;
    private IReadOnlyList<CoverageSample> _lastHeatmap = [];
    private Point2D? _lastWalkTarget;
    private IReadOnlyCollection<Wall>? _lastSelectedWalls;
    private IReadOnlyList<Point2D>? _lastRemainingWalkPoints;

    private enum OutdoorDragMode
    {
        None,
        Move,
        Resize,
    }

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

    public event EventHandler<(Point2D Min, Point2D Max)>? OutdoorBoundsChanged;

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
        _lastFloor = floor;
        _lastHeatmap = heatmap;
        _lastWalkTarget = walkTarget;
        _lastSelectedWalls = selectedWalls;
        _lastRemainingWalkPoints = remainingWalkPoints;

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

        if (floor.IsOutdoor
            && (_previewBoundsMin ?? floor.OutdoorBoundsMin) is { } boundsMin
            && (_previewBoundsMax ?? floor.OutdoorBoundsMax) is { } boundsMax)
        {
            MarkersLayer.Children.Add(BuildOutdoorBoundsRectangle(boundsMin, boundsMax));
            MarkersLayer.Children.Add(BuildOutdoorResizeHandle(boundsMax));
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

    private Rectangle BuildOutdoorBoundsRectangle(Point2D min, Point2D max)
    {
        var (x1, y1) = ToPixels(min);
        var (x2, y2) = ToPixels(max);
        var rectangle = new Rectangle
        {
            Width = Math.Abs(x2 - x1),
            Height = Math.Abs(y2 - y1),
            Stroke = new SolidColorBrush(Colors.SeaGreen),
            StrokeThickness = 2,
            StrokeDashArray = [6, 3],
            Fill = new SolidColorBrush(Color.FromArgb(24, 46, 139, 87)),
        };
        Canvas.SetLeft(rectangle, Math.Min(x1, x2));
        Canvas.SetTop(rectangle, Math.Min(y1, y2));
        return rectangle;
    }

    private Ellipse BuildOutdoorResizeHandle(Point2D max)
    {
        var (px, py) = ToPixels(max);
        double diameter = OutdoorResizeHandleRadiusPx * 2;
        var handle = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(Colors.SeaGreen),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
        };
        Canvas.SetLeft(handle, px - OutdoorResizeHandleRadiusPx);
        Canvas.SetTop(handle, py - OutdoorResizeHandleRadiusPx);
        return handle;
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
                if (TryStartOutdoorBoundsDrag(position, meters))
                {
                    RootGrid.CapturePointer(e.Pointer);
                    break;
                }

                WallSelectRequested?.Invoke(this, meters);
                break;

            default:
                break;
        }
    }

    // A click on the resize handle or inside the rectangle body starts a drag instead of firing
    // WallSelectRequested — but only when nothing selectable (test point/AP) is under the click,
    // so precise clicks on markers placed inside an outdoor area still work as before.
    private bool TryStartOutdoorBoundsDrag(Windows.Foundation.Point position, Point2D meters)
    {
        if (_lastFloor is not { IsOutdoor: true } floor
            || (_previewBoundsMin ?? floor.OutdoorBoundsMin) is not { } min
            || (_previewBoundsMax ?? floor.OutdoorBoundsMax) is not { } max
            || IsNearExistingMarker(floor, meters))
        {
            return false;
        }

        var (maxPx, maxPy) = ToPixels(max);
        double handleDx = position.X - maxPx;
        double handleDy = position.Y - maxPy;
        bool onHandle = Math.Sqrt((handleDx * handleDx) + (handleDy * handleDy)) <= OutdoorResizeHandleRadiusPx;
        bool insideBody = meters.X >= min.X && meters.X <= max.X && meters.Y >= min.Y && meters.Y <= max.Y;

        if (!onHandle && !insideBody)
        {
            return false;
        }

        _outdoorDragMode = onHandle ? OutdoorDragMode.Resize : OutdoorDragMode.Move;
        _dragStartPointerMeters = meters;
        _dragStartMin = min;
        _dragStartMax = max;
        return true;
    }

    private static bool IsNearExistingMarker(Floor floor, Point2D at) =>
        floor.TestPoints.Any(tp => tp.Position.DistanceTo(at) <= OutdoorHitToleranceMeters) ||
        floor.AccessPoints.Any(ap => ap.Position.DistanceTo(at) <= OutdoorHitToleranceMeters);

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_outdoorDragMode == OutdoorDragMode.None || _lastFloor is null)
        {
            return;
        }

        var meters = ToMeters(e.GetCurrentPoint(RootGrid).Position);
        double deltaX = meters.X - _dragStartPointerMeters.X;
        double deltaY = meters.Y - _dragStartPointerMeters.Y;

        if (_outdoorDragMode == OutdoorDragMode.Move)
        {
            _previewBoundsMin = new Point2D(_dragStartMin.X + deltaX, _dragStartMin.Y + deltaY);
            _previewBoundsMax = new Point2D(_dragStartMax.X + deltaX, _dragStartMax.Y + deltaY);
        }
        else
        {
            _previewBoundsMin = _dragStartMin;
            _previewBoundsMax = new Point2D(
                Math.Max(_dragStartMin.X + MinOutdoorSizeMeters, _dragStartMax.X + deltaX),
                Math.Max(_dragStartMin.Y + MinOutdoorSizeMeters, _dragStartMax.Y + deltaY));
        }

        RenderCore(_lastFloor, _lastHeatmap, _lastWalkTarget, _lastSelectedWalls, _lastRemainingWalkPoints);
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e) => EndOutdoorBoundsDrag(raiseChanged: true);

    private void RootGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndOutdoorBoundsDrag(raiseChanged: false);

    private void EndOutdoorBoundsDrag(bool raiseChanged)
    {
        if (_outdoorDragMode == OutdoorDragMode.None)
        {
            return;
        }

        _outdoorDragMode = OutdoorDragMode.None;

        if (raiseChanged && _previewBoundsMin is { } min && _previewBoundsMax is { } max)
        {
            OutdoorBoundsChanged?.Invoke(this, (min, max));
        }

        _previewBoundsMin = null;
        _previewBoundsMax = null;
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
        // FloorGrid uses for its candidate grid. Prefers the live drag preview (if any) so the
        // canvas doesn't clip a rectangle being resized/moved past its last-committed extent.
        if (floor.IsOutdoor
            && (_previewBoundsMin ?? floor.OutdoorBoundsMin) is { } outdoorMin
            && (_previewBoundsMax ?? floor.OutdoorBoundsMax) is { } outdoorMax)
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
