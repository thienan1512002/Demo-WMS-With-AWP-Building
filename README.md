# DeliveryDemo — Warehouse application

Foundation: .NET 10 API and React/TypeScript frontend. Warehouse business features are implemented through the development tasks in AWP.

## Local development

```powershell
dotnet restore
dotnet run --project src/DeliveryDemo.Api --urls http://localhost:5000
```

In another terminal:

```powershell
cd frontend
npm ci
npm run dev
```

The frontend runs on port 5173 and proxies `/api` to the backend on port 5000. Production requires the frontend and API to be routed under the same origin.

## Verification

```powershell
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes --no-restore
cd frontend
npm run build
npm run test
npm run lint
```

## Structure and conventions

- `src/DeliveryDemo.Domain`: domain models/rules, independent of API/persistence.
- `src/DeliveryDemo.Application`: use cases/contracts; references Domain.
- `src/DeliveryDemo.Infrastructure`: persistence/external adapters; references Application.
- `src/DeliveryDemo.Api`: HTTP endpoints and composition.
- `tests/DeliveryDemo.Api.Tests`: HTTP integration tests using WebApplicationFactory.
- `frontend`: React, TypeScript and Vite; Oxlint and Vitest/component tests.

Follow `.editorconfig`, keep nullable reference types enabled, and use feature folders for warehouse work. Never commit credentials. The repository currently has no database/ORM, authentication, migrations or warehouse business rules. Those choices must be documented by the architecture/domain tasks before implementation; initial health checks prove application startup only.

Approved dependency changes are carried between AWP task workspaces. Review each task's evidence before approving. AWP verification includes backend and frontend separately.
