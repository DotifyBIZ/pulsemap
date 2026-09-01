using Pulsemap.App.Core.Models;
using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;
using PdfSharp.Pdf;

namespace Pulsemap.App.Tests.Services;

public sealed class FloorPlanImageCacheTests : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"pulsemap-image-cache-tests-{Guid.NewGuid()}");
    private readonly FakeAppLogger _logger = new();
    private readonly FloorPlanImageCache _sut;

    public FloorPlanImageCacheTests()
    {
        _sut = new FloorPlanImageCache(_logger, _cacheDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_Png_CachesRawBytesAsAFile()
    {
        byte[] pngBytes = MinimalPng();
        var imagePlan = new ImagePlanSource { ImageData = pngBytes, FileExtension = ".png", PixelsPerMeter = 100 };

        string? path = await _sut.GetOrCreateAsync(imagePlan);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task GetOrCreateAsync_Pdf_RasterizesFirstPageToARealPngFile()
    {
        byte[] pdfBytes = MinimalPdf();
        var imagePlan = new ImagePlanSource { ImageData = pdfBytes, FileExtension = ".pdf", PixelsPerMeter = 100 };

        string? path = await _sut.GetOrCreateAsync(imagePlan);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        byte[] pngBytes = await File.ReadAllBytesAsync(path!);
        Assert.True(pngBytes.Length > 8);
        // PNG signature (89 50 4E 47 0D 0A 1A 0A) — confirms PDFium/SkiaSharp actually produced a
        // real PNG, not just an empty or garbage file, in this unpackaged environment.
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(pngSignature, pngBytes[..8]);
    }

    [Fact]
    public async Task GetOrCreateAsync_SameImageDataTwice_ReturnsSameCachedPath()
    {
        byte[] pngBytes = MinimalPng();
        var imagePlan = new ImagePlanSource { ImageData = pngBytes, FileExtension = ".png", PixelsPerMeter = 100 };

        string? first = await _sut.GetOrCreateAsync(imagePlan);
        string? second = await _sut.GetOrCreateAsync(imagePlan);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetOrCreateAsync_CorruptPdfBytes_ReturnsNullAndLogsRatherThanThrowing()
    {
        var imagePlan = new ImagePlanSource { ImageData = [1, 2, 3, 4, 5], FileExtension = ".pdf", PixelsPerMeter = 100 };

        string? path = await _sut.GetOrCreateAsync(imagePlan);

        Assert.Null(path);
        Assert.NotEmpty(_logger.ErrorMessages);
    }

    private static byte[] MinimalPng()
    {
        // 1x1 transparent PNG — smallest valid PNG file, enough to exercise the raw-copy path.
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    }

    private static byte[] MinimalPdf()
    {
        using var document = new PdfDocument();
        document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
