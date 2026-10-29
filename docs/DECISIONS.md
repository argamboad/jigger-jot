# Decisions (ADR log)

> Lightweight architecture/product decision records: decision + rationale + date. Stops us (and
> Claude Code) from re-litigating settled choices. Append new ones; supersede with a new dated
> entry rather than rewriting.
>
> **JiggerJot's own decisions**, numbered **JJ-001 …**. The platform's — the constant ADRs (`ADR-C…`) and its build
> decisions (`ADR-…`), ADR-034 the packages among them — are the platform's log:
> [perezosoft-platform `docs/DECISIONS.md`](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/DECISIONS.md). Until the pilot (2026-10, Pkg J.6) this file
> carried a copy of them as of the clone; the packages made the copy stale, so it went.

## JiggerJot decisions (app-specific)

> Numbered **JJ-001 …** rather than ADR-001 … because the platform's own build decisions already
> occupy ADR-001 onward (they were logged here while the platform was built as its own app). The
> JJ numbers preserve the app's original conceptualization sequence (2026-06-17) unchanged, so the
> product docs and the foundations tracker keep their references. Platform decisions are cited as
> `ADR-…` / `ADR-C…`; app decisions as `JJ-…`. When they disagree, the platform wins (JJ-026, JJ-028).

> All entries below: **2026-06-17** (initial conceptualization session).

---

**JJ-001 — Unit of account is the household (tenant), not the user.**
Inventory and custom content are shared across a household/bar. Multiple users can belong to one
tenant. *Rationale:* matches real use (a shared shelf); avoids duplicating inventory per person.

**JJ-002 — Shared catalog is referenced, with copy-on-fork (not copy-per-tenant).**
Households reference the global catalog; "Create my own version" forks an independent copy.
*Rationale:* keeps the shared catalog clean while allowing personalization without mutating
shared records.

**JJ-003 — "Makeable" is always derived, never stored.**
Computed from inventory + recipe lines + substitutions at query time. *Rationale:* a stored flag
would go stale the instant inventory changes.

**JJ-004 — Substitutions are ingredient-level, not recipe-level (MVP).**
A substitution applies globally between two ingredients. *Rationale:* far simpler; covers the
common case (Kahlúa ↔ Tia Maria). Recipe-level subs pinned for future.

**JJ-005 — Substitutions are global-only for MVP.**
No tenant-specific subs yet. *Rationale:* keeps the substitution graph simple and shared.
Tenant-level subs pinned.

**JJ-006 — Substitution symmetry stored as two directed rows.**
Rather than a `symmetric` flag. *Rationale:* simpler, more predictable queries.

**JJ-007 — Structured quantities (amount + unit), stored as authored.**
Not freeform text; not converted on storage. *Rationale:* enables conversion/scaling while
preserving idiomatic units (a dash, a barspoon) losslessly.

**JJ-008 — Unit-system preference is per-user; conversion at display time.**
Metric/imperial toggle on User; convertible units converted on display, neutral units pass
through. *Rationale:* household members can disagree on units; storage stays canonical.

**JJ-009 — Required/optional lives on the recipe line, not the ingredient.**
`is_required` on CocktailIngredient. *Rationale:* "optional" is contextual to a drink (lime is
required in a Margarita, optional elsewhere). A garnish is just an optional line.

**JJ-010 — Recipe lines carry a `role` (base/modifier/juice/garnish/…).**
*Rationale:* cheap to add, enables display grouping; wanted from day one.

**JJ-011 — Single `Ingredient` table with nullable `tenant_id` (null = global).**
Rather than separate global/custom tables. *Rationale:* simpler queries; substitutions and
recipes can span global + custom without unions.
> **Amended by JJ-031 (2026-09-09):** the single table stands, but it does **not** implement
> `ITenantScoped` — that interface's `TenantId` is non-nullable, and its filter and RLS policy would
> hide the null-tenant catalog rows. Isolation comes from an app-level query filter plus a mirrored
> hand-written RLS policy instead.

**JJ-012 — Single `Cocktail` table with nullable `tenant_id` + nullable `forked_from`.**
Same single-table rationale as JJ-011.
> **Amended by JJ-031 (2026-09-09):** as JJ-011 — not `ITenantScoped`; app-level filter + mirrored
> RLS policy. `CocktailIngredient` follows its parent cocktail and is treated the same way.

**JJ-013 — Fork = snapshot copy, not live reference.**
Forking copies the cocktail and all recipe lines; `forked_from` is provenance metadata only.
*Rationale:* households own a stable, independently editable recipe; upstream edits shouldn't
silently change their drink.

**JJ-014 — No manual `base_category` / "main spirit" field.**
Spirit/ingredient filtering is derived from recipe lines + ingredient categories. *Rationale:*
the manual field is fragile (no-spirit, two-spirit drinks); derived filtering is more powerful
("everything with elderflower"). Manual primary classification pinned for future curation.

**JJ-015 — Two-level ingredient categorization (category + subcategory).**
Self-referencing IngredientCategory (parent/child). *Rationale:* lets users filter "all rum" or
drill into "dark rum" without over-engineering a deep hierarchy.

**JJ-016 — Ingredient filtering matches by category AND by name.**
*Rationale:* "vodka" should find both the Vodka category and name matches; maximizes recall.

**JJ-017 — Generic ingredients only for MVP (no brand/product granularity).**
Recipes and inventory both operate on generic ingredients ("vodka," not "Tito's"). *Rationale:*
brand hierarchy is a whole subsystem; generic covers the core loop. Pinned.

**JJ-018 — Custom ingredients satisfy recipe lines by exact match only.**
They don't participate in the (global) substitution graph in MVP. *Rationale:* keeps the sub
graph shared/clean; revisit alongside tenant-level subs.

**JJ-019 — "Almost makeable" is in MVP, fixed at exactly one missing required ingredient.**
*Rationale:* high-value discovery/shopping feature, trivial given the model; N=1 keeps UX focused.

**JJ-020 — Ice and water are assumed always available, not modeled as blocking inventory.**
*Rationale:* otherwise every tenant must check ice or nothing is makeable.

**JJ-021 — Onboarding wizard seeds initial inventory.**
*Rationale:* avoids the empty-inventory cold-start where "what can I make" returns nothing.

**JJ-022 — Curated lookup tables for GlassType / Method / Unit; no tenant additions in MVP.**
*Rationale:* consistent filtering; user-added values (e.g. "tiki mug") pinned for future.

**JJ-023 — Inventory is boolean (available / not), no "running low."**
*Rationale:* simplest model that serves the core loop. Multi-state pinned.

**JJ-024 — Household cocktails are private only in MVP.**
No publishing to a community pool yet. *Rationale:* the community database is the long-term
vision but adds visibility/moderation complexity; defer. Pinned.
*Note (2026-09-16):* still in force. A plan to amend it exists — `docs/stories/community.md`,
epic `COMMUNITY`, with **JJ-042 drafted inside the story** — and is pasted here only on the day
the epic starts. Nothing is decided by the plan's existence.

**JJ-025 — Documentation set for solo+Claude Code: BRIEF, FEATURES, DATA_MODEL, DECISIONS,
CLAUDE.**
A full corporate PRD is overkill solo; PROJECT_BRIEF + FEATURES cover the useful PRD content.
*Rationale:* persistent, lean context for Claude Code over alignment ceremony.

---

> Entries below: **2026-09-08** (platform-alignment session).

**JJ-026 — JiggerJot is a downstream app of perezosoft-platform; this repo documents only the
product, not the stack or the process. (2026-09-08)**
The tech stack (ASP.NET Core API, Blazor WASM, shared RCL, PostgreSQL/EF Core, custom JWT auth,
MAUI Blazor Hybrid shells), the architecture (a clean-layered horizontal platform chassis with app
features built as vertical slices under `src/Api/Features/<Feature>/`), the process (slices,
per-epic Gherkin stories, TDD, Conventional Commits, PR template), `CLAUDE.md`, and the constant
ADRs (`ADR-C1` … `ADR-C15`) are all owned by **perezosoft-platform** and arrive when the app repo
is created from that template. They are **not re-decided or re-documented here**.

Consequences for this repo:
- The doc set is **product-only**: `PROJECT_BRIEF.md`, `FEATURES.md`, `DATA_MODEL.md`,
  `DECISIONS.md` (this file, app ADRs only), and per-epic `stories/`. This supersedes the doc list
  in JJ-025.
- `TECH_STACK.md` and `WAYS_OF_WORKING.md` were **removed** — the platform's versions are
  authoritative and this repo's copies had already drifted (see next point).
- The former stack/process block (numbered 026 … 037 on 2026-06-17) was **removed** rather than
  superseded one by one: it duplicated the platform's constant ADRs and in one case contradicted
  them outright (it claimed auth is ASP.NET Core Identity; the platform's ADR-002 is custom JWT +
  rotating refresh tokens, explicitly *not* Identity). Keeping a wrong stack record here would
  only mislead. Anything of lasting value in that block lives in the platform's `DECISIONS.md`.
- Every JiggerJot feature is built as a **vertical slice** on top of the platform's horizontal
  chassis, per platform ADR-004 and `docs/WAYS_OF_WORKING.md` there.
- The one carry-over that *is* app-specific — the **`JiggerJot` code name / namespace root**,
  decoupled from the customer-facing brand — is retained as JJ-027 below.

*Rationale:* the platform primer is explicit that per-app documentation covers concept, features,
data model, scope, and domain decisions, with the stack frozen upstream. Splitting the docs along
that seam keeps this repo honest about what it actually decides, and stops two copies of the same
decision from drifting apart (as they already had on auth).

**JJ-027 — App code name is JiggerJot; product/brand name kept separate. (2026-06-17, retained
2026-09-08)**
The solution and namespace root is **JiggerJot** (project names, root namespaces, the EF context).
The customer-facing brand name is deliberately decoupled and may differ or change later, living
only in UI strings/marketing — never in namespaces. *Rationale:* the namespace root is expensive to
change in .NET, so it's locked before scaffolding; the brand is cheap to change and shouldn't be
coupled to code. "JiggerJot" verified to have no existing collision (jigger = a 1–2 oz bar
measure; jot = to note/save), giving a clean repo/namespace/domain. Casing is PascalCase compound:
`JiggerJot`. The platform's rebrand step (`docs/REBRANDING.md` there) is where this name gets
applied to the cloned tree.

