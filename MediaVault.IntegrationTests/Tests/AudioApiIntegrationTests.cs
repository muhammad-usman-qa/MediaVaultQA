using System.Net;
using System.Net.Http.Json;
using Bogus;
using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Services;
using MediaVault.IntegrationTests.Fixtures;

namespace MediaVault.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Audio API endpoints.
/// Uses WebApplicationFactory (real HTTP stack) + WireMock (mocked storage service).
/// The factory starts WireMock with always-on default stubs — no per-test reset needed.
/// </summary>
public class AudioApiIntegrationTests : IClassFixture<MediaVaultWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly MediaVaultWebApplicationFactory _factory;
    private readonly Faker _faker = new();

    public AudioApiIntegrationTests(MediaVaultWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private CreateAudioFileRequest BuildValidRequest() =>
        new(
            Title: $"{_faker.Lorem.Word()} Remastered",
            Artist: _faker.Name.FullName(),
            DurationSeconds: _faker.Random.Int(60, 600),
            FileSizeBytes: _faker.Random.Long(1_000_000, 20_000_000),
            Format: AudioFormat.Mp3,
            FileName: $"{_faker.Random.Guid()}.mp3",
            Tags: "rock,demo");

    // ── POST /api/audio ──────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Audio_ValidRequest_Returns201WithAudioFile()
    {
        var request = BuildValidRequest();

        var response = await _client.PostAsJsonAsync("/api/audio", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<AudioFile>();
        created.Should().NotBeNull();
        created!.Title.Should().Be(request.Title);
        created.Artist.Should().Be(request.Artist);
        created.BlobUrl.Should().NotBeNullOrEmpty("WireMock storage stub should return a blob URL");
        created.IsProcessed.Should().BeFalse();
        created.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task POST_Audio_EmptyTitle_Returns400BadRequest()
    {
        var request = BuildValidRequest() with { Title = "" };

        var response = await _client.PostAsJsonAsync("/api/audio", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Audio_ZeroDuration_Returns400BadRequest()
    {
        var request = BuildValidRequest() with { DurationSeconds = 0 };

        var response = await _client.PostAsJsonAsync("/api/audio", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Audio_NegativeFileSize_Returns400BadRequest()
    {
        var request = BuildValidRequest() with { FileSizeBytes = 0 };

        var response = await _client.PostAsJsonAsync("/api/audio", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/audio ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Audio_ReturnsListOfAudioFiles()
    {
        await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());
        await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());

        var response = await _client.GetAsync("/api/audio");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await response.Content.ReadFromJsonAsync<List<AudioFile>>();
        files.Should().NotBeNull();
        files!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── GET /api/audio/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GET_AudioById_WhenExists_Returns200WithFile()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<AudioFile>();

        var response = await _client.GetAsync($"/api/audio/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await response.Content.ReadFromJsonAsync<AudioFile>();
        file!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GET_AudioById_WhenNotExists_Returns404()
    {
        var response = await _client.GetAsync($"/api/audio/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/audio/search ────────────────────────────────────────────────

    [Fact]
    public async Task GET_AudioSearch_ReturnsMatchingFiles()
    {
        var uniqueArtist = $"UniqueArtist_{Guid.NewGuid():N}";
        var request = BuildValidRequest() with { Artist = uniqueArtist };
        await _client.PostAsJsonAsync("/api/audio", request);

        var response = await _client.GetAsync($"/api/audio/search?q={Uri.EscapeDataString(uniqueArtist)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<AudioFile>>();
        results.Should().ContainSingle(f => f.Artist == uniqueArtist);
    }

    [Fact]
    public async Task GET_AudioSearch_NoMatches_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/audio/search?q=NOMATCH_XYZZY_99999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<AudioFile>>();
        results.Should().BeEmpty();
    }

    // ── PUT /api/audio/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task PUT_Audio_WhenExists_UpdatesAndReturns200()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<AudioFile>();

        var updateRequest = new UpdateAudioFileRequest("Updated Title", null, "jazz,live", true);
        var response = await _client.PutAsJsonAsync($"/api/audio/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AudioFile>();
        updated!.Title.Should().Be("Updated Title");
        updated.Tags.Should().Be("jazz,live");
        updated.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task PUT_Audio_WhenNotExists_Returns404()
    {
        var updateRequest = new UpdateAudioFileRequest("Title", null, null, null);
        var response = await _client.PutAsJsonAsync($"/api/audio/{Guid.NewGuid()}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/audio/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DELETE_Audio_WhenExists_Returns204()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created, "audio file creation must succeed for delete test");
        var created = await postResponse.Content.ReadFromJsonAsync<AudioFile>();

        var response = await _client.DeleteAsync($"/api/audio/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/audio/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_Audio_WhenNotExists_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/audio/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── WireMock verification ────────────────────────────────────────────────

    [Fact]
    public async Task POST_Audio_VerifiesStorageServiceWasCalled()
    {
        var logsBefore = _factory.StorageMock.LogEntries.Count();

        await _client.PostAsJsonAsync("/api/audio", BuildValidRequest());

        var logsAfter = _factory.StorageMock.LogEntries.Count();
        logsAfter.Should().BeGreaterThan(logsBefore, "creating an audio file should call the storage service");

        var storageCallMade = _factory.StorageMock.LogEntries
            .Any(e => e.RequestMessage.Path == "/storage/register");
        storageCallMade.Should().BeTrue("storage service /storage/register endpoint should have been called");
    }
}
