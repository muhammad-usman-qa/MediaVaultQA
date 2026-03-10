using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace MediaVault.IntegrationTests.WireMock;

/// <summary>
/// Configures WireMock stubs that simulate the external blob storage service.
/// Provides named setup methods so tests declare intent clearly.
/// </summary>
public static class StorageServiceWireMock
{
    /// <summary>Stubs POST /storage/register to return a generated blob URL.</summary>
    public static void SetupRegisterBlob(WireMockServer server, string? returnUrl = null)
    {
        var blobUrl = returnUrl ?? $"https://mock-storage.mediavault.com/audio/{Guid.NewGuid()}.mp3";

        server
            .Given(Request.Create()
                .WithPath("/storage/register")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { blobUrl }));
    }

    /// <summary>Stubs DELETE /storage/blobs to return 204 No Content.</summary>
    public static void SetupDeleteBlob(WireMockServer server, bool succeed = true)
    {
        server
            .Given(Request.Create()
                .WithPath("/storage/blobs")
                .UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(succeed ? 204 : 500));
    }

    /// <summary>Stubs GET /storage/blobs/exists to confirm blob presence.</summary>
    public static void SetupBlobExists(WireMockServer server, bool exists = true)
    {
        server
            .Given(Request.Create()
                .WithPath("/storage/blobs/exists")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(exists ? 200 : 404));
    }

    /// <summary>Stubs POST /storage/register to simulate a service outage.</summary>
    public static void SetupRegisterBlobFailure(WireMockServer server)
    {
        server
            .Given(Request.Create()
                .WithPath("/storage/register")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBodyAsJson(new { error = "Storage service unavailable" }));
    }
}
