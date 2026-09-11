# CLAUDE.md

> Operating manual for Claude Code on this project. Auto-loaded each session — keep it tight.
> Constant rules are pre-filled; fill the app-specific placeholders during conceptualization.

## What this project is
<!-- One-paragraph summary: what the app is and its core loop. -->
**JiggerJot** is a cocktail app for home bartenders that answers one question better than anyone
else: *"What can I make right now, with what I actually have?"* A **household** (the tenant) keeps a
checklist of the ingredients on its shelf; the app shows every cocktail it can make **now**
(substitution-aware) and every one it is exactly **one ingredient short** of; members browse and
filter a shared, seeded catalog, fork any cocktail into their own editable copy, and author their
own. Full context in `docs/PROJECT_BRIEF.md`; tagline "Mix what you have." (JJ-029).

## Read before you act
- **Every session → `docs/PLAN.md` first.** The three gates (branch only from `develop`; no next
  slice until the user says "merged"; no commit/push/PR until the user says "C+P+PR"), the slice
  ritual, the editing rules, and the sequenced backlog. It exists because a session broke all
  three gates in one day.
- **Writing or modifying ANY code → `docs/audits/v3-2026-07/FOUNDATION_RULES_v2.md` (v2.0: R1–R35
  carried from v1.0 + R36–R76) is binding.** It encodes the post-audit invariants (tenancy incl. the
  RLS backstop parity, second-factor/event replay, SSRF, fail-closed normalization, atomic quotas +
  single-use credentials, per-user AND per-tenant erasure completeness, injected clocks, slice
  boundaries, host parity, doc/Postman sync) as machine-enforced arch tests + CI gates. Comply; if a
  task seems to require violating a rule, stop and surface it. The frozen quality bar lives in
  `CONTRIBUTING.md` (v1.0 remains at `docs/audits/v2-2026-07/FOUNDATION_RULES.md` as the historical layer).
- **Hardening the template (or a clone) → follow `docs/audits/AUDIT_SUITE.md`.** The single repeatable
  super-audit (5 diagnostic/gate phases + QA-paranoia + docs/course currency) that produced the `audits/v*`
  runs. It's **triggered, not routine** — run it on a structural core change, a new wave of epics, a major
  dependency bump, or before generating a production app (at minimum re-run its Phase 4 adversarial slice
  pass). Between triggers, keep the gates green instead of re-auditing.
- Touching the schema or entities → read **`docs/DATA_MODEL.md`** first.
- Implementing a screen or flow → read **`docs/FEATURES.md`** first.
- Starting a build slice → read **`docs/WAYS_OF_WORKING.md`** (slices, story format, PR/commit
  conventions).
- Adding an app feature → follow the **clean-platform + vertical-slice convention** in
  `docs/WAYS_OF_WORKING.md` (and ADR-004); copy `src/Api/Features/Notes` as the reference, then
  delete the Notes sample.
- Wondering *why* something is the way it is → check **`docs/DECISIONS.md`** before changing it.
- Changing a settled decision → add a new dated ADR in `docs/DECISIONS.md`; don't silently
  reverse it.
- Rebranding (name, logo, colours, tagline) → follow **`docs/REBRANDING.md`** and complete every
  item. It explicitly covers the **transactional email templates** (`src/Infrastructure/Email/` —
  `BrandedEmail.cs` + `Assets/logo.png` + `Assets/marga.png`), which are inline and easy to miss; a
  rebrand that skips them is incomplete. **Both assets are copies**, not references: `Infrastructure`
  does not depend on `Shared.Ui` and must not start.

## Golden rules — constant (do not violate)
1. **Tenant-scoped, not user-scoped.** App data belongs to the tenant; never leak across tenants.
   Tenant entities implement `ITenantScoped` and are filtered automatically by a global EF query
   filter (see ADR-003); genuinely cross-tenant/pre-auth reads use the sanctioned escape hatch
   `IRepository<T>.QueryAllTenants()`, and signature-/system-authenticated tenant-scoped writes
   (the billing webhook, admin impersonation) enter their tenant via `ITenantContext.EnterTenant`.
   `IgnoreQueryFilters()` is **banned in `src/Api/Features/**`** (fails CI). Only preferences are
   per-user. **A Postgres RLS backstop (ADR-020) re-enforces this at the DB**: a new `ITenantScoped`
   entity must ship its policy in the same migration (`RlsDdl.StatementsFor` — the
   `RlsMigrationGateTests` parity gate fails CI otherwise), and set-based cross-tenant *writes*
   (`ExecuteUpdate/Delete`) need `EnterTenant` — query tags don't render there.
2. **Clean API boundary.** The UI is a client of the API and never accesses the DB directly.
3. **Blazor UI components live in the shared RCL**, not inline in the web app — keeps non-web
   clients cheap.
4. **Derived values are computed, never stored** as stale flags (confirm the app's specific
   derived rules in `docs/DATA_MODEL.md`).
5. **Web-first for features.** The platform ships MAUI desktop + Android shells with auth wired
   (see `docs/MOBILE_TESTING.md`); build each app feature on web first and extend the native
   shells only once it works there.
6. **Latest stable versions only, never previews.**
7. **Test-Driven Development — always.** Write the failing test before the production code on
   every slice. Unit tests (xUnit) in `Core.Tests` / `Api.Tests`; E2E tests (Playwright/NUnit)
   in `E2E.Tests`. No slice merges without tests that drove it; Gherkin scenarios map 1:1 to tests.
8. **Work in vertical slices.** Each slice is end-to-end and leaves the app working; follow
   `docs/WAYS_OF_WORKING.md` for slices, Gherkin stories, Conventional Commits, and the PR
   template. Don't build sprawling multi-epic chunks — propose a split.

## Golden rules — app-specific
1. **Makeable and almost-makeable are always derived** from inventory + recipe lines +
   substitutions at query time; never persisted as a flag (JJ-003, JJ-019). Both are **filters on the
   browse query** (`?makeable=true`, `?almost=true`) rather than screens of their own, so the one
   implementation is `CocktailBrowseHandler` — and "almost" is that same predicate counted rather
   than negated, which is what keeps the two lists adjacent and non-overlapping.
   **Read the substitution graph directionally** — a row says "when a recipe asks
   for X you may pour Y", and reading it the other way would offer drinks a household cannot actually
   make (cognac stands in for brandy; brandy does not stand in for cognac).
