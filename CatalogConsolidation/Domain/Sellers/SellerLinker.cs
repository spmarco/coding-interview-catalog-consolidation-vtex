using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Domain.Sellers;

public sealed record LinkAttemptResult(LinkOutcome Outcome, SellerProduct? Existing);

/// <summary>
/// Decides what happens when a seller's offer arrives: store the link when the seller is not yet
/// linked to that product, otherwise let the stored link reconcile the offer (README decision #7).
/// </summary>
public sealed class SellerLinker
{
    private readonly ISellerLinkRepository _links;

    public SellerLinker(ISellerLinkRepository links)
    {
        _links = links;
    }

    public LinkAttemptResult Link(SellerProduct incoming)
    {
        var existing = _links.Find(incoming.Seller, incoming.ProductId);
        if (existing is null)
        {
            _links.Add(incoming);
            return new LinkAttemptResult(LinkOutcome.Created, null);
        }

        return new LinkAttemptResult(existing.Reconcile(incoming), existing);
    }
}
