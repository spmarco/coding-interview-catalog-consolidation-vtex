namespace CatalogConsolidation.Application;

/// <summary>One row of the uploaded file, in the same shape as ProductEntry.json.</summary>
public sealed record ProductEntryDto(string? Id, string? SellerName, string? Name, string? Brand, string? Category);
