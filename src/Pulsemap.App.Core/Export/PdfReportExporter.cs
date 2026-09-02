using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Export;

/// <summary>Generates a printable coverage report: survey summary, floor statistics, and the per-band AP recommendation table.</summary>
public sealed class PdfReportExporter : IReportExporter
{
    private const double MarginPoints = 40;
    private const double LineHeightPoints = 18;

    private static readonly XFont TitleFont;
    private static readonly XFont HeadingFont;
    private static readonly XFont BodyFont;

    static PdfReportExporter()
    {
        GlobalFontSettings.FontResolver ??= WindowsFontResolver.Instance;
        TitleFont = new XFont("Segoe UI", 20, XFontStyleEx.Bold);
        HeadingFont = new XFont("Segoe UI", 13, XFontStyleEx.Bold);
        BodyFont = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
    }

    public Task ExportPdfAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(destination);

        // The PdfSharp calls below are synchronous CPU/disk work with no true async path — run
        // them off the calling thread so awaiting this from the UI thread doesn't block it.
        return Task.Run(() => WritePdf(survey, destination, cancellationToken), cancellationToken);
    }

    private static void WritePdf(Survey survey, Stream destination, CancellationToken cancellationToken)
    {
        using var document = new PdfDocument();
        using var writer = new ReportWriter(document);

        writer.DrawTitle("Pulsemap Coverage Report");
        writer.DrawLine(survey.Name, HeadingFont);
        if (!string.IsNullOrWhiteSpace(survey.SiteDescription))
        {
            writer.DrawLine(survey.SiteDescription, BodyFont);
        }

        writer.DrawLine($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC", BodyFont);
        writer.DrawGap();

        foreach (var floor in survey.Floors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.DrawLine(floor.IsOutdoor ? $"{floor.Name} (outdoor)" : floor.Name, HeadingFont);
            writer.DrawLine($"Walls: {floor.Walls.Count}", BodyFont);
            writer.DrawLine($"Test points: {floor.TestPoints.Count}", BodyFont);
            writer.DrawLine($"Access points: {floor.AccessPoints.Count}", BodyFont);
            writer.DrawGap();

            writer.DrawLine("Access point recommendations", HeadingFont);
            if (floor.AccessPoints.Count == 0)
            {
                writer.DrawLine("No access points suggested or placed yet.", BodyFont);
            }
            else
            {
                foreach (var accessPoint in floor.AccessPoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string overrideNote = accessPoint.IsUserOverride ? " [user-edited]" : string.Empty;
                    writer.DrawLine($"{accessPoint.Label} — ({accessPoint.Position.X:0.0}, {accessPoint.Position.Y:0.0}){overrideNote}", BodyFont);

                    foreach (var (band, radio) in accessPoint.Radios.OrderBy(radioEntry => radioEntry.Key))
                    {
                        writer.DrawLine($"    {FormatBand(band)}: {radio.TransmitPowerDbm:0} dBm, channel {radio.Channel}", BodyFont);
                    }
                }
            }

            writer.DrawGap();
        }

        document.Save(destination);
    }

    private static string FormatBand(Band band) => band switch
    {
        Band.TwoPointFourGhz => "2.4 GHz",
        Band.FiveGhz => "5 GHz",
        Band.SixGhz => "6 GHz",
        _ => band.ToString(),
    };

    private sealed class ReportWriter : IDisposable
    {
        private readonly PdfDocument _document;
        private PdfPage _page;
        private XGraphics _graphics;
        private double _y;

        public ReportWriter(PdfDocument document)
        {
            _document = document;
            _page = document.AddPage();
            _graphics = XGraphics.FromPdfPage(_page);
            _y = MarginPoints;
        }

        public void DrawTitle(string text)
        {
            _graphics.DrawString(text, TitleFont, XBrushes.Black, new XPoint(MarginPoints, _y + TitleFont.Height));
            _y += TitleFont.Height + (LineHeightPoints / 2);
        }

        public void DrawLine(string text, XFont font)
        {
            EnsureSpace();
            _graphics.DrawString(text, font, XBrushes.Black, new XPoint(MarginPoints, _y + font.Height));
            _y += LineHeightPoints;
        }

        public void DrawGap() => _y += LineHeightPoints / 2;

        private void EnsureSpace()
        {
            if (_y + LineHeightPoints < _page.Height.Point - MarginPoints)
            {
                return;
            }

            _graphics.Dispose();
            _page = _document.AddPage();
            _graphics = XGraphics.FromPdfPage(_page);
            _y = MarginPoints;
        }

        public void Dispose() => _graphics.Dispose();
    }
}
