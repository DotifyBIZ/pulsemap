using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Services;

/// <summary>
/// Rasterizes/caches a floor plan image or PDF to a local PNG/JPG file so FloorPlanCanvas can load
/// it via BitmapImage's file-Uri path. WinRT's own image/PDF decode APIs (Windows.Graphics.Imaging,
/// Windows.Data.Pdf) require package identity and are not available to this unpackaged app — same
/// failure family as LocalizationService's WinRT footguns, confirmed against Microsoft's own
/// package-identity-required API list rather than assumed.
/// </summary>
public sealed class FloorPlanImageCache
{
    private readonly string _cacheDirectory;
    private readonly IAppLogger _logger;

    public FloorPlanImageCache(IAppLogger logger)
        : this(logger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulsemap", "Cache"))
    {
    }

    public FloorPlanImageCache(IAppLogger logger, string cacheDirectory)
    {
        _logger = logger;
        _cacheDirectory = cacheDirectory;
    }

    /// <summary>Returns a local file path ready for BitmapImage to load, or null if the image/PDF
    /// couldn't be cached (corrupt data, unreadable PDF, disk error) — the caller should just skip
    /// showing a background rather than fail the whole canvas render.</summary>
    public async Task<string?> GetOrCreateAsync(ImagePlanSource imagePlan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePlan);

        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            bool isPdf = string.Equals(imagePlan.FileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);
            string cacheKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imagePlan.ImageData));
            string cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.png");

            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            if (isPdf)
            {
                using var pdfStream = new MemoryStream(imagePlan.ImageData);
                PDFtoImage.Conversion.SavePng(cachePath, pdfStream, page: 0);
            }
            else
            {
                await File.WriteAllBytesAsync(cachePath, imagePlan.ImageData, cancellationToken);
            }

            return cachePath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.LogErrorAsync("Failed to cache floor plan background image.", ex, CancellationToken.None);
            return null;
        }
    }
}
