# Stories — One ingredient away (ALMOST)

> One file per epic. The other half of "what can I make": the drinks a household is **exactly one**
> required line short of, each one naming the bottle that would unlock it. Read with **JJ-019**
> (derived, never stored), **JJ-004**/**JJ-006** (substitutions, directed) and **JJ-009** (optional
> lines never block). Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — ALMOST-1 shipped.

**Epic key:** `ALMOST`

**Prerequisites:** `SEED`, `INV-1` (a shelf) and `MAKE-1` (the query this narrows). No new packages,
no schema change, no migration.

**Depends on:** `MAKE`. **Depended on by:** `CKTL-4`, which shows the same status on the detail page.

---

### ALMOST-1 — The drinks I am one ingredient away from

**Status: ✅ Implemented.** `GET /api/cocktails?almost=true` and a second toggle on the catalog
screen. Implements **FEATURES §10**, presented through **§11**.

**As a** member of a household
**I want** to see the drinks I am one bottle short of, and which bottle that is
**So that** the next thing I buy unlocks something instead of sitting on the shelf

**Context / notes.** The requirement is two sentences and only one of them is about a filter.

**The name is the feature.** "Exactly one required line unsatisfied" on its own produces a list of
drinks the household cannot make, which it could already get by reading the catalog. What makes it a
shopping driver is `missingIngredient` on every row: *buy this, unlock these*. Every test in the
suite asks what is missing, not just what is listed, for that reason.

**"After substitutions" is the whole difficulty.** A White Lady wants gin, Cointreau and lemon juice.
A shelf holding gin and Curaçao is **one** ingredient away, not two — the graph covers the Cointreau —
and the missing bottle is the lemon juice. Counting unstocked lines instead would drop the drink from
the list entirely, and naming the first unstocked line would send someone out to buy Cointreau they
do not need. The filter and the name therefore run **character-for-character the same predicate** as
MAKE-1's, once counted and once selected, so the two can never disagree about which lines count.

**Adjacent, never overlapping.** FEATURES §10 asks for this "adjacent to what you can make". Zero
short is makeable; one short is this; no drink is both. The API keeps that literal — sending
`makeable=true&almost=true` applies both predicates and returns an empty page, rather than one flag
quietly winning over the other — and the UI keeps the pair honest by making the two switches
exclusive, so nobody can reach the contradiction by clicking.

**A filter, not a screen**, for the same reason MAKE-1 is: §11 puts these on the browse list with
search and paging, combinable with the ingredient, method, glass and serving-type filters `FILTER`
will add. It shares the empty state's link to the shelf, with its own wording — "no cocktails match
that" would read as a broken catalog when the real answer is that the shelf is more than one bottle
short of everything.

**Still says what you would pour.** FEATURES §9 does not stop applying because a drink is one short.
A row that reads "add lemon juice and you can make this" while silently planning to pour Curaçao for
the Cointreau would send someone to the shop and still leave them short at the shelf.

**Derived at query time, never stored** (JJ-003, JJ-019), like makeability. The journey test buys the
missing bottle and watches the drink move from one list to the other, with nothing recomputed and
nothing invalidated.

**Acceptance criteria**

```gherkin
Scenario: One line short is listed, and names the bottle
  Given I have ticked gin and Campari
  When I ask what I am one ingredient away from
  Then the Negroni is there
  And it tells me to add sweet vermouth

Scenario: Two lines short is not listed
  Given I have ticked gin only
  Then the Negroni is not there

Scenario: A drink I can already make is not listed
  Given I have ticked gin, Campari and sweet vermouth
  Then the Negroni is not one ingredient away
  And it is makeable

Scenario: A line a substitute covers is not what I am missing
  Given a recipe asks for Cointreau and lemon juice
  And I have gin and Curaçao
  Then the drink is one ingredient away
  And the ingredient named is the lemon juice

Scenario: An almost-makeable drink still says what I would pour
  Given the same shelf
  Then the row tells me I would be pouring Curaçao in place of Cointreau

Scenario: An optional line is never what I am missing
  Given I have every required ingredient but not the garnish
  Then the drink is makeable, not one ingredient away

Scenario: Search narrows what is one away
  Given I have ticked gin and Campari
  When I search for "negroni"
  Then only matching names come back

Scenario: Another household's shelf changes nothing here
  Given another household has ticked gin and Campari
  And mine is empty
  Then the Negroni is not one ingredient away for me

Scenario: Browsing everything names nothing missing
  When the filter is off
  Then no row claims a missing ingredient

Scenario: Asking for both filters asks for nothing
  When I ask for makeable and one-away together
  Then the page is empty
```

**Tests.** `tests/Api.Tests/Catalog/AlmostMakeableTests.cs` (ten) and one journey in
`tests/E2E.Tests/MakeableJourneyTests.cs` (suite 42 → 43) which buys the missing bottle and watches
the drink cross from one list to the other.

**Out of scope, deliberately:** "N short" for N greater than one — the MVP fixes N = 1 (DATA_MODEL),
and two away is a wish list rather than a shopping list. A real shopping list that ranks bottles by
how many drinks each unlocks is a plausible follow-up and is not in `PROJECT_BRIEF`. Showing this
status on the **detail** page is `CKTL-4`.
