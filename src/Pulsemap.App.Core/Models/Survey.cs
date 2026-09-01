namespace Pulsemap.App.Core.Models;

public sealed class Survey
{
    /// <summary>Bumped whenever a breaking change is made to the survey.json shape, so ZipSurveyFileService can migrate older files.</summary>
    public int SchemaVersion { get; init; } = 1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? SiteDescription { get; set; }

    public required SurveyType Type { get; set; }

    /// <summary>The SSID being audited — set only for <see cref="SurveyType.ExistingNetworkAudit"/>;
    /// a new-deployment survey has no live network yet, so the guided measurement walk captures
    /// ambient interference only when this is null.</summary>
    public string? TargetNetworkSsid { get; set; }

    /// <summary>Bands this survey covers — drives which radios get propagation predictions, heatmaps, and AP placement suggestions. At least one required.</summary>
    public required List<Band> TargetBands { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    public required Floor Floor { get; set; }
}
