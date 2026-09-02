using Pulsemap.App.Core.Settings;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeAppSettingsService : IAppSettingsService
{
    public AppSettings SettingsToReturn { get; set; } = new();

    public AppSettings? LastSaved { get; private set; }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SettingsToReturn);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        LastSaved = settings;
        return Task.CompletedTask;
    }
}
