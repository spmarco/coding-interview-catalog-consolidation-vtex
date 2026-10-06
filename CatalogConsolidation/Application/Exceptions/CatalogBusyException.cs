namespace CatalogConsolidation.Application.Exceptions;

/// <summary>Another import holds the catalog's write lock and it was not released in time. Safe to retry; mapped to HTTP 503.</summary>
public sealed class CatalogBusyException : Exception
{
    public CatalogBusyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
