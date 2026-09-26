namespace Fips.Infrastructure.Configuration;

public sealed class CompassApiOptions
{
    public const string SectionName = "CompassApi";

    /// <summary>COMPASS app base URL (no path), e.g. https://compass.education.gov.uk</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Bearer API token with Service Register read.</summary>
    public string? ApiToken { get; set; }

    /// <summary>In-memory cache TTL for successful Service Register reads. 0 disables caching.</summary>
    public int CatalogCacheSeconds { get; set; } = 120;
}
