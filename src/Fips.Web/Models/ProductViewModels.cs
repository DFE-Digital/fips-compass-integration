using Fips.Data;
using Microsoft.AspNetCore.Routing;

namespace Fips.Web.Models;

public sealed class ProductListViewModel
{
    public IReadOnlyList<CompassServiceRegisterProduct> Products { get; init; } =
        Array.Empty<CompassServiceRegisterProduct>();

    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalRecords { get; init; }
    public ProductRegister Register { get; init; } = ProductRegister.Standard;

    /// <summary>MVC controller for list actions and filter form postback — <c>Products</c> (standard) or <c>Enterprise</c>.</summary>
    public string ListController { get; init; } = "Products";

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<int> SelectedCategoryIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> SelectedChannelIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> SelectedTypeIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> SelectedBusinessAreaIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> SelectedUserGroupIds { get; init; } = Array.Empty<int>();

    /// <summary>User group known by name only, when the service register does not send an id.</summary>
    public string? SelectedUserGroupName { get; init; }
    public IReadOnlyList<int> SelectedContactRoleIds { get; init; } = Array.Empty<int>();

    /// <summary>Phase names selected in the filter (matched on the product phase label).</summary>
    public IReadOnlyList<string> SelectedPhases { get; init; } = Array.Empty<string>();

    /// <summary>Phase labels available for the current catalogue (and any selected phases).</summary>
    public IReadOnlyList<string> PhaseOptions { get; init; } = Array.Empty<string>();

    /// <summary>Title search (server-side contains).</summary>
    public string? SearchQuery { get; init; }

    /// <summary>Full FIPS taxonomy from configuration for filter checkboxes.</summary>
    public IReadOnlyList<CompassCategorisationItemRow> CategorisationItems { get; init; } =
        Array.Empty<CompassCategorisationItemRow>();

    /// <summary>Optional warning when categorisation lookup failed but products may still load.</summary>
    public string? CategorisationLookupError { get; init; }

    public bool IsFilterSelected(CompassCategorisationItemRow item) =>
        item.TaxonomyKind switch
        {
            FipsTaxonomyKind.Channel => SelectedChannelIds.Contains(item.Id),
            FipsTaxonomyKind.Type => SelectedTypeIds.Contains(item.Id),
            FipsTaxonomyKind.BusinessArea => SelectedBusinessAreaIds.Contains(item.Id),
            FipsTaxonomyKind.ContactRole => SelectedContactRoleIds.Contains(item.Id),
            FipsTaxonomyKind.Categorisation => SelectedCategoryIds.Contains(item.Id),
            _ => false
        };

    public static string FilterQueryParamName(FipsTaxonomyKind kind) =>
        kind switch
        {
            FipsTaxonomyKind.Channel => "channelIds",
            FipsTaxonomyKind.Type => "typeIds",
            FipsTaxonomyKind.BusinessArea => "businessAreaIds",
            FipsTaxonomyKind.UserGroup => "userGroupIds",
            FipsTaxonomyKind.ContactRole => "contactRoleIds",
            FipsTaxonomyKind.Categorisation => "categoryIds",
            _ => "categoryIds"
        };

    /// <summary>Any query-driven filter beyond the default standard listing.</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchQuery)
        || SelectedCategoryIds.Count > 0
        || SelectedChannelIds.Count > 0
        || SelectedTypeIds.Count > 0
        || SelectedBusinessAreaIds.Count > 0
        || SelectedUserGroupIds.Count > 0
        || !string.IsNullOrWhiteSpace(SelectedUserGroupName)
        || SelectedContactRoleIds.Count > 0
        || SelectedPhases.Count > 0;

    public static string TaxonomyKindCaption(FipsTaxonomyKind kind) =>
        kind switch
        {
            FipsTaxonomyKind.Channel => "Channel",
            FipsTaxonomyKind.Type => "Type",
            FipsTaxonomyKind.BusinessArea => "Business area",
            FipsTaxonomyKind.UserGroup => "User group",
            FipsTaxonomyKind.ContactRole => "Contact role",
            FipsTaxonomyKind.Categorisation => "Category",
            _ => "Filter"
        };

