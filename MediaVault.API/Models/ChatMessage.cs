using System.Text.Json.Serialization;

namespace MediaVault.API.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TranscriptId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public MessageSenderType SenderType { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; }

    [JsonIgnore] // Prevents circular serialization: ChatMessage → ChatTranscript → Messages → ChatMessage
    public ChatTranscript Transcript { get; set; } = null!;
}

public enum MessageSenderType { Agent, Customer, Bot }
