using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fips.Web.Controllers;

public sealed class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignIn(string? returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public new IActionResult SignOut()
    {
        var callbackUrl = Url.Action(nameof(SignedOut), "Account");
        return base.SignOut(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult SignedOut()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        ViewData["Title"] = "Signed out";
        ViewData["NavSection"] = "";
        return View();
    }
}
