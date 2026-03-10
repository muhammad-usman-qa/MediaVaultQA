using FluentAssertions;
using MediaVault.E2ETests.Fixtures;
using Microsoft.Playwright;

namespace MediaVault.E2ETests.Tests;

/// <summary>
/// E2E tests for the Email API endpoints.
/// Validates ingestion, read-state management, folder filtering, and lifecycle.
/// </summary>
[Collection("E2E")]
public class EmailE2ETests : IClassFixture<ApiFixture>
{
    private readonly IAPIRequestContext _api;

    public EmailE2ETests(ApiFixture fixture)
    {
        _api = fixture.ApiContext;
    }

    private static Dictionary<string, object> BuildEmailPayload(
        string subject = "E2E Test Email",
        string from = "sender@mediavault.com",
        string to = "recipient@mediavault.com",
        string folder = "Inbox") =>
        new()
        {
            ["messageId"] = $"<{Guid.NewGuid()}@mediavault.com>",
            ["subject"] = subject,
            ["fromAddress"] = from,
            ["toAddresses"] = to,
            ["body"] = "This is an E2E test email body.",
            ["receivedAt"] = DateTime.UtcNow.AddMinutes(-5).ToString("O"),
            ["hasAttachments"] = false,
            ["attachmentCount"] = 0,
            ["folderId"] = folder
        };

    // ── Full lifecycle ───────────────────────────────────────────────────────

    [Fact]
    public async Task EmailLifecycle_IngestReadDelete_Succeeds()
    {
        // INGEST
        var createResponse = await _api.PostAsync("/api/emails",
            new() { DataObject = BuildEmailPayload("Lifecycle Test") });

        createResponse.Status.Should().Be(201);
        var created = await createResponse.JsonAsync();
        var id = created!.Value.GetProperty("id").GetString();
        created.Value.GetProperty("isRead").GetBoolean().Should().BeFalse();

        // READ — verify it's retrievable
        var getResponse = await _api.GetAsync($"/api/emails/{id}");
        getResponse.Status.Should().Be(200);
        var emailJson = await getResponse.JsonAsync();
        emailJson!.Value.GetProperty("subject").GetString().Should().Be("Lifecycle Test");

        // MARK AS READ
        var markReadResponse = await _api.PutAsync($"/api/emails/{id}/read",
            new() { DataObject = new Dictionary<string, object>() });
        markReadResponse.Status.Should().Be(200);
        var updated = await markReadResponse.JsonAsync();
        updated!.Value.GetProperty("isRead").GetBoolean().Should().BeTrue();

        // DELETE
        var deleteResponse = await _api.DeleteAsync($"/api/emails/{id}");
        deleteResponse.Status.Should().Be(204);

        // CONFIRM GONE
        var getAfterDelete = await _api.GetAsync($"/api/emails/{id}");
        getAfterDelete.Status.Should().Be(404);
    }

    // ── Unread count ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_UnreadCount_DecreasesAfterMarkingRead()
    {
        // Create an unread email
        var createResp = await _api.PostAsync("/api/emails",
            new() { DataObject = BuildEmailPayload($"Unread_{Guid.NewGuid():N}") });
        var created = await createResp.JsonAsync();
        var id = created!.Value.GetProperty("id").GetString();

        // Get initial unread count
        var countResp1 = await _api.GetAsync("/api/emails/unread-count");
        var count1 = (await countResp1.JsonAsync())!.Value.GetProperty("count").GetInt32();

        // Mark as read
        await _api.PutAsync($"/api/emails/{id}/read",
            new() { DataObject = new Dictionary<string, object>() });

        // Get updated unread count
        var countResp2 = await _api.GetAsync("/api/emails/unread-count");
        var count2 = (await countResp2.JsonAsync())!.Value.GetProperty("count").GetInt32();

        count2.Should().BeLessThan(count1, "marking as read should decrease unread count");
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Email_EmptySubject_Returns400()
    {
        var payload = BuildEmailPayload(subject: "");

        var response = await _api.PostAsync("/api/emails", new() { DataObject = payload });

        response.Status.Should().Be(400);
    }

    [Fact]
    public async Task GET_Email_NonExistentId_Returns404()
    {
        var response = await _api.GetAsync($"/api/emails/{Guid.NewGuid()}");

        response.Status.Should().Be(404);
    }

    // ── Folder filtering ─────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Emails_FolderFilter_OnlyReturnsMatchingFolder()
    {
        var uniqueFolder = $"TestFolder_{Guid.NewGuid():N}";

        await _api.PostAsync("/api/emails",
            new() { DataObject = BuildEmailPayload(folder: uniqueFolder) });
        await _api.PostAsync("/api/emails",
            new() { DataObject = BuildEmailPayload(folder: "Inbox") });

        var response = await _api.GetAsync($"/api/emails?folder={uniqueFolder}");

        response.Status.Should().Be(200);
        var emails = await response.JsonAsync();
        var folders = emails!.Value.EnumerateArray()
            .Select(e => e.GetProperty("folderId").GetString())
            .ToList();
        folders.Should().AllBe(uniqueFolder);
    }
}
