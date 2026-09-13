# Stories — Back bar (BACKBAR)

> One file per epic. The restyle: every screen of the app redrawn to design direction 1A, "Back
> bar" — dark as the primary theme with a light counterpart, a display serif, hairline surfaces, and
> Marga given room. **Read with the handoff, `docs/design/2026-09-backbar-handoff.pdf`** (19 pages;
> page numbers below are the document's own), and with **JJ-036 → JJ-039**, which record what the
> review accepted, changed and refused. Stories use Gherkin acceptance criteria.
> **Status: 🚧 IN PROGRESS** — planned 2026-09-13; BACKBAR-1 ✅ through BACKBAR-6 ✅ built the same day. One branch,
> `feat/backbar`, one commit per slice or fix, pushed on the word (PLAN, the redesign rule).

**Epic key:** `BACKBAR`

**Prerequisites:** none in the code — every screen it touches has shipped. One asset action for the
maintainer (a 2× landscape crop of Marga's scene, see BACKBAR-5), which does not block anything.

**Depends on:** `MARGA`, `SHELL`, `INV-3`, `ONBOARD-1` — for the screens it restyles. **Depended on
by:** nothing. It is a restyle: the document's own "do not change" list (page 18) is binding — routes,
page parameters, API calls, the makeability and unlock logic, Marga's selection rules, optimistic
ticks, the pre-paint theme apply, unit conversion, localisation keys and every existing `data-testid`.

---

## What it is, precisely

**A restyle, not a feature wave.** The 2026-09-10 proposal (`MARGA`, `SHELL`, `INV-3`, `ONBOARD-1`)
changed what the screens *do*; every one of its nine screens has shipped. This document changes what
they *look like*, and it says so on page 1: "Structure, routes, copy and test ids are unchanged from
develop." Reading it as a feature request makes it look four times larger than it is, exactly as
reading Marga as an assistant did last time.

Sorted by what it costs, the whole document is four things:

| What | Where it lives | Size |
|---|---|---|
| **Tokens and type** — a dark set and a light set in `app.css`, one self-hosted display serif | `app.css`, `wwwroot/fonts/` | small, and everything else stands on it |
| **Primitives** — pill buttons and inputs, the bottle pill, status badges, the hairline row, the 16px panel, one focus ring | `app.css`, as CSS on Bootstrap's classes | medium; no markup churn by design |
| **The chrome** — header and tab bar off copper onto the surface, a hairline and a 2px indicator | `AppHeader`, `app.css` | small, and it is "most of what makes the app stop looking like Bootstrap" (page 18) |
| **Nine screens** — rearranged over the same data, the same ids, the same strings | `Pages/*.razor` + scoped CSS | the bulk, screen by screen |

Nothing in it needs a new endpoint, a new query, a schema change or a new package.

## The review

The document is good. It was drawn from `develop`, it names the files it changes, it lists the ids to
keep per screen, and its implementation order (page 18) is the right order. The findings below are
where it meets the codebase and something gives — each with the verdict, so the slice that hits it
does not re-argue it.

| # | Finding | Verdict |
|---|---|---|
| F1 | **"Every existing test green with no id edits" is not achievable as written.** `ShellJourneyTests` asserts the chrome's literal colours — the destinations at `rgba(255,255,255,.85)` and the account buttons at `rgb(248,249,250)` on copper — and the chrome step (page 04) moves the bar off copper. | The three colour assertions are rewritten to the **invariant they guard** — a destination is never fainter than the account cluster, and dark theme never repaints a button copper — rather than to the old literal. The structural assertions (one element per id, aria-current, no sideways scroll, the payoff clears the tab bar) stand untouched. **JJ-037.** |
| F2 | **The catalog chips collide with a journey.** Page 07 turns the two switches into `btn-check` chips. `.btn-check` hides the input, and Playwright refuses to `check()` a hidden input — the INV-3 lesson, learned once already. `MakeableJourneyTests` calls `CheckAsync`/`UncheckAsync` on `cocktail-makeable` and `cocktail-almost` nine times. | Three **radios** in one group — `cocktail-makeable`, `cocktail-almost`, and a new `cocktail-all` — because "they already behave exclusively in code" is the argument for radio semantics, not for two checkboxes with exclusivity re-implemented. The journey drives the labels through one `E2ETestBase` helper, the way `SetShelfAsync` does. One id added, none edited; one string pair added ("Everything"). **JJ-036.** |
| F3 | **"Each carries its count"** (page 07) means three totals on every catalog load — two extra requests per visit for numbers the pager line already shows for the active filter. The same page says "same fetch-once behaviour". | The **selected** chip carries the response's `total`; the others carry none. No new fetch. |
| F4 | **Page 15 puts the Write row's role and required selects "in the row's own popover".** The app loads no Bootstrap JS, a popover is a new interaction, and `new-line-role` / `new-line-required` would sit behind an open step every journey would have to learn. | **Refused.** Role and required stay visible in the row — a sub-line on desktop, a second line on mobile. Everything else on page 15 stands. **JJ-036.** |
| F5 | **Settings and Household were inherited from the platform** (pages 16–17 restructure them: five cards to two columns, six cards to four groups, segmented theme control, a `···` row menu). The first draft of this review sent that markup upstream first. | **Restyled in full, here.** The UI is the app's own — every screen in the RCL, inherited or not — so pages 16–17 are built as their own slice, BACKBAR-7, with the components' parameters, ids and behaviours kept and no backend change anywhere in the epic. The sibling apps stay a comparison, not a constraint. **JJ-039.** |
| F6 | **Two of the document's id lists are invented.** Page 15 names `write-name`, `write-serving`, `write-line-{n}-*`, `write-save`…; the page's ids are `new-name`, `new-serving`, `new-line-*`, `new-save`, `new-error`. Page 17 names eight `household-*` ids; three exist (`household-rename-input`, `household-rename-save`, `household-status`). *(Corrected in BACKBAR-3: `shelf-item-{id}` and `onboard-item-{id}` DO exist, on the hidden inputs — the review's first pass mis-read the razor's interpolated attributes. The pills' labels are reached through `label[for]`.)* | The ids **in the code** are the contract. The document's lists are a reading aid, not a spec, and every "KEEP" list is re-derived from the razor when its slice starts. |
| F7 | **"No new strings" has two exceptions.** The mobile chip labels "Make now · 14 / One away · 81" (page 07) and the Write footnote "Marga will tell you if you can pour it" (page 15) exist nowhere in `AppStrings.resx`. | The chips use the **full** labels at every width (`Cocktails_MakeableOnly`, `Cocktails_AlmostOnly`) and the row scrolls if it must; the footnote is **dropped** — it promises nothing the page does not already do. The one new pair is "Everything" (F2), EN + ES. |
| F8 | **The scene is square and the panel is not.** Login and Welcome give `marga_scene_512.png` a ~520×680 panel; the document flags the upscale itself (page 05). The 2 MB source is deliberately not committed (MARGA-1). | Ship with `object-fit: cover` on the 512 asset now. The 2× landscape crop is a **maintainer asset action** from the source; when it lands it takes the same optimisation pass and the same `< 250 KB` gate the boot asset has. Nothing waits on it. |
| F9 | **The serif has one weight, and `fw-bold` is on 31 elements across 14 files.** Faux-bold on a one-weight face is what the document warns about (page 18). | Only the elements the serif reaches lose `fw-bold` (the count, drink names, card headlines, her line). A repo gate refuses `.font-display` and `fw-bold` on the same element. Everything in the sans keeps its weight. |
| F10 | **The chrome must read on both grounds**, and the header currently hard-codes `navbar-dark` for white text on copper; the mark `icon_light.svg` is bone for dark grounds. | `navbar-dark` goes; the bar follows `data-bs-theme`. `icon_dark.svg` becomes a brand asset generated by `docs/brand/build_assets.py` from the same source and swapped by the existing `content: url()` pattern the lockups use; `REBRANDING.md` §3 gains the row. **JJ-037.** |
| F11 | **Auto stays the default** (page 04). | Confirmed against `theme.js`: `system` is already the default and tracks the OS live. Nothing to change. |
| F12 | **The shelf's DOM shape changes** (cards to sections) and the document expects "component tests to need new selectors". | Checked: `ShelfPageTests` selects by test id, by `label[for]` and by the `#cat-{slug}` anchors, all of which survive. The risk is smaller than the document says; the anchors and their `scroll-margin-top` are the thing to keep. |
| F13 | **Hairline, never a shadow on dark; `shadow-sm` on light.** `.shadow-sm` is on nearly every card. | One global rule under `[data-bs-theme="dark"]` removes the shadow; light keeps Bootstrap's. No markup touched. |
| F14 | **Print** (page 10): "a recipe is the one screen people print." | Accepted as written — white ground, chrome and the fork bar hidden — with a QA case, since no browser journey prints. |

## The decisions, in one place

- **JJ-036** — the app adopts direction 1A. A restyle bound by the document's own "do not change"
  list, with F2, F3, F4 and F7 as the recorded adjustments. The handoff is committed under
  `docs/design/` because the slices cite its pages and the Claude Design project is not versioned.
- **JJ-037** — the chrome leaves copper, in both themes. Amends SHELL-1's settled answer in colour
  only: the destinations stay raised above the account cluster, the current tab stays weight plus a
  drawn indicator plus `aria-current`. Copper is reserved for actions, the count and Marga's advice.
- **JJ-038** — one self-hosted display serif, one weight, display only, never below 20px, never bold.
  Self-hosted in the RCL so both hosts get it with no third-party request. Amends JJ-029's typography;
  the wordmark PNGs are unchanged.
- **JJ-039** — the UI is the app's own, inherited screens included, so Settings and Household are
  restyled in full here; the backend may be extended but its foundation is a red light, and this epic
  touches no backend at all.

## The ladder

Nine slices, each one branch off `develop`, one PR, after the previous is merged — the three gates
in `docs/PLAN.md` apply unchanged. The order is the document's (page 18) with its six steps regrouped
so that every PR leaves the app looking finished at the level it reached, never half-restyled:

| # | Slice | Handoff pages | What it is |
|---|---|---|---|
| 1 | **BACKBAR-1** Foundation | 01, 02 | Tokens (both sets), the serif, the primitives, `MargaSays` `Tone`. Three commits, one PR. Visible everywhere at once; no screen rearranged. |
| 2 | **BACKBAR-2** Chrome | 04 | Header and tab bar off copper. `icon_dark.svg`. The shell journey's colour assertions rewritten (F1). |
| 3 | **BACKBAR-3** Home + Shelf | 03–04, 11–12 | The two identity screens. Welcome's step 2 inherits the shelf's sections for free (one control, two screens). |
| 4 | **BACKBAR-4** Cocktails + Detail | 07–10 | Chips as radios (F2), the filter panel, hairline rows, the amounts column, two marks, print. The one slice that touches a journey's mechanics. |
| 5 | **BACKBAR-5** Login + Welcome | 05–06, 13–14 | The two full-scene screens; `/join` and `/auth-error` reuse the split. Native parity checked on the Android emulator. |
| 6 | **BACKBAR-6** Write | 15 | Two columns, the amount in the serif, a fixed Save bar on mobile. No popover (F4). |
| 7 | **BACKBAR-7** Settings + Household | 16–17 | Five cards to two columns, six cards to four groups, segmented theme and unit controls, text-link row actions, the `···` menu on mobile, the bell's dropdown. Same parameters, ids and calls (F5). |
| 8 | **BACKBAR-8** Billing + Admin, and every small screen | — (not drawn) | The two pages the handoff did not draw, restyled to the same language in full: labelled groups of hairline rows, one plan panel, the tenant table as hairline rows. Plus the screens nobody draws — not-found, the auth callback, the impersonation banner, the error bar. |
| 9 | **BACKBAR-9** Sweep | 18 | Focus rings, reduced motion, both themes at 390/768/1440 on every screen, the QA plan's cases and regenerated PDFs, the definition of done ticked line by line. |

**Why the foundation is one PR and not three.** Tokens without primitives change nothing visible;
primitives without the serif leave the count in Helvetica bold; `Tone` without either draws her at
96px in the old card. The batching rule (PLAN, 2026-09-11) is for exactly this: three separable
commits, one CI run, one reviewer pass over a change that only makes sense whole.

**What every slice does before its commit** (the ritual, restated for a restyle): re-derive the
screen's id list from the razor, not from the document (F6); grep `tests/E2E.Tests` and
`tests/Ui.Tests` for every id and class it touches; run both themes at 390, 768 and 1440 in the
browser; Release build with zero warnings; `Core.Tests`, `Api.Tests`, `Ui.Tests`; add the QA cases
(7b) with the PDFs regenerated, or `qa-artifacts` goes red.

---

### BACKBAR-1 — Foundation: tokens, type, primitives, Marga's tone

**Status: ✅ Implemented (2026-09-13).** Pages 01–02, and steps 1, 3 and 4 of page 18. Three
commits on `feat/backbar`: `1a` tokens and the face, `1b` the primitives, `1c` Marga's tone.

**As a** member of a household
**I want** every control in the app to share one shape and one palette in both themes
**So that** the screens that follow are rearrangements of things I already recognise

**What was decided while building it.**
- **Bootstrap's own tokens are re-pointed, not fought.** `--bs-body-color`, `--bs-secondary-color`,
  `--bs-border-color` and `--bs-body-bg` now resolve to the app's ink, muted, hairline and surface in
  both themes, so `.text-muted`, every border and every card take the palette with no rule per
  component. Dark clears `--bs-box-shadow-sm` rather than overriding `.shadow-sm`, which Bootstrap
  marks `!important`. The page-10 status colours are the success/warning/danger subtle tokens under
  dark; light already had Bootstrap's, which the page quotes verbatim.
- **The face ships as the two subsets Google serves** (latin 21 KB, latin-ext 12 KB) with their
  `unicode-range`, so an English page never fetches the extended block and a Spanish one pulls it on
  demand. The one-weight rule is a gate: `RestyleGateTests` refuses `font-display` beside `fw-bold`.
- **`--label-accent`**: brass on dark, copper on light, for the small uppercase labels — page 01 says
  brass is a label colour on dark only, and brass on cream fails contrast.
- **The focus ring moved to `:focus-visible`** so a mouse click does not light it while a keyboard
  still does; the hidden `.btn-check` input hands its ring to the label.
- **`Tone` is additive.** `MargaTone.None` is the default and renders exactly the pre-Tone shape, so
  all seven call sites compile untouched and each moves to a tone in its own slice. On a card the
  `Aside` becomes the label above the line; inline it is not rendered at all.
- **The shelf pill grows through Bootstrap's button tokens** (`--bs-btn-padding-*`, `--bs-btn-font-size`)
  so `.btn-sm` yields by order rather than by `!important`, and its 120ms fill is dropped under
  `prefers-reduced-motion` beside the boot animation.

**Seen, not proven.** The three gate tests passed on their first run rather than failing first: the
build took longer than the stylesheet edit, so they ran against the finished CSS. The Tone tests did
fail to compile first, as the ritual asks. No browser pass was possible in the session that built
this (no display); QA-CHROME-14 is the case that looks.

**Context / notes.** Everything on page 02, as CSS on Bootstrap's classes so no markup churns.

- **Tokens.** `:root` gets the light set and `[data-bs-theme="dark"]` the dark set exactly as page 01
  lists them, plus `--surface` (panels and inputs; `--bs-body-bg` re-points to it — the page ground is
  already painted from `--app-bg` explicitly), `--ink` / `--muted` / `--faint`, and `--font-display`.
  The `--bs-primary` family stays; the derived family (`text-emphasis`, `bg-subtle`, `border-subtle`)
  is already restated per theme and gets the page-01 values.
- **The serif.** Instrument Serif, regular only, as `@font-face` from `wwwroot/fonts/` in the RCL, with
  its OFL licence file beside it. `font-display: swap`; the fallback stack is Georgia / serif. Both
  hosts get it through `app.css`, so R68 parity is automatic and the MAUI shells have it offline.
  Applied through one class, `.font-display`, never by element — page 18: display only, never a
  label, never below 20px.
- **Primitives.** `.btn` → pill (999px); `.form-control` / `.form-select` → 12px field on the surface;
  `.card` → 16px panel with a hairline, `shadow-sm` on light only (F13); the status badges in the
  page-10 colours; `.list-group-flush` rows → hairline rows; one focus ring
  (`0 0 0 2px var(--app-bg), 0 0 0 4px rgba(180,86,42,.55)`) replacing the two rules in `app.css`
  today. The shelf pill grows to 9px/16px with a 14px label (page 11) — it lives in `app.css` already
  because the wizard shares it.
- **`MargaSays`.** Gains `Tone="Card|Inline"`: card is 96px with the brass label above the line
  (`Aside` moves up and becomes the label — same string, new position) and the line in the serif at
  23–29px; inline is 32px, 14px sans, no label. `Size` and `Compact` stay for call sites that pass a
  number, so every call site keeps compiling and improves in its own slice. Ring 1px brass on dark,
  none on light.

**Acceptance criteria**

```gherkin
Scenario: The serif ships with the app and needs nothing from the network
  Given the RCL's wwwroot/fonts directory
  Then it contains the display face and its licence file
  And app.css declares the @font-face from that path and defines --font-display
  And no stylesheet in either host references an external font origin

Scenario: The display face is never synthesised bold
  Given every razor file under Shared.Ui
  Then no element carries both font-display and fw-bold
  # held by a repo gate in Api.Tests beside the index.html parity gates

Scenario: Marga has two tones and the old sizes still work
  When MargaSays renders with Tone="Card" and an Aside
  Then her image is 96 pixels and the aside renders as a label BEFORE the line
  When MargaSays renders with Tone="Inline"
  Then her image is 32 pixels and no label renders
  When MargaSays renders with Size="40" and no Tone
  Then it renders exactly as before this slice

Scenario: Nothing is rearranged yet
  Given every page renders in Ui.Tests as it did before this slice
  Then every existing test passes with no selector changed
```

**Out of scope:** any screen's layout; the chrome. **Definition of done:** the scenarios above;
`app.css` carries both token sets; the fonts folder is under 60 KB; both themes look like page 02 at
the three widths; every existing test green with no id or selector edits.

---

### BACKBAR-2 — Chrome: the bar leaves copper

**Status: ✅ Implemented (2026-09-13).** Page 04 (chrome), step 2 of page 18. **JJ-037.** One
commit on `feat/backbar`.

**As a** member of a household
**I want** the header and the tab bar to sit on the page rather than on a copper band
**So that** copper means "act here" everywhere it appears

**What was decided while building it.**
- **The handoff had the marks backwards.** `icon_light.svg` is the mark in copper — for a *light*
  ground, not a dark one as page 04 says — and it was sitting copper-on-copper in the old header. So
  the markup keeps naming it, and the new `icon_dark.svg` is the same path in bone for the dark
  surface, swapped in by `app.css` the way the lockups are. The gate holds both files, the swap, and
  that `REBRANDING.md` and `build_assets.py` name both.
- **One button variant for the whole account cluster.** `btn-outline-light` was white lines on
  copper; on a white bar it vanishes. Every button in the cluster — Household, Billing, Settings, Sign
  out, and the bell — is `btn-outline-secondary`, recoloured through Bootstrap's button tokens to
  muted with a hairline border. That is also what makes the old dark-theme bug structurally
  impossible: an anchor-shaped button and the `<button>` beside it carry the same class, so they
  cannot be painted apart. The staff-only Admin button keeps `btn-outline-warning`; it is meant to
  stand out.
- **The hierarchy is ink against muted now** where it was white against white-alpha: destinations in
  `--ink`, hover and focus copper, the current one bold with a 2px copper inset. Below `lg` the tab
  bar takes the surface and a top hairline, and the current tab gets a copper-subtle fill under its
  2px line.
- **The journey asserts relations, not literals.** In dark theme: a destination's colour equals the
  body's ink; the billing anchor's colour equals the sign-out button's; and neither equals
  `--bs-link-color`. The three assertions survive any future palette.

**Seen, not proven.** The E2E project compiles; the journey itself needs Postgres, Mailpit and a
browser, so its rewritten lines run first in CI. QA-CHROME-15 is the case that looks.

**Context / notes.** In both themes the bar takes the surface colour and a bottom hairline; the tab
bar the same with a top hairline. The current tab keeps weight, gains a 2px copper indicator, keeps
`aria-current`. The destinations stay raised above the account cluster — by ink against muted now,
where it was white against white-alpha — because that hierarchy is SHELL-1's settled answer and only
the colour changes. `navbar-dark` goes (F10); the mark swaps to `icon_dark.svg` on light through the
lockups' `content: url()` pattern, and `build_assets.py` generates it from the same source.

**Acceptance criteria**

```gherkin
Scenario: One element, two positions, still
  Given the header at 1280 wide and again at 390 wide
  Then nav-home, nav-shelf and nav-cocktails each render exactly once
  And they sit outside the collapsible menu
  # AppHeaderNavTests, unchanged

Scenario: The hierarchy survives the colour change (replaces the literal-colour assertions)
  Given I am signed in at 1280 wide, in dark theme and again in light
  Then the current destination's font-weight is 700 and the others' is 500
  And no destination is rendered with less contrast against the bar than the account buttons
  And no anchor-shaped button in the bar is painted the link colour
  And the page does not scroll sideways

Scenario: The mark reads on both grounds
  Given the header in light theme
  Then the brand mark is the dark-ground variant
  Given the header in dark theme
  Then the brand mark is the light-ground variant
```

**Out of scope:** the account cluster's contents; the bell's dropdown (BACKBAR-7).
**Definition of done:** the scenarios; `ShellJourneyTests` green with its three colour lines rewritten
and nothing else; QA-CHROME-04/05 re-run; `REBRANDING.md` §3 lists `icon_dark.svg`.

---

### BACKBAR-3 — Home and Shelf, the identity screens

**Status: ✅ Implemented (2026-09-13).** Pages 03–04 and 11–12. One commit on `feat/backbar`.

**As a** member of a household
**I want** the count to be the first thing on the front page and the shelf to be pills on a page rather than pills in boxes
**So that** the two screens I open most look like the product and not like its admin console

**What was decided while building it.**
- **A `--copper-ink` token** for copper as TEXT on the ground: copper itself on light, lifted on dark
  where #B4562A does not reach 4.5:1. The count, a drink name on hover, the unlock link and the
  payoff count all take it, and it is what page 01's "numeral #D9865A → #B4562A" means.
- **A `.page-title` class** for every screen's h1 — the type page's "serif h1 29", never bold — so
  each screen's h1 is the same h1. The shelf takes it here; the others in their slices.
- **The unlock panel's "ONE BOTTLE AWAY" eyebrow is not drawn.** No such string exists and the
  handoff promised no new ones; the copper tint and the serif headline say what the panel is.
- **The shelf's Marga label is her name**, passed literally: a name is not copy and has no
  translation. Home keeps `Marga_HomeAside` as its label.
- **The payoff count is a `<span>`, not a `<strong>`.** The gate refuses `fw-bold` beside the display
  class, but an element that is bold by default slips past it; the face has one weight either way.
- **The named bottle is drawn in the subtle copper, never the fill** — `--bs-btn-color/bg/border`
  from the primary-subtle family — so it is findable and cannot be mistaken for owned; ticking it
  still takes the fill through the active tokens like any other pill. Matched by name, because that
  is what her sentence says.
- **Welcome's step 2 inherits the pills, not the sections.** The wizard groups categories with its
  own markup; the shared control (pill + named-bottle style) reaches it from `app.css`, the section
  headings do not. BACKBAR-5 brings the sections across.

**Seen, not proven.** The shelf and makeable journeys drive the pills through `label[for]` and read
`#cat-{slug}`, `shelf-count` and `shelf-payoff` — all kept — so they are expected green in CI; the
loading bar and the two-column grid have no test and QA-CHROME-16/17 are the cases that look.

**Context / notes.**

- **Home** (`/`, FEATURES §9 for the count, §10 for the bottle). Desktop two columns 1.15fr / 1fr,
  gap 52, max-width 1100; mobile one column. The numeral at 128px in the serif and copper; the three
  drinks as hairline rules, not a list-group; the unlock panel the only boxed thing. Empty shelf: her
  scene at 260px beside `Marga_HomeEmpty`, "Set up my shelf" primary, "Tick what you have" a text
  link — no zero. Loading: a 2px indeterminate brass bar under the header and the numeral holding its
  space at 40%, no spinner. Failure unchanged: she and the panel are absent, count and list stand.
- **Shelf** (`/shelf`, FEATURES §8). Cards become sections — a serif heading on a hairline with `n of
  m` right-aligned — keeping `#cat-{slug}` and its `scroll-margin-top`. The bottle Marga names is
  outlined in `#5C3620` on dark and copper-on-`#F9EDE7` on light: present, never mistaken for owned.
  The add form opens in place under the section's pills on the surface colour. The payoff stays
  sticky above `--tab-bar-clearance`, its count in the serif and copper, the phrase in the sans.
  Optimistic ticks unchanged; the fill animates 120ms.
- **Welcome step 2** renders the same sections with no change of its own (ONBOARD-1: one control).

**Acceptance criteria**

```gherkin
Scenario: Home keeps its ids and loses its boxes
  Given a household with drinks makeable and a bottle one away
  When the home page renders
  Then home-marga, home-count, home-show-makeable, home-update-shelf, home-makeable-list, home-unlocks and home-show-almost each render once
  And home-unlocks is the only element on the page with the panel class
  And the count carries the display face

Scenario: Home never shows a zero
  Given a household with nothing ticked
  When the home page renders
  Then home-count does not render
  And home-setup-shelf renders as the primary action beside her scene

Scenario: The shelf's anchors survive the sections
  Given the shelf for a household with gin and rum categories
  Then #cat-gin and #cat-rum exist, each carrying shelf-cat-{slug}
  And each section's count is over the whole category, never over what search left visible
  # ShelfPageTests, unchanged

Scenario: The named bottle is findable and not owned
  Given Marga names Sweet vermouth as one bottle away
  Then the Sweet vermouth pill carries the named-bottle class and its input is not checked

Scenario: The payoff still clears the tab bar
  # ShellJourneyTests' geometry assertion, unchanged
```

**Out of scope:** any change to what the two screens fetch. **Definition of done:** the scenarios;
`HomePageTests`, `ShelfPageTests`, `MargaPresenceTests`, `WelcomeWizardTests` and the shelf journeys
green; QA-SHELF and QA-CHROME-10/11 re-run; both themes at three widths match pages 03–04, 11–12.

---

### BACKBAR-4 — Cocktails and the recipe

**Status: ✅ Implemented (2026-09-13).** Pages 07–10. **JJ-036 (F2, F3).** One commit on
`feat/backbar`.

**As a** member of a household
**I want** the three ways of looking at the catalog to be one control, and a recipe to be read by its measures
**So that** the filter I am on is obvious and the quantity is the thing my eye lands on

**What was decided while building it.**
- **The rows keep their `list-group` classes.** The browse journey reads rows by `.list-group-item`
  and the recipe's lines by `li`, and the primitives already draw a list-group as hairlines — so the
  classes stay and the CSS does the restyle, which is the handoff's own rule ("build them as CSS on
  Bootstrap's classes so no markup churns"). Nothing in `CocktailBrowseJourneyTests` changes.
- **`MargaSays.Size` became nullable.** Page 02 draws the card tone at 96 on Home and the shelf but
  at 72 on the catalog, the recipe and the wizard; a set `Size` now overrides the tone's default, an
  unset one takes it. The three existing tone tests hold both readings.
- **The "oz · as written" label above the ingredient list is not drawn.** The recipe response does
  not say which unit system rendered it, and adding that is an API change — out of scope by page 18.
  `PREFS-2` owns the preference; Settings is one tap away.
- **An optional line that is missing says nothing.** Page 09's "two marks only" plus JJ-009 (an
  optional line never blocks): the missing mark is for required lines only, so a garnish you do not
  have is a dash and the word "optional", not a red "not on your shelf".
- **A `.eyebrow` class and a `--mark-missing` token** — the small uppercase labels ("METHOD",
  "INGREDIENTS") and the one red in the app, `#B02A37` light / `#E88A8A` dark, only ever on a line.
- **The empty-state parts moved to `app.css`** (`.catalog-empty`, `.empty-scene`,
  `.catalog-empty-line`, `.empty-starter`) because the recipe's not-found now uses the same layout
  and scoped CSS cannot be shared across two pages.
- **The chip labels are the switch labels at every width** (F7); "Everything" is the one new string,
  EN and ES. Only the selected chip carries a count — the response's own `total`, formatted, never a
  second request — and a test holds that exactly one GET is issued.
- **The journey helper waits on the list request**, `GET /api/cocktails?…`, not on the unlocks
  request that follows it; the unlocks panel is then awaited by its own `Expect`, which is what the
  old `RunAndWaitForResponseAsync` around the almost switch was doing by hand.

**Seen, not proven.** `MakeableJourneyTests` compiles against the helper; its nine rewritten calls
run first in CI. The print stylesheet, the sticky chip row and the fixed fork bar have no test —
QA-CHROME-18/19 are the cases that look.

**Context / notes.**

- **Chips.** Three radios in one group — `cocktail-makeable`, `cocktail-almost`, `cocktail-all` — as
  `btn-check` inputs with pill labels, so keyboard and screen reader get a radio group. Only the
  selected chip carries a count, the response's `total` (F3). `?makeable=true` / `?almost=true`
  preselect as today. The search field becomes a pill on the surface, 280px at desktop. The filter
  panel opens under the chips as a 16px panel, four fields in a 4-up grid, "Clear filters" a text
  link — same fetch-once behaviour. Rows are hairline, 18px vertical; Marga inline at 32px with her
  line in copper; the source credit right-aligned at 12px and `white-space: nowrap` (load-bearing:
  four names appear in both books). Pagination as text controls on a hairline. The unlocks panel in
  the copper-subtle style under "One ingredient away"; the empty state keeps her scene at 260px.
- **Detail.** The measure in the serif at 21px, copper-lifted, in a 104px column. Two marks only:
  substitute ("you'd pour X", brass) and missing (`#E88A8A` dark / `#B02A37` light); everything
  pourable silent. Status badge colours from page 10; further-away neutral, never red. "Based on" in
  the facet line. Optional lines: a dash in the amount column, muted name, "optional" as plain text —
  the Bootstrap badge goes. Title wraps to two lines at 52px before shrinking, `text-wrap: balance`,
  never truncated. Fork: a primary pill at the foot of the ingredient column on desktop, a fixed bar
  above the tab bar on mobile, `cocktail-fork-error` inline beneath. `@media print`: white ground,
  chrome and fork hidden.
- **The journey.** `MakeableJourneyTests` drives the chips through a `SetCatalogFilterAsync` helper in
  `E2ETestBase` that clicks the label and waits on the GET, the way `SetShelfAsync` does; every
  `UncheckAsync` becomes a click on `cocktail-all`. `ToBeCheckedAsync` on the inputs stays valid.

**Acceptance criteria**

```gherkin
Scenario: The chips are one control
  Given the catalog page
  Then cocktail-makeable, cocktail-almost and cocktail-all are radio inputs sharing one name
  And exactly one of them is checked at any time
  When I open /cocktails?almost=true
  Then cocktail-almost is checked and the page shows the one-away list

Scenario: Only the selected chip counts
  Given the makeable filter is selected and the response says 14
  Then the makeable chip's label contains 14
  And no other chip's label contains a number
  And the page issued one GET for the list, as before

Scenario: A recipe says something in exactly two cases
  Given a recipe with one substituted line, one missing line and three pourable lines
  Then cocktail-line-substitute renders once and cocktail-line-missing renders once
  And no pourable line carries either mark

Scenario: An optional line is text, not a badge
  Given a recipe with an optional garnish
  Then that line shows a dash in the amount column and the word optional as plain text
  And no badge element renders inside cocktail-lines

Scenario: The recipe prints clean
  Given a recipe page in print media
  Then the header, the tab bar and cocktail-fork are hidden
  # QA case; no journey prints
```

**Out of scope:** the filters' options, the queries, the pager's page size. **Definition of done:**
the scenarios; `MakeableJourneyTests` and `CocktailBrowseJourneyTests` green through the new helper;
`CocktailsEmptyStateTests` green; "Everything" in EN and ES; QA-MAKE, QA-CKTL and a new print case
re-run; both themes at three widths match pages 07–10.

---

### BACKBAR-5 — Login and Welcome, her two full appearances

**Status: ✅ Implemented (2026-09-13).** Pages 05–06 and 13–14. One commit on `feat/backbar`.

**As a** person who has not yet told the app anything
**I want** the first screen to be her, and her sentence to be its headline
**So that** the product's promise is the first thing I read, not a form

**What was decided while building it.**
- **One component, `MargaSplit`, for all four screens.** Login, the wizard, Join and the auth error
  page each wanted the same two panels; four copies of the split is how it stops being one. The
  night ground, the gradient, the brass label and the lockup over the scene are drawn there once —
  the "one intentional exception to the theme swap" (page 06) is a rule in one file rather than a
  memory in four. `Warm` gives the wizard its `#2A1A12` base; `Compact` shortens the scene for the
  screens with little beside it.
- **Login has no `MargaSays` any more.** She IS the scene; an avatar beside the form would put her
  face on the screen twice, the same reasoning the empty states used. The login line's label is
  `Onboard_MargaAside` ("Marga · behind the bar"), which page 05 quotes and which already existed.
- **The auth error page's line is the error heading, with no label** — it is not her line, so it
  gets no attribution. Join's line is the login line: the same promise, to someone arriving by
  invitation, and no new string.
- **The shelf's section rules moved from `Shelf.razor.css` to `app.css`.** The wizard's second step
  renders the same sections, and scoped CSS cannot reach a second page. This closes what BACKBAR-3
  left open ("Welcome's step 2 inherits the pills, not the sections"); it now inherits both. The
  wizard grew a `CountFor` of its own, read against what is WANTED rather than saved, because nothing
  there is saved until Finish.
- **No preload for the scene.** Page 06 asks for one on the anonymous route; the boot screen already
  fetched the same file, so the browser has it. Nothing to add.
- **The running total is a `<span>`,** like the shelf's payoff — a `<strong>` would ask the one-weight
  face for bold.

**Seen, not proven.** The sign-in, onboarding, MFA and invitation journeys drive these screens by
test id only (checked in the page objects too), so they are expected green; the above-the-fold
promise at 390×812, the 26px slide and the night panel in light theme are QA-CHROME-20's to look at.
Native parity (the MAUI shells render the same split) is the Android smoke's.

**Context / notes.** Both screens split: her scene on one side (`marga_scene_512.png`,
`object-fit: cover`, a bottom gradient to `#0F1216` so her line sits on solid ground), the working
side on the other. **Her panel keeps its dark ground in both themes** — the illustration is a night
bar and carries its own light; only the form side flips. On light, Welcome's panel base is `#2A1A12`
so the gradient lands warm. Over her scene the lockup is always `lockup_dark`; the form side keeps
today's `content: url()` swap. Mobile: the scene 300px tall on top (240 below 380px), the panel
sliding 26px up under the gradient, the whole login form above the fold at 390×812. The code step
swaps the buttons for the 6-digit field at 24px, letter-spacing .4rem, and she does not move.
Welcome adds a 3px brass progress rule at 50% / 100% under "Step 1 of 2"; step 2 is the shelf's
sections; it keeps the app header (page 13: "trapping people in it was never the intent"). `/join`
and `/auth-error` reuse the split with her line replaced by their copy. The scene is already in the
browser cache from the boot screen, so no preload is added. Native: the MAUI shells render the same
two panels; the magic-link button is absent and the code button primary, as today. The 2× landscape
crop is the maintainer's asset action (F8) and lands whenever it lands.

**Acceptance criteria**

```gherkin
Scenario: The form keeps every id
  Given the login page
  Then login-email, login-send-magic-link, login-send-otp, login-otp-code, login-verify-otp, login-mfa-code and login-error render where their step shows them
  And provider buttons render only for configured providers

Scenario: Her panel does not follow the theme
  Given the login page in light theme
  Then her panel's background is the night ground and the form side is #fff

Scenario: The wizard keeps every id and the header
  Given /welcome
  Then onboard-step, onboard-staples, onboard-search, onboard-count, onboard-back, onboard-next, onboard-finish and onboard-skip render on their steps
  And the app header renders above it

Scenario: Nothing is written until Finish
  # OnboardJourneyTests, unchanged
```

**Out of scope:** any sign-in mechanics. **Definition of done:** the scenarios; `AuthFlowTests`,
`MagicLinkJourneyTests`, `MfaJourneyTests`, `OnboardJourneyTests`, `WelcomeWizardTests` green; the
Android emulator smoke (`docs/MOBILE_TESTING.md`) shows the split; QA-SMK-01, QA-ONB and QA-AND-01
re-run; both themes at three widths match pages 05–06, 13–14.

---

### BACKBAR-6 — Write a cocktail

**Status: ✅ Implemented (2026-09-13).** Page 15. **JJ-036 (F4, F7).** One commit on `feat/backbar`.

**As a** member writing my own recipe
**I want** the drink on one side and its lines on the other, with the amounts reading like a recipe
**So that** the form previews what it is writing

**What was decided while building it.**
- **Page 15's "Fork: the same form, pre-filled, with Based on X" describes editing, not forking.**
  FORK-1 makes the snapshot on the server and lands on the copy's recipe page; the form is not in
  that flow. Pre-filling it with an existing recipe is AUTHORING-2, the one outstanding story, and it
  stays outstanding — a restyle does not open a flow. `write-forked-from` never existed (F6).
- **Client-side checks attach to the field.** A missing name marks the name field with the message
  under it; no complete line puts the message under the lines, on a new additive id
  `new-lines-error`. Both checks run rather than the first one returning, so a form with two
  problems shows two. `new-error` keeps its place above Save for the server's answer, which is
  still mapped from the API's codes. The one journey that saves a recipe fills the form correctly
  and reads none of these.
- **The amount and its unit share a 160px column** so the row reads "2 oz · London dry gin" the way
  the recipe page does; the amount input is in the display face at the recipe's 21px. Placeholders
  and `aria-label`s replaced the per-column labels the old grid needed; the field still has a name
  for a screen reader.
- **"3 lines" is `Cocktails_IngredientCount`** ("{0} ingredients"), which already existed.
- **The remove control has an `aria-label`** — `Household_Remove`, "Remove" / "Eliminar", which
  already existed; an "×" alone announces nothing. (The first draft named a `Common_Remove` that does
  not exist; the fake localizer echoes keys, so only a grep of the resx caught it. A missing key is
  the one thing these tests cannot see.)

**Seen, not proven.** The authoring journey (MINE-03/04) drives the form by test id and gets its run
in CI. The fixed Save bar and the collapsed row at 390 are QA-CHROME-21's to look at.

**Context / notes.** Two columns: the drink (name, served as, glass, method, instructions) left, the
ingredients right. Each line is an amount field whose value renders in the serif, the ingredient with
role and required as a visible sub-line (F4 — no popover), and a remove control. Errors attach to the
field; `new-error` keeps its place above Save for server failures. Mobile: one column, Save as a
fixed bar above the tab bar, the row collapsing to amount + name with role and required on a second
line. Fork: the same form pre-filled with "Based on X" under the title. The footnote on page 15 is
dropped (F7). Ids are the page's own: `new-name`, `new-serving`, `new-glass`, `new-method`,
`new-instructions`, `new-lines`, `new-line`, `new-line-*`, `new-line-add`, `new-line-remove`,
`new-save`, `new-error`.

**Acceptance criteria**

```gherkin
Scenario: Every field stays reachable without opening anything
  Given the write page with one line
  Then new-line-amount, new-line-unit, new-line-ingredient, new-line-role and new-line-required are all visible at 1440 wide and at 390 wide

Scenario: The amount previews in the display face
  Given a line whose amount is 2
  Then the amount field carries the display face

Scenario: Validation stays where it was
  # the authoring journey, unchanged: a lineless recipe and a unit with no amount are refused inline
```

**Out of scope:** editing (AUTHORING-2 is still outstanding and unrelated). **Definition of done:**
the scenarios; the authoring and fork journeys green; QA-AUTH(oring) cases re-run; both themes at
three widths match page 15.

---

### BACKBAR-7 — Settings and Household

**Status: 📋 Planned.** Pages 16–17. **JJ-039.** FEATURES §5 (invitations) and §6 (account settings).

**As a** member managing my account or my household
**I want** the same information at half the height, with the choices visible without opening anything
**So that** the two screens I visit least stop being the two that still look like a template

**Context / notes.** Both screens were inherited from the platform and are the app's to restyle in
full (F5). What changes is arrangement and control shape; what does not change is any call, any
parameter, any id, or any behaviour.

- **Settings.** Five bordered cards become labelled groups of hairline rows in two columns. Theme and
  Measurements become **segmented pills** — three fixed options each, the choice visible — writing the
  same preference through the same `PUT` the selects do today; the header's `ThemeSwitcher` may go
  icon-only once this exists. Language stays a select (the list grows). Each row still saves on
  change with no Save button; the toast stays, and on failure the control reverts. The danger zone
  becomes a single red text link at the foot that opens the existing confirm dialog — "the box made
  deletion feel like a feature." `MfaCard` and `NotificationPrefsCard` are restyled inside, same
  parameters: enabling 2FA still expands in place (QR, manual key, 6-digit confirm, recovery codes in
  a monospace block with copy); Unlink stays disabled on the last provider with the existing
  explanation beneath.
- **Household.** Six cards become four groups: Name and Members on the left, Invitations, Ownership
  and Data on the right. Row actions are text links, not outline buttons; Owner is the only
  copper-tinted badge; the household name renders in the serif because it is a name. On mobile, row
  actions collapse behind a `···` menu at 44px that calls exactly what the buttons called. Non-owners
  see the same layout with Rename, Transfer, Remove and role changes absent, not disabled. Destructive
  actions stay red text links behind the existing confirm dialog; Revoke and Remove still re-fetch
  the roster. `/join/{token}` reuses the Login split (BACKBAR-5) with her line replaced by the invite.
- **The bell.** Its dropdown becomes hairline rows on the surface colour with a brass unread dot;
  behaviour unchanged.

**Acceptance criteria**

```gherkin
Scenario: The segmented theme control and the header switcher are one preference
  Given Settings in light theme
  When I choose Dark on the segmented control
  Then data-bs-theme is dark, the header switcher reads dark, and one PUT /api/auth/theme was sent
  # the theme journey's assertions, unchanged in what they check

Scenario: Deleting an account still asks first
  Given Settings
  When I follow the red delete link
  Then the existing confirm dialog opens and nothing is sent until it is confirmed

Scenario: A non-owner sees the layout with the owner's controls absent
  Given a member (not owner) on /household
  Then the roster and the export are visible
  And no Rename, Transfer, Remove or role control exists in the DOM
  # the roster and membership journeys, unchanged

Scenario: The row menu calls what the buttons called
  Given /household at 390 wide as the owner
  When I open ··· on a member row and choose Remove
  Then the same confirm dialog opens and the same DELETE is sent as at 1280 wide
```

**Out of scope:** any endpoint, any preference's storage, any permission rule. **Definition of
done:** the scenarios; `RosterJourneyTests`, `MembershipLifecycleTests`, `MfaJourneyTests`,
`NotificationJourneyTests`, `ThemeJourneyTests`, `GdprExportJourneyTests`, `SwitcherStateTests` and
`PreferenceScopingTests` green; QA-SET, QA-HH, QA-INV, QA-MFA and QA-NOTIF re-run; both themes at
three widths match pages 16–17.

---

### BACKBAR-8 — Billing and Admin, and every small screen

**Status: 📋 Planned.** Not drawn in the handoff. **JJ-039** — all pages, no exceptions. Spec: page 02
(components) and the Settings/Household language of pages 16–17, applied by analogy.

**As a** household owner on the billing page, or platform staff on the console
**I want** the two pages the designer never saw to look like the rest of the app
**So that** there is no screen left that gives the old template away

**Context / notes.** These pages were not drawn, so the treatment is derived rather than copied: the
same labelled groups of hairline rows, the same text-link row actions, the same status colours, and
one boxed panel per page for the one thing to act on. Nothing about what they fetch, send or gate
changes; every id and every config gate (`Billing:Enabled`, the staff probe) stays.

- **Billing** (`/billing`, GATES-1 hides it entirely when billing is off). Four cards become two
  groups: **Plan** — the plan name in the serif with its status as a pill (the page-10 status colours;
  the `bg-info-subtle` badge goes), renews-on and ended-on as meta rows, Upgrade as the primary pill
  or Manage as a text link; **Seats** — used of allowed as the same `n of m` figure the shelf uses.
  The checkout success / cancel banners become one 12px-radius notice above the groups; the
  owner-only notice is a muted line, not an alert. `billing-*` ids unchanged; the fake-provider
  upgrade-loop journey unchanged.
- **Admin** (`/admin`, staff only). Six cards and one table become groups: **Broadcast** at the top
  as a panel (it is the one thing that acts platform-wide); **Tenants** as hairline rows with the
  name in the serif and the plan as a neutral pill, `admin-tenant-row` kept; the selected tenant's
  detail as three labelled groups — Subscription (comp / revert as text links, 409 message inline),
  Members (the MFA reset as a text link with its status inline), Announce. Success statuses become
  the same inline notice as Billing's rather than five green alerts. `admin-forbidden` keeps the
  page-06 error styling. `admin-*` ids unchanged; the announcement journey unchanged.
- **The screens nobody draws.** The not-found view in `App.razor` on the empty-state layout (her
  scene, the line, a link home); the auth-callback spinner replaced by the 2px brass bar; the
  impersonation banner as a hairline strip in the warn colours, still `impersonation-banner`; the
  `#blazor-error-ui` bar in the page-06 error colours instead of light yellow, still `color-scheme:
  light only` so it reads on either theme.

**Acceptance criteria**

```gherkin
Scenario: Billing keeps every id and every gate
  Given billing is enabled and I am the owner on a Pro plan
  Then billing-plan, billing-status, billing-renews, billing-seats and billing-portal render
  And billing-upgrade does not
  Given billing is disabled
  Then /billing is refused as today and the header shows no link
  # BillingGateUiTests and BillingJourneyTests, unchanged

Scenario: Admin keeps every id and the staff gate
  Given a non-staff user on /admin
  Then admin-forbidden renders and nothing else does
  Given staff, with two tenants listed
  Then admin-tenant-row renders twice, as rows, not as table cells
  # AnnouncementJourneyTests, unchanged

Scenario: Not found is an empty state, not a blank page
  Given a route that matches nothing
  Then her scene, the not-found copy and a link home render on the page ground
```

**Out of scope:** any endpoint, any gate, any admin write (ADR-021 enumerates them and this adds
none). **Definition of done:** the scenarios; the billing, seat-quota, announcement and impersonation
journeys green; QA-BILL and QA-ADM cases re-run; both themes at three widths read as the same
language as pages 16–17.

---

### BACKBAR-9 — The sweep

**Status: 📋 Planned.** Page 18.

**As a** member on any screen
**I want** the restyle to be finished, not mostly finished
**So that** no screen, state or width is the one that still gives the old app away

**Context / notes.** The cross-cutting pass page 18 asks for — focus rings on every interactive
element, `prefers-reduced-motion` dropping the 120ms fill and the 200ms panel expand, a pass in both
themes at 390, 768 and 1440 on every screen, and the definition of done ticked line by line. The QA
plan gains a case per user-visible change this epic made that no earlier slice already covered, the
traceability matrix and sign-off rows, and the PDFs regenerated.

**Acceptance criteria**

```gherkin
Scenario: No card border survives except the panel
  Given every page in Ui.Tests renders
  Then every .card resolves to the 16px hairline panel
  And the copper-subtle panel is the only element with a tinted border

Scenario: Motion is optional
  Given prefers-reduced-motion: reduce
  Then the pill fill and the panel expand have no transition

Scenario: Every journey is still green
  # the whole E2E suite, unchanged except the two helpers BACKBAR-2 and BACKBAR-4 introduced
```

**Out of scope:** nothing the document asks for; anything it does not. **Definition of done:** the
definition of done on page 18, every line; the whole suite green; `docs/QA_TEST_PLAN.md` updated with
the artifacts regenerated; the Slice Board updated on "merged".

---

## Risks carried, and the one that is new

- **From the document (page 18):** the one-weight serif and `fw-bold` (F9, gated); the shelf's DOM
  shape (F12, smaller than stated).
- **From this codebase:** scoped CSS cannot reach an element a child component renders (PLAN,
  "Writing component CSS that a child component renders") — every rule whose last element is a
  `<NavLink>` anchor, a `MargaSays` image or a Bootstrap-generated element belongs in `app.css`.
  BACKBAR-1 puts the primitives there for this reason as much as for sharing.
- **New:** a restyle is the one kind of change the suite is weakest at. Three widths × two themes ×
  nine screens is 54 frames, and no test looks at any of them. The per-slice browser pass is not
  optional, and the QA cases exist so that a person runs it again after the wave.
