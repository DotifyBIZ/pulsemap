using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeReportExporter : IReportExporter
{
    public Exception? ExceptionToThrow { get; set; }

    public int ExportPdfCallCount { get; private set; }

    public Task ExportPdfAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ExportPdfCallCount++;
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }
}
