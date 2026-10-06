using System.Globalization;
using System.Text;

namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// Normalizes product/brand text for comparison: lowercase, accent removal, trim,
/// whitespace collapsing, and removal of quotes/apostrophes (README decision #3).
/// A null or missing value normalizes to the empty string.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutQuotes = RemoveQuotesAndApostrophes(value);
        var withoutAccents = RemoveDiacritics(withoutQuotes);
        var collapsedWhitespace = CollapseWhitespace(withoutAccents);

        return collapsedWhitespace.Trim().ToLowerInvariant();
    }

    private static string RemoveQuotesAndApostrophes(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '"' or '\'')
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }
}
