using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;

namespace Pulsemap.App.Tests.Services;

public sealed class SurveyLibraryServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "PulsemapTests", Guid.NewGuid().ToString());
    private readonly ZipSurveyFileService _surveyFileService = new(new FakeAppLogger());
    private readonly SurveyLibraryService _sut;

    public SurveyLibraryServiceTests()
    {
        _sut = new SurveyLibraryService(_surveyFileService, new FakeAppLogger());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private async Task<string> CreateSurveyFileAsync(string name)
    {
        string filePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.pulsemap");
        var survey = new Survey
        {
            Name = name,
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.FiveGhz],
            Floors =
            [
                new Floor
                {
                    PlanSource = new ImagePlanSource { ImageData = [], FileExtension = ".png", PixelsPerMeter = 10 },
                },
            ],
        };
        await _surveyFileService.SaveAsync(survey, filePath);
        return filePath;
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFileFromDisk()
    {
        string filePath = await CreateSurveyFileAsync("To Delete");

        await _sut.DeleteAsync(filePath);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task RenameAsync_UpdatesTheSurveyNameInPlaceWithoutMovingTheFile()
    {
        string filePath = await CreateSurveyFileAsync("Old Name");

        await _sut.RenameAsync(filePath, "New Name");

        Assert.True(File.Exists(filePath));
        var reloaded = await _surveyFileService.LoadAsync(filePath);
        Assert.Equal("New Name", reloaded.Name);
    }
}
