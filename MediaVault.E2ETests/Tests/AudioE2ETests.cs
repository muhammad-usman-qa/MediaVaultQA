using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using MediaVault.E2ETests.Fixtures;
using Microsoft.Playwright;

namespace MediaVault.E2ETests.Tests;

/// <summary>
/// E2E tests for the Audio API endpoints using Playwright APIRequestContext.
/// These tests exercise the full HTTP stack as a black box — no mocks, no internals.
/// The API uses an in-memory storage stub so no real blob storage is required.
/// </summary>
[Collection("E2E")]
public class AudioE2ETests : IClassFixture<ApiFixture>
{
    private readonly IAPIRequestContext _api;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AudioE2ETests(ApiFixture fixture)
    {
        _api = fixture.ApiContext;
    }

    private static Dictionary<string, object> BuildAudioPayload(
        string title = "Test Track",
        string artist = "Test Artist",
        int duration = 240,
        long fileSize = 3_000_000,
        string format = "Mp3",
        string fileName = "track.mp3",
        string tags = "rock") =>
        new()
        {
            ["title"] = title,
            ["artist"] = artist,
            ["durationSeconds"] = duration,
            ["fileSizeBytes"] = fileSize,
            ["format"] = format,
            ["fileName"] = fileName,
            ["tags"] = tags
        };

    // ── CRUD flow ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AudioCRUD_FullLifecycle_Succeeds()
    {
        // CREATE
        var createResponse = await _api.PostAsync("/api/audio",
            new() { DataObject = BuildAudioPayload("E2E Track", "E2E Artist") });

        createResponse.Status.Should().Be(201);
        var body = await createResponse.JsonAsync();
        var id = body!.Value.GetProperty("id").GetString();
        id.Should().NotBeNullOrEmpty();

        // READ
        var getResponse = await _api.GetAsync($"/api/audio/{id}");
        getResponse.Status.Should().Be(200);
        var audioJson = await getResponse.JsonAsync();
        audioJson!.Value.GetProperty("title").GetString().Should().Be("E2E Track");
        audioJson.Value.GetProperty("artist").GetString().Should().Be("E2E Artist");
        audioJson.Value.GetProperty("isProcessed").GetBoolean().Should().BeFalse();

        // UPDATE
        var updateResponse = await _api.PutAsync($"/api/audio/{id}",
            new() { DataObject = new Dictionary<string, object?>
            {
                ["title"] = "Updated E2E Track",
                ["artist"] = null,
                ["tags"] = "jazz,live",
                ["isProcessed"] = true
            }});

        updateResponse.Status.Should().Be(200);
        var updatedJson = await updateResponse.JsonAsync();
        updatedJson!.Value.GetProperty("title").GetString().Should().Be("Updated E2E Track");
        updatedJson.Value.GetProperty("isProcessed").GetBoolean().Should().BeTrue();

        // DELETE
        var deleteResponse = await _api.DeleteAsync($"/api/audio/{id}");
        deleteResponse.Status.Should().Be(204);

        // VERIFY GONE
        var getAfterDelete = await _api.GetAsync($"/api/audio/{id}");
        getAfterDelete.Status.Should().Be(404);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_AudioSearch_FindsFilesByArtist()
    {
        var uniqueArtist = $"E2EArtist_{Guid.NewGuid():N}";

        await _api.PostAsync("/api/audio",
            new() { DataObject = BuildAudioPayload(artist: uniqueArtist) });

        var searchResponse = await _api.GetAsync(
            $"/api/audio/search?q={Uri.EscapeDataString(uniqueArtist)}");

        searchResponse.Status.Should().Be(200);
        var results = await searchResponse.JsonAsync();
        results!.Value.GetArrayLength().Should().BeGreaterThan(0);

        var titles = results.Value.EnumerateArray()
            .Select(r => r.GetProperty("artist").GetString())
            .ToList();
        titles.Should().Contain(uniqueArtist);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Audio_MissingTitle_Returns400()
    {
        var payload = BuildAudioPayload(title: "");

        var response = await _api.PostAsync("/api/audio", new() { DataObject = payload });

        response.Status.Should().Be(400);
    }

    [Fact]
    public async Task POST_Audio_ZeroDuration_Returns400()
    {
        var payload = BuildAudioPayload(duration: 0);

        var response = await _api.PostAsync("/api/audio", new() { DataObject = payload });

        response.Status.Should().Be(400);
    }

    [Fact]
    public async Task GET_Audio_UnknownId_Returns404()
    {
        var response = await _api.GetAsync($"/api/audio/{Guid.NewGuid()}");

        response.Status.Should().Be(404);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_AllAudio_ReturnsJsonArray()
    {
        var response = await _api.GetAsync("/api/audio");

        response.Status.Should().Be(200);
        var json = await response.JsonAsync();
        json!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }
}
