# Catalog Consolidation

## Assessment Statement (VTEX | AI Coding Interview, Take-home assessment)

### Submission & Timeline

**Timeline:**

- You have **48 hours** (two days) from the moment you receive this challenge to submit your solution.

**Submission Method:**

- Please host your solution in a **public** GitHub or GitLab repository and share the link replying to the email that you received this assessment.

**Guideline Adherence:**

- Before you begin, please carefully review the Guideline Document provided alongside this challenge. Please, ensure your code structure, naming conventions, and documentation align with the standards described there.

### The challenge

**Catalog Consolidation**

A traditional e-commerce company will start operating as a marketplace (a store that sells products from other stores). Marketplaces frequently receive product catalogs from multiple sellers (stores that sell their products on the marketplace).

Currently, this company has a product catalog that contains all the items it sells. The store must be capable of receiving products from different sellers and adding them to its catalog. It is important to note that it is common for a product to be sold by several stores. Each seller registers their own products, so there may be slight variations in the information for the same item across different stores. Duplicating items is undesirable. However, it is crucial to record which sellers offer each product.

You will receive a SQLite database populated with products. This database has only 2 tables: a product table and a table to link products to sellers.

Implement a catalog consolidation system that receives a file containing products from different sellers and saves them to the catalog database. In the case of a duplicate product, the system should not insert the item into the product table but should link the existing item to the seller in the appropriate table.

Demonstrating mastery of the problem and understanding of the solution is more important than presenting a scalable, production-ready implementation.

This challenge contains some intentional ambiguities. They are part of the assessment.

You may make changes to the database if you deem them necessary.

The use of AI agents in the solution's implementation is permitted and encouraged.

---

## Solution Overview

A system that receives a file of products from different sellers and consolidates them into the catalog (SQLite) without duplicating products. When a product already exists, it only links the seller to the existing product.

The database has two tables:

- `Product` (`Id`, `Name`, `Brand`, `Category`): the marketplace catalog.
- `SellerProduct` (`Id`, `SellerName`, `ProductId`, `SellerProductId`): which sellers offer each product.

Input: `ProductEntry.json`, with the fields `Id`, `SellerName`, `Name`, `Brand` and `Category`.

### How it works

For each entry in the file, inside a single transaction:

