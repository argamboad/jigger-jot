# Decisions (ADR log)

> Lightweight architecture/product decision records. Each entry: the decision, the rationale, and
> the date. Purpose: stop us (and Claude Code) from re-litigating settled choices later. Append
> new decisions; don't rewrite history — supersede with a new dated entry instead.

> All entries below: **2026-06-17** (initial conceptualization session).

---

**ADR-001 — Unit of account is the household (tenant), not the user.**
Inventory and custom content are shared across a household/bar. Multiple users can belong to one
tenant. *Rationale:* matches real use (a shared shelf); avoids duplicating inventory per person.

**ADR-002 — Shared catalog is referenced, with copy-on-fork (not copy-per-tenant).**
Households reference the global catalog; "Create my own version" forks an independent copy.
*Rationale:* keeps the shared catalog clean while allowing personalization without mutating
shared records.

**ADR-003 — "Makeable" is always derived, never stored.**
Computed from inventory + recipe lines + substitutions at query time. *Rationale:* a stored flag
would go stale the instant inventory changes.

**ADR-004 — Substitutions are ingredient-level, not recipe-level (MVP).**
A substitution applies globally between two ingredients. *Rationale:* far simpler; covers the
common case (Kahlúa ↔ Tia Maria). Recipe-level subs pinned for future.

**ADR-005 — Substitutions are global-only for MVP.**
No tenant-specific subs yet. *Rationale:* keeps the substitution graph simple and shared.
Tenant-level subs pinned.

**ADR-006 — Substitution symmetry stored as two directed rows.**
Rather than a `symmetric` flag. *Rationale:* simpler, more predictable queries.

**ADR-007 — Structured quantities (amount + unit), stored as authored.**
Not freeform text; not converted on storage. *Rationale:* enables conversion/scaling while
preserving idiomatic units (a dash, a barspoon) losslessly.

**ADR-008 — Unit-system preference is per-user; conversion at display time.**
Metric/imperial toggle on User; convertible units converted on display, neutral units pass
through. *Rationale:* household members can disagree on units; storage stays canonical.

**ADR-009 — Required/optional lives on the recipe line, not the ingredient.**
`is_required` on CocktailIngredient. *Rationale:* "optional" is contextual to a drink (lime is
required in a Margarita, optional elsewhere). A garnish is just an optional line.

**ADR-010 — Recipe lines carry a `role` (base/modifier/juice/garnish/…).**
*Rationale:* cheap to add, enables display grouping; wanted from day one.

**ADR-011 — Single `Ingredient` table with nullable `tenant_id` (null = global).**
Rather than separate global/custom tables. *Rationale:* simpler queries; substitutions and
recipes can span global + custom without unions.

**ADR-012 — Single `Cocktail` table with nullable `tenant_id` + nullable `forked_from`.**
Same single-table rationale as ADR-011.

**ADR-013 — Fork = snapshot copy, not live reference.**
Forking copies the cocktail and all recipe lines; `forked_from` is provenance metadata only.
*Rationale:* households own a stable, independently editable recipe; upstream edits shouldn't
silently change their drink.

**ADR-014 — No manual `base_category` / "main spirit" field.**
Spirit/ingredient filtering is derived from recipe lines + ingredient categories. *Rationale:*
the manual field is fragile (no-spirit, two-spirit drinks); derived filtering is more powerful
("everything with elderflower"). Manual primary classification pinned for future curation.

**ADR-015 — Two-level ingredient categorization (category + subcategory).**
Self-referencing IngredientCategory (parent/child). *Rationale:* lets users filter "all rum" or
drill into "dark rum" without over-engineering a deep hierarchy.

**ADR-016 — Ingredient filtering matches by category AND by name.**
*Rationale:* "vodka" should find both the Vodka category and name matches; maximizes recall.

**ADR-017 — Generic ingredients only for MVP (no brand/product granularity).**
Recipes and inventory both operate on generic ingredients ("vodka," not "Tito's"). *Rationale:*
brand hierarchy is a whole subsystem; generic covers the core loop. Pinned.

**ADR-018 — Custom ingredients satisfy recipe lines by exact match only.**
They don't participate in the (global) substitution graph in MVP. *Rationale:* keeps the sub
graph shared/clean; revisit alongside tenant-level subs.

**ADR-019 — "Almost makeable" is in MVP, fixed at exactly one missing required ingredient.**
*Rationale:* high-value discovery/shopping feature, trivial given the model; N=1 keeps UX focused.

**ADR-020 — Ice and water are assumed always available, not modeled as blocking inventory.**
*Rationale:* otherwise every tenant must check ice or nothing is makeable.

**ADR-021 — Onboarding wizard seeds initial inventory.**
*Rationale:* avoids the empty-inventory cold-start where "what can I make" returns nothing.

**ADR-022 — Curated lookup tables for GlassType / Method / Unit; no tenant additions in MVP.**
*Rationale:* consistent filtering; user-added values (e.g. "tiki mug") pinned for future.

**ADR-023 — Inventory is boolean (available / not), no "running low."**
*Rationale:* simplest model that serves the core loop. Multi-state pinned.

**ADR-024 — Household cocktails are private only in MVP.**
No publishing to a community pool yet. *Rationale:* the community database is the long-term
vision but adds visibility/moderation complexity; defer. Pinned.

