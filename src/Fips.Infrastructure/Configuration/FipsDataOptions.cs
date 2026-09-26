namespace Fips.Infrastructure.Configuration;

/// <summary>Content for the Data / API documentation area (no secrets).</summary>
public sealed class FipsDataOptions
{
    public const string SectionName = "FipsData";

    /// <summary>COMPASS host URL to show in documentation (no path, no token).</summary>
    public string? CompassApiPublicBaseUrl { get; set; }

    /// <summary>Who to contact and how to request API access. Newlines become paragraphs.</summary>
    public string? ApiAccessNotes { get; set; }
}
