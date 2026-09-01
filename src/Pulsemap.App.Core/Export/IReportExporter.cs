using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Export;

public interface IReportExporter
{
    Task ExportPdfAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default);
}
