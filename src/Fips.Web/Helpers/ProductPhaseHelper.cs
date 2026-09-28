using Fips.Data;

namespace Fips.Web.Helpers;

/// <summary>
/// Phase labels are not a COMPASS Service Register filter. Options are derived from loaded products
/// and ordered with a preferred lifecycle sequence.
/// </summary>
public static class ProductPhaseHelper
{
    /// <summary>Synthetic Categories group id for Phase (not from COMPASS configuration).</summary>
    public const int PhaseGroupId = -6;

    private static readonly string[] PreferredPhaseOrder =
    [
        "Discovery",
        "Alpha",
        "Beta",
        "Private beta",
        "Public beta",
        "Live",
        "Decommissioning",
        "Retired"
    ];

    public static IReadOnlyList<string> BuildPhaseOptions(
        IEnumerable<CompassServiceRegisterProduct> products,
        IEnumerable<string>? selectedPhases = null)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            var phase = product.Phase?.Trim();
            if (!string.IsNullOrEmpty(phase))
                names.Add(phase);
        }

        if (selectedPhases is not null)
        {
            foreach (var selected in selectedPhases)
            {
                var trimmed = selected?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    names.Add(trimmed);
            }
        }

        return names
            .OrderBy(PhaseSortKey)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int PhaseSortKey(string phase)
    {
        for (var i = 0; i < PreferredPhaseOrder.Length; i++)
        {
            if (string.Equals(PreferredPhaseOrder[i], phase, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return PreferredPhaseOrder.Length;
    }
}
