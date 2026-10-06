using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;

namespace CatalogConsolidation.Tests.Domain;

public class SellerProductTests
{
    private static readonly Product Camera = Product.Restore(10, "Camera Canon EOS R6", "Canon", "Photography");

    private static SellerProduct Offer(string seller, Product product, string sellerProductId)
        => SellerProduct.Link(SellerName.Create(seller), product, SellerProductId.Create(sellerProductId), "as sent");

    [Fact]
    public void A_seller_can_only_be_linked_to_a_product_that_is_already_in_the_catalog()
    {
        var notStoredYet = Product.Register("New", null, null);

        Assert.Throws<InvalidOperationException>(() => Offer("S", notStoredYet, "id-1"));
    }

    [Fact]
    public void A_new_link_points_at_the_product_and_keeps_the_sellers_own_name_for_it()
    {
        var link = Offer("S", Camera, "id-1");

        Assert.Equal(10, link.ProductId);
        Assert.Equal("as sent", link.SellerProductName);
        Assert.False(link.IsPersisted);
    }

    [Fact]
    public void The_identity_can_only_be_assigned_once()
    {
        var link = Offer("S", Camera, "id-1");
        link.AssignId(1);

        Assert.True(link.IsPersisted);
        Assert.Throws<InvalidOperationException>(() => link.AssignId(2));
    }

    [Fact]
    public void The_same_seller_product_id_just_repeats_the_link()
    {
        var stored = Offer("S", Camera, "id-1");

        Assert.Equal(LinkOutcome.AlreadyLinked, stored.Reconcile(Offer("S", Camera, "id-1")));
    }

    [Fact]
    public void A_different_seller_product_id_for_the_same_seller_and_product_is_discarded()
    {
        var stored = Offer("S", Camera, "id-1");

        Assert.Equal(LinkOutcome.DuplicateDiscarded, stored.Reconcile(Offer("S", Camera, "id-2")));
    }

    [Fact]
    public void Only_offers_of_the_same_seller_for_the_same_product_can_be_reconciled()
    {
        var stored = Offer("S", Camera, "id-1");
        var otherSeller = Offer("T", Camera, "id-1");
        var otherProduct = Offer("S", Product.Restore(11, "Other", "Canon", null), "id-1");

        Assert.Throws<InvalidOperationException>(() => stored.Reconcile(otherSeller));
        Assert.Throws<InvalidOperationException>(() => stored.Reconcile(otherProduct));
    }
}
