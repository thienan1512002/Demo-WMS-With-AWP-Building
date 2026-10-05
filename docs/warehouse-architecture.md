# Warehouse persistence architecture

## Approved decision and layering

TASK-001 surveyed the empty business layers. The task owner authorized EF Core 10
with PostgreSQL/Npgsql in this task. Domain contains persistence-independent
PascalCase models; Application owns IStockPosting; Infrastructure implements the
DbContext, mappings, migrations and posting adapter. API registers persistence only
when ConnectionStrings__Warehouse is supplied externally. Startup never migrates.
No connection strings or credentials are stored in source.

Use snake_case database tables, columns, keys and indexes, UUID identifiers and UTC
DateTime values mapped to PostgreSQL timestamp with time zone. CreatedAt records
creation, OccurredAt records the business event, Actor identifies its performer.
The posting adapter requires UTC occurrence time; PostgreSQL generates posting
creation and balance update audit times. Goods/warehouse CreatedAt and ArchivedAt
are UTC values supplied by the caller. Archive master data with ArchivedAt;
physical deletion is rejected, and historical references remain accessible.

## Master catalog API

`/api/goods` and `/api/warehouses` expose GET list/detail, POST, PUT and
DELETE through Minimal API routes. Application owns `IMasterCatalog`, DTOs and
shared validation; Infrastructure implements the service using the existing scoped
EF context. No schema changes are required. Code/name are trimmed and required,
with limits of 64/256 characters. Codes are case-sensitive (the existing database
unique index), immutable after creation, and reserved even after archive. Names
can be corrected while active. DELETE is an idempotent archive, never physical
deletion; existing ledger/balance references remain intact and the existing posting
trigger rejects new postings to archived masters. Archived records remain readable
and cannot be updated or restored through these routes.

Lists return `{ items, total, page, pageSize }`, sorted by code then UUID. Defaults
are page 1 and size 20 (maximum 100). Optional `search` matches a literal,
case-sensitive substring of code/name; `archived=true/false` filters state, while
omitting it includes both states. HTTP responses use 201 plus Location on create,
200 for reads/updates, 204 for archive, 404 for missing IDs, ValidationProblem 400
for invalid input, and ProblemDetails 409 for duplicate/immutable codes or updates
to archived records. Database unique violations are also translated to 409 so
competing creates receive the same response. Without persistence configuration,
catalog routes return ProblemDetails 503; health remains available.

The project currently has no authentication or authorization mechanism. These
routes do not introduce one; access control must be supplied by the project's
future authentication integration before exposing the API to untrusted clients.
API tests use an injected service double to verify the HTTP contract. The existing
opt-in disposable PostgreSQL test additionally exercises both real catalog services,
uniqueness, filtering, updates and archive/reference preservation.

## Inventory and history

Goods and Warehouse are separate master data with unique, nonempty codes and
names. StockTransaction is an immutable receipt/issue ledger with positive
numeric(20,6) quantities, actor, occurrence and creation timestamps. InventoryBalance
materializes the frequent stock lookup; its composite primary key guarantees one
row per goods/warehouse pair. Foreign keys restrict deletion, checks reject
negative balances and invalid transaction types, and indexes serve warehouse
inventory and goods/warehouse chronological history. Codes remain reserved after
archiving. Quantities have at most six decimal places; direct SQL callers must
likewise respect this precision (PostgreSQL numeric casts round extra decimals).

Posting inserts only history. A PostgreSQL BEFORE INSERT trigger verifies and
locks active masters FOR SHARE, initializes the balance with ON CONFLICT, then
updates it using a conditional atomic UPDATE. Concurrent issues contend on the
same balance row; after waiting, PostgreSQL rechecks the stock predicate. An
insufficient issue raises SQLSTATE 23514, rolling back both the ledger insert and
balance changes, including a newly initialized zero balance. Receipts use the
same row lock. Archive updates wait for master locks; no new posting can use an
already archived master. No process-local locks are used.

