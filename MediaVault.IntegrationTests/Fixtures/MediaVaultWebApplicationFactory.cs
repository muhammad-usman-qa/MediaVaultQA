using MediaVault.API.Data;
using MediaVault.API.Services;
using MediaVault.IntegrationTests.WireMock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;
using WireMock.Settings;

namespace MediaVault.IntegrationTests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that:
/// - Replaces EF Core with an in-memory database for test isolation
/// - Starts a WireMock server to mock the external blob storage service
/// - Wires up the HttpStorageService to point at WireMock instead of the real endpoint
/// </summary>
public class MediaVaultWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public WireMockServer StorageMock { get; private set; } = null!;

    public Task InitializeAsync()
    {
        StorageMock = WireMockServer.Start(new WireMockServerSettings { Port = 0 });

        // Default always-on stubs — tests don't need to configure WireMock individually.
        // Tests that need specific behavior can add higher-priority mappings via Given().AtPriority(1).
        StorageServiceWireMock.SetupRegisterBlob(StorageMock);
        StorageServiceWireMock.SetupDeleteBlob(StorageMock);
        StorageServiceWireMock.SetupBlobExists(StorageMock);

        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // ── Replace DB with isolated in-memory instance per test run ──
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MediaVaultDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            var dbName = $"IntegrationTestDb_{Guid.NewGuid()}";
            services.AddDbContext<MediaVaultDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            // ── Replace storage HttpClient to point at WireMock ──────────
            var storageDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IStorageService));
            if (storageDescriptor is not null)
                services.Remove(storageDescriptor);

            var wireMockUrl = StorageMock.Urls[0];
            services.AddHttpClient<IStorageService, HttpStorageService>(client =>
            {
                client.BaseAddress = new Uri(wireMockUrl);
            });
        });

        builder.UseEnvironment("Test");
    }

    public new Task DisposeAsync()
    {
        StorageMock?.Stop();
        StorageMock?.Dispose();
        return Task.CompletedTask;
    }
}
