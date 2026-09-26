using System.Text.Json.Serialization;

namespace Fips.Data;

public sealed class CompassServiceRegisterPagedResult
{
    public IReadOnlyList<CompassServiceRegisterProduct> Products { get; init; } = Array.Empty<CompassServiceRegisterProduct>();
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; }
    public int TotalPages { get; init; } = 1;
    public int TotalRecords { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class CompassServiceRegisterFetchResult
{
    public IReadOnlyList<CompassServiceRegisterProduct> Products { get; init; } = Array.Empty<CompassServiceRegisterProduct>();
    public string? ErrorMessage { get; init; }
}

public sealed class CompassServiceRegisterSingleResult
{
    public CompassServiceRegisterProduct? Product { get; init; }
    public string? ErrorMessage { get; init; }
    public bool NotFound { get; init; }
}

public sealed class CompassServiceRegisterPatchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool NotFound { get; init; }
}

public sealed class CompassServiceRegisterProduct
{
    public Guid Id { get; init; }
    public string? UniqueId { get; init; }

    /// <summary>Service register <c>uniqueId</c> when it is a positive number. Product pages use this in the URL.</summary>
    public int? NumericUniqueId =>
        int.TryParse(UniqueId?.Trim(), out var id) && id > 0 ? id : null;
    public string? ProductName { get; init; }
    public string? Phase { get; init; }
    public string? BusinessArea { get; init; }
    public string? ProductUrl { get; init; }
    public string? Status { get; init; }
    public bool IsEnterpriseService { get; init; }
    /// <summary>User description from COMPASS when set; otherwise CMDB description (<c>longDescription</c> in Service Register API).</summary>
    public string? LongDescription { get; init; }
    public IReadOnlyList<CompassProductCategory> Categories { get; init; } = Array.Empty<CompassProductCategory>();
    public IReadOnlyList<CompassNamedLookup> Channels { get; init; } = Array.Empty<CompassNamedLookup>();
    public IReadOnlyList<CompassNamedLookup> Types { get; init; } = Array.Empty<CompassNamedLookup>();
    public IReadOnlyList<CompassServiceRegisterContact> Contacts { get; init; } = Array.Empty<CompassServiceRegisterContact>();

    /// <summary>When returned by the Service Register API.</summary>
    public DateTime? LastUpdated { get; init; }
}

public sealed class CompassNamedLookup
{
    public int Id { get; init; }
    public string? Name { get; init; }
}

public sealed class CompassProductCategory
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public int GroupId { get; init; }
    public string? GroupName { get; init; }
}

public sealed class CompassCategorisationItemsResult
{
    public IReadOnlyList<CompassCategorisationItemRow> Items { get; init; } = Array.Empty<CompassCategorisationItemRow>();
    public string? ErrorMessage { get; init; }
}

/// <summary><c>GET /api/v1/ServiceRegister/fips/configuration</c> bundle response.</summary>
public sealed class CompassFipsConfigurationResult
{
    public CompassFipsConfigurationDto? Configuration { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class CompassFipsConfigurationDto
{
    public IReadOnlyList<CompassFipsLookupDto> Channels { get; init; } = Array.Empty<CompassFipsLookupDto>();
    public IReadOnlyList<CompassFipsLookupDto> Types { get; init; } = Array.Empty<CompassFipsLookupDto>();
    public IReadOnlyList<CompassFipsBusinessAreaDto> BusinessAreas { get; init; } =
        Array.Empty<CompassFipsBusinessAreaDto>();
    public IReadOnlyList<CompassFipsUserGroupDto> UserGroups { get; init; } = Array.Empty<CompassFipsUserGroupDto>();
    public IReadOnlyList<CompassFipsContactRoleDto> ContactRoles { get; init; } =
        Array.Empty<CompassFipsContactRoleDto>();
    public IReadOnlyList<CompassFipsCategorisationGroupDto> CategorisationGroups { get; init; } =
        Array.Empty<CompassFipsCategorisationGroupDto>();
}

/// <summary>Shared shape for <c>{ "data": [ … ] }</c> list endpoints.</summary>
public sealed class CompassFipsListResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public string? ErrorMessage { get; init; }
}

public class CompassFipsLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool Active { get; init; }
}

public sealed class CompassFipsBusinessAreaDto : CompassFipsLookupDto
{
    public int? BusinessAreaLookupId { get; init; }
}

public sealed class CompassFipsUserGroupDto : CompassFipsLookupDto
{
    /// <summary>
    /// Nested groups. The service register currently sends child names; a newer response sends full group objects, including a further level.
    /// </summary>
    [JsonConverter(typeof(CompassUserGroupChildrenConverter))]
    public IReadOnlyList<CompassFipsUserGroupNode> Children { get; init; } = Array.Empty<CompassFipsUserGroupNode>();

    /// <summary>Full nested groups, including a third level, when the service register returns <c>childGroups</c>.</summary>
    public IReadOnlyList<CompassFipsUserGroupNode> ChildGroups { get; init; } = Array.Empty<CompassFipsUserGroupNode>();

    public IReadOnlyList<string> Synonyms { get; init; } = Array.Empty<string>();
}

/// <summary>One user group in the service-register tree. Name-only nodes are direct children from the current API.</summary>
public sealed class CompassFipsUserGroupNode
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool Active { get; init; } = true;

    [JsonConverter(typeof(CompassUserGroupChildrenConverter))]
    public IReadOnlyList<CompassFipsUserGroupNode> Children { get; init; } = Array.Empty<CompassFipsUserGroupNode>();

    public IReadOnlyList<CompassFipsUserGroupNode> ChildGroups { get; init; } = Array.Empty<CompassFipsUserGroupNode>();
}

public sealed class CompassFipsContactRoleDto : CompassFipsLookupDto
{
    public bool AllowMultiple { get; init; }
}

public sealed class CompassFipsCategorisationGroupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool Active { get; init; }
    public IReadOnlyList<CompassFipsCategorisationItemDto> Items { get; init; } =
        Array.Empty<CompassFipsCategorisationItemDto>();
}

public sealed class CompassFipsCategorisationItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool Active { get; init; }
    public int CategorisationGroupId { get; init; }
}

/// <summary>FIPS lookup dimensions from <c>fips/configuration</c> (same bundle as COMPASS <c>/api/products/fips/configuration</c>).</summary>
public enum FipsTaxonomyKind
{
    Channel = 0,
    Type = 1,
    BusinessArea = 2,
    UserGroup = 3,
    ContactRole = 4,

    /// <summary>FIPS categorisation groups with nested items — ids match COMPASS product filter <c>categoryIds</c>.</summary>
    Categorisation = 5
}

public sealed class CompassCategorisationItemRow
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("groupId")]
    public int GroupId { get; init; }

    [JsonPropertyName("groupName")]
    public string? GroupName { get; init; }

    [JsonPropertyName("groupDescription")]
    public string? GroupDescription { get; init; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; init; }

    /// <summary>Which dimension this row belongs to (configuration bundle).</summary>
    public FipsTaxonomyKind TaxonomyKind { get; init; } = FipsTaxonomyKind.Categorisation;

    /// <summary>Orders groups on the Categories page; categorisation uses COMPASS group <c>displayOrder</c>.</summary>
    public int GroupDisplayOrder { get; init; }
}

public sealed class CompassServiceRegisterContact
{
    public string? Role { get; init; }
    public int? RoleId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public bool CanManage { get; init; }
}
