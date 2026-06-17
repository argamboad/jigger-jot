# Ways of Working

> The process layer: how work is sliced, how user stories and PRs are written, and naming
> conventions. Referenced by `CLAUDE.md` so Claude Code follows it.

## Slices (the unit of build work)

A **slice** is a thin, vertical, end-to-end increment that delivers one coherent piece of user
value and leaves the app working. Vertical means it cuts through all layers as needed (API →
Core/derived rules → Infrastructure/EF → Shared.Ui components → Web), not one horizontal layer in
isolation.

**Principles**
- **Vertical, not horizontal.** Prefer "user can toggle an ingredient's availability" (DB + API +
  UI) over "build all the tables."
- **Small enough to finish.** Completable and mergeable on its own. If it can't be stated in a
  sentence or two, split it.
- **Working state after each slice.** Every merged slice keeps the app runnable.
- **Foundational slices first** (auth + tenant scaffolding, data model + first migration), still
  vertical where possible.
- **One epic = a group of related slices.** Stories written per-epic at the start of that epic.

**Likely epic order** (refine in `ROADMAP.md`):
auth + tenant → ingredient catalog + categories → inventory (availability toggle) → cocktail
catalog + recipe lines → **makeable engine** → almost-makeable → fork ("create my own version")
→ custom cocktail authoring → onboarding wizard → filtering/browse polish.

**Slice lifecycle**
1. Pick the next slice from `ROADMAP.md`.
2. Write/refine the story/stories under `docs/stories/`.
3. Branch, build, keep `Core` derived-rule logic (makeable, almost-makeable, unit conversion)
   unit-tested.
4. Open a PR using the template; self-review against acceptance criteria.
5. Merge; app stays working. Log ADRs in `DECISIONS.md` for any decisions.

## User stories

Stories live in `docs/stories/`, **one file per epic** (e.g. `docs/stories/inventory.md`). They
translate `FEATURES.md` behavior into intent + testable acceptance criteria. **Acceptance
criteria use Gherkin (Given/When/Then).**

### Story template

```markdown
### <STORY-ID> — <short title>

**As a** <household member>
**I want** <capability>
**So that** <benefit>

**Context / notes:** <links to FEATURES.md flow, DATA_MODEL rule, constraints>

**Acceptance criteria**

Scenario: <name>
  Given <context>
  When <action>
  Then <outcome>

Scenario: <edge / unhappy path>
  Given ...
  When ...
  Then ...

**Out of scope:** <what this story does NOT cover>
**Definition of done:** criteria pass, Core logic unit-tested, tenant-scoping verified, merged,
app still working.
```

### Story ID & naming
- **Story ID:** `<EPIC>-<n>` with a short uppercase epic key. Cocktail epic keys:
  `AUTH`, `TENANT`, `INGREDIENT`, `INVENTORY` (`INV`), `COCKTAIL` (`CKTL`), `MAKEABLE` (`MAKE`),
  `FORK`, `AUTHORING`, `ONBOARD`, `FILTER`. e.g. `INV-3`, `MAKE-1`.
- **Story file:** `docs/stories/<epic-lower>.md` (e.g. `docs/stories/makeable.md`).
- Register the epic key(s) at the top of each story file.

## Branches, commits, PRs

### Conventional Commits
Commits and PR titles follow **Conventional Commits**:
```
<type>(<scope>): <description>
```
- **Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`, `build`, `ci`.
- **Scope:** the area/epic, e.g. `feat(inventory): ...`, `fix(makeable): ...`.
- **Description:** imperative, lowercase, no trailing period. e.g.
  `feat(makeable): factor substitutions into makeable query`.
- Breaking changes: `feat(api)!: ...` + explain in body.

### Branch naming
```
<type>/<epic-or-scope>-<short-desc>
```
e.g. `feat/inventory-availability-toggle`, `fix/makeable-substitution-match`. Optionally include
the story ID: `feat/MAKE-1-substitution-aware-query`.

### PR naming
PR title = a Conventional Commit line referencing the story:
`feat(makeable): substitution-aware makeable query (MAKE-1)`.

### PR template
Stored at `.github/pull_request_template.md` (auto-loaded by GitHub).

## How Claude Code should use this
- Default to vertical slices; propose a split rather than building multi-epic chunks at once.
- Write the per-epic story file before starting an epic; use the story + Gherkin as the spec.
- Name branches, commits, and PRs per the conventions above.
- Fill the PR template; check every box honestly or note why N/A.
- Especially: keep the **makeable / almost-makeable / unit-conversion** logic in `Core` and
  unit-tested — these are the domain's derived rules.
