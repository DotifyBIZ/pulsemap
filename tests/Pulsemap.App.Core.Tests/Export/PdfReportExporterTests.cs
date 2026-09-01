using PdfSharp.Pdf.IO;
using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Export;

public sealed class PdfReportExporterTests
{
    private readonly PdfReportExporter _sut = new();

    [Fact]
    public async Task ExportPdfAsync_ProducesAValidReadablePdfWithOnePage()
    {
        var survey = new Survey
        {
            Name = "Riverside Distribution Center",
            SiteDescription = "Client site — warehouse",
            Floor = new Floor { PlanSource = new RoomListSource() },
        };

        using var stream = new MemoryStream();
        await _sut.ExportPdfAsync(survey, stream);
        stream.Position = 0;

        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public async Task ExportPdfAsync_ManyAccessPoints_OverflowsOntoAdditionalPages()
    {
        var floor = new Floor { PlanSource = new RoomListSource() };
        for (int i = 0; i < 80; i++)
        {
            var accessPoint = new AccessPoint { Position = new Point2D(i, i), Label = $"AP {i + 1}" };
            accessPoint.Radios[Band.TwoPointFourGhz] = new BandRadioSettings { TransmitPowerDbm = 17, Channel = 6 };
            floor.AccessPoints.Add(accessPoint);
        }

        var survey = new Survey { Name = "Large Survey", Floor = floor };

        using var stream = new MemoryStream();
        await _sut.ExportPdfAsync(survey, stream);
        stream.Position = 0;

        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.True(document.PageCount > 1, $"Expected report to overflow onto more than one page, got {document.PageCount}.");
    }
}
