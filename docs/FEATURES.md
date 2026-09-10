# Features & User Flows

> How the product behaves, flow by flow. The "why/what" lives in `PROJECT_BRIEF.md`; structures
> live in `DATA_MODEL.md`. Fill one section per major flow. Pattern shown below.

## Flow template (copy per flow)

### N. <Flow name>
**Goal:** <what the user is trying to accomplish>

Flow:
1. <step>
2. <step>

Notes: <edge cases, derived-rule references, tenant-scoping considerations>

---

## Constant flows (auth + tenant onboarding — always present)

> These reflect the platform's **custom JWT + refresh-token** implementation (no ASP.NET Core
> Identity). Endpoint names match `AuthController` / `HouseholdInvitationsController`. The
> step-by-step QA scripts live in `docs/QA_TEST_PLAN.md`.

### 1. Sign in via OAuth (Google / Microsoft / future providers)
**Goal:** a user authenticates using their existing identity provider account.

Flow:
1. User clicks "Continue with Google" (or Microsoft) on the login page.
2. Browser navigates to `GET /api/auth/login/{provider}`; the API challenges the provider.
3. Provider redirects to `GET /api/auth/callback/{provider}`; the external principal rides a
   temporary `External` cookie scheme.
4. `UserService.GetOrCreateUserAsync` resolves the account: known `UserLogin` → that user; else a
   matching **verified** email → links the new provider (an **unverified** email match is refused —
   the takeover guard); else a brand-new `User` + fresh `Tenant` + owner `TenantMembership` are
   created atomically.
5. The API sets the refresh-token cookie and redirects to the client, which calls
   `POST /api/auth/refresh` to obtain its JWT access token.

Notes:
- Adding a provider = one `.AddXxx()` in `ServiceCollectionExtensions` + provider registration.
- A user can link multiple providers (rows in `UserLogin`); there is no `AspNetUserLogins`.
- **MFA step-up (ADR-012):** if the resolved user has MFA enabled, primary auth does **not** issue a
  full session — it returns a short-lived signed **challenge** and the callback redirects to
  `/login?mfa=<challenge>`; the client completes step-up via `POST /api/auth/mfa/verify` (a TOTP or
  recovery code). See §6 for enroll/manage.

### 2. Sign in via magic link (web, passwordless)
**Goal:** a user signs in without a password by clicking an emailed link.

