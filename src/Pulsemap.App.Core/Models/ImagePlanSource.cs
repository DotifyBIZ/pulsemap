using System.Text.Json.Serialization;

namespace Pulsemap.App.Core.Models;

/// <summary>A floor plan drawn on top of an uploaded image or PDF. The raw bytes travel separately as a zip asset entry (see ZipSurveyFileService), never inline in survey.json.</summary>
public sealed class ImagePlanSource : FloorPlanSource
{
    [JsonIgnore]
    public byte[] ImageData { get; set; } = [];

    /// <summary>Includes the leading dot, e.g. ".png", ".pdf".</summary>
    public required string FileExtension { get; set; }

    public required double PixelsPerMeter { get; set; }
}
