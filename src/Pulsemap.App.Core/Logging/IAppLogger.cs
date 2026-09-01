namespace Pulsemap.App.Core.Logging;

/// <summary>
/// Local troubleshooting log — not telemetry. Nothing here is ever transmitted anywhere; it only
/// ever writes to a local file the user can find and share manually if they hit a problem.
/// </summary>
public interface IAppLogger
{
    Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default);

    Task LogWarningAsync(string message, CancellationToken cancellationToken = default);

    Task LogInfoAsync(string message, CancellationToken cancellationToken = default);
}
