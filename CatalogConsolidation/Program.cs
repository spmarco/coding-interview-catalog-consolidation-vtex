using CatalogConsolidation.Api.Endpoints;
using CatalogConsolidation.Api.ErrorHandling;
using CatalogConsolidation.Application.DependencyInjection;
using CatalogConsolidation.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCatalogApplication(builder.Configuration);
builder.Services.AddCatalogInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.EnsureCatalogSchema();

app.UseExceptionHandler();

// Errors the framework answers by itself, with no exception and no body (415, 404, 405...), also use ProblemDetails.
app.UseStatusCodePages();

app.MapCatalogImportEndpoints();

app.Run();

// Exposed so CatalogConsolidation.Tests can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program;