**JJ-028 — Tenant and User are platform-owned; JiggerJot does not re-model them. (2026-09-08)**
`DATA_MODEL.md` previously defined `Tenant` and `User` as app entities, with a required
`tenant_id` column on `User`. Both are provided by perezosoft-platform, which links a user to a
tenant through a **`TenantMembership`** row (one tenant per user at a time; owner / admin / member
roles; invitations) and enforces tenant scoping on every query — there is **no `tenant_id` on
`User`**. The app doc now references the platform's Tenant and User instead of defining them, and
adds only `preferred_unit_system` at the user level. The product model is unchanged: JiggerJot is
still multi-tenant (households) and multi-user; the household is still the unit of account
(JJ-001) and only the unit preference is per-user (JJ-008). *Rationale:* the platform's tenancy
is already built, tested, and structurally enforced; describing a different mechanism here would
either be ignored at build time or, worse, followed. **When app docs and the platform disagree,
the platform wins.**

**JJ-029 — Brand: the customer-facing name is JiggerJot, the tagline is "Mix what you have.",
the mark is a jigger, and the palette is copper on a cool near-white. (2026-09-08)**
These are the three hand-over items the platform primer requires before the rebrand (name, tenant
label, logo), settled together with the tagline and palette the rebrand derives from the logo.
- **Name.** The brand is **JiggerJot**, the same as the code name (JJ-027 keeps them decoupled in
  code; they simply coincide for now). Verified 2026-09-08: no existing product uses the name;
  `jiggerjot.com`, `.app` and `.io` were all unregistered. The cocktail-app space already has
  *Jigger* and *MyJigger*, so the compound is the asset — never shorten the brand to "Jigger".
- **Tagline.** "**Mix what you have.**" — states the differentiator (availability-driven, not just
  recipes) in four words, works in the lockup and the email footer, and localizes cleanly for the
  platform's ES resources as "*Mezcla lo que tienes.*" Replaces "Lazy reputation. Efficient
  engineering." everywhere (`lockup_light.svg`, `BrandedEmail.cs`).
- **Mark.** A jigger drawn the way one stands on a bar: the larger cup down, the smaller up,
  elliptical rims so the cups have volume, a banded collar joining them. One even-odd vector path
  in a 100-unit box (the mouth and the base rim line are holes, so it sits on any ground). Source
  and every derived asset live in **`brand/`** in this repo, in the platform's folder layout
  (`Shared.Ui/`, `Web/`, `Maui/`, `Email/`) with the exact filenames and pixel sizes
  `docs/REBRANDING.md` §3 lists; `brand/build.py` re-renders the PNGs and `favicon.ico` from the
  SVGs (headless Edge + Pillow). The icon treatment is bone `#F3F1EC` on night `#111418` on every
  platform (launcher, store, PWA, splash); in the product the mark is copper. In the MAUI project
  file, both `MauiIcon Color` and `MauiSplashScreen Color` become `#111418`.
- **Palette** (→ `app.css` `:root` tokens and the `BrandedEmail.cs` constants, renamed):
  primary / `--bs-primary` copper `#B4562A` (white text on it: 4.9:1, AA); `--brand-dark` `#8C3F1D`;
  `--brand-accent` brass `#D9A441`; `--brand-accent-light` `#F6E7DE`; `--app-bg` `#F4F5F7`;
  `--app-border` `#E4D8CF`; ink `#1B1F24`; muted `#5B6470`. Dark theme lifts copper to `#D9865A`
  and the dark step to `#F0A87E`. Email constants: `Green`→`Copper`, `GreenDark`→`CopperDark`,
  `Sage`→`Brass`, `SageLight`→`BrassLight`; `Surface`/`Border`/`Ink`/`Muted` keep their names.
- **Wordmark.** Barlow Semi Condensed 600, "Jigger" in ink and "Jot" in copper; tagline in IBM
  Plex Sans 500. Both on Google Fonts. The lockup SVGs keep live text as the editable source; the
  PNG renders carry the real glyphs, which is what the UI references.
*Rationale:* one name to verify and defend instead of two; a tagline that says what the product
does rather than how it feels; a mark that is literally the product's namesake and reads at 16 px;
copper as a colour no cocktail app in the results owns and one that stands clearly apart from the
platform's green. Deciding the palette with the mark (rather than at rebrand time) means Phase 3 is
mechanical. Registering the domain is the owner's action and is not part of this ADR.

*Amendment (2026-09-08, Phase 3 — the rebrand as applied).*
1. **Assets now live in place, not in `brand/`.** The editable SVG sources are
   `src/Shared.Ui/wwwroot/brand/*.svg`, `src/Web/wwwroot/favicon.svg` and
   `src/Maui/Resources/{AppIcon,Splash}/*.svg`; every PNG, `favicon.ico` and the `docs/brand/`
   store/marketing set are regenerated by **`docs/brand/build_assets.py`**. The `brand/` staging
   folder was removed once applied.
2. **Two palette values differ from the table above, on purpose.** The pale tint `#F6E7DE` is a UI
   chip colour, not a text colour: in `app.css` the `--brand-accent-light` token (which the dark
   theme uses for link text) is the lifted copper `#D9865A`, and in `BrandedEmail.cs` the muted
   small-text roles are `Brass = #9C6A3F` and `BrassLight = #7A6E66` (both ≥ 4.5:1 on white). The
   primary/dark/surface/border/ink/muted values are as tabled.
3. **The rename went beyond `REBRANDING.md`'s list** to satisfy its verify step: the solution,
   every project file, root namespaces, the RCL static-asset path, CI smoke identifiers, the
   Postman files and the Docker tag became `JiggerJot.*`/`jiggerjot`; `ApplicationId` is
   **`com.jiggerjot.app`**, the Render service is **`jiggerjot-staging`**; the platform's
   conceptualization primer and its tutorial course (`docs/tutorial/`) were removed as
   template-only material; `README.md` was rewritten for the app.
4. **One accepted exception to the checklist's "no old-brand mention anywhere" verify:** the
   upstream repo slug `perezosoft-platform` is kept where it names the platform as provenance (this
   file, the brief, `STATUS.md`, two platform stories, the gitleaks config title, two audit logs).
   The verify for this app therefore greps for the old brand name case-insensitively, excludes
   `docs/REBRANDING.md`, and filters out that slug; expected empty (verified 2026-09-08).

**JJ-031 — How the shared catalog coexists with tenant isolation: one table, app-level filter,
mirrored RLS policy. (decided 2026-09-09)**
*Amends JJ-011 and JJ-012. Read with platform ADR-003 (tenancy) and ADR-020 (RLS backstop).*

**The problem.** JJ-011/JJ-012 specify one table each for `Ingredient` and `Cocktail` with a
**nullable `tenant_id`** — null = shared seed catalog, set = household-owned. Read against the
platform as actually built, that shape **cannot be implemented**:

1. `ITenantScoped` declares `Guid TenantId` — **non-nullable**. An entity implementing it cannot
   hold a null tenant, so the "null = shared" row is not expressible.
2. The global filter is `e.TenantId == CurrentTenantId` (`AppDbContext.ApplyTenantFilter`). Even if
   the column were nullable, `NULL == <guid>` is never true, so shared rows would be invisible.
3. The RLS policy (`RlsDdl.StatementsFor`) uses the same predicate with `FORCE`, so shared rows are
   hidden **at the database** too — and since 2026-09-09 that backstop is genuinely enforcing on
   staging (a non-`BYPASSRLS` runtime role), so this is no longer theoretical.
4. `TenantStampingInterceptor` stamps the current tenant onto unset `ITenantScoped` inserts and
   **throws** on a foreign tenant, so seeding null-tenant rows through the app is refused.
5. `IgnoreQueryFilters()` is **banned in `src/Api/Features/**`** by a build gate, so a slice cannot
   opt out to see the catalog.

So a decision is required before any entity or migration is written. Three shapes were considered.

**Option A — split tables.** A global `Ingredient`/`Cocktail` (not `ITenantScoped`, no tenant
column) plus a separate household-owned `TenantIngredient`/`TenantCocktail`. *For:* uses only
existing platform concepts; the shared side is ordinary reference data like `GlassType`. *Against:*
`CocktailIngredient` must point at either table — a polymorphic foreign key (two nullable columns +
a check constraint), which infects every query, and "list all my ingredients" becomes a union
everywhere.

**Option B — one table, app-level filter + mirrored policy (recommended).** Keep the single-table
design of JJ-011/JJ-012. `Ingredient`/`Cocktail`/`CocktailIngredient` carry a **nullable
`TenantId`** and deliberately do **not** implement `ITenantScoped`. Isolation is restored, not
abandoned, by two mirrored rules:
- an app-defined EF query filter `x.TenantId == null || x.TenantId == CurrentTenantId`, applied in
  `AppDbContext.OnModelCreating` next to the platform's own loop; and
- a hand-written RLS policy in the same migration with the matching predicate
  (`"TenantId" IS NULL OR "TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::uuid OR
  current_setting('app.rls_bypass', true) = 'on'`).

*For:* keeps simple foreign keys and the model the product docs already describe; keeps a
structural read guarantee and a database backstop; shared rows are readable by every household and
owned by none. *Against:* it is a **local divergence from the platform convention** — a reader who
knows the codebase will assume "no `ITenantScoped`" means "not filtered", so the entities and the
`AppDbContext` block must say plainly why. Two guarantees are also **not** inherited and must be
supplied by hand: the write-stamping interceptor will not stamp these rows (custom rows must set
`TenantId` explicitly at the call site), and `RlsMigrationGateTests` only checks `ITenantScoped`
tables, so nothing fails CI if the hand-written policy is forgotten — an app-level test must assert
it, or the backstop silently disappears on a later migration.

**Option C — a catalog tenant.** Shared rows belong to one well-known system tenant and the filter
widens to `TenantId == CurrentTenantId || TenantId == CatalogTenantId`. *For:* every row keeps a
real tenant, so `ITenantScoped` and the stamping interceptor still apply. *Against:* the widened
predicate has to replace the platform's generated filter **and** its RLS policy for those tables, so
it diverges from the platform in the one place hardest to keep in step; and a fictional tenant row
leaks into membership, export and dissolve paths that reasonably assume tenants are households.

**Decision: Option B**, with the two hand-supplied guarantees written as tests in the same slice —
one asserting the query filter admits shared rows and excludes another household's, one asserting
the policy exists on each table after migration. It preserves the product model, and the divergence
is one well-commented block rather than a shape that spreads through every query.

**Binding consequences for Phase 5.** `Ingredient`, `Cocktail` and `CocktailIngredient` carry a
nullable `TenantId` and do **not** implement `ITenantScoped`; `TenantInventory` still does (it is
purely household data). `IngredientCategory`, `GlassType`, `Method`, `Unit` and
`IngredientSubstitution` are global lookups with no tenant column at all (JJ-005, JJ-022). Every one
of the three dual-natured tables ships its hand-written policy in the same migration that creates
it, and inserting a household-owned row must set `TenantId` explicitly — nothing stamps it.

**Upstream:** if B works, it is a candidate to become a platform primitive
(`ISharedOrTenantScoped` + `RlsDdl` support), already filed as `PLATFORM_BACKLOG.md` §15 item 2 —
any app with a seeded catalog hits this exact wall.

