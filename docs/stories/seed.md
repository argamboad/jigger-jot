# Stories — Seed catalog (SEED)

> One file per epic. The shared catalog every household starts from: the curated vocabulary JiggerJot
> owns, then the ingredients, then the recipes. Read with **JJ-022** (lookups are curated and global),
> **JJ-031** (how a shared row coexists with tenant isolation) and `docs/DATA_MODEL.md`. Stories use
> Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — SEED-1 (lookups), the IBA extraction, SEED-2 (ingredients),
> SEED-3 (recipes) and SEED-4 (substitutions) all done. Sources settled by JJ-032.

**Epic key:** `SEED`

**Prerequisites:** CKTL-1 (the tables and both tenancy walls). No new packages.

**Reuses:** `AppDbContext`, `RlsSessionInterceptor`'s bypass-for-a-system-context rule (JJ-031).

**Depends on:** `CKTL`. **Depended on by:** `INGREDIENT`, `INV`, `MAKE`, `ALMOST`, `FILTER` — an empty
catalog makes all of them untestable and the app pointless on first run.

---

## Where the material lives

Two directories, and the split is deliberate.

| Path | What it is | Shipped? |
|---|---|---|
| `seed/` | the extraction workspace — `savoy_cocktails.json`, `iba_cocktails.json`, their two scrapers and the two curation scripts. `seed/sources/` stays gitignored for future raw material, and is empty | no |
| `src/Infrastructure/Persistence/Seed/` | the **curated** files, embedded in the assembly and read at startup | yes |

Extraction output is raw material, not seed data. Nothing reaches the second directory without a
curation pass, because the raw extractions are demonstrably dirty — the Savoy scrape's ingredient tail
holds entries like `1 er Boiling Water` and `1 big or 2 small Lemons Juice`, artifacts of parsing
1930s typesetting.

---

### SEED-1 — The curated global lookups

**Status: ✅ Implemented.** Glass types, methods, units and the two-level ingredient categories.

**As a** member of a household
**I want** the app to know what a coupe, a dash and a London dry gin are before I add anything
**So that** browsing, filtering and authoring have a vocabulary from the first boot

**Context / notes.** These are the four curated lookups of JJ-022, and they are **JiggerJot's own
vocabulary, not content taken from any source book** — which is why it could ship before the source
question was settled. One embedded file, `lookups.json`, holds all four; `CatalogSeeder` writes what
is missing at startup, behind `Seed:Catalog:Enabled` (default on).

**Three decisions worth knowing about:**

**Ids are derived from names, not stored.** `SeedId.For(kind, name)` hashes a namespace plus the
lower-cased name into a version-8 GUID, so the same glass has the same id on every machine and after
every wipe. That is what lets a later seed pass reference a category without a lookup table or an
import order. The cost is real and stated in the code: **a rename changes the id and orphans every
reference**, so renaming a curated row is a data migration, never an edit to the file.

**The seeder adds, and never deletes.** A row dropped from the file stays in the database. A curated
row may already be referenced by a household's own cocktail, and removing one to match a JSON file is
a worse failure than an unused lookup lingering.

**`part` is a unit.** The older books are proportional — "2/3 gin, 1/3 vermouth" carries no absolute
volume anywhere in it. A neutral `part` unit stores that as authored (JJ-007); inventing millilitres
would fabricate a precision the recipe never had. `barspoon` is neutral for the same reason: bar spoons
run 2.5 to 5 ml, so rendering one as "5 ml" would be a guess wearing a number's clothes.

**Acceptance criteria**

```gherkin
Scenario: A fresh database gets the whole vocabulary
  Given a database with no lookup rows
  When the app starts
  Then every glass type, method, unit and ingredient category in the seed file exists

Scenario: Starting again changes nothing
  Given the lookups have already been seeded
  When the app starts again
  Then no rows are added
  And no rows are duplicated

Scenario: A row keeps its identity across a rebuild
  Given the lookups have been seeded
  When the database is wiped and seeded again
  Then the coupe has the same id it had before

Scenario: Categories are exactly two levels
  Given the lookups have been seeded
  Then every subcategory's parent is itself a top-level category

Scenario: A unit converts exactly when it carries a millilitre factor
  Given the lookups have been seeded
  Then every neutral unit has no factor
  And every metric or imperial unit has one

Scenario: The catalog cannot be seeded from inside a household
  Given a context acting as a household
  When the seeder runs
  Then it refuses, naming the ambient household as the reason

Scenario: A system context can write a shared row, and a household cannot
  Given the app is connected as the row-level-security runtime role
  When a context with no ambient household inserts an ingredient with no owner
  Then the insert succeeds and every household can read it
  But the same insert from a household context is rejected by the database
```

