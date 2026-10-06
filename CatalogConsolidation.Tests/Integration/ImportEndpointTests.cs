using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatalogConsolidation.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Tests.Integration;

/// <summary>
/// End-to-end tests against the real `catalog.db` and `ProductEntry.json` fixtures shipped with
/// the challenge, through actual HTTP calls. Each test gets its own throwaway copy of catalog.db
/// and its own host, all of which are disposed (and deleted) with the test class instance.
/// </summary>
public sealed class ImportEndpointTests : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    [Fact]
    public async Task Missing_file_part_returns_400()
    {
        var (client, _) = NewClient();
        using var content = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/catalog/imports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_multipart_request_returns_415()
    {
        var (client, _) = NewClient();

        var response = await client.PostAsJsonAsync("/api/catalog/imports", new { });

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Errors_are_returned_as_problem_details()
    {
        var (client, _) = NewClient();

        var response = await PostRaw(client, "not valid json");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Invalid import file.", problem.GetProperty("title").GetString());
        Assert.Equal("The file is not a valid JSON array of product entries.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_catalog_locked_by_another_import_returns_503()
    {
        var (client, catalog) = NewClient(connectionStringSetting: "Default Timeout=1");

        // Another connection holds the write lock, as a long-running import would.
        using var competingImport = new SqliteConnection(catalog.ConnectionString);
        competingImport.Open();
        using (var begin = competingImport.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE;";
            begin.ExecuteNonQuery();
        }

        var response = await PostRaw(client, "[]");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(503, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Garbage_multipart_body_returns_400()
    {
        var (client, _) = NewClient();
        using var content = new StringContent("this is not a multipart body", Encoding.UTF8);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=abc");

        var response = await client.PostAsync("/api/catalog/imports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_null_row_is_rejected_on_its_own_instead_of_failing_the_import()
    {
        var (client, _) = NewClient();
        var body = """[null,{"Id":"c0c0c0c0-0000-4000-8000-000000000001","SellerName":"S","Name":"Valid New Widget","Brand":"B","Category":"C"}]""";

        var report = await PostImport(client, Encoding.UTF8.GetBytes(body));

        Assert.Equal(1, report.GetProperty("rejectedRows").GetArrayLength());
        Assert.Equal(1, report.GetProperty("productsCreated").GetInt32());
    }

    [Fact]
    public async Task A_numeric_Id_is_accepted_and_stored_as_text()
    {
        var (client, catalog) = NewClient();
        var body = """[{"Id":123,"SellerName":"S","Name":"Numeric Id Widget","Brand":"B","Category":"C"}]""";

        var report = await PostImport(client, Encoding.UTF8.GetBytes(body));

        Assert.Equal(1, report.GetProperty("linksCreated").GetInt32());
        Assert.Equal(0, report.GetProperty("rejectedRows").GetArrayLength());
        Assert.Equal("123", catalog.Scalar<string>("SELECT SellerProductId FROM SellerProduct"));
        Assert.Equal("text", catalog.Scalar<string>("SELECT typeof(SellerProductId) FROM SellerProduct"));
    }

    [Fact]
    public async Task Importing_the_real_fixture_matches_the_documented_catalog_analysis()
    {
        var (client, _) = NewClient();
        var fileBytes = await File.ReadAllBytesAsync(TempCatalog.ProductEntryPath);

        var report = await PostImport(client, fileBytes);

        // README "Catalog Analysis": 269 entries, 266 exact matches, 2 approximate matches, 1 new product.
        Assert.Equal(266, report.GetProperty("exactMatches").GetInt32());
        Assert.Equal(2, report.GetProperty("approximateMatches").GetArrayLength());
        Assert.Equal(1, report.GetProperty("productsCreated").GetInt32());

        // Derived from the fixture's repeated Ids: 257 distinct (seller, product) links, 11 of them
        // carrying a different SellerProductId than the one already on file for that pair.
        Assert.Equal(257, report.GetProperty("linksCreated").GetInt32());
        Assert.Equal(11, report.GetProperty("duplicateLinksIgnored").GetArrayLength());

        // No row is rejected; 3 Ids are outside the GUID pattern and 1 entry diverges on category.
        Assert.Equal(0, report.GetProperty("rejectedRows").GetArrayLength());
        Assert.Equal(4, report.GetProperty("warnings").GetArrayLength());

        var approximateNames = report.GetProperty("approximateMatches").EnumerateArray()
            .Select(m => m.GetProperty("enteredName").GetString())
            .ToList();
        Assert.Contains(approximateNames, n => n!.Contains("Roteador"));
        Assert.Contains(approximateNames, n => n!.Contains("Processador"));
    }

    [Fact]
    public async Task Importing_the_real_fixture_persists_the_data_as_designed()
    {
        var (client, catalog) = NewClient();
        await PostImport(client, await File.ReadAllBytesAsync(TempCatalog.ProductEntryPath));

        // README decision #6: the SQL-looking brand is stored as plain text and nothing was executed.
        Assert.Equal(
            "TestBrand'; SELECT 1; --",
            catalog.Scalar<string>("SELECT Brand FROM Product WHERE Name = 'Security Test Product'"));
        Assert.Equal(976, catalog.Scalar<int>("SELECT COUNT(*) FROM Product"));

        Assert.Equal(257, catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct"));
        Assert.Equal(257, catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct WHERE typeof(SellerProductId) = 'text'"));

        // The seller's own spelling is kept exactly as sent (double space included) for traceability,
        // while the catalog product keeps its original name.
        Assert.Equal(
            1,
            catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct WHERE SellerProductName = 'Smartphone  Galaxy S23'"));
    }

    [Fact]
    public async Task Reprocessing_the_same_file_is_idempotent()
    {
        var (client, _) = NewClient();
        var fileBytes = await File.ReadAllBytesAsync(TempCatalog.ProductEntryPath);

        var firstReport = await PostImport(client, fileBytes);
        var secondReport = await PostImport(client, fileBytes);

        Assert.Equal(0, secondReport.GetProperty("productsCreated").GetInt32());
        Assert.Equal(0, secondReport.GetProperty("linksCreated").GetInt32());

        // duplicateLinksIgnored reports a structural fact about the file (two different Ids from
        // the same seller resolving to the same product) that is still true on every rerun — it is
        // not a signal that something new happened, so it repeats identically, it does not drop to zero.
        Assert.Equal(
            firstReport.GetProperty("duplicateLinksIgnored").GetArrayLength(),
            secondReport.GetProperty("duplicateLinksIgnored").GetArrayLength());
    }

    [Fact]
    public async Task A_second_SellerProductId_for_an_already_linked_seller_and_product_is_reported()
    {
        var (client, _) = NewClient();
        var firstUpload = """
            [{"Id":"11111111-1111-4111-1111-111111111111","SellerName":"AcmeStore","Name":"Totally New Gadget","Brand":"Acme","Category":"Gadgets"}]
            """;
        var secondUpload = """
            [{"Id":"22222222-2222-4222-2222-222222222222","SellerName":"AcmeStore","Name":"Totally New Gadget","Brand":"Acme","Category":"Gadgets"}]
            """;

        await PostImport(client, Encoding.UTF8.GetBytes(firstUpload));
        var report = await PostImport(client, Encoding.UTF8.GetBytes(secondUpload));

        Assert.Equal(0, report.GetProperty("linksCreated").GetInt32());
        var ignored = Assert.Single(report.GetProperty("duplicateLinksIgnored").EnumerateArray());
        Assert.Equal("Totally New Gadget", ignored.GetProperty("productName").GetString());
        Assert.Equal("22222222-2222-4222-2222-222222222222", ignored.GetProperty("discardedSellerProductId").GetString());
        Assert.Equal("11111111-1111-4111-1111-111111111111", ignored.GetProperty("existingSellerProductId").GetString());
    }

    [Fact]
    public async Task Concurrent_imports_of_the_same_new_product_create_it_exactly_once()
    {
        // README decision #9: BEGIN IMMEDIATE serializes imports, so no run can create the same product twice.
        var (client, catalog) = NewClient();
        var body = Encoding.UTF8.GetBytes(
            """[{"Id":"c0c0c0c0-0000-4000-8000-000000000001","SellerName":"RaceSeller","Name":"Concurrency Probe Widget","Brand":"RaceBrand","Category":"Test"}]""");

        var reports = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostImport(client, body)));

        Assert.Equal(1, reports.Sum(r => r.GetProperty("productsCreated").GetInt32()));
        Assert.Equal(1, catalog.Scalar<int>("SELECT COUNT(*) FROM Product WHERE Name = 'Concurrency Probe Widget'"));
        Assert.Equal(1, catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct WHERE SellerName = 'RaceSeller'"));
    }

    private static async Task<HttpResponseMessage> PostRaw(HttpClient client, string body)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(body, Encoding.UTF8, "application/json"), "file", "entries.json" }
        };

        return await client.PostAsync("/api/catalog/imports", content);
    }

    private static async Task<JsonElement> PostImport(HttpClient client, byte[] fileBytes)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(fileBytes), "file", "entries.json" }
        };

        var response = await client.PostAsync("/api/catalog/imports", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private (HttpClient Client, TempCatalog Catalog) NewClient(string? connectionStringSetting = null)
    {
        var catalog = new TempCatalog();
        var connectionString = connectionStringSetting is null
            ? catalog.ConnectionString
            : catalog.ConnectionStringWith(connectionStringSetting);
        var factory = new CatalogWebApplicationFactory(connectionString);
        var client = factory.CreateClient();

        // Disposed in reverse order: client, then the host, then the database file.
        _disposables.Add(catalog);
        _disposables.Add(factory);
        _disposables.Add(client);
        return (client, catalog);
    }

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
    }

    private sealed class CatalogWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public CatalogWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("CatalogDatabase:ConnectionString", _connectionString);
        }
    }
}
