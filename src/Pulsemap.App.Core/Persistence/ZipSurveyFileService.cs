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
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(Survey survey, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        survey.ModifiedAt = DateTimeOffset.UtcNow;

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
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

    public async Task<Survey> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var surveyEntry = archive.GetEntry("survey.json")
            ?? throw new InvalidDataException($"'{filePath}' is not a valid Pulsemap survey file — missing survey.json.");

        Survey? survey;
        await using (var entryStream = surveyEntry.Open())
        {
            survey = await JsonSerializer.DeserializeAsync<Survey>(entryStream, SerializerOptions, cancellationToken);
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
                await assetStream.CopyToAsync(memoryStream, cancellationToken);
            }

            imagePlan.ImageData = memoryStream.ToArray();
        }

        return survey;
    }

    private static string AssetEntryName(ImagePlanSource imagePlan) => $"assets/floorplan{imagePlan.FileExtension}";
}