**Tests.** `tests/Api.Tests/Catalog/CatalogSeederTests.cs` (seven), plus two in
`tests/Api.Tests/Rls/SharedCatalogRlsTests.cs` that prove the write path through the real interceptor
rather than a hand-set GUC. The lookups themselves carry no tenant column, so those last two exist to
prove the mechanism **SEED-2 will depend on** before SEED-2 depends on it.

---

## Sources: the Savoy and the IBA list — decided (JJ-032)

Answered 2026-09-09, and the rights question and the coverage question turned out to be the same
question.

| Source | Role | Drinks |
|---|---|---|
| The 1930 Savoy Cocktail Book | vintage depth | 868 |
| IBA official list | the modern canon | 102 |

**Why not more books.** Measured against the actual extraction, Savoy's 868 recipes contain zero
tequila, zero bourbon, zero Aperol, one line of Campari and four of vodka. Jerry Thomas is from 1862
and *Old Waldorf Bar Days* from 1931, so neither closes that gap — nothing old enough to be free
contains a Margarita. Both are dropped, which also removes the wait on Waldorf, whose US public-domain
term does not expire until 1 January 2027. **Neither was ever extracted, and both PDFs were deleted on
2026-09-09** — there is no half-finished pipeline left behind to mislead anyone.

**What is taken.** Specifications only: name, category, ingredient lines, amounts, method, garnish.
Prose, headnotes, video and photography stay where they are. Attribution ships in the data, and
per-cocktail provenance lands in the model before SEED-3 writes a single recipe.

**Still open, and narrowed to one thread.** The Savoy extraction came from a transcription website
rather than the book. Re-deriving from a public-domain scan would close it. Not urgent.

### The IBA extraction

`seed/scrape_iba.py` → `seed/iba_cocktails.json`. Enumerated from the site's own sitemap rather than
by walking paginated HTML, one request per drink with a pause between, identifying User-Agent;
`robots.txt` disallows only `/wp-admin/`.

| | |
|---|---|
| cocktails | 102, in three groups of 34 |
| recipe lines | 418, of which 391 carry an amount |
| method detected | 99 of 102 |
| glass detected | 87 of 102 |

Amounts are **metric and absolute**, which is the practical difference from the Savoy: no proportions
to interpret and no house measure to guess at.

The three-equal-groups structure is used as a correctness check on the parse, and paid for itself
twice. The first run labelled all 102 drinks "The Unforgettables", because the nav menu lists every
group verbatim above the breadcrumb. The fix — take the last matching line instead of the first —
then labelled every Contemporary Classic a New Era drink, because that breadcrumb reads "Contemporary
Classics" with no leading "The" and the fallback landed on the nav's last entry. The parse now reads
the breadcrumb structurally, and the counts are 34, 34 and 34.

### SEED-2 — The ingredient catalog

**Status: ✅ Implemented.** 175 curated ingredients, each under a SEED-1 category, seeded as shared
rows.

**As a** member of a household
**I want** a catalog of real ingredients, categorized
**So that** I can tick what is on my shelf without typing it myself

**Context / notes.** The two extractions between them use **395 distinct names** for what turns out to
be 175 things. The mapping is the work, and each source is awkward in its own way. The Savoy is
period-specific: "French vermouth" is dry vermouth, "Italian vermouth" is sweet, and "Dry Gin", "Gin",
"Tom Gin" and "Plymouth Gin" are four names for three things. The IBA names brands inside the
specification itself.

**These are the first seeded rows that carry a tenant column,** and they carry it null. Nothing stamps
them (JJ-031), the insert policy would reject them from a household context, and the seeder's
ambient-household check is what stops that failing deep inside `SaveChanges` instead of at the door.

**Coverage is enforced, not hoped for.** `seed/build_ingredients.py` refuses to emit anything while a
single raw name is unaccounted for — mapped to an ingredient, or excluded with a written reason. The
failure mode of a curation job is silence: an unmapped name is a recipe line that will not resolve in
SEED-3, and you find out months later when a drink shows up missing an ingredient.

| | |
|---|---|
| raw names across both books | 395 |
| curated ingredients | 175 |
| lines mapped | 3258 of 3305 |
| lines excluded, with a reason | 47 |

The 47 are ice and water (always available, JJ-020), four entries that are a choice rather than an
ingredient ("Any Spirit"; "Rum, Brandy, Port Wine, Sherry, or Whisky"), three unidentifiable 1930s
proprietaries, and two lines the scrape merged that SEED-3 splits by hand.

**Brand names forced a decision (JJ-033).** JJ-017 says ingredients are generic, and taken literally
that deletes the catalog: Chartreuse, Campari, Bénédictine, Angostura and a dozen others are
proprietary and load-bearing. The rule now reads — generic **where a generic exists**, proper name
**where the product has no substitute**. The test is whether a bartender could hand you a different
bottle and have made the same drink.

