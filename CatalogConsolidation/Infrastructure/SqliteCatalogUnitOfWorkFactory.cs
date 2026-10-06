using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Infrastructure;

public sealed class SqliteCatalogUnitOfWorkFactory : ICatalogUnitOfWorkFactory
{
    private readonly CatalogDatabase _database;

    public SqliteCatalogUnitOfWorkFactory(CatalogDatabase database)
    {
        _database = database;
    }

    public ICatalogUnitOfWork Begin() => new SqliteCatalogUnitOfWork(_database.ConnectionString);
}
