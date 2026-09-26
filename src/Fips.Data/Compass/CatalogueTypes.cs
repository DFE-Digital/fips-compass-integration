namespace Fips.Data;

/// <summary>Optional Service Register product list filters (maps to COMPASS categoryIds, channelIds, and related query params).</summary>
public sealed class CompassProductListFilters
{
    public IReadOnlyList<int> CategoryIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> ChannelIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> TypeIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> BusinessAreaIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> UserGroupIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> ContactRoleIds { get; init; } = Array.Empty<int>();
    public string? Search { get; init; }
}

public enum ProductRegister
{
    Standard,
    Enterprise
}

public sealed class RegisterCompletenessStats
{
    public int TotalProducts { get; init; }
    public int WithCategoryAssignment { get; init; }
    public int WithBusinessArea { get; init; }
    public int WithContact { get; init; }
    public int WithProductUrl { get; init; }

    public int CategoryCompletionPercent =>
        TotalProducts == 0 ? 100 : (int)Math.Round(100.0 * WithCategoryAssignment / TotalProducts);

    public int BusinessAreaCompletionPercent =>
        TotalProducts == 0 ? 100 : (int)Math.Round(100.0 * WithBusinessArea / TotalProducts);

    public int ContactCompletionPercent =>
        TotalProducts == 0 ? 100 : (int)Math.Round(100.0 * WithContact / TotalProducts);

    public int ProductUrlCompletionPercent =>
        TotalProducts == 0 ? 100 : (int)Math.Round(100.0 * WithProductUrl / TotalProducts);
}