    public string GetTaxonomyItemDisplayName(FipsTaxonomyKind kind, int id)
    {
        var row = CategorisationItems.FirstOrDefault(i => i.TaxonomyKind == kind && i.Id == id);
        return string.IsNullOrWhiteSpace(row?.Name) ? $"#{id}" : row!.Name.Trim();
    }

    /// <summary>Query route for the current list (filters, search, page). Used for pagination and active-filter chips.</summary>
    public RouteValueDictionary ToProductsRoute(int page = 1) =>
        BuildProductsRoute(
            page,
            SearchQuery,
            SelectedCategoryIds,
            SelectedChannelIds,
            SelectedTypeIds,
            SelectedBusinessAreaIds,
            SelectedUserGroupIds,
            SelectedUserGroupName,
            SelectedContactRoleIds,
            SelectedPhases);

    public RouteValueDictionary ToProductsRouteRemovingTaxonomyItem(FipsTaxonomyKind kind, int itemId, int page = 1)
    {
        var cats = SelectedCategoryIds.ToList();
        var ch = SelectedChannelIds.ToList();
        var ty = SelectedTypeIds.ToList();
        var ba = SelectedBusinessAreaIds.ToList();
        var ug = SelectedUserGroupIds.ToList();
        var cr = SelectedContactRoleIds.ToList();
        switch (kind)
        {
            case FipsTaxonomyKind.Categorisation:
                cats.Remove(itemId);
                break;
            case FipsTaxonomyKind.Channel:
                ch.Remove(itemId);
                break;
            case FipsTaxonomyKind.Type:
                ty.Remove(itemId);
                break;
            case FipsTaxonomyKind.BusinessArea:
                ba.Remove(itemId);
                break;
            case FipsTaxonomyKind.UserGroup:
                ug.Remove(itemId);
                break;
            case FipsTaxonomyKind.ContactRole:
                cr.Remove(itemId);
                break;
            default:
                break;
        }

        return BuildProductsRoute(page, SearchQuery, cats, ch, ty, ba, ug, SelectedUserGroupName, cr, SelectedPhases);
    }

