using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Home";
        ViewData["NavSection"] = "home";
        ViewData["NavRegister"] = "";
        return View();
    }

    public IActionResult Error()
    {
        ViewData["NavRegister"] = "";
        ViewData["NavSection"] = "";
        return View();
    }
}
