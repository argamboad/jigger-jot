# JiggerJot

**Mix what you have.**

A cocktail app for home bartenders that answers one question better than anyone else:
*"What can I make right now, with what I actually have?"* A household keeps a checklist of what's
on its shelf; JiggerJot shows every cocktail it can make now (substitution-aware), every one it is
exactly one ingredient short of, and lets members browse, fork, and author recipes on top of a
shared, seeded catalog.

## Status

Every MVP flow is built: the seeded catalog, the shelf, makeable and one-away, filters, forking and authoring,
onboarding, and Marga. Where things stand and what comes next is `docs/PLAN.md`.

## Run it locally

```bash
cp .env.example .env          # then fill it — at minimum Jwt__Secret (any ≥32-char string)
dotnet tool restore           # the platform's CLI, `dotnet platform`
dotnet platform up            # Postgres 17 + Mailpit, and the telemetry dashboard
dotnet run --project src/Host --launch-profile https   # API on https://localhost:7360
dotnet run --project src/Web                           # web UI on https://localhost:7208
```

Sign in with **"Email me a 6-digit code"** and read the code from Mailpit at
<http://localhost:8027>. No OAuth keys are needed for local development.

## Local ports

The three apps run side by side on one machine, each on its own block, so a stack never answers for another. This
table is the same in all three repos. A repo states its block ONCE, in `local-ports.props`: code reads it through the
generated `LocalPorts` class, `dotnet platform ports -Apply` writes it into the files that cannot read MSBuild (compose
defaults, launch profiles, appsettings, `.env.example`, the Postman environment, this row), and `LocalPortsTests` fails
on any file that disagrees, on a port number in code, and if two apps share a port.
API http is the port the Android emulator uses; the app container is `docker compose --profile app up`.

| App | Postgres | Mailpit SMTP | Mailpit UI | API https | API http | Web https | Web http | app container |
|---|---|---|---|---|---|---|---|---|
| perezosoft-platform | 5433 | 1025 | 8025 | 7160 | 5238 | 7008 | 5169 | 8080 |
| y-el-vuelto | 5434 | 1026 | 8026 | 7260 | 5338 | 7108 | 5269 | 8180 |
| **jigger-jot** (this repo) | 5435 | 1027 | 8027 | 7360 | 5438 | 7208 | 5369 | 8280 |

## Where things are

| Path | What |
|---|---|
| `docs/README.md` | Index of JiggerJot's documents, links to the platform's, and the app's ports, staging and OAuth URIs |
| `docs/PLAN.md` | Read first: the gates, the slice ritual, where things stand |
| `docs/PROJECT_BRIEF.md` | What JiggerJot is, the core loop, MVP in / out |
| `docs/FEATURES.md` | User flows: the platform's constant flows (§1–6) and the app's (§7 on) |
| `docs/DATA_MODEL.md` | Entities, derived rules (makeable, almost-makeable, unit conversion) |
| `docs/DECISIONS.md` | JiggerJot's decisions, `JJ-…` |
| `CLAUDE.md` | Operating manual for Claude Code, incl. the app's golden rules |
| `seed/` | Seed-catalog extractions and scripts (the 1930 Savoy Cocktail Book and the IBA list) |
| `docs/brand/build_assets.py` | Regenerates every brand PNG and the favicon from the SVG sources |
| `src/Host` | The app's server: its slices (`Features/`), entities, migrations, seed data and mails, composed in `AppComposition.cs` |
| `src/Shared.Ui` · `src/Web` · `src/Maui` | The shared Razor UI, the Blazor WASM host, the MAUI shells |
| `tests/` | xUnit (JiggerJot's own under `tests/Api.Tests/App/`), bUnit, Playwright, the Android smoke |

## Provenance

JiggerJot is built on **perezosoft-platform**, a fixed-stack multi-tenant SaaS foundation
(ASP.NET Core API + Blazor WebAssembly + shared RCL + PostgreSQL/EF Core + custom JWT auth, with
MAUI Blazor Hybrid shells), **taken as packages**: `Perezosoft.Platform.*` from the GitHub Packages feed, at the
version in `Directory.Packages.props` (ADR-034). Auth, households and invitations, roles, billing, background jobs,
notifications, file storage, GDPR export/erasure, audit, the admin console, localization and the
CI/CD pipeline all come from the platform and are not re-implemented here; its documents are at
<https://github.com/argamboad/perezosoft-platform/blob/develop/docs>. When an app document and the platform disagree, the platform wins (JJ-026).