*Decided 2026-09-09. Options A and C are recorded above so the choice is not re-litigated.*

> **Amended 2026-09-09 (implementing CKTL-1).** Building Option B turned up two things the decision as
> written got wrong, both in the direction of being too trusting. Neither changes the choice; both change
> what "mirrored RLS policy" means in practice.
>
> **① One `FOR ALL` policy would have left the shared catalog deletable.** The predicate this ADR
> specified — `TenantId IS NULL OR TenantId = current OR bypass` — is right for reading and wrong for
> everything else. Postgres checks `DELETE` against `USING` and never against `WITH CHECK`, so a single
> `FOR ALL` policy carrying that predicate lets any household delete or update any shared catalog row —
> exactly what JJ-002 says must be impossible, and the one guarantee the whole seeded-catalog design rests
> on. The shipped shape is therefore **four command-scoped policies per dual-natured table**
> (`RlsDdl.SharedOrTenantStatementsFor`): `SELECT` admits shared rows, while `INSERT`, `UPDATE` and
> `DELETE` admit the household's own rows only. Writing a shared row now requires the bypass GUC, which
> makes seeding the catalog a deliberate, greppable act rather than the default for an unset `TenantId`.
> `SharedCatalogRlsTests` pins each half, including the mirror case (a household CAN delete its own row),
> so the policies cannot pass by forbidding everything.
>
> **② A fourth platform guarantee does not apply, not three.** The ADR named the stamping interceptor and
> the RLS migration gate. The third is **tenant dissolution**: the platform canary
> `EveryTenantOwnedEntity_IsWiredIntoTenantDissolution` only inspects entities with a **non-nullable**
> `Guid TenantId`, and every `ISharedOrTenantScoped` entity has a nullable one by construction — so a
> missing `ITenantDataContributor` on these tables would orphan a dissolved household's recipes with no
> test failing anywhere. `CatalogDataContributor` supplies the teardown and export (household rows only,
> shared catalog untouched); `SharedOrTenantDissolutionTests.EverySharedOrTenantEntity_IsWiredIntoTenantDissolution`
> is the app-level canary that replaces the blind one. `InventoryDataContributor` does the same for
> `TenantInventory`, which the platform canary *can* see and now lists. *(2026-09-24, v4 audit H6 port: the
> platform canary was widened to nullable `Guid?` keys — to catch `OutboxMessage` — and now lists
> `Ingredient`, `Cocktail` and `CocktailIngredient` too, so it is no longer blind here; the app-level canary
> stays.)*
>
> **Upstream:** both corrections belong with the `ISharedOrTenantScoped` primitive proposed in
> `PLATFORM_BACKLOG.md` §15 item 2 — the `FOR ALL` trap in particular is not obvious from reading the
> platform's own policy, since a non-nullable `TenantId` makes the asymmetry unnecessary there.

*Amendment (2026-09-08, Phase 2):* the four product docs were merged into this repo's `docs/`
when the platform tree was adopted (JJ-026's product-only doc set now lives alongside the
platform docs); `WAYS_OF_WORKING.md`, `TECH_STACK.md` and `CLAUDE.md` are the platform's.

**JJ-030 — Local ports are distinct from the platform's so both stacks run side by side. (2026-09-08)**
perezosoft-platform, vuelto and JiggerJot are developed on the same machine, and the platform's
launch profiles pin the app ports (ADR-C13 makes only the compose ports env-driven). The neighbours
occupy: platform API 7160/5238, Web 7008/5169, compose 5433/1025/8025; vuelto API 5000, client 5001,
a local Postgres on 5432 and a compose stack on 5434/1026/8026. JiggerJot therefore re-pins to a
free set: **API `https://localhost:7360` + `http://localhost:5438`** (the cleartext leg the
Android emulator reaches via `adb reverse`), **Web `https://localhost:7208` + `http://localhost:5369`**,
and in `.env.example` **`DB_PORT=5435`, `MAIL_SMTP_PORT=1027`, `MAIL_UI_PORT=8027`, `APP_PORT=8280`**
(the last for the optional prod-like `app` compose service, which the platform leaves on 8080). The
compose project name comes from the folder (`jigger-jot`), so containers, network and the `db_data`
volume are already namespaced apart from `perezosoft-platform-*` and `vuelto-*`. The committed dev
defaults follow (`appsettings.Development.json` SMTP port and CORS/`AppBaseUrl`, the Web client's
`ApiBaseUrl`, the MAUI fallbacks and `adb reverse`, the E2E and Android-smoke Mailpit defaults, the
Postman local environment, the docs). CI keeps its container-side ports (`5432`, `1025`, `8025`) and
maps Mailpit host-side to `1027`/`8027` so the committed defaults stay coherent there; `docker-compose.yml`
is untouched (its `${VAR:-default}` fallbacks are overridden by `.env`). Excluded from the rewrite:
vendored Bootstrap (where `5169` is a timing constant), the seed JSON (where `1025` is a record id),
lockfiles, audit logs. *Rationale:* the alternative — remembering to stop one stack before starting
the other — is exactly the kind of friction that gets skipped; a one-time mechanical re-pin, verified by
the same build and tests, removes it. OAuth redirect URIs are registered per app anyway, so nothing
breaks upstream.


**JJ-032 — Seed sources: the Savoy for depth, the IBA list for the canon; the two 1930s bar books
are dropped. (decided 2026-09-09; amended by JJ-043)**
*Closes the rights question raised in `docs/stories/seed.md`. Read with JJ-017 (ingredients are
generic, never brands) and the PROJECT_BRIEF scope line. **Amended 2026-10-09 by JJ-043:** where
both sources have a drink, only the IBA spec ships.*

**The problem.** Four sources were on the table: the scraped 1930 Savoy Cocktail Book, two local
PDFs (Jerry Thomas's 1862 *Bar-Tender's Guide* and 1931's *Old Waldorf Bar Days*), and whatever
online database might serve. Two questions had to be answered together, and separating them was the
mistake that made this look hard.

**The rights question.** The three books sit on different footing. Jerry Thomas is long in the US
public domain. The Savoy entered it on 1 January 2026. *Old Waldorf Bar Days*, published in 1931,
does not until **1 January 2027** on the 95-year term. Crediting a source is not the same as being
licensed by one, so a plan to attribute does not move that date.

**The coverage question, which turned out to be the same question.** Measured against the actual
Savoy extraction: 868 recipes containing **zero tequila, zero bourbon, zero Aperol, one line of
Campari and four of vodka**. The product exists to answer "what can I make right now with what I
actually have"; against a shelf stocked the way shelves are stocked today, a Savoy-only catalog
answers "nothing". Jerry Thomas is older still and Waldorf is contemporaneous, so **neither book
closes that gap** — and they cannot, because anything old enough to be free predates the drinks
people now ask for by name. Adding books buys more of what we already have.

**Options considered.**
- *A — the three books.* More period depth, the same modern hole, plus the Waldorf timing question.
- *B — Savoy plus a modern core authored in-house.* Fixes the gap, but invents a house standard for
  drinks that already have an authoritative one, and puts the burden of being right on us.
- *C — a public cocktail database.* Surveyed. TheCocktailDB is free and cheap to license but
  user-contributed, which is the quality problem restated. Kindred Cocktails is genuinely well
  curated and its terms forbid scraping "without prior authorization and licensing", so it is a
  conversation rather than a download.
- *D — Savoy plus the IBA official list (chosen).*

**Decision: D.** The Savoy supplies 868 recipes of vintage depth. The **IBA official list** supplies
the modern canon: 102 drinks in three groups of 34 — The Unforgettables, Contemporary Classics, New
Era Drinks — published by the International Bartenders Association, authoritative rather than
crowd-sourced, and small enough to normalise by hand. It is where the Margarita, the Negroni and the
Espresso Martini live, and they live in no public-domain book at all.

**Consequences.**
1. **Waldorf and Jerry Thomas are dropped.** This removes the January 2027 wait and two
   optical-character-recognition cleanups, one of them rough. *(Amended 2026-09-09: the two PDFs
   were deleted rather than kept locally. Neither was ever extracted, so nothing was lost but 155 MB,
   and a half-finished source sitting in the workspace is an invitation to pick it back up without
   re-reading why it was dropped. `seed/sources/` stays gitignored for future raw material.)*
2. **Specifications only, from every source.** Name, category, ingredient lines, amounts, method,
   garnish. Prose, headnotes, video and photography are left where they are. A list of ingredients
   with functional directions is thin ground for copyright in the US; the writing around it is not.
3. **Attribution ships with the data**, not in a footer — `seed/iba_cocktails.json` carries a
   `source_note`, and per-cocktail provenance goes into the model before SEED-3 seeds a single
   recipe, so a credit is a property of the row rather than a promise about the page.
4. **One thread stays open, narrowed.** The Savoy extraction came from `savoycocktaildatabase.com`,
   a modern transcription, and a transcriber's selection and arrangement can carry rights the 1930
   text does not. Re-deriving from a public-domain scan would close it. Not urgent, and no longer
   entangled with anything else.

**How the IBA extraction was taken.** `seed/scrape_iba.py`, enumerating from the site's own sitemap
rather than walking paginated HTML, one request per drink with a pause between and an identifying
User-Agent; `robots.txt` disallows only `/wp-admin/` (checked 2026-09-09). The list being three
equal groups of 34 is used as a correctness check on the parse, and earned its keep immediately: it
caught two breadcrumb misreads that would otherwise have shipped every drink under the wrong
category.

*Decided 2026-09-09. Options A, B and C are recorded so the choice is not re-litigated.*


**JJ-033 — Ingredients are generic, except where the product IS the ingredient. (decided 2026-09-09)**
*Amends JJ-017. Surfaced by building the SEED-2 curation against the real vocabulary.*

**The problem.** JJ-017 says ingredients are generic, never brands — "vodka", not a particular
distillery. Applied literally to the 395 raw names coming out of the two extractions, that rule
deletes the catalog. Chartreuse, Bénédictine, Campari, Fernet Branca, Maraschino, Cointreau, Amer
Picon, Angostura, Peychaud's, Drambuie, Aperol, Cynar, Falernum, Swedish punsch and Lillet are all
proprietary products, and all of them are load-bearing: a Last Word made with "herbal liqueur" is not
a Last Word, and a household that ticks "herbal liqueur" on its shelf has told the makeable engine
nothing it can use.

**The distinction JJ-017 was actually drawing.** Its own example gives it away — "vodka, not Tito's".
The rule is about **substitutable** products, where the brand on the bottle is a shopping preference
and the ingredient is the category. It was never about products that have no generic equivalent.

**Decision.** An ingredient is stored generically **when a generic exists**, and by its proper name
**when the product has no generic substitute**.

| The books say | We store | Why |
|---|---|---|
| Bacardi Rum | White rum | Any white rum makes the drink |
| Smirnoff Vodka | Vodka | As above |
| Canadian Club Whisky | Canadian whisky | As above |
| Lagavulin 16y | Scotch whisky | A recipe naming an age statement is a recommendation |
| Green Chartreuse | Green Chartreuse | Nothing else is Chartreuse |
| Bitter Campari | Campari | "Aperitivo bitter" would not make a Negroni |
| Angostura Bitters | Angostura bitters | Not interchangeable with orange bitters |

**How the line is drawn in practice:** could a bartender hand you a different bottle and have made the
same drink? If yes, store the category. If no, store the product.

**Consequences.**
1. The catalog holds proper names where it must. `seed/ingredient_map.json` is where the judgement is
   recorded, one line per ingredient, so the calls are reviewable rather than implicit.
2. **The substitution graph (SEED-4) carries the weight this rule does not.** Cointreau and triple sec
   are stored as one ingredient here because they genuinely interchange; where two proper-name products
   are near-substitutes and we keep both, the graph is the place to say so — not a merge that loses the
   distinction, and not silence that pretends there is none.
3. JJ-017 stands unchanged for the case it was written for. This adds the exception it did not
   anticipate, rather than reversing it.

*Decided 2026-09-09.*


**JJ-034 — A recipe's glass and method are optional. (decided 2026-09-09; the glass half retired by
JJ-043)**
*Amends the `Cocktail` shape in `docs/DATA_MODEL.md`. Forced by SEED-3, measured before deciding.*
***Retired in part 2026-10-09 by JJ-043:** a glass is required on every recipe, and a recipe whose
source names none does not ship. Method stays optional. The rule against filling a fact in silently
stands.*

