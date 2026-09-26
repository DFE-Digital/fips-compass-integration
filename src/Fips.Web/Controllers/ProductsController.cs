using Fips.Data;
using Fips.Infrastructure.Configuration;
using Fips.Web.Models;
using Fips.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fips.Web.Controllers;

public sealed class ProductsController : Controller
{
    private readonly ICompassServiceRegisterApiService _compass;
    private readonly IOptions<CompassApiOptions> _compassOptions;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ICompassServiceRegisterApiService compass,
        IOptions<CompassApiOptions> compassOptions,
        ILogger<ProductsController> logger)
    {
        _compass = compass;
        _compassOptions = compassOptions;
        _logger = logger;
    }

    /// <summary>Paged register of active products, excluding enterprise services.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] int[]? categoryIds = null,
        [FromQuery] int[]? channelIds = null,
        [FromQuery] int[]? typeIds = null,
        [FromQuery] int[]? businessAreaIds = null,
        [FromQuery] int[]? userGroupIds = null,
        [FromQuery] string? userGroup = null,
        [FromQuery] string[]? phases = null,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 25;
        page = Math.Max(1, page);

        var selectedCategoryIds = NormalizeIdList(categoryIds);
        var selectedChannelIds = NormalizeIdList(channelIds);
        var selectedTypeIds = NormalizeIdList(typeIds);
        var selectedBusinessAreaIds = NormalizeIdList(businessAreaIds);
        var selectedUserGroupIds = NormalizeIdList(userGroupIds);
        var selectedPhases = NormalizePhases(phases);

        var search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var userGroupName = string.IsNullOrWhiteSpace(userGroup) ? null : userGroup.Trim();

        var listFilters = new CompassProductListFilters
        {
            CategoryIds = selectedCategoryIds,
            ChannelIds = selectedChannelIds,
            TypeIds = selectedTypeIds,
            BusinessAreaIds = selectedBusinessAreaIds,
            UserGroupIds = selectedUserGroupIds,
            ContactRoleIds = Array.Empty<int>(),
            Search = search
        };

        var categorisation = await _compass.GetCategorisationItemsAsync(cancellationToken);

        var fetched = await GetAllMatchingProductsAsync(listFilters, cancellationToken);
        if (fetched.ErrorMessage is not null)
            _logger.LogWarning("Product list failed: {Message}", fetched.ErrorMessage);

        var catalogue = fetched.Products;
        var phaseOptions = BuildPhaseOptions(catalogue, selectedPhases);
        var filtered = selectedPhases.Count == 0
            ? catalogue
            : catalogue
                .Where(product => selectedPhases.Contains(product.Phase?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                .ToList();

        if (userGroupName is not null)
        {
            filtered = filtered
                .Where(product => ProductMentionsUserGroup(product, userGroupName))
                .ToList();
        }

        var totalRecords = string.IsNullOrEmpty(fetched.ErrorMessage) ? filtered.Count : 0;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        if (page > totalPages)
            page = totalPages;

        IReadOnlyList<CompassServiceRegisterProduct> pageProducts = string.IsNullOrEmpty(fetched.ErrorMessage)
            ? filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            : Array.Empty<CompassServiceRegisterProduct>();

        ViewData["NavSection"] = "products";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Products", null)
        };

        var vm = new ProductListViewModel
        {
            Products = pageProducts,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            Register = ProductRegister.Standard,
            ListController = "Products",
            ErrorMessage = fetched.ErrorMessage,
            SelectedCategoryIds = selectedCategoryIds,
            SelectedChannelIds = selectedChannelIds,
            SelectedTypeIds = selectedTypeIds,
            SelectedBusinessAreaIds = selectedBusinessAreaIds,
            SelectedUserGroupIds = selectedUserGroupIds,
            SelectedUserGroupName = userGroupName,
            SelectedPhases = selectedPhases,
            PhaseOptions = phaseOptions,
            SelectedContactRoleIds = Array.Empty<int>(),
            SearchQuery = search,
            CategorisationItems = categorisation.Items,
            CategorisationLookupError = categorisation.ErrorMessage
        };

        return View(vm);
    }

    /// <summary>Product overview by service register unique id.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var loaded = await LoadByUniqueIdAsync(id, cancellationToken);
        if (loaded.NotFound)
            return NotFound();
        return await RenderProductAsync(loaded.Product, loaded.ErrorMessage, ProductDetailSection.Overview, cancellationToken);
    }

    /// <summary>Categories for a product, by service register unique id.</summary>
    [HttpGet]
    public async Task<IActionResult> Categories(int id, CancellationToken cancellationToken)
    {
        var loaded = await LoadByUniqueIdAsync(id, cancellationToken);
        if (loaded.NotFound)
            return NotFound();
        return await RenderProductAsync(loaded.Product, loaded.ErrorMessage, ProductDetailSection.Categories, cancellationToken);
    }

    /// <summary>GUID overview URLs redirect to the unique-id page.</summary>
    [HttpGet]
    public Task<IActionResult> DetailsByGuid(Guid id, CancellationToken cancellationToken) =>
        RedirectGuidAsync(id, ProductDetailSection.Overview, cancellationToken);

    /// <summary>GUID category URLs redirect to the unique-id page.</summary>
    [HttpGet]
    public Task<IActionResult> CategoriesByGuid(Guid id, CancellationToken cancellationToken) =>
        RedirectGuidAsync(id, ProductDetailSection.Categories, cancellationToken);

    private async Task<IActionResult> RedirectGuidAsync(
        Guid id,
        ProductDetailSection section,
        CancellationToken cancellationToken)
    {
        var result = await _compass.GetProductByIdAsync(id, cancellationToken);
        if (result.NotFound)
            return NotFound();

        if (result.Product?.NumericUniqueId is int uniqueId)
        {
            var action = section == ProductDetailSection.Categories ? nameof(Categories) : nameof(Details);
            return RedirectToAction(action, new { id = uniqueId });
        }

        return await RenderProductAsync(result.Product, result.ErrorMessage, section, cancellationToken);
    }

    private async Task<(CompassServiceRegisterProduct? Product, string? ErrorMessage, bool NotFound)> LoadByUniqueIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return (null, null, true);

        var result = await _compass.GetProductsByNumericUniqueIdAsync(id, cancellationToken);
        if (result.ErrorMessage is not null)
            return (null, result.ErrorMessage, false);

        var product = result.Products.FirstOrDefault(item => item.NumericUniqueId == id);
        return product is null ? (null, null, true) : (product, null, false);
    }

    private async Task<IActionResult> RenderProductAsync(
        CompassServiceRegisterProduct? product,
        string? errorMessage,
        ProductDetailSection section,
        CancellationToken cancellationToken)
    {
        if (errorMessage is not null || product is null)
        {
            ViewData["NavSection"] = "products";
            ViewBag.ErrorMessage = errorMessage ?? "Product could not be loaded.";
            ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
            {
                ("Home", Url.Action("Index", "Home")),
                ("Products", Url.Action("Index", "Products")),
                ("Product", null)
            };
            return View("Details", new ProductDetailViewModel { Section = section });
        }

        ViewData["NavSection"] = "products";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Products", Url.Action("Index", "Products")),
            (product.ProductName ?? "Product", null)
        };
        ViewData["ShowProductPageMasthead"] = true;

        IReadOnlyList<ProductCategoryAssignmentViewModel> assignments = Array.Empty<ProductCategoryAssignmentViewModel>();
        if (section == ProductDetailSection.Categories)
            assignments = await BuildCategoryAssignmentsAsync(product, cancellationToken);

        return View(
            "Details",
            new ProductDetailViewModel
            {
                Product = product,
                Section = section,
                CategoryAssignments = assignments,
                CompassEntryUrl = CompassEntryUrl(product.Id)
            });
    }

    private async Task<IReadOnlyList<ProductCategoryAssignmentViewModel>> BuildCategoryAssignmentsAsync(
        CompassServiceRegisterProduct product,
        CancellationToken cancellationToken)
    {
        var taxonomy = await _compass.GetCategorisationItemsAsync(cancellationToken);
        var assignments = new List<ProductCategoryAssignmentViewModel>();

        foreach (var name in SplitList(product.BusinessArea))
        {
            var row = FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.BusinessArea, name);
            assignments.Add(Assignment(
                "Business area",
                name,
                row?.Description,
                row is null
                    ? ProductsIndexUrl()
                    : Url.Action("Index", "Products", new { businessAreaIds = row.Id }) ?? ProductsIndexUrl()));
        }

        foreach (var channel in product.Channels)
        {
            var name = FirstNonEmpty(channel.Name, FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Channel, channel.Id)?.Name);
            if (name is null)
                continue;
            var row = FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Channel, channel.Id)
                ?? FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Channel, name);
            assignments.Add(Assignment(
                "Channel",
                name,
                row?.Description,
                Url.Action("Index", "Products", new { channelIds = channel.Id > 0 ? channel.Id : row?.Id }) ?? ProductsIndexUrl()));
        }

        if (!string.IsNullOrWhiteSpace(product.Phase))
        {
            assignments.Add(Assignment(
                "Phase",
                product.Phase.Trim(),
                null,
                Url.Action("Index", "Products", new { phases = product.Phase.Trim() }) ?? ProductsIndexUrl()));
        }

        foreach (var type in product.Types)
        {
            var name = FirstNonEmpty(type.Name, FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Type, type.Id)?.Name);
            if (name is null)
                continue;
            var row = FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Type, type.Id)
                ?? FindTaxonomy(taxonomy.Items, FipsTaxonomyKind.Type, name);
            assignments.Add(Assignment(
                "Type",
                name,
                row?.Description,
                Url.Action("Index", "Products", new { typeIds = type.Id > 0 ? type.Id : row?.Id }) ?? ProductsIndexUrl()));
        }

        foreach (var category in product.Categories
            .OrderBy(item => item.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var row = taxonomy.Items.FirstOrDefault(item =>
                item.TaxonomyKind == FipsTaxonomyKind.Categorisation && item.Id == category.Id);
            var name = FirstNonEmpty(category.Name, row?.Name) ?? "Category";
            assignments.Add(Assignment(
                string.IsNullOrWhiteSpace(category.GroupName) ? "Category" : category.GroupName.Trim(),
                name,
                row?.Description,
                Url.Action("Index", "Products", new { categoryIds = category.Id }) ?? ProductsIndexUrl()));
        }

        return assignments;
    }

    private static ProductCategoryAssignmentViewModel Assignment(
        string groupName,
        string name,
        string? description,
        string productsUrl) =>
        new()
        {
            GroupName = groupName,
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ProductsUrl = productsUrl
        };

    private string ProductsIndexUrl() => Url.Action("Index", "Products") ?? "/Products";

    private string? CompassEntryUrl(Guid productId)
    {
        if (productId == Guid.Empty)
            return null;

        var baseUrl = (_compassOptions.Value.BaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        return $"{baseUrl}/modern/manage/fips/{productId:D}";
    }

    private static CompassCategorisationItemRow? FindTaxonomy(
        IReadOnlyList<CompassCategorisationItemRow> items,
        FipsTaxonomyKind kind,
        int id) =>
        items.FirstOrDefault(item => item.TaxonomyKind == kind && item.Id == id);

    private static CompassCategorisationItemRow? FindTaxonomy(
        IReadOnlyList<CompassCategorisationItemRow> items,
        FipsTaxonomyKind kind,
        string name) =>
        items.FirstOrDefault(item =>
            item.TaxonomyKind == kind
            && string.Equals(item.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(value => value?.Trim()).FirstOrDefault(value => !string.IsNullOrEmpty(value));

    /// <summary>
    /// Loads every product matching the COMPASS filters. Phase is not a service-register query, so the page
    /// filters and paginates that set in the web app.
    /// </summary>
    private async Task<CompassServiceRegisterPagedResult> GetAllMatchingProductsAsync(
        CompassProductListFilters filters,
        CancellationToken cancellationToken)
    {
        const int fetchSize = 500;
        var all = new List<CompassServiceRegisterProduct>();

        for (var page = 1; page <= 20; page++)
        {
            var result = await _compass.GetActiveProductsPageAsync(
                page,
                fetchSize,
                filters,
                excludeEnterprise: true,
                cancellationToken);

            if (result.ErrorMessage is not null)
                return result;

            all.AddRange(result.Products);
            if (page >= result.TotalPages || result.Products.Count == 0)
                break;
        }

        return new CompassServiceRegisterPagedResult
        {
            Products = all,
            CurrentPage = 1,
            PageSize = all.Count,
            TotalPages = 1,
            TotalRecords = all.Count
        };
    }

    private static IReadOnlyList<string> BuildPhaseOptions(
        IReadOnlyList<CompassServiceRegisterProduct> products,
        IReadOnlyList<string> selectedPhases)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            var phase = product.Phase?.Trim();
            if (!string.IsNullOrEmpty(phase))
                names.Add(phase);
        }

        foreach (var selected in selectedPhases)
            names.Add(selected);

        return names
            .OrderBy(PhaseSortKey)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int PhaseSortKey(string phase)
    {
        for (var i = 0; i < PreferredPhaseOrder.Length; i++)
        {
            if (string.Equals(PreferredPhaseOrder[i], phase, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return PreferredPhaseOrder.Length;
    }

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

    private static bool ProductMentionsUserGroup(CompassServiceRegisterProduct product, string userGroupName) =>
        ContainsText(product.ProductName, userGroupName) || ContainsText(product.LongDescription, userGroupName);

    private static bool ContainsText(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> NormalizePhases(string[]? phases) =>
        phases?
            .Select(phase => phase.Trim())
            .Where(phase => phase.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

    private static int[] NormalizeIdList(int[]? ids) =>
        ids?
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray() ?? Array.Empty<int>();
}
