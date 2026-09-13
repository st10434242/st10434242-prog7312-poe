using SmartX.Core;

namespace SmartX.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder sensors = app.MapGroup("/api/sensors/{nodeId}/attachments").WithTags("Attachments");

        // Minimal APIs demand an antiforgery token for IFormFile  which a device client has no way to send
        sensors.MapPost("/", Upload).DisableAntiforgery();
        sensors.MapGet("/", ListForNode);

        RouteGroupBuilder attachments = app.MapGroup("/api/attachments").WithTags("Attachments");

        attachments.MapGet("/{id:guid}", Download);
        attachments.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static async Task<IResult> Upload(
        string nodeId,
        IFormFile file,
        SensorRegistry registry,
        AttachmentStore store,
        CancellationToken cancellationToken)
    {
        if (!registry.TryGet(nodeId, out _))
        {
            return Results.NotFound();
        }

        Dictionary<string, string[]> errors = [];

        if (file.Length == 0)
        {
            errors[nameof(file)] = ["The file is empty."];
        }
        else if (file.Length > AttachmentStore.MaxSizeBytes)
        {
            errors[nameof(file)] = [$"Files must be {AttachmentStore.MaxSizeBytes / 1024 / 1024} MB or smaller."];
        }

        if (!AttachmentStore.IsAllowedExtension(file.FileName))
        {
            errors[nameof(file.FileName)] = [$"Allowed types are {AttachmentStore.AllowedExtensionList}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        await using Stream content = file.OpenReadStream();

        Core.Contracts.AttachmentDto saved = await store.SaveAsync(
            nodeId,
            Path.GetFileName(file.FileName),
            file.ContentType,
            content,
            cancellationToken);

        return Results.Created($"/api/attachments/{saved.Id}", saved);
    }

    private static IResult ListForNode(string nodeId, SensorRegistry registry, AttachmentStore store) =>
        registry.TryGet(nodeId, out _) ? Results.Ok(store.ForNode(nodeId)) : Results.NotFound();

    private static async Task<IResult> Download(
        Guid id,
        AttachmentStore store,
        CancellationToken cancellationToken)
    {
        if (!store.TryGet(id, out Core.Contracts.AttachmentDto? metadata))
        {
            return Results.NotFound();
        }

        Stream decrypted = await store.OpenDecryptedAsync(id, cancellationToken);

        return Results.File(decrypted, metadata!.ContentType, metadata.FileName);
    }

    private static IResult Delete(Guid id, AttachmentStore store) =>
        store.Delete(id) ? Results.NoContent() : Results.NotFound();
}
