using System.Text;
using CatalogConsolidation.Application;
using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Tests.Support;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CatalogConsolidation.Tests.Application;

public class CatalogImportServiceTests
{
    private const string OneNewProduct =
        """[{"Id":"11111111-1111-4111-1111-111111111111","SellerName":"S","Name":"Brand New Gadget","Brand":"Acme","Category":"Gadgets"}]""";

    private readonly IProductRepository _products = StatefulMocks.Products();
    private readonly ICatalogUnitOfWork _unitOfWork = Substitute.For<ICatalogUnitOfWork>();
    private readonly ICatalogUnitOfWorkFactory _factory = Substitute.For<ICatalogUnitOfWorkFactory>();
    private readonly CatalogImportService _service;

    public CatalogImportServiceTests()
    {
        // Each collaborator is built before it is handed to Returns(...): configuring a substitute
        // inside another one's Returns(...) confuses NSubstitute.
        var catalog = StatefulMocks.Catalog();
        var sellerLinks = StatefulMocks.SellerLinks();

        _unitOfWork.Catalog.Returns(catalog);
        _unitOfWork.Products.Returns(_products);
        _unitOfWork.SellerLinks.Returns(sellerLinks);
        _factory.Begin().Returns(_unitOfWork);

        _service = new CatalogImportService(_factory, Options.Create(new MatchingOptions()));
    }

    private static Stream Json(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task A_successful_import_is_committed_and_the_transaction_released()
    {
        var report = await _service.ImportAsync(Json(OneNewProduct), CancellationToken.None);

        Assert.Equal(1, report.ProductsCreated);
        _unitOfWork.Received(1).Commit();
        _unitOfWork.Received(1).Dispose();
    }

    [Fact]
    public async Task An_invalid_file_never_opens_a_transaction()
    {
        await Assert.ThrowsAsync<InvalidImportFileException>(
            () => _service.ImportAsync(Json("not valid json"), CancellationToken.None));

        _factory.DidNotReceive().Begin();
    }

    [Fact]
    public async Task A_failure_in_the_middle_of_the_import_is_not_committed_but_is_released()
    {
        _products.When(p => p.Add(Arg.Any<Product>())).Throw(new InvalidOperationException("storage failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ImportAsync(Json(OneNewProduct), CancellationToken.None));

        _unitOfWork.DidNotReceive().Commit();
        _unitOfWork.Received(1).Dispose();
    }
}
