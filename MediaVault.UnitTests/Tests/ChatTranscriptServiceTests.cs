using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Repositories;
using MediaVault.API.Services;
using MediaVault.UnitTests.Fakes;
using Moq;

namespace MediaVault.UnitTests.Tests;

public class ChatTranscriptServiceTests
{
    private readonly Mock<IRepository<ChatTranscript>> _mockTranscriptRepo;
    private readonly Mock<IRepository<ChatMessage>> _mockMessageRepo;
    private readonly ChatTranscriptService _sut;
    private readonly ChatTranscriptFaker _faker;

    public ChatTranscriptServiceTests()
    {
        _mockTranscriptRepo = new Mock<IRepository<ChatTranscript>>();
        _mockMessageRepo = new Mock<IRepository<ChatMessage>>();
        _sut = new ChatTranscriptService(_mockTranscriptRepo.Object, _mockMessageRepo.Object);
        _faker = new ChatTranscriptFaker();
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllTranscripts()
    {
        var transcripts = _faker.Generate(4);
        _mockTranscriptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(transcripts);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(4);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesOpenTranscript()
    {
        var request = new CreateTranscriptRequest(
            AgentId: "agent-001",
            CustomerId: "cust-abc",
            CustomerName: "Jane Doe");

        _mockTranscriptRepo.Setup(r => r.AddAsync(It.IsAny<ChatTranscript>()))
                           .ReturnsAsync((ChatTranscript t) => t);

        var result = await _sut.CreateAsync(request);

        result.Should().NotBeNull();
        result.AgentId.Should().Be("agent-001");
        result.CustomerId.Should().Be("cust-abc");
        result.CustomerName.Should().Be("Jane Doe");
        result.ResolutionStatus.Should().Be(ChatResolutionStatus.Open);
        result.SessionId.Should().NotBeNullOrEmpty();
        result.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithExplicitSessionId_UsesProvidedSessionId()
    {
        var sessionId = "session-xyz-789";
        var request = new CreateTranscriptRequest("agent-001", "cust-001", "Bob", sessionId);
        _mockTranscriptRepo.Setup(r => r.AddAsync(It.IsAny<ChatTranscript>()))
                           .ReturnsAsync((ChatTranscript t) => t);

        var result = await _sut.CreateAsync(request);

        result.SessionId.Should().Be(sessionId);
    }

    [Theory]
    [InlineData("", "cust-001", "Jane", "Agent ID is required")]
    [InlineData("agent-001", "", "Jane", "Customer ID is required")]
    [InlineData("agent-001", "cust-001", "", "Customer name is required")]
    public async Task CreateAsync_WithInvalidRequest_ThrowsArgumentException(
        string agentId, string customerId, string customerName, string expectedMessage)
    {
        var request = new CreateTranscriptRequest(agentId, customerId, customerName);

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{expectedMessage}*");

        _mockTranscriptRepo.Verify(r => r.AddAsync(It.IsAny<ChatTranscript>()), Times.Never);
    }

    // ── AddMessageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddMessageAsync_WhenTranscriptExists_AddsMessage()
    {
        var transcriptId = Guid.NewGuid();
        var request = new AddMessageRequest("agent-001", MessageSenderType.Agent, "How can I help?");
        _mockTranscriptRepo.Setup(r => r.ExistsAsync(transcriptId)).ReturnsAsync(true);
        _mockMessageRepo.Setup(r => r.AddAsync(It.IsAny<ChatMessage>()))
                        .ReturnsAsync((ChatMessage m) => m);

        var result = await _sut.AddMessageAsync(transcriptId, request);

        result.Should().NotBeNull();
        result!.TranscriptId.Should().Be(transcriptId);
        result.Sender.Should().Be("agent-001");
        result.SenderType.Should().Be(MessageSenderType.Agent);
        result.Content.Should().Be("How can I help?");
        _mockMessageRepo.Verify(r => r.AddAsync(It.IsAny<ChatMessage>()), Times.Once);
    }

    [Fact]
    public async Task AddMessageAsync_WhenTranscriptNotExists_ReturnsNull()
    {
        _mockTranscriptRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var result = await _sut.AddMessageAsync(Guid.NewGuid(),
            new AddMessageRequest("agent", MessageSenderType.Agent, "content"));

        result.Should().BeNull();
        _mockMessageRepo.Verify(r => r.AddAsync(It.IsAny<ChatMessage>()), Times.Never);
    }

    [Fact]
    public async Task AddMessageAsync_WithEmptyContent_ThrowsArgumentException()
    {
        _mockTranscriptRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var act = () => _sut.AddMessageAsync(Guid.NewGuid(),
            new AddMessageRequest("agent", MessageSenderType.Agent, ""));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Message content is required*");
    }

    // ── ResolveAsync ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChatResolutionStatus.Resolved)]
    [InlineData(ChatResolutionStatus.Escalated)]
    [InlineData(ChatResolutionStatus.Abandoned)]
    public async Task ResolveAsync_WhenExists_UpdatesStatusAndSetsEndedAt(ChatResolutionStatus status)
    {
        var transcript = _faker.Open();
        _mockTranscriptRepo.Setup(r => r.GetByIdAsync(transcript.Id)).ReturnsAsync(transcript);

        var result = await _sut.ResolveAsync(transcript.Id, status);

        result.Should().NotBeNull();
        result!.ResolutionStatus.Should().Be(status);
        result.EndedAt.Should().NotBeNull();
        result.EndedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _mockTranscriptRepo.Verify(r => r.UpdateAsync(transcript), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenNotExists_ReturnsNull()
    {
        _mockTranscriptRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ChatTranscript?)null);

        var result = await _sut.ResolveAsync(Guid.NewGuid(), ChatResolutionStatus.Resolved);

        result.Should().BeNull();
        _mockTranscriptRepo.Verify(r => r.UpdateAsync(It.IsAny<ChatTranscript>()), Times.Never);
    }

    // ── GetStatsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        var transcripts = new List<ChatTranscript>
        {
            new() { ResolutionStatus = ChatResolutionStatus.Open, SentimentScore = 0.8 },
            new() { ResolutionStatus = ChatResolutionStatus.Open, SentimentScore = 0.4 },
            new() { ResolutionStatus = ChatResolutionStatus.Resolved, SentimentScore = 0.9 },
            new() { ResolutionStatus = ChatResolutionStatus.Escalated, SentimentScore = -0.5 },
            new() { ResolutionStatus = ChatResolutionStatus.Abandoned, SentimentScore = null }
        };
        _mockTranscriptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(transcripts);

        var stats = await _sut.GetStatsAsync();

        stats.Total.Should().Be(5);
        stats.Open.Should().Be(2);
        stats.Resolved.Should().Be(1);
        stats.Escalated.Should().Be(1);
        stats.Abandoned.Should().Be(1);
        stats.AverageSentimentScore.Should().BeApproximately(0.4, 0.01);
    }

    [Fact]
    public async Task GetStatsAsync_WhenNoTranscriptsWithSentiment_ReturnsZeroAverageSentiment()
    {
        var transcripts = new List<ChatTranscript>
        {
            new() { ResolutionStatus = ChatResolutionStatus.Open, SentimentScore = null }
        };
        _mockTranscriptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(transcripts);

        var stats = await _sut.GetStatsAsync();

        stats.AverageSentimentScore.Should().Be(0.0);
    }

    [Fact]
    public async Task GetStatsAsync_WhenEmpty_ReturnsAllZeroes()
    {
        _mockTranscriptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var stats = await _sut.GetStatsAsync();

        stats.Total.Should().Be(0);
        stats.Open.Should().Be(0);
        stats.AverageSentimentScore.Should().Be(0.0);
    }
}
