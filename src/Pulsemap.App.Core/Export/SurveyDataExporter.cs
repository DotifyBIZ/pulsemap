using System.Globalization;
using System.Text.Json;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Export;

/// <summary>Raw data export — CSV in long format (one row per band measurement/radio, so the schema stays flat regardless of how many bands a survey covers) and JSON (the full Survey graph).</summary>
public sealed class SurveyDataExporter : ISurveyDataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task ExportTestPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(destination);

        var writer = new StreamWriter(destination, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteLineAsync("FloorName,TestPointId,X,Y,Band,SignalDbm,MeasuredAt,AdapterName");

            foreach (var floor in survey.Floors)
            {
                foreach (var point in floor.TestPoints)
                {
                    foreach (var (band, measurement) in point.Measurements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string[] fields =
                        [
                            floor.Name,
                            point.Id.ToString(),
                            point.Position.X.ToString(CultureInfo.InvariantCulture),
                            point.Position.Y.ToString(CultureInfo.InvariantCulture),
                            band.ToString(),
                            measurement.SignalDbm.ToString(CultureInfo.InvariantCulture),
                            measurement.MeasuredAt.ToString("O", CultureInfo.InvariantCulture),
                            measurement.AdapterName ?? string.Empty,
                        ];
                        await writer.WriteLineAsync(string.Join(',', fields.Select(EscapeCsvField)));
                    }
                }
            }

            await writer.FlushAsync(cancellationToken);
        }
    }

    public async Task ExportAccessPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(destination);

        var writer = new StreamWriter(destination, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteLineAsync("FloorName,AccessPointId,Label,X,Y,Band,TransmitPowerDbm,Channel,IsUserOverride");

            foreach (var floor in survey.Floors)
            {
                foreach (var accessPoint in floor.AccessPoints)
                {
                    foreach (var (band, radio) in accessPoint.Radios)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string[] fields =
                        [
                            floor.Name,
                            accessPoint.Id.ToString(),
                            accessPoint.Label,
                            accessPoint.Position.X.ToString(CultureInfo.InvariantCulture),
                            accessPoint.Position.Y.ToString(CultureInfo.InvariantCulture),
                            band.ToString(),
                            radio.TransmitPowerDbm.ToString(CultureInfo.InvariantCulture),
                            radio.Channel.ToString(CultureInfo.InvariantCulture),
                            accessPoint.IsUserOverride.ToString(CultureInfo.InvariantCulture),
                        ];
                        await writer.WriteLineAsync(string.Join(',', fields.Select(EscapeCsvField)));
                    }
                }
            }

            await writer.FlushAsync(cancellationToken);
        }
    }

    public Task ExportJsonAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(destination);

        return JsonSerializer.SerializeAsync(destination, survey, JsonOptions, cancellationToken);
    }

    private static string EscapeCsvField(string value)
    {
        // A leading =, +, -, @, or tab is interpreted as a formula by Excel/Sheets — prefix with an
        // apostrophe to force text interpretation (standard CSV-injection mitigation, CWE-1236).
        // AccessPoint.Label is free-text and flows here unescaped otherwise.
        if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@' or '\t'))
        {
            value = "'" + value;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
