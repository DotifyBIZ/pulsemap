namespace Pulsemap.App.Core.Models;

public sealed class Survey
{
    /// <summary>Bumped whenever a breaking change is made to the survey.json shape, so ZipSurveyFileService can migrate older files.</summary>
    public int SchemaVersion { get; init; } = 1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? SiteDescription { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    public required Floor Floor { get; set; }
}
