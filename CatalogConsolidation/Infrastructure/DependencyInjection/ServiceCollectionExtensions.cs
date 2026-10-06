using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var rawConnectionString = configuration["CatalogDatabase:ConnectionString"] ?? "Data Source=../catalog.db";
        var connectionString = CatalogConnectionStringResolver.Resolve(rawConnectionString, contentRootPath);

        services.AddSingleton(new CatalogDatabase(connectionString));
        services.AddSingleton<ICatalogUnitOfWorkFactory, SqliteCatalogUnitOfWorkFactory>();

        return services;
    }

    /// <summary>Applies README's "Database Changes" to the catalog database; fails fast if it cannot be opened.</summary>
    public static IHost EnsureCatalogSchema(this IHost host)
    {
        var database = host.Services.GetRequiredService<CatalogDatabase>();
        SqliteSchemaInitializer.EnsureSchema(database.ConnectionString);

        return host;
    }
}
