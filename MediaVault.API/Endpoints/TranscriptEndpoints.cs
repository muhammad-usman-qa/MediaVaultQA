using MediaVault.API.Models;
using MediaVault.API.Services;

namespace MediaVault.API.Endpoints;

public static class TranscriptEndpoints
{
    public static IEndpointRouteBuilder MapTranscriptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transcripts").WithTags("Transcripts");

        group.MapGet("/", async (IChatTranscriptService service) =>
            Results.Ok(await service.GetAllAsync()))
            .WithName("GetAllTranscripts")
            .WithSummary("Get all chat transcripts");

        group.MapGet("/stats", async (IChatTranscriptService service) =>
            Results.Ok(await service.GetStatsAsync()))
            .WithName("GetTranscriptStats")
            .WithSummary("Get transcript statistics");

        group.MapGet("/{id:guid}", async (Guid id, IChatTranscriptService service) =>
        {
            var transcript = await service.GetByIdAsync(id);
            return transcript is null ? Results.NotFound() : Results.Ok(transcript);
        })
        .WithName("GetTranscriptById")
        .WithSummary("Get transcript by ID");

        group.MapPost("/", async (CreateTranscriptRequest request, IChatTranscriptService service) =>
        {
            try
            {
                var created = await service.CreateAsync(request);
                return Results.Created($"/api/transcripts/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateTranscript")
        .WithSummary("Create a new chat transcript session");

        group.MapPost("/{id:guid}/messages", async (Guid id, AddMessageRequest request, IChatTranscriptService service) =>
        {
            try
            {
                var message = await service.AddMessageAsync(id, request);
                return message is null ? Results.NotFound() : Results.Created($"/api/transcripts/{id}/messages/{message.Id}", message);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddTranscriptMessage")
        .WithSummary("Add a message to a transcript");

        group.MapPut("/{id:guid}/resolve", async (Guid id, ResolveTranscriptRequest request, IChatTranscriptService service) =>
        {
            var resolved = await service.ResolveAsync(id, request.Status);
            return resolved is null ? Results.NotFound() : Results.Ok(resolved);
        })
        .WithName("ResolveTranscript")
        .WithSummary("Resolve or escalate a transcript");

        return app;
    }
}

public record ResolveTranscriptRequest(ChatResolutionStatus Status);
