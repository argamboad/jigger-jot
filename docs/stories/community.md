# Stories — Cocktails from other households (`COMMUNITY`)

> One file per epic. A household shares one of its own recipes with every other household, and a
> household can read what others have shared — on a page of its own, never mixed into the catalog.
> Read with **JJ-024** (private only in MVP — this epic would amend it), **JJ-002/JJ-013** (copy,
> never reference), **JJ-031** (the two walls, changed together) and **JJ-018** (a custom ingredient
> matches by exact identity only). Stories use Gherkin acceptance criteria.
> **Status: 📋 PLANNED (2026-09-16) — nothing started, nothing decided.** The maintainer asked for
> the idea to be written down so it can be picked up any time, and explicitly NOT built yet, because
> it is the first feature that changes what a row's tenant column means. **Decision record:** JJ-042,
> drafted at the end of this file; it is pasted into `DECISIONS.md` on the day the epic starts, not
> before. Until then `PROJECT_BRIEF.md` still lists publishing as OUT.

**Epic key:** `COMMUNITY`

**Prerequisites:** every app epic is complete (`CKTL`, `INV`, `MAKE`, `ALMOST`, `FORK`, `AUTHORING`,
`FILTER`, `MARGA`, `SHELL`, `BACKBAR`). No new package. One migration per slice that changes the
schema (1 and 2), each also re-creating the hand-written RLS policies it widens.

**Depends on:** `FORK` (the copy path), `AUTHORING` (the write path and its refusal mapping),
`ADMIN` + ADR-021 (an enumerated admin write for the takedown). **Depended on by:** nothing.

---

## Why

The brief calls the community pool "the vision" and JJ-024 deferred it for moderation reasons only.
The maintainer's framing (2026-09-16): *"I want to share this cocktail to the world"*, and a reader
can choose to see *"cocktails by other households"* or stick to the app's own catalog. Two
constraints came with it, and both shape the design more than the feature does:

1. **The base catalog and the community pool are never mixed.** The seeded catalog is thirty-one
   recipes today and nine hundred and sixty-nine behind a flag; the pool may reach thousands. A
   reader who wants the book gets the book. A reader who wants the pool asks for it.
2. **A shared recipe must render in full for everyone**, including a line that asks for the
   publisher's own custom ingredient — the ingredient's *visibility* is the problem, not its
   availability (nobody expects to own "Allan's coffee liqueur").

## The shape — read this before the slices

### What a row's tenant column means, before and after

Today (JJ-031) every catalog row is one of two things: `TenantId` **null** = the seeded catalog,
read by all and written by none; `TenantId` **set** = a household's own, read and written by that
household alone. Both walls — the app-level EF filter and the hand-written RLS policies — say
exactly that.

This epic adds a **third state, and it is a state of the household's own rows, not of the
catalog's**: `TenantId` **set** AND `PublishedAt` **set** = a household's row that every household
may **read**. Nothing about writing changes: only the owner (and staff, slice 3) may update or
delete it. The seeded catalog keeps its definition to the letter — `TenantId` null, and *nothing
this epic does ever writes a null-tenant row*. That is the separation the maintainer asked for,
enforced by the column that already exists rather than by a flag beside it.

**Why not a null-tenant copy with a "social" flag** (the first idea, 2026-09-16, rejected the same
day after reading `RlsSessionInterceptor`): the RLS bypass is set for a tenant-less context (the
seeder) or a query carrying the leading cross-tenant tag — and query tags never render on
`SaveChanges`, so a request-path INSERT of a null-tenant row has **no sanctioned path today**.
Building one would be a second escape hatch through the tenancy foundation, which JJ-039 marks as a
red light and the hatch guard exists to catch. Keeping the copy *owned* by its publisher needs no
hatch at all: the owner writes their own row under the ordinary policy, and the read wall is widened
by one clause. It also makes three hard problems disappear (see "the data questions").

**The two walls change together, in one migration, as JJ-031 requires**:

| | before | after |
|---|---|---|
| EF filter (`AppDbContext`) | `TenantId == null \|\| TenantId == current` | `… \|\| PublishedAt != null` |
| RLS `SELECT` policy | `"TenantId" IS NULL OR "TenantId" = current OR bypass` | `… OR "PublishedAt" IS NOT NULL` |
| RLS `INSERT` / `UPDATE` / `DELETE` | owner only | **unchanged** — owner only |