2. **The shared catalog is read-only and referenced, never mutated.** Households personalize by
   **forking** — a full snapshot copy with `forked_from_cocktail_id` as provenance only; edits to the
   original never propagate (JJ-002, JJ-013).
3. **Substitutions are global and ingredient-level**, stored as directed rows; custom
   (household) ingredients satisfy recipe lines by exact match only (JJ-004, JJ-005, JJ-006, JJ-018).
   **Not every substitution is mutual** — a recipe asking for brandy takes cognac, and one asking for
   cognac does not take any brandy, so `substitutions.json` has both an interchangeable shape and a
   one-way one. A symmetric graph would recommend drinks a household cannot actually make well.
   **Ingredients are generic where a generic exists, and a proper name where the product has no
   substitute (JJ-017 as amended by JJ-033)** — white rum, not Bacardi; but Chartreuse, Campari and
   Angostura by name, because nothing else is those. The test: could a bartender hand you a different
   bottle and have made the same drink?
4. **Amounts are stored as authored and converted only at display** to the viewing user's
   `preferred_unit_system`; neutral units (dash, barspoon, piece, to taste) pass through (JJ-007, JJ-008).
   The one implementation is **`AmountDisplay` in Core** — conversion happens server-side and the
   authored amount and unit ride along in the response, so a second front end inherits the rules
   rather than re-deriving them. A null preference means "never chose" and shows the recipe as
   written — **which is a choice a reader can return to** (PREFS-2), not merely where they start.
5. **Optional lines never block makeability**, and **ice and water are always available** — never
   model them as blocking inventory (JJ-009, JJ-020). **A recipe's glass and method are optional
   too (JJ-034)** — a quarter of the seeded catalog does not state a glass, so null means "the recipe
   does not say" and is never filled in with a plausible guess, at seed time or at display time.
6. **No manual "main spirit" field.** Spirit/ingredient filtering is derived from recipe lines and
   ingredient categories; a parent category matches all its children, and name matches too (JJ-014, JJ-016).
7. **Lookup tables are curated and global** (`GlassType`, `Method`, `Unit`, `IngredientCategory`); no
   tenant additions in MVP (JJ-022). Inventory is boolean (JJ-023) — and **unticking updates the row
   rather than deleting it**, so "checked and don't have it" stays distinguishable from "never
   looked". Everything that reads the shelf filters on `is_available`, so absence and false are the
   same to every reader.
8. **Platform wins.** App docs never override platform mechanics; when they disagree, defer to the
   platform and log a `JJ-` decision (JJ-026, JJ-028).
9. **The shared catalog is filtered by hand, not by `ITenantScoped` (JJ-031).** `Ingredient`,
   `Cocktail` and `CocktailIngredient` carry a nullable `TenantId` and are deliberately NOT
   `ITenantScoped`. Their isolation comes from an app-level query filter (`TenantId == null ||
   TenantId == CurrentTenantId`) mirrored by hand-written RLS policies in the same migration —
   change the two together. The policies are **asymmetric on purpose**: `SELECT` sees shared rows,
   `INSERT`/`UPDATE`/`DELETE` never do, because one `FOR ALL` policy would let a household delete the
   catalog. **Three** platform guarantees skip these tables, so the app must supply each: nothing
   stamps their `TenantId` (set it explicitly on a household-owned row), no CI gate covers their
   policy, and the dissolution canary cannot see them — every one of them needs an
   `ITenantDataContributor` that wipes household rows only.

## Tech stack (see `docs/TECH_STACK.md`)
- **Versions:** latest stable on the current .NET line — **.NET SDK 10.0.401 (pinned in `global.json`, the single source of truth, with `rollForward: disable` — the 2026-08 drift showed `latestPatch` let runners outrun both the lockfiles and the MCR image catalog), ASP.NET Core / EF Core packages 10.0.11, Npgsql.EF 10.0.3, PostgreSQL 17.** The SDK is **pinned, not floating** (v3 audit DEP-4): CI's `setup-dotnet` reads `global-json-file: global.json`, and both Dockerfile image tags (`sdk:10.0.401` build, `aspnet:10.0.11` runtime) match it — so a runner-image SDK patch can't outrun the committed `packages.lock.json` (the WASM SDK injects patch-sensitive implicit packages → NU1004 in locked-mode restore).
  - **Bump-together playbook** (do all of these in ONE PR when moving the SDK): ① edit `global.json` `version`; ② regenerate every lockfile with the new SDK (`dotnet restore --force-evaluate`); ③ bump the two `Dockerfile` tags — build `sdk:X` and runtime `aspnet:Y` where Y = the ASP.NET **package** line in `Directory.Packages.props` (the `SdkPin_HasOneSource` gate checks exactly that); ④ reconcile the version strings in this file + `docs/TECH_STACK.md` + `docs/DEPLOYMENT.md`; ⑤ re-check the Apple legs' Xcode requirement (the iOS/macCatalyst workload moves with the SDK; `ci.yml` pins the WORKLOAD SET to the image's default Xcode — non-default Xcodes on the runner images can be incomplete, so bump that pin only together with the image's default Xcode).
- **Backend:** ASP.NET Core Web API behind a clean API boundary.
- **Web frontend:** Blazor WebAssembly; UI components in a shared **RCL** (hard rule).
- **DB:** PostgreSQL via **EF Core (Npgsql)**; schema/migrations generated from `docs/DATA_MODEL.md`.
- **Auth:** custom JWT access tokens + rotating refresh tokens — **not** ASP.NET Core Identity
  (see ADR-002); tenant scoping layered on top via a global query filter.
- **Non-web clients:** MAUI Blazor Hybrid (mobile + Win/macOS desktop) — *deferred, don't build now.*

## Auth rules (constant)
- **Secrets are never in appsettings.** In dev they live in the gitignored repo-root **`.env`**
  (loaded by the API via DotNetEnv; the single local source of truth — see ADR-001); in
  production they come from real environment variables. Keys use the `Section__Sub` form.
  `.env.example` (committed) documents them. Never commit `.env`.
- **New OAuth provider = one line.** Add `.AddXxx()` in `ServiceCollectionExtensions`. Don't
  restructure anything else.