1. Build the domain objects for the entry, which validate themselves: `SellerName`, `SellerProductId` and `Product` (the required fields). A row the domain rejects is reported and skipped before anything is stored.
2. Normalize `Name` and `Brand` into the product's `ProductKey`.
3. **Exact match:** look for a product (including products created earlier in this run) with the same normalized `Name` and `Brand`.
4. **Approximate match:** if there is none, compare the normalized `Name` against the products with the same `Brand` (when the entry has one) — including products created earlier in this run — using the Levenshtein similarity (see [Approximate Matching](#approximate-matching)). If the best score reaches the threshold, use that product.
5. If no product was found, insert a new one into `Product`.
6. In every case, create the link in `SellerProduct` if it does not exist yet. If the seller already has a link to this product under a *different* `SellerProductId`, the new `SellerProductId` is discarded and surfaced in the report instead of being silently dropped.
7. Return an `ImportReport`: products created, links created, exact matches, approximate matches (the entered name, the catalog product it matched by name, and the score), duplicate links ignored (seller, the product's name, the discarded `SellerProductId` and the one already on file), warnings and rejected rows. The report never exposes the database's internal ids.

A request that cannot be imported at all (a missing `file` part, or a file that is not a JSON array of product entries) is rejected with `400 Bad Request` before any row is processed and before the transaction opens — a different failure mode from a row being rejected inside a valid import (see [API](#api)). The endpoint itself does nothing but hand the uploaded file to `ICatalogImportService`; reading the file, opening the transaction and importing the rows all happen in the Application layer.

Both matching stages read from `ICatalogSnapshot`, a read-only, in-memory, transaction-scoped view of the catalog loaded once at the start of the import. Writing and reading are separate: the importer stores each new product through `IProductRepository` and then tells the snapshot to `Track` it, so later rows of the same file see it. It is not a cache in the staleness/invalidation sense — it is always a consistent view of the catalog plus everything created so far in this run.

## Ambiguities Identified and Decisions

| # | Ambiguity | Decision | Rationale |
|---|-----------|----------|-----------|
| 1 | What is the `Id` field in the file? | It is the product code in the seller's system: stored as `SellerProductId` (changed to `TEXT`), never as `Product.Id` and never used to find duplicates. | Each seller registers its own products. The file carries GUIDs while the diagram shows a numeric column, and the same `Id` appears on different products, so it is not a reliable key. |
| 2 | When is a product a duplicate? | Equal normalized `Name` + `Brand`, or same `Brand` with `Name` similarity at or above the threshold. `Category` is not part of the criterion. | The same camera appears as `Photography` and `Photo`; including the category would create duplicates. The approximate stage covers entries where one word of the name differs (`Roteador` vs `Router`). |
| 3 | Spelling variations | Normalization of `Name` and `Brand`: lowercase, accent removal, trim, whitespace collapsing, and removal of double quotes and apostrophes. | Covers `"Smartphone  Galaxy S23"`, `"Câmera"` vs `"Camera"`, `12.9"` vs `12.9''` vs `12.9`, `55"` vs `55`, and `Levi's` vs `Levis`. |
| 4 | Risk of merging different products with similar names | The approximate match only compares products of the same brand, uses a conservative threshold (default `0.81`, configurable), and every approximate link is listed in the report with the matched product and its score. | Merging different products is as bad as duplicating. See [Approximate Matching](#approximate-matching) for the calibration. |
| 5 | Invalid data (empty fields, malformed Ids such as `ddddeee-ffff-...` and `uddd0000-...`) | A row is rejected only when it is `null` or when `Id`, `SellerName` or `Name` is null, empty or whitespace-only, and it is listed in the report with the reason (the first missing field). The `Id` format is not validated; an `Id` outside the GUID pattern only generates a warning. Scalar JSON values in any field (for example a numeric `Id` such as `123`) are read as text and stored as text; an object or array in a field is read as null. | The `Id` is an opaque code chosen by the seller (the original column was even numeric), so rejecting by format or type would drop valid products. A rejected row, or a field with an unexpected scalar type, does not stop the rest of the import. |
| 6 | Malicious text (`TestBrand'; SELECT 1; --`) | Queries are always parameterized and the value is stored as plain text. | Prevents SQL injection. |
| 7 | Repeated data: same seller and product on several rows, duplicates inside the file, reprocessing the same file | Unique constraint on `SellerProduct (SellerName, ProductId)`; both exact and approximate matching see products created earlier in the same run; everything runs in one transaction. | A repeated link is ignored and listed in the report with the discarded `SellerProductId`; the file cannot duplicate itself regardless of row order; and a second run creates nothing new. |
| 8 | Which data wins when an entry matches an existing product | The catalog's. Only the link is created; `Name`, `Brand` and `Category` stay as they are in the catalog (a different category, such as `Photo`, only adds a warning to the report). | The catalog is data the company already has. `Product` is never altered, with no new columns and no backfill. |
| 9 | Concurrent imports | The transaction is opened with `BEGIN IMMEDIATE`, which takes the SQLite write lock up front. | Imports are serialized, so two runs cannot create the same product at the same time. A tested example: four simultaneous uploads of the same new product create it exactly once. If a lock the import needs is not obtained within the timeout (the write lock to start, or the exclusive lock for the commit, which a reader on another connection can hold up), the import fails with `503`, stores nothing and is safe to retry. |
| 10 | Seller identity: spelling of `SellerName` | `SellerName` is only trimmed and then compared exactly, so `GardenStore` and `gardenstore` are different sellers. | The challenge provides no seller registry to reconcile names against, and merging sellers on a guessed spelling would attach one seller's offers to another. Recorded as a limitation. |

## Approximate Matching

The second matching stage uses the **Levenshtein distance**: the minimum number of single-character insertions, deletions and substitutions needed to turn one string into the other.

The score used here is the normalized similarity:

```
similarity(a, b) = 1 - levenshtein(a, b) / max(length(a), length(b))
```

where `a` and `b` are the normalized product names. A score of `1.0` means identical and `0.0` means nothing in common. The algorithm is implemented in the `Domain/` layer with no external library.

References:

- V. I. Levenshtein, "Binary codes capable of correcting deletions, insertions, and reversals", *Soviet Physics Doklady* 10(8), 707-710, 1966.
- https://en.wikipedia.org/wiki/Levenshtein_distance

### Threshold calibration

The threshold was calibrated on the provided catalog, comparing only products with the same brand:

| Pair | Same product? | Similarity |
|------|---------------|------------|
| `Processador AMD Ryzen 9 7950X` and `Processor AMD Ryzen 9 7950X` | yes | 0.931 |
| `Roteador WiFi 6 TP-Link` and `Router WiFi 6 TP-Link` | yes | 0.826 |
| `Colander Stainless Steel` and `Ladle Stainless Steel` (OXO) | no | 0.792 |
| `Tennis Racket Adult` and `Tennis Racket Bag` (Wilson) | no | 0.737 |

The highest score among different products of the same brand is `0.792` and the lowest among the true matches is `0.826`, so the default threshold is `0.81` (roughly the middle). The margin is narrow, which is why the threshold is configurable (`Matching:SimilarityThreshold`) and every approximate match is reported. This calibration rests on only 4 pairs from the provided fixture (2 known matches, 2 known near-misses) — a small sample, not a generally validated value. Treat it as a reasonable starting point for this dataset rather than proof that `0.81` generalizes to arbitrary catalogs.

## API

`POST /api/catalog/imports`

- Request: `multipart/form-data` with the products file (`file`), in the same format as `ProductEntry.json`.
- Response: the `ImportReport`.
- Every error response is RFC 7807 `ProblemDetails` (`application/problem+json`): exceptions are mapped to a status code in one place, `GlobalExceptionHandler`, and the errors the framework answers by itself without an exception (415, and also 404 and 405 for unknown routes or methods) go through `UseStatusCodePages`:
  - `400 Bad Request`: the `file` part is missing, the multipart body is malformed, or the file is not a valid JSON array of product entries (an empty file included). This is distinct from a row being rejected inside a valid import, which is reported in `rejectedRows`.
  - `415 Unsupported Media Type`: the request is not `multipart/form-data` (answered by the framework itself).
  - `503 Service Unavailable`: another connection holds a lock the import needs past the timeout, either the write lock when it starts or a read lock that blocks its commit; nothing is stored, retry.
  - `500 Internal Server Error`: anything unexpected. The exception's message is logged, never sent to the client.

## Tech Stack and Project Structure

- .NET 10 minimal API (a single application project, plus a test project), SQLite.
- DDD-style folders:

```
CatalogConsolidation/                 # the API
├── Domain/
│   ├── Abstractions/                 # ICatalogSnapshot, IProductRepository, ISellerLinkRepository, ICatalogUnitOfWork(Factory)
│   ├── Exceptions/                   # DomainException and the invariant violations
│   ├── Products/                     # Product, ProductKey, ProductMatcher, TextNormalizer, Levenshtein similarity
│   └── Sellers/                      # SellerProduct, SellerName, SellerProductId, SellerLinker
├── Application/                      # CatalogImportService (file -> transaction -> report), CatalogImporter, DTOs, ImportReport
│   └── Exceptions/                   # InvalidImportFileException, CatalogBusyException
├── Infrastructure/                   # SQLite: unit of work, in-memory ICatalogSnapshot, product and link repositories, schema setup, DI
└── Api/
    ├── Endpoints/                    # the import endpoint (hands the file to the Application layer)
    └── ErrorHandling/                # GlobalExceptionHandler (exceptions -> ProblemDetails)
CatalogConsolidation.Tests/           # xUnit: unit tests plus end-to-end tests against catalog.db and ProductEntry.json
```

The domain model carries the rules instead of just holding data:

- `Product` is created already valid, knows its own `ProductKey`, scores how similar another description is, flags a divergent category, and never changes afterwards (only its database identity is assigned, once).
- `ProductKey` (normalized name + brand, never the category) is the value object that defines "the same product".
- `SellerName` and `SellerProductId` validate themselves; `SellerProductId` also knows whether it looks like a GUID.
- `SellerProduct` is the seller's link to a product and reconciles an incoming offer against itself (`AlreadyLinked` or `DuplicateDiscarded`); `SellerLinker` and `ProductMatcher` are the domain services that apply those rules through the abstractions.
- Infrastructure only stores and indexes: it holds no business rule.

## Catalog Analysis

The provided `catalog.db` and `ProductEntry.json` were analyzed before choosing the rules:

- `Product` has 975 rows (119 with a null `Brand`, 34 with a null `Category`) and `SellerProduct` is empty.
- No two catalog products collide by normalized `Name` + `Brand`.
- `ProductEntry.json` has 269 entries from 20 sellers: 266 are exact matches of an existing product after normalization, 2 are approximate matches (`Roteador`, `Processador`) and 1 is genuinely new (`Security Test Product`).
- One entry has a category divergence against the catalog (`Camera Canon EOS R6`: `Photo` vs `Photography`).

## Database Changes

- `SellerProduct.SellerProductId` changes from `INTEGER` to `TEXT`.
- Unique index on `SellerProduct (SellerName, ProductId)`.
- `SellerProduct.SellerProductName` column storing the name exactly as the seller sent it, for traceability.

The app applies these changes to `catalog.db` itself on startup, idempotently (SQLite cannot alter a column type, so the `SellerProduct` table is rebuilt once, keeping any rows it has). The database path comes from `CatalogDatabase:ConnectionString` (default `Data Source=../catalog.db`, relative to the `CatalogConsolidation/` folder) and is opened read-write without the create option: a wrong path fails at startup with a clear message instead of silently creating an empty database.

## How to Run

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the projects target `net10.0`).

```bash
dotnet restore
dotnet run --project CatalogConsolidation

# In another terminal (adjust the port to the one printed by dotnet run; catalog.db and
# ProductEntry.json live at the repo root)
curl -X POST http://localhost:5053/api/catalog/imports -F "file=@ProductEntry.json"
```

Run the tests with:

```bash
dotnet test
```

## Tests

The test cases use the traps found in `ProductEntry.json` itself: duplicate spaces, accents, inches, divergent category, null brand, repeated Ids, invalid Ids and SQL injection. They also cover the approximate match: `Processador` and `Roteador` link to the existing products, while the closest different products of the same brand (`Colander`/`Ladle`) do not.

Beyond the real catalog pairs, synthetic cases pin the threshold boundary directly (a pair just below `0.81` is rejected, a pair just above is accepted) and confirm that identical names with different brands never match.

The end-to-end tests call the real HTTP endpoint against a throwaway copy of the original challenge catalog kept in `CatalogConsolidation.Tests/Fixtures/catalog.original.db`. They never read or touch the `catalog.db` the app runs against, so importing into it (which migrates and fills it) does not change the tests; a small test guards that the fixture stays the original catalog. They check, among other things:

- the documented analysis of the fixture (266 exact, 2 approximate, 1 new product, 257 links, 11 discarded duplicate links, 4 warnings, no rejected row);
- what ends up in the database: the SQL-looking brand stored as plain text, `SellerProductId` stored as text, the seller's own spelling kept in `SellerProductName`;
- idempotent reprocessing, and four simultaneous uploads creating a new product exactly once;
- bad input: a malformed file (as `ProblemDetails`), a missing `file` part and a garbage multipart body return `400 Bad Request`, a non-multipart request returns `415` (also as `ProblemDetails`), and a catalog locked by another import returns `503`; a `null` row or a numeric `Id` inside a valid file is handled per row instead of failing the import (which kinds of file count as invalid, such as an empty file or JSON that is not an array, is covered by the parser's unit tests);
- a duplicate `SellerProductId` from the same seller resolving to an existing link (reported as a discarded link, not duplicated).

The layers are tested on their own as well:

- Domain: the entities and value objects (validation, the one-time identity, the key ignoring case/accents/spacing/category, similarity only inside a brand, category divergence, link reconciliation) and the two domain services (`ProductMatcher`, `SellerLinker`); every collaborator is an NSubstitute substitute of its domain interface (no hand-written fakes), and what was stored or tracked is checked with `Received()`, including that a new product is stored before the snapshot is told about it.
- Application: `CatalogImporter` (every report field, a rejected row storing nothing), the `Stream.ReadProductEntriesAsync` extension that parses the upload (numbers and booleans read as text, `null` rows kept, anything that is not an array refused) and `CatalogImportService` (commit on success, no transaction for an invalid file, rollback on failure).
- Api: `GlobalExceptionHandler` (every exception type to its status code, and an unexpected exception's message never reaching the client).
- Infrastructure: the schema migration (shape, idempotency, legacy rows kept and converted to text, the unique index, a missing database failing clearly) and the unit of work (parameterized storage, a stored product reaching the snapshot only once tracked, round trip of a link, rollback without commit, a second unit of work reporting the catalog as busy while the first holds the write lock, and a commit blocked by another connection's reader reporting it as busy and storing nothing).

## Known Limitations

- Levenshtein measures characters, not meaning. If one brand sold two variants whose names differ by a short word (for example `Multivitamin Men` and `Multivitamin Women`, similarity `0.889`), they would be merged. In the provided catalog these two have different brands, so the brand restriction prevents it. The default threshold (`0.81`) was calibrated on only 4 pairs from the provided fixture (2 known matches, 2 known near-misses) with a narrow margin (`0.792`–`0.826`); this is a small-sample calibration, not a generally validated value.
- Approximate matching never applies to entries without a `Brand`: there is no comparison group to fall back to, so a typo variant of an unbranded product always creates a new product. This is a deliberate trade-off — removing the brand filter would reintroduce the false-merge risk the filter exists to prevent.
- Word-reordering in `Name` (for example `"Memory Foam Queen Mattress"` vs `"Mattress Memory Foam Queen"`) is not detected by either matching stage; this is treated as the same class of limitation as cross-language differences.
- `SellerName` is compared exactly (only trimmed), so differently spelled or cased names of the same seller are treated as different sellers (decision #10).
- Designed to demonstrate understanding of the problem, not for production volumes: each approximate lookup compares against all products of the same brand, and `ICatalogSnapshot` loads the relevant catalog fully into memory per import.