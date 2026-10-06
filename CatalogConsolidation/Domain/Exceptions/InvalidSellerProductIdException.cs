namespace CatalogConsolidation.Domain.Exceptions;

public sealed class InvalidSellerProductIdException : DomainException
{
    public InvalidSellerProductIdException(string message) : base(message)
    {
    }
}
