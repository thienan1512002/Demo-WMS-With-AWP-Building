# Current architecture survey

Survey date: 2026-10-05. This is the source-backed baseline for subsequent warehouse
work, not a proposal to replace the existing architecture. No runtime behavior was
changed. The worktree write probe was created and removed successfully.

## Stack and dependency boundaries

`DeliveryDemo.sln` contains four runtime projects and one test project. All target
`net10.0`; `Directory.Build.props` enables nullable references, implicit usings and
warnings as errors. The actual composition and implementation are:

| Layer | Source evidence and responsibility |
| --- | --- |
| Domain | `src/DeliveryDemo.Domain/Warehouse/Models.cs`: persistence-independent mutable models `Goods`, `Warehouse`, `StockTransaction`, `InventoryBalance`; receipt/issue enum. No project dependencies. |
| Application | `src/DeliveryDemo.Application/Warehouse/IMasterCatalog.cs` and `IStockPosting.cs`: async contracts, catalog DTO records, shared input validation and conflict exception; references Domain. |
| Infrastructure | `WarehouseDbContext.cs`, `MasterCatalog.cs`, `StockPosting.cs`, `WarehouseServices.cs` under `src/DeliveryDemo.Infrastructure/Warehouse`: EF Core/Npgsql persistence, scoped services, mappings and migration SQL; references Application. EF Design 10.0.9 and Npgsql EF provider 10.0.2 are declared here. |
| API | `src/DeliveryDemo.Api/Program.cs` and `MasterEndpoints.cs`: ASP.NET Core Minimal API, dependency injection, CORS, exception handling and ProblemDetails; references Application and Infrastructure. |
| Frontend | `frontend/src/main.tsx`, `App.tsx`, `BackendStatus.tsx`: React 19, TypeScript 6 and Vite 8, separate build from the .NET solution. No router, shared API client, form library or state store is present. |

`Program.cs` registers persistence only when the externally supplied Warehouse
connection string exists. `AddWarehousePersistence` uses `UseNpgsql` and scoped
`WarehouseDbContext`, `IMasterCatalog` and `IStockPosting`. Startup does not migrate.
No generic repository, mediator or separate unit-of-work abstraction exists;
services use the scoped EF context directly. Reuse these boundaries rather than
introducing a parallel persistence stack.

## Naming, HTTP and UI conventions

`.editorconfig` specifies UTF-8, LF, final newline, spaces, four-space C# and
two-space TS/TSX/JSON/CSS indentation. C# uses feature folders/namespaces ending in
`Warehouse`, PascalCase types/members, `I`-prefixed contracts, `Async` methods and
cancellation tokens. DTOs are records. Database tables/columns use snake_case;
the migration names keys/indexes with `pk_`, `fk_`, `ix_`, checks with `ck_`.
IDs are UUIDs, dates UTC PostgreSQL `timestamp with time zone`, quantities
`numeric(20,6)`. TS components use PascalCase files and functions, hooks, relative
imports and plain CSS classes. Existing TS code uses single quotes without
semicolons; tsconfig checks unused locals/parameters but does not enable `strict`.

`MasterEndpoints.MapMasterEndpoints` shares route registration between
`/api/goods` and `/api/warehouses`. GET list/detail, POST, PUT and DELETE use
Application DTOs, camelCase JSON, validation dictionaries and an endpoint filter
for missing persistence (503) and catalog conflicts (409). Create returns 201 with
Location, read/update 200, archive 204, missing IDs 404, invalid input
ValidationProblem 400. Lists return `{ items, total, page, pageSize }`, default
page 1/size 20, size cap 100, ordered by code then ID, with search and archived
filters. Codes are trimmed, case-sensitive, immutable and reserved after archive;
DELETE archives rather than deleting. See [warehouse architecture](warehouse-architecture.md)
for the full contract.

`App.tsx` is a Vietnamese workspace shell: topbar/brand, connection indicator,
three numbered module cards and footer. `App.css` uses white cards, warm neutral
background, indigo accents and a three-column grid collapsing below 700px.
Semantic headings and `aria-labelledby` are present; `BackendStatus` uses
`role="status"`. Reuse the shell and its responsive spacing, but no reusable
table, editor, modal, pagination or catalog page exists yet. `BackendStatus` shows
the existing direct-fetch pattern with loading/ready/offline state and abort on
cleanup. Vite proxies `/api` to port 5000; production routing requires the same
origin. A healthy response means application readiness, not database readiness.

## Reusable warehouse foundations

- Use `IMasterCatalog`, `MasterInput`, `MasterDetail`, `MasterPage` and
  `MasterValidation` for goods/warehouse management. Infrastructure already
  translates PostgreSQL duplicate-key errors into catalog conflicts and reloads
  created entities to return persisted timestamp precision.
- Use `IStockPosting` for receipts/issues; do not write balances independently.
  The adapter validates type, positive quantity, six-decimal precision, range,
  UTC occurrence time and actor, then inserts the ledger entry. Failed inserts
  are detached from the context.
- `InventoryBalance` is keyed by goods/warehouse; `StockTransaction` is immutable
  history. `Migrations/WarehouseSql.cs` locks active masters and atomically updates
  balances in a PostgreSQL trigger. Insufficient stock rolls back the insertion;
  guards reject ledger mutations, direct balance changes and master deletion.
  Reuse this mechanism for concurrency rather than process-local locks.
- Reuse `WebApplicationFactory<Program>` and the injected `IMasterCatalog` double
  in `MasterApiTests.cs` for HTTP contracts. Real provider coverage belongs in the
  existing disposable PostgreSQL test, not an EF in-memory substitute.

