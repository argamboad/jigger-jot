# Stories — What can I make (MAKE)

> One file per epic. The question the whole app exists to answer. Read with **JJ-003** and **JJ-019**
> (derived, never stored), **JJ-004**/**JJ-006** (substitutions, directed), **JJ-009** (optional lines
> never block) and **JJ-020** (ice and water are always available). Stories use Gherkin acceptance
> criteria.
> **Status: 🚧 IN PROGRESS** — MAKE-1 shipped; `ALMOST` is the sibling epic.

**Epic key:** `MAKE`

**Prerequisites:** `SEED` (a catalog and a substitution graph) and `INV-1` (a shelf). No new packages.

**Depends on:** `INV`. **Depended on by:** `ALMOST`, which is the same query asking for one missing
line instead of none.

---

### MAKE-1 — Everything I can make right now

**Status: ✅ Implemented.** `GET /api/cocktails?makeable=true` and the toggle on the catalog
screen. Implements **FEATURES §9**, filtered through **§11**.

**As a** member of a household
**I want** to see every drink my shelf can actually make
**So that** I can pour something tonight without reading nine hundred recipes

**Context / notes.** The whole differentiator in one query, and the shape of it is the interesting
part.

**Derived at query time, never stored** (JJ-003, JJ-019). There is no `is_makeable` column and there
must never be one: the answer changes the instant a shelf changes or a recipe is edited, and a cached
flag would be wrong in between with nothing to say so. The journey test unticks an ingredient and
looks again precisely to show the difference between derived and computed-once.

**A drink qualifies when every required line is satisfied** — by the exact ingredient, or by anything
the substitution graph allows **in that direction**. Reading the graph directionally is what keeps a
one-way substitution one-way: cognac stands in for brandy, brandy does not stand in for cognac, and a
household with only brandy is not offered a drink it cannot actually make well.

**Optional lines never block** (JJ-009), which is why a garnish is modelled as a line rather than a
special case. Ice and water never appear at all (JJ-020) because they were kept out of the catalog.

**A filter, not a screen.** FEATURES §11 lists "makeable now" as an on/off filter on the browse
list, combinable with the ingredient, method, glass and serving-type filters that `FILTER` will add.
So it is a toggle on `/cocktails`, sharing that list's search and paging, rather than a separate route
and nav entry. The empty state links to the shelf when the filter is what emptied it, because that is
almost always the reason.

**A substituted result says what you would actually pour.** FEATURES §9 requires it — "using Kahlúa in
place of Tia Maria" — and it is the difference between a list that is right and a list that is useful.
Being told you can make a White Lady and finding no Cointreau at the shelf is worse than not being
told. Each row carries the swaps in play, one suggestion per asked-for ingredient; the list is empty
when the household can pour the drink as written, and empty on an unfiltered browse, where a row makes
no makeability claim at all.

**A model warning closed on the way past.** `Ingredient` carries a query filter and is the required
end of both relationships on `IngredientSubstitution`, so EF warned that a row's required navigation
could filter away to null. It cannot, because both ends of a substitution are shared catalog
ingredients (JJ-005) — and that invariant is now a query filter on `IngredientSubstitution` saying so,
rather than prose in a decision log. The warning is gone because the model states the rule.

**Acceptance criteria**

```gherkin
Scenario: An empty shelf makes nothing
  Given I have ticked nothing
  When I open "Make now"
  Then I am told nothing is makeable yet
  And I am pointed at my shelf

Scenario: Stocking every line makes the drink
  Given I have ticked gin, Campari and sweet vermouth
  When I open "Make now"
  Then the Negroni is there

Scenario: Missing one line does not
  Given I have ticked gin and Campari only
  Then the Negroni is not there

Scenario: A substitute counts, and says so
  Given a recipe asks for Cointreau
  And I have Curaçao
  Then the drink is makeable
  And it tells me I would be pouring Curaçao in place of Cointreau

Scenario: A drink I can pour as written claims no substitution
  Given I have exactly what the recipe asks for
  Then no swap is suggested

Scenario: Browsing everything claims nothing about makeability
  When the makeable filter is off
  Then no row suggests a substitution

Scenario: A one-way substitution does not run backwards
  Given a recipe asks for cognac
  And I have only brandy
  Then the drink is not makeable

Scenario: An optional line never blocks
  Given I have every required ingredient but not the garnish
  Then the drink is makeable

Scenario: Taking something off the shelf takes the drink with it
  Given the Negroni was makeable
  When I untick Campari
  Then it is no longer makeable

Scenario: Another household's shelf changes nothing here
  Given another household has stocked a full Negroni
  And mine is empty
  Then I can make nothing
```

**Tests.** `tests/Api.Tests/Catalog/MakeableTests.cs` (twelve) and two journeys in
`tests/E2E.Tests/MakeableJourneyTests.cs` (suite 40 → 42).

> **This slice was built twice.** The first version invented a separate `/make` screen and said
> nothing about substitutions, because it was written from the golden rules and the decision log
> without reading `FEATURES.md` — which specifies both. The engine was right; the shape and the
> surfacing were not. Read the flow first.

**Out of scope, deliberately:** one-ingredient-short is `ALMOST`, and filtering by spirit or category
is `FILTER`.
