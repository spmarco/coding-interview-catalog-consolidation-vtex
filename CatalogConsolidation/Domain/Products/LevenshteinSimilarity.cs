namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// Levenshtein distance and normalized similarity, used as the second (approximate)
/// matching stage described in README's "Approximate Matching" section.
/// No external library: a straightforward two-row dynamic programming implementation.
/// </summary>
public static class LevenshteinSimilarity
{
    public static int Distance(string a, string b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previousRow = new int[b.Length + 1];
        var currentRow = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previousRow[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            currentRow[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var deletion = previousRow[j] + 1;
                var insertion = currentRow[j - 1] + 1;
                var substitution = previousRow[j - 1] + cost;

                currentRow[j] = Math.Min(Math.Min(deletion, insertion), substitution);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[b.Length];
    }

    /// <summary>
    /// similarity(a, b) = 1 - levenshtein(a, b) / max(length(a), length(b)).
    /// 1.0 means identical, 0.0 means nothing in common. Two empty strings are identical.
    /// </summary>
    public static double Similarity(string a, string b)
    {
        var maxLength = Math.Max(a.Length, b.Length);
        if (maxLength == 0)
        {
            return 1.0;
        }

        return 1.0 - (double)Distance(a, b) / maxLength;
    }
}
