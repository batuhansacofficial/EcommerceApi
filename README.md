# EcommerceApi · MIRA storefront

.NET 10 / PostgreSQL API with an original React + Bootstrap 5 storefront. Includes registration, secure browser sessions, catalog search, cart, simulated checkout, order history and administrator product management.

**Checkout does not collect payment.** Orders are simulated `Pending` orders. Shipping, tax, refunds and payment webhooks require a separate implementation before real trading.

## Local Docker development

Requires Docker Engine / Docker Desktop with Linux containers.

1. Copy `.env.example` to `.env`.
2. Set a random database password and a random JWT key with at least 32 characters. Optional administrator bootstrap password must have at least 16 characters.
3. Run `docker compose up --build`.
4. Open `http://localhost:3000`. Register an account with a password of at least 12 characters.

Development applies migrations and inserts four sample SKUs. Repeated startup preserves existing records and stock. Database and local API ports bind only to loopback. The web container proxies `/api` to the API.

## Production / Render

The root `Dockerfile` compiles both applications and serves them from ASP.NET Core on one origin. `render.yaml` describes a free demo web service and PostgreSQL 18 in Frankfurt. The free database expires after 30 days. The production app never automatically runs migrations or creates an administrator at normal startup.

Read [the deployment and operations runbook](docs/OPERATIONS-TR.md) for the separate migration step, secrets, database network rules, backups and verification. Deploy only a commit with successful CI.

## Verification

```sh
dotnet restore
dotnet build -c Release --no-restore --warnaserror
# Requires an isolated PostgreSQL database whose name ends in _test.
TEST_DATABASE='Host=localhost;Database=mira_test;...' dotnet run --project tests/EcommerceApi.IntegrationTests -c Release --no-build
cd src/EcommerceApi.Web
pnpm install --frozen-lockfile
pnpm run lint
pnpm run build
```

GitHub Actions additionally runs real API browser tests, dependency audits and a Docker image build. The integration scenario runner exits nonzero on failure. It covers concurrency, idempotency, transactional rollback, authorization, product archival, session revocation, CSRF, seeding, pagination and rate limits.

## API changes

- Browser session endpoints under `/api/auth/session/` use HttpOnly cookies and CSRF; no JWT is stored in localStorage. Existing `/api/auth/login` and `/api/auth/register` retain bearer responses for API clients.
- Checkout requires a UUID `Idempotency-Key` header and JSON `{ "expectedTotal": 120 }`. Reuse the key for retries of the same intent.
- Product PUT requires the `version` from the last GET. Stale updates return `409`.
- Product listing supports `page`, `pageSize` (1–100), `sort` and bounded literal text search; total is in `X-Total-Count`.
- Product DELETE archives the product and keeps order snapshots.

See [OPERATIONS-TR.md](docs/OPERATIONS-TR.md) for all compatibility and security details.

## Template assets

The storefront is an original implementation inspired by the general ecommerce package format. It does not contain Evara source files. Images were generated for this package; generation prompts are in [ASSET-PROMPTS.md](ASSET-PROMPTS.md). Currency defaults to USD and can be configured at frontend build time. Product image mapping currently uses SKU values in `src/EcommerceApi.Web/src/catalog.ts`.
