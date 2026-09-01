using System.Globalization;

namespace Pulsemap.App.Services;

public sealed record SurveySummary(string FilePath, string Name, string? SiteDescription, DateTimeOffset ModifiedAt)
{
    public string ModifiedAtDisplay => ModifiedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
