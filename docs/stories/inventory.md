# Stories — The shelf (INV)

> One file per epic. What a household has on hand: the checklist every "what can I make" answer is
> computed from. Read with **JJ-020** (ice and water are always available), **JJ-023** (boolean only,
> absence means not available) and `docs/DATA_MODEL.md`. Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — INV-1 (the shelf) and INV-2 (custom ingredients) shipped.

**Epic key:** `INV`

**Prerequisites:** CKTL-1 (the tables) and SEED-2 (ingredients to tick). No new packages.

**Depends on:** `SEED`. **Depended on by:** `MAKE` and `ALMOST` — both are queries over this table,
and an empty shelf makes them untestable and the app pointless.

---

### INV-1 — Tick what you have

**Status: ✅ Implemented.** `GET /api/inventory`, `PUT /api/inventory/{ingredientId}`, and the
`/shelf` screen.

**As a** member of a household
**I want** to tick the ingredients we actually own
**So that** the app can tell me what I can make right now

**Context / notes.** The simplest slice in the project and the one everything else waits on.

**`TenantInventory` is the one JiggerJot entity that is plainly `ITenantScoped`,** so the platform's
global filter, write stamping and generated RLS policy all cover it with nothing hand-written. After
the shared-catalog machinery of JJ-031, this handler is deliberately ordinary — and the tests for it
are dull, which is the news.

**The list is the whole catalog, not just what is ticked.** You cannot tick what you cannot see, and
absence of an inventory row is exactly what "not available" means (JJ-023).

**Unticking updates the row rather than deleting it.** "I checked and I do not have it" is worth
keeping apart from "I never looked" — a shopping list would want that difference — and everything
that reads the shelf filters on `is_available`, so absence and false behave identically to every
reader either way.

**Grouping is the design work, not the query.** 191 ingredients is far too many to tick down a flat
list, and a shelf nobody can fill in is a shelf nobody fills in — after which the whole app has
nothing to work with. The API returns each row's category so every client groups the same way, and
the screen adds search and an "only what I have" filter for once a shelf is full.

**Ticking is optimistic and rolls back on failure.** 191 checkboxes should feel like checkboxes
rather than form submissions; the rollback is what keeps that honest instead of a lie the user finds
out about later.

**Ice and water never appear.** They were kept out of the catalog at seed time (JJ-020), so the test
here is really a guard against putting them back — a shelf that asks whether you have water is a
shelf nobody trusts.

**Acceptance criteria**

```gherkin
Scenario: A new household starts with an empty shelf
  When I open the shelf
  Then I see the whole catalog
  And nothing is ticked

Scenario: Ticking an ingredient is remembered
  When I tick an ingredient
  And I reload the page
  Then it is still ticked

Scenario: Unticking puts it back
  Given I have ticked an ingredient
  When I untick it
  Then it no longer counts as available

Scenario: The shelf is private to my household
  Given another household has ticked something
  When I open my shelf
  Then nothing of theirs is ticked on mine

Scenario: I cannot tick something I am not allowed to see
  When I try to tick another household's own ingredient
  Then it is not found

Scenario: Water and ice are never on the shelf
  When I open the shelf
  Then neither is offered, because both are always available
```

**Tests.** `tests/Api.Tests/Catalog/InventoryTests.cs` (ten) and
`tests/E2E.Tests/ShelfJourneyTests.cs` (suite 39 → 40). The journey reloads after ticking, which is
the only way to see that an optimistic checkbox actually reached the database rather than just the
screen.

**Out of scope, deliberately:** quantities and "running low" are not in MVP (JJ-023).

---

### INV-2 — Add a custom ingredient inline

**Status: ✅ Implemented.** `POST /api/inventory/ingredients` and `GET /api/inventory/categories`,
plus the add form on the `/shelf` screen. Closes the bullet of **FEATURES §8** that INV-1 shipped
without: *"Add a custom ingredient inline (name + category + subcategory) → creates a tenant-owned
`Ingredient` and is immediately checkable."*

