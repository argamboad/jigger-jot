# Stories — Seed catalog (SEED)

> One file per epic. The shared catalog every household starts from: the curated vocabulary JiggerJot
> owns, then the ingredients, then the recipes. Read with **JJ-022** (lookups are curated and global),
> **JJ-031** (how a shared row coexists with tenant isolation) and `docs/DATA_MODEL.md`. Stories use
> Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — SEED-1 (curated global lookups) ✅ and the IBA extraction ✅; the
> source question is settled (JJ-032: Savoy plus the IBA list). Ingredients and recipes to follow.

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
| `seed/` | the extraction workspace — `savoy_cocktails.json`, `iba_cocktails.json` and their scripts, plus `seed/sources/` (gitignored: the two dropped books' scans run to 141 MB, past GitHub's file limit) | no |
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
term does not expire until 1 January 2027.

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

### SEED-2 — The ingredient catalog 📋 PLANNED

**As a** member of a household
**I want** a catalog of real ingredients, categorized
**So that** I can tick what is on my shelf without typing it myself

Maps both extracted vocabularies onto curated ingredients under SEED-1's categories: 214 distinct
names from the Savoy and 205 from the IBA list, overlapping heavily. The mapping is the work, and each
source is awkward in its own way. The Savoy is period-specific and needs aliases — "French vermouth"
is dry vermouth, "Italian vermouth" is sweet, and "Dry Gin", "Gin", "Tom Gin" and "Plymouth Gin" are
four names for three things. The IBA names brands in the spec itself, and JJ-017 says ingredients are
generic — so "Bitter Campari" becomes a bitter aperitivo and "Bacardi Rum" becomes white rum, in both
directions.

Rows are shared (`TenantId` null) and therefore go in under the system context SEED-1 proved out.

### SEED-3 — Recipes and their lines 📋 PLANNED

**As a** member of a household
**I want** a library of cocktails on first run
**So that** the app can answer "what can I make" before I have added anything

Normalizes raw lines into `CocktailIngredient` rows: amount, unit, role, `is_required`,
`display_order` (JJ-007, JJ-009, JJ-010). **Adds per-cocktail provenance to the model first** — source
name, year and a licence note — so a credit is a property of the row rather than a promise in a footer;
`Cocktail` has nowhere to record it today.

Three known shapes: the Savoy's proportional recipes, which take the `part` unit; the 92 Savoy recipes
and 3 IBA ones whose method the scrape could not determine; and the IBA's garnish field, which is a
sentence of prose rather than a line, and becomes an optional line with the `garnish` role.

### SEED-4 — The substitution graph 📋 PLANNED

Global and ingredient-level, both directions stored as separate rows (JJ-004, JJ-005, JJ-006). Both
ends must be shared ingredients — a household's own ingredient satisfies a line by exact match only
(JJ-018). Small and hand-curated: this is the file that decides whether "I have no Cointreau" ends the
search or suggests triple sec.
