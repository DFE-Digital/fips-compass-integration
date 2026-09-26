using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class AboutController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "About this product";
        ViewData["NavSection"] = "";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("About this product", null)
        };
        return View();
    }
}
