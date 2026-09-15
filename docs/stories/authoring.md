# Stories — Authoring a cocktail (AUTHORING)

> One file per epic. A household writes its own recipe, from nothing. Read with **JJ-034** (glass and
> method are optional), **JJ-009**/**JJ-010** (optional lines and roles), **JJ-003**/**JJ-014**
> (makeability and filtering are derived, never stored) and **JJ-031** (nothing stamps these tables).
> Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE** — AUTHORING-1 through AUTHORING-4 shipped, editing included.

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

---

### AUTHORING-3 — The ingredient suggests the line's role

**Status: ✅ Implemented (2026-09-15).** `GET /api/cocktails/roles` and the write form. Implements
**FEATURES §14**'s recipe lines.

**As a** member of a household writing a cocktail
**I want** each line's role filled in from the bottle I pick
**So that** I am not choosing "Juice" for lime juice by hand, and my drink reads like one from the books

**Context / notes.** The maintainer asked whether the role dropdown under each ingredient should filter
the ingredient list. It goes the other way: people pick the bottle first, a role is not a set of
bottles (gin is the base of a Martini and a modifier in a brandy drink), and the list is already
grouped by category. So picking the ingredient fills in the role — the maintainer's choice.

**The rule already existed, in Python.** `seed/build_cocktails.py` gave every seeded line its role
from the ingredient's top-level category. It now has a twin in Core, `RecipeRoles`, and
`SeedRolesParityTests` holds every line of the embedded catalog to it, so neither can change alone.
The first spirit is the base and every later spirit a modifier; juices, syrups, bitters and mixers
take their own role; fruit, herbs and garnishes are garnishes, and a garnish starts optional (JJ-009);
vermouths, liqueurs, amari, wines and beers are modifiers; everything else is other.

**The server suggests; the form asks.** `Shared.Ui` does not reference Core, and a second front end
should inherit the rule rather than copy it, so the form calls `GET /api/cocktails/roles` with the
whole recipe in order after every ingredient change and every removed line — the whole recipe, because
only the first spirit leads, and removing it promotes the next. Each ask carries a ticket and an
overtaken answer is dropped.

**A person always wins.** A role or required box someone changed by hand is never overwritten, however
often the suggestion is asked again.

**An ingredient the household cannot see is "Other".** A suggestion that read its real category would
tell one household what another keeps (JJ-031), so it answers exactly as it would for an unknown id.

**Acceptance criteria**

```gherkin
Scenario: Picking an ingredient fills in its role
  When I pick lime juice for a line
  Then its role is Juice, and it is required

Scenario: A garnish arrives optional
  When I pick mint
  Then its role is Garnish and "required" is unticked

Scenario: Only the first spirit is the base
  Given gin on one line
  When I add Campari and cognac
  Then gin is Base, and Campari and cognac are Modifiers
  And a vermouth written before the gin does not make the gin a modifier

Scenario: A role I chose is never overwritten
  Given I set a line's role or required box by hand
  When I pick or change its ingredient
  Then my choice stays

Scenario: Another household's ingredient suggests nothing
  When its id is asked about
  Then the answer is Other, the same as an unknown id

Scenario: The seeded catalog and the form agree
  Then every seeded line has the role the Core rule gives it
```

**Tests.** `tests/Core.Tests/RecipeRolesTests.cs` (new); `tests/Api.Tests/Catalog/SeedRolesParityTests.cs`
(new); a handler test in `CocktailAuthoringTests` and an HTTP binding test in
`CocktailAuthoringEndpointTests`; `tests/Ui.Tests/WriteRoleSuggestionTests.cs` (new, five); and the
authoring journey in `CocktailBrowseJourneyTests` now asserts Campari arrives as a modifier with nobody
choosing it (suite unchanged at 52).

**Out of scope:** a searchable ingredient picker (offered, not chosen then — it is AUTHORING-4);
suggesting roles on the seeded catalog, which already has them; re-suggesting a role on a fork, which
is AUTHORING-2's form.

---

### AUTHORING-4 — A searchable ingredient picker

**Status: ✅ Implemented (2026-09-15).** Offered with AUTHORING-3, chosen by the maintainer afterwards.
Presentation only: no endpoint, no schema.

**As a** member of a household writing a cocktail
**I want** to type part of a bottle's name and pick it
**So that** I am not scrolling a list of two hundred bottles for each line

**Context / notes.** Each line's ingredient was a `<select>` grouped by category: correct, and a scroll.
It is now `IngredientPicker`, a combobox in the RCL.

- **Type a name or a category.** Names that start with what was typed come first, then anything whose
  name or category contains it, alphabetical within each — so "gin" finds London dry gin and Sloe gin,
  and "juice" finds every juice. An empty box lists everything, by category then name. At most fifty
  are shown; typing narrows it.
- **Mouse or keyboard.** Arrow keys move through the list, Enter picks, Escape leaves the line as it
  was. Options are picked on mousedown, before the input's blur can close the list.
- **A screen reader hears it.** The ARIA combobox pattern: the input has `role="combobox"`,
  `aria-expanded`, `aria-controls` naming the listbox and `aria-activedescendant` naming the option the
  arrows are on.