**The problem.** `Cocktail.glass_type_id` and `method_id` were non-nullable, on the reasonable
assumption that every recipe states both. Against the real catalog, they do not:

| | of 969 seeded recipes |
|---|---|
| state no glass at all | 96 |
| state a "glass" that is not a glass type | 163 |
| state no method | 94 |

The second row is the interesting one. The Savoy's most common glass instructions are **"medium size
glass"** (76 recipes) and bare **"glass"** (32). Those are not glass types; they are a 1930 bar book
assuming you can see the bar.

**Options.**
- *Fill them in.* Pick a plausible glass for each. Rejected: it puts a fact in the database that
  nobody wrote down, and once written it is indistinguishable from a fact that somebody did. A
  reader disagreeing with the choice cannot even tell a choice was made.
- *Drop the recipes.* 259 of 969, including most of the punches and coolers. Absurd.
- *A "Not specified" lookup row.* A sentinel that every query has to remember to special-case, and
  that shows up in a browse filter as though it were a kind of glass.
- *Make both nullable (chosen).*

**Decision: nullable.** Null reads as "the recipe does not say" and filters as such. Nothing depends
on either field: makeability is derived from inventory, recipe lines and substitutions (JJ-003), and
glass and method are browse facets (FEATURES §11), where "not specified" is an ordinary answer.

**Consequences.**
1. The UI shows the glass and the method when there is one, and says nothing when there is not.
   Neither is ever invented at display time either — the same rule, one layer up.
2. **A household authoring its own cocktail is not forced to pick a glass**, which is a better first
   experience than a required dropdown on a screen whose point is to capture a drink quickly.
3. The seed pipeline reports "unstated" and "too vague to map" as separate numbers, because they are
   different data-quality stories and only the second is ours to improve.

*Decided 2026-09-09.*

**JJ-035 — A ranked "which bottle unlocks the most" IS in MVP, reversing two earlier exclusions.**
`ALMOST-1` shipped saying a ranked shopping list was out of scope, and `FORK-1` repeated it. Both were
right at the time and both are now overturned deliberately rather than quietly.

*What changed.* The starter catalog puts 81 drinks one bottle away on a modest shelf. Per-drink the
answer is correct and useless: eighty-one rows each naming a bottle is a list nobody reads. Grouped by
the missing ingredient it becomes one sentence — *buy this and four open up* — which is the same data
answering the question a person actually has. The UI proposal (`MARGA`) depends on it for three
screens, and nothing else in that proposal needs new data at all.

*Rationale.* It costs one query and no schema. It reuses `ALMOST-1`'s predicate verbatim, so the two
readings of the set cannot drift, and a test walks both to prove it. The earlier exclusions were about
building a *shopping list feature* — multi-bottle combinations, saved lists, quantities — and those
stay out.

*Consequences.* Ties order by count then by ingredient name, so the answer does not change between
requests with nothing behind it. The count and the drinks it names are one value read twice, never
two values computed twice, so the endpoint never truncates the names it returns. Suggesting a first
bottle to a household that is one away from *nothing* is a different question with no set to rank —
that stays open, logged against `MARGA-3`.

*Decided 2026-09-10.*

**ADR-025 — (number reserved; never adopted) CI runner selection is variable-driven with a hosted fallback (LOCALCI-1). (drafted 2026-09-08)**
*Stub.* The draft lives in [`stories/localci.md`](stories/localci.md) ("ADR-025 draft") and was to be pasted
here with LOCALCI-1's first commit. It never was: LOCALCI-4 (ADR-028) replaced the variable-driven design
before it was built, and ADR-030 then retired self-hosted CI altogether. The number stays reserved so the
references to it in `localci.md` resolve; do not reuse it.

**ADR-026 — (draft, not yet adopted) The platform becomes the reference implementation of a stack-neutral spec (FLAVORS). (drafted 2026-09-08)**
*Stub.* The draft lives in [`stories/flavors.md`](stories/flavors.md), with the spec ADRs S-001..S-003 beside
it, and is pasted here when the FLAVORS program starts (SPEC-1). Nothing is decided until then.

**ADR-027 — Pre-launch gates: billing and account creation are deployment configuration, not runtime switches (GATES-1/2). (2026-09-11)**
A deployment must be able to run **private and free** before it is published: nothing offers to sell
a tester anything, and a stranger who finds the URL cannot create an account. Hiding the deployment
itself was explicitly **not** wanted — knowing the app exists is fine.

Both properties are decided before the process starts and change exactly once, on launch day. That
makes them configuration, and it puts them in the same family as the PUBAPI/HOOKS gates
(ADR-015/016): default off, opted into per deployment.

**Decision:**
1. **`Billing:Enabled`, default off.** With the gate off the billing controllers and the provider
   webhook are removed from the application model at startup, so the routes do not exist (404) rather
   than existing and refusing. The client hides the billing link and refuses the route, but the API
   remains the authority. No new economics code is needed: `PlanCatalog.Get` already falls back to
   Free for an absent plan key, so with billing off **every tenant without a granting subscription is
   Free** — and nobody can be *given* one (the routes, the webhook and the staff comp are gone).
   *(Corrected 2026-09-28, v4 T46: the original text said "every tenant is Free"; plan resolution is
   gate-blind by design — a granting row, a comp or a Stripe row from before the gate closed, keeps its
   plan so that flipping the gate never silently downgrades anyone. `BillingPostureCheck` warns at startup
   when Stripe-managed rows linger while off, since the webhook that would cancel them is gone.)*
2. **`Signup:AllowedEmails` / `Signup:AllowedDomains`, empty means open.** Non-empty restricts
   account creation. The green list decides **who may found a household**; inside a household owned
   by a green-listed person, membership is that owner's business, bounded by the seat cap.
3. **The invitation bypass keys on the household's OWNER, not the inviter.** A valid pending
   invitation addressed to the caller admits them when that invitation's tenant owner is
   green-listed. Checking the owner (rather than `InvitedByUserId`) is what lets a non-listed admin
   member invite into their owner's household, while keeping a household whose owner is not listed
   from admitting anyone new.
4. **The gate fires at account creation only, never at sign-in.** It lives at the single choke point
   `UserService.CreateUserWithTenantAsync`, which every user-minting path funnels through (magic
   link, OTP, web OAuth, native OAuth). Gating sign-in would lock out people who already have data
   the moment the list is edited.
5. **It deliberately does not fire when a magic link or OTP is issued.** Refusing at issue time would
   require knowing whether the address already has an account, turning the login form into a "does
   this person use the app" oracle. Accepted cost: a non-listed visitor learns they are not invited
   only after entering their code.

**Addendum (2026-09-28, v4 audit T45 — BILL-2/BILL-3/ADV-P4-6, R86):** the proof behind decision 1 is the
**route table**, not a list of controllers. `BillingGateConvention` still removes the two billing controllers,
but what CI holds is `BillingGateTests.GateOff_NothingUnderTheGatedPrefixes_IsMapped`: the host booted at the
shipped defaults maps nothing under `api/billing`, `api/public`, `api/apikeys` or `api/webhooks`, by any
mechanism (a minimal-API group, an action-level absolute route, a differently named controller — the
adversarial pass had a billing-prefixed group answering 200 next to a 404 on `/api/billing`). The relaxed
Stripe startup check (the fake provider is tolerated outside Development while the gate is off) depends on
exactly that proof. The staff **comp/revert** actions under `api/admin` are part of the surface: they answer
**404 while billing is off**, before the staff check, and the console's block follows `GET /api/features`.

**Addendum (2026-09-28, v4 audit T46 — LB-BILL-21/22/27, R128):** the Stripe mapping **fails safe and
loud**. A missing `current_period_end` (Stripe.net's field is a non-nullable `DateTime`) is stored as null
with a Warning, not as the converter's sentinel date (the Unix epoch — which read as a lapsed period: a paying
tenant on Free, "renews on 1970-01-01", nudged every six hours). A price absent from `Billing:Stripe:Prices` is acknowledged, **not
applied**, and logged as an Error — never an "active Free" row staff cannot comp around. An event whose
`livemode` disagrees with the deployment's expectation (`Billing:Stripe:ExpectLiveKey`, else inferred from
the key prefix) is ignored with a Warning, so a test-mode signing secret in production cannot let Dashboard
test events move real tenants' plans. Signed fixtures drive all three (`StripeBillingProviderTests`).
6. **The Free seat limit moves 3 → 5** (`PlanCatalog`, code/config per ADR-006). Three is exactly one
   family with no headroom, and a pending invitation already consumes a seat.

**Rejected — a runtime toggle on the `/admin` console.** It would need a platform-scoped settings
table, cross-instance cache invalidation, an audit trail and an admin write endpoint (which ADR-021
requires be enumerated), and it would downgrade the gate from "the route does not exist" to a
per-endpoint runtime check that every future billing endpoint must remember to run. It is also a
footgun once real subscriptions exist: switching billing off does not stop the provider charging.
The console may display the state **read-only**.

**Rejected — promotional codes attached to an email address.** A code delivered to an inbox proves
nothing that the passwordless sign-in to that same inbox does not already prove, so it would buy a
code lifecycle (issuance, redemption, single-use, expiry, revocation) for no additional security.

