namespace CatalogConsolidation.Application.Exceptions;

/// <summary>The upload as a whole cannot be imported (missing, or not a JSON array of product entries). Mapped to HTTP 400.</summary>
public sealed class InvalidImportFileException : Exception
{
    public InvalidImportFileException(string message) : base(message)
    {
    }
}
