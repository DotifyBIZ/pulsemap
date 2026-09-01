using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Export;

public interface ISurveyDataExporter
{
    Task ExportTestPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default);

    Task ExportAccessPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default);

    Task ExportJsonAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default);
}
