using CatalogConsolidation.Domain.Exceptions;
using CatalogConsolidation.Domain.Sellers;

namespace CatalogConsolidation.Tests.Domain;

public class SellerValueObjectsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_seller_name_is_required(string? raw)
    {
        Assert.Throws<InvalidSellerNameException>(() => SellerName.Create(raw));
    }

    [Fact]
    public void Seller_names_are_trimmed_and_compared_exactly()
    {
        Assert.Equal(SellerName.Create("GardenStore"), SellerName.Create("  GardenStore "));
        Assert.NotEqual(SellerName.Create("GardenStore"), SellerName.Create("gardenstore"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_seller_product_id_is_required(string? raw)
    {
        Assert.Throws<InvalidSellerProductIdException>(() => SellerProductId.Create(raw));
    }

    [Theory]
    [InlineData("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d", true)]
    [InlineData("E5E5E5E5-F6F6-4A7A-B8B8-C9C9C9C9C9C9", true)]
    [InlineData("ddddeee-ffff-4000-1111-222233334444", false)]
    [InlineData("uddd0000-eeee-4111-ffff-aaaa22223333", false)]
    [InlineData("09835342345-4678-9abc-def012345678", false)]
    [InlineData("123", false)]
    [InlineData("  a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d  ", true)] // surrounding spaces are trimmed before the check
    public void The_guid_shape_is_informational(string raw, bool expected)
    {
        // Creating never fails because of the format: only the flag changes.
        Assert.Equal(expected, SellerProductId.Create(raw).IsGuidShaped);
    }
}
