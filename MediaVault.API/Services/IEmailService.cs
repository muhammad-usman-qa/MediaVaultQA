using MediaVault.API.Models;

namespace MediaVault.API.Services;

public interface IEmailService
{
    Task<IReadOnlyList<EmailRecord>> GetAllAsync(string? folder = null);
    Task<EmailRecord?> GetByIdAsync(Guid id);
    Task<EmailRecord> IngestAsync(IngestEmailRequest request);
    Task<EmailRecord?> MarkAsReadAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetUnreadCountAsync(string? folder = null);
}

public record IngestEmailRequest(
    string MessageId,
    string Subject,
    string FromAddress,
    string ToAddresses,
    string Body,
    DateTime ReceivedAt,
    bool HasAttachments = false,
    int AttachmentCount = 0,
    string FolderId = "Inbox");