## Build, test, lint and migration commands

Run backend commands from the repository root; frontend commands from `frontend`.
Windows commands use `npm.cmd` to avoid PowerShell script policy issues.

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes --no-restore

cd frontend
npm.cmd ci
npm.cmd run build
npm.cmd run test
npm.cmd run lint
```

The package scripts independently confirm build = `tsc -b && vite build`, test =
`vitest run`, lint = `oxlint`. `vitest.config.ts` supplies React, jsdom and
`test/setup.ts` with jest-dom. Three `BackendStatus.test.tsx` tests cover healthy,
unavailable and unexpected responses. Backend uses xUnit 2, Test SDK,
WebApplicationFactory and coverlet; `HealthTests`, `MasterApiTests` and
`WarehouseTests` cover HTTP contracts, boundary validation, model/snapshot
consistency and generated forward/rollback SQL.

`dotnet-tools.json` at the root pins dotnet-ef 10.0.9 (not under `.config`).
`WarehouseDbContextFactory` supports design-time SQL generation without secrets.
The committed InitialWarehouse migration includes a designer, snapshot and
explicit trigger Up/Down SQL. From the root:

```powershell
dotnet tool restore
dotnet ef migrations list --project src/DeliveryDemo.Infrastructure
dotnet ef migrations script --project src/DeliveryDemo.Infrastructure --output warehouse-up.sql
dotnet ef migrations script InitialWarehouse 0 --project src/DeliveryDemo.Infrastructure --output warehouse-down.sql
```

These commands need a successful backend build. Application to a separately
authorized development database uses `dotnet ef database update --project
src/DeliveryDemo.Infrastructure --connection $env:WAREHOUSE_DEV_CONNECTION`.
Rollback to 0 drops warehouse data. No database update was executed in this survey.
Reuse the migration/snapshot convention for schema changes, including reversible
trigger SQL; use a separate migration role from the restricted runtime role.

The opt-in test in `WarehouseTests.cs` creates a randomly named
`postgres:17-alpine` Docker container with generated credentials and removes it in
`finally`; it never uses an external database connection string. It exercises
up/down/up, real catalog operations, concurrent issues, rollback and constraints.
AWP's external-service verifier should run:

```powershell
$env:WAREHOUSE_POSTGRES_TESTS = '1'
dotnet test --no-build --logger "trx;LogFileName=warehouse-postgres.trx" --results-directory TestResults
Remove-Item Env:WAREHOUSE_POSTGRES_TESTS
```

## Fresh verification evidence and limits

This survey executed the following checks, independently of historical claims in
the warehouse document:

| Command | Observed result |
| --- | --- |
| `dotnet --version` / `npm.cmd --version` | 10.0.401 / 11.19.0. |
| `npm.cmd ci --offline --ignore-scripts` | Succeeded from available cache; 106 packages installed. This does not establish a fresh online security audit. |
| `npm.cmd run build` | Passed TypeScript and Vite production build. |
| `npm.cmd run test` | One file, three tests passed. |
| `npm.cmd run lint` | Passed, exit 0. |
| `dotnet restore --ignore-failed-sources` | Failed NU1900: NuGet vulnerability service unavailable, treated as error. |
| `dotnet restore --ignore-failed-sources -p:NuGetAudit=false` | Succeeded; audit disabled for this invocation only, no repository setting changed. |
| `dotnet build --no-restore` | Failed NETSDK1064: missing Microsoft.CodeAnalysis.Analyzers 3.11.0. Domain and Application built, full solution did not. |
| `dotnet format --verify-no-changes --no-restore` | Completed with exit 0; does not establish a successful compilation. |
| `dotnet tool restore` / `dotnet ef --version` | Passed; EF CLI 10.0.9 available. |

Backend test execution and migration script generation remain unverified because
the prerequisite solution build failed. The disposable provider suite was not
run in this sandbox; no PostgreSQL/Docker success is claimed. AWP should restore
with network/audit enabled, build and run both normal and opt-in suites. Existing
tests were inspected and frontend tests executed; a documentation-only survey
does not add behavior tests or alter the existing suites.

## Decisions needed before further implementation

1. Define authentication, permissions and warehouse access scope. `Program.cs`
   has no authentication/authorization registration or middleware, and catalog
   routes have no authorization policy. CORS is not access control. Decide how
   authenticated identity supplies `Actor` before adding posting HTTP endpoints.
2. Define posting/query HTTP contracts: no receipt/issue endpoint or inventory/
   history/report query contract exists. Decide idempotency/retry policy,
   multi-line transactions and business mapping of PostgreSQL 23514 (currently
   exposed as DbUpdateException by the posting adapter).
3. Confirm scope for units, transfers, adjustments and reports: current models
   represent only goods, warehouses and receipt/issue quantities, with no unit,
   tenant, document or transfer model. Extend only for approved requirements.
4. Agree catalog UI interaction conventions (navigation, forms, validation,
   archive confirmation, list filters and pagination). The card stating that
   catalog is unimplemented describes UI status; catalog backend already exists.
   Do not infer absence of backend from that placeholder or the earlier survey
   sentence about empty business layers in `warehouse-architecture.md`.
5. Establish production migration ownership, backup/restore procedure and
   deployment routing. Source provides EF tooling, not an automated deployment
   pipeline; no CI YAML or Docker orchestration was found in the surveyed files.
   Resolve cached analyzer availability for reproducible backend verification.

This baseline retains the implemented layering, catalog rules and trigger-backed
posting design. New authentication, UI infrastructure and business capabilities
above are open decisions, not architecture approved by this survey.
