using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Exceptions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;

namespace CatalogConsolidation.Application;

/// <summary>
/// Runs README's "How it works" over a batch of entries, against the given catalog and seller
/// links: build the domain objects (which validate themselves), match, insert when nothing
/// matches, link the seller, and report. A row the domain rejects is reported and skipped; it
/// never stops the rest of the import.
/// </summary>
public sealed class CatalogImporter
{
    private readonly ICatalogSnapshot _catalog;
    private readonly IProductRepository _products;
    private readonly ProductMatcher _matcher;
    private readonly SellerLinker _linker;

    public CatalogImporter(
        ICatalogSnapshot catalog,
        IProductRepository products,
        ISellerLinkRepository sellerLinks,
        MatchingOptions options)
    {
        _catalog = catalog;
        _products = products;
        _matcher = new ProductMatcher(catalog, options.SimilarityThreshold);
        _linker = new SellerLinker(sellerLinks);
    }

    public ImportReport Import(IReadOnlyList<ProductEntryDto?> entries)
    {
        var report = new ImportReport();

        foreach (var entry in entries)
        {
            if (entry is null)
            {
                report.RejectedRows.Add(new RejectedRowReport(null, null, null, "Row is null."));
                continue;
            }

            try
            {
                ImportEntry(entry, report);
            }
            catch (DomainException ex)
            {
                report.RejectedRows.Add(new RejectedRowReport(entry.Id, entry.SellerName, entry.Name, ex.Message));
            }
        }

        return report;
    }

    private void ImportEntry(ProductEntryDto entry, ImportReport report)
    {
        // Everything that can reject the row is built before anything is stored.
        var seller = SellerName.Create(entry.SellerName);
        var sellerProductId = SellerProductId.Create(entry.Id);
        var candidate = Product.Register(entry.Name, entry.Brand, entry.Category);

        if (!sellerProductId.IsGuidShaped)
        {
            report.Warnings.Add(new ImportWarningReport(entry.Id, "Id is not in the expected GUID format; processed anyway."));
        }

        var product = ResolveProduct(candidate, entry, report);

        var link = SellerProduct.Link(seller, product, sellerProductId, entry.Name);
        var result = _linker.Link(link);

        switch (result.Outcome)
        {
            case LinkOutcome.Created:
                report.LinksCreated++;
                break;
            case LinkOutcome.DuplicateDiscarded:
                report.DuplicateLinksIgnored.Add(new DuplicateLinkIgnoredReport(
                    seller.Value, product.Name, sellerProductId.Value, result.Existing!.SellerProductId.Value));
                break;
            case LinkOutcome.AlreadyLinked:
                break;
        }
    }

    private Product ResolveProduct(Product candidate, ProductEntryDto entry, ImportReport report)
    {
        var match = _matcher.Match(candidate.Key);
        if (match is null)
        {
            // Store first, then expose it to matching: later rows of this file must see it (README decision #7).
            _products.Add(candidate);
            _catalog.Track(candidate);
            report.ProductsCreated++;
            return candidate;
        }

        var product = match.Product;

        if (match.Kind == MatchKind.Exact)
        {
            report.ExactMatches++;
        }
        else
        {
            report.ApproximateMatches.Add(new ApproximateMatchReport(entry.Id!, entry.Name!, product.Name, match.Score!.Value));
        }

        if (product.HasDivergentCategory(entry.Category))
        {
            report.Warnings.Add(new ImportWarningReport(
                entry.Id,
                $"Category mismatch for '{product.Name}': catalog has '{product.Category}', entry has '{entry.Category}'."));
        }

        return product;
    }
}
