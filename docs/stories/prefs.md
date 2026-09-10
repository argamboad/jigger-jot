# Epic PREFS — per-user preferences that actually follow the user

> Epic key: `PREFS`. Builds on THEME-1 (`theme.md`) and the locale playbook (B7-3/MITI-5).
> Fixes the QA-I18N-02 failure (locale never persisted server-side from the UI) and the
> "theme applies only after a manual reload" instability. ADR-022.

### PREFS-1 — Locale and theme follow the user across devices

**As a** signed-in user
**I want** my language and theme choices saved to my account and applied on every device I sign in on
**So that** the app looks and reads the same everywhere without re-picking my preferences

**Context / notes:**
- Storage stays as-is (ADR-C2 carve-out): `User.Locale` + `User.Theme` columns, `PUT
  /api/auth/locale` / `PUT /api/auth/theme`, claims on the JWT. No schema change.
- Pre-slice defects this story closes:
  1. The only `LanguageSwitcher` lived on the login page, where the user is always anonymous —
     `PUT /api/auth/locale` was unreachable from the UI, so `User.Locale` was always null.
  2. The server→device reconcile ran only in `MainLayout.OnInitializedAsync` (cold start), so
     OTP/MFA sign-ins (soft navigations) didn't apply the saved preferences until a manual reload
     (`ThemeJourneyTests` had a workaround `ReloadAsync` proving it).
  3. The B7-3 in-process culture switch doesn't load WASM satellite resource assemblies, so even
     when the reconcile ran, strings could stay English until the next reload.
  4. Theme "system" was stored as null, indistinguishable from "never chose", so switching back
     to System on one device never propagated to others.
- Design (ADR-022): a `/settings` Preferences card gives the switchers a signed-in home;
  `AuthService` raises a `SignedIn` event so `MainLayout` reconciles on every sign-in path; on a
  locale mismatch the reconcile persists + full-reloads once (satellite-assembly-safe — reload
  happens only on an actual mismatch, so B7-3's "guaranteed double reload" stays fixed); theme
  "system" is stored explicitly; and a device preference with no server counterpart is adopted
  server-side on sign-in (so a pre-auth login-page choice becomes the account preference).

**Acceptance criteria**

Scenario: signed-in user changes language from Settings
  Given I am signed in
  When I open /settings and choose Español in the Preferences card
  Then the UI re-renders in Spanish
  And my user record's locale is "es" (PUT /api/auth/locale)

Scenario: locale follows the user to a fresh browser
  Given my saved locale is "es"
  When I sign in on a browser that has never seen my locale
  Then the app renders in Spanish without me touching the switcher
  And the device store now caches "es" for the next cold start

Scenario: pre-auth device choice is adopted on sign-in
  Given I chose Español on the login page (anonymous, device-local only)
  And my user record has no saved locale
  When I sign in
  Then my user record's locale becomes "es"

Scenario: theme applies immediately after an OTP sign-in (no manual reload)
  Given my saved theme is "dark"
  When I sign in with an email code on a fresh browser
  Then the page switches to dark as part of signing in
  And no manual reload is needed

Scenario: choosing System is a real preference that propagates
  Given my saved theme is "dark" and a second browser shows dark
  When I switch my theme to System on the first browser
  Then my user record stores "system" (not null)
  And the second browser returns to the OS scheme on its next sign-in/reload reconcile

Scenario: impersonation never rewrites the impersonated user's preferences
  Given I am platform staff impersonating a user
  Then no preference adoption PUT is issued on my behalf

**Out of scope:** notification preferences (NOTIFY-2 owns them); a generic preferences
table/endpoint (declined — see the 2026-07-14 discussion; storage stays per-column); native
(MAUI) UI changes beyond what the shared RCL provides automatically.

**Definition of done:** tests written first (TDD); Api.Tests cover the "system"-is-stored rule;
E2E covers the cross-browser locale journey and the reconcile-on-sign-in theme journey (workaround
reload removed); QA_TEST_PLAN updated in the same PR (R31) + PDFs regenerated; ADR-022 added;
Postman descriptions updated; app working.

---

### PREFS-2 — Measurement preference

**Status: ✅ Implemented.** `PUT /api/auth/unit-system` and the `UnitSwitcher` beside language and
theme in Settings.

**As a** reader of recipes
**I want** to choose whether amounts show in millilitres or ounces
**So that** a recipe reads in the measures I actually pour

**Context / notes.** CKTL-3 built the conversion and read `User.PreferredUnitSystem`, but nothing set
it — so every reader saw recipes as authored, which is the correct behaviour for a null preference
and not yet a choice anyone could make. This closes that.

**Three options, and "as written" is one of them.** Null is a real value meaning *never chose*, and a
reader can return to it deliberately: it is the only way to read the 1930 recipes in the book's own
words. Collapsing null to a default would have removed that, so the profile response carries null as
null rather than substituting anything.

**"Neutral" is rejected with a 400.** Neutral is a property of a *unit*, not something a reader can
prefer — "show me everything in dashes" is not a request anyone can act on, and quietly accepting it
would store a value that does nothing.

**No device-local copy, unlike theme and language.** Nothing renders differently until a recipe is
opened, and the server does the conversion, so there is nothing to apply before first paint and no
`localStorage` bootstrap to keep in step. The switcher reads the server and writes to it. That makes
this the simplest of the three preferences despite looking like the same shape.

Impersonation is blocked by the same server-side guard as the other preference writes (ADR-022): a
staff session must not change the target's account settings.

**Acceptance criteria**

```gherkin
Scenario: Choosing imperial changes what a recipe says
  Given a recipe written in millilitres
  When I choose imperial in settings and open it
  Then the amounts read in ounces

Scenario: As written is a choice I can come back to
  Given I have chosen imperial
  When I choose "as written"
  Then recipes read exactly as their books wrote them

Scenario: A preference that is not a system is refused
  When something that is not metric or imperial is sent
  Then the request is rejected
  And "neutral" is refused too, because no reader can prefer it

Scenario: The switcher shows where I actually am
  When I open settings
  Then the control reflects the preference stored on my account
```

**Tests.** `tests/Api.Tests/Catalog/UnitPreferenceTests.cs` (seven) and a journey in
`tests/E2E.Tests/CocktailBrowseJourneyTests.cs` that reads the same Negroni before, during and after
the choice (suite 38 → 39). The conversion arithmetic itself is tested in
`tests/Core.Tests/AmountDisplayTests.cs`, from CKTL-3.
