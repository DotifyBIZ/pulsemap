using Pulsemap.App.Core.Logging;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeAppLogger : IAppLogger
{
    public string LogDirectory { get; set; } = "C:\\FakeLogs";

    public List<string> ErrorMessages { get; } = [];

    public List<string> WarningMessages { get; } = [];

    public List<string> InfoMessages { get; } = [];

    public Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        ErrorMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message, CancellationToken cancellationToken = default)
    {
        WarningMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task LogInfoAsync(string message, CancellationToken cancellationToken = default)
    {
        InfoMessages.Add(message);
        return Task.CompletedTask;
    }
}