**ADR-025 — Documentation set for solo+Claude Code: BRIEF, FEATURES, DATA_MODEL, DECISIONS,
CLAUDE.**
A full corporate PRD is overkill solo; PROJECT_BRIEF + FEATURES cover the useful PRD content.
*Rationale:* persistent, lean context for Claude Code over alignment ceremony.

---

> Tech-stack entries below: **2026-06-17** (tech-stack session).

**ADR-026 — Backend is ASP.NET Core Web API behind a clean API boundary.**
The frontend is a client of the API; no direct DB access from the UI. *Rationale:* developer's
strongest area; the API is the durable, client-agnostic asset that any future client (web,
mobile) reuses.

**ADR-027 — Web frontend is Blazor WebAssembly (not Blazor Server).**
*Rationale:* preserves the "frontend is just another API client" boundary central to the
architecture; 2026 Blazor improvements reduced WASM bundle size and improved AOT. Server would
couple UI to the server and hold a per-user live connection — rejected for that reason.

**ADR-028 — Blazor UI components live in a shared Razor Class Library (RCL).**
Not inline in the web app project. *Rationale:* the single low-cost rule that makes mobile-later
(MAUI Blazor Hybrid) a component-reuse exercise rather than a frontend rewrite. Cheap now,
expensive to retrofit.

**ADR-029 — Database is PostgreSQL.**
*Rationale:* free, portable, cheap to host; handles self-referencing categories, the substitution
graph, and sparse inventory easily. Chosen over SQL Server for economy/portability on a solo
project.

**ADR-030 — ORM is Entity Framework Core (Npgsql provider).**
*Rationale:* default .NET ORM; first-class Postgres support; maps DATA_MODEL.md to migrations
directly — the path from conceptual model to concrete schema.

**ADR-031 — Auth is ASP.NET Core Identity; Tenant scoping layered on top.**
*Rationale:* built-in user/auth; the household (tenant) model sits above Identity as a query
concern (each user belongs to a tenant).

**ADR-032 — Non-web clients (mobile + Win/macOS desktop) are MAUI Blazor Hybrid as the intended
direction, but DEFERRED (not built now).**
Strategy is "web first, other clients later." MAUI Blazor Hybrid is recorded as the planned path
for both mobile and Windows/macOS desktop because it reuses the Blazor UI (via the RCL), not just
the API. *Rationale:* MAUI had notable quality/stability debate through 2025–2026, so final
commitment is deferred until that work begins; by then there's more signal and, worst case, only
the frontend is affected since the API is client-agnostic. Alternatives to reconsider then: Uno
Platform, Avalonia, or a JS frontend (e.g. React Native) against the same API.

**ADR-033 — Hosting specifics deferred.**
*Rationale:* undemanding profile (.NET API + static WASM + Postgres); pick on budget near deploy.

**ADR-034 — Target the latest STABLE release, never previews; baseline is .NET 10 (LTS).**
"Use the absolute latest" is interpreted as latest *stable*. As of 2026-06-17 that is the .NET 10
line (.NET 10, ASP.NET Core 10, Blazor 10, EF Core 10.0.x, Npgsql provider 10.0.2, Identity 10).
*Rationale:* .NET 10 is simultaneously the newest stable release *and* LTS (supported to Nov
2028); .NET 11 exists only as preview until Nov 2026 and offers no benefit worth building on
shifting ground. Take minor/patch updates within the 10 line; reconsider .NET 11 only after its
stable GA, weighing LTS vs STS. PostgreSQL server version chosen at deploy (17 stable; 18 enables
native UUIDv7).

**ADR-035 — Multi-OS desktop (Windows + macOS) added as an intended future client; Linux out of
scope.**
Desktop is delivered via the same deferred MAUI Blazor Hybrid path as mobile, reusing the RCL —
purely additive, no new technology. *Rationale:* Windows + macOS is MAUI's first-class desktop
target, so it fits cleanly. Linux desktop is explicitly excluded; if it were ever required, that
would be the factor tilting the non-web-client framework toward Uno Platform or Avalonia instead
of MAUI. Reinforces ADR-028 (RCL discipline) — the RCL now protects three client types.

**ADR-036 — Process conventions: vertical slices, per-epic Gherkin user stories, Conventional
Commits, standard PR template.**
Work proceeds in vertical end-to-end slices that keep the app working; user stories live in
`docs/stories/` one file per epic with Gherkin (Given/When/Then) acceptance criteria; branches,
commits, and PR titles follow Conventional Commits; PRs use `.github/pull_request_template.md`.
Full detail in `docs/WAYS_OF_WORKING.md`. *Rationale:* a defined, lightweight process keeps solo
+ Claude Code work consistent and mergeable; "slices" were referenced across docs but previously
undefined — this pins them down.

**ADR-037 — App code name is JiggerJot; product/brand name kept separate.**
The solution and namespace root is **JiggerJot** (`JiggerJot.Api/.Core/.Infrastructure/.Web/
.Shared.Ui`, `JiggerJotDbContext`). The customer-facing brand name is deliberately decoupled and
may differ or change later, living only in UI strings/marketing — never in namespaces.
*Rationale:* the namespace root is expensive to change in .NET (solution, project names, root
namespaces, assemblies, EF context/migrations), so it's locked before scaffolding; the brand is
cheap to change and shouldn't be coupled to code. "JiggerJot" verified to have no existing
collision (jigger = a 1–2 oz bar measure; jot = to note/save), giving a clean repo/namespace/
domain. Casing is PascalCase compound: `JiggerJot`.
