# DepotFlow

Container depot management system: gate in and gate out, yard slots, storage billing, reports.
Built with ASP.NET Core (.NET 10), EF Core and SQL Server 2022.

Status: Days 1 and 2 are done (gate operations, yard, billing, invoices, audit log). Reports, performance work and the UI come next.

## What works today

- JWT login with four roles: `Admin`, `GateClerk`, `YardPlanner`, `BillingOfficer`
- Shipping lines, and container number validation (ISO 6346 check digit)
- **Gate in**: needs an active tariff, then assigns the first free yard slot that obeys the stacking rule
- **Yard**: slot listing with occupant, occupancy per block, relocation of a container to another slot
- **Tariffs**: tiered or flat daily rates per shipping line and container size, with free days; a new tariff replaces the old one
- **Gate out**: in one transaction it releases the visit, frees the slot and issues the invoice
- **Invoices**: charge preview before leaving, invoice with lines, pay; amounts are copied onto the invoice so later tariff changes cannot alter it
- **Audit log**: every insert, update and delete of the business data, with who and what changed; append-only
- Search: visits (status, line, container prefix, dates, sort) and containers (prefix, size, in yard)
- One active visit per container, and one active visit per slot, enforced by database indexes even under concurrent requests

Who may call what is declared with `[Authorize(Roles = ...)]` on each controller action, and `AuthorizationMatrixTests` checks every endpoint against every role (and against a missing token).

## Simplifications to know about

- **Stacking rule**: a container can go on tier N above 1 only when the slot below it is occupied. This is checked when a container is placed or relocated. Removing a lower container while others sit on top is allowed; the system does not model re-handling moves.
- **Dwell days** count calendar days in Asia/Dhaka time, counting both the gate-in day and the gate-out day (gate in 1 Oct 23:50, out 2 Oct 00:10 is 2 days).
- **Invoice numbers** (`INV-2026-0000123`) come from a SQL sequence; the year part is the database server's UTC year.
- **Tariffs** are never edited: to change prices, create a new tariff (the old one is deactivated). A tariff can only be switched off through `PUT /api/v1/tariffs/{id}`.

## Prerequisites

- .NET 10 SDK
- Docker Desktop (on Apple Silicon, enable "Use Rosetta for x86_64/amd64 emulation" so SQL Server can run)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

## Run it

```bash
# 1. Database password (any strong value; .env is git-ignored)
cp .env.example .env

# 2. Start SQL Server
docker compose up -d db

# 3. Local secrets for the API (stored outside the repo, in your user profile)
set -a && . ./.env && set +a
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=DepotFlow;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True" --project src/DepotFlow.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project src/DepotFlow.Api

# 4. Run the API (applies migrations and seeds data on first start in Development)
dotnet run --project src/DepotFlow.Api --launch-profile http
```

Then open <http://localhost:5141/swagger>, call `POST /api/v1/auth/login`, and paste the returned `accessToken`
into the Authorize button. `GET /health` returns 200.

In Development the first start also seeds a demo tariff for every shipping line and both sizes (switch with `Seed:DemoTariffs`), and a 4 x 10 x 10 x 4 yard of 1,600 slots (`Yard` section).

Seeded users (demo only, password `Demo#DepotFlow1`):

| Email | Role |
|---|---|
| admin@depotflow.local | Admin |
| gate@depotflow.local | GateClerk |
| yard@depotflow.local | YardPlanner |
| billing@depotflow.local | BillingOfficer |

## Try a gate-in

```bash
TOKEN=$(curl -s -X POST localhost:5141/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"gate@depotflow.local","password":"Demo#DepotFlow1"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['accessToken'])")

curl -X POST localhost:5141/api/v1/visits/gate-in -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"containerNumber":"CSQU3054383","sizeFeet":20,"shippingLineId":3,"truckNumber":"DHK-TA-11-2345","sealNumber":"SL889201"}'
```

## Tests

```bash
dotnet test
```

147 tests: unit tests for the billing, stacking and container-number rules, and integration tests for every journey.
The integration tests start their own SQL Server in a container (Testcontainers), so Docker must be running.
They use a separate database and never touch the development one.

## Layout

```
src/DepotFlow.Domain          entities, value objects, rules (no framework dependencies)
src/DepotFlow.Application     use cases, DTOs, interfaces
src/DepotFlow.Infrastructure  EF Core, migrations, Identity, JWT
src/DepotFlow.Api             controllers, auth setup, Swagger
tests/DepotFlow.Domain.Tests  fast unit tests
tests/DepotFlow.Api.Tests     integration tests against real SQL Server
```
