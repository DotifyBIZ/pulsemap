using System.Globalization;

namespace Pulsemap.App.Core.Logging;

/// <summary>
/// Appends timestamped lines to a daily-rolling text file under
/// %LocalAppData%\Pulsemap\Logs — app diagnostic data, distinct from where surveys (user data)
/// live under MyDocuments. Never throws: a logging failure must never take down the caller that's
/// often already handling its own error.
/// </summary>
public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(30);

    private readonly string _logDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _prunedOldLogs;

    public FileAppLogger()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulsemap", "Logs"))
    {
    }

    public FileAppLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public string LogDirectory => _logDirectory;

    public void Dispose() => _writeLock.Dispose();

    // Daily-rolling files with nothing deleting them accumulate for the life of the install. Run
    // once per process, under the write lock that already serializes this class's file access.
    private void PruneOldLogsOnce()
    {
        if (_prunedOldLogs)
        {
            return;
        }

        _prunedOldLogs = true;

        var cutoff = DateTime.UtcNow - LogRetention;
        foreach (string oldLog in Directory.EnumerateFiles(_logDirectory, "pulsemap-*.log"))
        {
            if (File.GetLastWriteTimeUtc(oldLog) < cutoff)
            {
                File.Delete(oldLog);
            }
        }
    }

    public Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default) =>
        WriteAsync("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}", cancellationToken);

    public Task LogWarningAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("WARN", message, cancellationToken);

    public Task LogInfoAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("INFO", message, cancellationToken);

    private async Task WriteAsync(string level, string message, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            string filePath = Path.Combine(_logDirectory, $"pulsemap-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");
            string line = $"{DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)} [{level}] {message}{Environment.NewLine}";

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await File.AppendAllTextAsync(filePath, line, cancellationToken);
                PruneOldLogsOnce();
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never itself throw — a failure here (disk full, permissions) just
            // means this one line is lost, not that the caller's own error handling breaks too.
        }
    }
}
