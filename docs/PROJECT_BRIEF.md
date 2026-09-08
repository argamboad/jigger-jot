# Project Brief — JiggerJot

> The lean PRD. Why this exists, what it is, what it is *not* (yet). The structure and the
> multi-tenant framing are constant (they come from perezosoft-platform); everything app-specific
> was settled in the 2026-06-17 conceptualization and the 2026-09-08 platform-alignment sessions.

> **Name.** **JiggerJot** is the code name and namespace root (JJ-027) and, for now, also the
> customer-facing brand (JJ-029). The two are deliberately decoupled in code — the brand lives only
> in UI strings, assets and marketing — so it can change later without renames. Renaming the
> platform's `Perezosoft.*` projects and namespaces to `JiggerJot.*` is part of the rebrand
> (`REBRANDING.md`). "jigger" = a bartender's 1–2 oz measure; "jot" = to note/save your own —
> measure + personalize.

## Elevator pitch
A cocktail app for home bartenders that answers one question better than anyone else:
**"What can I make right now, with what I actually have?"** It ships with a curated database of
cocktails and ingredients, but every household can extend it with their own ingredients and their
own cocktails, building a personal bar on top of a shared foundation.

## The core loop
1. The household tells the app which ingredients they have on hand (a simple checklist).
2. The app shows every cocktail they can make **right now** — factoring in ingredient
   substitutions — and every cocktail they're just one ingredient short of.
3. The household browses, filters, forks existing recipes, and authors their own.

Everything in the product serves that loop. If a feature doesn't make the core loop better, it's a
candidate for the OUT list below.

## Who it's for
- **Primary user:** home bartenders and cocktail enthusiasts.
- **Unit of account:** a **tenant** — here a **Household**: a kitchen/bar shared by one or more
  people (JJ-001). Inventory and custom recipes belong to the household, not the individual.
  Multiple users per tenant, via the platform's `TenantMembership`; the platform's reference
  implementation already labels the tenant "Household", so no relabel is needed.

## What makes it different
- **Availability-driven discovery.** Most apps show you recipes; this one shows you *your*
  recipes — the ones your current shelf supports.
- **Substitution-aware.** Missing Tia Maria but have Kahlúa? The drink still shows up.
- **Extensible, not fixed.** The shared catalog is a starting point, not a ceiling. Households fork
  and create freely.
- **"Almost there."** Surfaces the cocktails you're one ingredient away from — a genuine
  shopping-list / discovery driver.

## MVP scope — what's IN
- Multi-tenant foundation: tenants, users, tenant-scoped data, per-user preferences. *(constant)*
- Shared, pre-seeded catalog of ingredients and cocktails (read-only reference), seeded from the
  1930 Savoy Cocktail Book and other public-domain sources (`seed/`).
- Household-owned **ingredient inventory** with a simple available / not-available checklist.
- Custom ingredients (household-created), living alongside the shared catalog.
- Custom cocktails (household-created), including **"Create my own version"** (fork) of any shared
  cocktail — a fork is an independent snapshot copy.
- **Makeable engine:** list cocktails the household can make now, substitution-aware.
- **"Almost makeable":** list cocktails the household is exactly one required ingredient short of.
- Filtering: by ingredient/category (e.g. "everything with vodka"), by makeable-now, and other
  facets (method, glass, serving type).
- Two-level ingredient categorization (category + subcategory) driving filtering.
- Global ingredient substitutions (shared graph), applied in the makeable query.
- Recipe lines with structured quantities (amount + unit), required/optional flags, and a role
  (base / modifier / juice / garnish / …).
- Per-**user** unit-system preference (metric / imperial) with on-the-fly display conversion.
- **Onboarding wizard** to seed initial inventory so a new household isn't staring at an empty
  "what can I make" list.

## MVP scope — what's OUT (deferred / pinned)
- Non-web clients (mobile + desktop) — planned, not in MVP. *(constant)*
- **App signing, installers & store distribution — OUT for the platform, permanently (ADR-024).**
  Signed AAB/MSIX/IPA/pkg + store listings are per-app deliverables; each downstream app runs the
  first-native-release checklist (`NEW_APP_GUIDE.md` Phase 9). The platform proves capability via
  the CI build gate + boot smokes only. *(constant)*
- **Publishing household cocktails to a public/community pool** (community-extended database).
  The vision, but private-only for now (JJ-024).
- **Tenant-level (household-specific) substitutions.** Global-only for MVP (JJ-005).
- **Custom ingredients participating in the substitution graph** (e.g. "my homemade coffee liqueur
  subs for Kahlúa"). Custom ingredients satisfy recipe lines by exact match only (JJ-018).
- **Brand / product granularity** (e.g. "Tito's *is a* vodka"). Ingredients are generic (JJ-017).
- **Recipe-level substitutions** (a sub that applies only within one drink). Ingredient-level only
  (JJ-004).
- **Optional manual "primary classification"** for a cocktail (the editorial "this is a rum drink"
  label). Filtering is derived from recipe ingredients instead (JJ-014).
- **User-added controlled-list values** (custom glass types, methods, e.g. "tiki mug"). Curated
  lists only (JJ-022).
- **"Running low" / multi-state inventory.** Boolean available/not-available only (JJ-023).

## Guiding principles
- **Derived, not stored.** "Makeable" and "almost makeable" are always computed from inventory +
  recipes, never persisted as a flag — it would go stale instantly (JJ-003).
- **Tenant-scoped, not user-scoped.** Data belongs to the tenant; only preferences are per-user.
  *(constant)* For JiggerJot the single per-user preference is the unit system (JJ-008).
- **Clean API boundary.** UI is a client of the API; never hits the DB directly. *(constant)*
- **Shared catalog stays clean.** Households reference the shared catalog; forking creates an
  independent copy rather than mutating the shared record (JJ-002, JJ-013).
- **Lean MVP.** When in doubt, check the OUT list before building.
- **Platform wins.** JiggerJot is a downstream app of perezosoft-platform; when an app doc and the
  platform disagree, the platform wins and the app logs a JJ- decision (JJ-026, JJ-028).

## Related docs
- `FEATURES.md` — concrete user flows and behavior.
- `DATA_MODEL.md` — entities, relationships, derived rules.
- `TECH_STACK.md` — stack & architecture (constant).
- `DECISIONS.md` — ADR log: platform `ADR-…` and app `JJ-…` decisions.
- `../CLAUDE.md` — operating manual for Claude Code (repo root).
- `../brand/` — the approved brand assets (mark, lockups, icons, palette; JJ-029), staged for the
  rebrand. `../seed/` — seed-catalog extractions and scripts.
