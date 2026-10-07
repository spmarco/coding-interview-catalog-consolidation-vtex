using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;
using NSubstitute;

namespace CatalogConsolidation.Tests.Support;

/// <summary>
/// NSubstitute mocks of the domain abstractions that keep just enough state for a whole import to
/// run: stored products get an identity, tracked products and stored links can be found again.
/// What was stored or tracked is checked with <c>Received()</c> on the mock.
/// </summary>
internal static class StatefulMocks
{
    /// <summary>A snapshot seeded with <paramref name="seed"/> that also finds every product it is later asked to track.</summary>
    public static ICatalogSnapshot Catalog(params Product[] seed)
    {
        var products = new List<Product>(seed);

        var catalog = Substitute.For<ICatalogSnapshot>();
        catalog.FindByKey(Arg.Any<ProductKey>())
            .Returns(call => products.FirstOrDefault(p => p.Key == call.Arg<ProductKey>()));
        catalog.FindByBrand(Arg.Any<ProductKey>())
            .Returns(call =>
            {
                var key = call.Arg<ProductKey>();
                return key.HasBrand
                    ? products.Where(p => p.Key.NormalizedBrand == key.NormalizedBrand).ToList()
                    : new List<Product>();
            });
        catalog.FindByName(Arg.Any<ProductKey>())
            .Returns(call =>
            {
                var key = call.Arg<ProductKey>();
                return products.Where(p => p.Key.NormalizedName == key.NormalizedName).ToList();
            });
        catalog.When(c => c.Track(Arg.Any<Product>()))
            .Do(call => products.Add(call.Arg<Product>()));

        return catalog;
    }

    /// <summary>A product repository that assigns identities after <paramref name="lastId"/>, like the database would.</summary>
    public static IProductRepository Products(long lastId = 0)
    {
        var products = Substitute.For<IProductRepository>();
        products.When(p => p.Add(Arg.Any<Product>())).Do(call => call.Arg<Product>().AssignId(++lastId));

        return products;
    }

    /// <summary>A seller-link repository that remembers what it was given, so a later <c>Find</c> returns it.</summary>
    public static ISellerLinkRepository SellerLinks()
    {
        var stored = new Dictionary<(SellerName Seller, long ProductId), SellerProduct>();

        var links = Substitute.For<ISellerLinkRepository>();
        links.Find(Arg.Any<SellerName>(), Arg.Any<long>())
            .Returns(call => stored.GetValueOrDefault((call.Arg<SellerName>(), call.Arg<long>())));
        links.When(l => l.Add(Arg.Any<SellerProduct>()))
            .Do(call =>
            {
                var link = call.Arg<SellerProduct>();
                link.AssignId(stored.Count + 1);
                stored[(link.Seller, link.ProductId)] = link;
            });

        return links;
    }
}
