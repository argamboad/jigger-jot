# Stories — Seed catalog (SEED)

> One file per epic. The shared catalog every household starts from: the curated vocabulary JiggerJot
> owns, then the ingredients, then the recipes. Read with **JJ-022** (lookups are curated and global),
> **JJ-031** (how a shared row coexists with tenant isolation) and `docs/DATA_MODEL.md`. Stories use
> Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — SEED-1 (curated global lookups) ✅; ingredients and recipes to follow,
> and one rights question is open before any book-derived row ships.

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
| `seed/` | the extraction workspace — scraped JSON, scripts, and `seed/sources/` (gitignored: scanned books run to 141 MB, past GitHub's file limit) | no |
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
vocabulary, not content taken from any source book** — which is why this slice ships ahead of the
rights question below. One embedded file, `lookups.json`, holds all four; `CatalogSeeder` writes what
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

## ⚠️ Open before any book-derived row ships — a rights decision

This is a call for the maintainer, not a technical blocker, and it does not affect SEED-1. Stated as
found, not as legal advice:

| Source | Published | Status as of 2026-09-09 |
|---|---|---|
| Jerry Thomas, *The Bar-Tender's Guide* | 1862 | Public domain in the US by a wide margin |
| *The Savoy Cocktail Book* | 1930 | Entered the US public domain on **1 January 2026**. In the UK and EU it runs to life plus 70 — Craddock died in 1963, so **2034** |
| *Old Waldorf Bar Days* | 1931 | **Not yet** in the US public domain on the 95-year term; that falls on **1 January 2027**. Crockett died in 1937, so it is already public domain in the UK and EU |

Two further points that the table does not capture:

- **The Savoy extraction came from a website, not the book.** `savoycocktaildatabase.com` is a modern
  transcription. Even where the 1930 text is free, a transcriber's selection, arrangement and added
  notes can carry their own rights, and the site's terms are a separate question from the book's.
- **A recipe and its write-up are not the same thing.** A list of ingredients with functional
  directions is thin ground for copyright in the US; the surrounding prose, the headnotes and the
  jokes are not. Whatever is decided, the pipeline should take the structured fields and leave the
  narrative behind.

**Options:** ship the two clearly-free sources now and hold Waldorf until January; ship all three and
accept the exposure; or re-derive Savoy from a public-domain scan rather than the website. **Pick one
before SEED-3.** Attribution should be shown either way — it costs nothing and is the decent thing.

---

### SEED-2 — The ingredient catalog 📋 PLANNED

**As a** member of a household
**I want** a catalog of real ingredients, categorized
**So that** I can tick what is on my shelf without typing it myself

Maps the extracted vocabulary onto curated ingredients under SEED-1's categories. The mapping is the
work: the Savoy vocabulary is period-specific and needs aliases — "French vermouth" is dry vermouth,
"Italian vermouth" is sweet, and "Dry Gin", "Gin", "Tom Gin" and "Plymouth Gin" are four names for
three things. Ingredients are generic, never brands (JJ-017), so "Bacardi Rum" becomes white rum.

Rows are shared (`TenantId` null) and therefore go in under the system context SEED-1 proved out.

### SEED-3 — Recipes and their lines 📋 PLANNED

**As a** member of a household
**I want** a library of cocktails on first run
**So that** the app can answer "what can I make" before I have added anything

Normalizes raw lines into `CocktailIngredient` rows: amount, unit, role, `is_required`,
`display_order` (JJ-007, JJ-009, JJ-010). Needs the rights decision above. Two known shapes to handle:
proportional recipes, which take the `part` unit; and the 92 recipes whose method the scrape could not
determine, which need a default or a pass by hand.

### SEED-4 — The substitution graph 📋 PLANNED

Global and ingredient-level, both directions stored as separate rows (JJ-004, JJ-005, JJ-006). Both
ends must be shared ingredients — a household's own ingredient satisfies a line by exact match only
(JJ-018). Small and hand-curated: this is the file that decides whether "I have no Cointreau" ends the
search or suggests triple sec.