Balance writes outside the posting trigger and ledger updates/deletes are rejected.
Corrections require compensating postings. The adapter validates type, precision,
positive quantity, UTC and actor before accessing the database, and detaches a
failed insert to allow retry with the same context. DbUpdateException exposes the
provider failure; the future API should translate 23514 into a business conflict.
Use a fresh scoped context per operation. Multi-posting transactions should lock
pairs in consistent order or retry the entire transaction after deadlock. Actors
must ultimately come from authenticated application identity; authentication is
outside this schema task.

The runtime database role must not own tables or have permission to disable
triggers, truncate tables or run DDL. Use a separate migration role. Database owners
can bypass database protections; the trigger is not a security boundary against
administrators. Never update balances independently of history.

References: [Npgsql EF provider](https://www.npgsql.org/efcore/) and
[PostgreSQL Read Committed behavior](https://www.postgresql.org/docs/current/transaction-iso.html#XACT-READ-COMMITTED).

## Migration and recovery tooling

There was no pre-existing deployment/migration mechanism. The local dotnet-ef
manifest pins EF tooling; InitialWarehouse includes the generated model snapshot,
tables/indexes/checks and reversible trigger SQL. The design-time factory requires
no credentials to generate or inspect SQL. Existing health startup remains usable
without database configuration. Restore and inspect scripts before independently
authorizing application to a development database:

```powershell
dotnet restore
dotnet tool restore
dotnet ef migrations script --project src/DeliveryDemo.Infrastructure --output warehouse-up.sql
dotnet ef migrations script InitialWarehouse 0 --project src/DeliveryDemo.Infrastructure --output warehouse-down.sql
# Only a separately authorized disposable/development database:
dotnet ef database update --project src/DeliveryDemo.Infrastructure --connection $env:WAREHOUSE_DEV_CONNECTION
# Destructive rollback, only after backup and explicit authorization:
dotnet ef database update 0 --project src/DeliveryDemo.Infrastructure --connection $env:WAREHOUSE_DEV_CONNECTION
```

Down removes triggers/functions before dropping dependent tables and then masters.
Rollback to 0 destroys all warehouse history and balances. Back up with the site's
PostgreSQL backup tooling, test restore, stop writers and restore the backup for
recovery; prefer a forward corrective migration once production data exists. No
migration was applied to an AWP or company database during this task.

## Verification

```powershell
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes --no-restore
# Docker required; creates and removes a dedicated postgres:17-alpine container:
$env:WAREHOUSE_POSTGRES_TESTS = '1'
dotnet test --no-build --logger "trx;LogFileName=warehouse-postgres.trx" --results-directory TestResults
Remove-Item Env:WAREHOUSE_POSTGRES_TESTS
```

Normal tests check snapshot/model consistency, forward/rollback SQL and validation.
The opt-in provider test never reads an external connection string: it creates a
randomly named local disposable container with generated credentials. It covers
migration up/down/up, real posting, competing issues, no-stock rollback, forbidden
balance/history mutations, master deletion, duplicate master keys, quantity checks,
explicit transaction rollback and archived-master rejection. The container is
removed in finally. The operator reported a successful prior verification of all
seven original backend tests with the PostgreSQL test enabled, including migration,
constraints, concurrent posting and rollback. On this retry, Docker access was
denied by the workspace permissions, so the provider test could not run here.
Build and formatting checks passed. Additional validation tests cover decimal
precision, quantity range, UTC occurrence time and blank actors.
NuGet networking was also blocked during the original implementation; verification used the
existing package cache through a temporary local feed with audit disabled for that
restore command only. Run normal online restore/audit in the verification environment.

The latest catalog review preserves the persisted-timestamp reload on create and
the provider test's rollback-before-archive ordering. The operator reported all 18
backend tests passing with PostgreSQL enabled before this review. Added API cases
cover trimmed code/name length boundaries and update conflict responses. This
review's build and format checks passed; the fresh `TestResults/warehouse-backend.trx`
records 23 passed tests and one skipped provider test because Docker access is
denied in the model sandbox. AWP must run the full suite with
`WAREHOUSE_POSTGRES_TESTS=1` using the TRX command above for fresh provider evidence.
