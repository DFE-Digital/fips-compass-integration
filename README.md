# FIPS catalogue (COMPASS)

Public catalogue of Department for Education products and services. Product, service, and category data is read from the COMPASS Service Register API. There is no CMS.

Licensed under the [MIT Licence](LICENCE).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`)
- This repository checked out next to the `FIPS` repository, because `Fips.Web` references `FIPS/dfe-frontend`:

```text
code-digital-ops/
  fips-compass-integration/
  FIPS/dfe-frontend/
```

- A COMPASS API token with `read` on the `ServiceRegister` resource

## Configuration

App settings files are gitignored. Do not commit API tokens.

Set the COMPASS base URL and token with user secrets:

```bash
dotnet user-secrets set "CompassApi:BaseUrl" "https://compass.education.gov.uk" --project src/Fips.Web
dotnet user-secrets set "CompassApi:ApiToken" "<token>" --project src/Fips.Web
```

The same keys can be set as environment variables (`CompassApi__BaseUrl`, `CompassApi__ApiToken`) or in a local `src/Fips.Web/appsettings.Development.json` file, which stays out of source control.

| Setting | Purpose |
| --- | --- |
| `CompassApi:BaseUrl` | COMPASS host, with no path |
| `CompassApi:ApiToken` | Bearer token for the Service Register API |
| `CompassApi:CatalogCacheSeconds` | In-memory cache lifetime for successful reads. `0` turns caching off. Default is 120. |

## Run

```bash
dotnet run --project src/Fips.Web
```

Development listens on http://localhost:5277. The Test and Production launch profiles use ports 5278 and 5279.

## Solution

Open `Fips.slnx`.

| Project | Role |
| --- | --- |
| `src/Fips.Data` | Service Register models: products, categories, channels, types, contacts, taxonomy |
| `src/Fips.Services` | Catalogue contract, completeness stats, Excel export |
| `src/Fips.Infrastructure` | COMPASS HTTP client, options, in-memory response cache |
| `src/Fips.Web` | MVC site, views, CSS, and JavaScript |

Further behaviour is described in [docs/spec.md](docs/spec.md).

## Routes

| URL | Page |
| --- | --- |
| `/` | Home |
| `/Products` | Product and service register, with search and filters |
| `/product/{id}` | Product, by numeric id or GUID |
| `/Categories` | Taxonomy groups |
| `/categories/user-group/{id}` | User group |
| `/contact` | Contact |
| `/cookies` | Cookie preferences |
