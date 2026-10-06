using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;
using NSubstitute;

namespace CatalogConsolidation.Tests.Domain;

public class SellerLinkerTests
{
    private static readonly Product Hose = Product.Restore(1, "Garden Hose 50 Feet", "Flexzilla", null);

    private readonly ISellerLinkRepository _links = Substitute.For<ISellerLinkRepository>(); // Find returns null: nothing stored yet
    private readonly SellerLinker _linker;

    public SellerLinkerTests()
    {
        _linker = new SellerLinker(_links);
    }

    private static SellerProduct Offer(string seller, string sellerProductId)
        => SellerProduct.Link(SellerName.Create(seller), Hose, SellerProductId.Create(sellerProductId), null);

    private static SellerProduct StoredLink(string seller, string sellerProductId)
        => SellerProduct.Restore(1, SellerName.Create(seller), Hose.Id, SellerProductId.Create(sellerProductId), null);

    [Fact]
    public void A_first_offer_is_stored()
    {
        var offer = Offer("SuperMart", "id-1");

        var result = _linker.Link(offer);

        Assert.Equal(LinkOutcome.Created, result.Outcome);
        Assert.Null(result.Existing);
        _links.Received(1).Add(offer);
    }

    [Fact]
    public void Repeating_the_same_offer_stores_nothing_new()
    {
        _links.Find(SellerName.Create("SuperMart"), Hose.Id).Returns(StoredLink("SuperMart", "id-1"));

        var result = _linker.Link(Offer("SuperMart", "id-1"));

        Assert.Equal(LinkOutcome.AlreadyLinked, result.Outcome);
        _links.DidNotReceive().Add(Arg.Any<SellerProduct>());
    }

    [Fact]
    public void A_different_seller_product_id_is_discarded_and_the_stored_one_is_returned()
    {
        var stored = StoredLink("SuperMart", "id-1");
        _links.Find(SellerName.Create("SuperMart"), Hose.Id).Returns(stored);

        var result = _linker.Link(Offer("SuperMart", "id-2"));

        Assert.Equal(LinkOutcome.DuplicateDiscarded, result.Outcome);
        Assert.Same(stored, result.Existing);
        _links.DidNotReceive().Add(Arg.Any<SellerProduct>());
    }

    [Fact]
    public void Another_seller_offering_the_same_product_gets_its_own_link()
    {
        _links.Find(SellerName.Create("SuperMart"), Hose.Id).Returns(StoredLink("SuperMart", "id-1"));
        var offer = Offer("TechWorld", "id-1");

        var result = _linker.Link(offer);

        Assert.Equal(LinkOutcome.Created, result.Outcome);
        _links.Received(1).Add(offer);
    }
}
