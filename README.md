# Find information about products and services

Find information about products and services (FIPS) is a read-only .NET web catalogue of DfE products and services. Staff use it to search and browse what exists, who owns it, and how each entry is categorised. The site does not create, edit, or retire entries.

## How the parts fit together

- **This website** reads the COMPASS Service Register API and presents the register. It does not call ServiceNow.
- **COMPASS** holds the register. Manage an entry, take an Excel export, or use the Service Register API there — not in this site.
- **ServiceNow** is where a missing product or service is created as a service offering. COMPASS holds that record; this site then shows it.
- **Microsoft Entra** signs the user in before any page is shown. Sign-in and the signed-out page are the only anonymous views.

Phase is not a COMPASS API filter. This site applies phase after products load. Phase also appears as a browse group on `/Categories`, with values in lifecycle order. Other category groups and their items are alphabetical.

Further behaviour is described in [docs/spec.md](docs/spec.md).

## What’s in this repo

Open `Fips.slnx`.

- `src/Fips.Data` — Service Register models (products, categories, contacts, taxonomy)
- `src/Fips.Services` — COMPASS catalogue contract and helpers
- `src/Fips.Infrastructure` — COMPASS HTTP client, options, and in-memory response cache
- `src/Fips.Web` — MVC site (views, CSS, JavaScript)
- `FIPS/dfe-frontend` — DfE frontend ASP.NET Core package (sibling checkout; see below)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`)
- This repository checked out next to `FIPS/dfe-frontend`, because `Fips.Web` references it:

```text
code-digital-ops/
  fips-compass-integration/
  FIPS/dfe-frontend/
```

- A COMPASS API token with `read` on the `ServiceRegister` resource
- An Entra app registration for this site (`AzureAd` tenant, client id, and client secret). The redirect URI must include `https://<this-site>/signin-oidc` (and the matching local URI when developing)

## Run locally

Set secrets with user secrets (preferred). Do not commit tokens or client secrets. `appsettings*.json` files are gitignored.

```bash
dotnet user-secrets set "CompassApi:BaseUrl" "https://compass.education.gov.uk" --project src/Fips.Web
dotnet user-secrets set "CompassApi:ApiToken" "<token>" --project src/Fips.Web
dotnet user-secrets set "AzureAd:ClientSecret" "<secret>" --project src/Fips.Web
```

You can get details of these from the COMPASS developers site.

The same keys can be set as environment variables (`CompassApi__BaseUrl`, `CompassApi__ApiToken`, `AzureAd__ClientSecret`) or in a local `src/Fips.Web/appsettings.Development.json`.

Then:

```bash
dotnet run --project src/Fips.Web
```

Development listens on [http://localhost:5277](http://localhost:5277). The Test and Production launch profiles use ports 5278 and 5279.

## Configuration

| Setting | Purpose |
| --- | --- |
| `AzureAd:Instance` | Entra authority host, usually `https://login.microsoftonline.com/` |
| `AzureAd:TenantId` | Entra tenant id |
| `AzureAd:ClientId` | Application (client) id |
| `AzureAd:ClientSecret` | Client secret for the web app |
| `AzureAd:CallbackPath` | OIDC callback path, `/signin-oidc` |
| `AzureAd:Domain` | Entra domain (optional display / config) |
| `CompassApi:BaseUrl` | COMPASS host, with no path |
| `CompassApi:ApiToken` | Bearer token for the Service Register API |
| `CompassApi:CatalogCacheSeconds` | In-memory cache lifetime for successful catalogue reads. `0` turns caching off. Default is `120` |
| `FipsData:CompassApiPublicBaseUrl` | COMPASS host shown on the Data / docs pages (no token) |
| `FipsData:ApiAccessNotes` | Optional notes shown on the Data page about requesting API access |

Per-environment cache values in the appsettings templates: Development and Test use `60`; Production uses `300`.

In Azure App Service, application settings override the files.

## Data and caching

Successful COMPASS GETs are cached in `IMemoryCache` for `CompassApi:CatalogCacheSeconds`. Each running instance has its own cache, so a change in COMPASS can take that long to appear here.

## Build and test

```bash
dotnet restore src/Fips.Web/Fips.Web.csproj
dotnet build src/Fips.Web/Fips.Web.csproj --configuration Release
```

There is no test project in this solution yet.

## Deployment

GitHub Actions build and publish `src/Fips.Web`, then deploy the artifact to Azure App Service. Both workflows also check out `DFE-Digital/dfefrontend` beside this repo so the DfE frontend project reference resolves.

- [`.github/workflows/dev-build-deploy.yml`](.github/workflows/dev-build-deploy.yml) — on push to `dev` (or manual run): deploy to Azure Web App `fips-frontend` using secret `AZURE_FIPS_FRONTEND`
- [`.github/workflows/prod-build-deploy.yml`](.github/workflows/prod-build-deploy.yml) — on push to `main` (or manual run): deploy to Azure Web App `find-products-services-test` using secret `AZURE_FIPS_PROD`

## Licence

Licensed under the [MIT Licence](LICENCE).
