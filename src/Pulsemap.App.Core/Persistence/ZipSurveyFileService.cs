using System.IO.Compression;
using System.Text.Json;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Persistence;

/// <summary>
/// Saves/loads a Survey as a .pulsemap file — a zip containing survey.json plus, for every floor
/// whose plan is image-based, an assets/floor-&lt;id&gt;&lt;ext&gt; entry. Keeping images out of the JSON
/// avoids base64 bloat and keeps survey.json readable and diffable.
/// </summary>
public sealed class ZipSurveyFileService(IAppLogger logger) : ISurveyFileService
{
    // Ceilings on decompressed entry size — a zip's declared size can't be trusted, so entries are
    // copied through a bounded copy rather than trusting ZipArchiveEntry.Length. Generous for a
    // real survey.json or floor plan image/PDF, tight enough to stop a decompression-bomb entry.
    private const long MaxSurveyJsonBytes = 50_000_000;
    private const long MaxAssetBytes = 200_000_000;

    private const int CurrentSchemaVersion = 2;

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

                foreach (var floor in survey.Floors)
                {
                    if (floor.PlanSource is ImagePlanSource imagePlan)
                    {
                        var assetEntry = archive.CreateEntry(AssetEntryName(floor, imagePlan), CompressionLevel.Optimal);
                        await using var assetStream = assetEntry.Open();
                        await assetStream.WriteAsync(imagePlan.ImageData, cancellationToken);
                    }
                }
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            File.Delete(tempFilePath);
            if (ex is not OperationCanceledException)
            {
                // CancellationToken.None: a caller-cancelled save shouldn't also cancel the log write.
                await logger.LogErrorAsync($"Failed to save survey to '{filePath}'.", ex, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<Survey> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var surveyEntry = archive.GetEntry("survey.json")
            ?? throw new InvalidDataException($"'{filePath}' is not a valid Pulsemap survey file — missing survey.json.");

        Survey survey;
        bool wasMigratedFromV1;
        try
        {
            await using (var entryStream = surveyEntry.Open())
            using (var boundedStream = new MemoryStream())
            {
                await CopyWithLimitAsync(entryStream, boundedStream, MaxSurveyJsonBytes, cancellationToken);
                boundedStream.Position = 0;

                using var document = await JsonDocument.ParseAsync(boundedStream, cancellationToken: cancellationToken);
                int schemaVersion = document.RootElement.TryGetProperty(nameof(Survey.SchemaVersion), out var versionElement)
                    ? versionElement.GetInt32()
                    : 1;

                wasMigratedFromV1 = schemaVersion < CurrentSchemaVersion;
                survey = wasMigratedFromV1
                    ? MigrateFromV1(document.RootElement)
                    : document.RootElement.Deserialize<Survey>(SerializerOptions)
                        ?? throw new InvalidDataException($"'{filePath}' contains an empty or invalid survey.json.");
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // KeyNotFoundException/InvalidOperationException/FormatException: MigrateFromV1's
            // JsonElement property lookups and typed getters throw these (not JsonException) for a
            // legacy document that's missing an expected property or has one of an unexpected kind.
            await logger.LogErrorAsync($"'{filePath}' contains invalid or corrupted survey.json.", ex, CancellationToken.None);
            throw new InvalidDataException($"'{filePath}' contains an invalid or corrupted survey.json.", ex);
        }

        foreach (var floor in survey.Floors)
        {
            if (floor.PlanSource is not ImagePlanSource imagePlan)
            {
                continue;
            }

            // A migrated v1 file was saved under the old single-floor, no-id asset name; it'll move
            // to the new per-floor name on its next save through SaveAsync above.
            string assetEntryName = wasMigratedFromV1 ? LegacyAssetEntryName(imagePlan) : AssetEntryName(floor, imagePlan);
            var assetEntry = archive.GetEntry(assetEntryName)
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

    // Schema v1 had a single, unnamed `"Floor"` object instead of a `"Floors"` array, and Floor had
    // none of its current Id/Name/IsOutdoor/Level/OutdoorBounds properties. None of those are
    // required, so deserializing the old object straight into the current Floor type works as-is —
    // only Name needs a value, since v1 floors were never named.
    private static Survey MigrateFromV1(JsonElement root)
    {
        var floor = root.GetProperty("Floor").Deserialize<Floor>(SerializerOptions)
            ?? throw new InvalidDataException("Legacy survey.json has a null Floor.");
        floor.Name = "Floor 1";

        return new Survey
        {
            SchemaVersion = CurrentSchemaVersion,
            Id = root.GetProperty("Id").Deserialize<Guid>(SerializerOptions),
            Name = root.GetProperty("Name").GetString() ?? string.Empty,
            SiteDescription = root.TryGetProperty("SiteDescription", out var siteDescription) ? siteDescription.GetString() : null,
            Type = root.GetProperty("Type").Deserialize<SurveyType>(SerializerOptions),
            TargetNetworkSsid = root.TryGetProperty("TargetNetworkSsid", out var ssid) ? ssid.GetString() : null,
            TargetBands = root.GetProperty("TargetBands").Deserialize<List<Band>>(SerializerOptions) ?? [],
            CreatedAt = root.GetProperty("CreatedAt").Deserialize<DateTimeOffset>(SerializerOptions),
            ModifiedAt = root.GetProperty("ModifiedAt").Deserialize<DateTimeOffset>(SerializerOptions),
            Floors = [floor],
        };
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

    private static string AssetEntryName(Floor floor, ImagePlanSource imagePlan) => $"assets/floor-{floor.Id}{imagePlan.FileExtension}";

    private static string LegacyAssetEntryName(ImagePlanSource imagePlan) => $"assets/floorplan{imagePlan.FileExtension}";
}
