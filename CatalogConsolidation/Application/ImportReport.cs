namespace CatalogConsolidation.Application;

public sealed record ApproximateMatchReport(string RowId, string EnteredName, string MatchedProductName, double Score);

public sealed record DuplicateLinkIgnoredReport(string SellerName, string ProductName, string DiscardedSellerProductId, string ExistingSellerProductId);

public sealed record RejectedRowReport(string? Id, string? SellerName, string? Name, string Reason);

public sealed record ImportWarningReport(string? RowId, string Message);

public sealed class ImportReport
{
    public int ProductsCreated { get; set; }

    public int LinksCreated { get; set; }

    public int ExactMatches { get; set; }

    public List<ApproximateMatchReport> ApproximateMatches { get; } = [];

    public List<DuplicateLinkIgnoredReport> DuplicateLinksIgnored { get; } = [];

    public List<ImportWarningReport> Warnings { get; } = [];

    public List<RejectedRowReport> RejectedRows { get; } = [];
}
