using Fips.Data;
using Fips.Web.Helpers;
using Fips.Web.Models;
using Fips.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class CategoriesController : Controller
{
    private readonly ICompassServiceRegisterApiService _compass;

    public CategoriesController(ICompassServiceRegisterApiService compass)
    {
        _compass = compass;
    }

    /// <summary>FIPS taxonomy groups. Each title links to the options in that group.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["NavSection"] = "categories";
        ViewData["NavRegister"] = "";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Categories", null)
        };

        var result = await _compass.GetCategorisationItemsAsync(cancellationToken);

        var groups = result.Items
            .Where(i => i.TaxonomyKind != FipsTaxonomyKind.ContactRole)
            .GroupBy(i => new { i.TaxonomyKind, i.GroupId })
            .Select(g =>
            {
                var first = g.First();
                return new CategoryGroupSummaryViewModel
                {
                    TaxonomyKind = g.Key.TaxonomyKind,
                    GroupId = g.Key.GroupId,
                    Title = first.GroupName ?? "Category",
                    Description = string.IsNullOrWhiteSpace(first.GroupDescription)
                        ? null
                        : first.GroupDescription.Trim(),
                    ItemCount = g.Count()
                };
            })
            .ToList();

        // Phase is not in COMPASS fips/configuration; surface it as a browse group like sibling lookups.
        var lookupGroups = groups
            .Where(g => g.TaxonomyKind != FipsTaxonomyKind.Categorisation)
            .Append(new CategoryGroupSummaryViewModel
            {
                TaxonomyKind = FipsTaxonomyKind.Phase,
                GroupId = ProductPhaseHelper.PhaseGroupId,
                Title = "Phase",
                Description = null,
                ItemCount = 0
            })
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categorisationGroups = groups
            .Where(g => g.TaxonomyKind == FipsTaxonomyKind.Categorisation)
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new CategoriesIndexViewModel
        {
            LookupGroups = lookupGroups,
            CategorisationGroups = categorisationGroups,
            ErrorMessage = result.ErrorMessage
        });
    }

    /// <summary>Items within one taxonomy group (channels, types, a categorisation group, etc.).</summary>
    [HttpGet]
    public async Task<IActionResult> Group(int taxonomyKind, int groupId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(FipsTaxonomyKind), taxonomyKind))
            return NotFound();

        var kind = (FipsTaxonomyKind)taxonomyKind;
        if (kind == FipsTaxonomyKind.ContactRole)
            return NotFound();

        ViewData["NavSection"] = "categories";
        ViewData["NavRegister"] = "";

        if (kind == FipsTaxonomyKind.Phase)
            return await PhaseGroupViewAsync(cancellationToken);

        var result = await _compass.GetCategorisationItemsAsync(cancellationToken);
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
            {
                ("Home", Url.Action("Index", "Home")),
                ("Categories", Url.Action("Index", "Categories")),
                ("Category", null)
            };
            return View(new CategoryGroupDetailViewModel
            {
                TaxonomyKind = kind,
                GroupId = groupId,
                GroupName = "Category",
                ErrorMessage = result.ErrorMessage,
                CategoryNavLinks = [],
                Items = []
            });
        }

        var categoryNavLinks = BuildCategoryNavLinks(result.Items, kind, groupId);

        var inGroup = result.Items
            .Where(i => i.TaxonomyKind == kind && i.GroupId == groupId)
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inGroup.Count == 0)
            return NotFound();

        var groupName = inGroup[0].GroupName ?? "Category";
        var groupDescription = inGroup[0].GroupDescription;

        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Categories", Url.Action("Index", "Categories")),
            (groupName, null)
        };

        IReadOnlyList<CategoryTaxonomyItemViewModel> items = inGroup.Select(row => new CategoryTaxonomyItemViewModel
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            ProductsUrl = BuildProductsUrlForTaxonomyItem(kind, row.Id)
        }).ToList();

        if (kind == FipsTaxonomyKind.UserGroup)
        {
            var loaded = await LoadUserGroupForestAsync(cancellationToken);
            if (!string.IsNullOrEmpty(loaded.ErrorMessage))
            {
                return View(new CategoryGroupDetailViewModel
                {
                    TaxonomyKind = kind,
                    GroupId = groupId,
                    GroupName = groupName,
                    GroupDescription = groupDescription,
                    ErrorMessage = loaded.ErrorMessage,
                    CategoryNavLinks = categoryNavLinks,
                    Items = []
                });
            }

            if (loaded.Roots.Count > 0)
                items = loaded.Roots.Select(ToUserGroupListItem).ToList();
        }

        return View(new CategoryGroupDetailViewModel
        {
            TaxonomyKind = kind,
            GroupId = groupId,
            GroupName = groupName,
            GroupDescription = groupDescription,
            CategoryNavLinks = categoryNavLinks,
            Items = items
        });
    }

    /// <summary>
    /// Phase options from loaded products (not a COMPASS taxonomy filter). Each phase links to the product list.
    /// </summary>
    private async Task<IActionResult> PhaseGroupViewAsync(CancellationToken cancellationToken)
    {
        var taxonomy = await _compass.GetCategorisationItemsAsync(cancellationToken);
        var categoryNavLinks = string.IsNullOrEmpty(taxonomy.ErrorMessage)
            ? BuildCategoryNavLinks(taxonomy.Items, FipsTaxonomyKind.Phase, ProductPhaseHelper.PhaseGroupId)
            : Array.Empty<CategoryGroupNavLinkViewModel>();

        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Categories", Url.Action("Index", "Categories")),
            ("Phase", null)
        };

        var (products, errorMessage) = await _compass.GetAllActiveProductsAsync(
            ProductRegister.Standard,
            cancellationToken);

        if (!string.IsNullOrEmpty(errorMessage))
        {
            return View(new CategoryGroupDetailViewModel
            {
                TaxonomyKind = FipsTaxonomyKind.Phase,
                GroupId = ProductPhaseHelper.PhaseGroupId,
                GroupName = "Phase",
                ErrorMessage = errorMessage,
                CategoryNavLinks = categoryNavLinks,
                Items = []
            });
        }

        var phases = ProductPhaseHelper.BuildPhaseOptions(products);
        var items = phases
            .Select((phase, index) => new CategoryTaxonomyItemViewModel
            {
                Id = index + 1,
                Name = phase,
                ProductsUrl = Url.Action("Index", "Products", new { phases = phase })!
            })
            .ToList();

        return View(new CategoryGroupDetailViewModel
        {
            TaxonomyKind = FipsTaxonomyKind.Phase,
            GroupId = ProductPhaseHelper.PhaseGroupId,
            GroupName = "Phase",
            CategoryNavLinks = categoryNavLinks,
            Items = items
        });
    }

    private IReadOnlyList<CategoryGroupNavLinkViewModel> BuildCategoryNavLinks(
        IReadOnlyList<CompassCategorisationItemRow> items,
        FipsTaxonomyKind currentKind,
        int currentGroupId)
    {
        var links = items
            .Where(i => i.TaxonomyKind != FipsTaxonomyKind.ContactRole)
            .GroupBy(i => new { i.TaxonomyKind, i.GroupId })
            .Select(g =>
            {
                var first = g.First();
                var tk = g.Key.TaxonomyKind;
                var gid = g.Key.GroupId;
                return new CategoryGroupNavLinkViewModel
                {
                    Text = first.GroupName ?? "Category",
                    Href = Url.Action("Group", "Categories", new { taxonomyKind = (int)tk, groupId = gid })!,
                    IsCurrent = tk == currentKind && gid == currentGroupId
                };
            })
            .Append(new CategoryGroupNavLinkViewModel
            {
                Text = "Phase",
                Href = Url.Action("Group", "Categories", new
                {
                    taxonomyKind = (int)FipsTaxonomyKind.Phase,
                    groupId = ProductPhaseHelper.PhaseGroupId
                })!,
                IsCurrent = currentKind == FipsTaxonomyKind.Phase
            })
            .OrderBy(link => link.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return links;
    }

    /// <summary>One user group, with links onward to its sub groups.</summary>
    [HttpGet]
    public async Task<IActionResult> UserGroup(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        var loaded = await LoadUserGroupForestAsync(cancellationToken);
        if (!string.IsNullOrEmpty(loaded.ErrorMessage))
            return await UserGroupErrorViewAsync(loaded.ErrorMessage, cancellationToken);

        var node = FindUserGroup(loaded.Roots, id);
        return node is null ? NotFound() : await UserGroupViewAsync(node, cancellationToken);
    }

    /// <summary>A sub group that the service register identifies by name only.</summary>
    [HttpGet]
    public async Task<IActionResult> UserGroupChild(int id, string slug, CancellationToken cancellationToken)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var loaded = await LoadUserGroupForestAsync(cancellationToken);
        if (!string.IsNullOrEmpty(loaded.ErrorMessage))
            return await UserGroupErrorViewAsync(loaded.ErrorMessage, cancellationToken);

        var parent = FindUserGroup(loaded.Roots, id);
        var node = parent?.Children.FirstOrDefault(child =>
            string.Equals(child.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (node is null)
            return NotFound();

        if (node.Id > 0)
            return RedirectToAction(nameof(UserGroup), new { id = node.Id });

        return await UserGroupViewAsync(node, cancellationToken);
    }

    private async Task<IActionResult> UserGroupViewAsync(UserGroupNode node, CancellationToken cancellationToken)
    {
        ViewData["NavSection"] = "categories";
        ViewData["NavRegister"] = "";

        var result = await _compass.GetCategorisationItemsAsync(cancellationToken);
        var categoryNavLinks = string.IsNullOrEmpty(result.ErrorMessage)
            ? BuildCategoryNavLinks(result.Items, FipsTaxonomyKind.UserGroup, -4)
            : Array.Empty<CategoryGroupNavLinkViewModel>();

        var breadcrumbs = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Categories", Url.Action("Index", "Categories")),
            ("User groups", Url.Action("Group", "Categories", new { taxonomyKind = (int)FipsTaxonomyKind.UserGroup, groupId = -4 }))
        };

        var chain = new List<UserGroupNode>();
        for (var current = node; current is not null; current = current.Parent)
            chain.Add(current);
        chain.Reverse();
        for (var i = 0; i < chain.Count; i++)
        {
            var entry = chain[i];
            breadcrumbs.Add((entry.Name, i == chain.Count - 1 ? null : UserGroupPageUrl(entry)));
        }

        ViewData["Breadcrumbs"] = breadcrumbs;

        return View("UserGroup", new CategoryGroupDetailViewModel
        {
            TaxonomyKind = FipsTaxonomyKind.UserGroup,
            GroupId = node.Id,
            GroupName = node.Name,
            GroupDescription = node.Description,
            ProductsUrl = UserGroupProductsUrl(node),
            CategoryNavLinks = categoryNavLinks,
            Items = node.Children.Select(ToUserGroupListItem).ToList()
        });
    }

    private async Task<IActionResult> UserGroupErrorViewAsync(string errorMessage, CancellationToken cancellationToken)
    {
        ViewData["NavSection"] = "categories";
        ViewData["NavRegister"] = "";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Categories", Url.Action("Index", "Categories")),
            ("User groups", null)
        };

        var result = await _compass.GetCategorisationItemsAsync(cancellationToken);
        return View("UserGroup", new CategoryGroupDetailViewModel
        {
            TaxonomyKind = FipsTaxonomyKind.UserGroup,
            GroupName = "User group",
            ErrorMessage = errorMessage,
            CategoryNavLinks = string.IsNullOrEmpty(result.ErrorMessage)
                ? BuildCategoryNavLinks(result.Items, FipsTaxonomyKind.UserGroup, -4)
                : [],
            Items = []
        });
    }

    private async Task<(IReadOnlyList<UserGroupNode> Roots, string? ErrorMessage)> LoadUserGroupForestAsync(
        CancellationToken cancellationToken)
    {
        var configuration = await _compass.GetFipsConfigurationAsync(cancellationToken);
        if (!string.IsNullOrEmpty(configuration.ErrorMessage))
            return ([], configuration.ErrorMessage);

        var groups = configuration.Configuration?.UserGroups ?? [];
        return (BuildUserGroupForest(groups), null);
    }

    private CategoryTaxonomyItemViewModel ToUserGroupListItem(UserGroupNode node) =>
        new()
        {
            Id = node.Id,
            Name = node.Name,
            Description = node.Description,
            PageUrl = UserGroupPageUrl(node),
            ProductsUrl = UserGroupProductsUrl(node),
            ChildCount = node.Children.Count
        };

    private string UserGroupPageUrl(UserGroupNode node)
    {
        if (node.Id > 0)
            return Url.Action("UserGroup", "Categories", new { id = node.Id })!;

        return Url.Action("UserGroupChild", "Categories", new { id = node.Parent!.Id, slug = node.Slug })!;
    }

    private string UserGroupProductsUrl(UserGroupNode node)
    {
        if (node.Id > 0)
            return Url.Action("Index", "Products", new { userGroupIds = node.Id })!;

        var ancestor = node.Parent;
        while (ancestor is not null && ancestor.Id <= 0)
            ancestor = ancestor.Parent;

        if (ancestor is { Id: > 0 })
            return Url.Action("Index", "Products", new { userGroupIds = ancestor.Id, userGroup = node.Name })!;

        return Url.Action("Index", "Products", new { userGroup = node.Name })!;
    }

    private static IReadOnlyList<UserGroupNode> BuildUserGroupForest(
        IReadOnlyList<CompassFipsUserGroupDto> groups)
    {
        var trail = new HashSet<int>();
        return groups
            .Where(group => group.Active && !string.IsNullOrWhiteSpace(group.Name))
            .Select(group => MapUserGroup(
                group.Id,
                group.Name,
                group.Description,
                NestedGroups(group.ChildGroups, group.Children),
                parent: null,
                trail))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static UserGroupNode MapUserGroup(
        int id,
        string name,
        string? description,
        IReadOnlyList<CompassFipsUserGroupNode> children,
        UserGroupNode? parent,
        HashSet<int> trail)
    {
        var node = new UserGroupNode
        {
            Id = id,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Parent = parent,
            Slug = ToUserGroupSlug(name)
        };

        if (id > 0 && !trail.Add(id))
            return node;

        node.Children = children
            .Where(child => child.Active && !string.IsNullOrWhiteSpace(child.Name))
            .Select(child => MapUserGroup(
                child.Id,
                child.Name,
                child.Description,
                NestedGroups(child.ChildGroups, child.Children),
                node,
                trail))
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (id > 0)
            trail.Remove(id);

        return node;
    }

    private static UserGroupNode? FindUserGroup(IEnumerable<UserGroupNode> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
                return node;

            var found = FindUserGroup(node.Children, id);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static IReadOnlyList<CompassFipsUserGroupNode> NestedGroups(
        IReadOnlyList<CompassFipsUserGroupNode> childGroups,
        IReadOnlyList<CompassFipsUserGroupNode> children) =>
        childGroups.Count > 0 ? childGroups : children;

    private static string ToUserGroupSlug(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return slug.Length == 0 ? "group" : slug;
    }

    private sealed class UserGroupNode
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string? Description { get; init; }
        public UserGroupNode? Parent { get; init; }
        public string Slug { get; init; } = "";
        public List<UserGroupNode> Children { get; set; } = [];
    }

    private string BuildProductsUrlForTaxonomyItem(FipsTaxonomyKind kind, int itemId)
    {
        return kind switch
        {
            FipsTaxonomyKind.Channel => Url.Action("Index", "Products", new { channelIds = new[] { itemId } })!,
            FipsTaxonomyKind.Type => Url.Action("Index", "Products", new { typeIds = new[] { itemId } })!,
            FipsTaxonomyKind.BusinessArea => Url.Action("Index", "Products", new { businessAreaIds = new[] { itemId } })!,
            FipsTaxonomyKind.UserGroup => Url.Action("Index", "Products", new { userGroupIds = itemId })!,
            FipsTaxonomyKind.ContactRole => Url.Action("Index", "Products", new { contactRoleIds = new[] { itemId } })!,
            FipsTaxonomyKind.Categorisation => Url.Action("Index", "Products", new { categoryIds = new[] { itemId } })!,
            _ => Url.Action("Index", "Products")!
        };
    }
}
