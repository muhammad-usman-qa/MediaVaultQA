using Bogus;
using MediaVault.API.Models;

namespace MediaVault.UnitTests.Fakes;

/// <summary>
/// Generates realistic fake EmailRecord entities for testing.
/// Produces varied email scenarios including attachments, folders, and read states.
/// </summary>
public class EmailRecordFaker : Faker<EmailRecord>
{
    private static readonly string[] Folders = ["Inbox", "Sent", "Archive", "Spam", "Drafts"];

    public EmailRecordFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.MessageId, f => $"<{f.Random.Guid()}@mediavault.com>");
        RuleFor(x => x.Subject, f => f.Lorem.Sentence(4, 4));
        RuleFor(x => x.FromAddress, f => f.Internet.Email());
        RuleFor(x => x.ToAddresses, f => string.Join(";", new[] { f.Internet.Email(), f.Internet.Email() }));
        RuleFor(x => x.Body, f => f.Lorem.Paragraphs(2));
        RuleFor(x => x.ReceivedAt, f => f.Date.Past(1).ToUniversalTime());
        RuleFor(x => x.HasAttachments, f => f.Random.Bool(0.3f));
        RuleFor(x => x.AttachmentCount, (f, e) => e.HasAttachments ? f.Random.Int(1, 5) : 0);
        RuleFor(x => x.IsRead, f => f.Random.Bool(0.6f));
        RuleFor(x => x.FolderId, f => f.PickRandom(Folders));
    }

    /// <summary>Creates an unread inbox email.</summary>
    public EmailRecord UnreadInbox() =>
        RuleFor(x => x.IsRead, _ => false)
        .RuleFor(x => x.FolderId, _ => "Inbox")
        .Generate();

    /// <summary>Creates an email with attachments.</summary>
    public EmailRecord WithAttachments(int count = 2) =>
        RuleFor(x => x.HasAttachments, _ => true)
        .RuleFor(x => x.AttachmentCount, _ => count)
        .Generate();
}
