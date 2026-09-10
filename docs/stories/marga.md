# Stories — Marga (MARGA)

> One file per epic. Marga is a **character in the UI**, not a feature: a drawn bartender who says a
> handful of **fixed lines** with **real data** dropped into them. She has no model behind her, makes
> no decisions, and never generates text. Every sentence she says is a localized resource string with
> placeholders, filled from queries that already exist or from `ALMOST-2`.
> Read with the design proposal (`JiggerJot Proposal.dc.html`, nine screens at three widths, both
> themes) and **JJ-029** (brand). Stories use Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — MARGA-1 and MARGA-2 shipped; MARGA-3 planned.

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
- **She is drawn once.** A single 1254×1254 illustration at 2 MB in the design bundle, now shipped as
  two optimized assets. Flat colour with a limited palette, so quantizing costs nothing visible:

  | Asset | Use | Size |
  |---|---|---|
  | `marga_avatar_160.png` | head and shoulders, for the inline component | 19 KB |
  | `marga_scene_512.png` | the whole scene, for the empty states and `SHELL-2` | 115 KB |

  Together that is **7% of what the bundle delivered**. The source PNG is deliberately not committed:
  these are delivered brand assets like the lockups, not generated ones, and a 2 MB original in git
  buys nothing.

**Her Spanish is a writing job, not a translation.** Her lines are voiced, and a literal translation
of a voiced line reads like a machine. The placeholders have to survive being rewritten.

---

### MARGA-1 — The character

**Status: ✅ Implemented.** Her asset, one shared component, and the three lines that need no new
data. Covers proposal screens **2**, **3** and **8**.

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

**What her component does and does not do.** It takes a *finished* sentence and renders her beside
it at a given size. It never builds a sentence, never picks a string and never fetches anything — the
screen decides what she says. That is what keeps "she is not an assistant" true in code rather than
only in this file.

**She is decorative to a screen reader.** `alt=""` and `aria-hidden`, because the line beside her
carries the whole meaning and "photo of a bartender" ahead of it would only get in the way.

**Out of scope:** anything that names a bottle to buy — that is `ALMOST-2` and lands in `MARGA-2`.

**Tests.** Covered through the existing substitution journey in
`tests/E2E.Tests/MakeableJourneyTests.cs`, which now also asserts she is the one saying it in the
list and that her card and the per-line marker agree on the recipe page. No new unit tests: this
slice adds no logic, and asserting that a component renders a string it was handed would test Blazor
rather than JiggerJot.

---

### MARGA-2 — The home screen

**Status: ✅ Implemented.** Proposal screen **1**. Needed `ALMOST-2` and `MARGA-1`, both merged first.

**As a** member of a household
**I want** the front page to answer the question the app exists for
**So that** I do not have to go looking for it

`Home.razor` is 34 lines: an inherited platform welcome card, the lockup, a name, a tenant, and
`<!-- TODO: app-specific content goes here -->`. The signed-out half already works and is not touched.
The signed-in half becomes the count as a headline, Marga's line naming the one purchase that extends
it, three makeable drinks, and the one-bottle-away summary.

**Two calls, each internally consistent** — that is how the one-query rule actually lands here. The
headline count and the three drinks under it come from one response, so they can never disagree; the
bottle and the drinks it opens come from another, where the count is the length of the names by
construction (`ALMOST-2`). Her sentence quotes one number from each, and **each half agrees with the
list beneath it**.

**Her line is picked, never assembled.** Three whole resource strings — one for an empty shelf, one
when a bottle is worth naming, one when nothing is close — and the screen chooses which. No sentence
is built from fragments at runtime.

**The buttons are deep-linked.** `/cocktails?makeable=true` and `?almost=true` pre-set the toggles,
so "show me them" lands on the list that produced the number it quotes. Without that, the front page
would hand someone a number and then make them find the filter behind it. That query-string read is a
small addition to `Cocktails.razor` beyond the literal story, and it is the reason the button is
honest.

**The signed-out half is untouched.** It already had a hero and a working sign-in call to action.

> **Found while building, fixed here.** The unlocks card named **thirteen** drinks on a real shelf,
> which is a wall rather than a list. The DISPLAY now stops at four and counts the rest — "and 9
> more" — in both places that show it. The DATA stays whole: `unlocks` is the length of the names, so
> truncating those would break the number shown beside them.

**Tests.** One journey in `tests/E2E.Tests/MakeableJourneyTests.cs` (suite 49 → 50): an empty shelf
gets her pointing at the shelf rather than a count of nothing; a stocked one gets the headline; and
both buttons land on the filter that produced the number they quote, with the headline compared
against what that filter reports.

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
