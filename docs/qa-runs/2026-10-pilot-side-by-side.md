# Pilot side by side (Pkg J.7, #199) — 2026-10-29

JiggerJot at the same app commit, two ways: the **baseline** is `develop` at `c8b6259` (the platform's code in the repo,
stamp `e7a2962`); the **pilot** is `packaging/pilot` at `4488414` (the platform's packages, `1.0.0-beta.3`), whose app
content is that same commit's. Done when nothing differs. Nothing does, except what the packages move on purpose,
each listed below with where it went.

## Both suites

| | Baseline | Pilot |
|---|---|---|
| `Core.Tests` | 208 ✅ | — (the app's four suites joined `Api.Tests/App/Catalog` in J.4) |
| `Api.Tests` | 1345 ✅ | 470 ✅ |
| `Ui.Tests` | 350 ✅ | 298 ✅ |
| `ui-js` | — (inside the platform's js tests) | 3 ✅ |

The difference is the platform's own tests, which run in perezosoft-platform now. Compared by `Class.Method` from
`dotnet test --list-tests`:

- **86 classes in both**, every JiggerJot class among them; 82 with the same tests.
- **Baseline-only: 154 classes, 1112 tests**, every one found in perezosoft-platform's suites. Three were renamed on
  the way: `TenantInvariantTests`, `TenantScopeFilterTests` and `TenantStampingInterceptorTests` are the packaged
  `TenancyTests` (Pkg 5.1b).
- **Four shared classes are smaller in the pilot: 42 cases, 35 names**, all in the platform's suites: four
  `ArchitectureTests` facts about the platform's own tree, two `NativeChromeGateTests` and two `NativeShellGateTests`
  facts about files the packages own (theme.js, the publish script; J.4), and 34 `SessionKeepAliveTests` cases that are
  the platform's `Client.Tests`.
- **Pilot-only: 3 classes, 26 tests**, the inherited suites from `Perezosoft.Platform.Testing`: `CompositionSmokeTests`,
  `OwnershipTests`, `TenancyTests`.

## The schema, on a copy of staging

Two Neon branches of the staging database (project Jigger-Jot, parent `production` at LSN `0/3051448`): `pilot-j7` for
the baseline, `pilot-j7-pkg` for the pilot. Staging's history ends at `20261009184424_CocktailGlassRequired`: 2 users,
1 household, 651 cocktails, none without a glass. Each API host started on its own branch as staging starts it
(`Production`, migrations on start), mail pinned to the local Mailpit, then stopped at ready.

| | Baseline host | Pilot host |
|---|---|---|
| Migrations applied | none ("already up to date") | `20261029165730_UserMfaFailedAttemptCountDefault` (`ALTER … SET DEFAULT 0`) |
| `/health/ready` | 200 | 200 |
| `/api/version` | `platform: e7a2962…` | `platform: 1.0.0-beta.3` |

- `pg_dump --schema-only`: **identical**, before either host, after the baseline and after the pilot. The catch-up
  migration restates a default the history had already set.
- `pg_dump --data-only`: **identical** but for the pilot's one `__EFMigrationsHistory` row.

## E2E and Postman against both API hosts

The same tests against both stacks: the baseline's `E2E.Tests` build (the platform's journeys and JiggerJot's, 58 in
all) and its Postman collection. Each stack got a fresh `e2e` database (the seed is the app's `CatalogSeeder`, the same
in both) and jigger-jot's compose Postgres and Mailpit, wired as `tools/e2e.ps1` wires it.

| | Baseline stack | Pilot stack |
|---|---|---|
| E2E, main lane (billing on) | 54/54 ✅ | 54/54 ✅ |
| E2E, gates lane (billing off, green list) | 4/4 ✅ | 4/4 ✅ |
| Postman: requests / assertions / failed | 102 / 121 / 14 | 102 / 121 / 14 |

- **Postman results are identical request by request**: same sequence, same status code, same pass or fail on every
  assertion. All 77 JSON bodies have the same shape (keys and types; ids and times vary).
- The 14 Postman failures are the same on both: requests that need a value a person types (a TOTP code, an id from
  another request, a recipe body). The run used `newman@6` with a 7 s pause between requests, so the Mailpit fetch
  reads the code the outbox has just sent. It used a copy of the collection without `Logout`, `⚠ Delete my account` and
  `MFA — verify (step-up)`: the collection is built for clicking through, and a straight run signs itself out and then
  deletes its own account.

## Android

- `dotnet build src/Maui -f net10.0-android`: **0 errors on both** (the one warning is `adb reverse` with no emulator
  attached).
- **The device leg** (CI's manual *Run workflow*, `devices`) needs the branch on GitHub; it runs when the pilot is
  pushed. jigger-jot is public, so its minutes are free (~30).

## Noted

- Running the baseline's E2E against the pilot is the stronger check: the pilot's own E2E project keeps only the
  starter's journeys and JiggerJot's (28, J.4). The platform's journeys run in perezosoft-platform.
- Both hosts log the same two warnings on the staging copy: billing off while one household holds a Stripe-managed
  subscription (set off for this run; staging runs it on), and no https port for redirects (http only here).
