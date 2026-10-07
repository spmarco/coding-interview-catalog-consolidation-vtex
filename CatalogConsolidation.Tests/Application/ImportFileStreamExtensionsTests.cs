using System.Text;
using CatalogConsolidation.Application.Entries;
using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Tests.Application;

public class ImportFileStreamExtensionsTests
{
    private static Task<ImportFile> Read(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json)).ReadImportFileAsync(CancellationToken.None);

    private static async Task<IReadOnlyList<ProductEntryDto?>> Entries(string json) => (await Read(json)).Entries;

    [Fact]
    public async Task Reads_the_challenge_file_shape()
    {
        var entries = await Entries("""[{"Id":"a","SellerName":"S","Name":"N","Brand":"B","Category":"C"}]""");

        var entry = Assert.Single(entries);
        Assert.Equal(new ProductEntryDto("a", "S", "N", "B", "C"), entry);
    }

    [Fact]
    public async Task Property_names_are_not_case_sensitive_and_missing_fields_are_null()
    {
        var entries = await Entries("""[{"id":"a","sellername":"S","name":"N"}]""");

        Assert.Equal(new ProductEntryDto("a", "S", "N", null, null), Assert.Single(entries));
    }

    [Fact]
    public async Task Numbers_and_booleans_are_read_as_text()
    {
        var entries = await Entries("""[{"Id":123,"SellerName":"S","Name":"N","Brand":true,"Category":1.50}]""");

        Assert.Equal(new ProductEntryDto("123", "S", "N", "true", "1.50"), Assert.Single(entries));
    }

    [Fact]
    public async Task Objects_and_arrays_in_a_field_are_read_as_null_without_failing_the_file()
    {
        var entries = await Entries("""[{"Id":{"x":1},"SellerName":["a"],"Name":"N"}]""");

        Assert.Equal(new ProductEntryDto(null, null, "N", null, null), Assert.Single(entries));
    }

    [Fact]
    public async Task A_null_element_is_kept_so_the_importer_can_reject_that_row()
    {
        var entries = await Entries("""[null,{"Id":"a","SellerName":"S","Name":"N"}]""");

        Assert.Equal(2, entries.Count);
        Assert.Null(entries[0]);
    }

    [Fact]
    public async Task An_empty_array_is_a_valid_empty_import()
    {
        Assert.Empty(await Entries("[]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not valid json")]
    [InlineData("""{"not":"an array"}""")]
    [InlineData("""{"products":null}""")]
    [InlineData("""{"products":{"Id":"a"}}""")]
    [InlineData("null")]
    [InlineData("""[{"Id":"a"}""")]
    public async Task Anything_that_is_not_a_products_file_is_invalid(string json)
    {
        await Assert.ThrowsAsync<InvalidImportFileException>(() => Read(json));
    }

    [Fact]
    public async Task The_challenge_file_shape_asks_for_the_default_strategy()
    {
        var file = await Read("""[{"Id":"a","SellerName":"S","Name":"N"}]""");

        Assert.Equal(MatchStrategy.NameAndBrandSimilarity, file.Strategy);
        Assert.Null(file.UnrecognizedStrategy);
    }

    [Theory]
    [InlineData("name", MatchStrategy.Name)]
    [InlineData("NAME", MatchStrategy.Name)]
    [InlineData("nameAndBrand", MatchStrategy.NameAndBrand)]
    [InlineData("nameandbrandsimilarity", MatchStrategy.NameAndBrandSimilarity)]
    public async Task The_envelope_chooses_the_strategy_whatever_the_casing(string value, MatchStrategy expected)
    {
        var file = await Read($$"""{"matchStrategy":"{{value}}","products":[{"Id":"a","SellerName":"S","Name":"N"}]}""");

        Assert.Equal(expected, file.Strategy);
        Assert.Null(file.UnrecognizedStrategy);
        Assert.Single(file.Entries);
    }

    [Fact]
    public async Task The_envelope_may_carry_the_products_before_the_strategy()
    {
        var file = await Read("""{"products":[{"Id":"a","SellerName":"S","Name":"N"}],"matchStrategy":"name"}""");

        Assert.Equal(MatchStrategy.Name, file.Strategy);
    }

    [Theory]
    [InlineData("""{"products":[]}""")]
    [InlineData("""{"matchStrategy":null,"products":[]}""")]
    public async Task An_envelope_without_a_strategy_runs_the_default(string json)
    {
        var file = await Read(json);

        Assert.Equal(MatchStrategy.NameAndBrandSimilarity, file.Strategy);
        Assert.Null(file.UnrecognizedStrategy);
        Assert.Empty(file.Entries);
    }

    [Theory]
    [InlineData("\"bogus\"", "bogus")]
    [InlineData("\"7\"", "7")]       // a number with no enum member
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    public async Task An_unknown_strategy_keeps_the_raw_value_so_the_import_can_report_it(string jsonValue, string expected)
    {
        var file = await Read($$"""{"matchStrategy":{{jsonValue}},"products":[]}""");

        Assert.Equal(MatchStrategy.NameAndBrandSimilarity, file.Strategy);
        Assert.Equal(expected, file.UnrecognizedStrategy);
    }

    [Fact]
    public async Task An_unknown_property_in_the_envelope_is_ignored()
    {
        var file = await Read("""{"exportedAt":"2026-10-08","source":{"system":"erp"},"products":[],"matchStrategy":"name"}""");

        Assert.Equal(MatchStrategy.Name, file.Strategy);
        Assert.Empty(file.Entries);
    }
}
