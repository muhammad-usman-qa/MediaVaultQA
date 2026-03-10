using MediaVault.API.Services;

namespace MediaVault.API.Endpoints;

public static class AudioEndpoints
{
    public static IEndpointRouteBuilder MapAudioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audio").WithTags("Audio");

        group.MapGet("/", async (IAudioFileService service) =>
            Results.Ok(await service.GetAllAsync()))
            .WithName("GetAllAudio")
            .WithSummary("Get all audio files");

        group.MapGet("/search", async (string q, IAudioFileService service) =>
            Results.Ok(await service.SearchAsync(q)))
            .WithName("SearchAudio")
            .WithSummary("Search audio files by title, artist, or tags");

        group.MapGet("/{id:guid}", async (Guid id, IAudioFileService service) =>
        {
            var file = await service.GetByIdAsync(id);
            return file is null ? Results.NotFound() : Results.Ok(file);
        })
        .WithName("GetAudioById")
        .WithSummary("Get audio file by ID");

        group.MapPost("/", async (CreateAudioFileRequest request, IAudioFileService service) =>
        {
            try
            {
                var created = await service.CreateAsync(request);
                return Results.Created($"/api/audio/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateAudio")
        .WithSummary("Upload a new audio file");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAudioFileRequest request, IAudioFileService service) =>
        {
            var updated = await service.UpdateAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("UpdateAudio")
        .WithSummary("Update audio file metadata");

        group.MapDelete("/{id:guid}", async (Guid id, IAudioFileService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteAudio")
        .WithSummary("Delete an audio file");

        return app;
    }
}
