using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Tests.Support;

/// <summary>
/// A throwaway copy of the challenge's original catalog (Fixtures/catalog.original.db), so tests
/// neither depend on nor mutate the catalog.db the app is run against. Pooling is off so no pooled
/// connection keeps the file open: disposing really deletes it.
/// </summary>
internal sealed class TempCatalog : IDisposable
{
    private static readonly string OriginalCatalogPath =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "catalog.original.db");

    public static readonly string RepoRoot = FindRepoRoot();

    public static readonly string ProductEntryPath =
        System.IO.Path.Combine(RepoRoot, "load-tests", "ProductEntry.json");

    public TempCatalog()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"catalog-test-{Guid.NewGuid():N}.db");
        File.Copy(OriginalCatalogPath, Path);
    }

    public string Path { get; }

    public string ConnectionString => $"Data Source={Path};Pooling=False";

    public string ConnectionStringWith(string extraSetting) => $"{ConnectionString};{extraSetting}";

    public T Scalar<T>(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    public void Execute(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose() => File.Delete(Path);

    /// <summary>
    /// The solution file marks the root: it is tracked and never moves, unlike the load files.
    /// </summary>
    private static string FindRepoRoot()
    {
        const string SolutionFile = "CatalogConsolidation.slnx";

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, SolutionFile)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException($"Could not locate {SolutionFile} above the test base directory.");
    }
}
