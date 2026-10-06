namespace CatalogConsolidation.Domain.Exceptions;

/// <summary>
/// Base type for every exception raised when a domain invariant is violated (for example,
/// building a product without a name). The importer turns these into rejected rows; if one ever
/// escapes a request, <c>GlobalExceptionHandler</c> maps it to HTTP 400.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
