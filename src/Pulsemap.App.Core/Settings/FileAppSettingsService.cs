using System.Text.Json;

namespace Pulsemap.App.Core.Settings;

/// <summary>
/// Reads/writes app preferences as JSON at %LocalAppData%\Pulsemap\settings.json — same directory
/// family as FileAppLogger's Logs folder, distinct from where surveys (user data) live under
/// MyDocuments. A missing or corrupt file quietly falls back to defaults rather than failing
/// startup; a failed save is just asked again next launch.
/// </summary>
public sealed class FileAppSettingsService : IAppSettingsService
{
    private readonly string _filePath;

    public FileAppSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulsemap", "settings.json"))
    {
    }

    public FileAppSettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed save (disk full, permissions) just means the preference reverts to its
            // default next launch — not worth failing whatever action triggered the save.
        }
    }
}