Flow:
1. User requests a link: `POST /api/auth/magic-link/send` (always 200 — no account is created yet,
   so the response can't be used to probe for accounts).
2. `PasswordlessService` stores a single-use, hashed `LoginToken` (`purpose = magic-link`, 15 min
   default) and emails the URL via `IEmailSender` (Mailpit in dev).
3. User clicks it → `GET /api/auth/magic-link/verify`; the token is validated and **consumed**
   (`consumed_at`). The account is resolved/created now (`GetOrCreateByEmailAsync`, marked
   email-verified), provisioning a tenant if new.
4. The API sets the refresh cookie and the client refreshes into its session.

Notes:
- Only the token **hash** is stored; lifetime is `Auth:MagicLink:TokenLifespanMinutes`.
- Single-use: a redeemed or expired link no longer works.
- **MFA step-up (ADR-012):** an MFA-enabled user is redirected to `/login?mfa=<challenge>` instead of
  a session; the client completes it via `POST /api/auth/mfa/verify`.

### 3. Email OTP sign-in (web + native)
**Goal:** authenticate with a one-time 6-digit code — the only passwordless method on native
clients, where a magic-link email can't return to the app.

Flow:
1. User requests a code: `POST /api/auth/otp/send` (always 200). A hashed `LoginToken`
   (`purpose = otp`, 6 digits, 10 min default) is stored.
2. User enters the code: `POST /api/auth/otp/verify`. On match it's consumed and the session is
   issued; wrong codes increment `attempt_count` and lock out after the max (default 5).

Notes:
- **MFA step-up (ADR-012):** on a correct OTP, if the user has MFA enabled the API returns an
  `{ mfa_required, challenge }` response (JSON path) rather than a session; the client completes it via
  `POST /api/auth/mfa/verify` with a TOTP or recovery code. Enforced on **every** sign-in path.
- Email OTP here is the passwordless *primary* factor; authenticator-app **TOTP** is the optional
  *second* factor (enroll/manage in §6). SMS OTP is deferred (needs a phone field + an SMS provider).

### 4. New-tenant onboarding (automatic)
**Goal:** a newly authenticated user lands in their own tenant with no extra step.

Flow:
1. On first sign-in (any method) `UserService` creates the `User`, a fresh `Tenant`
   ("<name>'s Household"), and an owner `TenantMembership` in one transaction.
2. There is **no separate "create household" screen** and **no `tenant_id` on User** — tenancy is
   the membership. The user can rename the household later on `/household`.

### 5. Invite a member to the household
**Goal:** an owner **or admin** invites someone to their tenant.

Flow:
1. An owner or admin submits an email: `POST /api/household/invitations` (gated by
   `Permission.ManageMembers`, which both owner and admin hold — RBAC, ADR-009). A `TenantInvitation`
   is created (status `pending`, hashed token); inviting an existing member is refused (409), a pending
   invite for the same email is refreshed, not duplicated, and hitting the plan's seat cap returns
   **402 `seat_limit_reached`** (BILLING-5 — pending invites reserve a seat).
2. The raw token is returned once (revealed in the UI) **and** emailed as `/join?token=...`.
3. The invitee opens `/join`, signs in if needed, then `POST /api/household/invitations/accept`
   validates the token, moves their `TenantMembership` to the inviting tenant, and consumes the
   invite. A departing solo owner's empty tenant is dissolved (the re-home invariant).

Notes:
- `TenantInvitation.is_valid` = `status == pending AND !is_expired` (derived, not stored).
- An owner or admin can regenerate (new token; the old one dies) or revoke a pending invite.
- A user is always in exactly one tenant — accepting **moves** them, never adds a second.

### 6. Account settings (linked providers + language + theme)
**Goal:** manage per-user account settings on `/settings`.

- **Linked accounts:** `GET /api/auth/logins` lists linked providers;
  `POST /api/auth/link/{provider}` links another (refused if that identity belongs to someone else);
  `DELETE /api/auth/logins/{provider}` unlinks (email sign-in always remains, so this can't lock
  you out).
- **Language:** the switcher (Settings → Preferences card; also on the login page for pre-auth
  picks) persists the user's locale via `PUT /api/auth/locale`; it lands in the JWT on the next
  refresh and localizes the UI and outgoing emails. See `docs/LOCALIZATION.md`.
- **Theme (THEME-1 + PREFS-1):** a Light/Dark/System switcher in the header, Settings →
  Preferences, and the login page. Applies live via Bootstrap's `data-bs-theme`; persists
  device-locally (`localStorage["app_theme"]`, applied pre-paint by `theme.js`) and — signed in —
  server-side via `PUT /api/auth/theme` ("system" stored verbatim — ADR-022). See
  `docs/stories/theme.md`.
- **Preference sync (PREFS-1, ADR-022):** both preferences follow the *user*: reconciled on every
  sign-in (server value wins — theme applies live, a locale mismatch reloads once), and a
  device-local choice made before signing in is adopted into the user record when the account has
  none. See `docs/stories/prefs.md`.
- **MFA (authenticator TOTP; ADR-012):** enroll via `POST /api/auth/mfa/enroll` (returns an
  `otpauth://…` provisioning URI to render as a QR + one-time recovery codes), confirm possession with
  a valid code to enable, and disable/regenerate recovery codes from Settings. Once enabled, every
  sign-in path (§§1–3) requires the step-up (`POST /api/auth/mfa/verify`). The secret is encrypted at
  rest and never returned after enrollment; recovery codes are hashed + single-use, and are short,
  human-typeable `xxxxx-xxxxx` codes (unambiguous alphabet; entry is case/hyphen/space-insensitive).
- **Unit system (JiggerJot):** the `preferred_unit_system` toggle (metric / imperial) lives on the
  same Preferences card — see §15.

---

## App-specific flows

> JiggerJot's own flows. Numbering continues from the constant flows so cross-references stay
> unambiguous. "Household" is the tenant.

### 7. Onboarding wizard (new household)
**Goal:** a brand-new household has an empty inventory, so "what can I make?" would return nothing.
The wizard seeds initial inventory and avoids a dead first impression (JJ-021).

Flow:
1. The platform has just created the household and its first user (§4).
2. Wizard presents the shared ingredient catalog, organized by category, optimized for fast
   bulk-checking ("check what you have on your shelf").
3. Common staples may be pre-suggested to speed things up.
4. On finish, checked ingredients become available rows in `TenantInventory`.
5. User lands on "What can I make right now" — ideally already populated.

Notes: ice/water are assumed available and not part of the checklist (see DATA_MODEL derived
rules, JJ-020). Members who join an existing household by invitation (§5) skip the wizard — the
household already has an inventory.

### 8. Managing inventory
**Goal:** keep the household's available-ingredients list current.

Flow:
- A checklist of ingredients (shared catalog + this household's custom), grouped by category.
- Toggling an item writes/updates its `is_available` in `TenantInventory`; absence = not available.
- Add a **custom ingredient** inline (name + category + subcategory) → creates a tenant-owned
  `Ingredient` and is immediately checkable.
- Boolean only — no "running low" (pinned, JJ-023).

### 9. "What can I make right now" (the makeable engine)
**Goal:** the headline feature.

Behavior:
- Lists every cocktail (shared + custom) that is **makeable** for this household per the
  DATA_MODEL makeable rule (all required lines satisfied by available ingredients or their valid
  substitutes).
- Optional lines (garnishes) never block a result.
- When a result is shown thanks to a substitution, surface that ("using Kahlúa in place of Tia
  Maria") so the user understands why it qualified and what they'd actually pour.

### 10. "Almost makeable"
**Goal:** discovery + shopping driver (JJ-019).

Behavior:
- Lists cocktails where **exactly one** required line is unsatisfied (after substitutions).
- Each result names the single missing ingredient — the "buy this, unlock these drinks" hook.
- Presented as its own view or section adjacent to "what can I make."
- When **nothing** is one bottle away — the cold start, an empty shelf — the screen names a bottle to
  start from instead of stating a verdict: the ingredient the most recipes ask for, among those the
  household does not already have. It says how many recipes **ask for** it, never how many it would
  unlock; one bottle on an empty shelf makes very nearly nothing, and promising otherwise would be
  the one dishonest number in the app.

### 11. Browsing & filtering
**Goal:** explore the whole catalog, not just what's makeable.

Filters (combinable):
- **Makeable now** (on/off) — the toggle between "everything" and "what I can make."
- **By ingredient / category** — e.g. "everything with vodka." Matches by category (parent catches
  all children: "rum" → white + dark + spiced) **and** by ingredient name (JJ-016). Derived from
  recipe lines; no manual tagging (JJ-014).
- **By method** (shake / stir / …), **glass type**, **serving type** (shot / full drink).
- Results draw from shared + custom cocktails together.

### 12. Viewing a cocktail
Shows: name, recipe lines (ingredient, amount, unit, role grouping, notes), method, glass, serving
type, instructions. Amounts display in the **viewing user's** unit preference (convertible units
converted; neutral units as-authored). Indicates makeable / almost-makeable status and any
substitution in play.

### 13. "Create my own version" (fork)
**Goal:** let a household adapt a shared (or any) cocktail (JJ-002, JJ-013).

Flow:
1. From any cocktail, user hits **Create my own version**.
2. System creates a **full snapshot copy**: a new tenant-owned `Cocktail` + copies of all its recipe
   lines, with `forked_from_cocktail_id` set to the original (provenance only).
3. The copy is fully independent and editable; later edits to the original never propagate.
4. The fork appears among the household's custom cocktails.

### 14. Authoring a custom cocktail (from scratch)
Flow:
- Create a tenant-owned `Cocktail`: name, method, glass, serving type, instructions.
- Add recipe lines: pick ingredient (shared or custom), amount + unit, required/optional, role,
  order, notes.
- Same ingredient may appear on multiple lines.
- Immediately participates in makeable / filtering like any other cocktail.

### 15. Unit preference
- Per-user setting: metric or imperial (JJ-008), on the Settings → Preferences card next to
  language and theme (§6).
- Affects **display only**; storage is always as-authored (JJ-007).
- Two users in the same household may view the same recipe in different units.

## Flow-to-rule cross-reference
| Flow | Key derived rule |
|------|------------------|
| Invite member (§5) | `TenantInvitation.IsValid` (`status == pending && !is_expired`) |
| Magic link / OTP sign-in (§2, §3) | `LoginToken` single-use (`consumed_at`) + expiry |
| What can I make (§9) | Makeable (`DATA_MODEL.md` derived rules) |
| Almost makeable (§10) | Almost makeable (N = 1) |
| Browsing / filtering by spirit (§11) | Spirit derived from recipe ingredients + categories |
| Any amount displayed (§12) | Unit display conversion |
| Onboarding / inventory (§7, §8) | Ice & water assumed available |

## Out of scope
- SMS OTP — deferred until phone-based OTP is needed (no phone field / SMS provider yet).
- Social login beyond Google + Microsoft — infrastructure is provider-agnostic; add per-app.
- JiggerJot pins (see the OUT list in `PROJECT_BRIEF.md`): publishing to a community pool,
  tenant-level substitutions, custom ingredients in the substitution graph, brand/product
  granularity, recipe-level substitutions, manual primary classification, user-added lookup values,
  "running low" inventory.

_(Authenticator-app **TOTP MFA** is **implemented** — ADR-012, §6 + the sign-in step-up in §§1–3.)_
