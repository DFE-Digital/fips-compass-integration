# FIPS catalogue (COMPASS)

Public catalogue for Department for Education products and services. Product, service, and category data comes from the COMPASS Service Register API. There is no CMS.

## Solution

.NET 10. Projects:

| Project | Role |
| --- | --- |
| `src/Fips.Data` | Service Register models: products, categories, channels, types, contacts, taxonomy |
| `src/Fips.Services` | Catalogue contract, completeness stats, Excel export |
| `src/Fips.Infrastructure` | COMPASS HTTP client, options, in-memory response cache |
| `src/Fips.Web` | MVC site, views, CSS, and JavaScript |

## COMPASS

Bearer token in `CompassApi:ApiToken`. Base URL in `CompassApi:BaseUrl`.

| Environment | Settings file | COMPASS host |
| --- | --- | --- |
| Development | `appsettings.Development.json` | `https://compass.education.gov.uk` |
| Test | `appsettings.Test.json` | COMPASS test app service |
| Production | `appsettings.Production.json` | `https://compass.education.gov.uk` |

Reads:

- `GET /api/v1/ServiceRegister/products` — active products (`status=Active`, enterprise excluded on the standard register)
- `GET /api/v1/ServiceRegister/products/enterprise-active` — enterprise services
- `GET /api/v1/ServiceRegister/products/{id}` — one product
- `GET /api/v1/ServiceRegister/fips/configuration` — channels, types, business areas, user groups, contact roles, categorisation groups

The token needs `read` on resource `ServiceRegister`.

## Routes

| URL | Page |
| --- | --- |
| `/` | Home |
| `/Products` | Product and service register, search and filters |
| `/Products/Details/{guid}` | Product |
| `/product/{numericId}` | Resolve FIPS id, then product |
| `/Categories` | Taxonomy groups |
| `/Categories/Group` | Items in one group |

## Out of scope

CMS content pages, admin editing, Airtable feedback, and service assessments are not part of this app. Propose-a-change and new-entry flows that wrote to the CMS are not carried over. Named contacts can still open the COMPASS manage screen for a product.
