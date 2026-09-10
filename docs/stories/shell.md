# Stories — App shell (SHELL)

> One file per epic. The chrome every screen sits inside: the header, the responsive navigation, and
> the state the app shows before Blazor has finished loading. Read with `docs/NATIVE_PARITY.md` — the
> two hosts carry their own `index.html` and keeping them in sync is a maintainer rule, not a nicety.
> Stories use Gherkin acceptance criteria.
> **Status: 📋 PLANNED.**

**Epic key:** `SHELL`

**Prerequisites:** `MARGA-1` for the illustration used by SHELL-2. No new packages, no schema change.

**Depends on:** `MARGA` for the asset only. **Depended on by:** nothing.

---

### SHELL-1 — The responsive shell

**Status: 📋 Planned.** Applies to every proposal screen below the `lg` breakpoint.

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

The header nav links are white at 55% opacity from Bootstrap's `--bs-navbar-color`, which the fixes
document flagged as a hierarchy question rather than a defect: the app's own three destinations are
the faintest text in the chrome while Household, Billing and Settings sit beside them at full
strength. Moving them to a tab bar answers that question at narrow widths and leaves it open at wide
ones. **Decide whether wide screens change too, or the inconsistency simply moves.**

**Acceptance criteria**

```gherkin
Scenario: The destinations follow the width
  Given a viewport below lg
  Then Home, Shelf and Cocktails are in a bottom tab bar
  And the top bar carries only the account cluster

Scenario: The test ids survive
  Then nav-shelf and nav-cocktails resolve at every width

Scenario: The current destination is obvious
  Then the active tab is distinguishable without relying on colour alone
```

---

### SHELL-2 — The boot state

**Status: 📋 Planned.** Proposal screen **9**. The smallest slice in the wave.

Replaces the stock two-circle `.loading-progress` spinner with the illustration, keeping the brass
`--brand-accent` arc it already uses and reading the real `--blazor-load-percentage`. A markup swap
rather than new plumbing; the tilt is CSS on the static drawing, so there is no second asset.

> **⚠️ Two files, not one.** Both hosts carry their own `index.html`, and `NATIVE_PARITY.md` names
> keeping them in sync as a maintainer rule. A boot state that only ships on web is a bug on four
> native shells.

**And it is the payload, not just the markup.** The illustration is 2 MB as delivered. The boot screen
is the one place it is fetched *before* the app is usable, so an unoptimized asset here makes the very
problem it decorates worse.

**Acceptance criteria**

```gherkin
Scenario: It shows real progress
  Then the arc reads --blazor-load-percentage rather than animating on a timer

Scenario: Both hosts agree
  Then the web and MAUI index.html render the same boot state

Scenario: It does not cost what it saves
  Then the illustration is optimized before it lands in the boot path
```
