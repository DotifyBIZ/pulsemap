using Pulsemap.App.Core.Settings;

namespace Pulsemap.App.Core.Tests.Settings;

public sealed class FileAppSettingsServiceTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"pulsemap-settings-tests-{Guid.NewGuid()}", "settings.json");
    private readonly FileAppSettingsService _sut;

    public FileAppSettingsServiceTests()
    {
        _sut = new FileAppSettingsService(_filePath);
    }

    public void Dispose()
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFileYet_ReturnsDefaults()
    {
        var settings = await _sut.LoadAsync();

        Assert.True(settings.CheckForUpdatesEnabled);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTheValue()
    {
        await _sut.SaveAsync(new AppSettings { CheckForUpdatesEnabled = false });

        var settings = await _sut.LoadAsync();

        Assert.False(settings.CheckForUpdatesEnabled);
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefaultsRatherThanThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "{ not valid json");

        var settings = await _sut.LoadAsync();

        Assert.True(settings.CheckForUpdatesEnabled);
    }
}
