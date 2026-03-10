namespace MediaVault.API.Models;

public class ChatTranscript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public ChatResolutionStatus ResolutionStatus { get; set; } = ChatResolutionStatus.Open;
    public double? SentimentScore { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}

public enum ChatResolutionStatus { Open, Resolved, Escalated, Abandoned }
