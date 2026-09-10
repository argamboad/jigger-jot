# The plan

> Written 2026-09-10 after a review of the first two build days. This is the sequenced list of what
> is next, the rules for how each item is done, and the mistakes that produced this document. Read it
> at the start of every session. It is short on purpose.

## The three gates — non-negotiable

1. **Branch only from `develop`.** Never from another feature branch. Never a stacked pull request.
   A PR that does not target `develop` gets no CI at all, because the workflow only fires for PRs
   into `develop` and `main`.
2. **Do not start the next slice until the user says "merged".** If a slice turns out to depend on
   something unmerged, **stop and say so**. Do not work around it. Working around it is how a branch
   ended up based on a feature branch.
3. **Do not commit, push, or open a PR until the user says "C+P+PR".** "go" means *build it*. It
   does not mean publish it. Every report ends with "waiting on your C+P+PR".

## The slice ritual — every time, in this order

1. **Read `docs/FEATURES.md` for the flow being built**, and quote the flow number in the report.
   Three merged slices shipped with requirements missing because this step was skipped. The
   golden rules and the decision log are constraints; `FEATURES.md` is the requirement.
2. Check the OUT list in `docs/PROJECT_BRIEF.md` and the story file for the epic.
3. Write the tests first. Run them; they should fail to compile.
4. Build API → Core → Infrastructure → Shared.Ui → Web. UI lives in the RCL, never the web app.
5. Update the Postman collection **by splicing a rendered object into the file**, never by
   round-tripping the whole JSON (that reflowed 2 000 lines once). The parity gate will catch a
   missing request; let it.
6. Add EN **and** ES strings.
7. Write or update the story file with Gherkin that maps 1:1 to the tests.
8. Run: Release build of API and Web (zero warnings), then `Core.Tests`, `Api.Tests`, `Ui.Tests`.
9. Report: what flow it implements, what was decided and why, what is deliberately out, the test
   counts. Then **wait**.

## Editing rules that came out of this review

- **Edit files with the Edit tool, not with scripted find-and-replace.** Scripted replacements
  collided three times: one produced an infinitely recursive test helper, one silently dropped a
  whole test, one applied to a file that had already been changed by the previous step.
- **Never write JSON or C# through a bash heredoc.** Backslashes get mangled and the file is left
  invalid. Write the content with the Write tool, or a Python script file, then run it.
- **Never hard-code the seeded catalog's size in a test.** Derive it from
  `CatalogSeeder.LoadCocktails().Cocktails.Count`. Six tests broke when the catalog shrank, and
  every one of them had been a claim about the data rather than about behaviour.
- **In a Playwright journey, wait on the write, not on the paint.** An optimistic UI re-renders
  before the request returns. Use `RunAndWaitForResponseAsync` on the PUT/POST, the way the theme
  journey does. And wait for the list to load before applying a filter to it.
- **A failing E2E job is read from the CI log first.** Reproducing locally requires pointing
  `src/Web/wwwroot/appsettings.json` at the http API, stale processes on 5269/5338 must be killed,
  and every process started by `dotnet run` outlives the shell. It cost an hour twice. Do it only
  when the log does not answer the question.

## Where things stand — 2026-09-10

**Merged into `develop`** (PRs #1–#16): CKTL-1 domain model and both tenancy walls; SEED-1 to
SEED-4; CKTL-2 browse; CKTL-3 detail + PREFS-2 measurement preference; INV-1 the shelf; MAKE-1 the
makeable engine; ALMOST-1 one-ingredient-away and the header's app/platform split; CKTL-4 the
recipe's own makeability; INV-2 custom ingredients; FILTER-1 the catalog filters. Staging deploys on
every merge and the seeder runs there against the enforcing RLS role. The shipped seed is a **starter
catalog** of 31 recipes; `python seed/build_cocktails.py --full` emits all 969.

**Built, verified, uncommitted** on `feat/FORK-1-create-my-own-version` (branched from `develop`):

- **FORK-1** to `FEATURES.md` §13: `POST /api/cocktails/{id}/fork` and a button on the recipe page.
  A snapshot copy, never a reference (JJ-013) — half the tests exist to prove the copy stays put when
  the original is edited, deleted or forked again.
- The source credit is deliberately **not** copied: the book wrote the original, not the household's
  version of it (JJ-032). Provenance rides on `ForkedFromCocktailId` and reads as "Based on X".
- `TenantId` set by hand on the cocktail **and** every line — nothing stamps either (JJ-031).

## What is next, in order

Each is one branch off `develop`, one PR, after the previous one is merged.

| # | Slice | Flow | What it is |
|---|---|---|---|
| 1 | **FORK-1** | §13 | The uncommitted work above. Waiting on C+P+PR. |
| 2 | **AUTHORING-1** | §14 | A household writes a cocktail from scratch. Glass and method optional (JJ-034). |
| 3 | **ONBOARD-1** | §7 | New-household wizard: tick a starter shelf, land on what you can make. |

Not on this list, deliberately: the Savoy transcription-source question (JJ-032, open, blocks
nothing), the two scrape-merged recipe lines, and restoring the 969-recipe catalog — that is a
flag, not a slice, and it happens when the slices are done.

## What went wrong, in one paragraph each — so it is not repeated

**Publishing without being asked.** After the user had typed "C+P+PR" explicitly a few times, "go"
started being treated as covering the whole cycle. It did not. One approval is not a standing one.

**Stacking.** Four seed PRs were opened against each other's branches. None of them had CI until
they were retargeted to `develop`, and retargeting alone does not fire CI — the PR has to be closed
and reopened. Then MAKE-1 was branched off INV-1's unmerged branch to dodge a compile error. The
compile error was the signal to stop.

**Building from the rules instead of the requirements.** MAKE-1 invented a `/make` screen and
surfaced no substitutions; INV-1 skipped inline custom ingredients; CKTL-3 skipped makeable status.
All three are stated in `FEATURES.md`, which was not opened before any of them.

**Two slices in one PR.** PREFS-2 was added to the CKTL-3 PR because "CKTL-3 alone would ship a
preference nobody could set". That reasoning was fine; the shape was not. It should have been
reported and left to the user.

**Tests that asserted the data.** "Total > 900", "page two of fifty has fifty", "unticking Campari
leaves nothing makeable" — a household with gin and sweet vermouth can still make seven drinks.
Assertions about the catalog's contents break the moment the catalog changes for unrelated reasons.
