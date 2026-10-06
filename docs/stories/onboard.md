# Stories — Onboarding (ONBOARD)

> One file per epic. The first minute: a brand-new household has an empty shelf, so "what can I make
> right now" would answer nothing. Read with **JJ-021** (the wizard seeds initial inventory) and
> **FEATURES.md §7**. Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — ONBOARD-1 shipped.

**Epic key:** `ONBOARD`

**Prerequisites:** `INV-3` (the reworked shelf, whose control this reuses) and `MARGA-3` (which
settled what a first bottle is). No new packages, no schema change, no migration.

**Depends on:** `INV`, `MAKE`, `MARGA`. **Depended on by:** nothing.

---

### ONBOARD-1 — The first minute

**Status: ✅ Implemented.** FEATURES §7. The last unbuilt flow, and the only one predating the UI wave.

**As a** household that has just signed up
**I want** to be walked through filling my shelf
**So that** the first thing the app shows me is drinks rather than nothing

**Context / notes.** It was scheduled last on purpose: a wizard whose job is landing someone on a
filled shelf should be built against the reworked shelf, and that shelf now exists. Two of the
decisions it needed had already been made by other slices.

#### It is the shelf, guided — not a second way to record what you own

Two steps over the same data and **the same control**. Step one is the short list worth asking about;
step two is everything else, for anyone who wants it. Someone who fills this in and opens the shelf
next week should find the screen they already know.

That is why INV-3's pill styles moved from `Shelf.razor.css` into the shared stylesheet. Scoped CSS is
per-component by design, so leaving them there would have meant a second copy — and two copies of a
control that is meant to be the same control is how they stop being the same.

#### "Common staples may be pre-suggested" — answered without a second curated list

FEATURES §7 asks for staples and does not say where they come from. The honest definition was already
settled by `MARGA-3`: **the bottles the most recipes ask for, minus what this household has.** So the
wizard asks `GET /api/cocktails/starters?limit=12` and puts the answer on screen already ticked.

No second list to curate, nothing to keep in step with the catalog by hand, and it grows by itself
when the full 969-recipe catalog is switched on. Twelve because past a dozen the step stops being a
shortcut and becomes the shelf again.

**Pre-ticked is the point.** Answering "which of these is wrong" is far faster than picking a dozen
bottles out of 191 — and it is also why nothing may be written before Finish.

#### Nothing is written until Finish, which is why the write is new

`INV-1`'s per-ingredient `PUT` stays exactly as it is: on the shelf screen a tick **is** the decision,
and it should be saved before someone looks away. The wizard is the opposite. Saving suggestions as
they appear would record a shelf the household never confirmed, and leave half of one behind for
anyone who closed the tab midway.

So `PUT /api/inventory` takes the whole shelf in one request, one transaction, all or nothing. Three
properties it needs and the single write does not:

- **The state is stated, never toggled**, so the result does not depend on what the shelf held when
  the request landed.
- **Safe to send twice** — a wizard finishing on a flaky connection is exactly what produces a retry.
- **An unknown id is reported, not fatal.** The catalog can change under a wizard that has been open a
  while, and losing eleven good ticks because the twelfth went stale is the wrong trade. That same
  visibility filter is the authorization check, so another household's custom ingredient lands in
  `unknown` like any unrecognised id.

**Every row is sent, not just the changes.** The wizard's answer is "this is my shelf", and unticking
something the household already owned has to reach the server as a `false` or it would silently stay
(JJ-023).

#### Offered, never forced

There is **no redirect and no dismissal flag**. The home screen's empty branch — which `MARGA-2` built
and `MARGA-3` styled — now leads with *Set up my shelf*, pointing here, and keeps the raw checklist one
tap further on for anyone who would rather get on with it. Skip writes nothing and goes to the catalog.

That is deliberate. A redirect that fires until you comply is a modal you cannot dismiss, and it would
need a "has this household been onboarded" fact that nothing in the schema records — `Tenant` belongs
to the platform, and adding a column to it to hold one bit of app state is the wrong direction
(golden rule 8). Members who join by invitation are handled for free: the household already has a
shelf, so the front page shows the count and never offers the wizard (FEATURES §7).

**Acceptance criteria**

```gherkin
Scenario: A new household is offered the guided route
  Given a household that has ticked nothing
  When I open the home page
  Then it leads with setting up my shelf, and the raw checklist is still reachable

Scenario: The suggestions arrive ticked
  Then the bottles the most recipes ask for are already selected
  And anything already on my shelf stays selected

Scenario: Nothing is written until I finish
  When I untick a suggestion and move to the next step
  Then the server has been told nothing

Scenario: Finishing writes the whole shelf at once
  Then one request carries every ingredient and the state it should be in
  And sending it twice leaves the same shelf

Scenario: I land on something to pour
  When I finish
  Then I am on what I can make, with the filter already applied

Scenario: I can leave
  When I skip
  Then nothing is written and I am in the catalog
```

**Out of scope, deliberately:** a stored "onboarded" flag (see above), pre-suggesting a full starter
*set* as a single choice rather than individual bottles, and re-running the wizard automatically for a
household that empties its shelf later.