    public RouteValueDictionary ToProductsRouteRemovingPhase(string phase, int page = 1)
    {
        var remaining = SelectedPhases
            .Where(p => !string.Equals(p, phase, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return BuildProductsRoute(
            page,
            SearchQuery,
            SelectedCategoryIds,
            SelectedChannelIds,
            SelectedTypeIds,
            SelectedBusinessAreaIds,
            SelectedUserGroupIds,
            SelectedUserGroupName,
            SelectedContactRoleIds,
            remaining);
    }

    public RouteValueDictionary ToProductsRouteClearSearch(int page = 1) =>
        BuildProductsRoute(
            page,
            null,
            SelectedCategoryIds,
            SelectedChannelIds,
            SelectedTypeIds,
            SelectedBusinessAreaIds,
            SelectedUserGroupIds,
            SelectedUserGroupName,
            SelectedContactRoleIds,
            SelectedPhases);

    public RouteValueDictionary ToProductsRouteRemovingUserGroupName(int page = 1) =>
        BuildProductsRoute(
            page,
            SearchQuery,
            SelectedCategoryIds,
            SelectedChannelIds,
            SelectedTypeIds,
            SelectedBusinessAreaIds,
            SelectedUserGroupIds,
            null,
            SelectedContactRoleIds,
            SelectedPhases);

    /// <summary>Drop all taxonomy and search filters (same list: standard vs enterprise is fixed by <see cref="ListController"/>).</summary>
    public RouteValueDictionary ToProductsRouteClearAllFilters(int page = 1) =>
        new RouteValueDictionary { ["page"] = page };

    private RouteValueDictionary BuildProductsRoute(
        int page,
        string? searchQuery,
        IReadOnlyList<int> categoryIds,
        IReadOnlyList<int> channelIds,
        IReadOnlyList<int> typeIds,
        IReadOnlyList<int> businessAreaIds,
        IReadOnlyList<int> userGroupIds,
        string? userGroupName,
        IReadOnlyList<int> contactRoleIds,
        IReadOnlyList<string> phases)
    {
        var r = new RouteValueDictionary { ["page"] = page };

        if (!string.IsNullOrWhiteSpace(searchQuery))
            r["q"] = searchQuery.Trim();

        if (categoryIds.Count > 0)
            r["categoryIds"] = categoryIds.ToArray();
        if (channelIds.Count > 0)
            r["channelIds"] = channelIds.ToArray();
        if (typeIds.Count > 0)
            r["typeIds"] = typeIds.ToArray();
        if (businessAreaIds.Count > 0)
            r["businessAreaIds"] = businessAreaIds.ToArray();
        if (userGroupIds.Count > 0)
            r["userGroupIds"] = userGroupIds.ToArray();
        if (!string.IsNullOrWhiteSpace(userGroupName))
            r["userGroup"] = userGroupName.Trim();
        if (contactRoleIds.Count > 0)
            r["contactRoleIds"] = contactRoleIds.ToArray();
        if (phases.Count > 0)
            r["phases"] = phases.ToArray();

        return r;
    }
}

public sealed class CategoriesIndexViewModel
{
    /// <summary>Channels, types, business areas, user groups — not contact roles or Compass categorisation groups.</summary>
    public IReadOnlyList<CategoryGroupSummaryViewModel> LookupGroups { get; init; } =
        Array.Empty<CategoryGroupSummaryViewModel>();

    /// <summary>Compass categorisation groups only, ordered by group name.</summary>
    public IReadOnlyList<CategoryGroupSummaryViewModel> CategorisationGroups { get; init; } =
        Array.Empty<CategoryGroupSummaryViewModel>();

    public string? ErrorMessage { get; init; }
}

/// <summary>One taxonomy group on the categories landing page.</summary>
public sealed class CategoryGroupSummaryViewModel
{
    public FipsTaxonomyKind TaxonomyKind { get; init; }
    public int GroupId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public int ItemCount { get; init; }
}

/// <summary>Link in the categories side navigation (all taxonomy groups).</summary>
public sealed class CategoryGroupNavLinkViewModel
{
    public string Text { get; init; } = "";
    public string Href { get; init; } = "";
    public bool IsCurrent { get; init; }
}

/// <summary>Detail page for a single taxonomy group — items and links to filtered products.</summary>
public sealed class CategoryGroupDetailViewModel
{
    public FipsTaxonomyKind TaxonomyKind { get; init; }
    public int GroupId { get; init; }
    public string GroupName { get; init; } = "";
    public string? GroupDescription { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ProductsUrl { get; init; }
    public IReadOnlyList<CategoryGroupNavLinkViewModel> CategoryNavLinks { get; init; } =
        Array.Empty<CategoryGroupNavLinkViewModel>();
    public IReadOnlyList<CategoryTaxonomyItemViewModel> Items { get; init; } =
        Array.Empty<CategoryTaxonomyItemViewModel>();
}

public sealed class CategoryTaxonomyItemViewModel
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string ProductsUrl { get; init; } = "";
    public string PageUrl { get; init; } = "";
    public int ChildCount { get; init; }
    public IReadOnlyList<CategoryTaxonomyItemViewModel> Children { get; init; } =
        Array.Empty<CategoryTaxonomyItemViewModel>();
}

public enum ProductDetailSection
{
    Overview,
    Categories
}

public sealed class ProductCategoryAssignmentViewModel
{
    public string GroupName { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string ProductsUrl { get; init; } = "";
}

public sealed class ProductDetailViewModel
{
    public CompassServiceRegisterProduct? Product { get; init; }
    public ProductDetailSection Section { get; init; } = ProductDetailSection.Overview;
    public IReadOnlyList<ProductCategoryAssignmentViewModel> CategoryAssignments { get; init; } =
        Array.Empty<ProductCategoryAssignmentViewModel>();

    /// <summary>COMPASS manage page for this product, when the register id and COMPASS host are known.</summary>
    public string? CompassEntryUrl { get; init; }
}
