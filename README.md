# Bhagyashree Pattar Store (BPS) — Marketplace

Bhagyashree Pattar Store (BPS) is a marketplace built as independently deployable .NET 10 services backed by PostgreSQL 18. Prices and checkout totals are denominated in Indian rupees (INR). Catalog owns product data and stock, Identity owns customer credentials and JWT issuance, Orders owns checkout and order history, and Notifications consumes order events into durable notification work items. Each service has its own database. Orders uses Catalog's private inventory contract and validates Identity's signed tokens. SOLID boundaries, repositories, a unit of work, checkout idempotency, a durable saga, expiring reservations, a transactional outbox, an idempotent inbox consumer, Docker, and GitHub Actions are included.

> **Local payment scope:** checkout is authenticated and persists orders in PostgreSQL. Compose defaults to a simulated payment that never charges. For Razorpay test mode, configure `PAYMENT_PROVIDER=Razorpay`, `RAZORPAY_KEY_ID`, and `RAZORPAY_KEY_SECRET` in `.env`. Never use live keys for local development. Development uses a local-only JWT secret and auto-applies EF migrations plus sample data; production must use a secret store and reviewed migrations.

## Run locally

### Fastest: Docker Compose

Requirements: Docker Desktop or Docker Engine with Compose.

```bash
cp .env.example .env
# Edit .env only if you need custom local settings. Keep production secrets out of Git.
docker compose up --build
```

- Storefront: <http://localhost:3000>
- Orders API Swagger: <http://localhost:8080/swagger>
- Catalog API Swagger: <http://localhost:8081/swagger>
- Identity API Swagger: <http://localhost:8082/swagger>
- Notifications worker: background consumer of the `marketly-orders` queue when Service Bus is configured
- Catalog products: <http://localhost:8081/api/products>
- Health: `http://localhost:8080/health`, `http://localhost:8081/health`, and `http://localhost:8082/health`
- PostgreSQL: Orders `localhost:5432`, Catalog `localhost:5433`, Identity `localhost:5434` (database names `marketly`, `marketly_catalog`, `marketly_identity`)

Each API waits for its own PostgreSQL database and applies its own migrations. Catalog seeds sample products. The storefront proxies `/api/catalog`, `/api/identity`, and `/api/orders` to the internal APIs.

### Run the API without Docker

Requirements: .NET 10 SDK and PostgreSQL 18+.

```bash
dotnet restore Marketly.sln
```

Start the four PostgreSQL databases first. Configure ConnectionStrings__Identity, ConnectionStrings__Catalog, ConnectionStrings__Orders, ConnectionStrings__Notifications, the same Jwt__Secret for Identity and Orders, and ServiceAuth__ApiKey for Catalog and Orders. Configure ServiceBus__ConnectionString for Notifications to consume events. In production, use a secret manager and reviewed EF Core migrations before deploying. Run each API/worker in its own terminal:

```bash
dotnet run --project src/Marketly.Identity.Api
```

```bash
dotnet run --project src/Marketly.Catalog.Api
```

```bash
dotnet run --project src/Marketly.Api
```

```bash
dotnet run --project src/Marketly.Notifications.Worker
```

## Try the API

Register an account (password minimum 10 characters):

```bash
curl -X POST http://localhost:8082/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"Sam","email":"sam@example.com","password":"A-strong-demo-pass-123"}'
```

The response contains a bearer token. Use a product ID returned by Catalog and include a delivery address:

```bash
curl http://localhost:8081/api/products?category=Home
curl -X POST http://localhost:8080/api/checkout \
  -H 'Authorization: Bearer YOUR_TOKEN' -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 2c0e8880-0f51-4adf-9e45-b25464e9acb4' \
  -d '{"items":[{"productId":"PRODUCT_GUID","quantity":1}],"deliveryAddress":{"name":"Sam Example","addressLine1":"1 Main Street","addressLine2":"","city":"Bengaluru","region":"Karnataka","postalCode":"560001","countryCode":"IN"}}'
# The checkout response is 202 with sagaId and payment details. For local Simulated mode:
curl -X POST http://localhost:8080/api/checkout/SAGA_GUID/simulate-payment \
  -H 'Authorization: Bearer YOUR_TOKEN'
curl http://localhost:8080/api/orders -H 'Authorization: Bearer YOUR_TOKEN'
```

