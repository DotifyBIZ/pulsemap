using Pulsemap.App.Core.Logging;

namespace Pulsemap.App.Core.Tests.Logging;

public sealed class FileAppLoggerTests : IDisposable
{
    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), $"pulsemap-log-tests-{Guid.NewGuid()}");
    private readonly FileAppLogger _sut;

    public FileAppLoggerTests()
    {
        _sut = new FileAppLogger(_logDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LogErrorAsync_WritesLevelMessageAndExceptionToTodaysFile()
    {
        await _sut.LogErrorAsync("save failed", new InvalidOperationException("disk full"));

        string content = await ReadTodaysLogAsync();
        Assert.Contains("[ERROR] save failed", content, StringComparison.Ordinal);
        Assert.Contains("disk full", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogWarningAsync_WritesWarnLevel()
    {
        await _sut.LogWarningAsync("skipped a corrupt survey file");

        string content = await ReadTodaysLogAsync();
        Assert.Contains("[WARN] skipped a corrupt survey file", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogInfoAsync_WritesInfoLevel()
    {
        await _sut.LogInfoAsync("survey loaded");

        string content = await ReadTodaysLogAsync();
        Assert.Contains("[INFO] survey loaded", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogInfoAsync_TwoCalls_BothLinesPresent()
    {
        await _sut.LogInfoAsync("first");
        await _sut.LogInfoAsync("second");

        string content = await ReadTodaysLogAsync();
        Assert.Contains("first", content, StringComparison.Ordinal);
        Assert.Contains("second", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogErrorAsync_LogDirectoryIsFileNotDirectory_DoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logDirectory)!);
        await File.WriteAllTextAsync(_logDirectory, "blocking this path with a plain file");

        await _sut.LogErrorAsync("should not throw even though the log directory can't be created");

        File.Delete(_logDirectory);
    }

    [Fact]
    public void LogDirectory_ReturnsConfiguredDirectory()
    {
        Assert.Equal(_logDirectory, _sut.LogDirectory);
    }

    private async Task<string> ReadTodaysLogAsync()
    {
        string filePath = Path.Combine(_logDirectory, $"pulsemap-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");
        return await File.ReadAllTextAsync(filePath);
    }
}
