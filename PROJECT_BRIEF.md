# Project Brief — JiggerJot

> The lean PRD. Why this exists, what it is, and — most importantly — what it is *not* (yet).

> **Name:** **JiggerJot** is the code name and namespace root (solution `JiggerJot.sln`; projects
> `JiggerJot.Api`, `JiggerJot.Core`, `JiggerJot.Infrastructure`, `JiggerJot.Web`,
> `JiggerJot.Shared.Ui`; EF context `JiggerJotDbContext`). The customer-facing **product/brand
> name is intentionally kept separate** and may differ or change later — it lives only in UI
> strings, marketing, and this doc, never in namespaces. So the brand can evolve without code
> renames. ("jigger" = a bartender's 1–2 oz measure; "jot" = to note/save your own — measure +
> personalize.)

## Elevator pitch

A cocktail app for home bartenders that answers one question better than anyone else:
**"What can I make right now, with what I actually have?"**

It ships with a curated database of cocktails and ingredients, but every user (household) can
extend it with their own ingredients and their own cocktails, building a personal bar on top of
a shared foundation.

## The core loop

1. The household tells the app which ingredients they have on hand (a simple checklist).
2. The app shows every cocktail they can make **right now** — factoring in ingredient
   substitutions — and every cocktail they're just one ingredient short of.
3. The household browses, filters, forks existing recipes, and authors their own.

Everything in the product serves that loop. If a feature doesn't make the core loop better,
it's a candidate for the "out of scope" list below.

## Who it's for

- **Primary:** home bartenders and cocktail enthusiasts.
- **Unit of account:** a **household** (a "tenant") — a kitchen/bar shared by one or more people.
  Inventory and custom recipes belong to the household, not the individual. Multiple users can
  belong to one household and share its bar.

## What makes it different

- **Availability-driven discovery.** Most apps show you recipes; this one shows you *your*
  recipes — the ones your current shelf supports.
- **Substitution-aware.** Missing Tia Maria but have Kahlúa? The drink still shows up.
- **Extensible, not fixed.** The shared catalog is a starting point, not a ceiling. Households
  fork and create freely.
- **"Almost there."** Surfaces the cocktails you're one ingredient away from — a genuine
  shopping-list / discovery driver.

## MVP scope — what's IN

- Shared, pre-seeded catalog of ingredients and cocktails (read-only reference).
- Household-owned **ingredient inventory** with a simple available / not-available checklist.
- Custom ingredients (household-created), living alongside the shared catalog.
- Custom cocktails (household-created), including **"Create my own version"** (fork) of any
  shared cocktail — a fork is an independent snapshot copy.
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

These are deliberately parked. They are good ideas; they are not MVP. Each is recorded so we
don't re-debate whether they were forgotten — they weren't.

- **Publishing household cocktails to a public/community pool** (community-extended database).
  The vision, but private-only for now.
- **Tenant-level (household-specific) substitutions.** Global-only for MVP.
- **Custom ingredients participating in the substitution graph** (e.g. "my homemade coffee
  liqueur subs for Kahlúa"). Custom ingredients satisfy recipe lines by exact match only.
- **Brand / product granularity** (e.g. "Tito's *is a* vodka"). Ingredients are generic in MVP.
- **Recipe-level substitutions** (a sub that applies only within one drink). Ingredient-level only.
- **Optional manual "primary classification"** for a cocktail (the editorial "this is a rum
  drink" label). Filtering is derived from recipe ingredients instead.
- **User-added controlled-list values** (custom glass types, methods, e.g. "tiki mug"). Curated
  lists only in MVP.
- **"Running low" / multi-state inventory.** Boolean available/not-available only.

## Guiding principles

- **Derived, not stored.** "Makeable" and "almost makeable" are always computed from inventory +
  recipes. Never persisted as a flag — it would go stale instantly.
- **Household-scoped, not user-scoped.** Inventory and custom content belong to the household.
  Only unit preference is per-user.
- **Shared catalog stays clean.** Households reference the shared catalog; forking creates an
  independent copy rather than mutating the shared record.
- **Lean MVP.** When in doubt, check the "OUT" list before building.

## Related docs

- `FEATURES.md` — concrete user flows and behavior.
- `DATA_MODEL.md` — entities, relationships, and the derived-rule definitions.
- `DECISIONS.md` — why each major choice was made.
- `../CLAUDE.md` — operating manual for Claude Code (lives at repo root).
