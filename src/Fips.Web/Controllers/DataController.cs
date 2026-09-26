using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class DataController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Data";
        ViewData["NavSection"] = "data";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Data", null)
        };
        return View();
    }
}
