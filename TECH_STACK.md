# Tech Stack

> The chosen stack and the reasoning. Web-now decisions are committed; mobile is a recorded but
> deferred direction. Stack-specific concretions (schema dialect, seed format) follow from this.

## Summary

| Layer | Choice | Status |
|-------|--------|--------|
| Backend API | ASP.NET Core Web API | Committed |
| Web frontend | Blazor WebAssembly (WASM) | Committed |
| UI components | Shared **Razor Class Library (RCL)** | Committed (discipline rule) |
| Database | PostgreSQL | Committed |
| ORM | Entity Framework Core (Npgsql) | Committed |
| Auth | ASP.NET Core Identity | Committed |
| Non-web clients (mobile + desktop) | .NET MAUI **Blazor Hybrid**, reusing the RCL | **Deferred** (intended direction) |
| Hosting | TBD (cheap .NET API + static WASM + Postgres) | Deferred |

## Target versions (as of 2026-06-17)

**Policy: target the latest _stable_ release, never previews.** "Latest" means latest stable.

| Component | Version | Notes |
|-----------|---------|-------|
| .NET SDK / runtime | **.NET 10 (LTS)** | Newest stable *and* LTS (supported to Nov 2028). **Not .NET 11** — preview only until Nov 2026. |
| ASP.NET Core | 10 | Ships with .NET 10 |
| Blazor WASM | 10 | Ships with .NET 10; 2026 brought smaller JS bundles + better AOT |
| EF Core | 10.0.x | Stable |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.2 | Pulls EF Core 10.0.4, Npgsql 10.0.3 |
| ASP.NET Core Identity | 10 | Ships with .NET 10 |
| PostgreSQL (server) | 17 stable (18 emerging) | Decide at deploy. PG 18 + EFCore.PG 10 enables native `uuidv7()` via `Guid.CreateVersion7()` |
| .NET MAUI | 10 | Deferred — only when non-web clients (mobile + Win/macOS desktop) begin |

> **Keep current:** pin to these majors; take minor/patch updates within the .NET 10 line. Re-evaluate moving to .NET 11 only after it reaches stable GA (Nov 2026), and even then weigh LTS (.NET 10) vs STS (.NET 11).
> **UUIDv7 note (design-time):** EF Core 10 on PostgreSQL 18 can use time-ordered UUIDv7 keys (`Guid.CreateVersion7()` → native `uuidv7()`), which are more index-friendly than random GUIDs. Worth considering for primary keys when the schema is built — not yet decided.

## Architecture shape

**Clean API boundary.** The Blazor WASM web app is a client of the ASP.NET Core API, like any
other client would be. The frontend never talks to the database directly. This boundary is the
durable architectural asset: any future client (MAUI mobile, a different web frontend, etc.)
consumes the same API.

```
            ┌─────────────────────────┐
            │  ASP.NET Core Web API    │
            │  + EF Core (Npgsql)      │──── PostgreSQL
            │  + ASP.NET Core Identity │
            └────────────┬────────────┘
                         │ HTTP (API boundary)
        ┌────────────────┴───────────────────┐
        │                                     │
┌───────────────┐                  ┌─────────────────────────┐
│ Blazor WASM   │  (NOW)           │ MAUI Blazor Hybrid      │  (LATER)
│ web app       │                  │ mobile/desktop shell    │
└───────┬───────┘                  └───────────┬─────────────┘
        │                                       │
        └──────────────┬────────────────────────┘
                        │  both consume
              ┌─────────────────────┐
              │ Shared Razor Class  │
              │ Library (UI)        │
              └─────────────────────┘
```

## The RCL discipline (present-day rule)

**Blazor UI components live in a shared Razor Class Library, not inline in the web app project.**
This is the single rule that keeps future non-web clients cheap: a future MAUI Blazor Hybrid app
(mobile **and** Windows/macOS desktop) reuses the same components from the RCL rather than
rewriting the frontend. Reuse won't be 100% (navigation and some platform-specific bits differ),
but it captures the large majority of the UI — and the RCL now protects three client types (web,
mobile, desktop), strengthening the case for the rule.

Cost now: near-zero (just where components live). Cost of retrofitting later: high. So we pay it
up front.

## Why these choices

- **ASP.NET Core Web API** — the developer's strongest area; best-in-class for APIs; excellent
  tooling and Claude Code support; the durable asset behind any client.
- **Blazor WASM (over Server)** — preserves the "frontend is just another API client" boundary
  that the whole architecture leans on; 2026 Blazor improvements substantially shrank JS bundle
  size and improved AOT, easing WASM's historical download-size drawback. Server was a reasonable
  alternative (simpler start, lighter first load) but couples UI to the server and holds a live
  per-user connection — at odds with the clean boundary.
- **PostgreSQL** — free, portable, cheap to host anywhere; handles the model (self-referencing
  categories, substitution graph, sparse inventory) easily. Chosen over SQL Server for economy
  and portability on a solo project.
- **EF Core (Npgsql)** — default .NET ORM; first-class Postgres support; maps `DATA_MODEL.md`
  to migrations directly (this is how the conceptual model becomes concrete schema).
- **ASP.NET Core Identity** — built-in user/auth; the Tenant (household) scoping layers on top as
  a query concern (each user belongs to a tenant).
- **MAUI Blazor Hybrid (deferred)** — the path for **all non-web clients: mobile and Windows/
  macOS desktop**. The only one that reuses the C# Blazor UI (via the RCL), not just the API.
  Windows + macOS desktop is MAUI's sweet spot, so desktop is purely additive to the mobile path —
  no new technology, same RCL. Recorded as the intended direction, **not built now**. MAUI carried
  notable quality/stability debate through 2025–2026, so final commitment is deliberately deferred
  until non-web client work actually begins — by then there's more signal, and worst case only the
  frontend is affected (the API is client-agnostic). Alternatives to reconsider at that point if
  MAUI quality disappoints: Uno Platform, Avalonia, or a JS frontend (e.g. React Native) against
  the same API. (Linux desktop is out of scope, which keeps MAUI a clean fit; were Linux ever
  required, Uno/Avalonia would be the tilt.)

## Deferred sub-decisions (revisit when relevant, not now)

- **Final non-web-client framework commitment** — MAUI Blazor Hybrid is intended for mobile and
  Windows/macOS desktop; confirm when that work starts.
- **Hosting specifics** — pick based on budget when nearing deploy. Profile is undemanding
  (.NET API + static WASM assets + a Postgres instance).
- **Identity details** — external/social login, email confirmation flows, etc., as auth is built.

## Implications for other docs

- `SCHEMA.sql` / EF Core migrations are generated from `DATA_MODEL.md` in the Postgres/EF dialect.
- Seed data (categories, substitutions, starter cocktails) targets Postgres + EF seeding.
- User stories' acceptance criteria can now reference real Blazor screens / API endpoints.
