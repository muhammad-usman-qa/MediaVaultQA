using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Repositories;
using MediaVault.API.Services;
using MediaVault.UnitTests.Fakes;
using Moq;

namespace MediaVault.UnitTests.Tests;

public class EmailServiceTests
{
    private readonly Mock<IRepository<EmailRecord>> _mockRepository;
    private readonly EmailService _sut;
    private readonly EmailRecordFaker _faker;

    public EmailServiceTests()
    {
        _mockRepository = new Mock<IRepository<EmailRecord>>();
        _sut = new EmailService(_mockRepository.Object);
        _faker = new EmailRecordFaker();
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithNoFilter_ReturnsAllEmails()
    {
        var emails = _faker.Generate(8);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emails);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(8);
    }

    [Fact]
    public async Task GetAllAsync_WithFolderFilter_ReturnsOnlyMatchingFolder()
    {
        var emails = new List<EmailRecord>
        {
            new() { Subject = "A", FromAddress = "a@test.com", ToAddresses = "b@test.com", MessageId = "1", FolderId = "Inbox" },
            new() { Subject = "B", FromAddress = "a@test.com", ToAddresses = "b@test.com", MessageId = "2", FolderId = "Inbox" },
            new() { Subject = "C", FromAddress = "a@test.com", ToAddresses = "b@test.com", MessageId = "3", FolderId = "Archive" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emails);

        var result = await _sut.GetAllAsync("Inbox");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.FolderId.Should().Be("Inbox"));
    }

    [Fact]
    public async Task GetAllAsync_FolderFilter_IsCaseInsensitive()
    {
        var emails = new List<EmailRecord>
        {
            new() { Subject = "A", FromAddress = "a@test.com", ToAddresses = "b@test.com", MessageId = "1", FolderId = "INBOX" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emails);

        var result = await _sut.GetAllAsync("inbox");

        result.Should().HaveCount(1);
    }

    // ── IngestAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IngestAsync_WithValidRequest_CreatesEmailRecord()
    {
        var receivedAt = DateTime.UtcNow.AddMinutes(-5);
        var request = new IngestEmailRequest(
            MessageId: "<msg-001@mediavault.com>",
            Subject: "Q3 Report",
            FromAddress: "cfo@company.com",
            ToAddresses: "team@company.com;cto@company.com",
            Body: "Please review the attached Q3 report.",
            ReceivedAt: receivedAt,
            HasAttachments: true,
            AttachmentCount: 1,
            FolderId: "Inbox");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<EmailRecord>()))
                       .ReturnsAsync((EmailRecord e) => e);

        var result = await _sut.IngestAsync(request);

        result.Should().NotBeNull();
        result.Subject.Should().Be("Q3 Report");
        result.FromAddress.Should().Be("cfo@company.com");
        result.HasAttachments.Should().BeTrue();
        result.AttachmentCount.Should().Be(1);
        result.IsRead.Should().BeFalse("newly ingested emails are unread");
        result.FolderId.Should().Be("Inbox");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<EmailRecord>()), Times.Once);
    }

    [Theory]
    [InlineData("", "sender@x.com", "recipient@x.com", "Subject is required")]
    [InlineData("   ", "sender@x.com", "recipient@x.com", "Subject is required")]
    [InlineData("Hello", "", "recipient@x.com", "From address is required")]
    [InlineData("Hello", "sender@x.com", "", "To addresses are required")]
    public async Task IngestAsync_WithInvalidRequest_ThrowsArgumentException(
        string subject, string from, string to, string expectedMessage)
    {
        var request = new IngestEmailRequest("<id>", subject, from, to, "body", DateTime.UtcNow);

        var act = () => _sut.IngestAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{expectedMessage}*");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<EmailRecord>()), Times.Never);
    }

    [Fact]
    public async Task IngestAsync_WithNegativeAttachmentCount_ThrowsArgumentException()
    {
        var request = new IngestEmailRequest(
            "<id>", "Subject", "from@x.com", "to@x.com", "body", DateTime.UtcNow,
            HasAttachments: true, AttachmentCount: -1);

        var act = () => _sut.IngestAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Attachment count cannot be negative*");
    }

    // ── MarkAsReadAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_WhenExists_MarksEmailAsRead()
    {
        var email = _faker.UnreadInbox();
        email.IsRead = false;
        _mockRepository.Setup(r => r.GetByIdAsync(email.Id)).ReturnsAsync(email);

        var result = await _sut.MarkAsReadAsync(email.Id);

        result.Should().NotBeNull();
        result!.IsRead.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(email), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotExists_ReturnsNull()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((EmailRecord?)null);

        var result = await _sut.MarkAsReadAsync(Guid.NewGuid());

        result.Should().BeNull();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<EmailRecord>()), Times.Never);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenExists_DeletesAndReturnsTrue()
    {
        var email = _faker.Generate();
        _mockRepository.Setup(r => r.GetByIdAsync(email.Id)).ReturnsAsync(email);

        var result = await _sut.DeleteAsync(email.Id);

        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteAsync(email), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ReturnsFalse()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((EmailRecord?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<EmailRecord>()), Times.Never);
    }

    // ── GetUnreadCountAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        var emails = new List<EmailRecord>
        {
            new() { Subject = "A", FromAddress = "a@x.com", ToAddresses = "b@x.com", MessageId = "1", IsRead = false, FolderId = "Inbox" },
            new() { Subject = "B", FromAddress = "a@x.com", ToAddresses = "b@x.com", MessageId = "2", IsRead = true,  FolderId = "Inbox" },
            new() { Subject = "C", FromAddress = "a@x.com", ToAddresses = "b@x.com", MessageId = "3", IsRead = false, FolderId = "Inbox" },
            new() { Subject = "D", FromAddress = "a@x.com", ToAddresses = "b@x.com", MessageId = "4", IsRead = false, FolderId = "Archive" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emails);

        var allUnread = await _sut.GetUnreadCountAsync();
        var inboxUnread = await _sut.GetUnreadCountAsync("Inbox");

        allUnread.Should().Be(3);
        inboxUnread.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenAllRead_ReturnsZero()
    {
        var emails = _faker.Generate(5);
        foreach (var e in emails) e.IsRead = true;
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emails);

        var result = await _sut.GetUnreadCountAsync();

        result.Should().Be(0);
    }
}
