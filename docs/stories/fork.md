# Stories — Create my own version (FORK)

> One file per epic. A household adapts a cocktail by copying it, never by changing the shared one.
> Read with **JJ-002** (the catalog is referenced, with copy-on-fork), **JJ-013** (a fork is a
> snapshot, not a live reference), **JJ-031** (nothing stamps these tables) and **JJ-032**
> (attribution is a real claim). Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — FORK-1 shipped.

**Epic key:** `FORK`

**Prerequisites:** `CKTL-1` (the tables), `CKTL-3` (the screen the button lives on). No new packages,
no schema change, no migration — `ForkedFromCocktailId` has been on `Cocktail` since CKTL-1, waiting
for something to write it.

**Depends on:** `CKTL`. **Depended on by:** `AUTHORING`, which reuses the same write path in reverse —
a household-owned cocktail with no original behind it.

---

### FORK-1 — Create my own version

**Status: ✅ Implemented.** `POST /api/cocktails/{id}/fork` and a button on the recipe page.
Implements **FEATURES §13**.

**As a** member of a household
**I want** my own copy of a recipe I can change
**So that** my version of a drink is mine, and stays put when the original changes

**Context / notes.** The endpoint is twenty lines. What is worth reading is the three things it
deliberately does not do.

**It does not mutate the shared catalog.** That is the whole architecture (JJ-002): households
reference the global catalog and personalise by copying. The alternative — a copy of the catalog per
household — was rejected before any of this was built.

**It does not reference the original.** A fork is a **snapshot**: a new tenant-owned `Cocktail` plus
copies of every recipe line, amounts exactly as authored (JJ-013). Half the tests in this slice exist
to prove the copy stays put when the original moves — edit the original, delete the original, fork it
twice. A reference implementation would pass the first test in the file and fail every one of those.

**It does not carry the credit across.** The book wrote the original, not this household's version of
it, and attribution is a real claim rather than decoration (JJ-032). Copying `SourceId` would
attribute whatever the household does next to Craddock or the IBA. Provenance rides on
`ForkedFromCocktailId` instead, and the detail page renders it as *"Based on Adonis Cocktail"* — a
link, not a byline. This is the one place the slice made a call `FEATURES.md` does not spell out.

**`ForkedFromCocktailId` is not a foreign key, and this is the slice that proves why.** Deleting the
original neither blocks nor cascades: the copy survives and simply stops being able to say where it
came from. There is a test that deletes a seeded cocktail and reads the fork back.

**`TenantId` is set by hand on two tables at once** (JJ-031). Nothing stamps either — the interceptor
keys off `ITenantScoped` and these tables deliberately are not. A line written without one would land
in the shared catalog attached to a household's recipe, which is the worst of both and invisible until
it surfaced in someone else's app.

**Landing on the copy is the point.** Someone forks a drink because they want to change it; leaving
them looking at the original would be the one screen where they cannot.

**Acceptance criteria**

```gherkin
Scenario: Forking copies the whole recipe
  When I create my own version of a cocktail
  Then I get a copy with the same name, glass, method and instructions
  And every line, in order, with its amounts as authored

Scenario: The copy is mine
  Then the cocktail and every line carry my household id
  And another household neither sees it nor can fork it

Scenario: The copy records what it came from
  Then it names the original as provenance
  And forking a fork records the one it came from, not the root

Scenario: The copy takes no credit from the book
  Then the original still credits its source
  And the copy credits nothing

Scenario: Editing the original never touches the copy
  When the original is renamed and a line removed
  Then my copy is unchanged

Scenario: Deleting the original leaves the copy standing
  When the original is deleted
  Then my copy is still there with all its lines
  And it no longer says what it came from

Scenario: Forking twice makes two independent copies
  When I fork the same drink twice and rename one
  Then the other is unchanged

Scenario: The copy appears in the catalog
  When I browse
  Then my copy is there beside the original, marked as mine

Scenario: Dissolving my household takes the copy and its lines
  And leaves the shared catalog exactly as it was
```

**Tests.** `tests/Api.Tests/Catalog/CocktailForkTests.cs` (twelve) and one journey in
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` (suite 46 → 47), which forks a drink, checks the copy
opens with its lines and its provenance, and goes back to find the original untouched.

**Out of scope, deliberately:** **editing** the copy is `AUTHORING`, which needs the same form a
from-scratch cocktail needs — a fork you cannot yet edit is still worth having, because it is the
household's and the catalog can no longer change it underneath them. Deleting a household cocktail is
likewise unasked-for. Nothing walks the fork chain: provenance is one hop, and a lineage view is not
in `PROJECT_BRIEF`.
