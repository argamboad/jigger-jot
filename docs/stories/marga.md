# Stories — Marga (MARGA)

> One file per epic. Marga is a **character in the UI**, not a feature: a drawn bartender who says a
> handful of **fixed lines** with **real data** dropped into them. She has no model behind her, makes
> no decisions, and never generates text. Every sentence she says is a localized resource string with
> placeholders, filled from queries that already exist or from `ALMOST-2`.
> Read with the design proposal (`JiggerJot Proposal.dc.html`, nine screens at three widths, both
> themes) and **JJ-029** (brand). Stories use Gherkin acceptance criteria.
> **Status: 📋 PLANNED.**

**Epic key:** `MARGA`

**Prerequisites:** `ALMOST-2` for any line that names a bottle to buy. Everything else she says is
already on the page. No new packages, no schema change, no migration — she is presentation.

**Depends on:** `ALMOST`, `MAKE`, `CKTL`. **Depended on by:** nothing; she is additive throughout.

---

## What she is, precisely

The proposal shows her speaking, and the temptation is to read that as an assistant. It is not one.

- **Her lines are fixed.** *"Twelve tonight. Pick up triple sec and I can make you four more."* is one
  resource string with two placeholders. The wording never varies; only the numbers and the names do.
- **The data is real.** The count comes from `GET /api/cocktails?makeable=true`, the substitution from
  the `substitutions` already on every makeable row, and the bottle from `ALMOST-2`.
- **She is not a new source of truth.** If her sentence and the list under it disagree, the bug is that
  they came from two queries. Every screen below takes one.
- **She is drawn once.** A single 1254×1254 illustration, cropped by each screen and shown uncropped
  only in the empty states. It ships at 2 MB from the design bundle and **must be optimized before
  it goes near the WASM payload** — that is part of MARGA-1, not an afterthought.

**Her Spanish is a writing job, not a translation.** Her lines are voiced, and a literal translation
of a voiced line reads like a machine. The placeholders have to survive being rewritten.

---

### MARGA-1 — The character

**Status: 📋 Planned.** Her asset, one shared component, and the three lines that need no new data.
Covers proposal screens **2**, **3** and **8**.

**As a** member of a household
**I want** the app to talk to me like the person behind a bar would
**So that** a substitution reads as advice rather than as a system caveat

**Scope.** Extract and optimize the illustration into `src/Shared.Ui/wwwroot/brand/`. Add one
component that renders her at a given size beside a line of text, so no screen re-spells the layout.
Then three copy replacements, each over data already on its page:

| Screen | Today | With her |
|---|---|---|
| Cocktails, makeable | `Cocktails_Using` in washed-out amber | the same swap, in her voice |
| Cocktail detail | the `(you'd pour X)` line-level marker | a card explaining why it qualified, marker kept |
| Login | `Login_Subtitle`, which describes the buttons | a line about the product |

**Out of scope:** anything that names a bottle to buy — that is `ALMOST-2` and lands in `MARGA-2`.

---

### MARGA-2 — The home screen

**Status: 📋 Planned.** Proposal screen **1**. Depends on `ALMOST-2` and `MARGA-1`.

**As a** member of a household
**I want** the front page to answer the question the app exists for
**So that** I do not have to go looking for it

`Home.razor` is 34 lines: an inherited platform welcome card, the lockup, a name, a tenant, and
`<!-- TODO: app-specific content goes here -->`. The signed-out half already works and is not touched.
The signed-in half becomes the count as a headline, Marga's line naming the one purchase that extends
it, three makeable drinks, and the one-bottle-away summary.

**The count in her sentence and the list beneath it come from one query**, or they drift the first
time the catalog changes.

---

### MARGA-3 — The two empty states

**Status: 📋 Planned.** Proposal screens **6** and **7**.

Today both state a problem and stop. `Make_NothingYet` reads as a broken catalog rather than an empty
shelf, and `Almost_NothingYet` gives a verdict with no next action. Both gain the uncropped
illustration, and `Make_FillYourShelf` is promoted from a text link to a primary button.

> **⚠️ Open question, and it is a real one.** Screen 7 offers a concrete first purchase to a household
> that is one bottle away from *nothing*. `ALMOST-2` cannot answer that: with an empty shelf there is
> no almost-makeable set to rank. Suggesting a first bottle means asking a different question —
> which ingredient appears in the most recipes outright — which is a second query and a second
> decision about what "best first bottle" means. **Settle this before building MARGA-3.**

---

## Acceptance criteria (all slices)

```gherkin
Scenario: Her line and the list agree
  Given she says a number
  Then that number and the list beneath it came from the same query

Scenario: She never invents
  Then every sentence she says is a resource string with placeholders
  And no sentence is assembled at runtime from fragments

Scenario: She speaks both languages
  Then every line has an English and a Spanish resource
  And the placeholders survive the rewrite

Scenario: She is optional furniture
  Given the data behind a line is unavailable
  Then the screen renders without her rather than with an empty speech line
```

---

## Out of scope for the epic, deliberately

A model behind her, generated text, per-household personalization, and any notion of her
"remembering" anything. She is a drawn character with fixed lines. Anything else is a different
product and is not in `PROJECT_BRIEF`.
