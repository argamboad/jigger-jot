# Stories — Marga (MARGA)

> One file per epic. Marga is a **character in the UI**, not a feature: a drawn bartender who says a
> handful of **fixed lines** with **real data** dropped into them. She has no model behind her, makes
> no decisions, and never generates text. Every sentence she says is a localized resource string with
> placeholders, filled from queries that already exist or from `ALMOST-2`.
> Read with the design proposal (`JiggerJot Proposal.dc.html`, nine screens at three widths, both
> themes) and **JJ-029** (brand). Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — MARGA-1, MARGA-2 and MARGA-3 all shipped.

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

**Status: ✅ Implemented.** Proposal screens **6** and **7**.

**As a** household with nothing on its shelf
**I want** the empty screens to tell me what to do next
**So that** the app's first impression is a starting point rather than a verdict

Both states used to state a problem and stop. `Make_NothingYet` read as a broken catalog rather than
an empty shelf, and `Almost_NothingYet` gave a verdict with no next action. Both now carry the
uncropped illustration, and `Make_FillYourShelf` is a primary button rather than a text link.

**The two states get different next actions, and that is the design.** Nothing makeable means the
action is *tell me what you have* — with nothing ticked, sending someone shopping is premature.
Nothing one bottle away is the stronger signal, and the only state where naming a purchase is honest.

#### The open question, settled

`ALMOST-2` cannot answer "what should I buy first?" for an empty shelf: it ranks the
almost-makeable set, and a household that owns nothing is not one bottle away from anything, so the
set is empty and there is nothing to rank. Its own test asserts exactly that and points here.

Three readings were on the table:

| Reading | Verdict |
|---|---|
| The bottle the most recipes **ask for** | **chosen** |
| The bottle that would make the most drinks makeable **on its own** | useless — one bottle alone makes very nearly nothing, so every candidate scores nought or one |
| A **starter set** — "these four get you eleven drinks" | better advice, but that is the onboarding wizard (`ONBOARD-1`), not an empty state |

So `GET /api/cocktails/starters` counts, per ingredient, how many cocktails this household can see
have a **required** line naming it, drops what the household already has, and ranks. Three decisions
inside that, each of which could have gone the other way:

- **Required lines only** (JJ-009). An optional line never blocks a drink, so an ingredient that only
  ever garnishes is not one the catalog leans on — counting it would send someone out for a lemon twist.
- **Substitutions ignored.** A first bottle should be the one the recipes name, and with an empty
  shelf there is nothing to substitute *from*, which is the case this exists for.
- **What the household already has is removed**, so the answer stays useful as the shelf fills rather
  than only on the first day.

**The honesty constraint falls out of the choice.** `appears` is how many recipes ASK for the bottle,
not how many it would unlock, so her line says exactly that. `ALMOST-2`'s card may promise drinks
because it measured them; this one may not, and reading `appears` as "drinks you could make" is the
single way this endpoint could mislead.

> **Found in the browser, not by a test.** With the one-away filter on *and a search typed*, an empty
> list means the **search** found nothing — and answering that with "starting from nothing? get gin"
> tells someone who may own forty bottles to go shopping. The one-away filter now has to be the only
> thing narrowing the list before its emptiness is allowed to say anything about the shelf.

**Tested as components, not as a journey.** The state itself is a cold start: an empty one-away list
means the catalog holds no recipe within one bottle of this household, and the development database
cannot produce that. It was seeded before the shipped catalog was cut to its starter set and still
holds single-ingredient recipes, so an empty shelf there is one bottle away from fourteen drinks.
`tests/Ui.Tests/CocktailsEmptyStateTests.cs` renders the real page against stubbed responses instead.

**She is the illustration here, not beside it.** The empty states show the 512px scene and state her
line beneath it, rather than nesting `MargaSays` — which would put her face on the screen twice.

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
