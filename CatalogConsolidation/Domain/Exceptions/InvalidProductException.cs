namespace CatalogConsolidation.Domain.Exceptions;

public sealed class InvalidProductException : DomainException
{
    public InvalidProductException(string message) : base(message)
    {
    }
}
