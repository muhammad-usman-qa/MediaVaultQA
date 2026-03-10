namespace MediaVault.API.Models;

public class EmailRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool HasAttachments { get; set; }
    public int AttachmentCount { get; set; }
    public bool IsRead { get; set; }
    public string FolderId { get; set; } = "Inbox";
}
