using Fips.Data;

namespace Fips.Services;

/// <summary>
/// COMPASS Service Register <c>/api/v1/ServiceRegister/…</c> including FIPS lookups under <c>/api/v1/ServiceRegister/fips/…</c>.
/// </summary>
public interface ICompassServiceRegisterApiService
{
    /// <summary>
    /// Active products — <c>GET .../products?status=Active&amp;excludeEnterprise=…&amp;page=&amp;pageSize=</c>.
    /// When <paramref name="excludeEnterprise"/> is <see langword="false"/>, enterprise services are included in the same listing (not the enterprise-only endpoint).
    /// Optional FIPS filters via <paramref name="filters"/>.
    /// </summary>
    Task<CompassServiceRegisterPagedResult> GetActiveProductsPageAsync(
        int page,
        int pageSize,
        CompassProductListFilters? filters = null,
        bool excludeEnterprise = true,
        CancellationToken cancellationToken = default);

    /// <summary><c>GET .../products/enterprise-active?page=&amp;pageSize=</c> with the same optional FIPS filters.</summary>
    Task<CompassServiceRegisterPagedResult> GetEnterpriseActiveProductsPageAsync(
        int page,
        int pageSize,
        CompassProductListFilters? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads all pages of active products for the given register (exports and analytics). Uses page size 500.</summary>
    Task<(IReadOnlyList<CompassServiceRegisterProduct> Products, string? ErrorMessage)> GetAllActiveProductsAsync(
        ProductRegister register,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full FIPS taxonomy from <c>GET /api/v1/ServiceRegister/fips/configuration</c> — channels, types, business areas,
    /// user groups, contact roles, and categorisation groups (nested items). Same bundle as COMPASS <c>/api/products/fips/configuration</c>.
    /// Product filters map taxonomy rows to COMPASS query params (<c>categoryIds</c>, <c>channelIds</c>, etc.).
    /// </summary>
    Task<CompassCategorisationItemsResult> GetCategorisationItemsAsync(
        CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/v1/ServiceRegister/fips</c> or <c>…/fips/configuration</c> — bundle of all FIPS lookups.</summary>
    Task<CompassFipsConfigurationResult> GetFipsConfigurationAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsLookupDto>> GetFipsChannelsAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsLookupDto>> GetFipsTypesAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsBusinessAreaDto>> GetFipsBusinessAreasAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsUserGroupDto>> GetFipsUserGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsContactRoleDto>> GetFipsContactRolesAsync(
        CancellationToken cancellationToken = default);

    Task<CompassFipsListResult<CompassFipsCategorisationGroupDto>> GetFipsCategorisationGroupsAsync(
        CancellationToken cancellationToken = default);

    Task<CompassServiceRegisterFetchResult> GetProductsByNumericUniqueIdAsync(
        int numericUniqueId,
        CancellationToken cancellationToken = default);

    Task<CompassServiceRegisterSingleResult> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CompassServiceRegisterPatchResult> PatchProductUrlAsync(
        Guid id,
        string? productUrl,
        CancellationToken cancellationToken = default);
}

