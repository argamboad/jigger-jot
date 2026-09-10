# Stories — Authoring a cocktail (AUTHORING)

> One file per epic. A household writes its own recipe, from nothing. Read with **JJ-034** (glass and
> method are optional), **JJ-009**/**JJ-010** (optional lines and roles), **JJ-003**/**JJ-014**
> (makeability and filtering are derived, never stored) and **JJ-031** (nothing stamps these tables).
> Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — AUTHORING-1 shipped. `AUTHORING-2` (editing) is the open follow-on.

**Epic key:** `AUTHORING`

**Prerequisites:** `CKTL-1` (the tables), `SEED` (ingredients and lookups to choose from), `INV-2`
(so a household can name what it actually owns). No new packages, no schema change, no migration.

**Depends on:** `CKTL`, `INV`. **Depended on by:** nothing yet — `AUTHORING-2` would let a household
edit what this creates, and a fork.

---

### AUTHORING-1 — Write a cocktail from scratch

**Status: ✅ Implemented.** `POST /api/cocktails`, `GET /api/cocktails/lookups`, and the
`/cocktails/new` form. Implements **FEATURES §14**.

**As a** member of a household
**I want** to write down a drink of my own
**So that** the app knows about the cocktail I actually make

**Context / notes.** The first real form in the app, and the flow's last line is the one worth
reading: *"immediately participates in makeable / filtering like any other cocktail."*

**It does, and it costs nothing.** Both are derived from the recipe lines at query time (JJ-003,
JJ-014), so a drink written a second ago is exactly as visible to the engine as one seeded from a
1930 book. There is no index to rebuild and no tag to remember to set. Two tests prove it rather than
assume it, because "for free" is the kind of thing that stops being true quietly.

**Two lookup endpoints that must not be merged.** `GET /api/cocktails/filters` is derived from the
catalog, so a filter never offers a glass that returns nothing. `GET /api/cocktails/lookups` is the
whole curated lookup (JJ-022), because a household writing down what it actually pours must be able
to reach a glass no seeded recipe happens to use. Locally that is twenty glasses against twelve. They
answer different questions and a test asserts the difference.

**Glass and method stay optional** (JJ-034) and the form says *"Not stated"* rather than defaulting to
something plausible. A quarter of the seeded catalog is in that position and a person writing at their
own bar is often in it too.

**Line order is the array's.** `DisplayOrder` is assigned from position, so nobody types a number into
a form. The same ingredient may appear on several lines — FEATURES §14 says so, and there is
deliberately no unique index on (cocktail, ingredient), because a split pour is a real thing to write
down.

**Three refusals, each with a reason.** A cocktail with **no lines** is not merely empty but
misleading: makeability counts *unsatisfied required* lines, so a drink with none is "makeable" by the
letter of the rule and would sit in tonight's list made of nothing. A **unit with no amount** renders
as nothing at all — the display formatter shows an empty string when there is no amount — so it would
be a line its author could never see; the other way round is fine and means what it says, one egg. And
a line naming an **ingredient this household cannot see** is refused, because a reference to an
invisible row is how one household learns another exists.

**A bug the handler tests could not see.** The form sends `servingType: "FullDrink"` and `role:
"Base"` — the names, as any client would, and as `/lookups` hands them out. The request could not be
bound at all: nothing had ever sent this API an enum before, because every response turns them into
strings on the way out by hand. The converter is now on the two properties rather than configured
globally, since changing how the whole surface serialises would be a contract change nobody asked for.
`CocktailAuthoringEndpointTests` is the layer that catches this class of failure, and it is the second
time this project has needed reminding that a handler test cannot see model binding.

**Acceptance criteria**

```gherkin
Scenario: Writing a cocktail
  When I write a name, instructions and two lines
  Then I get a recipe with those lines in the order I wrote them
  And it is mine, and nobody else's to see

Scenario: It joins the app immediately
  Given I have its ingredients on my shelf
  Then it is in "what can I make"
  And it answers the ingredient filter

Scenario: Glass and method are optional
  When I leave them unstated
  Then the recipe simply does not say

Scenario: The same ingredient may appear twice
  Then both lines are kept

Scenario: My own custom ingredient can go in my own recipe
  Then it is accepted

Scenario: Another household's ingredient cannot
  Then nothing is written

Scenario: A name that is not a name is refused
Scenario: A recipe with no lines is refused
Scenario: An amount of zero or less is refused
Scenario: A unit with no amount is refused
Scenario: An amount with no unit is fine

Scenario: An unknown glass, method or unit is refused

Scenario: The form offers the whole curated lookups
  Then it can reach a glass no seeded recipe uses

Scenario: Enums cross the wire by name
  When a client posts "FullDrink" and "Base"
  Then the request binds
```

**Tests.** `tests/Api.Tests/Catalog/CocktailAuthoringTests.cs` (twenty),
`tests/Api.Tests/Catalog/CocktailAuthoringEndpointTests.cs` (two, over real HTTP) and one journey in
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` (suite 47 → 48), which writes a two-line drink and
finds it makeable and in the catalog.

**Out of scope, deliberately:** **editing** an existing cocktail — including a fork, which FORK-1 left
to this epic — is `AUTHORING-2`. It needs this form again in an edit shape plus a decision about what
happens to a recipe someone else in the household is reading, and folding it in here would have
doubled the slice. Deleting a household cocktail is likewise unasked-for. Photographs, tags, ratings
and per-recipe notes are not in `PROJECT_BRIEF`.
