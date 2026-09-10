# Stories — Browsing & filtering (FILTER)

> One file per epic. Exploring the whole catalog rather than only what the shelf can make. Read with
> **JJ-014** (no manual "main spirit" field), **JJ-015** (two-level categories), **JJ-016** (match by
> category AND by name) and **JJ-034** (glass and method are optional). Stories use Gherkin
> acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — FILTER-1 shipped.

**Epic key:** `FILTER`

**Prerequisites:** `CKTL-2` (the browse list this filters), `SEED` (a catalog with categories on its
ingredients). No new packages, no schema change, no migration.

**Depends on:** `CKTL`. **Depended on by:** nothing — but it is where the "a filter, not a screen"
decision taken in MAKE-1 and ALMOST-1 finally pays.

---

### FILTER-1 — Filter the catalog

**Status: ✅ Implemented.** Four query parameters on `GET /api/cocktails`, a
`GET /api/cocktails/filters` endpoint for the dropdowns, and a filter panel on `/cocktails`.
Implements the rest of **FEATURES §11**.

**As a** member of a household
**I want** to narrow the catalog by what a drink is made with, how it is made, and how it is served
**So that** I can find something to pour without knowing its name

**Context / notes.** Four filters, of which three are an equality check and one has an argument
behind it.

**There is no "main spirit" column and there never will be (JJ-014).** A drink with two spirits, or
none, makes that field a lie, and someone has to keep it true by hand for every recipe forever. So
the ingredient filter reads the recipe lines instead. That is more powerful as well as more honest:
it can answer "everything with elderflower", which no editor would have thought to tag.

**One box, three matches.** The text is compared against the ingredient's **name**, its **category**
and its **subcategory** at once (JJ-015, JJ-016). That is what makes a parent catch every child —
"rum" finds the dark and the white and the spiced, "dark rum" finds only the dark, and nobody has to
know which of the three their word happens to be. Matching only the name would fail the first case;
matching only the category would fail the third.

**The dropdown options come from the catalog, not from the lookup tables.** The curated lookups hold
nineteen glasses and ten methods; the shipped 31-recipe catalog uses a fraction of them, and a filter
whose options mostly return nothing reads as broken rather than as precise. Deriving them from the
cocktails the household can see also means the lists grow by themselves when the full catalog is
switched on, and that a household's own cocktails contribute their glasses while another's never do.

**A recipe that never stated a glass does not match a glass filter** (JJ-034). No glass is not a
glass, and sweeping the quarter of the catalog that leaves it unstated into whichever glass was asked
for would be inventing a fact about the drink.

**Everything combines, and it is an AND.** With each other, with the name search, and with both
makeability toggles — "gin drinks I can make tonight" is one request. This is the pay-off for the
decision taken in MAKE-1 and repeated in ALMOST-1: had either been a screen of its own, each would
now need its own copy of the search box and its own four dropdowns.

**A bad id matches nothing rather than failing.** The same reasoning that clamps an out-of-range page
number instead of rejecting it: this is a read, and a 400 helps nobody who mistyped a query string.
Wildcards in the ingredient text are escaped, so `%` looks for a percent sign — without that it would
return the whole catalog and look for all the world like a working filter.

**Acceptance criteria**

```gherkin
Scenario: By ingredient name
  When I filter by "Campari"
  Then every result has a Campari line

Scenario: By category, where no ingredient carries that name
  When I filter by "Amaro and bitter"
  Then the Negroni is there
  And no result is called that

Scenario: A parent category catches every child
  When I filter by "gin"
  Then every result has a line filed under gin by name, category or subcategory

Scenario: By method, glass and serving type
  When I filter by any of them
  Then every result matches
  And a recipe that never stated a glass is not swept into a glass filter

Scenario: Filters combine
  When I filter by ingredient and method together
  Then the list is narrower than either alone
  And the same holds with the search box and the makeability toggles

Scenario: A filter that matches nothing
  When I pass an unknown id, or a word nothing uses
  Then I get an empty page rather than an error

Scenario: A wildcard is text
  When I filter by "%"
  Then nothing matches

Scenario: The count follows the filter
  When a filter is on
  Then the total counts the filtered catalog, so the pager offers pages that exist

Scenario: The dropdowns only offer what works
  When I open the filters
  Then every method and glass listed returns at least one drink
```

**Tests.** `tests/Api.Tests/Catalog/CatalogFilterTests.cs` (fifteen) and one journey in
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` (suite 45 → 46), which filters by an ingredient no
drink is named after, stacks a method on top, and clears back to the whole catalog.

**Out of scope, deliberately:** filtering by source book, by ingredient count or by ABV — none is in
`PROJECT_BRIEF`. Saved or shareable filter sets likewise. The ingredient filter takes one term rather
than several, because "gin AND lime" is a different feature from "made with gin" and nothing has
asked for it.
