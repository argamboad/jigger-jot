# Data Model

> The structural source of truth. Entities, fields, relationships, and — just as important —
> the **derived rules** that are computed rather than stored. Stack-agnostic; concrete SQL comes
> later in `SCHEMA.sql` once a database is chosen.

## Conventions

- `id` is the primary key on every entity unless noted.
- `tenant_id` nullable on shared-catalog entities: **null = global/shared seed**, **set =
  household-owned**. This is the single-table pattern (see DECISIONS.md).
- "Household" and "tenant" are the same thing. The code term is **tenant**.
- Timestamps (`created_at`, `updated_at`) assumed on all entities; omitted below for brevity.

## Entities

### Tenant
The household. Owns inventory and all custom content.
- `id`
- `name`

### User
A person belonging to a tenant. Many users → one tenant.
- `id`
- `tenant_id` (required — every user belongs to a household)
- `email` / auth fields (TBD with stack)
- `preferred_unit_system` — enum: `metric` | `imperial`. **The only per-user preference.**

### Ingredient
Single table for both shared and custom ingredients.
- `id`
- `tenant_id` — **null = shared catalog**, set = household-custom.
- `name`
- `category_id` → IngredientCategory (the top level, e.g. Rum)
- `subcategory_id` → IngredientCategory (nullable; the child level, e.g. Dark Rum)

> Custom ingredients (tenant_id set) are private to that tenant. They satisfy recipe lines by
> **exact match only** in MVP — they do not participate in the substitution graph.

### IngredientCategory
Two-level categorization (category + subcategory) that drives filtering.
- `id`
- `name`
- `parent_id` — nullable; null = top-level category, set = subcategory under that parent.

> Modeled as a self-referencing table to keep two levels clean and extensible. Filtering "all
> rum" matches the parent and all its children; "dark rum" matches the child. Filtering also
> works by ingredient **name** as well as by category — both are supported (e.g. searching
> "vodka" matches category Vodka *and* name matches).

### Cocktail
Single table for both shared and custom cocktails.
- `id`
- `tenant_id` — **null = shared catalog**, set = household-custom.
- `forked_from_cocktail_id` — nullable; provenance only. Set when this cocktail was created via
  "Create my own version" of another. **A fork is a full snapshot copy** — changes to the
  original never propagate.
- `name`
- `glass_type_id` → GlassType
- `method_id` → Method
- `serving_type` — enum: `shot` | `full_drink`
- `instructions` — free text (preparation steps / notes)

> No `base_category` / "main spirit" field. A cocktail's spirit(s) are derived from its recipe
> lines + ingredient categories. (Manual primary classification is a pinned future feature.)

### CocktailIngredient  *(the recipe line — the heart of the model)*
Links a cocktail to one ingredient with how it's used.
- `id`
- `cocktail_id` → Cocktail
- `ingredient_id` → Ingredient
- `amount` — numeric, nullable (nullable for "to taste" / garnishes)
- `unit_id` → Unit (nullable, pairs with amount)
- `is_required` — boolean. **Drives the makeable calculation.** Garnishes are simply
  `is_required = false`.
- `role` — enum: `base` | `modifier` | `juice` | `syrup` | `bitters` | `garnish` | `mixer` |
  `other` (used for display grouping; final list TBD when seeding).
- `display_order` — integer, for stable recipe ordering.
- `notes` — nullable free text (e.g. "freshly squeezed").

> The same ingredient may appear on multiple lines of one cocktail — **not constrained to
> unique**.

### IngredientSubstitution
Global-only substitution graph (MVP). Both substituted ingredients must be **shared** (tenant_id
null) ingredients.
- `id`
- `ingredient_id` → Ingredient (the one a recipe calls for)
- `substitute_ingredient_id` → Ingredient (what can stand in)
- Symmetry handled by storing **both directions** as separate rows (simpler queries than a
  symmetric flag).

### TenantInventory
The availability checklist. Sparse — a row exists only for ingredients the tenant has marked.
- `id`
- `tenant_id` → Tenant
- `ingredient_id` → Ingredient (shared or this tenant's custom)
- `is_available` — boolean. (Absence of a row = not available.)

### GlassType / Method / Unit
Curated lookup tables (no tenant additions in MVP).
- **GlassType:** `id`, `name` (coupe, rocks, highball, …)
- **Method:** `id`, `name` (shake, stir, build, blend, muddle, …)
- **Unit:** `id`, `name`, `system` (`metric` | `imperial` | `neutral`), conversion metadata.
  - Convertible units (oz ↔ ml) carry conversion factors.
  - **Neutral / non-convertible** units (dash, barspoon, piece, leaves, to-taste) display as
    authored regardless of user preference.

## Relationship summary

- Tenant 1 — N User
- Tenant 1 — N Ingredient (custom) / 1 — N Cocktail (custom) / 1 — N TenantInventory
- Cocktail 1 — N CocktailIngredient (recipe lines)
- Ingredient 1 — N CocktailIngredient
- Ingredient N — N Ingredient via IngredientSubstitution (both directions stored)
- IngredientCategory self-referencing (parent / subcategory)
- Cocktail → GlassType, Method; CocktailIngredient → Unit; Ingredient → IngredientCategory ×2

## Derived rules (computed, never stored)

### Makeable
A cocktail is **makeable** for a tenant when **every `is_required` recipe line is satisfied**.
A required line is satisfied when the tenant has, marked available, either:
- the line's exact ingredient, **or**
- any valid substitute of it (via IngredientSubstitution; shared ingredients only).

Optional lines (`is_required = false`, e.g. garnishes) never block makeability.

### Almost makeable
A cocktail is **almost makeable** when **exactly one** required line is unsatisfied (after
applying substitutions). Surface the missing ingredient(s) as a discovery / shopping driver.
(Generalizable to "N short," but MVP fixes N = 1.)

### Ice & water
Ubiquitous staples (ice, water) are **assumed always available** and are **not modeled as
blocking inventory**. Either omit them from required lines or treat them as always-satisfied.

### Unit display conversion
Stored **as authored** (2 oz stays "2 oz"). At display time, convert convertible units to the
viewing user's `preferred_unit_system`. Neutral/non-convertible units pass through unchanged.

## Pinned model extensions (future, not built)

- Tenant-level substitutions (add `tenant_id` to IngredientSubstitution).
- Custom ingredients in the substitution graph / "treat as equivalent to" a shared ingredient.
- Brand/product granularity (a "specific product *is a* generic ingredient" hierarchy).
- Recipe-level substitutions (accepted subs enumerated per recipe line).
- Publishing custom cocktails to a public pool (visibility/moderation fields on Cocktail).
- Manual "primary classification" on Cocktail.
- User-added lookup values (tenant_id on GlassType / Method / Unit).
- Multi-state inventory ("running low").
