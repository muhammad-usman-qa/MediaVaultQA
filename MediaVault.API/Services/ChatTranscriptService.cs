using MediaVault.API.Models;
using MediaVault.API.Repositories;

namespace MediaVault.API.Services;

public class ChatTranscriptService : IChatTranscriptService
{
    private readonly IRepository<ChatTranscript> _transcriptRepository;
    private readonly IRepository<ChatMessage> _messageRepository;

    public ChatTranscriptService(
        IRepository<ChatTranscript> transcriptRepository,
        IRepository<ChatMessage> messageRepository)
    {
        _transcriptRepository = transcriptRepository;
        _messageRepository = messageRepository;
    }

    public async Task<IReadOnlyList<ChatTranscript>> GetAllAsync() =>
        await _transcriptRepository.GetAllAsync();

    public async Task<ChatTranscript?> GetByIdAsync(Guid id) =>
        await _transcriptRepository.GetByIdAsync(id);

    public async Task<ChatTranscript> CreateAsync(CreateTranscriptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
            throw new ArgumentException("Agent ID is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("Customer ID is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("Customer name is required.", nameof(request));

        var transcript = new ChatTranscript
        {
            SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
            AgentId = request.AgentId,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            ResolutionStatus = ChatResolutionStatus.Open
        };

        return await _transcriptRepository.AddAsync(transcript);
    }

    public async Task<ChatMessage?> AddMessageAsync(Guid transcriptId, AddMessageRequest request)
    {
        if (!await _transcriptRepository.ExistsAsync(transcriptId))
            return null;

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Message content is required.", nameof(request));

        var message = new ChatMessage
        {
            TranscriptId = transcriptId,
            Sender = request.Sender,
            SenderType = request.SenderType,
            Content = request.Content
        };

        return await _messageRepository.AddAsync(message);
    }

    public async Task<ChatTranscript?> ResolveAsync(Guid id, ChatResolutionStatus status)
    {
        var transcript = await _transcriptRepository.GetByIdAsync(id);
        if (transcript is null) return null;

        transcript.ResolutionStatus = status;
        transcript.EndedAt = DateTime.UtcNow;
        await _transcriptRepository.UpdateAsync(transcript);
        return transcript;
    }

    public async Task<TranscriptStats> GetStatsAsync()
    {
        var all = await _transcriptRepository.GetAllAsync();
        var sentimentScores = all.Where(t => t.SentimentScore.HasValue)
                                  .Select(t => t.SentimentScore!.Value)
                                  .ToList();

        return new TranscriptStats(
            Total: all.Count,
            Open: all.Count(t => t.ResolutionStatus == ChatResolutionStatus.Open),
            Resolved: all.Count(t => t.ResolutionStatus == ChatResolutionStatus.Resolved),
            Escalated: all.Count(t => t.ResolutionStatus == ChatResolutionStatus.Escalated),
            Abandoned: all.Count(t => t.ResolutionStatus == ChatResolutionStatus.Abandoned),
            AverageSentimentScore: sentimentScores.Count > 0 ? sentimentScores.Average() : 0.0);
    }
}