**Normalisation lives in the workspace, not the app.** `seed/` strips casing, "fresh"/"freshly
squeezed", parentheticals, leading counts and accents before matching, so only genuinely different
words need an alias. The shipped `ingredients.json` is a plain list; the aliases are facts about two
particular books and stay where those books do.

**Acceptance criteria**

```gherkin
Scenario: The ingredient catalog is seeded as shared rows
  Given a database with the lookups seeded
  When the app starts
  Then every curated ingredient exists
  And none of them belongs to a household

Scenario: Every ingredient hangs off a real category
  Given the catalog has been seeded
  Then each ingredient's category is a top-level category
  And each ingredient's subcategory belongs to that same category

Scenario: A household's own ingredient is neither seeded over nor counted
  Given the catalog has been seeded
  And a household has added its own ingredient sharing a name with a shared one
  When the app starts again
  Then nothing is added
  And both rows still exist, one shared and one the household's

Scenario: Curation refuses to ship with a name nobody decided about
  Given a raw ingredient name that is neither mapped nor excluded
  When the catalog is built
  Then the build fails and names it
```

**Tests.** `tests/Api.Tests/Catalog/CatalogSeederTests.cs` (ten). The household-ingredient one is the
load-bearing case: the seeder counts only shared rows as already-seeded, so a household adding
"Absinthe" neither suppresses the shared row nor gets counted as one.

### SEED-3 — Recipes and their lines

**Status: ✅ Implemented.** 969 cocktails, 3526 recipe lines, credited to their sources.

**As a** member of a household
**I want** a library of cocktails on first run
**So that** the app can answer "what can I make" before I have added anything

**Context / notes.** `seed/build_cocktails.py` resolves both extractions against SEED-2's curation
and emits the shipped `cocktails.json`. Every line must resolve to a curated ingredient and a known
unit, or be dropped for a reason the script names — a recipe that quietly loses a line is a recipe
that quietly stops being makeable.

| | |
|---|---|
| cocktails extracted | 969 |
| **cocktails shipped (starter set)** | **31** |
| recipe lines extracted | 3526 |
| lines dropped (ice, water, the SEED-2 exclusions) | 74 |
| lines the scrape left blank | 13 |
| recipes recovered from tag lists | 60 |
| glass null: unstated / too vague | 96 / 163 |
| method null | 94 |

**Provenance landed first, as promised.** A new `RecipeSource` lookup and a nullable
`Cocktail.SourceId`, so a credit is a property of the row (JJ-032). The catalog mixes sources; a flat
attribution page cannot say which drink came from where, and pulling a source later would mean
re-deriving which rows to remove.

**Sixty prose recipes were recovered rather than dropped.** The Savoy writes some entries as
narrative — *"Put on the fire in a saucepan one quart of Ale"* — and the line parser found nothing in
them. The site's own ingredient **tags** did. Since makeability is a question about which ingredients
a drink needs and not how much of each (JJ-003), those recipes work fully with unmeasured lines, and
their quantities remain readable in the instructions. Dropping 60 real cocktails to avoid an empty
amount column would have been the worse trade. One recipe, `Common Highball`, has no ingredients in
any form and is genuinely dropped.

**Identity is the source plus that source's slug, never the name.** Four names appear in both books,
and the Savoy alone carries *Mr. Manhattan Cocktail* twice — once in the main chapter and once among
the Prohibition cocktails, with mint and sugar the second time. Keyed on the name, one of each pair
would silently replace the other, and the loss would surface as a missing drink rather than an error.
The build fails outright if two recipes from one source ever share a slug.

**Glass and method became optional (JJ-034).** 259 of 969 recipes state no glass or state something
that is not one; the Savoy's "medium size glass" and bare "glass" are 108 between them. Filling those
in would put a fact in the database that nobody wrote down, and afterwards it would be
indistinguishable from a fact somebody did.

**Roles are derived, never tagged.** The first spirit in a drink is its base and later spirits are
modifiers; bitters, juice, syrup and mixers come from the ingredient's category. A garnish is simply
an optional line, so optional lines never block makeability (JJ-009) — invert that one bit and every
drink with a mint sprig becomes unmakeable without mint.

**Acceptance criteria**

```gherkin
Scenario: The recipe catalog is seeded as shared rows
  Given a database with the ingredients seeded
  When the app starts
  Then every curated cocktail and its lines exist
  And neither the cocktails nor their lines belong to a household

Scenario: Every seeded recipe is credited
  Given the catalog has been seeded
  Then each cocktail names a source
  And each source carries an attribution

Scenario: Two sources may share a recipe name
  Given the catalog has been seeded
  When I look up "Gin Fizz"
  Then I find two recipes, one from each source

Scenario: A recipe that did not state a glass has none
  Given the catalog has been seeded
  Then some cocktails have no glass and no method
  And most still do

Scenario: Proportional amounts keep the fraction the book wrote
  Given the catalog has been seeded
  Then the proportional lines carry the neutral "part" unit
  And that unit never converts

Scenario: Garnishes are optional and nothing else is
  Given the catalog has been seeded
  Then every garnish line is optional
  And every other line is required
```

