# Stories — App shell (SHELL)

> One file per epic. The chrome every screen sits inside: the header, the responsive navigation, and
> the state the app shows before Blazor has finished loading. Read with `docs/NATIVE_PARITY.md` — the
> two hosts carry their own `index.html` and keeping them in sync is a maintainer rule, not a nicety.
> Stories use Gherkin acceptance criteria.
> **Status: ✅ COMPLETE for MVP** — SHELL-1 (the responsive shell) and SHELL-2 (the boot state) shipped.

**Epic key:** `SHELL`

**Prerequisites:** `MARGA-1` for the illustration used by SHELL-2. No new packages, no schema change.

**Depends on:** `MARGA` for the asset only. **Depended on by:** nothing.

---

### SHELL-1 — The responsive shell

**Status: ✅ Implemented.** Applies to every proposal screen below the `lg` breakpoint.

**As a** member of a household on a phone
**I want** the app's own destinations where my thumb is
**So that** getting to my shelf is not a trip through a hamburger menu

**Context / notes.** ALMOST-1 already separated app from platform on the header: `Home`, `Shelf` and
`Cocktails` sit on the left as plain links, and the account furniture stays on the right as outline
buttons. This finishes that idea at narrow widths — the three destinations move to a bottom tab bar
and the top bar keeps only the account cluster.

> **⚠️ The browser journeys click `nav-shelf` and `nav-cocktails` by test id.** Whatever renders those
> destinations at any width must carry those same ids, or the suite goes red for a reason that has
> nothing to do with the feature. This is the single highest-risk detail in the epic.

**One element, two positions — and that is what defuses the risk above.** The obvious build is to
render the destinations twice and hide one set with CSS, which puts **two** `nav-shelf` in the DOM;
Playwright refuses an ambiguous locator, so every journey in the suite would fail on a slice that had
nothing to do with them. Instead the same `<ul>` is repositioned: it sits in the header row at `lg`
and above, and below that breakpoint it is `position: fixed` to the bottom of the viewport. A fixed
element does not care where it lives in the DOM, so no second copy is needed and the ids cannot
double.

It also sits **outside** `.navbar-collapse` on purpose. Inside it, the tab bar would be `display:
none` below `lg` until someone opened the hamburger — which is exactly the trip through a menu this
slice exists to remove. That structure is held by a test rather than left to a future tidy-up.

#### The wide-screen question, settled: fix it at both widths

The header nav links were white at 55% opacity from Bootstrap's `--bs-navbar-color`, which the fixes
document flagged as a hierarchy question rather than a defect: the app's own three destinations were
the faintest text in the chrome while Household, Billing and Settings sat beside them at full
strength. Answering it only in the tab bar would have **moved** the inconsistency rather than settled
it, so the links are lifted at every width — 85% white, full white on hover, and the current one at
full white and bold.

The account cluster is deliberately left alone. The complaint was that the destinations read as less
important than the furniture; the fix is to raise the destinations, not to dim the platform's
buttons, which are shared chrome and not this slice's to redesign.

**The current tab is weight plus a drawn indicator, never colour alone** — an inset bar under the
label in the header, above it in the tab bar, where it reads as the tab being lifted rather than as
an underline hanging off the bottom of the screen. `aria-current="page"` carries the same fact to a
screen reader, which `NavLink` does not do on its own: it supplies the `active` class and nothing
else. Computing it here means the header must re-render on **every** location change rather than only
when it has a menu to close, or it announces the previous page for the rest of the session.

**A fixed bar sits ON the page, not below it**, so two things had to make room: the layout's content
container gained bottom padding, and INV-3's sticky payoff footer now sits clear of the bar. That
clearance is one global token (`--tab-bar-clearance`, zero above the breakpoint) rather than a media
query re-spelled on each screen — the payoff footer is the first claimant and will not be the last.

**Acceptance criteria**

```gherkin
Scenario: The destinations follow the width
  Given a viewport below lg
  Then Home, Shelf and Cocktails are in a bottom tab bar, reachable without opening anything
  And the top bar carries only the account cluster

Scenario: The test ids survive
  Then nav-shelf and nav-cocktails resolve at every width
  And each resolves to exactly ONE element, because there is only one

Scenario: The current destination is obvious
  Then the active tab is distinguishable without relying on colour alone
  And it is announced as the current page, not merely styled
  And reading a recipe still counts as being in the catalog

Scenario: The bar does not cover the page
  Then the end of every screen is reachable above it
  And anything else anchored to the bottom sits clear of it
```

---

### SHELL-2 — The boot state

**Status: ✅ Implemented.** Proposal screen **9**. The smallest slice in the wave.

**As a** member of a household opening the app
**I want** the wait to look like the app I am waiting for
**So that** the first thing I see is not a stock spinner

Replaces the stock two-circle `.loading-progress` spinner with the illustration, keeping the brass
`--brand-accent` arc it already used and still reading the real `--blazor-load-percentage`. A markup
swap rather than new plumbing; **the tilt is CSS on the static drawing**, so she rocks as if shaking
and no second asset or animated format is needed to say it.

**Two files, and the rule was already being broken.** Both hosts carry their own `index.html`, and
`NATIVE_PARITY.md` names keeping them in sync as a maintainer rule — but the web host had the stock
spinner while the MAUI host had the literal word `Loading...`. So the parity was gone before there was
a boot state to lose it with. It is a **CI gate** now rather than a line in a document: both files must
carry the boot markup and point at the same drawing.

**One intended difference, asserted in both directions.** A WebView loads the app out of the app
package: there is no download to measure, `--blazor-load-percentage` is never set, and an arc reading
it would sit frozen at zero for the whole boot — which reads as broken rather than as fast. The MAUI
host adds `boot-indeterminate` and the same arc sweeps instead of filling. The gate requires that class
on MAUI and forbids it on web, so the difference stays deliberate rather than drifting.

> **Found in the browser, not by a test.** The arc showed about a sixth of a turn while the text beside
> it read 81%. `calc()` cannot divide one percentage by another, and an invalid `calc` is dropped
> **silently** — so the arc rendered with no relation to the number printed inside it. The percentage
> stays a percentage and is only multiplied. In a 100×100 viewBox a dasharray percentage resolves
> against a normalized diagonal of 100, so the circumference (2 π 46) is 289%, and 100% of the load
> now maps exactly onto 289% of dash. Measured: 0 → 0%, 25 → 72.25%, 100 → 289%.

**The payload, not just the markup.** The illustration was 2 MB as delivered. The boot screen is the one
place it is fetched *before* the app is usable, so an unoptimized asset here makes the very wait it
decorates longer. MARGA-1 shipped it at 115 KB, and a gate now holds a ceiling on it — room to redraw
it, no room to paste the original back.

**Motion is optional**, because a boot screen is the one thing nobody can choose to skip. Under
`prefers-reduced-motion` the rocking stops and the sweep becomes a static ring; the determinate arc
still shows progress, it simply does not move on its own.

**Acceptance criteria**

```gherkin
Scenario: It shows real progress
  Then the arc reads --blazor-load-percentage rather than animating on a timer
  And the length of the arc matches the number printed inside it

Scenario: Both hosts agree
  Then the web and MAUI index.html both render the boot state, from the same drawing
  And the only difference is the sweep, on the host that has no download to measure

Scenario: It does not cost what it saves
  Then the illustration is optimized before it lands in the boot path

Scenario: The motion can be turned off
  Given a viewer who has asked for reduced motion
  Then nothing on the boot screen moves on its own
```
