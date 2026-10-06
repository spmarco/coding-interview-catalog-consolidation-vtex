using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Abstractions;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// Owns the single connection and transaction an import runs in. Opens with BEGIN IMMEDIATE
/// (README decision #9) so SQLite takes the write lock up front and concurrent imports serialize
/// instead of racing to create the same product. If the lock cannot be obtained in time, that is
/// reported as <see cref="CatalogBusyException"/> rather than a raw SQLite error.
/// </summary>
public sealed class SqliteCatalogUnitOfWork : ICatalogUnitOfWork
{
    private const int SqliteBusy = 5;

    private readonly SqliteConnection _connection;
    private bool _completed;

    public SqliteCatalogUnitOfWork(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);

        // The constructor never completes if this throws, so Dispose will not run:
        // release the connection here instead.
        try
        {
            _connection.Open();
            Execute("BEGIN IMMEDIATE;");

            Catalog = new SqliteCatalogSnapshot(_connection);
            Products = new SqliteProductRepository(_connection);
            SellerLinks = new SqliteSellerLinkRepository(_connection);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteBusy)
        {
            _connection.Dispose();
            throw new CatalogBusyException("The catalog is locked by another import. Please retry.", ex);
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    public ICatalogSnapshot Catalog { get; }

    public IProductRepository Products { get; }

    public ISellerLinkRepository SellerLinks { get; }

    public void Commit()
    {
        Execute("COMMIT;");
        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            try
            {
                Execute("ROLLBACK;");
            }
            catch (SqliteException)
            {
                // Connection may already be in a failed state; nothing more to roll back.
            }
        }

        _connection.Dispose();
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
