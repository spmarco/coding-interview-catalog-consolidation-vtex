using System.Text;
using CatalogConsolidation.Application;
using CatalogConsolidation.Application.Exceptions;

namespace CatalogConsolidation.Tests.Application;

public class ProductEntryStreamExtensionsTests
{
    private static Task<IReadOnlyList<ProductEntryDto?>> Read(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json)).ReadProductEntriesAsync(CancellationToken.None);

    [Fact]
    public async Task Reads_the_challenge_file_shape()
    {
        var entries = await Read("""[{"Id":"a","SellerName":"S","Name":"N","Brand":"B","Category":"C"}]""");

        var entry = Assert.Single(entries);
        Assert.Equal(new ProductEntryDto("a", "S", "N", "B", "C"), entry);
    }

    [Fact]
    public async Task Property_names_are_not_case_sensitive_and_missing_fields_are_null()
    {
        var entries = await Read("""[{"id":"a","sellername":"S","name":"N"}]""");

        Assert.Equal(new ProductEntryDto("a", "S", "N", null, null), Assert.Single(entries));
    }

    [Fact]
    public async Task Numbers_and_booleans_are_read_as_text()
    {
        var entries = await Read("""[{"Id":123,"SellerName":"S","Name":"N","Brand":true,"Category":1.50}]""");

        Assert.Equal(new ProductEntryDto("123", "S", "N", "true", "1.50"), Assert.Single(entries));
    }

    [Fact]
    public async Task Objects_and_arrays_in_a_field_are_read_as_null_without_failing_the_file()
    {
        var entries = await Read("""[{"Id":{"x":1},"SellerName":["a"],"Name":"N"}]""");

        Assert.Equal(new ProductEntryDto(null, null, "N", null, null), Assert.Single(entries));
    }

    [Fact]
    public async Task A_null_element_is_kept_so_the_importer_can_reject_that_row()
    {
        var entries = await Read("""[null,{"Id":"a","SellerName":"S","Name":"N"}]""");

        Assert.Equal(2, entries.Count);
        Assert.Null(entries[0]);
    }

    [Fact]
    public async Task An_empty_array_is_a_valid_empty_import()
    {
        Assert.Empty(await Read("[]"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not valid json")]
    [InlineData("""{"not":"an array"}""")]
    [InlineData("null")]
    [InlineData("""[{"Id":"a"}""")]
    public async Task Anything_that_is_not_a_json_array_of_entries_is_an_invalid_file(string json)
    {
        await Assert.ThrowsAsync<InvalidImportFileException>(() => Read(json));
    }
}
