# Stories — Cocktail catalog (CKTL)

> One file per epic. The catalog is the substrate the rest of JiggerJot stands on: the shared, seeded
> recipe collection every household reads, and the household's own rows that live in the same tables
> beside it. Design decision + the shape's constraints in **JJ-031** (read with platform ADR-003 and
> ADR-020); entity-by-entity detail in `docs/DATA_MODEL.md`. Stories use Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — CKTL-1 (domain model + tenancy walls) ✅, CKTL-2 (browse) ✅; detail
> and filtering to follow.

**Epic key:** `CKTL`

**Prerequisites (external, before any code):** none — the platform ships everything this needs
(tenancy, RLS backstop, dissolve/export contributors, repositories). No new packages.

**Reuses:** `ITenantScoped` + the global query filter (ADR-003), `RlsDdl` and the RLS backstop
(ADR-020), `IRepository<T>`, `ITenantDataContributor` (ADR-011).

**Depends on:** nothing. **Depended on by:** `INGREDIENT`, `INV`, `MAKE`, `ALMOST`, `FORK`,
`AUTHORING`, `FILTER`, `SEED` — every one of them reads or writes these tables.

---

### CKTL-1 — Domain model and the two tenancy walls

**Status: ✅ Implemented** (`feat/CKTL-1-domain-model`). Nine entities, their configurations, one
migration, and the walls that make the dual-natured tables safe.

**As a** member of a household
**I want** the shared cocktail catalog and my household's own recipes to live side by side
**So that** I can browse everything I'm allowed to see without ever seeing another household's

**Context / notes.** The whole slice turns on one shape: `Ingredient`, `Cocktail` and
`CocktailIngredient` each hold **both** shared catalog rows (`TenantId` null) and household rows
(`TenantId` set), which is why they implement
[`ISharedOrTenantScoped`](../../src/Core/Entities/ISharedOrTenantScoped.cs) rather than `ITenantScoped`
— the platform interface's `TenantId` is non-nullable and its filter and policy both test
`TenantId = current`, so a shared row would be invisible in the app *and* at the database (JJ-031).

Everything else is ordinary. `TenantInventory` is plain `ITenantScoped` household data and inherits the
platform's filter, write stamping and generated policy untouched. `IngredientCategory`, `GlassType`,
`Method`, `Unit` and `IngredientSubstitution` are curated global lookups with no tenant column at all
(JJ-005, JJ-022).

Three things are deliberately **not** in the schema, and each has a decision behind it: no "main
spirit" column (derived from recipe lines and categories — JJ-014), no `is_makeable` flag (derived at
query time — JJ-003, JJ-019), and no foreign key on `Cocktail.ForkedFromCocktailId` (a fork is a
snapshot, so it must survive its original being deleted — JJ-013).

**What the platform does not give these three tables** — each replaced here by hand:

| Platform guarantee | Why it misses | Replacement |
|---|---|---|
| `TenantStampingInterceptor` stamps writes | keys off `ITenantScoped` | call sites set `TenantId` explicitly |
| `RlsMigrationGateTests` fails CI on a missing policy | inspects `ITenantScoped` tables only | `SharedOrTenantRlsMigrationGateTests` |
| `EveryTenantOwnedEntity_IsWiredIntoTenantDissolution` | inspects a **non-nullable** `TenantId` only | `SharedOrTenantDissolutionTests` + `CatalogDataContributor` |

**Acceptance criteria**

```gherkin
Scenario: A household sees the shared catalog and its own rows
  Given a shared catalog ingredient and one my household added
  And an ingredient another household added
  When my household lists ingredients
  Then I see the shared one and my own
  And I do not see the other household's

Scenario: The catalog is readable before a household is chosen
  Given no current household
  When cocktails are listed
  Then only shared catalog cocktails come back
  And nothing belonging to any household does

Scenario: A household cannot delete a shared catalog row
  Given a shared catalog ingredient
  When my household deletes it directly at the database
  Then no rows are affected
  And the ingredient is still there

Scenario: A household cannot rewrite a shared catalog row
  Given a shared catalog ingredient
  When my household updates its name directly at the database
  Then no rows are affected

Scenario: A household can delete its own row
  Given an ingredient my household added
  When my household deletes it directly at the database
  Then it is removed

Scenario: Creating a shared row requires the bypass GUC
  Given my household is current
  When a row with a null tenant is inserted
  Then the database rejects it with insufficient_privilege
  But the same insert succeeds when the bypass GUC is on

Scenario: Dissolving a household removes its catalog and leaves the shared one
  Given a shared cocktail, one my household authored, and one another household authored
  When my household is dissolved
  Then my cocktail and its recipe lines are gone
  And the shared cocktail and the other household's are untouched

Scenario: An export carries the household's own rows only
  Given a shared ingredient and one my household added
  When my household exports its data
  Then the export names my ingredient
  And it does not name the shared one
```

