using Fips.Data;

namespace Fips.Services;

public static class DataDashboardCalculator
{
    public static RegisterCompletenessStats ComputeRegisterStats(
        IReadOnlyList<CompassServiceRegisterProduct> products)
    {
        var total = products.Count;
        if (total == 0)
        {
            return new RegisterCompletenessStats();
        }

        var withCategory = 0;
        var withBa = 0;
        var withContact = 0;
        var withUrl = 0;
        foreach (var p in products)
        {
            if (p.Categories.Count > 0)
                withCategory++;
            if (!string.IsNullOrWhiteSpace(p.BusinessArea))
                withBa++;
            if (p.Contacts.Count > 0)
                withContact++;
            if (!string.IsNullOrWhiteSpace(p.ProductUrl))
                withUrl++;
        }

        return new RegisterCompletenessStats
        {
            TotalProducts = total,
            WithCategoryAssignment = withCategory,
            WithBusinessArea = withBa,
            WithContact = withContact,
            WithProductUrl = withUrl
        };
    }
}
