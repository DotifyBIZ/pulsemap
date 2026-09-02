using System.Net;
using System.Net.Http;
using System.Text;
using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;

namespace Pulsemap.App.Tests.Services;

public sealed class GitHubUpdateCheckServiceTests
{
    private readonly FakeAppLogger _logger = new();

    [Fact]
    public async Task CheckForUpdateAsync_NewerReleasePublished_ReturnsUpdateAvailable()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"tag_name":"v99.0.0"}""", Encoding.UTF8, "application/json"),
        });
        var sut = new GitHubUpdateCheckService(new FakeHttpClientFactory(handler), _logger);

        var result = await sut.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("v99.0.0", result.LatestVersion);
        Assert.NotNull(result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_RequestThrows_ReturnsNoUpdate()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("offline"));
        var sut = new GitHubUpdateCheckService(new FakeHttpClientFactory(handler), _logger);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ServerErrorStatus_ReturnsNoUpdate()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = new GitHubUpdateCheckService(new FakeHttpClientFactory(handler), _logger);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_MalformedJson_ReturnsNoUpdate()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json"),
        });
        var sut = new GitHubUpdateCheckService(new FakeHttpClientFactory(handler), _logger);

        var result = await sut.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }
}
