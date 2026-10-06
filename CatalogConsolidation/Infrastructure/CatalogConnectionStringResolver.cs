using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// Resolves a relative "Data Source" against the app's content root and, unless the connection
/// string sets a Mode explicitly, opens the database ReadWrite (never ReadWriteCreate): a wrong
/// path must fail loudly instead of silently creating an empty database.
/// </summary>
public static class CatalogConnectionStringResolver
{
    public static string Resolve(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        }

        // SqliteConnectionStringBuilder reports every known keyword as present, so ask a generic
        // builder whether the caller actually wrote a Mode.
        var modeWasExplicit = new DbConnectionStringBuilder { ConnectionString = connectionString }.ContainsKey("Mode");
        if (!modeWasExplicit)
        {
            builder.Mode = SqliteOpenMode.ReadWrite;
        }

        return builder.ConnectionString;
    }
}