**Tests.** `tests/Api.Tests/Catalog/CatalogSeederTests.cs` (sixteen in total across SEED-1 to 3).

**The shipped catalog is a starter set, not the whole extraction.** 969 recipes is the right eventual
catalog and the wrong thing to develop against: every test assertion ends up being a claim about nine
hundred rows rather than about behaviour, and a change to the data breaks tests that had nothing to do
with it. `build_cocktails.py` therefore emits 31 by default and all 969 behind `--full`.

The picks are not arbitrary — between them they cover every **shape** the model handles: metric and
absolute amounts, proportional fractions and whole "parts", unmeasured lines recovered from a tag
list, one name in two books, one name twice in a single book, a recipe with no glass or method
recorded, an optional garnish line, a substitution in play, and the modern spirits the Savoy never
had. Enough to page, few enough to reason about. The build fails if a named slug stops existing,
rather than quietly shipping a smaller catalog.

**Known data debt, deliberately left visible.** Two Savoy lines were merged by the scrape (a lemon
and a grapefruit juice; an allspice dram and a lime juice) and are excluded rather than guessed at.
Splitting them is a hand-fix on the extraction, not on the curated output.

### SEED-4 — The substitution graph

**Status: ✅ Implemented.** 17 interchangeable groups and 12 one-way entries, expanding to 88
directed rows.

**As a** member of a household
**I want** the app to know that Curaçao will do when a recipe asks for Cointreau
**So that** a nearly-stocked shelf still gets me a drink

**Context / notes.** This is the file that decides whether "no Cointreau" ends the search or offers
the Curaçao already on the shelf, so it is hand-written and deliberately conservative. The bar for
inclusion is *a bartender would pour this without comment*, not *these are both brown*. Bourbon for
rye, yes. Mezcal for tequila, no — it changes the drink, and someone who wants that can fork the
recipe.

**Global only.** Both ends are shared catalog ingredients (JJ-005), which is structural rather than
enforced: the table has no tenant column at all. A household's own ingredient satisfies a recipe line
by exact match and never through this graph (JJ-018).

**Two shapes, because substitution is not always mutual.** This is the part worth arguing with:

- **`interchangeable`** — every member stands in for every other, both ways. Seventeen groups: the
  orange liqueurs, the dry gins, the American whiskeys, the aged rums, the anise spirits, the
  sparkling wines, and so on.
- **`oneWay`** — a recipe asking for brandy is happy with cognac; one asking for cognac is **not**
  happy with any brandy. A symmetric graph would recommend drinks the household cannot actually make
  well, which is worse than recommending nothing.

Both expand to **directed rows** (JJ-006), so a group of three becomes six. Storing the direction
rather than a symmetry flag is what keeps the makeable query a plain join instead of an OR across two
columns — and it is what lets the file say cognac stands in for brandy without claiming the reverse.

**Every entry carries its reasoning**, in a `note` beside it, because these are judgement calls and a
future reader should be able to disagree with a specific one rather than the whole file.

**Acceptance criteria**

```gherkin
Scenario: The graph is seeded as directed rows
  Given the ingredient catalog has been seeded
  When the app starts
  Then every substitution names two real catalog ingredients
  And no ingredient substitutes for itself

Scenario: Interchangeable ingredients work in both directions
  Given the graph has been seeded
  Then a recipe asking for Cointreau accepts Curaçao
  And a recipe asking for Curaçao accepts Cointreau

Scenario: A one-way substitution does not run backwards
  Given the graph has been seeded
  Then a recipe asking for brandy accepts cognac
  But a recipe asking for cognac does not accept brandy
```

**Tests.** `tests/Api.Tests/Catalog/CatalogSeederTests.cs` (eighteen across the whole epic). The
one-way assertion is the load-bearing one: it is the only thing standing between a considered graph
and a symmetric one that quietly over-promises.

---

## What is left

The epic is complete for MVP. Two threads stay open and neither blocks anything:

- **The Savoy transcription path.** The extraction came from a website rather than the book;
  re-deriving from a public-domain scan would close the last rights question (JJ-032).
- **Two merged lines.** The scrape ran a lemon and a grapefruit juice together, and an allspice dram
  and a lime juice, in one recipe each. Both are excluded rather than guessed at, and splitting them
  is a hand-fix on the extraction.