**As a** member of a household
**I want** to add a bottle the catalog has never heard of
**So that** my shelf is the truth about my shelf, and the makeable engine works from it

**Context / notes.** Small on the surface, and it is the app's **first write to a dual-natured
catalog table** — which is what earns it more tests than its size suggests.

**Three platform guarantees skip these tables (JJ-031), and two of them bite here.** Nothing stamps
`TenantId`, so the handler sets it by hand: a row written without that line lands in the *shared*
catalog, visible to every household on the platform. The RLS insert policy would refuse the attempt,
which is the backstop doing its job, but the fix belongs in the handler and a test asserts the column.
And the platform's dissolution canary only sees a non-nullable `TenantId`, so it cannot see this table
at all — until this slice no household could put a row in it and the gap was theoretical. A test now
adds one, ticks it, dissolves the household, and checks both rows are gone and the shared catalog is
untouched.

**Ticked on arrival, not merely tickable.** Someone adds a bottle to their shelf because it is on
their shelf; making them add it and then tick it is two actions for one intent. It unticks like any
other row. The flow says "immediately checkable", which this satisfies and then some — it is the one
place this slice reads past the literal wording, and it is recorded here rather than assumed.

**The duplicate check is wider than the unique index, on purpose.** The index is keyed on
`(TenantId, Name)`, so a household's "campari" and the catalog's "Campari" are different rows to the
database. Two Camparis on one shelf is nobody's intent, and the custom one would satisfy recipe lines
by exact name only (JJ-018) — so it would quietly not do what its owner expected. The handler compares
case-insensitively across everything the household can see, and the 409 carries
`existingIngredientId` so the UI can point at what is already there instead of leaving someone to hunt
for a name they just typed. The screen searches for it rather than ticking it: pointing is not a write
nobody asked for.

**Categories are chosen, not typed.** The lookups are curated and global with no household additions
(JJ-022), so the form reads the two-level tree from a new endpoint. The pairing is validated —
top-level category, and a subcategory that is one of *its* children — because filtering a parent
matches all its children (JJ-016), so a mismatched pair breaks filtering as well as browsing.

**Acceptance criteria**

```gherkin
Scenario: Adding a bottle the catalog does not list
  Given I am at my shelf
  When I add "Homemade coffee liqueur" under a category
  Then it appears on my shelf, ticked
  And it is marked as mine

Scenario: It belongs to my household and nobody else's
  Given I added a custom ingredient
  Then its row carries my household id
  And another household neither sees it nor can tick it by id

Scenario: The same name twice is refused, and points at what is there
  Given I added "Sloe gin infusion"
  When I add "SLOE GIN INFUSION"
  Then I am told it is already in the list
  And only one row exists

Scenario: A name the shared catalog already has is refused too
  When I add "campari"
  Then I am pointed at the catalog's Campari

Scenario: A name that is not a name is refused
  When I add a blank name
  Then nothing is written

Scenario: The category has to make sense
  When I pass an unknown category
  Or a subcategory belonging to a different category
  Or a subcategory where a category belongs
  Then nothing is written

Scenario: No subcategory is fine
  When I add an ingredient with a category only
  Then it files at the top level

Scenario: Dissolving my household takes it with me
  Given I added and ticked a custom ingredient
  When my household is dissolved
  Then the ingredient and its shelf row are gone
  And the shared catalog is untouched
```

**Tests.** `tests/Api.Tests/Catalog/CustomIngredientTests.cs` (fifteen, including the dissolution
canary) and one journey in `tests/E2E.Tests/ShelfJourneyTests.cs` (suite 44 → 45), which adds a
bottle, reloads to prove it was written rather than drawn, and then tries to add it again.

**Out of scope, deliberately:** custom ingredients in the substitution graph — both ends of a
substitution must be shared rows (JJ-005, JJ-018), and the query filter on `IngredientSubstitution`
enforces it — plus editing or deleting a custom ingredient, which nothing has asked for yet, and
household additions to the curated lookups (JJ-022).
