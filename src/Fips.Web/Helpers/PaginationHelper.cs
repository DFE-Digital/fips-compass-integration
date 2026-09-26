namespace Fips.Web.Helpers;

/// <summary>Builds page numbers for GOV.UK <c>govuk-pagination</c> (with ellipses when needed).</summary>
public static class PaginationHelper
{
    public sealed class PageEntry
    {
        public bool IsEllipsis { get; init; }
        public int Page { get; init; }
    }

    /// <summary>
    /// Includes page 1, last page, current ± <paramref name="siblingCount"/>, then inserts ellipsis between gaps.
    /// </summary>
    public static IReadOnlyList<PageEntry> GetPageEntries(int currentPage, int totalPages, int siblingCount = 2)
    {
        if (totalPages <= 1)
            return Array.Empty<PageEntry>();

        currentPage = Math.Clamp(currentPage, 1, totalPages);

        var include = new SortedSet<int> { 1, totalPages };
        for (var i = currentPage - siblingCount; i <= currentPage + siblingCount; i++)
        {
            if (i >= 1 && i <= totalPages)
                include.Add(i);
        }

        var sorted = include.ToList();
        var result = new List<PageEntry>();

        for (var idx = 0; idx < sorted.Count; idx++)
        {
            var page = sorted[idx];
            if (idx > 0)
            {
                var prev = sorted[idx - 1];
                if (page - prev > 1)
                    result.Add(new PageEntry { IsEllipsis = true, Page = 0 });
            }

            result.Add(new PageEntry { IsEllipsis = false, Page = page });
        }

        return result;
    }
}
