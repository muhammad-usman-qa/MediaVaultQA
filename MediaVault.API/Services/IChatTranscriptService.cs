using MediaVault.API.Models;

namespace MediaVault.API.Services;

public interface IChatTranscriptService
{
    Task<IReadOnlyList<ChatTranscript>> GetAllAsync();
    Task<ChatTranscript?> GetByIdAsync(Guid id);
    Task<ChatTranscript> CreateAsync(CreateTranscriptRequest request);
    Task<ChatMessage?> AddMessageAsync(Guid transcriptId, AddMessageRequest request);
    Task<ChatTranscript?> ResolveAsync(Guid id, ChatResolutionStatus status);
    Task<TranscriptStats> GetStatsAsync();
}

public record CreateTranscriptRequest(
    string AgentId,
    string CustomerId,
    string CustomerName,
    string? SessionId = null);

public record AddMessageRequest(
    string Sender,
    MessageSenderType SenderType,
    string Content);

public record TranscriptStats(
    int Total,
    int Open,
    int Resolved,
    int Escalated,
    int Abandoned,
    double AverageSentimentScore);
