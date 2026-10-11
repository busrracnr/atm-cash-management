# ATM Cash Management Backend

Backend for managing the cash in a bank's ATM network: cassette configuration, cash loading,
withdrawals (quote → confirm), deposits, reconciliation with four-eyes approval, an immutable
cassette movement ledger and operational reporting.

> **Status — walking skeleton.** The end-to-end plumbing (API → application → database, Docker,
> migrations, error model, health checks, tests) is in place with a first slice (ATM registration).
> Sections marked 🚧 are completed as the corresponding requirements are implemented.

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

### 🚧 Note selection algorithm (B3)
### 🚧 Two-step withdrawal, reservations and idempotency (B4)
### 🚧 Daily limit and card number protection (B5)
### 🚧 Concurrency (B6)
### 🚧 Cassette movement ledger (B8)
### 🚧 Reconciliation with four-eyes approval (B9)
### 🚧 Reporting, low-cash alerts and the outbox (B10)

## Assumptions

🚧 Collected while implementing; each ambiguity in the requirements is listed here with the decision taken.

## Unfinished items and known limitations

- 🚧 Part A and B1–B10 are being implemented on top of this skeleton.
- Swagger UI is enabled in every environment to make the assignment easy to evaluate; a real
  deployment would restrict it.

## AI usage

🚧 Written at the end of the assignment.
