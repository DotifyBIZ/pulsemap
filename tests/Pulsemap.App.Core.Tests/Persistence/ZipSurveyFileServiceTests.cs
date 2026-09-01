using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;

namespace Pulsemap.App.Core.Tests.Persistence;

public sealed class ZipSurveyFileServiceTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pulsemap");
    private readonly ZipSurveyFileService _sut = new(new NoOpAppLogger());

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsRoomListSurvey()
    {
        var survey = new Survey
        {
            Name = "Riverside Distribution Center",
            SiteDescription = "Client site — warehouse",
            Type = SurveyType.ExistingNetworkAudit,
            TargetBands = [Band.TwoPointFourGhz, Band.FiveGhz],
            Floor = new Floor
            {
                PlanSource = new RoomListSource
                {
                    Rooms = { new RoomListEntry { Name = "Loading Bay", WidthMeters = 20, LengthMeters = 30 } },
                },
                Walls =
                {
                    new Wall { Start = new Point2D(0, 0), End = new Point2D(10, 0), Material = WallMaterial.Concrete, ThicknessMeters = 0.2 },
                },
                TestPoints = { new TestPoint { Position = new Point2D(5, 5) } },
            },
        };

        await _sut.SaveAsync(survey, _filePath);
        var loaded = await _sut.LoadAsync(_filePath);

        Assert.Equal(survey.Id, loaded.Id);
        Assert.Equal(survey.Name, loaded.Name);
        Assert.Equal(survey.SiteDescription, loaded.SiteDescription);
        var loadedRooms = Assert.IsType<RoomListSource>(loaded.Floor.PlanSource);
        Assert.Single(loadedRooms.Rooms);
        Assert.Equal("Loading Bay", loadedRooms.Rooms[0].Name);
        Assert.Single(loaded.Floor.Walls);
        Assert.Equal(WallMaterial.Concrete, loaded.Floor.Walls[0].Material);
        Assert.Equal(0.2, loaded.Floor.Walls[0].ThicknessMeters);
        Assert.Single(loaded.Floor.TestPoints);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsImagePlanBytes()
    {
        byte[] imageBytes = [1, 2, 3, 4, 5, 255, 0, 128];
        var survey = new Survey
        {
            Name = "Dotify HQ",
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.FiveGhz],
            Floor = new Floor
            {
                PlanSource = new ImagePlanSource
                {
                    ImageData = imageBytes,
                    FileExtension = ".png",
                    PixelsPerMeter = 12.5,
                },
            },
        };

        await _sut.SaveAsync(survey, _filePath);
        var loaded = await _sut.LoadAsync(_filePath);

        var loadedImage = Assert.IsType<ImagePlanSource>(loaded.Floor.PlanSource);
        Assert.Equal(imageBytes, loadedImage.ImageData);
        Assert.Equal(".png", loadedImage.FileExtension);
        Assert.Equal(12.5, loadedImage.PixelsPerMeter);
    }

    [Fact]
    public async Task LoadAsync_MissingSurveyJson_ThrowsInvalidDataException()
    {
        await using (var stream = new FileStream(_filePath, FileMode.Create))
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("not-a-survey.txt");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.LoadAsync(_filePath));
    }

    private sealed class NoOpAppLogger : IAppLogger
    {
        public Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogWarningAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogInfoAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
