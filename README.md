# DepotFlow

Container depot management system: gate in and gate out, yard slots, storage billing, reports.
Built with ASP.NET Core (.NET 10), EF Core and SQL Server 2022.

Status: Day 1 (foundation and gate operations) is done. Yard slots, billing and reports come next.

## What works today

- JWT login with four roles: `Admin`, `GateClerk`, `YardPlanner`, `BillingOfficer`
- Shipping lines: list, get, create, update
- Container number validation (ISO 6346 check digit)
- Gate in and gate out, with one active visit per container enforced by a database index, even under concurrent requests
- Container lookup with visit history

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