**Known and accepted:** `TenantService.ReHomeAsync` gives any departing member a fresh tenant-of-one
they own, so someone who arrived by invitation can end up owning a household. That household's owner
is not green-listed, so it admits nobody new — the leak is cosmetic, not a hole. *Same family, added by v4
T64 (AUTH-8, decision #3, 2026-10-01):* an invited-but-unlisted person **founds** their household of one at
sign-in, before accepting anything — `CreateUserWithTenantAsync` is the single choke point and it always
provisions tenant + owner membership. If they never accept, the invitation is revoked, or accepting would
abandon data they have since created (`WouldAbandonData`), that household stays, owned by someone the list
never named. Deferring the household until the invitation is accepted was considered and rejected: it would
give every path that mints a user a second shape (a user with no tenant), which the tenant filter, the
JWT's `tenant_id` claim and the RLS backstop all assume away. The household admits nobody new, same as
above; it is a cosmetic orphan, not an access path.

**Ports downstream** (`vuelto`, `jigger-jot`) once the platform suite is green, like LOCALCI-3.

> *Numbering note: 025 and 026 are reserved upstream for platform-only work (local CI, the flavors program) that this app does not carry, so the sequence jumps.*

**JJ-036 — The UI adopts design direction 1A, "Back bar": a restyle bound by the handoff's own
"do not change" list, with four recorded adjustments. (2026-09-13)**
A second design document arrived (`docs/design/2026-09-backbar-handoff.pdf`, 19 pages): every screen
restyled — dark as the primary theme with a light counterpart, one display serif, hairline surfaces,
Marga at the size she was drawn for — with the structure, routes, copy and test ids "unchanged from
develop". The first proposal (2026-09-10) changed what the screens do and has shipped in full; this
one changes what they look like. It is adopted as epic `BACKBAR` (`docs/stories/backbar.md`), in the
document's own order.

*What is binding.* Page 18's list: routes, page parameters, API calls, the makeability and unlock
logic, Marga's selection rules, optimistic ticks, the pre-paint theme apply, unit conversion,
localisation keys and every existing `data-testid`. A change that would show up in a behaviour test
is out of scope — with the one test-mechanics exception below, stated rather than discovered.

*The adjustments, from the review.*
1. **The catalog chips are three radios**, not two `btn-check` checkboxes with exclusivity
   re-implemented: `cocktail-makeable`, `cocktail-almost` and a new `cocktail-all`. "They already
   behave exclusively in code" argues for radio semantics. The hidden input cannot be `check()`ed by
   Playwright (the INV-3 lesson), so the one journey that drives them clicks labels through a shared
   `E2ETestBase` helper. One id added, none edited; one new string pair, "Everything", EN and ES.
2. **Only the selected chip carries a count** — the response's own `total`. Three simultaneous counts
   would cost two extra requests on every catalog load for numbers the pager already shows, and the
   document's own rule is "same fetch-once behaviour".
3. **No popover on the Write row.** The app loads no Bootstrap JS, a popover is a new interaction, and
   it would put `new-line-role` and `new-line-required` behind an open step. Both stay visible.
4. **Two strings the document assumes do not exist** ("Make now / One away" as mobile chip labels,
   and "Marga will tell you if you can pour it"). The chips use the full labels at every width; the
   footnote is dropped.

*Why the PDF is committed.* The slices cite its pages, and the Claude Design project it came from is
not versioned. `.gitattributes` already treats PDFs as binary; the `changes` job counts it as docs.

*Consequences.* Seven slices, each one branch off `develop` and one PR (the batching rule lets the
foundation ride as three commits in one). The ids in the code are the contract — two of the
document's "keep" lists name ids that do not exist (`write-*`, most of `household-*`), and every list
is re-derived from the razor when its slice starts. The definition of done on page 18 is the epic's.

*Decided 2026-09-13.*

**JJ-037 — The chrome leaves copper, in both themes; SHELL-1's hierarchy stands in a new colour.
(2026-09-13)**
The header and the bottom tab bar move onto the surface colour with a 1px hairline, and the current
tab gets a 2px copper indicator. Copper is reserved for actions (buttons, the makeable count, Marga's
advice) — "the copper header is what makes today's app read as a Bootstrap admin" (handoff page 04),
and the sibling apps' green bars read the same way.

*What this amends and what it keeps.* SHELL-1 settled that the app's three destinations are raised
above the platform's account cluster at every width, that the current tab is weight plus a drawn
indicator and never colour alone, and that `aria-current` carries the fact to a screen reader. All of
that stands; only the ground and the ink change (ink against muted, where it was white against
white-alpha). The account cluster stays untouched in content, as SHELL-1 decided.

*The test it changes.* `ShellJourneyTests` asserts three literal colours — written against a dark-theme
bug where `[data-bs-theme="dark"] a` repainted every anchor-shaped button copper. Those three lines
are rewritten to the invariant they guard: a destination is never fainter than the account buttons,
and no anchor-shaped button in the bar takes the link colour. The structural assertions (one element
per id, `aria-current`, no sideways scroll, the payoff footer clearing the tab bar) do not change.

*Consequences.* `navbar-dark` goes and the bar follows `data-bs-theme`. The mark needs a dark-ground
variant: `icon_dark.svg` is generated from the same source by `docs/brand/build_assets.py`, swapped by
the `content: url()` pattern the lockups already use, and listed in `REBRANDING.md` §3 — a rebrand
that skips it ships a bone mark on a white bar.

*Decided 2026-09-13.*

**JJ-038 — One self-hosted display serif, one weight, display only. (2026-09-13; amends JJ-029)**
Instrument Serif (SIL Open Font License), regular weight only, ships in the RCL's `wwwroot/fonts/`
with its licence file beside it, declared once in `app.css` and applied through one class,
`.font-display`. It is for the count, drink names, card headlines and Marga's line — never a label,
never a control, never below 20px, never bold. Body, controls and meta stay on the system sans, as
today.

*Why self-hosted.* Both hosts load `app.css` from the RCL, so the face reaches the MAUI shells
offline and R68 parity is automatic. No request leaves the app for a third party on first paint, which
a Google Fonts link would add on the one route (login) that is seen before consent to anything.
`font-display: swap` with Georgia/serif as the fallback, so text is never invisible while it loads.

*The one-weight rule is a gate, not a note.* The face has a single weight and a browser asked for
bold synthesises one; `fw-bold` sits on 31 elements today. A repo gate in `Api.Tests` refuses
`font-display` and `fw-bold` on the same element. Only the elements the serif reaches lose `fw-bold`;
everything in the sans keeps its weight.

*What JJ-029 keeps.* The wordmark (Barlow Semi Condensed, in the rendered PNG lockups) and the
palette are unchanged; the token values on handoff page 01 are the palette restated per theme, with
`--surface` added for panels and inputs.

*Decided 2026-09-13.*

**JJ-039 — The UI is the app's own, every screen of it; the backend may be extended but its
foundation is not changed. (2026-09-13)**
Handoff pages 16–17 restructure Settings (five cards to two columns, segmented theme and unit
controls) and Household (six cards to four groups, a `···` row menu), and page 17 restyles the
notification bell's dropdown. These screens were inherited from `perezosoft-platform`, and the first
draft of this decision treated them as the platform's — primitives only here, markup upstream first.
The maintainer corrected that the same day, and the correction is the decision:

*Decision.*
1. **Every app from the platform owns its UI — all pages, no exceptions.** The RCL is downstream
   property, the inherited screens included. Settings and Household are restyled **in full** here, to
   pages 16–17; Billing, Admin, Join, AuthError, the not-found view, the auth callback, the
   impersonation banner and the error bar — none of which the handoff drew — are restyled to the
   same language by analogy, with nothing sent upstream and nothing waited for. The sibling apps remain a useful comparison
   (the last header fix was found by holding the bar next to `vuelto`'s), never a constraint.
2. **The backend may be extended, never re-founded.** New endpoints, queries, features and
   contributors are the app's to add (the whole of `CKTL`, `INV`, `MAKE`, `ALMOST` is exactly that);
   the platform's foundation — auth, tenancy and its walls, the outbox, billing, the RLS backstop,
   R1–R76 — is a red light. JJ-026 / JJ-028 ("platform wins") is about that foundation and the
   platform's *mechanics*, not about what a screen looks like.
3. **This epic touches no backend at all.** A restyle with an API change in it is two slices.

*What the restyle keeps on those pages.* The components' parameters, every existing id, and every
behaviour: each Settings row still saves on change through the same `PUT`, the segmented theme
control writes the same preference the header switcher does, the danger zone still opens the existing
confirm dialog, row actions behind `···` still call what the buttons called. The journeys that drive
them (roster, membership lifecycle, MFA, notifications, billing, GDPR export) stay green as they are.

*Consequences.* The ladder gains two slices: **BACKBAR-7 Settings + Household** (pages 16–17) and
**BACKBAR-8 Billing + Admin and every small screen** (not drawn; derived from pages 02 and 16–17),
with the sweep becoming **BACKBAR-9**. CLAUDE.md's golden rule 8 gains the sentence that keeps this from being
re-argued: the UI is the app's; the foundation is the platform's.

*Decided 2026-09-13; corrected the same day before publication.*

**JJ-040 — Marga speaks on every app screen that has something true to say; the platform's screens
stay without her. (2026-09-14; amends MARGA-5's placement rule)**
MARGA-5 settled where she speaks with a rule — "where a number needs interpreting, and quiet where
the screen already says it plainly, one page-level Marga per screen" — and used it to keep her off a
recipe more than one bottle away, the plain catalog, and the write form. The maintainer asked for
more of her on the app's own screens and chose the four places, keeping the platform screens
(Settings, Household, Billing, Admin) as they are.

*Decision.*
1. **She speaks on:** a recipe two or more bottles short (she names the missing required lines — all
   of them up to three, the count and the first two past that); the catalog under Everything and under
   any filter or search (the total read against how many of those the shelf can pour); a shelf search
   that matches no bottle on the list (a 32px inline line offering to add it); and the write form (the
   bottles on the shelf to write with).
2. **The contract does not move.** Every sentence is one whole resource string with real data in its
   placeholders — lines the API marked missing, a count the API returned, the typed term, the ticked
   bottles. No model, no generated text, nothing she decides. Lists of two or three names use their
   own whole patterns ("{0} and {1}") rather than a joined fragment.
3. **One page-level Marga per screen still holds.** Under One ingredient away the count stays plain,
   because her panel already speaks there; the shelf's no-match line is inline, beside the page-level
   line at the top.
4. **The browse count costs one request, asked the same question.** The pourable figure is the
   makeable filter's own total for the same search and filters (`pageSize=1`), behind the same request
   ticket as the list; if it fails the plain count stands.

*Consequences.* MARGA-6 in `docs/stories/marga.md`; QA-CHROME-11 and -12 amended and QA-CHROME-26
added. MARGA-5's "where she deliberately does not go" now reads as history: the plain browse, the
far-away recipe and the authoring form are hers; every platform screen still is not.

*Decided 2026-09-14.*

**JJ-041 — Every volume is stored in ounces; a reader sees ounces or millilitres, never parts.
(2026-09-15; amends JJ-007, JJ-008 and PREFS-2)**
JJ-007 stored amounts as authored, so a 1930 recipe reached the screen as "2/3 part" and "1 wineglass"
and the IBA's specifications as millilitres. The maintainer found parts hard to read, and 567 of the
Savoy's 868 recipes are proportional, so the problem grows with the catalog. Converting at display
alone could not settle it: the exact 29.5735 ml ounce turned 2 oz into 59 ml, and a proportional
recipe has no volume until someone decides what one drink is.

*Decision (the maintainer's, taken question by question).*
1. **Stored in ounces, on the quarter marks of a jigger.** Millilitres, centilitres and the other
   volumes are converted and rounded — so the IBA's 20 ml and 25 ml both become 3/4 oz, because no
   modern bar book writes 5/6 oz. Never rounded to nothing: the smallest amount is 1/4 oz.
2. **An ounce is 30 ml.** The bar's ounce, not the exact one: 1 1/2 oz reads as 45 ml, 3/4 oz as
   22.5 ml, which is what a metric bar book says.
3. **Parts share a three-ounce drink** (2/3 absinthe, 1/6 gin, 1/6 anisette → 2, 1/2, 1/2 oz), and a
   recipe whose fractions do not add up to one keeps its ratio.
4. **The period glasses are volumes too:** a glass and a wineglass are 2 oz, a liqueur glass 1 oz —
   the readings the old bar books give. A batch recipe keeps its proportions; its total may differ
   from what the book meant.
5. **Teaspoons and tablespoons stay** — they are universal — and so do dashes, barspoons and the other
   neutral units.
6. **Two choices, imperial the default.** "As written" is gone: with everything stored in ounces it
   would only ever mean imperial. A null preference reads as imperial and the profile says so; clearing
   the preference is a 400.
7. **The authoring form offers the writer's own volume unit** (oz or ml, never a part or a glass) and
   stores ounces.
8. **One implementation.** `BarMeasure` in Core holds the table and the rounding; the seeder and the
   authoring handler write through it and `AmountDisplay` reads through it. The one-off migration that
   converts existing rows is a frozen SQL copy, held to `BarMeasure` by a test.

*Consequences.* The seed file (`cocktails.json`) keeps the books' amounts; the table keeps ounces, so
the extraction is still the record of what each book wrote. The migration has no way down. The
`Unit` rows keep their exact millilitre factors, which no recipe reads any more. PREFS-3 in
`docs/stories/prefs.md`; QA-CAT-07 rewritten and QA-CAT-10 added; CLAUDE.md's golden rule 4 restated.

*Decided 2026-09-15.*

**JJ-043 — A curated catalog: one recipe per drink, the IBA list first, and every recipe has a glass.
(2026-10-09; amends JJ-032, retires JJ-034's "glass is optional")**
Staging carried all 969 extracted recipes — SEED-3 shipped them, MAKE-1 cut the shipped file to a
31-recipe starter set, and the add-only seeder never removed the rest — and the maintainer read them
as duplicates: *Dry Martini* beside *Dry Martini Cocktail*, *Daiquiri* beside *Daiquiri Cocktail*.
Measured, 29 drinks appear in both books under near-identical names, and four more under different
ones (*Mint Julep* / *Southern Mint Julep*, *Ramos Fizz* / *New Orleans Gin Fizz*). JiggerJot is for
people who care about the spec — classic recipes from credited sources, not social-media trend
recipes — and to them two specs of one drink is noise.

*Options.* **Group the versions** under one drink ("Daiquiri, 2 versions"): keeps every recipe, but
needs a drink/version model and still ships two specs. **Keep the names and research each recipe
online**: for the famous drinks the IBA spec already *is* the accepted recipe; for the ~800
Savoy-only drinks there is nothing beyond the Savoy to find; and a spec assembled from websites has no
public-domain source to credit, which JJ-032 exists to avoid. **IBA first, the Savoy for what the IBA
lacks (chosen, the maintainer's).**

*Decision.*
1. **One recipe per drink.** Where both books have a drink, the IBA spec ships and the Savoy one does
   not. "The same drink" is a judgement made drink by drink in `seed/overlap.json`, never a name match
   — *Corpse Reviver (No. 1)* is not the IBA's #2, and *Manhattan (Dry)* is a Perfect Manhattan — and
   `seed/build_cocktails.py` fails on any name-match candidate the file does not settle.
2. **A Savoy recipe ships only as a usable spec:** a glass, a method, two or more required lines, an
   amount on every one. The prose recipes recovered from tag lists and the book's how-to entries
   ("Cobblers", "Basic Sour") stay in the extraction; `seed/savoy_excluded.txt` lists each with its
   reason. The IBA list ships whole.
3. **A glass is required, period — on every recipe, the household's own included.** The glass is part
   of the cocktail. It comes from the source's glass, from the source's own instructions where they
   name one, or — for five IBA drinks whose source names none (Mojito, Piña Colada, Kir,
   Canchanchara, and Champagne Cocktail's "large Champagne glass") — from a curator's call, labelled
   as one in the build script. What JJ-034 forbade still stands: nothing is filled in silently, and a
   Savoy recipe whose text names no usable glass does not ship. Method stays optional.
4. **The Savoy's "Cocktail" suffix goes** from the shipped name (*Dry Martini Cocktail* → *Dry
   Martini*), unless what is left would be an ingredient or a fragment (*Coffee Cocktail*, *Devil's
   Cocktail*). Qualifiers stay (*Alexander (No. 1)*). No two shipped recipes share a name; the build
   fails if they do.
5. **The starter set retires.** One shipped file; a test that needs a shape the curated set no
   longer has builds its own rows.
6. **Existing databases are cleaned by a one-off migration** (`ReseedCuratedCatalog`): it deletes the
   shared recipes, and the seeder writes the curated set on the next startup with the same ids. Only
   shared rows; a household's recipes and forks are untouched. Chosen over a hand-run cleanup because
   it reaches every database — staging, a developer's, the next one — the same way. The seeder stays
   add-only.

*Consequences.* 644 recipes (102 IBA + 542 Savoy); 33 Savoy recipes superseded, 292 below the bar.
The extraction is untouched and remains the record of what each book wrote. `Cocktail.glass_type_id`
becomes NOT NULL and the authoring endpoints refuse a recipe without one; Marga says so on the form.
A new curated glass, *Goblet*, for the IBA's goblets and footed copos. Milestone *Curated catalog*,
issues #176–#182.

*Decided 2026-10-09.*

**ADR-028 — The self-hosted Forgejo is the primary forge and runs the full CI/CD; GitHub stays a mirror whose own CI runs only when pushed to on purpose (LOCALCI-4). (2026-09-16) — SUPERSEDED by ADR-030 (2026-10-02)**
The maintainer moved day-to-day git to a private Forgejo on the Windows desk (WSL2 + Docker, reachable over
Tailscale) so that routine pushes cost nothing and leave nothing on a third-party server. GitHub keeps the
repository — Render builds from it, and pushing there on purpose still runs the GitHub pipeline unchanged.
The Forgejo pipeline must do everything the GitHub one does, deploys included. This supersedes
LOCALCI-1's approach (route GitHub Actions to self-hosted runners) for this repo: the runners belong to
Forgejo, and GitHub keeps its hosted ones.

**Decision:**
1. **Two workflow files, held together by a test.** `.forgejo/workflows/ci.yml` is a copy of
   `.github/workflows/ci.yml` (each forge reads only its own directory once `.forgejo/workflows` exists).
   `ForgejoCiParityTests` (**R80**) fails when the job list, a `runs-on` line, a pinned version or the change
   classifier differs; the LOCALCI-3 gate tests run against both files. Every deliberate difference is
   marked `LOCALCI-4:` in the copy.
2. **The runners carry the hosted labels.** `ubuntu-latest` is a container image
   (`forgejo-ci/ubuntu:24.04`: catthehacker's act image + Docker CLI, PowerShell, `gh`, JDK 17, the Android
   SDK with the smoke's system image, the pinned .NET SDK and Android workload) on the WSL runner, which runs
   jobs inside Docker-in-Docker with host networking and `/dev/kvm` passed through. Every WSL job shares that
   one network namespace, so the two jobs that bind fixed ports (`e2e`, `native-smoke-android`) use
   `ubuntu-host-ports` — the same image on a second WSL runner that takes one job at a time, which also
   serializes them across runs and repos. That is the only `runs-on` difference the parity test allows, and
   it fails for any Linux job with service containers left on the shared runner. `windows-latest` is the
   Windows desk in host mode (a logon task, so WebView2 has a session). `macos-26` will be the MacBook.
   Identical labels are what make `runs-on` comparable line for line.
3. **Apple jobs require `vars.CI_MACOS_RUNNER`.** A job whose label no runner carries queues forever on
   Forgejo (GitHub would expire it in 24 h), so until the Mac is registered the Apple legs are skipped.
4. **Deploys publish to `deploy/*` branches on GitHub.** Render only builds from GitHub, and GitHub's
   `ci.yml` only triggers on `main`, `develop` and pull requests. The deploy job force-pushes the tested
   commit to `deploy/staging` (or `deploy/prod`) with a token scoped to that repository, fires the Render
   hook, and runs the same `deploy-smoke.sh`. Each Render service tracks its `deploy/*` branch with
   auto-deploy off. No GitHub Actions run for a deploy.
5. **Prod is a manual dispatch.** Forgejo has no Environments and no required reviewers. `deploy-prod` runs
   only on `workflow_dispatch` from `main`; a push to `main` runs every gate and deploys nothing. The
   dispatched run re-runs the gates first (`changes` fails open without a diff base).
6. **GitHub's deploy hooks are removed from GitHub.** With both pipelines holding the hook, a deliberate push
   to GitHub's `develop` would fire a second deploy of whatever `deploy/staging` holds. Without the secret,
   GitHub's deploy jobs already skip with a notice (DEPLOY-3's opt-in design) — a settings change, not a
   code change.

**Consequences.** The desk is now a deploy path: deploys need the laptop on (the accepted trade-off of
SETUP.md; GitHub stays the emergency route — restore the hook secret there and push). Runners are not
clean machines: the smoke's Postgres is recreated per run, port-binding Linux jobs queue behind each other,
and the image's SDK/workload pins join the CLAUDE.md
bump-together playbook. The Render deploy-hook and the GitHub mirror token live in Forgejo's secrets only.

**Rejected — one shared, forge-aware `ci.yml`.** It would remove the duplication but thread
`github.server_url` conditions through the file GitHub runs today, for every future reader of both.
**Rejected — pushing `develop` to GitHub to deploy.** It runs the whole GitHub pipeline, and its deploy job,
on every deploy. **Rejected — image-backed Render services.** Faster builds, but it means new services, a
registry account, and losing the "redeploy from GitHub" escape hatch; revisit if Render's build time bites.

**Addendum (2026-09-16, same day, after the first green runs) — deploys and smokes are on demand; the
deploy pushes the real branch to GitHub; points 4–6 above are superseded.** Two things became clear once
the pipeline ran on one machine: (a) a develop push that runs the emulator and the WebView2 smoke every
time costs 20 minutes of a laptop the maintainer is also working on, for legs that rarely change; and
(b) keeping GitHub deploying too was wanted after all ("I don't mind if GitHub deploys again when I push"),
which makes the `deploy/*` branches pointless — Render can follow only one branch, and it stays on
`develop`. So:
- **Pushes and PRs run the gates, `e2e` and the native *builds* only** (≈10 min). The native **smokes**
  run from `workflow_dispatch` (`smokes` = windows | android | apple | all) and on the Monday schedule.
  The builds stay per push on purpose: they are free in wall clock and catch compile rot within one merge.
- **Deploys run only from `workflow_dispatch`** (`deploy` = staging on `develop` | prod on `main`), never on
  a push, and only after every gate and every smoke *selected in that run* is green (a smoke not selected
  is skipped, and a skip is not a failure). Staging no longer tracks `develop` automatically: the
  maintainer decides when.
- **The deploy pushes the commit to the same branch on GitHub** — a plain fast-forward, never forced; if
  GitHub is ahead the deploy stops and says so — then fires the hook and runs the shared smoke. GitHub's
  pipeline runs on that push and re-deploys the same commit; accepted as harmless. GitHub keeps its hook,
  Render keeps `develop`, nothing to reconfigure. "The deploy from Forgejo" and "the push to GitHub" are
  the same button.
- The earlier rejection of "pushing `develop` to GitHub to deploy" is withdrawn: its cost (GitHub's run
  and second deploy) was re-weighed against the operational simplicity and accepted.

**Addendum (2026-09-22) — a commit that is already green can be deployed without re-running its gates.**
Three repos now share one desk. A deploy dispatch re-ran every gate on a commit whose merge run had gone
green minutes earlier: ~10 minutes of a laptop the maintainer is also working on, for an answer already
known. `.forgejo/workflows/deploy.yml` (dispatch only, Forgejo only) deploys such a commit directly — but it
does not take anyone's word for it: it asks this instance's own Actions API which jobs ran for THAT commit
and requires `changes`, `build-test`, `secret-scan`, `qa-artifacts`, `license-scan`, `docker-build`, `e2e`
and both `native-build` legs to have a success. A missing job, a failure, a docs-only run that skipped the
gates, or an API it cannot reach all stop the deploy and point back to `ci.yml`'s `deploy` input, which
runs the gates itself. The job token can read that API and write nothing (probed 2026-09-22).
Publishing is unchanged and shared: `push-to-github.sh` (fast-forward, never forced), the Render hook, then
`deploy-smoke.sh`. Native smokes are not required, exactly as in `ci.yml`'s deploy jobs — the Monday
schedule and the `smokes` input remain how those are proven. `ForgejoCiParityTests` pins the guard, the
dispatch-only trigger and the shared script.
**Rejected — a `skip_gates` input on `ci.yml`.** Same saving, but it would thread "was this skipped?"
through every gate's `if:` and every deploy `needs:`, and one careless edit would turn the deploy's
"everything green" rule into "nothing ran". A separate file cannot weaken the existing one.

**Ported to jigger-jot (2026-09-22).** Forgejo repo `argamboad/jigger-jot`, GitHub mirror of the same name;
Render `jiggerjot-staging` stays on GitHub's `develop` with auto-deploy turned OFF for the move (it used to
deploy every GitHub commit, red ones included), so the Forgejo deploy and a deliberate `git push github develop`
are its only triggers. Runbook: `DEPLOYMENT.md` §10. The Forgejo `ci.yml` is the platform's GitHub→Forgejo
changes three-way-merged onto this repo's own `ci.yml`; the three conflicts were this repo's `smoke` dispatch
input, which the Forgejo copy replaces with its `smokes`/`deploy` inputs. Carried over from vuelto's port: the
Android smoke waits for the install's broadcasts before launching the app (a late PACKAGE_ADDED relaunches
the running activity on the slower Forgejo emulator) and opens the hamburger before the header's Household
link; the device-log upload is `upload-artifact@v3` on Forgejo.

**ADR-030 — GitHub is the forge again; Forgejo is retired (supersedes ADR-028). (2026-10-02)**
Forgejo ran on the maintainer's work laptop, and a reboot or a Docker Desktop restart could stop CI
(the 2026-10-02 morning: the Linux runner's Docker-in-Docker had restarted 187 times). An always-on
machine at home is not in sight, so the forge goes back to GitHub:

1. **`origin` is GitHub** (private) for code, PRs, merges and issues. The Forgejo issues were migrated
   with their comments, labels and milestones; each GitHub issue's footer names its Forgejo number,
   which old commit messages cite.
2. **Forgejo is archived read-only, its runners are stopped.** It stays up as the rollback until it is
   switched off; its encrypted backup is the frozen copy.
3. **The repo is private, so every Actions minute is billed** (Free plan, 2,000 minutes a month, a $0
   budget that stops runs at the limit). On 2026-10-01 one deploy round cost ~600 minutes, so the `CI`
   workflow is **disabled until a CI rebuild**, whose agreed direction is: PRs and merges run backend +
   web UI only (build-test, the scans, docker-build, e2e); every **device leg** (Windows, Linux-native,
   macOS, iOS, Android builds and smokes) and every Render deploy runs only from a manual *Run workflow*.
   The rebuild gets its own ADR (it amends NATIVE-1 and LOCALCI-3) and deletes `.forgejo/`,
   `ForgejoCiParityTests` and R80; until then they stay, unused.
4. **No branch protection:** a private repo on GitHub Free has neither branch protection nor rulesets.
   Decided in ADR-031: none, the repos stay private.

**Consequences.** DEPLOYMENT §10 and the LOCALCI-4 story are history. The Postman workspace sync runs
from GitHub on every `develop` change. The platform and its downstream apps share this ADR.

**ADR-031 — CI on billed minutes: pull requests run the web gates only; the device legs and every deploy run on request. (2026-10-02)**
The repos are private (ADR-030), so every Actions minute is billed against 2,000 a month, and one develop push
used to cost 57–147 of them (macOS bills 10×, Windows 2×). The maintainer manages the minutes by hand, and the CI
says so:

1. **Triggers:** `pull_request` into `develop`/`main`, and `workflow_dispatch`. No push trigger — a merge runs
   nothing, its pull request already did — and no schedule.
2. **A pull request runs the web gates only:** `changes`, `secret-scan` and `qa-artifacts` always (a docs-only
   change can leak a credential or desync the QA PDFs), and `build-test`, `license-scan`, `docker-build`, `e2e`
   when code changed. e2e runs as ONE shard: on hosted runners every shard boots the stack again, and that
   setup cost more than the wall-clock it saved.
3. **The device legs run only from *Run workflow* → `devices`** (android / windows / apple / all): the MAUI
   builds, the Release APK and the three native smokes. This amends NATIVE-1 (ADR-018: "a change that breaks
   the native build fails the PR") and LOCALCI-3's per-push Apple build: a change that can affect the MAUI app
   is no longer proven by its PR. The maintainer runs them periodically and before a release. There is no
   Linux desktop flavor (MAUI has no Linux target; the web app and the Flavors fronts cover it).
4. **Deploys run only from *Run workflow* → `deploy`:** staging from `develop`, prod from `main` (a wrong
   branch fails loudly). That run re-runs every web gate and deploys only if all pass — deploys are rare, and
   re-testing beats trusting a merge commit. Choosing `prod` is the approval: GitHub Free has no Environment
   reviewers on a private repo. The device legs are not in the deploy's needs (a native result must not block
   a web deploy, ADR-018).
5. **Cost hygiene:** `timeout-minutes` on every job, a NuGet cache keyed on the lockfiles, 3-day artifact
   retention, and a new push to a pull request cancels its previous run.
6. **No branch protection** (ADR-030's open point, decided): the repos stay private on GitHub Free.
7. **`.forgejo/` is gone**, with `ForgejoCiParityTests`, the Forgejo branch-protection script and the
   Forgejo-only CI-logic targets; rules **R80 and R138 retire**, **R98, R137 and R139** now speak of the one
   workflow, and `CiWorkflowTests` holds this shape.

**Consequences.** A pull request costs roughly 25–35 minutes, a docs-only one a few. The `changes` job's
classifier still reports `native`/`maui` for the record. The platform and its downstream apps share this ADR.

**ADR-032 — The platform's tenant is a "household", by design; an app whose tenant is something else renames at rebrand time. (2026-10-06)**
The platform names its tenant with a product noun, not a neutral one: `HouseholdController` at `api/household`,
`HouseholdInvitationsController`, the `Household.razor` page at `/household`, the `Household_*` resource keys
(about 120 strings in EN and ES), the `nav-household` test id, and the word in emails and QA cases. The boundary
audit of 2026-10-05 (Arch A11, #370) asked whether to rename it to a neutral word, make the display noun a per-app
resource, or record it as intended. **Decided: record it as intended.** Both current apps (vuelto, jigger-jot) are
household products, and the one word in code, routes, strings and tests is what lets a journey, a QA case and a
support conversation say the same thing. A neutral word (`tenant`, `workspace`) would be right for nobody and
would still be renamed by every app with a different noun.
*Consequences:* the word is a **rebrand-time** concern, beside the brand itself. An app whose tenant is a team,
a company or a classroom renames `Household` → its noun in the same pass as `JiggerJot` → its brand, following
the entry in `docs/REBRANDING.md` §1 (routes, pages, controllers, resource keys, test ids and QA cases move
together; `TestIdContractTests` and `ResourceParityTests` catch a half-done rename). The platform's own tests and
docs keep saying household. Ports of platform changes into an app that renamed are three-way merges with a second
token pair, the same mechanism the brand rename already uses. ADR-C1/C2 (tenant ≠ user; tenant-scoped data) are
untouched: this is vocabulary, not shape.
*Amendment (ADR-034, 2026-10-13):* once the API ships as packages, its routes and controllers keep "household" in
every app; an app with another tenant noun renames the seeded UI (strings, pages, test ids, QA cases) only.

**ADR-033 — Gates guard behaviour and truth, not process. (2026-10-12)**
By the end of the v4 audit, about a third of the platform's gate layer checked the repo's own process instead of
the product: a manifest that named a test for every `[machine]` rule, a gate that held the manifest to the rules
file, a gate that the rule ids cited in tests were final, the PR template's checkboxes, the CLAUDE.md doc map and
its course-reconcile bullet, the add-a-slice checklist against the gates, the wording of deploy sentences, and a
two-way test-id contract with an analyzer for parameterised components. Each was added after a real slip, and
each slip was real; but the gates cost 20–35 billed minutes a PR in upkeep, failed on edits that changed nothing a
user or a reader relies on, and every one of them had to be ported to each app. Before the platform is packaged
(the *Platform packaging* project, milestone M1 "Scissors", #389–#393), the layer is cut to what guards a
behaviour or a document's truth. **Decided:**

1. **Retired** (Pkg 1.1, #389): `RuleIds_CitedInTests_AreFinalRules`, `EveryMachineRule_NamesAStandingCheck` and the
   manifest it read (`tests/Api.Tests/RulesEnforcement.cs` + the app's `RulesEnforcement.App.cs`),
   `PrTemplate_CarriesTheRuleCheckboxes`, `ClaudeMdDocMap_ListsEveryDoc`, `ClaudeMd_CarriesTheCourseReconcileRule`,
   `AddASliceChecklist_NamesEveryArtifactAGateForces`, both `DeployTriggerWording_*`, and
   `TheTwoGatesThatCatchDocsMistakes_AreNeverCodeGated`. The tests the manifest pointed at stay, as ordinary named
   tests; the PR template is a three-line checklist.
2. **Kept, in one place** (Pkg 1.2, #390): every fact that compares a document with the code lives in
   `DocsAreTrueTests` — entities in DATA_MODEL, config keys and settings sections in `.env.example`, compiled-in
   limits, ARCHITECTURE and FLOWS naming real classes and error codes, the Postman descriptions naming each action's
   error codes and gates, the QA case count, and the course gate's wiring. Postman parity, the QA PDFs and sign-off
   sheet (`check_qa_artifacts.py`) and every behaviour, tenancy, RLS, schema, route, slice, ownership, pin, release
   and CI-shape gate are unchanged.
3. **The test-id contract runs one way** (Pkg 1.3, #391): an id a test or a QA case references must exist; an id
   nothing references is not a failure, and the parameterised-component analyzer is gone (an id derived from a
   caller's literal `TestId="x"` counts as existing).
4. **The course check runs when it can find something** (Pkg 1.4, #392): when the course, a file a lesson quotes,
   or the set of tracked files changed (the classifier's `course` output), failing open on an unknown diff.
5. **FOUNDATION_RULES v3.1:** R116 and R117 retire (numbers never reused); R118 and R158 become `[review]`; R85,
   R144, R149, R114 and R115 name their new mechanism; every number is kept for citation, and no gate parses the
   file. The course-reconcile rule stays, as text, in CLAUDE.md's "Read before you act" and in the rules' coda.

**The habit from here on:** a slip gets a test for the thing that slipped (the document that drifted, the code
that misbehaved), never a rule about the rule or a gate on the process that let it through.
*Consequences:* a doc map or checklist can drift without a red build, and is fixed when found; the course is
reconciled once per milestone during packaging, not per commit. The apps take the same cut in their scissors port
(jigger-jot and vuelto P.1), deleting their `RulesEnforcement.App.cs`.

**ADR-034 — The platform ships as packages; a new app starts from a starter (supersedes ADR-019's downstream convention; adopts Backlog §10). (2026-10-13)**
Apps have been clones of the platform, rebranded once and kept in step by porting every fix (ADR-019, its A2
amendment: the ownership map, the stamp, the port tool). The ports work, and they measure the cost: before the
2026-10-08 ports vuelto and jigger-jot differed from the platform in 142 and 144 platform files, after them in 4 and 2,
none of them source — the copies were the same code carried twice. Every fix is a port per app, the cost grows with
apps × fixes, and a missed port drifts silently. Delphos and further apps are coming. **Decided:**

1. **Packages split by runtime target, not by domain.** `Perezosoft.Platform.Core`, `.Infrastructure` and `.Api`
   (the server); `.Client` (the non-visual half of today's Shared.Ui: `AuthService`, the bearer handlers, the session
   and OAuth seams, the persistences, the boot scripts), `.Web` and `.Maui` (the hosts' platform halves:
   `AddPlatformWeb`, `UsePlatformMaui` / `UsePlatformShell`); `.Testing` (the harness, the architecture guards and the
   inheritable gate suites); `.Build` (the solution's build props as `buildTransitive`). Beside them: a dotnet tool
   (`dotnet platform <command>`: the port, the QA generators with their Python inside it), a reusable CI workflow
   (`workflow_call`, the same billed minutes as ADR-031's shape) and `argamboad/.github` for the community files.
   The domains — auth, tenancy, billing, jobs, files, GDPR, MFA, notifications, admin, public API, webhooks,
   observability — are **namespaces inside those packages and options on `AddPlatform(o => …)`**, never packages of
   their own: no app wants half the platform, and a package per domain multiplies versions for no consumer.
   Platform types keep their `JiggerJot.*` namespaces in every app; an app declares none (an inherited guard holds
   it). vuelto and jigger-jot renamed `JiggerJot.*` to their brand, so their conversion renames platform namespaces
   back in app code, mechanically, by the stamp's brand map.
2. **Three kinds of code, and the rule that decides them: "if a fix here must reach existing apps, it is
   packaged."** **Referenced** — everything that rule catches, in the packages above. **Seed** — the UI (pages,
   components, layout, CSS, strings, brand assets, the UI and browser tests that exercise them) is cloned once from
   the starter and is then the app's, never synced either way: the platform seeds a UI, it never owns one. The
   seeded UI stays as the runnable catalogue of the platform's capabilities, with a capabilities page on Home.
   **Register** — the small set of files every app must hold and keep in step (the hosts' `Program.cs`, `MauiProgram`,
   the two `index.html`, `AppComposition.cs`, `AppDbContext.App.cs`, project files, `global.json`, the CI caller,
   the gates file, the stamp, `render.yaml`, the doc skeletons): cloned, pinned to a platform version, synced by
   recipe.
3. **The register's sync mechanism is the port tool**, scoped to the register: as each package replaces code, its
   files leave the tool's scope and the ownership map's `platform` class shrinks to the register. The tool moves into
   `dotnet platform port`; the manifest gate stays its check.
4. **An app changes platform behaviour by extending it, in this order:** options → contribute an implementation →
   replace a service through DI → decorate it → inherit from it. When none fits, the missing seam goes upstream as a
   new platform seam (plans, permissions, the email brand, routes and policies are the first four). Never a local
   modification of platform code.
5. **Migrations: one context, the app owns every migration.** The app's `AppDbContext` derives from
   `PlatformDbContext`. A platform model change ships in a package release; the app bumps the package and runs
   `dotnet ef migrations add`, and EF emits only the platform's delta into the app's own linear history.
   `PlatformSchemaTests` compares the migrated database with the `platform-schema.json` the package embeds (a bump
   that was not migrated fails the app's PR), and the RLS migration gate covers any new policy. Platform model
   changes are **additive** (expand; a contraction ships in a later release, after every app expanded); data steps
   and RLS policies ship as callable steps (`ApplyPlatformStep(name)`, like `RlsDdl`) that the release note names.
   *Rejected:* a separate platform DbContext with its own history table — no cross-context navigations, and two
   histories per database.
6. **"Household" stays the internal API noun** (amends ADR-032's consequence): the packaged controllers and routes
   (`api/household`, `HouseholdController`, `HouseholdInvitationsController`) keep it in every app; an app whose tenant
   is something else renames the seed — strings, pages, test ids, QA cases — and never the packaged API.
7. **Release discipline.** Semantic versions from git tags through **MinVer**; prereleases on the way to 1.0
   (`1.0.0-alpha.1` backend, `-alpha.2` client, `-alpha.3` tooling, `-beta.1` for the pilot). Packages publish to
   **GitHub Packages** (the NuGet feed under `argamboad`), read with a token in every environment (laptop, codespace,
   cloud session, CI), and only from the manual release workflow (ADR-031: no push trigger, no schedule). Every
   release note says three things: **schema change** (yes/no; the steps to apply), **register change** (the files and
   the recipe) and **breaking** (yes/no; what to do). On the laptop, `PlatformSource=<path>` turns the package
   references into project references to a platform checkout, for work on platform and app together.

*Consequences:* a fix reaches every app as a package bump; an app holds its own code, its seeded UI and about twenty
register files, and runs no platform suites — only the inherited guards against its own code, a few composition
smokes, its own tests and the browser journeys it keeps. ADR-019's identity (the `JiggerJot.*` names, the frozen
DataProtection purposes) stands; its downstream convention and Backlog §10's "defer hard" are superseded. The work
is the *Platform packaging* project (platform milestones M2–M7): jigger-jot converts on the beta as the pilot (a
clone restructured to references, compared side by side with its current tree), vuelto on 1.0, and new apps start
from the starter (`dotnet new`). Until an app converts, it stays a clone kept in step by the port tool.

*Amendment (2026-10-13, Pkg 2.5, #430) — what the publisher keeps, and what the brand map never renames.* The first
port of M2 into jigger-jot (jigger-jot#192) showed two holes. (1) The release machinery — `release.yml`, the note
template, `tools/release/**` (the dry-run package), its ci-logic cases and `platform-packages.props` — only makes sense
in the publisher, yet the port forced it on the app, and removing it failed five gates. It is now a fifth ownership
class, **`producer`**: the port tool never writes it downstream (an old copy is reported for removal), the manifest
leaves it out, and the gates that read it run only in the platform (`PlatformOwnership.IsThePlatform`: the stamp's
commit is null); `ProducerFiles_ExistInThePlatform_AndNowhereElseAreDemanded` keeps them demanded here. (2) The brand map
renamed the platform's package ids (`Perezosoft.Platform.*` → `JiggerJot.Platform.*`) and the feed key — so, once an app
referenced a platform package, its source mapping would have missed it (sending it to nuget.org's `*`: a
dependency-confusion opening) and the source switch would never fire. `platform-ownership.json` now carries a
**`keep`** list (`Perezosoft.Platform.`, `key="perezosoft"`) that the port tool reads from the platform commit it ports
to and never rewrites; `PlatformOwnership.LocalPath` spells paths with the same list. The gate hashes the app's own text,
where a kept token is already the platform's, so the list changes no hash — the tool and the gate agree by construction,
and the port-tool test proves it on a real port. The ci-logic course cases now declare `requires` the course and skip
in an app; `no-course-here` runs everywhere.