On all three dual-natured tables (`Cocktail`, `CocktailIngredient`, `Ingredient`): a line carries its
parent's nature, so it carries `PublishedAt` too, stamped with the same instant. The mirror test
that holds the filter and the policies to the same predicate gains the clause.

### The pool is a filter, not a wall

`GET /api/cocktails` gains `pool=catalog|community`, default `catalog`:

- **catalog** = `TenantId == null || (TenantId == current && PublishedAt == null)` — the book plus
  the household's private recipes and forks. **This is what the catalog page, Home's count, Marga's
  lines, `/unlocks` and `/starters` read, always, gate or no gate.** Thousands of community recipes
  must never skew "your first bottle" or the one-away shopping list.
- **community** = `PublishedAt != null` — every household's published copies, the reader's own
  included (marked "yours").

The predicate lives in the browse handler beside the makeable and one-away ones, so the three
radios, search, the four filters, the substitutions in play and MARGA-6's count line all work on the
community page with **no second implementation**. It is *not* an isolation wall — a bug in it shows
a public row on the wrong page, never a private row to the wrong household; the walls above are
what keep private rows private.

### Publishing is a snapshot edition, like a fork

`POST /api/cocktails/{id}/publish` copies the household's own cocktail (written or forked) into a
**second row the household owns**: `TenantId` = the household, `PublishedAt` = now,
`ForkedFromCocktailId` = the private original, `SourceId` null (the book did not write it, JJ-032),
every line copied with amounts as stored. The private original is untouched and stays private;
later edits to it change nothing until the household **publishes again**, which replaces the
edition in place — same id, every line replaced, the way `PUT` replaces — so a fork someone made of
it keeps pointing at a row that exists. **Unpublish** deletes the edition; forks of it stand, as
forks always do (JJ-013). One live edition per original, held by a partial unique index on
`ForkedFromCocktailId WHERE PublishedAt IS NOT NULL`.