- **Passwordless sign-in uses the `LoginToken` entity + `PasswordlessService`** (NOT Identity
  token providers). Magic links and email OTP are single-use, hashed, and time-limited; lifetimes
  are in config (`Auth:MagicLink:TokenLifespanMinutes`, `Auth:Otp:*`).
- **`IEmailSender` (Core abstraction) is the only way to send email.** Never reference MailKit
  directly outside `Infrastructure/Email/`.
- **JWT Bearer auth is configured** in `Program.cs` (validates the app-issued access token, scheme
  `JwtBearerDefaults.AuthenticationScheme`). The token carries a `tenant_id` claim that drives
  tenant query scoping.

## API documentation (constant)
- **The Postman collection mirrors the API — and the repo copy is canonical (ADR-023; the
  `PostmanParityTests` CI gate enforces the floor).** Any change to API
  endpoints (route, verb, path/query params, request/response shape, auth requirements, or error
  codes) must update **`docs/postman/JiggerJot.postman_collection.json`** (+ the environment
  files when config/env expectations change) in the same slice. Controllers in
  `src/Api/Controllers/` and slices under `src/Api/Features/` are the source of truth; the
  collection documents them.
- Keep its conventions: numbered folders per area; `{{baseUrl}}`/`{{accessToken}}` variables with
  collection-level Bearer auth; chaining test scripts that capture shown-once secrets; request
  descriptions stating roles, config gates, and expected error codes; env-specific values live in
  the `*.postman_environment.json` files (one per deploy target), never in the collection.
- Copies in the Postman app/workspace are **mirrors, never the source** (not versioned or
  reviewed). CI keeps the workspace mirror fresh: the `postman-sync` workflow pushes
  `docs/postman/**` to the workspace on every `develop` change (needs `POSTMAN_API_KEY` secret +
  `POSTMAN_WORKSPACE_ID` variable; syncs by name — see `docs/postman/README.md`). Edits made in
  the Postman UI are overwritten on the next sync.

## Scope discipline
Before building anything, check the **"OUT" list in `docs/PROJECT_BRIEF.md`**. Don't implement
deferred items without an explicit decision.

## Conventions
- Code term for the tenant is **tenant**; the reference implementation's app-facing label is
  **Household** (`/api/household`, `HouseholdController`). Rename per app — see `docs/REBRANDING.md`.
- **App decisions are `JJ-nnn`** (closing section of `docs/DECISIONS.md`); platform decisions stay
  `ADR-nnn` / `ADR-Cn`. Cite them that way in code comments, stories and PRs.
- **The tenant label stays "Household"** — the platform's reference label is the app's real one; no
  relabel (JJ-001).
- **Epic keys** for stories and scopes: `INGREDIENT`, `INV` (inventory), `CKTL` (cocktail catalog),
  `MAKE` (makeable engine), `ALMOST`, `FORK`, `AUTHORING`, `ONBOARD`, `FILTER`, `SEED`,
  `MARGA` (the UI character) and `SHELL` (header, responsive nav, boot state). Feature
  slices live in `src/Api/Features/<Feature>/` (e.g. `Features/Inventory`).
- **Shared vs. household rows:** `Ingredient` and `Cocktail` use one table each with a nullable
  `tenant_id` — null = shared seed catalog, set = household-owned (JJ-011, JJ-012). Lookups are global.
- **Recipe line vocabulary:** `is_required` (drives makeable), `role` (`base | modifier | juice |
  syrup | bitters | garnish | mixer | other`), `display_order`; a garnish is just an optional line.
- **Brand:** name **JiggerJot**, tagline **"Mix what you have."** (ES: "Mezcla lo que tienes."),
  copper `#B4562A` primary, icons bone-on-night (JJ-029). Editable SVG sources live in
  `src/Shared.Ui/wwwroot/brand/`, `src/Web/wwwroot/favicon.svg` and `src/Maui/Resources/`;
  regenerate every PNG/ICO with `python docs/brand/build_assets.py`.

## Status / not yet decided
- **Seed data — in progress** (`docs/stories/seed.md`). **Done (SEED-1):** the curated global
  lookups — glasses, methods, units and the two-level ingredient categories — live in the embedded
  `src/Infrastructure/Persistence/Seed/lookups.json` and are written at startup by `CatalogSeeder`
  (idempotent, adds only what is missing, never deletes). **Extraction workspace** stays in `seed/`:
  the 1930 Savoy Cocktail Book (`seed/savoy_cocktails.json`, 868 recipes) and the **IBA official
  list** (`seed/iba_cocktails.json`, 102 drinks in three groups of 34) are both extracted. Raw
  extractions are **not** seed data; nothing ships without a curation pass. **Sources are settled
  (JJ-032):** Savoy for vintage depth, the IBA list for the modern canon — Savoy has zero tequila,
  zero bourbon and one Campari line, and no public-domain book fixes that, so the Waldorf-Astoria
  and bartender's-guide PDFs were **dropped and deleted** (2026-09-09) — neither was ever extracted.
  Specifications only from every source; prose stays where it is, and attribution ships in the data.
  **Done (SEED-2):** the ingredient catalog — 175 curated ingredients from 395 raw names, in
  `ingredients.json`, seeded as shared rows; `seed/build_ingredients.py` fails the build while any raw
  name is neither mapped nor explicitly excluded. **Done (SEED-3):** 969 cocktails and 3526 recipe
  lines extracted, each credited to a `RecipeSource` (JJ-032). **The SHIPPED catalog is a 31-recipe
  starter set** chosen to cover every shape the model handles (metric and proportional amounts,
  duplicate names within and across sources, missing glass/method, an optional garnish, a substitution
  in play, modern spirits); `python seed/build_cocktails.py --full` emits all 969. Developing against
  nine hundred rows made every test assertion a claim about the catalog rather than about behaviour.
  **Never hard-code the catalog's size in a test** — derive it from `CatalogSeeder.LoadCocktails()`. **Done (SEED-4):** the substitution graph — 17
  interchangeable groups and 12 one-way entries, 88 directed rows. **The seed epic is complete for
  MVP.** Two threads stay open and neither blocks anything: the Savoy extraction came from a
  transcription website rather than the book, and two scrape-merged recipe lines are excluded rather
  than guessed at.