**Tests.** `tests/Api.Tests/Catalog/SharedCatalogFilterTests.cs` (EF filter, model-built database),
`tests/Api.Tests/Rls/SharedCatalogRlsTests.cs` (the same guarantee at the database, as the
non-privileged runtime role — a second wall only counts if it holds when the first is gone),
`tests/Api.Tests/Rls/SharedOrTenantRlsMigrationGateTests.cs` (all four policies survive a real
migration), `tests/Api.Tests/Catalog/SharedOrTenantDissolutionTests.cs` (canary + teardown + export).
`ArchitectureTests` now lists `TenantInventory` against `InventoryDataContributor`.

**Out of scope for this slice:** any endpoint, any UI, and any seed data. The tables exist and are
safe; filling and reading them is `SEED` and the browse slices below.

---

### CKTL-2 — Browse the catalog

**Status: ✅ Implemented.** `GET /api/cocktails` and the `/cocktails` screen — the first slice that
reads what CKTL-1 modelled and SEED-1 to SEED-4 filled.

**As a** member of a household
**I want** a paged, searchable list of cocktails
**So that** I can find a drink without knowing its name in advance

**Context / notes.** A handler, an endpoint, a page in the shared component library, and nothing
clever. What is worth reading is the four places the real data forced a decision.

**There is no tenant predicate in the query, and that is the point.** `Query()` carries the
shared-or-tenant filter, so the handler sees the shared catalog plus the household's own rows and can
no more leak across households than a platform slice can (JJ-031). A slice that re-spelled the
predicate by hand would be the one that eventually got it wrong.

**Ordering is name then id, because name alone is not a total order here.** Four names appear in both
source books and *Mr. Manhattan Cocktail* appears twice in the Savoy alone. Under a non-total order
Postgres is free to return page two overlapping page one, and it will do it intermittently — the
worst kind of bug to chase. There is a test that pages twice and counts distinct ids.

**Source is in the browse row, and it is load-bearing rather than decoration.** Without it the list
shows two rows called "Gin Fizz" and no way to tell which is which.

**Glass and method render as nothing when the recipe never said** (JJ-034). No "unknown" chip, no
guess. A quarter of the catalog is in that position.

**Two things the browser needed that the handler did not.** The search box is debounced at 300 ms, so
typing a name is one request rather than six. And every load takes a ticket that is checked before it
renders, because a slow response for "gin" must not overwrite the list a later request for "gimlet"
already painted. That race is invisible to a unit test and is the reason the journey test exists.

**Out of scope, deliberately:** filtering by ingredient, category or spirit is the `FILTER` epic, and
opening a drink is CKTL-3. This slice ends at a list.

**Acceptance criteria**

```gherkin
Scenario: The catalog is browsable on first sign-in
  Given a household that has added nothing
  When I open the cocktails page
  Then I see a page of drinks from the shared catalog
  And I am told how many there are in total

Scenario: Paging does not repeat or skip
  Given the catalog has more than one page
  When I read the first page and then the second
  Then no drink appears on both

Scenario: Search matches anywhere in the name
  When I search for "martini"
  Then "Dry Martini" is among the results
  And every result contains "martini"

Scenario: A search with no hits is an empty state
  When I search for something no drink is called
  Then I am told nothing matches
  And I am not shown an error

Scenario: I see the shared catalog and my own, never another household's
  Given another household has added a cocktail
  When I browse
  Then I see the shared catalog and my household's own
  And I do not see theirs

Scenario: Two books may share a drink's name
  When I search for "Gin Fizz"
  Then I see both, each showing which book it came from

Scenario: A recipe that never stated a glass shows none
  When I browse
  Then drinks with no recorded glass or method simply show neither

Scenario: An unreasonable page or page size is clamped
  When I ask for page 0, or for 5000 rows
  Then I get the first page, and at most 100 rows
```

**Tests.** `tests/Api.Tests/Catalog/CocktailBrowseTests.cs` (twelve, run against the real seeded
catalog rather than fixtures — ordering, paging and search are all things only volume exposes) and
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` (suite 35 → 36).

> **One test here was wrong before the code was.** The first ordering assertion re-derived the
> expected order with `StringComparer.OrdinalIgnoreCase` and disagreed with the database: Postgres
> files "Absinthe (Special) Cocktail" among the other Absinthes because its collation looks past the
> punctuation, while an ordinal comparer sorts "(" ahead of every letter. The database is right —
> that is what a person expects from an alphabetical list — so the test now asserts the order is
> stable rather than re-deriving it with a comparer that disagrees with the query.

### CKTL-3 — Cocktail detail 📋 PLANNED

**As a** member of a household
**I want** to open a cocktail and see its recipe
**So that** I can make it

Lines in `display_order` with role grouping, glass, method, serving type and instructions. Amounts
render in the viewing user's `preferred_unit_system`, converted at display only, with neutral units
passing through unchanged (JJ-007, JJ-008).