*Rejected: publishing the original in place* (set `PublishedAt` on the household's own row). Simpler
data, but every private edit is public the instant it is saved, there is no such thing as a private
variation of a shared drink, and "unpublish" would have to mean "hide" rather than "remove". The
snapshot is the rule this app already lives by.

### Custom ingredients travel by being published, in place

A line on the publisher's custom ingredient renders for everyone because the **ingredient row
itself** gets `PublishedAt` stamped — in place, since an ingredient is a name and a category and
has no private variation to protect. It stays the publisher's shelf item. Other households can see
it in a recipe and **nowhere else**: the shelf, the picker, the category list, the duplicate-name
check, the starter ranking and `PUT /api/inventory` all read the household's own pool
(`TenantId == null || TenantId == current`), so a published ingredient never appears on another
household's shelf and cannot be ticked there. A community line on such an ingredient therefore reads
as **missing** for every other household, and the recipe page says so plainly — *"Allan's coffee
liqueur — theirs; fork to make it your own"*. It is unstamped again when the household's last
published edition that uses it is unpublished or replaced.

**Forking a community recipe materializes those ingredients**: for each line on another household's
published ingredient, the fork finds a custom ingredient of the *same name* in the forker's
household or creates one (unticked, same category), and points the copied line at it. That is the
only new work the fork handler ever does, and it is the reason the reader can, after forking, tick
the bottle and see the drink go green.

*Rejected: snapshotting the ingredient's name as text on the community line.* It needs a nullable
`IngredientId`, and that column is read by every makeable predicate, the one-away count, the
ingredient filter, the roles endpoint, the draft, Core's `Makeability` and EF's required navigation.
Publishing the row touches one column and a handful of readers that already filter by tenant.

### Where it lives on screen

- **`/community`** — the same `Cocktails.razor` page component with the pool bound, its own route and
  title, the three radios and the filter panel intact, each row credited *"Shared by the X
  household"* (the tenant's current name, joined at read time — never a snapshot, see below).
- **The link** — on the catalog page, above the list: *"Cocktails from other households →"*, and the
  reverse link on the community page. **Not a fourth tab**: SHELL-1 settled a three-destination bar
  and this epic does not reopen it. If the pool earns a tab, that is a `SHELL` decision later.
- **The recipe page** — *Share with everyone* / *Published · Publish again · Unpublish* on the
  household's own recipes, in the same actions row as Edit and Delete; a community recipe shows the
  credit where a seeded one shows its book, and *Create my own version* as always. Edit and Delete on
  a community recipe do not show, and the API refuses them (403 `community_read_only`, the same
  mapping as `catalog_read_only`) — with the DB refusing underneath, because the UPDATE and DELETE
  policies never changed.
- **Marga** — no new engine work and one new line: on the community page she keeps MARGA-6's *"N
  here, you can pour M"*; the empty pool gets *"Nobody has shared a drink yet. Yours could be the
  first."* Nothing else; she stays quiet where the screen already says it.
- **The gate** — `Community:Enabled`, default **off**, like `PublicApi`, `Webhooks` and `Billing`
  (ADR-027 pattern): off ⇒ the publish/unpublish routes and `pool=community` are not in the
  application model (404), `GET /api/features` says so, the client hides both links and refuses
  `/community`. The migration and the widened read wall apply regardless — a row published while the
  gate was on stays readable by id after it is turned off, which is acceptable and stated.

## The data questions — answered here so the first session does not re-derive them

The maintainer's reason for not starting: *"it can be messy in many ways in terms of data."* Each
mess, and the default the slices assume. Change a default by editing this list before slice 1.

| # | Question | Default |
|---|---|---|
| D1 | The original is edited after publishing | Nothing changes until *Publish again*. The recipe page shows *"Published · differs from this version"* when the edition's lines differ — computed at read time, never stored. |
| D2 | The original is deleted | The edition stays (a snapshot never depends on its source, JJ-013). It is still the household's, marked *yours* on the community page, unpublishable from there. `ForkedFromCocktailId` dangles, as it may today. |
| D3 | The household is dissolved | **Every edition and every published ingredient go with it** — they are the household's rows, and `CatalogDataContributor.WipeAsync` already deletes everything with its tenant id. Forks other households made survive, as forks do. No new contributor, no anonymization, no retained name. The dissolution test gains a scenario. |
| D4 | The publishing household is renamed | The credit follows, because it is joined at read time. A snapshot of the name would be retained personal data after D3. |
| D5 | Two households publish the same name | Allowed. Rows are ordered name-then-id (CKTL-2), and the credit tells them apart. No uniqueness on names anywhere in this app, and none here. |
| D6 | The publisher forks a community recipe of their own | Allowed and pointless; it is a fork like any other. |
| D7 | A published ingredient's owner deletes it (INV-4) | Refused with 409 `ingredient_in_use`, already — the edition's lines are the household's recipes. |
| D8 | A published ingredient's owner unticks it | Their business; publishing is not availability. Other households never see it on a shelf. |
| D9 | A community line's ingredient is a *seeded* one the reader lacks | Reads as missing exactly as in the catalog, substitutions included. Nothing new. |
| D10 | Substitutions | Never reach a published ingredient (JJ-018 holds: exact identity only). The seeder is the only writer of `IngredientSubstitution` today and only ever names null-tenant rows; a test pins that a published ingredient is never a substitute or substituted. |
| D11 | The seeder | Untouched. It reads and writes null-tenant rows only, keyed by seed ids, and no edition ever has a null tenant. |
| D12 | The 969-recipe flag | Irrelevant to the pool; both pools obey it independently. |
| D13 | Units | Stored ounces, read in the viewer's system (JJ-041). A metric writer's edition reads in ounces for an imperial reader. Free. |
| D14 | Export (GDPR) | The household's editions and published ingredients are in its export already — same contributor, same tenant id. Verify with a test rather than assume. |
| D15 | Republishing while another household is reading the old edition | Last write wins; a page already open shows the old lines until reloaded. No lock, as AUTHORING-2 decided for edits. |
| D16 | Can a *member* publish, or owners only? | Any member — it is household data like the recipe itself, and the roster has no per-feature roles (RBAC is owner/admin/member over membership, not content). Revisit if moderation asks for it. |
| D17 | A flood | Slice 3: a per-household cap on live editions through the existing quota seam, and a staff takedown. Until then the signup green list (GATES-2) decides who can publish at all. |

## Slices — one branch each off `develop`, one PR, after the previous one is merged

| # | Slice | What it is | Migration |
|---|---|---|---|
| 1 | **COMMUNITY-1** The pool | `PublishedAt` on the three tables, both walls widened together, `pool=` on browse, publish / publish again / unpublish for recipes that use **seeded ingredients only**, the `/community` page and the two links, the gate, `GET /api/features`, the credit, the dissolution scenario | yes |
| 2 | **COMMUNITY-2** Custom ingredients travel | Publishing stamps the household's ingredients the edition uses and unstamps on unpublish/republish; every shelf-side reader filters to the household's own pool; the community line says *theirs*; forking a community recipe materializes the ingredient by name; the slice-1 refusal is lifted | no (the column shipped in 1) |
| 3 | **COMMUNITY-3** Keeping it clean | Staff takedown as an enumerated, audited admin write (ADR-021) with the console row; a per-household cap on live editions via `IQuotaService`; a *Report* link that opens a prefilled mail — no report table, no queue | no |

Each slice: the slice ritual in `PLAN.md` in full — `FEATURES.md` §16 quoted, tests first, Postman
spliced, EN + ES strings, the story's Gherkin, QA cases in a new **§10j — Community** with the
traceability and sign-off rows, Release build with zero warnings, `Core.Tests` + `Api.Tests` +
`Ui.Tests`, then the journeys actually run.

---

### COMMUNITY-1 — The pool: publish, unpublish, and a page of their own

**Status: 📋 Planned.** Implements **FEATURES §16** (to be written into `FEATURES.md` as the flow
when the epic starts; the outline is in this file).

**As a** member of a household
**I want** to share a recipe of ours with every household, and to read what others have shared —
without either list getting into the other
**So that** the app's catalog grows beyond two books, and the book stays the book

**Context / notes.** Everything in "The shape" above. The refusal that makes this slice small: a
recipe with a line on a household ingredient answers **409 `custom_ingredient`** naming the line,
until COMMUNITY-2 lifts it — so slice 1 never has to make an ingredient visible. The migration adds
three nullable timestamp columns, the partial unique index, and re-creates the three tables' SELECT
policies; the EF filter changes in the same commit; the two mirror tests —
`SharedCatalogFilterTests` (the EF side) and `Rls/SharedCatalogRlsTests` (the DB side) — gain the
clause, and `SharedOrTenantRlsMigrationGateTests` holds the widened DDL to the policies.

**Acceptance criteria**

```gherkin
Scenario: Publishing makes a copy every household can read
  Given a cocktail my household wrote, using seeded ingredients only
  When I share it with everyone
  Then a second cocktail exists, owned by my household, with a published stamp
  And it names my original as what it came from and credits no book
  And every line is copied in order with its stored amounts
  And another household can open it and read every line

Scenario: The original stays private
  Then my original has no published stamp
  And another household still cannot see it

Scenario: The catalog never shows the pool
  Given another household has published a drink
  When I browse the catalog, or the makeable filter, or the one-away filter
  Then that drink is not in the list
  And Home's count, the unlock ranking and the starter ranking do not count it

Scenario: The community page shows the pool and nothing else
  When I browse the community pool
  Then every published drink is there, with the name of the household that shared it
  And my own published drink is there, marked as mine
  And no seeded recipe and no private recipe of anyone's is there

Scenario: The community page is a real catalog page
  Given a published drink I can make with what I have
  When I browse the community pool with the makeable filter
  Then it is listed, with the substitutions in play
  And search and the four filters narrow the pool the way they narrow the catalog

Scenario: Publishing again replaces the edition in place
  Given my published drink, and a fork of it by another household
  When I change my original's lines and share it again
  Then the edition has the same id and the new lines
  And the other household's fork is unchanged

Scenario: Unpublishing removes the edition and leaves forks standing
  When I unpublish
  Then the edition is gone
  And another household's fork of it is unchanged
  And my original is unchanged

Scenario: Nobody else can write an edition
  Given another household's published drink
  When my household tries to edit, delete, publish or unpublish it
  Then the answer is 403 community_read_only
  And the database would have refused the write regardless

Scenario: The seeded catalog cannot be published
  When I try to share a seeded cocktail
  Then the answer is 403 catalog_read_only

Scenario: A recipe on a custom ingredient is refused, for now
  Given a cocktail of ours with a line on a bottle we added
  When I share it
  Then the answer is 409 custom_ingredient naming that line

Scenario: Dissolving the household takes its editions with it
  Given my household has published two drinks and another household forked one
  When my household is dissolved
  Then both editions are gone
  And the fork is unchanged
  And the seeded catalog is exactly as it was

Scenario: The gate is off by default
  Given Community:Enabled is unset
  Then the publish and unpublish routes are 404
  And pool=community answers 404
  And the features probe says community is off, and the client shows no link

Scenario: Marga on an empty pool
  Given nobody has published anything
  When I open the community page
  Then she says yours could be the first
```

**Tests (planned).** `tests/Api.Tests/Catalog/CocktailPublishTests.cs`, `CommunityBrowseTests.cs`,
the clause added to `SharedCatalogFilterTests`, `Rls/SharedCatalogRlsTests` and
`SharedOrTenantDissolutionTests`, a gate test in
the pattern of the billing one; one journey `CommunityJourneyTests` with two households (the
invitation journeys already run two users), covering publish → read on the other side → the catalog
unchanged → unpublish. UI tests for the links, the credit and the actions row.

**Out of scope, deliberately:** custom ingredients (2); moderation, caps, reports (3); a fourth tab;
comments, likes, counts of forks, anything that makes it a feed; notifications ("X forked your
drink"); a publisher profile page; a public, signed-out view — the pool is for households, behind
sign-in, like everything else.

---

### COMMUNITY-2 — Custom ingredients travel

**Status: 📋 Planned.**

**As a** household sharing a recipe that uses a bottle we added ourselves
**I want** the recipe to read in full for everyone
**So that** a drink built on our own coffee liqueur can still be shared, and copied, and made

**Context / notes.** "Custom ingredients travel by being published, in place" above. The work is
mostly **readers**: every place that lists ingredients *to* a household gains the own-pool
predicate, and a test walks each one with another household's published ingredient in the table.
The fork handler gains find-or-create by name in the forker's household. The detail response's
per-line `availability` gains a value for "another household's ingredient" so the page can say
*theirs*. The slice-1 refusal is removed and its test inverted.

**Acceptance criteria**

```gherkin
Scenario: Publishing a recipe on our own bottle publishes the bottle
  Given a cocktail of ours with a line on a bottle we added
  When I share it
  Then the edition exists with that line intact
  And our bottle carries a published stamp
  And another household reading the recipe sees the bottle's name on that line

Scenario: A published bottle is nowhere else
  Given another household's published bottle
  Then my shelf does not list it, my picker does not offer it, my categories do not count it
  And I cannot tick it, by the single tick or by the whole-shelf write
  And I may add a bottle of the same name to my own shelf

Scenario: The line reads as theirs
  When I open the recipe
  Then that line is marked as the other household's
  And the drink is never makeable for me by that line, substitutions or not

Scenario: Forking brings the bottle
  When I create my own version
  Then my copy's line points at a bottle in MY household with that name and category, unticked
  And if I already had one by that name, it points at mine
  And ticking it makes my copy makeable

Scenario: Unpublishing lets the bottle go private again
  Given our published bottle is used by one edition only
  When I unpublish that edition
  Then the bottle has no published stamp
  And a recipe of ours using it in two editions keeps the stamp until both are gone

Scenario: Deleting a published bottle is refused while an edition uses it
  When I delete it from my shelf
  Then the answer is 409 ingredient_in_use naming the edition

Scenario: Substitutions never touch a published bottle
  Then no substitution row names it in either direction, and the seeder cannot write one
```

**Out of scope, deliberately:** merging a published bottle into the seeded catalog (JJ-017 says
proper names stay out); a published bottle appearing in the substitution graph (JJ-018); the
publisher choosing which bottles to share — the recipe decides.

---

### COMMUNITY-3 — Keeping it clean

**Status: 📋 Planned.**

**As** platform staff
**I want** to remove a shared recipe, and to keep any one household from flooding the pool
**So that** the reason JJ-024 deferred this is answered before the pool is open to strangers

**Context / notes.** The takedown is an **enumerated admin write** (ADR-021): `DELETE
/api/admin/community/{id}`, audited, deleting the edition only — never the household's original, and
unstamping ingredients per COMMUNITY-2's rule. The cap is a plan limit through `IQuotaService`
(`CanPublishAsync`), default in `.env.example`, answering 402 `publish_limit_reached` in the shape
of the seat limit. The *Report* link opens `mailto:` with the recipe's id in the subject — a report
table, a queue and a review screen are a later decision, when there is something to review.

**Acceptance criteria**

```gherkin
Scenario: Staff take an edition down
  Given a published drink
  When staff remove it from the console
  Then the edition is gone and the household's original is untouched
  And the audit log records who removed what
  And forks of it stand

Scenario: The cap
  Given the plan allows N live editions and my household has N
  When I share another
  Then the answer is 402 publish_limit_reached
  And publishing again an existing edition still works

Scenario: Report
  When I choose Report on a community recipe
  Then a mail opens addressed to the operator, naming the recipe
```

**Out of scope, deliberately:** a review queue; user-to-user blocking; a "verified" mark; a
publisher's reputation; a feed of new drinks. Every one of those is a product, not a slice.

---

## Pick-up checklist — the first session does these, in order

1. Read this file, then `PLAN.md`'s three gates. Branch from `develop`.
2. Paste JJ-042 (below) into `DECISIONS.md` with the day's date; move the publishing line in
   `PROJECT_BRIEF.md` from OUT to IN; write `FEATURES.md` §16 from "Where it lives on screen" and
   the acceptance criteria; update `DATA_MODEL.md`'s three entities (`PublishedAt`), the derived-rules
   section ("the pool"), and its pinned-extensions list. One docs commit.
3. Walk the data questions D1–D17 and change any default the maintainer wants changed **before**
   the migration is written.
4. COMMUNITY-1, by the ritual. The migration re-creates the SELECT policies through
   `RlsDdl.SharedOrTenantStatementsFor` (widened, not duplicated) so the DDL stays in one place.
5. Add the row to `CLAUDE.md`'s doc map and the golden rule 9 note about what `PublishedAt` means.

## Rejected alternatives — recorded so they are not re-explored

- **A null-tenant copy with a `social` flag.** Needs a request-path write of a null-tenant row, for
  which no sanctioned path exists; would create a second escape hatch through the tenancy
  foundation; and would need its own erasure contributor and an anonymization story. Rejected
  2026-09-16, above.
- **A live reference** (publish the original in place). Rejected above.
- **A "Community" `RecipeSource` row.** A source is provenance from outside the app (JJ-032); a
  household is inside it. The credit comes from the tenant, the provenance from
  `ForkedFromCocktailId`.
- **Snapshotting the ingredient name on the line.** Rejected above.
- **Community drinks in the catalog with a chip to hide them.** Reverses the maintainer's constraint;
  the pool would leak into every count.
- **A fourth destination in the tab bar.** A `SHELL` decision, not this epic's.
- **A signed-out public page.** Everything in this app is behind sign-in and tenancy; a public view
  is a different product surface with its own caching, crawling and abuse questions.

---

## JJ-042 (draft) — paste at pickup

**JJ-042 — A household may publish a recipe for every household to read; the pool is a page of its
own, the copy stays the publisher's, and the seeded catalog is never written. (amends JJ-024)**
JJ-024 kept household cocktails private for MVP, naming moderation as the cost. The maintainer
(2026-09-16) asked for sharing, with two constraints: the seeded catalog and the community pool are
never mixed, and a shared recipe renders in full for everyone including lines on the publisher's own
ingredients.

*Decision.*
1. **A third state of the household's own rows, not of the catalog's.** `PublishedAt` on
   `Cocktail`, `CocktailIngredient` and `Ingredient`; a row with a tenant AND a stamp is readable by
   every household and writable by its owner only. The read wall — the EF filter and the RLS `SELECT`
   policy — gains `OR PublishedAt IS NOT NULL`, changed together in one migration (JJ-031). The write
   policies do not change. **No null-tenant row is ever written by a request**: the seeded catalog's
   definition stands to the letter.
2. **Publishing is a snapshot edition**, a second row the household owns, `ForkedFromCocktailId`
   naming the private original, `SourceId` null; *publish again* replaces it in place, *unpublish*
   deletes it, forks of it stand (JJ-013). One live edition per original.
3. **The pool is a filter** (`pool=catalog|community`, default catalog) on the one browse handler,
   never a wall and never a second list implementation. The catalog page, Home, Marga's rankings,
   `/unlocks` and `/starters` read the catalog pool always.
4. **Custom ingredients are published in place** with the edition that uses them and unstamped when
   none does; they are visible in a recipe and nowhere a household lists its own bottles; forking a
   community recipe materializes them by name in the forker's household (JJ-018 holds).
5. **Editions die with their household** — they are its rows, and the existing contributor wipes
   them; the credit is the tenant's current name joined at read time, never snapshotted.
6. **Config-gated, default off** (`Community:Enabled`, ADR-027's pattern), with an enumerated,
   audited staff takedown (ADR-021) and a per-household cap before the gate opens to strangers.

*Consequences.* JJ-031's description of the read wall gains a clause and its tests gain a case;
golden rule 9 in `CLAUDE.md` says what a stamp means; `PROJECT_BRIEF.md` moves publishing to IN;
`FEATURES.md` gains §16; the hatch guard is untouched because no new hatch exists. What stays OUT:
a public signed-out view, feeds, comments, reputation, a review queue.

*Drafted 2026-09-16; not decided.*
