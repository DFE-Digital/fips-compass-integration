using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class ContactController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Contact";
        ViewData["NavSection"] = "";
        ViewData["Breadcrumbs"] = new List<(string Text, string? Href)>
        {
            ("Home", Url.Action("Index", "Home")),
            ("Contact", null)
        };
        return View();
    }
}
