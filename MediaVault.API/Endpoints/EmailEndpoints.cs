using MediaVault.API.Services;

namespace MediaVault.API.Endpoints;

public static class EmailEndpoints
{
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/emails").WithTags("Emails");

        group.MapGet("/", async (string? folder, IEmailService service) =>
            Results.Ok(await service.GetAllAsync(folder)))
            .WithName("GetAllEmails")
            .WithSummary("Get all emails, optionally filtered by folder");

        group.MapGet("/unread-count", async (string? folder, IEmailService service) =>
            Results.Ok(new { count = await service.GetUnreadCountAsync(folder) }))
            .WithName("GetUnreadCount")
            .WithSummary("Get count of unread emails");

        group.MapGet("/{id:guid}", async (Guid id, IEmailService service) =>
        {
            var email = await service.GetByIdAsync(id);
            return email is null ? Results.NotFound() : Results.Ok(email);
        })
        .WithName("GetEmailById")
        .WithSummary("Get email by ID");

        group.MapPost("/", async (IngestEmailRequest request, IEmailService service) =>
        {
            try
            {
                var created = await service.IngestAsync(request);
                return Results.Created($"/api/emails/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("IngestEmail")
        .WithSummary("Ingest a new email record");

        group.MapPut("/{id:guid}/read", async (Guid id, IEmailService service) =>
        {
            var updated = await service.MarkAsReadAsync(id);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("MarkEmailRead")
        .WithSummary("Mark an email as read");

        group.MapDelete("/{id:guid}", async (Guid id, IEmailService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteEmail")
        .WithSummary("Delete an email record");

        return app;
    }
}
