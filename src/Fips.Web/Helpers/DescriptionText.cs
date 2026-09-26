using System.Text.RegularExpressions;

namespace Fips.Web.Helpers;

public static partial class DescriptionText
{
    public static string? Truncate(string? text, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var collapsed = Whitespace().Replace(text.Trim(), " ");
        if (collapsed.Length <= maxLength)
            return collapsed;

        var cut = collapsed[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace >= maxLength - 40)
            cut = cut[..lastSpace];

        return cut.TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
