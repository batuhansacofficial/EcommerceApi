# Known limitations

Last reviewed: 26 September 2026.

## Demo scope

- Checkout creates a simulated `Pending` order. Payment collection, shipping, tax, invoicing and refunds are not implemented.
- Demo orders reduce stock. Seeding adds missing sample SKUs without resetting stock or order history.
- The current Render database expires on **24 October 2026**. Free web hosting can sleep when idle.

## Storefront and administration

- After a price or stock conflict, checkout returns `409`. The cart does not refresh automatically; reload the page or update an item quantity to retrieve current values.
- The admin product list shares the active catalog page. Independent admin search, pagination and an archived-product list are planned.
- Product images are mapped to SKUs in the frontend. Uploads, categories and variants are not implemented.
- Brand content and theme settings are defined in source files. A store configuration layer is planned.
- Currency is a frontend formatting setting; it is not stored on orders. Do not relabel an existing store's prices or order history as another currency.
- Product and account views use client state rather than shareable routes.
- Dialog keyboard focus management and broader mobile/accessibility coverage need further work.

## Operations

- CI includes a restore rehearsal using disposable data. Live-data backup/restore, rollback and notification delivery have not yet been verified.
- Runtime database permissions are not separated from migration permissions in the supplied deployment configuration.
- Authentication rate limits are process-local. Shared limits are needed before running multiple replicas; proxy/client-IP behavior still needs deployment-level validation.
- Administrator bootstrap is coupled to demo seeding. A separate bootstrap command is planned.
- Browser CI runs against the application in Development. Production Docker/HTTPS smoke tests and a clean Compose installation test are planned.
- GitHub branch rules do not currently require successful CI before merging. The deployment guide requires checking the target commit manually.
- Startup can log a missing `libgssapi_krb5.so.2` probe. The observed deployment connected to PostgreSQL and passed readiness despite this message.

## Next milestones

1. Database continuity, restore and alert verification, and required CI checks.
2. Repeatable installation, independent administrator setup and restricted runtime database permissions.
3. Admin catalog management, recoverable cart/network errors and accessible dialogs.
4. Store configuration, product asset metadata and a second store example.
5. Production-image verification and a versioned distribution package.
