using System.Text.Json;
using Fips.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class CookiesController : Controller
{
    public const string PreferenceCookieName = "cookie-preferences";

    [HttpGet]
    public IActionResult Preferences()
    {
        SetPageData();
        return View(new CookiePreferencesViewModel
        {
            AnalyticsAccepted = ReadAnalyticsAccepted()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Preferences(CookiePreferencesViewModel model)
    {
        WritePreference(model.AnalyticsAccepted);
        TempData["SuccessMessage"] = "Your cookie preferences have been saved.";
        return RedirectToAction(nameof(Preferences));
    }

    private void SetPageData()
    {
        ViewData["Title"] = "Cookies";
        ViewData["NavSection"] = "";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Cookies", null)
        };
    }

    private bool ReadAnalyticsAccepted()
    {
        if (!Request.Cookies.TryGetValue(PreferenceCookieName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            var preferences = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            return preferences is not null
                && preferences.TryGetValue("analytics", out var analytics)
                && string.Equals(analytics, "on", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void WritePreference(bool analyticsAccepted)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["analytics"] = analyticsAccepted ? "on" : "off"
        });

        Response.Cookies.Append(PreferenceCookieName, json, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true
        });
    }
}