Catalog supports `search`, `category`, `sort` (`name`, `price-asc`, `price-desc`), `page`, and `pageSize`. Checkout requires an `Idempotency-Key`; the initial response is `202 Accepted` with a payment intent while stock is reserved for 15 minutes. With the local simulated provider, the storefront offers a confirmation step and does not charge. With Razorpay test mode, Checkout.js collects payment details and Orders verifies the signature and captured amount server-side before completing the order. SQL query examples are in [`sql/queries.sql`](sql/queries.sql).

## Architecture

```text
Browser ──> Storefront/Nginx (:3000) ──> Catalog API (:8081) ──> Catalog PostgreSQL
                                ├─────> Identity API (:8082) ──> Identity PostgreSQL
                                └─────> Orders API (:8080) ───> Orders PostgreSQL
                         ├──────────> Catalog inventory API
                         └──────────> transactional outbox ──> Azure Service Bus ──> Notifications worker
```

Catalog, Identity, Orders, and Notifications deploy independently and own separate databases. The three HTTP APIs use MVC controllers; each API `Program.cs` configures its own host, and the Notifications worker has a separate non-HTTP entry point. Orders retains Clean Architecture layers (`Marketly.Api`, `Marketly.Application`, `Marketly.Domain`, `Marketly.Infrastructure`) internally. Checkout uses compensating actions because database transactions cannot span services. See [`docs/architecture.md`](docs/architecture.md) for the payment lifecycle and operational limits.

The services use AutoMapper profiles at API/application boundaries for response DTO transformations. Add mappings to the owning service's profile and keep business rules in use cases and domain code; see [`docs/mapping.md`](docs/mapping.md).

## Project map

```text
index.html, styles.css, app.js       responsive marketplace storefront
src/Marketly.Api/                    Orders API transport/composition root
src/Marketly.Catalog.Api/            Catalog microservice and owned persistence model
src/Marketly.Identity.Api/           Identity microservice, accounts, JWTs, database
src/Marketly.Notifications.Worker/   Service Bus consumer, inbox dedupe, durable notification work queue
src/Marketly.Application/            use cases, DTOs, mapping profiles, interfaces
src/Marketly.Domain/                 commerce entities and relationships
src/Marketly.Infrastructure/         Orders EF Core context/migrations, repositories and adapters
src/Marketly.Catalog.Api/Data/       Catalog context, migrations, persistence entities and seeder
src/Marketly.Catalog.Api/DTOs/      Catalog DTOs
src/Marketly.Catalog.Api/Interfaces/ Catalog repository interface
src/Marketly.Catalog.Api/Mapping/    Catalog AutoMapper profiles
src/Marketly.Identity.Api/Data/      Identity context and migrations
src/Marketly.Identity.Api/Entities/  Identity persistence entities
src/Marketly.Identity.Api/DTOs/     Identity request/response DTOs
src/Marketly.Identity.Api/Mapping/   Identity AutoMapper profile
src/Marketly.Notifications.Worker/Data/ notification context and migrations
docs/architecture.md                 SOLID map, persistence and migration guide
sql/queries.sql                      catalog and commerce reporting examples
Dockerfile                           Orders API container
Dockerfile.catalog                   Catalog API container
Dockerfile.identity                  Identity API container
Dockerfile.notifications             Notifications worker container
docker-compose.yml                   storefront + APIs + worker + independent PostgreSQL databases
.github/workflows/build.yml          restore/build and container build on GitHub
```

## Azure deployment path

1. Create separate Azure Database for PostgreSQL databases for Orders, Catalog, Identity, and Notifications, plus Azure Service Bus and Azure Container Apps (or App Service containers).
2. Build/publish each service image to Azure Container Registry from GitHub Actions.
3. Deploy each service with its own database connection and service-specific secrets. Configure the shared JWT signing/validation settings for Identity and Orders and a private Catalog service credential for Orders-to-Catalog calls. Restrict database network access to the app environment and require TLS.
4. Apply reviewed EF Core migrations as a release step (`Database__ApplyMigrations=false` in all containers). Configure the durable outbox publisher and Inbox-backed Notifications consumer, including dead-letter monitoring.
5. Serve the static storefront from Azure Static Web Apps or Storage static website and configure its API origin/CORS policy for that exact domain.

No Azure resources or credentials are required for local development. Razorpay support is for test configuration until live merchant credentials, webhook reconciliation, refunds/operations, and deployment controls are reviewed and configured.

## License

MIT. Product photos are loaded from Unsplash URLs at runtime; replace them with assets you have rights to distribute for a production catalog.
