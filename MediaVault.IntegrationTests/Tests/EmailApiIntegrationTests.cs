using System.Net;
using System.Net.Http.Json;
using Bogus;
using FluentAssertions;
using MediaVault.API.Models;
using MediaVault.API.Services;
using MediaVault.IntegrationTests.Fixtures;

namespace MediaVault.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Email API endpoints.
/// Verifies email ingestion, folder filtering, read-state tracking, and unread counts.
/// </summary>
public class EmailApiIntegrationTests : IClassFixture<MediaVaultWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Faker _faker = new();

    public EmailApiIntegrationTests(MediaVaultWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private IngestEmailRequest BuildValidRequest(string folder = "Inbox") =>
        new(
            MessageId: $"<{_faker.Random.Guid()}@mediavault.com>",
            Subject: _faker.Lorem.Sentence(4),
            FromAddress: _faker.Internet.Email(),
            ToAddresses: _faker.Internet.Email(),
            Body: _faker.Lorem.Paragraph(),
            ReceivedAt: DateTime.UtcNow.AddMinutes(-_faker.Random.Int(1, 60)),
            HasAttachments: false,
            AttachmentCount: 0,
            FolderId: folder);

    // ── POST /api/emails ─────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Email_ValidRequest_Returns201()
    {
        var request = BuildValidRequest();

        var response = await _client.PostAsJsonAsync("/api/emails", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<EmailRecord>();
        created.Should().NotBeNull();
        created!.Subject.Should().Be(request.Subject);
        created.IsRead.Should().BeFalse("all ingested emails start as unread");
        created.FolderId.Should().Be("Inbox");
    }

    [Theory]
    [InlineData("", "from@x.com", "to@x.com")]
    [InlineData("Subject", "", "to@x.com")]
    [InlineData("Subject", "from@x.com", "")]
    public async Task POST_Email_MissingRequiredFields_Returns400(
        string subject, string from, string to)
    {
        var request = new IngestEmailRequest("<id>", subject, from, to, "body", DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync("/api/emails", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/emails ──────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Emails_NoFilter_ReturnsAllEmails()
    {
        await _client.PostAsJsonAsync("/api/emails", BuildValidRequest("Inbox"));
        await _client.PostAsJsonAsync("/api/emails", BuildValidRequest("Archive"));

        var response = await _client.GetAsync("/api/emails");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var emails = await response.Content.ReadFromJsonAsync<List<EmailRecord>>();
        emails!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GET_Emails_WithFolderFilter_ReturnsOnlyThatFolder()
    {
        var uniqueSubject = $"UniqueFolder_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/emails",
            BuildValidRequest("Inbox") with { Subject = uniqueSubject });
        await _client.PostAsJsonAsync("/api/emails", BuildValidRequest("Archive"));

        var response = await _client.GetAsync("/api/emails?folder=Inbox");
        var emails = await response.Content.ReadFromJsonAsync<List<EmailRecord>>();

        emails.Should().Contain(e => e.FolderId == "Inbox");
        emails.Should().NotContain(e => e.FolderId == "Archive");
    }

    // ── GET /api/emails/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task GET_EmailById_WhenExists_Returns200()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/emails", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<EmailRecord>();

        var response = await _client.GetAsync($"/api/emails/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_EmailById_WhenNotExists_Returns404()
    {
        var response = await _client.GetAsync($"/api/emails/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/emails/{id}/read ────────────────────────────────────────────

    [Fact]
    public async Task PUT_MarkEmailRead_WhenExists_SetsIsReadTrue()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/emails", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<EmailRecord>();
        created!.IsRead.Should().BeFalse("precondition check");

        var response = await _client.PutAsync($"/api/emails/{created.Id}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<EmailRecord>();
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task PUT_MarkEmailRead_WhenNotExists_Returns404()
    {
        var response = await _client.PutAsync($"/api/emails/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/emails/unread-count ─────────────────────────────────────────

    [Fact]
    public async Task GET_UnreadCount_ReflectsActualUnreadState()
    {
        // Seed two unread emails
        var r1 = await _client.PostAsJsonAsync("/api/emails", BuildValidRequest());
        var r2 = await _client.PostAsJsonAsync("/api/emails", BuildValidRequest());
        var e1 = await r1.Content.ReadFromJsonAsync<EmailRecord>();
        var e2 = await r2.Content.ReadFromJsonAsync<EmailRecord>();

        // Mark one as read
        await _client.PutAsync($"/api/emails/{e1!.Id}/read", null);

        var countResponse = await _client.GetAsync("/api/emails/unread-count");
        var body = await countResponse.Content.ReadFromJsonAsync<Dictionary<string, int>>();

        body!["count"].Should().BeGreaterThanOrEqualTo(1,
            "at least one email (e2) should remain unread");
    }

    // ── DELETE /api/emails/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task DELETE_Email_WhenExists_Returns204AndIsGone()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/emails", BuildValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<EmailRecord>();

        var deleteResponse = await _client.DeleteAsync($"/api/emails/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/emails/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
