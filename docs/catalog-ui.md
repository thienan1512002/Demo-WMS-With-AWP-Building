# Catalog UI

The implementation follows the TASK-001 baseline in `current-architecture.md`:
React hooks, relative imports, direct same-origin fetch with AbortController,
Vietnamese shell, plain CSS, white cards and indigo accents. No dependency is added.

The baseline has no router, API client, form components or permission framework.
Hash links `#/goods` and `#/warehouses` support direct entry and browser history
without additional server rewrite requirements. The existing home shell and other
module placeholders remain available. Unknown hashes show the home screen.
`Catalog` shares list/filter/pagination behavior and `CatalogForm` shares editing
between the two existing API resources. State remains local to each mounted page;
navigation cancels in-flight requests and resets page-specific state.

Lists use the API's code/name search (case-sensitive), archive filter and 20-row
pagination. Loading, empty, retryable error and successful save states use semantic
status/alert regions. Forms trim input and enforce the API's required code/name
and 64/256 character limits. API ProblemDetails and field errors are displayed.
Codes remain read-only on update; archived rows have no edit action. Save requests
disable the form and use a synchronous pending guard against duplicate submission.
After save, the current filtered list reloads. No archive/delete UI is requested.

Labeled controls, validation descriptions, keyboard focus, active navigation and
semantic tables follow the existing accessibility conventions. Forms and filters
wrap at the shell's 700px breakpoint; tables can scroll on narrow screens.

No authentication or authorization exists in the baseline frontend or API. Catalog
navigation mirrors the existing unrestricted API; no invented client-side permission
policy is added. Production access control remains the project-level open decision
identified by TASK-001.

`Catalog.test.tsx` uses the existing Vitest/jsdom/Testing Library setup with mocked
fetch responses to verify both resources, create/update, validation, API errors,
pending submission, refresh, filters, pagination, archive read-only behavior,
request cleanup, focus and hash navigation. Run `npm.cmd run build`,
`npm.cmd run lint` and `npm.cmd run test` from `frontend`.
