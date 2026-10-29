# JiggerJot's docs

JiggerJot is an app on the Perezosoft platform's packages (`Perezosoft.Platform.*`, ADR-034): the platform's
documents describe the platform, these describe JiggerJot.

| Document | What |
|---|---|
| [`PLAN.md`](PLAN.md) | **Read first, every session** — the gates, the slice ritual, where things stand, the backlog |
| [`PROJECT_BRIEF.md`](PROJECT_BRIEF.md) | What JiggerJot is, the core loop, MVP in / out (the OUT list) |
| [`FEATURES.md`](FEATURES.md) | User flows: the platform's constant flows (§1–6) and the app's (§7 on) |
| [`DATA_MODEL.md`](DATA_MODEL.md) | Entities, relationships, derived rules (makeable, almost-makeable, units) |
| [`DECISIONS.md`](DECISIONS.md) | JiggerJot's decisions, `JJ-…` |
| [`QA_TEST_PLAN.md`](QA_TEST_PLAN.md) | Manual QA across web and the native shells; the PDFs are generated from it (`dotnet platform qa-guide`, `qa-runlog`, `check-qa`) |
| [`stories/`](stories/) | The app's epics, as Gherkin |
| [`postman/`](postman/) | The API collection and its environments (ADR-023) |
| [`brand/`](brand/) | The brand sources and `build_assets.py`, which renders every raster |
| [`qa-runs/`](qa-runs/) | Filled QA run logs |

**The platform's documents**, at its `develop`:
[NEW_APP_GUIDE](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/NEW_APP_GUIDE.md) (taking a platform release, the add-a-slice path) ·
[WAYS_OF_WORKING](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/WAYS_OF_WORKING.md) (slices, stories, commits, the add-a-slice checklist) ·
[ARCHITECTURE](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/ARCHITECTURE.md) · [FLOWS](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/FLOWS.md) ·
[DEPLOYMENT](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/DEPLOYMENT.md) (Render, Neon, Brevo; the image's `nuget` secret) ·
[MOBILE_TESTING](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/MOBILE_TESTING.md) · [LOCALIZATION](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/LOCALIZATION.md) ·
[REBRANDING](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/REBRANDING.md) · [TECH_STACK](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/TECH_STACK.md) ·
[DECISIONS](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/DECISIONS.md) ·
[FOUNDATION_RULES_v3](https://github.com/argamboad/perezosoft-platform/blob/develop/docs/audits/v4-2026-09/FOUNDATION_RULES_v3.md) (the binding rules).

## JiggerJot's specifics the platform's documents do not know

- **Ports** (`local-ports.props`, `dotnet platform ports`): API https 7360 / http 5438, Web https 7208 / http 5369,
  Postgres 5435, Mailpit 1027 (SMTP) / 8027 (UI), the app container 8280. The Android emulator reaches the API at
  `http://localhost:5438` through `adb reverse tcp:5438 tcp:5438`.
- **Staging**: `https://jiggerjot-staging.onrender.com` (the `jiggerjot-staging` service in `render.yaml`). Its Docker build
  restores the platform's packages through the secret file `nuget` (the feed token, `read:packages`), set on the service.
- **OAuth redirect URIs** to register with Google and Microsoft: `https://localhost:7360/signin-{provider}` (web/desktop),
  `http://localhost:5438/signin-{provider}` (Android emulator), `https://jiggerjot-staging.onrender.com/signin-{provider}`.
- **Rebranding**, on top of the platform's REBRANDING checklist: the mark ships **twice**, one per ground —
  `icon_light.svg` (copper, light surface) and `icon_dark.svg` (bone, night surface) in `src/Shared.Ui/wwwroot/brand/`,
  swapped by `app.css` (JJ-037). Replace both, then `python docs/brand/build_assets.py` renders every raster. The mails
  carry their own copies in `src/Host/Email/Assets/` (`lockup.png`, `marga.png`, the serif) for `JiggerJotEmail`.
