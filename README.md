# EcommerceApi

An ASP.NET Core 10 API and React storefront backed by PostgreSQL. The MIRA demo includes a product catalog, customer accounts, shopping cart, order history and product administration.

[Live demo](https://ecommerce-mira.onrender.com) · [CI](https://github.com/batuhansacofficial/EcommerceApi/actions/workflows/verify.yml) · [Deployment guide](docs/OPERATIONS-TR.md) · [Changelog](CHANGELOG.md)

Checkout creates a simulated `Pending` order and reduces demo stock. It does not collect payment or calculate shipping and tax.

## Features

- Registration and login with server-validated browser sessions, HttpOnly cookies and CSRF protection.
- JWT authentication for API clients.
- Searchable, paginated product catalog with product details.
- Persistent shopping cart and order history.
- Idempotent checkout, transactional stock updates and price confirmation.
- Administrator product creation, updates and archival with version checks.
- PostgreSQL integration tests, browser tests and container/dependency checks in CI.

## Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10, Entity Framework Core 10, Npgsql |
| Database | PostgreSQL 18 |
| Storefront | React 19, TypeScript, Vite, Bootstrap 5 |
| Tests | PostgreSQL integration scenarios, Playwright |
| Deployment | Docker, Docker Compose, Render |

## Run locally

Requires Docker with Linux containers.

1. Copy `.env.example` to `.env`.
2. Set `ECOMMERCE_DB_PASSWORD` and a random `ECOMMERCE_JWT_SECRET` of at least 32 characters.
3. Optionally set `ECOMMERCE_ADMIN_EMAIL` and `ECOMMERCE_ADMIN_PASSWORD` to create a local administrator. The password must contain at least 16 characters.
4. Start the services:

```sh
docker compose up --build
```

Open [localhost:3000](http://localhost:3000). Register a customer account with a password of at least 12 characters. The API listens on `localhost:5211`.

Development startup applies migrations and adds four sample products. Repeated startup preserves existing products and stock. Local service ports bind to loopback, and the web container proxies `/api` to the API.

## Repository layout

```text
src/EcommerceApi.Api/                API, database mappings and migrations
src/EcommerceApi.Web/                Storefront and browser tests
tests/EcommerceApi.IntegrationTests/ PostgreSQL integration scenarios
docs/                               Operations, assets and known limitations
Dockerfile                          Combined production image
docker-compose.yml                  Local development services
render.yaml                         Demo infrastructure
```

## Development checks

For local builds outside Docker, use the .NET SDK specified in `global.json`, Node.js 24 and pnpm 11.19.0.

```sh
dotnet restore
dotnet build -c Release --no-restore --warnaserror
cd src/EcommerceApi.Web
pnpm install --frozen-lockfile
pnpm run lint
pnpm run build
```

Integration tests require a disposable PostgreSQL database whose name ends in `_test`. From the repository root, with `TEST_DATABASE` set to its connection string:

```sh
dotnet run --project tests/EcommerceApi.IntegrationTests -c Release --no-build
```

The [verification workflow](.github/workflows/verify.yml) runs 13 PostgreSQL scenarios and four browser tests, including checkout concurrency, authorization, session revocation, mobile catalog and expired-session handling. It also checks backup/restore on isolated test data, health responses during database failure, the Docker image and dependency vulnerabilities.

## API conventions

| Operation | Contract |
|---|---|
| Browser authentication | `/api/auth/session/*`; cookies and `X-CSRF-TOKEN` for writes |
| API authentication | `/api/auth/login` and `/api/auth/register`; bearer JWT responses |
| Checkout | UUID `Idempotency-Key` header and JSON `{ "expectedTotal": 120 }`; reuse the key when retrying the same checkout |
| Product updates | Include the last retrieved `version`; stale updates return `409` |
| Catalog | `page`, `pageSize` (1–100), `sort` and search; total in `X-Total-Count` |
| Product deletion | Archives the product and preserves order snapshots |

The OpenAPI document is available at `/openapi/v1.json` in Development. Production browser requests use the same origin as the storefront.

## Deployment

The root Dockerfile builds the storefront and API into one image. ASP.NET Core serves both on port 8080. Production migrations run as an explicit deployment step.

See the [deployment and operations guide](docs/OPERATIONS-TR.md) for configuration, migrations, secrets, health checks and recovery procedures. The current demo database expires on **24 October 2026**; the free web service can sleep when idle.

## Documentation

- [Known limitations and planned work](docs/KNOWN-LIMITATIONS.md)
- [Storefront assets and customization](docs/ASSETS.md)
- [Deployment and operations — Türkçe](docs/OPERATIONS-TR.md)
- [Changelog](CHANGELOG.md)

## License

[MIT](LICENSE.txt)
