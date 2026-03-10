using System.Net;
using System.Net.Http.Json;
using Bogus;
using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Services;
using MediaVault.IntegrationTests.Fixtures;

namespace MediaVault.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Chat Transcript API endpoints.
/// Verifies session creation, message threading, resolution workflows, and statistics.
/// </summary>
public class TranscriptApiIntegrationTests : IClassFixture<MediaVaultWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Faker _faker = new();

    public TranscriptApiIntegrationTests(MediaVaultWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private CreateTranscriptRequest BuildValidRequest() =>
        new(
            AgentId: $"agent-{_faker.Random.Int(100, 999)}",
            CustomerId: $"cust-{_faker.Random.Guid().ToString()[..8]}",
            CustomerName: _faker.Name.FullName());

    // ── POST /api/transcripts ────────────────────────────────────────────────

    [Fact]
    public async Task POST_Transcript_ValidRequest_Returns201WithOpenStatus()
    {
        var request = BuildValidRequest();

        var response = await _client.PostAsJsonAsync("/api/transcripts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ChatTranscript>();
        created.Should().NotBeNull();
        created!.ResolutionStatus.Should().Be(ChatResolutionStatus.Open);
        created.EndedAt.Should().BeNull("session just started");
        created.SessionId.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("", "cust-001", "Jane Doe")]
    [InlineData("agent-001", "", "Jane Doe")]
    [InlineData("agent-001", "cust-001", "")]
    public async Task POST_Transcript_MissingFields_Returns400(
        string agentId, string customerId, string customerName)
    {
        var request = new CreateTranscriptRequest(agentId, customerId, customerName);

        var response = await _client.PostAsJsonAsync("/api/transcripts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/transcripts ─────────────────────────────────────────────────

    [Fact]
    public async Task GET_Transcripts_ReturnsAll()
    {
        await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());

        var response = await _client.GetAsync("/api/transcripts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transcripts = await response.Content.ReadFromJsonAsync<List<ChatTranscript>>();
        transcripts!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ── GET /api/transcripts/{id} ────────────────────────────────────────────

    [Fact]
    public async Task GET_TranscriptById_WhenExists_Returns200()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<ChatTranscript>();

        var response = await _client.GetAsync($"/api/transcripts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transcript = await response.Content.ReadFromJsonAsync<ChatTranscript>();
        transcript!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GET_TranscriptById_WhenNotExists_Returns404()
    {
        var response = await _client.GetAsync($"/api/transcripts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/transcripts/{id}/messages ──────────────────────────────────

    [Fact]
    public async Task POST_Message_AddsMessageToTranscript_Returns201()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var transcript = await postResponse.Content.ReadFromJsonAsync<ChatTranscript>();

        var messageRequest = new AddMessageRequest(
            "agent-001", MessageSenderType.Agent, "Hello, how can I help you today?");

        var response = await _client.PostAsJsonAsync(
            $"/api/transcripts/{transcript!.Id}/messages", messageRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await response.Content.ReadFromJsonAsync<ChatMessage>();
        message.Should().NotBeNull();
        message!.Content.Should().Be("Hello, how can I help you today?");
        message.SenderType.Should().Be(MessageSenderType.Agent);
        message.TranscriptId.Should().Be(transcript.Id);
    }

    [Fact]
    public async Task POST_Message_EmptyContent_Returns400()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var transcript = await postResponse.Content.ReadFromJsonAsync<ChatTranscript>();

        var messageRequest = new AddMessageRequest("agent-001", MessageSenderType.Agent, "");

        var response = await _client.PostAsJsonAsync(
            $"/api/transcripts/{transcript!.Id}/messages", messageRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Message_ToNonExistentTranscript_Returns404()
    {
        var messageRequest = new AddMessageRequest("agent-001", MessageSenderType.Agent, "Hello");

        var response = await _client.PostAsJsonAsync(
            $"/api/transcripts/{Guid.NewGuid()}/messages", messageRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/transcripts/{id}/resolve ────────────────────────────────────

    [Fact]
    public async Task PUT_Resolve_WhenExists_UpdatesStatusAndSetsEndedAt()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<ChatTranscript>();

        var resolveRequest = new { Status = ChatResolutionStatus.Resolved };
        var response = await _client.PutAsJsonAsync(
            $"/api/transcripts/{created!.Id}/resolve", resolveRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await response.Content.ReadFromJsonAsync<ChatTranscript>();
        resolved!.ResolutionStatus.Should().Be(ChatResolutionStatus.Resolved);
        resolved.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PUT_Resolve_WhenNotExists_Returns404()
    {
        var resolveRequest = new { Status = ChatResolutionStatus.Resolved };
        var response = await _client.PutAsJsonAsync(
            $"/api/transcripts/{Guid.NewGuid()}/resolve", resolveRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/transcripts/stats ───────────────────────────────────────────

    [Fact]
    public async Task GET_Stats_ReflectsCurrentStateOfTranscripts()
    {
        // Create 2 sessions and resolve one
        var r1 = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var r2 = await _client.PostAsJsonAsync("/api/transcripts", BuildValidRequest());
        var t1 = await r1.Content.ReadFromJsonAsync<ChatTranscript>();

        await _client.PutAsJsonAsync(
            $"/api/transcripts/{t1!.Id}/resolve",
            new { Status = ChatResolutionStatus.Resolved });

        var statsResponse = await _client.GetAsync("/api/transcripts/stats");

        statsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await statsResponse.Content.ReadFromJsonAsync<TranscriptStats>();
        stats!.Total.Should().BeGreaterThanOrEqualTo(2);
        stats.Resolved.Should().BeGreaterThanOrEqualTo(1);
    }
}
