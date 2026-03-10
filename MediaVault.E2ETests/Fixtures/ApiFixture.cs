using MediaVault.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace MediaVault.E2ETests.Fixtures;

/// <summary>
/// Shared test fixture for Playwright E2E tests.
/// Spins up the real API in-process via WebApplicationFactory (no browser required).
/// Provides a Playwright APIRequestContext for black-box HTTP testing.
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private IPlaywright? _playwright;

    public IAPIRequestContext ApiContext { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Override the base URL from environment (for real deployment E2E runs)
        var envUrl = Environment.GetEnvironmentVariable("API_BASE_URL");

        if (envUrl is not null)
        {
            BaseUrl = envUrl.TrimEnd('/');
        }
        else
        {
            // Start the API in-process with an isolated in-memory DB
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Test");
                    builder.ConfigureServices(services =>
                    {
                        var dbDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<MediaVaultDbContext>));
                        if (dbDescriptor is not null)
                            services.Remove(dbDescriptor);

                        var dbName = $"E2ETestDb_{Guid.NewGuid()}";
                        services.AddDbContext<MediaVaultDbContext>(opts =>
                            opts.UseInMemoryDatabase(dbName));
                    });
                });

            var httpClient = _factory.CreateClient();
            BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
        }

        _playwright = await Playwright.CreateAsync();
        ApiContext = await _playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = BaseUrl,
            // Treat 4xx/5xx as valid responses (not exceptions) so we can assert status codes
            IgnoreHTTPSErrors = true
        });
    }

    public async Task DisposeAsync()
    {
        if (ApiContext is not null)
            await ApiContext.DisposeAsync();

        _playwright?.Dispose();
        _factory?.Dispose();
    }
}
