# CLAUDE.md

Catalog Consolidation: a .NET minimal API that imports a sellers' products file into a SQLite catalog without duplicating products (take-home assessment). The ambiguities and every design decision are in `README.md` — read it before changing behavior.

## Resolved standards (project-standards)

| # | Parameter | Resolved value |
|---|-----------|----------------|
| P1 | Stack | .NET (C#), `net10.0` |
| P2 | Granularity | Single project, DDD-style folders `Domain/Application/Infrastructure/Api`, plus `CatalogConsolidation.Tests` |
| P3 | Consistency | None: one SQLite store and a real transaction (`BEGIN IMMEDIATE` unit of work) |
| P4 | Unique ids | DB auto-increment (`AUTOINCREMENT`) |
| P5 | Testing | Mocks (NSubstitute) for unit tests, plus self-contained integration tests against a real SQLite copy; no staging |
| P6 | Runtime | Standard JIT, no AOT |
| P7 | Idempotency | A request-level key is not needed yet: documented as a future extension, nothing implemented |
| P8 | Sensitive data | None in scope (product names, brands, seller names and ids) |

## Rules that apply

**Process**
- Settle design decisions with the user, one at a time, before coding; write assumptions down in the README instead of guessing silently.
- Give honest technical opinions. An open question ("why is X like this?") is a request for an explanation, not for a change. Only refactor what was explicitly asked; fix only the specific defect if a user edit introduces one.

**Domain modeling**
- A value object is built only through a validating factory that throws on invalid input: no public constructor or setter that bypasses validation.
- Entities cannot exist in an invalid state: factories take already-validated value objects.
- Decisions belong in a policy/domain service (`ProductMatcher`), not in the value object they are about.
- Do not add a DTO, struct or mapper only to carry locals between two private methods.

**Time and culture**
- No ambient clock in domain or application logic: inject `TimeProvider` if time is ever needed. Prefer `DateTimeOffset` for anything persisted or crossing a boundary.
- No implicit-culture parsing or formatting of numbers or dates: pass an explicit invariant culture every time (including numbers interpolated into exception messages).

**Error handling**
- Every error response is RFC 7807 `application/problem+json`: exceptions go through `GlobalExceptionHandler`, and errors the framework answers by itself (415, 404, 405) go through `UseStatusCodePages`.
- Domain and validation failures are 4xx; a busy or unavailable dependency is 503 (502 for an upstream service); anything else is 500 and its message is never sent to the client. A compensation failure would be 500 with a `Critical` log (no saga exists today).
- Transport-level retry (e.g. Polly on an HTTP client) stays separate from business-level retry; there are no HTTP clients today.

**Testing**
- Integration tests never touch tracked seed data: they copy `CatalogConsolidation.Tests/Fixtures/catalog.original.db` to a temp file (`TempCatalog`). They never read or write the `catalog.db` the app runs against.
- Anything that reserves or writes under concurrency needs a test firing N parallel operations and asserting no duplicates (see `Concurrent_imports_of_the_same_new_product_create_it_exactly_once`).
- Integration tests are self-contained: the test run itself starts everything it needs.
- Tests verify business behavior, not entities in isolation: cover a rule through the real flow (the importer with real entities, collaborators substituted at the abstraction boundary). No test class for a single entity or value object only to raise coverage or repeat what a flow test proves. A direct test is for logic with real branching (a policy, the normalizer, the similarity algorithm, a value object's validation); a guard no normal flow can reach is not a reason for a test of its own.

**Git hygiene and secrets**
- Files that accumulate local state from running the app belong in `.gitignore`, and are untracked once with `git rm --cached`.
- Never log or persist secrets beyond the minimum needed. Not applicable today (P8), but binding the moment sensitive data appears.

## Project conventions (the user's explicit preferences)

- Classes use explicit constructors with `private readonly _field` members, not primary constructors. Positional `record`s are fine.
- Domain interfaces live in `Domain/Abstractions`.
- Entities are rich (behavior and invariants); Infrastructure only stores and indexes. The snapshot is read-only (`Track`); writing goes through `IProductRepository`.
- The `ImportReport` never exposes the database's internal ids.
- Tests use NSubstitute `Substitute.For<T>()`, with no hand-written fakes. The shared substitutes that keep state are in `Support/StatefulMocks`. The default arrangement goes in the test class constructor (xUnit builds a new instance per test); static helpers are only for pure data builders.
- The project and its documentation are in English.

## Commands

- Run: `dotnet run --project CatalogConsolidation`. It migrates and writes to `catalog.db`, the user's working database. `catalog.db` is git-ignored and untracked; seed it with `cp CatalogConsolidation.Tests/Fixtures/catalog.original.db catalog.db`. Use a copy for experiments and probes.
- Test: `dotnet test`. If a running API locks `bin/`, test into an isolated folder: `dotnet test --artifacts-path <dir>`, then delete it.
