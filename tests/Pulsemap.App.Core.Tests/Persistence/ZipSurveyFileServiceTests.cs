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
            Floors =
            [
                new Floor
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
            ],
        };

        await _sut.SaveAsync(survey, _filePath);
        var loaded = await _sut.LoadAsync(_filePath);

        Assert.Equal(survey.Id, loaded.Id);
        Assert.Equal(survey.Name, loaded.Name);
        Assert.Equal(survey.SiteDescription, loaded.SiteDescription);
        var loadedRooms = Assert.IsType<RoomListSource>(loaded.Floors[0].PlanSource);
        Assert.Single(loadedRooms.Rooms);
        Assert.Equal("Loading Bay", loadedRooms.Rooms[0].Name);
        Assert.Single(loaded.Floors[0].Walls);
        Assert.Equal(WallMaterial.Concrete, loaded.Floors[0].Walls[0].Material);
        Assert.Equal(0.2, loaded.Floors[0].Walls[0].ThicknessMeters);
        Assert.Single(loaded.Floors[0].TestPoints);
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
            Floors =
            [
                new Floor
                {
                    PlanSource = new ImagePlanSource
                    {
                        ImageData = imageBytes,
                        FileExtension = ".png",
                        PixelsPerMeter = 12.5,
                    },
                },
            ],
        };

        await _sut.SaveAsync(survey, _filePath);
        var loaded = await _sut.LoadAsync(_filePath);

        var loadedImage = Assert.IsType<ImagePlanSource>(loaded.Floors[0].PlanSource);
        Assert.Equal(imageBytes, loadedImage.ImageData);
        Assert.Equal(".png", loadedImage.FileExtension);
        Assert.Equal(12.5, loadedImage.PixelsPerMeter);
    }

    [Fact]
    public async Task LoadAsync_SchemaV1File_MigratesSingularFloorIntoFloorsList()
    {
        // A real v1 survey.json — hand-written, not produced by the current SaveAsync, since that
        // always writes the current (v2+) shape. Property names are PascalCase, and enums are their
        // plain numeric underlying value (Drywall=0, NewDeployment=0, TwoPointFourGhz=0) — this
        // service's JsonSerializerOptions configures no JsonStringEnumConverter, so that's the real
        // shape a v1 file would actually have on disk, not a string.
        const string legacyJson = """
            {
              "SchemaVersion": 1,
              "Id": "5f3b6f2a-0000-4000-8000-000000000001",
              "Name": "Legacy Survey",
              "SiteDescription": "Pre-multi-floor file",
              "Type": 0,
              "TargetNetworkSsid": null,
              "TargetBands": [0],
              "CreatedAt": "2026-01-01T00:00:00+00:00",
              "ModifiedAt": "2026-01-02T00:00:00+00:00",
              "Floor": {
                "PlanSource": { "kind": "roomList", "Rooms": [] },
                "Walls": [{ "Start": { "X": 0, "Y": 0 }, "End": { "X": 5, "Y": 0 }, "Material": 0, "ThicknessMeters": 0.02 }],
                "TestPoints": [],
                "AccessPoints": []
              }
            }
            """;

        await using (var stream = new FileStream(_filePath, FileMode.Create))
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("survey.json");
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync(legacyJson);
        }

        var loaded = await _sut.LoadAsync(_filePath);

        Assert.Equal(2, loaded.SchemaVersion);
        Assert.Equal(Guid.Parse("5f3b6f2a-0000-4000-8000-000000000001"), loaded.Id);
        Assert.Equal("Legacy Survey", loaded.Name);
        Assert.Equal("Pre-multi-floor file", loaded.SiteDescription);
        Assert.Equal(SurveyType.NewDeployment, loaded.Type);
        Assert.Equal([Band.TwoPointFourGhz], loaded.TargetBands);
        Assert.Single(loaded.Floors);
        Assert.Equal("Floor 1", loaded.Floors[0].Name);
        Assert.False(loaded.Floors[0].IsOutdoor);
        Assert.Single(loaded.Floors[0].Walls);
        Assert.Empty(loaded.Snapshots);
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
        public string LogDirectory => string.Empty;

        public Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogWarningAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogInfoAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
