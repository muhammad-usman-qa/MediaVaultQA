using FluentAssertions;
using MediaVault.E2ETests.Fixtures;
using Microsoft.Playwright;

namespace MediaVault.E2ETests.Tests;

/// <summary>
/// E2E tests for the Chat Transcript API endpoints.
/// Validates session creation, message flow, resolution, and statistics.
/// </summary>
[Collection("E2E")]
public class TranscriptE2ETests : IClassFixture<ApiFixture>
{
    private readonly IAPIRequestContext _api;

    public TranscriptE2ETests(ApiFixture fixture)
    {
        _api = fixture.ApiContext;
    }

    private static Dictionary<string, object> BuildTranscriptPayload(
        string agentId = "agent-e2e-01",
        string customerId = "cust-e2e-01",
        string customerName = "E2E Customer") =>
        new()
        {
            ["agentId"] = agentId,
            ["customerId"] = customerId,
            ["customerName"] = customerName
        };

    private static Dictionary<string, object> BuildMessagePayload(
        string sender = "agent-e2e-01",
        int senderType = 0, // 0 = Agent
        string content = "Hello, how can I help?") =>
        new()
        {
            ["sender"] = sender,
            ["senderType"] = senderType,
            ["content"] = content
        };

    // ── Full conversation flow ────────────────────────────────────────────────

    [Fact]
    public async Task TranscriptFlow_CreateAddMessagesResolve_Succeeds()
    {
        // CREATE session
        var createResp = await _api.PostAsync("/api/transcripts",
            new() { DataObject = BuildTranscriptPayload() });

        createResp.Status.Should().Be(201);
        var created = await createResp.JsonAsync();
        var id = created!.Value.GetProperty("id").GetString();
        created.Value.GetProperty("resolutionStatus").GetString().Should().Be("Open");
        created.Value.TryGetProperty("endedAt", out _); // should be null

        // ADD customer message
        var msg1Resp = await _api.PostAsync($"/api/transcripts/{id}/messages",
            new() { DataObject = BuildMessagePayload("E2E Customer", 1, "I need help with my files.") });
        msg1Resp.Status.Should().Be(201);

        // ADD agent reply
        var msg2Resp = await _api.PostAsync($"/api/transcripts/{id}/messages",
            new() { DataObject = BuildMessagePayload("agent-e2e-01", 0, "I can help with that!") });
        msg2Resp.Status.Should().Be(201);

        // RESOLVE
        var resolveResp = await _api.PutAsync($"/api/transcripts/{id}/resolve",
            new() { DataObject = new Dictionary<string, object> { ["status"] = 1 } }); // 1 = Resolved

        resolveResp.Status.Should().Be(200);
        var resolved = await resolveResp.JsonAsync();
        resolved!.Value.GetProperty("resolutionStatus").GetString().Should().Be("Resolved");
        resolved.Value.TryGetProperty("endedAt", out var endedAt);
        endedAt.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Transcript_MissingAgentId_Returns400()
    {
        var payload = BuildTranscriptPayload(agentId: "");

        var response = await _api.PostAsync("/api/transcripts", new() { DataObject = payload });

        response.Status.Should().Be(400);
    }

    [Fact]
    public async Task POST_Message_EmptyContent_Returns400()
    {
        var createResp = await _api.PostAsync("/api/transcripts",
            new() { DataObject = BuildTranscriptPayload() });
        var created = await createResp.JsonAsync();
        var id = created!.Value.GetProperty("id").GetString();

        var response = await _api.PostAsync($"/api/transcripts/{id}/messages",
            new() { DataObject = BuildMessagePayload(content: "") });

        response.Status.Should().Be(400);
    }

    [Fact]
    public async Task POST_Message_ToNonExistentTranscript_Returns404()
    {
        var response = await _api.PostAsync(
            $"/api/transcripts/{Guid.NewGuid()}/messages",
            new() { DataObject = BuildMessagePayload() });

        response.Status.Should().Be(404);
    }

    // ── Statistics ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Stats_Returns200WithValidStructure()
    {
        var response = await _api.GetAsync("/api/transcripts/stats");

        response.Status.Should().Be(200);
        var stats = await response.JsonAsync();
        stats.Should().NotBeNull();

        var root = stats!.Value;
        root.TryGetProperty("total", out _).Should().BeTrue();
        root.TryGetProperty("open", out _).Should().BeTrue();
        root.TryGetProperty("resolved", out _).Should().BeTrue();
        root.TryGetProperty("escalated", out _).Should().BeTrue();
        root.TryGetProperty("abandoned", out _).Should().BeTrue();
        root.TryGetProperty("averageSentimentScore", out _).Should().BeTrue();
    }
}
