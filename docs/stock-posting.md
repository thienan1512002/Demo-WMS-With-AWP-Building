# Stock posting service contract

The existing IStockPosting service and PostgreSQL stock_post trigger remain the
posting boundary. No HTTP posting route is added because authentication and actor
identity are not configured. Callers supply a trusted actor; this service does not
authenticate users.

Use PostAsync(requestId, goodsId, warehouseId, type, quantity, occurredAt, actor)
for retryable operations. Keep the UUID requestId and complete payload stable.
The request UUID is the immutable ledger primary key, globally scoped across
receipts/issues, goods and warehouses. The legacy overload generates a fresh ID
for each call and is intended only for operations without retries.

A transaction-scoped PostgreSQL advisory lock serializes the request ID before
reading history. Identical retries return the original ID even after stock is
exhausted or a master is archived. Changed payloads raise Conflict. Timestamp
comparison uses PostgreSQL microsecond precision. The ledger primary key remains
the database uniqueness constraint; hash collisions only reduce concurrency.
Locks live until transaction completion. Each operation uses its own scoped EF
context. The default PostgreSQL READ COMMITTED isolation is required for seeing
a concurrent request after waiting. Existing caller transactions are respected;
callers must commit them, and roll back/retry the whole unit on transaction errors.
Multi-posting callers should order request and inventory keys consistently.

The existing trigger locks active masters and conditionally updates stock within
the ledger insertion. Failed insertion or transaction rollback preserves both
balance and history. No new schema or balance-writing path is introduced.

Invalid IDs, quantity, type, UTC time or actor raise ArgumentException before DB
access. StockPostingException carries NotFound, Conflict, BusinessRule or
Persistence with fixed public messages, without provider SQL/error details.
BusinessRule covers insufficient stock, archived masters and balance overflow.
Infrastructure cancellation propagates. Failed/uncommitted request IDs may be
retried; only committed history consumes an ID. Unexpected runtime failures should
be handled by the existing generic ProblemDetails handler when exposed via HTTP.

Provider tests extend the disposable PostgreSQL suite with simultaneous duplicate
issues, replay after exhaustion, mismatched payload rejection, missing masters,
audit field checks and consistent ledger/balance assertions. Existing receipt,
competing distinct issues, constraint rollback and explicit rollback tests remain.
Run with WAREHOUSE_POSTGRES_TESTS=1 in AWP's Docker-enabled verifier. Local normal
tests skip that opt-in test; skipped tests are not evidence of provider success.
