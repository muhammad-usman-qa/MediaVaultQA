using MediaVault.API.Models;
using MediaVault.API.Repositories;

namespace MediaVault.API.Services;

public class EmailService : IEmailService
{
    private readonly IRepository<EmailRecord> _repository;

    public EmailService(IRepository<EmailRecord> repository) => _repository = repository;

    public async Task<IReadOnlyList<EmailRecord>> GetAllAsync(string? folder = null)
    {
        var all = await _repository.GetAllAsync();
        return folder is null
            ? all
            : all.Where(e => e.FolderId.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<EmailRecord?> GetByIdAsync(Guid id) =>
        await _repository.GetByIdAsync(id);

    public async Task<EmailRecord> IngestAsync(IngestEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Subject is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FromAddress))
            throw new ArgumentException("From address is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ToAddresses))
            throw new ArgumentException("To addresses are required.", nameof(request));
        if (request.AttachmentCount < 0)
            throw new ArgumentException("Attachment count cannot be negative.", nameof(request));

        var email = new EmailRecord
        {
            MessageId = request.MessageId,
            Subject = request.Subject,
            FromAddress = request.FromAddress,
            ToAddresses = request.ToAddresses,
            Body = request.Body,
            ReceivedAt = request.ReceivedAt,
            HasAttachments = request.HasAttachments,
            AttachmentCount = request.AttachmentCount,
            FolderId = request.FolderId,
            IsRead = false
        };

        return await _repository.AddAsync(email);
    }

    public async Task<EmailRecord?> MarkAsReadAsync(Guid id)
    {
        var email = await _repository.GetByIdAsync(id);
        if (email is null) return null;

        email.IsRead = true;
        await _repository.UpdateAsync(email);
        return email;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var email = await _repository.GetByIdAsync(id);
        if (email is null) return false;

        await _repository.DeleteAsync(email);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(string? folder = null)
    {
        var all = await _repository.GetAllAsync();
        return all.Count(e =>
            !e.IsRead &&
            (folder is null || e.FolderId.Equals(folder, StringComparison.OrdinalIgnoreCase)));
    }
}
