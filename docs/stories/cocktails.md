# Stories — Cocktail catalog (CKTL)

> One file per epic. The catalog is the substrate the rest of JiggerJot stands on: the shared, seeded
> recipe collection every household reads, and the household's own rows that live in the same tables
> beside it. Design decision + the shape's constraints in **JJ-031** (read with platform ADR-003 and
> ADR-020); entity-by-entity detail in `docs/DATA_MODEL.md`. Stories use Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — CKTL-1 (domain model + tenancy walls) ✅, CKTL-2 (browse) ✅,
> CKTL-3 (detail) ✅. Filtering is the `FILTER` epic.

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

### CKTL-3 — Cocktail detail

**Status: ✅ Implemented.** `GET /api/cocktails/{id}` and the `/cocktails/{id}` screen.

**As a** member of a household
**I want** to open a cocktail and see its recipe
**So that** I can make it

**Context / notes.** Lines in display order, the glass and method when there are any, the
instructions, and the credit. The query is unremarkable; **amount display is the whole slice**, and
it is where JJ-007 and JJ-008 stop being sentences in a decision log.

**The arithmetic lives in Core, in `AmountDisplay`, with no database or HTTP near it.** Every rule is
a judgement worth being able to test on its own, and there are eleven tests doing exactly that.

| Rule | Why |
|---|---|
| Neutral units never convert | Rendering "1 barspoon" as "5 ml" invents a precision the recipe never had |
| Metric never uses fractions | "22 1/2 ml" is not something anyone has written; metric is decimal by construction |
| Ounces and parts always do | "0.75 oz" reads like a spreadsheet, "3/4 oz" reads like a recipe |
| Imperial → metric rounds to 2.5 ml | 1.5 oz is 44.36 ml, and every metric recipe in the world says 45 |
| Metric → imperial rounds to 1/4 oz | A jigger is marked in quarters; "0.68 oz" is not pourable |
| Rounding never reaches zero | A 2 ml dash becoming "0 oz" reads as *none*, which is worse than any rounding error |
| No preference means as authored | `PreferredUnitSystem` is nullable and null means "never chose" |
| An unrecognisable decimal stays a decimal | 0.37 is not a fraction anyone writes, and inventing "3/8" would misstate the recipe |

**Conversion happens on the server, and the authored values ride along anyway.** Each line carries
`amount` and `unit` exactly as stored plus a `display` string already rendered for the reader. One
implementation to get right rather than one per client, which matters more once there is a second
front end — and a client that wants to format its own still has everything it needs.

**A cocktail belonging to another household is a 404, not a 403.** The query filter simply does not
return the row; "forbidden" would be a claim the endpoint is in no position to make, and saying it
would confirm the row exists.

**The preference has no UI yet.** `PreferredUnitSystem` is read but nothing sets it, so today every
reader sees recipes as authored. The switcher belongs beside the language and theme controls in
Settings and is `PREFS` work, not this slice's.

**Acceptance criteria**

```gherkin
Scenario: Opening a drink shows its recipe
  When I open a cocktail from the list
  Then I see its ingredients in the order the recipe writes them
  And I see its method and the book it came from

Scenario: Amounts follow my preference
  Given a recipe written in millilitres
  When I read it having asked for imperial
  Then the amounts are shown in ounces
  And the recipe itself is unchanged

Scenario: A proportional recipe keeps its fractions
  Given a 1930 recipe written in proportions
  When I read it having asked for metric
  Then it still reads "2/3 part"

Scenario: With no preference I see the recipe as written
  Given I have never chosen a unit system
  Then amounts are shown exactly as the book wrote them

Scenario: Someone else's cocktail is not found
  When I open a cocktail belonging to another household
  Then I am told it is not in my catalog
```

**Tests.** `tests/Core.Tests/AmountDisplayTests.cs` (eleven, pure), plus
`tests/Api.Tests/Catalog/CocktailDetailTests.cs` (eight) and two more journeys in
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` (suite 36 → 38).

> **Two things the gates caught before review did.** The first draft reached for
> `IRepository<User>.QueryAllTenants()` to read the preference, and the architecture test that bans
> the cross-tenant escape hatch outside a data contributor failed it — correctly. `User` is a
> platform entity with its own repository, which is what the handler uses now. Separately, a Core
> test caught "22 1/2 ml" before any of this reached a screen.
