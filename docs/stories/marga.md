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

### MARGA-4 — Her in the emails

**Status: ✅ Implemented.** Not from the design proposal — asked for directly, after the app shipped
and she turned out to be scarcer in practice than on paper.

**As a** person who gets an email from this app
**I want** it to sound like the app it came from
**So that** a sign-in code does not read like it was sent by a different product

**She goes on the three emails a person ASKED for**, and the decision worth recording is the fourth
one she does not go on:

| Email | Her? |
|---|---|
| Sign-in code (OTP) | yes — the line she already says on the login page |
| Sign-in link (magic link) | yes — the same line |
| Household invitation | yes — welcoming someone in |
| **Notification** | **no** |

That last template wraps **arbitrary system messages**, including "your subscription is past due" and
a security alert. A bartender character on those undercuts the message, and the person reading it is
not in the mood. It is a judgement rather than a rule, so it is held by a test instead of a comment.

**She is attached, not merely hidden.** The image rides along as a CID inline attachment — the one
approach Gmail and Outlook both render, since both block data-URIs — and only the emails that show
her carry it. Her 19 KB does not travel on every notification for nothing.

**Her avatar is a COPY, not a reference.** `Infrastructure` does not depend on `Shared.Ui` and must
not start; `REBRANDING.md` already treats the email assets as their own set, and now names both.

**Same contract as in the app.** `MargaSays(...)` takes a finished sentence — it never builds, picks
or fetches one — and every line is one whole resource string from `EmailStrings.resx`. If a key ever
goes missing the resolver echoes the key, which would ship `Marga_SignIn` to a real inbox, so a test
fails on that instead.

**Her avatar is decorative in email too** (`alt=""`), which matters more here than in the app: most
clients block images by default, so the common case is the sentence without her. It has to read
correctly on its own, and "photo of a bartender" in front of it would only get in the way.

**Verified in a real client path**, not as a string: sent through the running API into Mailpit in
both languages, confirming two inline images, her line above the code, and the Spanish reading as
written Spanish. Mailpit's compatibility check adds no new warnings — every CSS property in her block
was already used elsewhere in the template, except the avatar's `border-radius`, which degrades to a
square in Outlook and is fine.

**Acceptance criteria**

```gherkin
Scenario: The emails someone asked for sound like the app
  Given a sign-in code, a sign-in link or an invitation
  Then Marga says one line above the thing I came for
  And the email carries her image alongside the logo

Scenario: She stays off the bad news
  Given a system notification — a failed payment, a security alert
  Then she is not there
  And her image is not attached either

Scenario: An image-blocking client loses only her
  Given images are blocked
  Then her sentence still reads, with no alt text in front of it

Scenario: She speaks both languages
  Then her email lines have an English and a Spanish resource
  And the Spanish is written rather than translated
```

---

### MARGA-5 — Where she actually is

**Status: ✅ Implemented.** Not from the design proposal either — asked for after the wave shipped,
because on paper she was on seven surfaces and in practice you met her once.

**As a** member of a household
**I want** her where I spend my time, not only on the front page
**So that** the app keeps its voice past the first screen

**The measurement that prompted it.** Of her seven surfaces, **four were conditional** (a substituted
row, a substituted recipe, two empty states) and **one is a flash** (the boot screen). So a household
with a filled shelf met her on the home page and then never again — and the **shelf**, the screen with
the most dwell time in the whole app, had no Marga at all.

#### The rule, because without one she becomes wallpaper

**She speaks where a number needs interpreting, and stays quiet where the screen already says it
plainly. One page-level Marga per screen.** A per-row aside at 24px is a footnote on that row, not the
page's voice, and does not count against it.

| Screen | What she says | Why there |
|---|---|---|
| **Shelf** (new) | what the shelf is one bottle short of | most dwell time, and she was absent; the footer already counts what you HAVE, so she takes the other half |
| **Catalog, makeable filter** (new) | the count | under that filter the count IS the product's question answered, so she gives it — the one place she REPLACES a number |
| **One-away list** | the unlocking bottle | the card was already hers in shape and colour and simply had nobody in it |
| **Recipe page** (widened) | "you can pour this now", or the one bottle missing | she used to appear here ONLY for a substitution, so she only ever turned up to explain a compromise, never to say yes |
| **Home** | unchanged, at 76px rather than 56 | the first sentence anyone reads; at 56 she was an icon beside it rather than the one saying it |

**Where she deliberately does not go:** a plain catalog browse ("969 cocktails" is a fact about the
list, and a character who narrates every number stops being worth reading), a drink more than one
bottle away (the badge has said so; piling on is not her job), the authoring form, and every platform
screen.

**No new engine work.** Every line is fixed copy over data the page already had, or over `unlocks` and
`starters`, both of which already existed.

> **Two bugs it introduced, both caught by tests rather than by looking.**
>
> Her shelf fetch joined the payoff footer's and **shared its catch**, so failing to get her line
> blanked the drinks count too. An existing INV-3 test went red immediately. The footer states a fact
> the screen owns; she is optional furniture; the two must fail apart, and now do.
>
> Then, once separated, a failed fetch left her saying **"that is everything your shelf reaches"** —
> a claim she had nothing behind, because "nothing is within one bottle" and "I could not find out"
> were both just `null`. She now tracks whether the answer actually came back and says nothing when
> it did not.

**The test id follows the number.** `cocktail-count` stays on whichever element carries the count —
her sentence under the makeable filter, the plain line otherwise — because a journey reads it to check
the home screen and the list agree. SHELL-1 moved information without moving its id and took three
suites down; that is not repeated here.

**Acceptance criteria**

```gherkin
Scenario: She is on the screen I spend the most time on
  Given a shelf with something ticked
  Then she names the one bottle that would open the most
  And she does not repeat the bottle count the footer already shows

Scenario: An untouched shelf gets a starting point
  Given nothing ticked, so nothing is one bottle away from anything
  Then she suggests where to start instead

Scenario: She says nothing rather than something empty
  Given her data cannot be fetched
  Then she is absent, and the footer's count is unaffected

Scenario: She takes the count only where the count is the answer
  Given the makeable filter
  Then she gives the number
  But given a plain browse
  Then the number is plain text and she is not there

Scenario: She can say yes
  Given a recipe I can pour right now
  Then she says so — not only when a substitution needs explaining
```

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
