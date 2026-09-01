using System.IO.Compression;
using System.Text.Json;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Persistence;

/// <summary>
/// Saves/loads a Survey as a .pulsemap file — a zip containing survey.json plus, when the floor
/// plan is image-based, an assets/floorplan&lt;ext&gt; entry. Keeping the image out of the JSON
/// avoids base64 bloat and keeps survey.json readable and diffable.
/// </summary>
public sealed class ZipSurveyFileService : ISurveyFileService
{
    // Ceilings on decompressed entry size — a zip's declared size can't be trusted, so entries are
    // copied through a bounded copy rather than trusting ZipArchiveEntry.Length. Generous for a
    // real survey.json or floor plan image/PDF, tight enough to stop a decompression-bomb entry.
    private const long MaxSurveyJsonBytes = 50_000_000;
    private const long MaxAssetBytes = 200_000_000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(Survey survey, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        survey.ModifiedAt = DateTimeOffset.UtcNow;

        // Write to a temp file and swap it in — FileMode.Create truncates the destination
        // immediately, so writing straight to filePath would leave a corrupt file behind if the
        // process dies mid-write (a real risk here since this backs Workspace's auto-save).
        string tempFilePath = filePath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

                var surveyEntry = archive.CreateEntry("survey.json", CompressionLevel.Optimal);
                await using (var entryStream = surveyEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(entryStream, survey, SerializerOptions, cancellationToken);
                }

                if (survey.Floor.PlanSource is ImagePlanSource imagePlan)
                {
                    var assetEntry = archive.CreateEntry(AssetEntryName(imagePlan), CompressionLevel.Optimal);
                    await using var assetStream = assetEntry.Open();
                    await assetStream.WriteAsync(imagePlan.ImageData, cancellationToken);
                }
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch
        {
            File.Delete(tempFilePath);
            throw;
        }
    }

    public async Task<Survey> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var surveyEntry = archive.GetEntry("survey.json")
            ?? throw new InvalidDataException($"'{filePath}' is not a valid Pulsemap survey file — missing survey.json.");

        Survey? survey;
        try
        {
            await using (var entryStream = surveyEntry.Open())
            using (var boundedStream = new MemoryStream())
            {
                await CopyWithLimitAsync(entryStream, boundedStream, MaxSurveyJsonBytes, cancellationToken);
                boundedStream.Position = 0;
                survey = await JsonSerializer.DeserializeAsync<Survey>(boundedStream, SerializerOptions, cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{filePath}' contains an invalid or corrupted survey.json.", ex);
        }

        if (survey is null)
        {
            throw new InvalidDataException($"'{filePath}' contains an empty or invalid survey.json.");
        }

        if (survey.Floor.PlanSource is ImagePlanSource imagePlan)
        {
            var assetEntry = archive.GetEntry(AssetEntryName(imagePlan))
                ?? throw new InvalidDataException($"'{filePath}' references a floor plan image but is missing its asset entry.");

            using var memoryStream = new MemoryStream();
            await using (var assetStream = assetEntry.Open())
            {
                await CopyWithLimitAsync(assetStream, memoryStream, MaxAssetBytes, cancellationToken);
            }

            imagePlan.ImageData = memoryStream.ToArray();
        }

        return survey;
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException("Survey file entry exceeds the maximum allowed decompressed size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static string AssetEntryName(ImagePlanSource imagePlan) => $"assets/floorplan{imagePlan.FileExtension}";
}
