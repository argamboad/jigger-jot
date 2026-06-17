# Features & User Flows

> How the product behaves, flow by flow. The "why/what" lives in `PROJECT_BRIEF.md`; the
> structures these flows touch live in `DATA_MODEL.md`. This doc is the behavior spec.

## 1. Onboarding wizard (new household)

**Goal:** a brand-new tenant has an empty inventory, so "what can I make?" would return nothing.
The wizard seeds initial inventory and avoids a dead first impression.

Flow:
1. New household is created (with its first user).
2. Wizard presents the shared ingredient catalog, organized by category, optimized for fast
   bulk-checking ("check what you have on your shelf").
3. Common staples may be pre-suggested to speed things up.
4. On finish, checked ingredients become available rows in TenantInventory.
5. User lands on "What can I make right now" — ideally already populated.

Notes: ice/water are assumed available and not part of the checklist (see DATA_MODEL derived
rules).

## 2. Managing inventory

**Goal:** keep the household's available-ingredients list current.

Flow:
- A checklist of ingredients (shared catalog + this tenant's custom), grouped by category.
- Toggling an item writes/updates its `is_available` in TenantInventory; absence = not available.
- Add a **custom ingredient** inline (name + category + subcategory) → creates a tenant-owned
  Ingredient and is immediately checkable.
- Boolean only — no "running low" (pinned).

## 3. "What can I make right now" (the makeable engine)

**Goal:** the headline feature.

Behavior:
- Lists every cocktail (shared + custom) that is **makeable** for this tenant per the
  DATA_MODEL makeable rule (all required lines satisfied by available ingredients or their valid
  substitutes).
- Optional lines (garnishes) never block a result.
- When a result is shown thanks to a substitution, surface that ("using Kahlúa in place of Tia
  Maria") so the user understands why it qualified and what they'd actually pour.

## 4. "Almost makeable"

**Goal:** discovery + shopping driver.

Behavior:
- Lists cocktails where **exactly one** required line is unsatisfied (after substitutions).
- Each result names the single missing ingredient — the "buy this, unlock these drinks" hook.
- Presented as its own view or section adjacent to "what can I make."

## 5. Browsing & filtering

**Goal:** explore the whole catalog, not just what's makeable.

Filters (combinable):
- **Makeable now** (on/off) — the toggle between "everything" and "what I can make."
- **By ingredient / category** — e.g. "everything with vodka." Matches by category (parent
  catches all children: "rum" → white + dark + spiced) **and** by ingredient name. Derived from
  recipe lines; no manual tagging.
- **By method** (shake / stir / …), **glass type**, **serving type** (shot / full drink).
- Results draw from shared + custom cocktails together.

## 6. Viewing a cocktail

Shows: name, recipe lines (ingredient, amount, unit, role grouping, notes), method, glass,
serving type, instructions. Amounts display in the **viewing user's** unit preference
(convertible units converted; neutral units as-authored). Indicates makeable / almost-makeable
status and any substitution in play.

## 7. "Create my own version" (fork)

**Goal:** let a household adapt a shared (or any) cocktail.

Flow:
1. From any cocktail, user hits **Create my own version**.
2. System creates a **full snapshot copy**: a new tenant-owned Cocktail + copies of all its
   recipe lines, with `forked_from_cocktail_id` set to the original (provenance only).
3. The copy is fully independent and editable; later edits to the original never propagate.
4. The fork appears among the household's custom cocktails.

## 8. Authoring a custom cocktail (from scratch)

Flow:
- Create a tenant-owned Cocktail: name, method, glass, serving type, instructions.
- Add recipe lines: pick ingredient (shared or custom), amount + unit, required/optional, role,
  order, notes.
- Same ingredient may appear on multiple lines.
- Immediately participates in makeable / filtering like any other cocktail.

## 9. Unit preference

- Per-user setting: metric or imperial.
- Affects **display only**; storage is always as-authored.
- Two users in the same household may view the same recipe in different units.

## Flow-to-rule cross-reference

| Flow | Key derived rule (see DATA_MODEL) |
|------|-----------------------------------|
| What can I make | Makeable |
| Almost makeable | Almost makeable (N = 1) |
| Viewing / filtering by spirit | Spirit derived from recipe ingredients + categories |
| Any amount displayed | Unit display conversion |
| Onboarding / inventory | Ice & water assumed available |

## Out of scope (see PROJECT_BRIEF "OUT" list)

Publishing to community, tenant-level subs, custom ingredients in sub graph, brand granularity,
recipe-level subs, manual primary classification, user-added lookup values, "running low."
