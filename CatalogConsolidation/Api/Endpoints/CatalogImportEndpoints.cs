using CatalogConsolidation.Application;
using CatalogConsolidation.Application.Exceptions;

namespace CatalogConsolidation.Api.Endpoints;

public static class CatalogImportEndpoints
{
    public static IEndpointRouteBuilder MapCatalogImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/catalog/imports", async (IFormFile? file, ICatalogImportService importService, CancellationToken cancellationToken) =>
        {
            if (file is null)
            {
                throw new InvalidImportFileException("The 'file' part is required.");
            }

            await using var content = file.OpenReadStream();
            var report = await importService.ImportAsync(content, cancellationToken);

            return Results.Ok(report);
        })
        .DisableAntiforgery()
        .WithName("ImportCatalog")
        .Produces<ImportReport>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