- **It picks from the list and nothing else.** No free text reaches a recipe, because a line must name a
  bottle this household can see (JJ-031). A bottle that is not there says "No bottle by that name. Add it
  on your shelf first."
- **Bottles on the shelf say so**, beside their category.
- **The test id did not move.** The input is still `new-line-ingredient`, the options are
  `new-line-ingredient-option`, and picking still asks for the role suggestion (AUTHORING-3). The journey
  types and picks through one helper in `E2ETestBase`, `PickIngredientAsync`.

**Acceptance criteria**

```gherkin
Scenario: Typing narrows the list
  When I type "gin" into a line's ingredient
  Then I see London dry gin and Sloe gin and no juices
  And names that start with what I typed come first

Scenario: A category finds its bottles
  When I type "juice"
  Then every juice is offered

Scenario: I can pick with the mouse or the keyboard
  When I pick an option, or arrow down to it and press Enter
  Then the line names that bottle

Scenario: Escape leaves it as it was
Scenario: Nothing matching says so
Scenario: Bottles on my shelf are marked
Scenario: A screen reader can use it as a combobox
```

**Tests.** `tests/Ui.Tests/IngredientPickerTests.cs` (new, nine); `WriteRoleSuggestionTests` and
`WriteLayoutTests` now type and pick; the authoring journey picks through `PickIngredientAsync`.

**Out of scope:** adding a new bottle from inside the picker (the shelf does that, INV-2); showing
substitutes in the list.

---

### AUTHORING-2 — Edit a cocktail, including a fork

**Status: ✅ Implemented (2026-09-15).** `GET /api/cocktails/{id}/draft`, `PUT /api/cocktails/{id}`, an
**Edit** button on the recipe page and `/cocktails/{id}/edit`. Implements **FEATURES §13** ("the copy is
fully independent and editable") and **§14**.

**As a** member of a household
**I want** to change a cocktail we wrote or forked
**So that** our recipe book says what we actually pour, and a fork can finally become ours

**Context / notes.** AUTHORING-1 left two things to decide: what happens to a recipe someone else in
the household is reading, and how a fork becomes editable. Decided for the slice, and said in the PR
rather than invented quietly:

- **Only a household's own cocktails are editable** — one it wrote, or one it forked. **The shared
  catalog is read-only** (JJ-002): a book's recipe is visible to every household, so editing one is a
  **403 `catalog_read_only`**, not a 404, and the page offers *Create my own version* instead of Edit.
  Another household's cocktail is a **404**, as everywhere (JJ-031).
- **A fork stays a fork.** `ForkedFromCocktailId` is kept, so an edited fork still says "Based on …",
  and the original is untouched (JJ-013).
- **Last save wins.** No lock and no version check: two people editing one household's recipe at the
  same moment is rare, and when it happens the second save is the recipe. Someone reading it sees the
  new version on their next load.
- **Held to exactly the rules of writing.** One `PrepareAsync` behind both `CreateAsync` and
  `UpdateAsync`, and one refusal mapping behind both endpoints, so an edit can never pass a line a new
  cocktail would refuse. A refused edit writes nothing.
- **Every line replaced.** The form sends the whole recipe in order and nothing references a line's
  id, so the old rows go as orphans of a required relationship in the same save.
- **The form opens in the writer's units.** The draft returns stored ounces as millilitres for a metric
  reader (`BarMeasure.ForWriter`, JJ-041), so what the form shows is what that person would type, and
  saving it unchanged stores exactly what was there.
- **One form, two routes.** `/cocktails/new` and `/cocktails/{id}/edit` are the same `WriteCocktail`
  page; editing fills it from the draft, titles it *Edit cocktail*, sends `PUT` and returns to the
  recipe. Lines loaded from the draft count as set by hand, so the role suggestion (AUTHORING-3) never
  overwrites a role the recipe already has.

**Acceptance criteria**

```gherkin
Scenario: Editing a cocktail I wrote
  When I open it and choose Edit
  Then the form opens with its name, instructions, glass, method and every line
  And when I change them and save
  Then the recipe says what I saved, lines in the new order

Scenario: Editing a fork
  Given I forked the Negroni
  When I edit my copy
  Then it still says "Based on Negroni"
  And the book's Negroni is unchanged

Scenario: The shared catalog cannot be edited
  Given a recipe from a book
  Then there is no Edit button, only Create my own version
  And an edit sent anyway is refused with catalog_read_only

Scenario: Another household's cocktail is not found

Scenario: An edit is held to the rules of writing
  When I save with no name, no lines, or a unit with no amount
  Then it is refused and nothing changes

Scenario: The form opens in my units
  Given a line stored as 1 1/2 oz
  When a metric member edits it
  Then the amount reads 45 and the unit ml

Scenario: The last save wins
```

**Tests.** `tests/Api.Tests/Catalog/CocktailEditingTests.cs` (new, seven); `BarMeasureTests` gains the
writer's-units pair; `tests/Ui.Tests/WriteEditTests.cs` (new); and the fork journey in
`CocktailBrowseJourneyTests` now edits its copy.

**Out of scope:** deleting a household cocktail (unasked-for); a history of edits; any merge of two
people's simultaneous edits.
