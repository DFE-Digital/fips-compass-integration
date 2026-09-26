using System.Threading.RateLimiting;
using Fips.Infrastructure;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Azure App Service terminates TLS and forwards the original scheme.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

if (builder.Environment.IsDevelopment())
{
    var compass = builder.Configuration.GetSection("CompassApi");
    var baseUrl = compass["BaseUrl"];
    var hasToken = !string.IsNullOrWhiteSpace(compass["ApiToken"]);
    Console.WriteLine(
        $"[Fips] CompassApi: BaseUrl={(string.IsNullOrWhiteSpace(baseUrl) ? "(empty)" : baseUrl)}; ApiToken configured={hasToken}");
}

builder.Services.AddFipsInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllersWithViews();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? httpContext.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/Home/Error");

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://*.googletagmanager.com https://*.google-analytics.com https://*.google.com https://www.clarity.ms https://scripts.clarity.ms https://*.clarity.ms; " +
        "style-src 'self' 'unsafe-inline' https://rsms.me; " +
        "font-src 'self' data: https://rsms.me; " +
        "img-src 'self' data: https://*.googletagmanager.com https://*.google-analytics.com https://*.google.com https://c.bing.com https://*.clarity.ms; " +
        "connect-src 'self' https://*.googletagmanager.com https://*.google-analytics.com https://*.google.com https://www.clarity.ms https://*.clarity.ms https://c.bing.com https://login.microsoftonline.com; " +
        "frame-src https://*.googletagmanager.com https://login.microsoftonline.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://login.microsoftonline.com; " +
        "object-src 'none'";
    await next();
});

app.UseStaticFiles();

// govuk-frontend.min.css requests fonts and images from /assets, not /lib/govuk/assets.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var govukAssetsPath = Path.Combine(webRoot, "lib", "govuk", "assets");
if (Directory.Exists(govukAssetsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(govukAssetsPath),
        RequestPath = "/assets"
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "product-categories-by-unique-id",
    pattern: "product/{id:int}/categories",
    defaults: new { controller = "Products", action = "Categories" });

app.MapControllerRoute(
    name: "product-by-unique-id",
    pattern: "product/{id:int}",
    defaults: new { controller = "Products", action = "Details" });

app.MapControllerRoute(
    name: "product-categories-by-guid",
    pattern: "product/{id:guid}/categories",
    defaults: new { controller = "Products", action = "CategoriesByGuid" });

app.MapControllerRoute(
    name: "product-by-guid",
    pattern: "product/{id:guid}",
    defaults: new { controller = "Products", action = "DetailsByGuid" });

app.MapControllerRoute(
    name: "user-group-child",
    pattern: "categories/user-group/{id:int}/{slug}",
    defaults: new { controller = "Categories", action = "UserGroupChild" });

app.MapControllerRoute(
    name: "user-group",
    pattern: "categories/user-group/{id:int}",
    defaults: new { controller = "Categories", action = "UserGroup" });

app.MapControllerRoute(
    name: "cookies",
    pattern: "cookies",
    defaults: new { controller = "Cookies", action = "Preferences" });

app.MapControllerRoute(
    name: "contact",
    pattern: "contact",
    defaults: new { controller = "Contact", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Razor class library files (DfE CSS, logo, script) are endpoints, so the
// site-wide sign-in rule would otherwise block them.
app.MapStaticAssets().AllowAnonymous();

app.Run();
