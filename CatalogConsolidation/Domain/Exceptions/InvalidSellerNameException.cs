namespace CatalogConsolidation.Domain.Exceptions;

public sealed class InvalidSellerNameException : DomainException
{
    public InvalidSellerNameException(string message) : base(message)
    {
    }
}
