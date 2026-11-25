namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.StringExtensions (Similarity, OrderBySimilarityTo).</summary>
public static class StringExtensions
{
    /// <summary>
    /// The edit distance of two strings, ignoring case (Levenshtein): the lower, the more similar.
    /// </summary>
    public static int Similarity(this string? s, string? other)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(other) ? 0 : other.Length;
        }

        if (string.IsNullOrEmpty(other))
        {
            return s.Length;
        }

        s = s.ToLowerInvariant();
        other = other.ToLowerInvariant();
        int n = s.Length;
        int m = other.Length;
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 1; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = other[j - 1] == s[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    public static IEnumerable<string> OrderBySimilarityTo(this IEnumerable<string> list, string value) =>
        list.OrderBy(x => x.Similarity(value));
}
