using CatalogConsolidation.Api.Endpoints;
using CatalogConsolidation.Api.ErrorHandling;
using CatalogConsolidation.Application.DependencyInjection;
using CatalogConsolidation.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCatalogApplication();
builder.Services.AddCatalogInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Force scope validation on regardless of the environment
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true; // Also validates that all services can be constructed
});

var app = builder.Build();

app.EnsureCatalogSchema();

app.UseExceptionHandler();

// Errors the framework answers by itself, with no exception and no body (415, 404, 405...), also use ProblemDetails.
app.UseStatusCodePages();

app.MapCatalogImportEndpoints();

app.Run();

// Exposed so CatalogConsolidation.Tests can bootstrap the app via WebApplicationFactory<Program>.
public partial class Program;
