using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeSurveyDataExporter : ISurveyDataExporter
{
    public Exception? ExceptionToThrow { get; set; }

    public int ExportTestPointsCsvCallCount { get; private set; }

    public int ExportAccessPointsCsvCallCount { get; private set; }

    public int ExportJsonCallCount { get; private set; }

    public Task ExportTestPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ExportTestPointsCsvCallCount++;
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }

    public Task ExportAccessPointsCsvAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ExportAccessPointsCsvCallCount++;
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }

    public Task ExportJsonAsync(Survey survey, Stream destination, CancellationToken cancellationToken = default)
    {
        ExportJsonCallCount++;
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }
}
