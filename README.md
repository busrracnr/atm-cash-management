# ATM Cash Management Backend

Backend for managing the cash in a bank's ATM network: cassette configuration, cash loading,
withdrawals (quote → confirm), deposits, reconciliation with four-eyes approval, an immutable
cassette movement ledger and operational reporting.

> **Status — in progress.** Done: ATM registration, cassette configuration, lifecycle, cash loading and
> unloading, balances (A1–A7, A12, B1, B2), the movement ledger with its consistency check (B8) and
> the fewest-notes selection algorithm with its acceptance tests (B3), and the two-step withdrawal
> with reservations, expiry and idempotent confirmation (A9, A10, B4), daily limits and card number
> protection (B5).
> Sections marked 🚧 are completed as the remaining requirements are implemented.

## Contents

1. [Tech stack](#tech-stack)
2. [How to run](#how-to-run)
3. [How to run the tests](#how-to-run-the-tests)
4. [Project structure](#project-structure)
5. [Architecture and key design decisions](#architecture-and-key-design-decisions)
6. [Assumptions](#assumptions)
7. [Unfinished items and known limitations](#unfinished-items-and-known-limitations)
8. [AI usage](#ai-usage)

## Tech stack

| Concern       | Choice                                                                          |
|---------------|---------------------------------------------------------------------------------|
| Runtime       | .NET 10 (LTS), ASP.NET Core Web API with controllers                            |
| Persistence   | EF Core 10 code-first with migrations, PostgreSQL 17 (Npgsql)                   |
| API docs      | OpenAPI document (`Microsoft.AspNetCore.OpenApi`) + Swagger UI                  |
| Errors        | RFC 9457 ProblemDetails with a machine-readable `errorCode`                     |
| Tests         | xUnit v3 on Microsoft.Testing.Platform, Testcontainers (real PostgreSQL)        |
| Packaging     | Multi-stage Docker image, `docker compose`                                      |

## How to run

### With Docker (recommended)

```bash
docker compose up --build
```

| URL                                    | What                                      |
|----------------------------------------|-------------------------------------------|
| http://localhost:8088/swagger          | Swagger UI                                |
| http://localhost:8088/openapi/v1.json  | OpenAPI document                          |
| http://localhost:8088/health           | Readiness (includes the database check)   |
| http://localhost:8088/health/live      | Liveness (process only)                   |

The database schema is created by EF Core migrations, applied automatically when the API starts.

Host ports default to `8088` (API) and `55432` (PostgreSQL) to avoid clashing with tools that
commonly occupy `8080`/`5432`. Override them if needed:

```bash
API_PORT=9090 DB_PORT=5433 docker compose up --build
```

### API on the host, database in Docker

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
docker compose up -d db
dotnet run --project src/AtmCashManagement.Api
```

Swagger UI: http://localhost:5080/swagger

## How to run the tests

```bash
dotnet test
```

- **Unit tests** (`tests/AtmCashManagement.UnitTests`) have no external dependencies.
- **Integration tests** (`tests/AtmCashManagement.IntegrationTests`) boot the real API in memory and
  run it against a throwaway PostgreSQL container started by Testcontainers, so **Docker must be
  running**. Migrations run exactly as they do in production.

Testcontainers uses the active Docker context, so no extra configuration is needed with Docker
Desktop or Colima (verified with Colima 0.10 on Apple Silicon).

### End-to-end scenario (Postman)

Import `postman/AtmCashManagement.postman_collection.json` (and optionally
`postman/local.postman_environment.json`) and run the collection in order with the Collection Runner,
or from the command line:

```bash
npx newman run postman/AtmCashManagement.postman_collection.json
```

Every request carries assertions, so a green run means the scenario behaves as specified. Steps are
numbered after the order in the assignment; the error examples at the end show the rejection paths.

## API overview

| Method | Route                                  | Purpose                                                      |
|--------|----------------------------------------|--------------------------------------------------------------|
| POST   | `/api/atms`                            | Register an ATM (starts UNCONFIGURED)                        |
| GET    | `/api/atms`, `/api/atms/{id}`          | List ATMs / ATM with its cassettes and notes                 |
| PUT    | `/api/atms/{id}/cassettes`             | Configure the five cassettes                                 |
| PUT    | `/api/atms/{id}/status`                | Put in service / switch to maintenance                       |
| POST   | `/api/atms/{id}/cash-loads`            | Record cash loaded by hand                                   |
| POST   | `/api/atms/{id}/cash-unloads`          | Empty cassettes                                              |
| GET    | `/api/atms/{id}/balance`               | Notes per denomination and total balance (requirement A12)   |
| GET    | `/api/atms/{id}/movements`             | Cassette movement ledger                                     |
| GET    | `/api/ledger/consistency?atmId=`       | Stored balances vs. balances recomputed from the ledger      |
| POST   | `/api/atms/{id}/withdrawal-quotes`     | Withdrawal step 1: breakdown or alternatives, notes reserved |
| GET    | `/api/withdrawal-quotes/{id}`          | Quote and its status                                         |
| POST   | `/api/withdrawal-quotes/{id}/confirm`  | Withdrawal step 2 (requires `Idempotency-Key`)               |
| POST   | `/api/withdrawal-quotes/{id}/cancel`   | Cancel a quote and release its notes                         |

Every request may carry an `X-User` header; it is recorded on ledger entries (default `anonymous`).

## Project structure

```
├── src
│   ├── AtmCashManagement.Domain          # Entities, business rules, error catalogue — no dependencies
│   ├── AtmCashManagement.Application     # Use cases (services), request/response contracts, ports
│   ├── AtmCashManagement.Infrastructure  # EF Core + PostgreSQL, migrations, DI wiring, health checks
│   └── AtmCashManagement.Api             # Controllers, ProblemDetails mapping, OpenAPI, Dockerfile
├── tests
│   ├── AtmCashManagement.UnitTests
│   └── AtmCashManagement.IntegrationTests
├── postman                               # End-to-end collection + local environment
├── docker-compose.yml
├── Directory.Build.props                 # Shared compiler settings for every project
├── Directory.Packages.props              # Central package version management
└── global.json                           # SDK and test-runner selection
```

## Architecture and key design decisions

### Layering

The solution follows Clean Architecture: dependencies point inwards only.

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure
```

- **Domain** has no package references. Business rules live on the entities, so they can be unit
  tested without a database or web host.
- **Application** orchestrates use cases. It depends on EF Core's abstractions through
  `IAppDbContext`, a deliberate trade-off: LINQ over `DbSet<T>` instead of a hand-written
  repository per aggregate, which would mostly duplicate what EF Core already provides.
- **Infrastructure** implements the ports (PostgreSQL, migrations, health checks).
- **Api** is the composition root and the only layer that knows about HTTP.

### Error model

Business-rule violations throw a `DomainException` carrying an `Error(Code, Message, Type)` from a
central catalogue (for example `AtmErrors`). A single `IExceptionHandler` turns it into a
ProblemDetails response:

| `ErrorType`  | HTTP status                 |
|--------------|-----------------------------|
| `Validation` | 422 Unprocessable Entity    |
| `NotFound`   | 404 Not Found               |
| `Conflict`   | 409 Conflict                |
| `Forbidden`  | 403 Forbidden               |

Malformed requests are rejected by model validation with 400 (`VALIDATION_FAILED`). Unexpected
exceptions become a 500 (`INTERNAL_ERROR`) without leaking internals. Every error response, including
those produced by the framework, carries `errorCode` and `traceId`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "ATM '0199d4a4-…' was not found.",
  "errorCode": "ATM_NOT_FOUND",
  "traceId": "00-…"
}
```

### Persistence

- **PostgreSQL** — lightweight native multi-arch image, and it offers row locks
  (`SELECT … FOR UPDATE`) for the concurrency requirements.
- **Conventions** — snake_case tables and columns, enums stored as text (readable, safe to reorder),
  `timestamptz` columns written in UTC.
- **Keys** — UUIDv7 (`Guid.CreateVersion7`): globally unique like any GUID, but time-ordered, so the
  primary-key index stays append-only.
- **Migrations on startup** — applied with a short retry loop while the database container starts.
  In a production system this would run as a separate deployment step (for example a migration
  bundle) rather than from every API instance.

### Time and culture

- `TimeProvider` is injected wherever the current time matters (quote expiry, daily limits), so
  tests can control the clock.
- `InvariantGlobalization` is enabled: the API behaves the same on a developer machine with a
  Turkish locale as in the container (no `i`/`İ` casing surprises).

### ATM lifecycle (B2)

`AtmLifecycle` holds both rule tables, so the whole state machine can be read in one place and is
tested exhaustively (every status pair):

| Operation                  | Allowed while                               | Effect on status            |
|----------------------------|---------------------------------------------|-----------------------------|
| Configure cassettes        | UNCONFIGURED, CONFIGURED, MAINTENANCE       | UNCONFIGURED → CONFIGURED   |
| Load cash                  | CONFIGURED, MAINTENANCE                     | CONFIGURED → LOADED         |
| Unload cash                | MAINTENANCE                                 | —                           |
| Withdraw / deposit         | IN_SERVICE                                  | —                           |
| `PUT /status` IN_SERVICE   | LOADED, MAINTENANCE                         | → IN_SERVICE                |
| `PUT /status` MAINTENANCE  | IN_SERVICE                                  | → MAINTENANCE               |

A disallowed status change returns `409 INVALID_STATE_TRANSITION`; an operation in the wrong state
returns `409 OPERATION_NOT_ALLOWED_IN_CURRENT_STATE`. Both carry `currentStatus` and
`allowedStatuses` so a client can explain the refusal.

### Cassettes and balances

An ATM is an aggregate: the `Atm` root owns its five `Cassette`s and every change goes through it.
A cassette's content is stored as `CassetteBalance` rows per currency and denomination: a recycling
cassette has exactly one, the deposit cassette one per note type it has received. Database CHECK
constraints mirror the domain rules (slot 1–5, valid definitions, 0 ≤ reserved ≤ notes ≤ 100) as a
last line of defence.

### Cassette movement ledger (B8)

- **Single write path.** A balance can only change through `Cassette.Record(...)`, which applies the
  delta *and* returns the matching `CassetteMovement`. The context adds those entries in the same
  `SaveChanges` (same transaction) as the balances, so they cannot drift apart.
- **Two quantities.** Each entry has `noteCountDelta` (physical notes) and `reservedCountDelta`
  (notes held by open quotes), so reservations are reproducible from the ledger too.
- **Append-only.** EF Core refuses to update or delete an entry, and a PostgreSQL trigger rejects
  `UPDATE`, `DELETE` and `TRUNCATE` on `cassette_movements` even for direct SQL.
- **Ordering.** A database identity column (`sequence`) gives a strict global order of entries.
- **Consistency check.** `GET /api/ledger/consistency` sums the movements per cassette, currency and
  denomination and compares them with the stored balances. An integration test corrupts a balance
  with direct SQL and asserts that the check reports it.

### Concurrency (B6)

Every cash-changing operation runs in a transaction that first locks the ATM row
(`SELECT … FOR UPDATE`), then loads and changes the aggregate. Operations on the same ATM therefore
run one after another, while different ATMs proceed in parallel; an ATM serves one customer at a
time, so this costs nothing in practice. 🚧 The parallel-quote test is added with the quote feature.

### Note selection algorithm (B3)

Paying an amount with the fewest notes from a limited stock is the *bounded change-making* problem.
Taking the largest note first does not work: for 160 with 50×3 and 20×10, greedy takes three 50s
and cannot pay the remaining 10, while 50×2 + 20×3 pays it exactly.

`NoteSelector` solves it with dynamic programming:

- Every denomination is a multiple of 10, so amounts are indexed in units of 10. A full ATM holds at
  most 4 × 100 × 500 = 200,000 per currency, i.e. 20,000 table cells.
- Denominations are added one at a time:
  `fewest[s] = min over k ≤ available of (fewest with the smaller notes)[s − k·note] + k`,
  remembering the chosen `k` so the breakdown can be read back.
- One table answers *every* amount, so when the request cannot be paid exactly the lower
  alternative (largest payable amount below) and the upper alternative (smallest payable amount
  above, capped by a maximum such as the remaining daily limit) are simple scans.
- On a tie in note count the breakdown with more large notes wins, keeping results deterministic.

Correctness is covered three ways: the eight acceptance tests are written as a table that mirrors
the assignment line by line; a randomized test compares the result with exhaustive search on 400
seeded stocks and requests; and edge cases (amounts that are not a multiple of 10, requests above
the whole stock, an empty stock, the upper cap) have their own tests.

`CassetteAllocator` then maps the breakdown onto cassettes. When the same currency and denomination
sits in two cassettes, the one with the higher fill ratio pays first (B2).

### Two-step withdrawal, reservations and idempotency (B4)

- **Quote.** The breakdown (or the two alternatives) is computed from the *unreserved* notes, mapped
  onto cassettes and reserved with `RESERVATION` entries. When two alternatives are offered, each
  cassette reserves the larger of the two needs: enough for whichever the customer picks, without
  holding both. A quote is valid for 60 seconds.
- **Confirm.** The chosen option is paid out (`WITHDRAWAL` entries consume the reservation) and the
  unused part is released (`RESERVATION_RELEASE`). EXACT is the default when it was offered.
- **Cancel** releases the whole reservation. Switching the ATM to MAINTENANCE cancels its open quotes.
- **Expiry.** A hosted service sweeps expired quotes every 5 seconds. In addition, a new quote first
  expires the ATM's overdue quotes under the same lock, so released notes can be offered again at
  once even between sweeps. From its 60th second a quote reads as EXPIRED and cannot be confirmed.
- **Idempotency.** The confirmation response is stored together with the `Idempotency-Key` in the
  same transaction as the payout, and a repeated request returns the stored response verbatim. The
  key is looked up again after the ATM lock is acquired, so two concurrent duplicates cannot both
  pay; the primary key on (scope, key) is the last line of defence. Reusing a key for a different
  request returns `422 IDEMPOTENCY_KEY_REUSED`.

### Daily limit and card number protection (B5)

- **Card numbers** are validated (12–19 digits, Luhn) and stored only as an HMAC-SHA256 under a
  secret key kept outside the database (`Security:CardHashKey`). A plain SHA-256 would not be
  enough: with a known issuer prefix and the Luhn digit, the remaining digits can be brute-forced.
  The key is a development value in `docker-compose.yml`; a real deployment injects it from a secret
  store. An integration test reads the stored value and checks it is the keyed hash, not the number
  or its plain hash.
- **Daily limit.** TRY 20,000, USD 1,000 and EUR 1,000 per card and business day, tracked separately
  per currency and checked at the quote step. "Used today" is the sum of confirmed amounts plus open,
  unexpired quotes (each counted with its largest offered amount, since the customer may still take
  it). A request above the remaining limit is rejected with `422 DAILY_LIMIT_EXCEEDED`, reporting
  `usedToday` and `remaining`; the upper alternative is only offered when it fits in the remaining
  limit.
- **Parallel requests for one card.** The ATM lock does not cover two ATMs, so the quote also takes a
  transaction-scoped PostgreSQL advisory lock on the card hash. It is always taken after the ATM lock;
  a fixed order means no deadlocks.
### 🚧 Reconciliation with four-eyes approval (B9)
### 🚧 Reporting, low-cash alerts and the outbox (B10)

## Assumptions

1. **Foreign currency cassettes.** "At least one and at most two of the recycling cassettes may be
   configured for a foreign currency" is enforced literally: every ATM has one or two USD/EUR
   recycling cassettes, so a TRY-only ATM is rejected.
2. **Loading states.** B2 allows loading only in MAINTENANCE or during initial setup
   (UNCONFIGURED/CONFIGURED). A second load while LOADED is therefore rejected; a correction before
   the first service goes through IN_SERVICE → MAINTENANCE. The rule was kept literal rather than
   widened.
3. **What can be loaded.** Cash is loaded by hand into recycling cassettes only. The deposit cassette
   starts empty and is filled by customer deposits.
4. **Unloading** empties whole cassettes, as when a cash-in-transit crew swaps them, and is allowed in
   MAINTENANCE only.
5. **Reconfiguration** is allowed only for empty cassettes; unchanged cassettes keep their notes and
   their identity, so their ledger history stays attached.
6. **Direct status changes** are limited to LOADED → IN_SERVICE, IN_SERVICE → MAINTENANCE and
   MAINTENANCE → IN_SERVICE. CONFIGURED and LOADED are reached by configuring and loading.
7. **Balance totals** are reported per currency. A single TRY total needs exchange rates, which are
   introduced with reconciliation (B9) and reporting (B10).
8. **`X-User`** is optional for now and recorded as `anonymous` when missing.
9. **Amounts that are not a multiple of 10** cannot be paid exactly with 20/50/100/500 notes; the
   nearest payable amounts are offered instead (e.g. 135 → 120 or 140).
10. **"Higher fill ratio first"** uses the unreserved notes over the capacity. The fuller cassette
    pays until it is empty and the next one continues; dispensing note by note alternately from both
    was considered and not chosen, as the requirement describes an order, not balancing.
11. **Exact payment comes alone.** When the request can be paid exactly, no alternatives are offered.
12. **Idempotency stores successes.** Only successful confirmations are stored. A failed attempt
    (e.g. `QUOTE_EXPIRED`) can be retried with the same key and is evaluated again.
13. **Maintenance cancels quotes.** A customer cannot confirm a withdrawal at an ATM that has been
    taken out of service, so open quotes are cancelled and their notes released.
14. **Business day** for the daily limit is the calendar day in Türkiye (UTC+3 all year); it is
    defined in one place (`BusinessDay`).
15. **Over-limit requests are rejected, not reduced.** The customer gets the remaining amount in the
    error and can ask again; silently paying less was considered surprising for a customer.

## Unfinished items and known limitations

- 🚧 Withdrawals, deposits, daily limits, reconciliation and reporting (A9–A11, B3–B7, B9, B10) are
  being implemented on top of this foundation.
- Swagger UI is enabled in every environment to make the assignment easy to evaluate; a real
  deployment would restrict it.

## AI usage

🚧 Written at the end of the assignment.
