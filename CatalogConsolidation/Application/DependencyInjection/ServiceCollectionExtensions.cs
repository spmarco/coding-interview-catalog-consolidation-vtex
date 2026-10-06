namespace CatalogConsolidation.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MatchingOptions>(configuration.GetSection(MatchingOptions.SectionName));
        services.AddScoped<ICatalogImportService, CatalogImportService>();

        return services;
    }
}
