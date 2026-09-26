using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fips.Data;
using Fips.Infrastructure.Configuration;
using Fips.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fips.Infrastructure.Compass;

public sealed class CompassServiceRegisterApiService : ICompassServiceRegisterApiService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<CompassApiOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CompassServiceRegisterApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CompassServiceRegisterApiService(
        HttpClient httpClient,
        IOptions<CompassApiOptions> options,
        IMemoryCache cache,
        ILogger<CompassServiceRegisterApiService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CompassServiceRegisterPagedResult> GetActiveProductsPageAsync(
        int page,
        int pageSize,
        CompassProductListFilters? filters = null,
        bool excludeEnterprise = true,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
        {
            return PagedError(configError!);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var excludeParam = excludeEnterprise ? "true" : "false";
        var requestUri =
            $"{baseUrl}/api/v1/ServiceRegister/products?status=Active&excludeEnterprise={excludeParam}&page={page}&pageSize={pageSize}";
        requestUri = AppendProductFilters(requestUri, filters);
        return await FetchProductsPageAsync(
            requestUri,
            token,
            filterNonEnterprise: excludeEnterprise,
            cancellationToken);
    }

    public async Task<CompassServiceRegisterPagedResult> GetEnterpriseActiveProductsPageAsync(
        int page,
        int pageSize,
        CompassProductListFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
        {
            return PagedError(configError!);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var requestUri =
            $"{baseUrl}/api/v1/ServiceRegister/products/enterprise-active?page={page}&pageSize={pageSize}";
        requestUri = AppendProductFilters(requestUri, filters);
        return await FetchProductsPageAsync(
            requestUri,
            token,
            filterNonEnterprise: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<CompassServiceRegisterProduct> Products, string? ErrorMessage)> GetAllActiveProductsAsync(
        ProductRegister register,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 500;
        var all = new List<CompassServiceRegisterProduct>();

        for (var page = 1; ; page++)
        {
            var result = register == ProductRegister.Enterprise
                ? await GetEnterpriseActiveProductsPageAsync(page, pageSize, null, cancellationToken)
                : await GetActiveProductsPageAsync(page, pageSize, null, cancellationToken: cancellationToken);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
                return ([], result.ErrorMessage);

            all.AddRange(result.Products);

            if (page >= result.TotalPages || result.Products.Count == 0)
                break;

            if (page > 500)
            {
                _logger.LogWarning(
                    "GetAllActiveProductsAsync stopped after safety page cap ({Register})",
                    register);
                break;
            }
        }

        return (all, null);
    }

    public async Task<CompassCategorisationItemsResult> GetCategorisationItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var configResult = await GetFipsConfigurationAsync(cancellationToken);
        if (!string.IsNullOrEmpty(configResult.ErrorMessage))
            return new CompassCategorisationItemsResult { ErrorMessage = configResult.ErrorMessage };

        if (configResult.Configuration is null)
            return new CompassCategorisationItemsResult { Items = [] };

        var items = FlattenFipsTaxonomyFromConfiguration(configResult.Configuration);
        return new CompassCategorisationItemsResult { Items = items };
    }

    /// <summary>
    /// Expands the full FIPS configuration bundle into browse rows: channels, types, business areas, user groups,
    /// contact roles, and categorisation groups (nested items). Lookup dimensions use synthetic negative <see cref="CompassCategorisationItemRow.GroupId"/> values.
    /// Categorisation items include each active item even when the parent group is inactive.
    /// </summary>
    private static IReadOnlyList<CompassCategorisationItemRow> FlattenFipsTaxonomyFromConfiguration(
        CompassFipsConfigurationDto config)
    {
        var rows = new List<CompassCategorisationItemRow>();

        void AddLookupPlane(
            IEnumerable<CompassFipsLookupDto> lookups,
            FipsTaxonomyKind kind,
            string groupTitle,
            int syntheticGroupId,
            int groupDisplayOrder)
        {
            foreach (var x in lookups
                         .Where(a => a.Active)
                         .OrderBy(a => a.DisplayOrder)
                         .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new CompassCategorisationItemRow
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    GroupId = syntheticGroupId,
                    GroupName = groupTitle,
                    GroupDescription = null,
                    DisplayOrder = x.DisplayOrder,
                    TaxonomyKind = kind,
                    GroupDisplayOrder = groupDisplayOrder
                });
            }
        }

        AddLookupPlane(config.Channels, FipsTaxonomyKind.Channel, "Channels", -1, 10);
        AddLookupPlane(config.Types, FipsTaxonomyKind.Type, "Types", -2, 20);

        foreach (var x in config.BusinessAreas
                     .Where(a => a.Active)
                     .OrderBy(a => a.DisplayOrder)
                     .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new CompassCategorisationItemRow
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                GroupId = -3,
                GroupName = "Business areas",
                GroupDescription = null,
                DisplayOrder = x.DisplayOrder,
                TaxonomyKind = FipsTaxonomyKind.BusinessArea,
                GroupDisplayOrder = 30
            });
        }

        AddLookupPlane(config.UserGroups, FipsTaxonomyKind.UserGroup, "User groups", -4, 40);
        AddLookupPlane(config.ContactRoles, FipsTaxonomyKind.ContactRole, "Contact roles", -5, 50);

        foreach (var g in config.CategorisationGroups
                     .Where(g => g.Active)
                     .OrderBy(x => x.DisplayOrder)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var i in g.Items
                         .OrderBy(x => x.DisplayOrder)
                         .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!i.Active)
                    continue;

                rows.Add(new CompassCategorisationItemRow
                {
                    Id = i.Id,
                    Name = i.Name,
                    Description = i.Description,
                    GroupId = g.Id,
                    GroupName = g.Name,
                    GroupDescription = g.Description,
                    DisplayOrder = i.DisplayOrder,
                    TaxonomyKind = FipsTaxonomyKind.Categorisation,
                    GroupDisplayOrder = g.DisplayOrder
                });
            }
        }

        return rows;
    }

    public async Task<CompassFipsConfigurationResult> GetFipsConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        var (config, error) = await GetCompassBearerJsonAsync<CompassFipsConfigurationDto>(
            "/api/v1/ServiceRegister/fips/configuration",
            cancellationToken);
        if (error is not null)
            return new CompassFipsConfigurationResult { ErrorMessage = error };

        return new CompassFipsConfigurationResult { Configuration = config };
    }

    public Task<CompassFipsListResult<CompassFipsLookupDto>> GetFipsChannelsAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsLookupDto>("/api/v1/ServiceRegister/fips/channels", cancellationToken);

    public Task<CompassFipsListResult<CompassFipsLookupDto>> GetFipsTypesAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsLookupDto>("/api/v1/ServiceRegister/fips/types", cancellationToken);

    public Task<CompassFipsListResult<CompassFipsBusinessAreaDto>> GetFipsBusinessAreasAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsBusinessAreaDto>(
            "/api/v1/ServiceRegister/fips/business-areas",
            cancellationToken);

    public Task<CompassFipsListResult<CompassFipsUserGroupDto>> GetFipsUserGroupsAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsUserGroupDto>("/api/v1/ServiceRegister/fips/user-groups", cancellationToken);

    public Task<CompassFipsListResult<CompassFipsContactRoleDto>> GetFipsContactRolesAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsContactRoleDto>(
            "/api/v1/ServiceRegister/fips/contact-roles",
            cancellationToken);

    public Task<CompassFipsListResult<CompassFipsCategorisationGroupDto>> GetFipsCategorisationGroupsAsync(
        CancellationToken cancellationToken = default) =>
        GetFipsDataListAsync<CompassFipsCategorisationGroupDto>(
            "/api/v1/ServiceRegister/fips/categorisation",
            cancellationToken);

    private async Task<CompassFipsListResult<T>> GetFipsDataListAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        var (payload, error) = await GetCompassBearerJsonAsync<FipsDataEnvelope<T>>(path, cancellationToken);
        if (error is not null)
            return new CompassFipsListResult<T> { ErrorMessage = error };

        var list = payload?.Data ?? new List<T>();
        return new CompassFipsListResult<T> { Items = list };
    }

    /// <summary>
    /// Bearer GET to Compass JSON APIs (Service Register v1 and <c>/api/v1/ServiceRegister/fips/…</c>).
    /// </summary>
    private async Task<(T? Body, string? ErrorMessage)> GetCompassBearerJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
            return (default, configError);

        var requestUri = $"{baseUrl}{path}";
        var http = await SendAuthorizedGetAsync(requestUri, token, cancellationToken);
        if (http.Exception is not null)
        {
            _logger.LogError(http.Exception, "COMPASS Bearer JSON request failed: {Uri}", requestUri);
            return (
                default,
                "Could not reach COMPASS. Check CompassApi:BaseUrl and network, then try again.");
        }

        var bodyText = http.Body;
        var contentType = http.ContentType;

        if (http.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "COMPASS Bearer JSON forbidden (403): {Uri}. Body: {Body}",
                requestUri,
                bodyText);
            return (
                default,
                "This COMPASS API is not available for this token or user (403). Check ServiceRegister permissions on the API token.");
        }

        if (http.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            _logger.LogError(
                "COMPASS Bearer JSON returned {StatusCode}: {Uri} Body: {Body}",
                (int)http.StatusCode,
                requestUri,
                bodyText);
            return (
                default,
                "The COMPASS API returned an error. Check the API token and permissions.");
        }

        var trimmed = bodyText.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal)
            || (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && trimmed.Length > 0
                && !trimmed.StartsWith("{", StringComparison.Ordinal)
                && !trimmed.StartsWith("[", StringComparison.Ordinal)))
        {
            var preview = bodyText.Length > 280 ? bodyText[..280] + "…" : bodyText;
            _logger.LogError(
                "COMPASS Bearer JSON returned non-JSON from {Uri} (Content-Type: {ContentType}). Preview: {Preview}",
                requestUri,
                contentType,
                preview);
            return (
                default,
                "COMPASS returned HTML instead of JSON (often OIDC login). Use Service Register v1 URLs under /api/v1/ServiceRegister/ with a valid Bearer token.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(bodyText, JsonOptions);
            return (parsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse COMPASS Bearer JSON from {Uri}", requestUri);
            return (default, "Unexpected response from the COMPASS API.");
        }
    }

    private sealed class FipsDataEnvelope<T>
    {
        public List<T>? Data { get; set; }
    }

    private static string AppendProductFilters(string uri, CompassProductListFilters? filters)
    {
        if (filters is null)
            return uri;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filters.Search))
            parts.Add("q=" + Uri.EscapeDataString(filters.Search.Trim()));

        AppendRepeatedIntParams(parts, "categoryIds", filters.CategoryIds);
        AppendRepeatedIntParams(parts, "channelIds", filters.ChannelIds);
        AppendRepeatedIntParams(parts, "typeIds", filters.TypeIds);
        AppendRepeatedIntParams(parts, "businessAreaIds", filters.BusinessAreaIds);
        AppendRepeatedIntParams(parts, "userGroupIds", filters.UserGroupIds);
        AppendRepeatedIntParams(parts, "contactRoleIds", filters.ContactRoleIds);

        if (parts.Count == 0)
            return uri;

        var sep = uri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return uri + sep + string.Join("&", parts);
    }

    private static void AppendRepeatedIntParams(
        List<string> parts,
        string queryName,
        IReadOnlyList<int> ids)
    {
        if (ids is not { Count: > 0 })
            return;
        foreach (var id in ids.Distinct())
        {
            if (id > 0)
                parts.Add($"{queryName}={id}");
        }
    }

    private async Task<CompassServiceRegisterPagedResult> FetchProductsPageAsync(
        string requestUri,
        string token,
        bool filterNonEnterprise,
        CancellationToken cancellationToken)
    {
        var http = await SendAuthorizedGetAsync(requestUri, token, cancellationToken);
        if (http.Exception is not null)
        {
            _logger.LogError(http.Exception, "COMPASS Service Register page request failed: {Uri}", requestUri);
            return PagedError(
                "Could not reach the COMPASS service register. Check CompassApi:BaseUrl and network, then try again.");
        }

        if (http.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            _logger.LogError(
                "COMPASS Service Register returned {StatusCode}: {Body}",
                (int)http.StatusCode,
                http.Body);
            return PagedError(
                "The COMPASS service register API returned an error. Check the API token and permissions (ServiceRegister read).");
        }

        var json = http.Body;
        CompassListResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CompassListResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse COMPASS Service Register JSON");
            return PagedError("Unexpected response from the COMPASS service register.");
        }

        var list = new List<CompassServiceRegisterProduct>();
        if (parsed?.Data is { Count: > 0 } rows)
        {
            foreach (var row in rows)
            {
                var product = MapListRowToProduct(row);
                if (filterNonEnterprise && product.IsEnterpriseService)
                    continue;
                list.Add(product);
            }
        }

        var pagination = parsed?.Pagination;
        return new CompassServiceRegisterPagedResult
        {
            Products = list,
            CurrentPage = pagination?.CurrentPage ?? 1,
            PageSize = pagination?.PageSize ?? list.Count,
            TotalPages = Math.Max(1, pagination?.TotalPages ?? 1),
            TotalRecords = pagination?.TotalRecords ?? list.Count
        };
    }

    private static CompassServiceRegisterPagedResult PagedError(string message) =>
        new() { ErrorMessage = message };

    public async Task<CompassServiceRegisterFetchResult> GetProductsByNumericUniqueIdAsync(
        int numericUniqueId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
        {
            return new CompassServiceRegisterFetchResult { ErrorMessage = configError };
        }

        var requestUri =
            $"{baseUrl}/api/v1/ServiceRegister/products?numericId={numericUniqueId}&page=1&pageSize=50";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("FipsCompassIntegration/1.0");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COMPASS Service Register numericId lookup failed for {NumericId}", numericUniqueId);
            return new CompassServiceRegisterFetchResult
            {
                ErrorMessage =
                    "Could not reach the COMPASS service register. Check CompassApi:BaseUrl and network, then try again."
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "COMPASS Service Register numericId={NumericId} returned {StatusCode}: {Body}",
                numericUniqueId,
                (int)response.StatusCode,
                err);
            return new CompassServiceRegisterFetchResult
            {
                ErrorMessage =
                    "The COMPASS service register API returned an error. Check the API token and permissions (ServiceRegister read)."
            };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        CompassListResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CompassListResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse COMPASS Service Register numericId JSON");
            return new CompassServiceRegisterFetchResult
            {
                ErrorMessage = "Unexpected response from the COMPASS service register."
            };
        }

        var list = new List<CompassServiceRegisterProduct>();
        if (parsed?.Data is { Count: > 0 } rows)
        {
            foreach (var row in rows)
                list.Add(MapListRowToProduct(row));
        }

        return new CompassServiceRegisterFetchResult { Products = list };
    }

    public async Task<CompassServiceRegisterSingleResult> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
        {
            return new CompassServiceRegisterSingleResult { ErrorMessage = configError };
        }

        var requestUri = $"{baseUrl}/api/v1/ServiceRegister/products/{id:N}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("FipsCompassIntegration/1.0");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COMPASS Service Register GET product {ProductId} failed", id);
            return new CompassServiceRegisterSingleResult
            {
                ErrorMessage =
                    "Could not reach the COMPASS service register. Check CompassApi:BaseUrl and network, then try again."
            };
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new CompassServiceRegisterSingleResult { NotFound = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "COMPASS Service Register GET product returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                err);
            return new CompassServiceRegisterSingleResult
            {
                ErrorMessage =
                    "The COMPASS service register API returned an error. Check the API token and permissions (ServiceRegister read)."
            };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        CompassSingleDataResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CompassSingleDataResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse COMPASS Service Register single product JSON");
            return new CompassServiceRegisterSingleResult
            {
                ErrorMessage = "Unexpected response from the COMPASS service register."
            };
        }

        if (parsed?.Data is null)
        {
            return new CompassServiceRegisterSingleResult
            {
                ErrorMessage = "Unexpected response from the COMPASS service register."
            };
        }

        return new CompassServiceRegisterSingleResult
        {
            Product = MapListRowToProduct(parsed.Data)
        };
    }

    public async Task<CompassServiceRegisterPatchResult> PatchProductUrlAsync(
        Guid id,
        string? productUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUrlAndToken(out var baseUrl, out var token, out var configError))
        {
            return new CompassServiceRegisterPatchResult { ErrorMessage = configError };
        }

        var requestUri = $"{baseUrl}/api/v1/ServiceRegister/products/{id:N}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("FipsCompassIntegration/1.0");
        request.Content = JsonContent.Create(new { productUrl });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COMPASS Service Register PATCH product {ProductId} failed", id);
            return new CompassServiceRegisterPatchResult
            {
                ErrorMessage =
                    "Could not reach the COMPASS service register. Check CompassApi:BaseUrl and network, then try again."
            };
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new CompassServiceRegisterPatchResult { NotFound = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "COMPASS Service Register PATCH product returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                err);
            return new CompassServiceRegisterPatchResult
            {
                ErrorMessage =
                    "The COMPASS service register API could not update the product URL. Check the API token and permissions (ServiceRegister update)."
            };
        }

        return new CompassServiceRegisterPatchResult { Success = true };
    }

    private bool TryGetBaseUrlAndToken(out string baseUrl, out string token, out string? errorMessage)
    {
        token = _options.Value.ApiToken?.Trim() ?? "";
        baseUrl = (_options.Value.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage =
                "Compass API token is not configured. Set CompassApi:ApiToken (and CompassApi:BaseUrl).";
            return false;
        }

        if (string.IsNullOrEmpty(baseUrl))
        {
            errorMessage = "Compass API base URL is not configured. Set CompassApi:BaseUrl.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static CompassServiceRegisterProduct MapListRowToProduct(CompassListRow row)
    {
        IReadOnlyList<CompassServiceRegisterContact> contacts = Array.Empty<CompassServiceRegisterContact>();
        if (row.Contacts is { Count: > 0 })
        {
            contacts = row.Contacts
                .Select(c => new CompassServiceRegisterContact
                {
                    Role = c.Role,
                    RoleId = c.RoleId,
                    Email = c.Email,
                    Name = c.Name,
                    CanManage = c.CanManage
                })
                .ToList();
        }

        IReadOnlyList<CompassProductCategory> categories = Array.Empty<CompassProductCategory>();
        if (row.Categories is { Count: > 0 })
        {
            categories = row.Categories
                .Select(c => new CompassProductCategory
                {
                    Id = c.Id,
                    Name = c.Name,
                    GroupId = c.GroupId,
                    GroupName = c.GroupName
                })
                .ToList();
        }

        return new CompassServiceRegisterProduct
        {
            Id = row.Id,
            UniqueId = row.UniqueId,
            ProductName = row.ProductName,
            Phase = row.Phase,
            BusinessArea = row.BusinessArea,
            ProductUrl = row.ProductUrl,
            Status = row.Status,
            IsEnterpriseService = row.IsEnterpriseService,
            LongDescription = row.LongDescription,
            Categories = categories,
            Channels = MapNamedLookups(row.Channels),
            Types = MapNamedLookups(row.Types),
            Contacts = contacts,
            LastUpdated = row.LastUpdated
        };
    }

    private static IReadOnlyList<CompassNamedLookup> MapNamedLookups(List<CompassNamedLookupRow>? rows)
    {
        if (rows is not { Count: > 0 })
            return Array.Empty<CompassNamedLookup>();

        return rows
            .Select(r => new CompassNamedLookup { Id = r.Id, Name = r.Name })
            .ToList();
    }

    private async Task<CompassHttpResult> SendAuthorizedGetAsync(
        string requestUri,
        string token,
        CancellationToken cancellationToken)
    {
        var ttlSeconds = _options.Value.CatalogCacheSeconds;
        var cacheKey = "GET " + requestUri;
        if (ttlSeconds > 0 && _cache.TryGetValue(cacheKey, out CompassHttpResult? cached) && cached is not null)
            return cached;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("FipsCompassIntegration/1.0");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return new CompassHttpResult { Exception = ex };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = new CompassHttpResult
        {
            StatusCode = response.StatusCode,
            Body = body,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? ""
        };

        if (ttlSeconds > 0 && response.IsSuccessStatusCode)
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(ttlSeconds));

        return result;
    }

    private sealed class CompassHttpResult
    {
        public HttpStatusCode StatusCode { get; init; }
        public string Body { get; init; } = "";
        public string ContentType { get; init; } = "";
        public Exception? Exception { get; init; }
    }

    private sealed class CompassListResponse
    {
        public List<CompassListRow>? Data { get; set; }
        public CompassPagination? Pagination { get; set; }
    }

    private sealed class CompassSingleDataResponse
    {
        public CompassListRow? Data { get; set; }
    }

    private sealed class CompassListRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("uniqueId")]
        [JsonConverter(typeof(CompassUniqueIdStringConverter))]
        public string? UniqueId { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("businessArea")]
        public string? BusinessArea { get; set; }

        [JsonPropertyName("productUrl")]
        public string? ProductUrl { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("isEnterpriseService")]
        public bool IsEnterpriseService { get; set; }

        [JsonPropertyName("longDescription")]
        public string? LongDescription { get; set; }

        [JsonPropertyName("categories")]
        public List<CompassListCategoryRow>? Categories { get; set; }

        [JsonPropertyName("channels")]
        public List<CompassNamedLookupRow>? Channels { get; set; }

        [JsonPropertyName("types")]
        public List<CompassNamedLookupRow>? Types { get; set; }

        [JsonPropertyName("contacts")]
        public List<CompassListContactRow>? Contacts { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime? LastUpdated { get; set; }
    }

    private sealed class CompassNamedLookupRow
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class CompassListCategoryRow
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("groupId")]
        public int GroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string? GroupName { get; set; }
    }

    private sealed class CompassListContactRow
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("roleId")]
        public int? RoleId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("canManage")]
        public bool CanManage { get; set; }
    }

    private sealed class CompassPagination
    {
        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("totalRecords")]
        public int TotalRecords { get; set; }
    }
}
