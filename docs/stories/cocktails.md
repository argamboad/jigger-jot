# Stories — Cocktail catalog (CKTL)

> One file per epic. The catalog is the substrate the rest of JiggerJot stands on: the shared, seeded
> recipe collection every household reads, and the household's own rows that live in the same tables
> beside it. Design decision + the shape's constraints in **JJ-031** (read with platform ADR-003 and
> ADR-020); entity-by-entity detail in `docs/DATA_MODEL.md`. Stories use Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — CKTL-1 (domain model + tenancy walls) ✅; browse/detail/filter to follow.

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

### CKTL-2 — Browse the catalog 📋 PLANNED

**As a** member of a household
**I want** a paged, searchable list of cocktails
**So that** I can find a drink without knowing its name in advance

Reads shared + household rows through the filter established in CKTL-1. Name search, stable ordering,
paging. No filtering by ingredient or category yet — that is `FILTER`.

### CKTL-3 — Cocktail detail 📋 PLANNED

**As a** member of a household
**I want** to open a cocktail and see its recipe
**So that** I can make it

Lines in `display_order` with role grouping, glass, method, serving type and instructions. Amounts
render in the viewing user's `preferred_unit_system`, converted at display only, with neutral units
passing through unchanged (JJ-007, JJ-008).