- Concrete schema (EF Core migrations) — generated from `docs/DATA_MODEL.md`.
- **User stories: generated per-epic at build time**, under `docs/stories/` (one file per epic).
- Non-web framework: **decided and built** — MAUI Blazor Hybrid ships all four native shells
  (epic `NATIVE` ✅ complete 2026-07-14, ADR-018; signing/stores are downstream per ADR-024, resolving the 2026-07-06 amendment's gate). Hosting is
  likewise **decided** (ADR-017: Render free single-origin + Neon + Brevo) — built by epic `DEPLOY`.

## Doc map
| File | Purpose |
|------|---------|
| `CLAUDE.md` (root) | This file — operating manual, auto-loaded |
| `docs/PLAN.md` | **Read first, every session** — the three gates, the slice ritual, the editing rules, where things stand, and the sequenced backlog (MAKE-1 → ALMOST-1 → CKTL-4 → INV-2 → FILTER-1 → FORK-1 → AUTHORING-1 → ONBOARD-1) |
| `docs/NEW_APP_GUIDE.md` | **The onboarding spine** — every phase from idea to production, in order, linking the detailed doc per step |
| `docs/OVERVIEW.md` | Friendly platform tour (PM/power-user/developer/architect) — no codebase knowledge assumed |
| `docs/PROJECT_BRIEF.md` | Why/what/scope (lean PRD) + OUT list |
| `docs/brand/build_assets.py` | Regenerates every brand PNG + `favicon.ico` from the SVG sources (headless Edge + Pillow); store/marketing renders land in `docs/brand/` |
| `seed/` | The seed workspace — both extractions (`savoy_cocktails.json`, `iba_cocktails.json`), their scrapers, and the two curation scripts that emit the shipped files. Not shipped |
| `docs/FEATURES.md` | User flows & behavior |
| `docs/DATA_MODEL.md` | Entities, relationships, derived rules |
| `docs/TECH_STACK.md` | Stack choices + rationale |
| `docs/DECISIONS.md` | ADR log (the "why") |
| `docs/ARCHITECTURE.md` | Mermaid diagram layer — solution map, seams, per-subsystem class diagrams; drawn from the code, ADR-cross-linked |
| `docs/FLOWS.md` | Sequence diagrams for the core call stacks (auth, tenancy, outbox, billing webhook, dissolve) + the OTP line-level walkthrough |
| `docs/WAYS_OF_WORKING.md` | Slices, story format, commit/PR conventions |
| `docs/audits/AUDIT_SUITE.md` | **The repeatable super-audit** — 5 diagnostic/gate phases + QA-paranoia + docs/course currency; triggered, not routine; `audits/v1..v3` are its worked runs |
| `docs/REBRANDING.md` | Every brand touchpoint to replace per app — **incl. the email templates** |
| `docs/LOCALIZATION.md` | i18n setup (EN/ES live) + how to add a language |
| `docs/MOBILE_TESTING.md` | Run/sign-in on the Android emulator (adb reverse, OAuth) |
| `docs/QA_TEST_PLAN.md` | Manual QA plan — step-by-step tests across web + all four native platforms (211 cases: smoke + regression + §10d–§10i the app's own flows + §14a v3-audit adversarial/tenant-isolation + §13c native release checklist). **§10d–§10i are JiggerJot's own flows** — the shelf, the catalog and filters, makeable/one-away, forking and authoring, the onboarding wizard, and Marga plus the chrome. They were added in one pass after nineteen app slices had shipped against a plan that covered the platform only; **the slice ritual in `docs/PLAN.md` now carries a step so it cannot drift again** |
| `docs/ROADMAP.md` | Sequenced plan — pillars + all waves done (terminal state 2026-07-14) + the **post-terminal cost wave** (`LOCALCI` — local/self-hosted CI) + the **flavors wave** (`FLAVORS` — spec + conformance kit, then React/Angular/Flutter fronts, Node/Go/Spring/FastAPI backs, Expo for true-native mobile, tiered DBs incl. SQL Server; both planned 2026-09-08) |
| `docs/STATUS.md` | 2026-07-04 status snapshot + operator guides — native QA pass (✅ 2026-07-14), Apple first-run smoke (MacBook walkthrough), prod activation (⤵ downstream Phase-8 runbook, ADR-017 amendment); SaaS-readiness assessment |
| `docs/PLATFORM_BACKLOG.md` | Per-item design sketches for the future foundation slices (the detail behind ROADMAP) |
| `docs/stories/` | User stories per epic — generated at build time |
| `docs/stories/seed.md` | epic `SEED` 🚧 IN PROGRESS — SEED-1 ✅ the curated global lookups (19 glasses, 10 methods, 22 units, 25 categories with 154 subcategories) in one embedded `lookups.json`, written at startup by an idempotent `CatalogSeeder` behind `Seed:Catalog:Enabled`; ids derive from names (`SeedId`) so they are stable across environments and a rename is a data migration; the seeder refuses to run under a household because only a system context may write a shared row (JJ-031). plus the IBA extraction ✅ (`seed/scrape_iba.py`, 102 drinks, sitemap-driven, three-groups-of-34 used as a parse check). Sources settled by **JJ-032**: Savoy plus the IBA list, the two 1930s bar books dropped. SEED-2 ✅ the ingredient catalog (175 curated from 395 raw names; coverage enforced by `seed/build_ingredients.py`, brand-name rule in **JJ-033**). SEED-3 ✅ 969 cocktails + 3526 lines, credited to a `RecipeSource`; identity is the source plus its slug, glass and method optional (**JJ-034**), 60 prose recipes recovered from tag lists. SEED-4 ✅ the substitution graph (17 interchangeable groups + 12 one-way entries → 88 directed rows, each carrying its reasoning). **Epic complete for MVP** |
| `docs/stories/almost.md` | epic `ALMOST` ✅ COMPLETE for MVP — ALMOST-1 `GET /api/cocktails?almost=true` + a second toggle on the catalog screen, exclusive with the makeable one. Exactly ONE required line unsatisfied **after substitutions** (FEATURES §10, JJ-019), and every row names that bottle in `missingIngredient` — the name is the feature, since "one short" alone is just a list of drinks you cannot make. Same predicate as MAKE-1's, counted rather than negated, so the two lists are adjacent and can never overlap; asking for both filters returns an empty page rather than one flag winning. Rows still carry the substitutions in play (FEATURES §9). ALMOST-2 ✅ (**JJ-035**) the inverse — `GET /api/cocktails/unlocks` groups the almost set by MISSING INGREDIENT rather than by cocktail and ranks it, so one bottle can be named as unlocking several drinks (measured: a gin-and-Campari shelf leaves 81 drinks one away, and dry vermouth alone accounts for 13). Runs ALMOST-1's predicate verbatim so the two readings cannot drift; `unlocks` always equals the length of `cocktails`, ties break on name, an empty shelf ranks nothing. Reverses the "no ranked shopping list" exclusion this epic and FORK both carried |
| `docs/stories/authoring.md` | epic `AUTHORING` ✅ COMPLETE for MVP — AUTHORING-1 `POST /api/cocktails` + `GET /api/cocktails/lookups` + the `/cocktails/new` form. A written drink joins makeable and filtering immediately and for free, because both derive from the recipe lines at query time (JJ-003, JJ-014). **Two lookup endpoints that must not be merged**: `/filters` is catalog-derived so no filter is a dead end, `/lookups` is the whole curated set (JJ-022) so a form can reach a glass no recipe uses. Glass and method stay optional (JJ-034); line order is the array's; refuses a lineless recipe, a unit with no amount and an ingredient the household cannot see. Request enums cross the wire BY NAME — a per-property converter, not a global one. ⚠️ **AUTHORING-2 outstanding** — editing a cocktail, including a fork |
| `docs/stories/marga.md` | epic `MARGA` ✅ COMPLETE for MVP — the UI character. **Not an assistant**: a drawn bartender who says FIXED resource strings with real data in the placeholders; no model, no generated text. MARGA-1 her asset + one shared component + three lines over existing data (proposal screens 2, 3, 8) · MARGA-2 the home screen, closing the `Home.razor` TODO (screen 1) · MARGA-3 the two empty states (screens 6, 7). MARGA-1 ✅ — `MargaSays` takes a FINISHED sentence and renders her beside it; it never builds one, picks one or fetches anything, which is what keeps "not an assistant" true in code. Her illustration ships as two optimized brand assets (avatar 19 KB, scene 115 KB) from a 2 MB source that is deliberately NOT committed. MARGA-2 ✅ the home screen — the makeable count as the headline, her line naming the one purchase that extends it, and both buttons deep-linked so each lands on the list that produced its number. MARGA-3 ✅ the empty states, which settled **what a first bottle is**: `GET /api/cocktails/starters` ranks the ingredient the most recipes ASK for among those the household lacks (required lines only per JJ-009, substitutions ignored, shelf subtracted). A THIRD reading of the catalog, not a variant of `/unlocks` — that one ranks the almost-makeable set, which is empty for a household that has ticked nothing. **`appears` is how many recipes ask for it, never how many it would unlock**, and the copy says so; one bottle on an empty shelf makes very nearly nothing. The suggestion shows only when the one-away filter is the ONLY thing narrowing the list, or an empty search would tell someone with forty bottles to go shopping MARGA-4 ✅ her in the TRANSACTIONAL EMAILS — the sign-in code, the sign-in link and the invitation, and deliberately **not** the notification wrapper, which carries failed payments and security alerts (a bartender on those undercuts the message; held by a test, not a comment). A CID inline attachment like the logo, since Gmail and Outlook both block data-URIs, and attached ONLY to the emails that show her. Her avatar is a COPY in `Email/Assets/` because `Infrastructure` must not depend on `Shared.Ui` — `REBRANDING.md` names both files. `alt=""` matters more here than in the app: most clients block images, so the sentence alone is the common case MARGA-5 ✅ **where she actually is** — she shipped on seven surfaces but four were conditional and one is a flash, so a filled shelf met her once on the home page and never again, and the SHELF (most dwell time in the app) had none. Now: the shelf names the bottle it is one short of, the makeable filter's count becomes her line, the one-away card gets her face, and the recipe page can finally say YES rather than only explaining a compromise. **The rule that keeps her from becoming wallpaper**: she speaks where a number needs interpreting and stays quiet where the screen already says it plainly, one PAGE-LEVEL Marga per screen (a 24px per-row aside is a footnote, not the page's voice). No new engine work. Two bugs it caused, both caught by tests: her fetch shared the payoff footer's catch and blanked its count, and once separated a FAILED fetch still let her claim "that is everything your shelf reaches" — she now tracks whether the answer came back |
| `docs/stories/shell.md` | epic `SHELL` ✅ COMPLETE for MVP — the chrome. SHELL-1 ✅ the responsive shell: the app's three destinations become a bottom tab bar below `lg`, and the hamburger keeps only the account cluster. **ONE element, two positions** — the same `<ul>` repositioned by CSS, never a second copy hidden at one width, because two `nav-shelf` in the DOM fails every journey that clicks it by test id on an ambiguous locator; it also sits OUTSIDE `.navbar-collapse`, or the tab bar would be hidden below `lg` until someone opened the menu this slice removes. Settled the wide-screen hierarchy question **at both widths**: the destinations are raised (85% white, full white and bold when current), the platform's account buttons deliberately untouched. Current tab = weight plus a drawn indicator, never colour alone, plus `aria-current` — which `NavLink` does not supply, so the header re-renders on EVERY location change. A fixed bar sits ON the page, so the content container pads and INV-3's payoff footer clears it via one global `--tab-bar-clearance` token · SHELL-2 ✅ the boot state: the stock two-circle spinner becomes her illustration, rocking as if shaking (**CSS on the static drawing**, so there is no second asset), with the brass arc still reading the real `--blazor-load-percentage`. It lands in **two** `index.html` files per `NATIVE_PARITY.md`, and that rule is now a **CI gate** rather than a maintainer note — both must carry the markup and point at the same drawing, with the one intended difference (`boot-indeterminate` on MAUI, which has no download to measure) asserted in BOTH directions. A second gate caps the asset's size, since this is the one place it is fetched before the app is usable. ⚠️ **`calc()` cannot divide a percentage by a percentage, and an invalid `calc` is dropped silently** — the arc rendered a sixth of a turn beside a label reading 81% |
| `docs/stories/fork.md` | epic `FORK` ✅ COMPLETE for MVP — FORK-1 `POST /api/cocktails/{id}/fork` + the button on the recipe page. A SNAPSHOT copy, never a reference (JJ-002, JJ-013): a new tenant-owned `Cocktail` plus copies of every line, amounts as authored, `TenantId` set by hand on both tables (JJ-031). The source credit is deliberately NOT copied — the book wrote the original, not the household's version (JJ-032) — so provenance rides on `ForkedFromCocktailId`, surfaced as "Based on X". That column is not a foreign key, and the slice has the test that proves why: deleting the original leaves the copy standing |
| `docs/stories/filter.md` | epic `FILTER` ✅ COMPLETE for MVP — FILTER-1 four combinable filters on `GET /api/cocktails` (`ingredient`, `method`, `glass`, `serving`) + `GET /api/cocktails/filters` for the dropdowns + the filter panel on `/cocktails`. The ingredient filter reads the RECIPE LINES (there is no main-spirit column and never will be, JJ-014) and matches the ingredient's name, category and subcategory at once, so a parent catches every child (JJ-015, JJ-016). Dropdown options are derived from the catalog rather than the curated lookups, so every option returns something. A recipe with no glass (JJ-034) does not match a glass filter; a bad id is an empty page, not a 400 |
| `docs/stories/makeable.md` | epic `MAKE` ✅ COMPLETE for MVP — MAKE-1 ✅ `GET /api/cocktails?makeable=true` + a toggle on the catalog screen (FEATURES §11 makes it a combinable filter, not a separate view). Each result carries the substitutions in play — "using Curaçao in place of Cointreau" (FEATURES §9). Derived at query time, never stored (JJ-003): a required line is satisfied by the exact ingredient or by anything the substitution graph allows **in that direction**, so a one-way substitution stays one-way. Closed the EF warning about `Ingredient`'s filter vs `IngredientSubstitution`'s required navigations by making JJ-005 a query filter |
| `docs/stories/inventory.md` | epic `INV` ✅ COMPLETE for MVP — INV-1 the shelf: `GET /api/inventory` (the WHOLE catalog with an availability flag, since you cannot tick what you cannot see) + `PUT /api/inventory/{id}` + the `/shelf` screen, grouped by category with search and an only-what-I-have filter. Unticking updates rather than deletes, so "checked and don't have it" stays distinguishable from "never looked"; ticking is optimistic and rolls back. The one plainly `ITenantScoped` entity, so tenancy is entirely the platform's. INV-2 ✅ add a custom ingredient inline — `POST /api/inventory/ingredients` + `GET /api/inventory/categories` and the add form on `/shelf`. The app's first write to a dual-natured catalog table, so the handler sets `TenantId` by hand (nothing stamps it, JJ-031) and a test dissolves a household to close the gap the platform canary cannot see. Created ticked; duplicates refused case-insensitively against the shared catalog as well as the household's own, with 409 carrying `existingIngredientId`; the category/subcategory pairing is validated (JJ-015, JJ-016). INV-3 ✅ the shelf rework — `.btn-check` pills (a real checkbox, filled when selected so it cannot be confused with the catalog's outlined filter pills), per-category counts, a jump bar reading the same value so the two cannot drift, `+ Add your own` inside the card it files into, and a sticky footer showing the payoff. **No API change**: presentation over data the screen already had. The payoff's makeable total is re-asked of the browse endpoint rather than returned by the write, because a slice may not reference another slice (R7/TR-9) and duplicating the query would give the app two definitions of makeable — and it is asked once per BURST, not once per tick. Category counts are over the whole category, never over what search left visible. A jump link spells out its path (`/shelf#cat-gin`): `<base href="/">` makes a fragment-only href resolve against the base, so every one of them left the page |
| `docs/stories/cocktails.md` | epic `CKTL` ✅ COMPLETE for MVP — CKTL-1 ✅ the nine domain entities, their configurations, the one migration that creates them, and the two walls that make the dual-natured catalog tables safe (app-level query filter + four command-scoped RLS policies, JJ-031) plus the three app-level tests that replace the platform guarantees those tables do not inherit; CKTL-2 ✅ browse — `GET /api/cocktails` (paged, name search, clamped rather than 400) + the `/cocktails` screen in the RCL; ordered name-then-id because name alone is not a total order in this catalog, and the source is shown per row because four names appear in both books. CKTL-3 ✅ detail — `GET /api/cocktails/{id}` + the `/cocktails/{id}` screen; amount display lives in Core's `AmountDisplay` (neutral units never convert, metric never uses fractions, ounces always do, rounding never reaches zero) and conversion happens server-side with the authored values riding along. A cocktail from another household is a 404, never a 403. CKTL-4 ✅ the recipe says where it stands — `makeability` (`Makeable`/`AlmostMakeable`/`NotMakeable`) on the detail response plus per-line `availability` and `substituteWith`, rendered as a badge and a mark on the one line that needs attention. The derived rule moved into `Core/Catalog/Makeability.cs` beside `AmountDisplay`; the browse filters stay set-based SQL, and a test walks the whole catalog asserting the two never disagree |
| `docs/stories/onboard.md` | epic `ONBOARD` ✅ COMPLETE for MVP — the first minute (FEATURES §7, JJ-021), and the last unbuilt flow. ONBOARD-1 ✅ `/welcome`: two steps over the SAME data and the SAME control as the shelf — guided, not a second way to record what you own (which is why INV-3's pill styles moved from scoped CSS into `app.css`; two copies of one control is how it stops being one). **"Common staples" needed no second curated list** — MARGA-3 had already settled the honest definition, so it asks `/api/cocktails/starters?limit=12` and puts them on screen ALREADY TICKED, which is what makes the step fast. **Nothing is written until Finish**, hence the new `PUT /api/inventory`: the whole shelf in one request, state stated rather than toggled, safe to send twice, an unknown id reported rather than fatal. The per-ingredient `PUT` stays for the shelf, where a tick IS the decision. Every row is sent, not just the changes, or an unticked bottle would silently stay (JJ-023). **Offered, never forced**: no redirect and no dismissal flag, so no "has this household been onboarded" fact to store — `Tenant` is the platform's and holding app state there is the wrong direction. Members joining by invitation skip it for free: they have a shelf, so the front page shows the count |
| `docs/stories/ui.md` | epic `UI` ✅ COMPLETE — **retrospective** (v3 T59, closing v2 DOC-22): the four 2026-07 web-UI slices that shipped without a story file — UI-1 GDPR export/erasure UI, UI-2 MFA UI, UI-3 notification bell/prefs UI, UI-4 staff `/admin` console; defines what QA §2 + the traceability matrix cite |
| `docs/stories/billing.md` | epic `BILLING` ✅ COMPLETE — entitlements + Checkout + webhook + Portal (1–4) + seat/usage quotas (5, `IQuotaService`) + trial/dunning (6, `IBillingNotifier` + lapse sweep via NOTIFY) + dissolve cleanup (7, `BillingDataContributor` cancels the provider sub + wipes the projection) + billing page (8, `GET /api/billing` summary + `/billing` UI, fake-provider E2E upgrade loop) + seat re-check at invitation accept (9, 2026-07-14: downgrade left stale invites joinable past the cap → 402 `seat_limit_reached` + `/join` "household full" state, self-heals on upgrade); ADR-006 |
| `docs/stories/async-jobs.md` | epic `JOBS` ✅ COMPLETE — outbox+dispatcher, inbox, scheduler (ADR-007) |
| `docs/stories/observability.md` | epic `OBS` ✅ COMPLETE — logging, OpenTelemetry, health, append-only audit log (ADR-008) |
| `docs/stories/rbac.md` | epic `RBAC` ✅ COMPLETE — `admin` role + permission seam (RBAC-1) + owner-only role change (RBAC-2) + admin-aware roster UI (RBAC-3); ADR-009 |
| `docs/stories/files.md` | epic `FILES` ✅ COMPLETE — `IFileStorage` local/S3, tenant-scoped keys, signed URLs (FILES-1 abstraction, FILES-2 download, FILES-3 S3); ADR-010 |
| `docs/stories/gdpr.md` | epic `GDPR` ✅ COMPLETE — tenant data export + account erasure on the contributor/dissolve/file-storage machinery (GDPR-1 export, GDPR-2 erasure); ADR-011 |
| `docs/stories/mfa.md` | epic `MFA` ✅ COMPLETE — authenticator TOTP; Otp.NET, secret encrypted, hashed recovery codes (MFA-1 enroll/manage, MFA-2 JSON-path step-up, MFA-3 OAuth/magic-link redirect step-up, MFA-4 native step-up — enforced on **every** sign-in path); ADR-012 |
| `docs/stories/notify.md` | epic `NOTIFY` ✅ COMPLETE — per-user in-app notification center + delivery prefs, fan-out via the outbox (NOTIFY-1 center, NOTIFY-2 prefs+email); 2026-07-09: caller-scoped delete/clear (`DELETE /{id}`, bulk `?read=true`/all) + bell trash/clear-read/clear-all UI; ADR-013 |
| `docs/stories/admin.md` | epic `ADMIN` ✅ COMPLETE — config-gated platform-staff surface: cross-tenant inspection + short-lived audited impersonation + staff announcements via NOTIFY fan-out (ADMIN-1 gate/inspect, ADMIN-2 impersonate, ADMIN-3 announce — audited in-tenant, per-user rows only); 2026-07-09 (ADR-021 — enumerated admin **writes**): announce `user_ids` targeting + platform-wide `announce-all` (202 → outbox fan-out) + subscription comp/revert (409 when Stripe-backed) w/ console UI; ADR-014 |
| `docs/stories/pubapi.md` | epic `PUBAPI` — public API + tenant API keys, **config-gated default-off** (PUBAPI-1 ✅ — hash-only keys, API-key auth scheme → `tenant_id`-scoped principal, owner mgmt, scoped `/api/public`; PUBAPI-2 ✅ — per-key rate limit + anonymous public OpenAPI doc `/api/public/openapi.json`); ADR-015 |
| `docs/stories/hooks.md` | epic `HOOKS` — outbound webhooks, **config-gated default-off** (HOOKS-1 ✅ — `WebhookSubscription` encrypted secret, `IWebhookPublisher` fan-out → outbox → HMAC-signed POST w/ retry, owner `/api/webhooks` + send-test; HOOKS-2 ✅ — delivery log + replay); ADR-016 |
| `docs/stories/theme.md` | epic `THEME` ✅ COMPLETE — per-user dark mode (THEME-1): Light/Dark/System on Bootstrap `data-bs-theme`; pre-paint `theme.js` bootstrap in BOTH hosts' index.html (parity), `IThemePersistence`/localStorage (one impl serves web + MAUI — no Preferences bootstrap needed, unlike culture), header + login `ThemeSwitcher`, `User.Theme` + `PUT /api/auth/theme` + `theme` JWT claim + `MainLayout` reconcile; dark token block in `app.css`; E2E `ThemeJourneyTests` (suite 30→31), QA-SET-08/DSK-15/AND-14. **Amended by PREFS-1** ("system" now stored verbatim; reconcile on every sign-in) |
| `docs/stories/prefs.md` | epic `PREFS` ✅ COMPLETE — **PREFS-2** ✅ measurement preference (`PUT /api/auth/unit-system` + `UnitSwitcher` in Settings; three options, of which "as written" is a real one — null means never chose and a reader can come back to it; "Neutral" is a 400, since it is a property of a unit and not something anyone can prefer; no device-local copy, because the server does the conversion). Per-user preference sync (PREFS-1, ADR-022): Settings → Preferences card (language + theme switchers, signed-in home); `AuthService.SignedIn` event → `MainLayout` reconciles on EVERY sign-in path (not just cold starts); two-way sync (server wins; never-set server value adopts the device choice — a pre-auth login-page pick becomes the account pref; never while impersonating); locale mismatch = persist + ONE reload (WASM satellite assemblies — reverts B7-3's in-process switch); "system" stored verbatim (null = never chose). Fixes QA-I18N-02 (was: locale unreachable from UI) + theme-after-OTP-sign-in instability; E2E `LocaleChoice_FollowsTheUser_AcrossBrowsers` (suite 31→32), QA-I18N-02 now ⚙️ automated |
| `docs/stories/e2e.md` | epic `E2E` ✅ COMPLETE — Playwright journeys (suite 7→26 tests): E2E-1 RBAC roster; E2E-2 billing seat-quota 402 UX; E2E-3 notification bell/prefs (list/mark-read covered via ADMIN-3 announcements); E2E-4 magic-link sign-in (happy + single-use); E2E-5 membership lifecycle (transfer/leave/dissolve/delete-account); BILLING-8 added the fake-provider upgrade-loop journey. Health = DEPLOY-3 smoke, not a browser test |
| `docs/stories/deploy.md` | epic `DEPLOY` ✅ COMPLETE — staging/prod on the free tier: DEPLOY-1 single-origin (API serves the WASM) + config-gated forwarded headers; DEPLOY-2 Dockerfile + compose parity + `render.yaml` + `docs/DEPLOYMENT.md` (staging live, all 4 sign-in paths verified); DEPLOY-3 CI deploy pipeline (develop→staging auto + version-gated smoke, main→prod gated) + QA §1.5; ADR-017 |
| `docs/DEPLOYMENT.md` | Deployment runbook (DEPLOY-2/3) — Render + Neon + Brevo free-tier bring-up; `Dockerfile` + `render.yaml` reference; required env incl. the Production Stripe-key guard; §6 CI-gated auto-deploy |
| `docs/stories/localci.md` | epic `LOCALCI` 📋 PLANNED (2026-09-08) — local + self-hosted CI, pick-up-ready: LOCALCI-1 variable-driven `runs-on` (`vars.CI_{LINUX,WINDOWS,MACOS}_RUNNER` → self-hosted, hosted fallback; deploys stay hosted; R77 tripwire; runner install runbook → DEPLOYMENT §10; ADR-025 draft in the file) · LOCALCI-2 land `ci-local.ps1` (extract from the STALE unpushed `ci/local-gates` branch, never merge it) + R78 pin/gate-list tripwire · LOCALCI-3 paths gate for every non-deploy job + weekly Apple smoke in hosted mode. Measured: ≈190 billed min per develop code push, ≈157 of them macOS/Windows |
| `docs/stories/flavors.md` | program `FLAVORS` 📋 PLANNED (2026-09-08) — the platform as a stack-neutral **spec** + conformance kit, pick-up-ready: `SPEC` (SPEC-1 extract contract into a `spec` repo, SPEC-2 R1–R76 restated as outcomes + classified K/S/P/X, SPEC-3 kit = Newman 79 requests + 35 journeys + QA-ADV adversarial + DB probes as one Docker image, SPEC-4 test-id contract from the 95 existing ids, SPEC-5 conformance matrix + version protocol) → `FRONT-REACT` (FR-1..8 screen ladder + Capacitor/Tauri) → `BACK-NODE` (BN-1..11 slice ladder) → `BACK-GO` → Spring+Angular → FastAPI → `NATIVE-RN`/`FRONT-FLUTTER`/`DB-SQLSERVER`/`SCAFFOLD` on demand; ADR-026 + spec ADRs S-001..S-003 drafted inside |
| `docs/stories/native.md` | epic `NATIVE` ✅ COMPLETE (2026-07-14 — NATIVE-6 device pass green; distribution moved downstream, ADR-024) — full MAUI parity (Android/Windows/iOS/macOS): NATIVE-1 ✅ CI build gate (all 4 TFMs; Apple legs on develop pushes — 10× macOS minutes; Maui lockfile excluded by design) + NATIVE-2 ✅ parity audit → gaps G1–G6 + NATIVE-4b ✅ join-by-invite-code on /join (G5 closed; E2E suite 26→28) + NATIVE-5 ✅ culture bootstrap (G6 closed: ICulturePersistence seam, MAUI Preferences + MauiProgram bootstrap; Windows-verified via WebView2 CDP) + NATIVE-3 ✅ downloads (G1 closed: Content-Disposition attachment + IFileDownloadLauncher — web same-tab download, native OS share sheet; E2E suite 28→29); + NATIVE-4 ✅ (G2 refresh-on-resume AppResumeNotifier + G3 Android back handler) — Wave 2 COMPLETE, all six gaps closed; NATIVE-6 QA plan authored (117 cases: DSK-08..14, AND-07..13, iOS/mac first-run smoke, release checklist) + G7 Apple-boot fix (iOS/macCatalyst crashed at startup — WebAuthenticator initiator generalized + Info.plist schemes); **Apple column UNPINNED 2026-07-06** — §13b run on the maintainer's MacBook: QA-IOS-01/02/04 + QA-MAC-01/02 + OAuth PASS (two gaps fixed in PR #125: SMTP revocation knob + Catalyst Debug session store); NATIVE-7 ✅ COMPLETE smokes for all four platforms in CI (Windows WebView2-CDP + Android emulator playwright-core _android + iOS-simulator & Mac Catalyst boot-to-login canaries in one `native-smoke-apple` job — WKWebView has no CDP, so they assert process-alive + provider-probe-200, no UI driving; native-paths gate skips Apple/smoke legs on docs-only pushes; deploy-staging concurrency); **NATIVE-6 ✅ FULL PASS 2026-07-14** — the maintainer completed the entire manual QA process as the plan stood then (125 cases incl. the leftover §13b spot-checks), no open findings; post-pass additions (§14a re-runs + QA-AND-15) are the open device items; **NATIVE-8..11 (signing/installers/stores) ⤵ MOVED DOWNSTREAM (ADR-024)** — per-app work, checklist in `NEW_APP_GUIDE.md` Phase 9, Wave 4 kept as reference incl. the two signing-identity re-verify traps (packaged-MSIX SecureStorage + Apple keychain-access-groups); ADR-018 |
| `docs/NATIVE_PARITY.md` | NATIVE-2 audit — WebView-vs-browser deltas × platform × screen (✅/⚠️/🔍 verdicts); gap register G1–G6 → Wave-2 slices; maintainer rules (index.html sync, emailed links land on web, forceLoad = leaves the app) |
| `docs/postman/` | **Postman collection for the whole API** (collection + per-env environments: local, staging/Render + README) — chained OTP sign-in w/ Mailpit auto-fetch, token rotation, all surfaces incl. config-gated PUBAPI/HOOKS and the admin writes. **CI-mirrored to the Postman workspace** (`postman-sync` on develop; one-way, repo canonical); rename on rebrand (incl. the workflow's file path) |
| `.env.example` | **Config catalog** — every configurable key + its default (CONFIGURATION REFERENCE block) + the compiled-in "not configurable" limits; CI-enforced source of truth (`ConfigKeys_ReadInCode_AreDocumented`) |
| `.github/pull_request_template.md` | PR checklist (auto-loaded by GitHub) |
| `src/Infrastructure/Persistence/Migrations/` | Concrete schema — EF Core migrations generated from DATA_MODEL.md |
