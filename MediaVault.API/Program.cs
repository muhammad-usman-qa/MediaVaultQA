using MediaVault.API.Data;
using MediaVault.API.Endpoints;
using MediaVault.API.Repositories;
using MediaVault.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<MediaVaultDbContext>(options =>
    options.UseInMemoryDatabase("MediaVaultDb"));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Storage Service (HTTP client pointing at external blob storage)
builder.Services.AddHttpClient<IStorageService, HttpStorageService>(client =>
{
    var baseUrl = builder.Configuration["StorageService:BaseUrl"] ?? "https://storage.mediavault.com";
    client.BaseAddress = new Uri(baseUrl);
});

// Domain Services
builder.Services.AddScoped<IAudioFileService, AudioFileService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IChatTranscriptService, ChatTranscriptService>();

// OpenAPI / Swagger
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MediaVault API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map all domain endpoints
app.MapAudioEndpoints();
app.MapEmailEndpoints();
app.MapTranscriptEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in test projects
public partial class Program { }
