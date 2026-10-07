using CatalogConsolidation.Application.Imports;

namespace CatalogConsolidation.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        // CatalogImportService reads its own settings from the IConfiguration the host registers.
        services.AddScoped<ICatalogImportService, CatalogImportService>();

        return services;
    }
}
