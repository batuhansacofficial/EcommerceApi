# Changelog

## 2026-09-26

### Added

- MIRA storefront with customer registration, product details, cart and order history.
- Browser cookie sessions, CSRF protection, server-side session revocation and authentication rate limits.
- Idempotent checkout, atomic stock updates, price confirmation and administrator product version checks.
- Product archival, bounded catalog search and pagination.
- Explicit production migrations, controlled demo seeding, health endpoints and structured logs.
- Combined Docker image, Render configuration and CI checks for the API, storefront and dependencies.

### Changed

- Target .NET 10 with Microsoft packages at 10.0.12 and the Npgsql EF provider at 10.0.3.
- Clear private storefront state when a browser session expires, including during logout.

### Verification

- [CI run](https://github.com/batuhansacofficial/EcommerceApi/actions/runs/36247069922): 13 PostgreSQL scenarios and four browser tests passed, alongside build, lint, isolated backup/restore, health, image and dependency checks.
- HTTPS smoke test: registration, cart quantity changes, simulated checkout, restored session, order history and logout passed.

Operational gaps and demo restrictions are tracked in [known limitations](docs/KNOWN-LIMITATIONS.md).
