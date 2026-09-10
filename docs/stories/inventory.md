# Stories — The shelf (INV)

> One file per epic. What a household has on hand: the checklist every "what can I make" answer is
> computed from. Read with **JJ-020** (ice and water are always available), **JJ-023** (boolean only,
> absence means not available) and `docs/DATA_MODEL.md`. Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — INV-1 shipped.

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

**Out of scope, deliberately:** quantities and "running low" are not in MVP (JJ-023), and adding a
household's own ingredient is the `INGREDIENT` epic.
