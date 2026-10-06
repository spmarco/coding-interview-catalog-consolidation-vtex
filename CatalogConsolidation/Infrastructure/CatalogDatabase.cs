namespace CatalogConsolidation.Infrastructure;

/// <summary>The resolved (absolute) connection string for the catalog database, registered as a singleton.</summary>
public sealed class CatalogDatabase
{
    public CatalogDatabase(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
