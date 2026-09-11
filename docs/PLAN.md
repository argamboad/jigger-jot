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
10. **On "merged", update the Slice Board** before anything else — it is the only view of this
    project the maintainer has that is not a diff, and a board that lags is worse than no board.
    `https://claude.ai/code/artifact/a9fed2f8-60e9-478e-9030-864170fab1d7`; read it first, then
    republish to the same URL. Move the slice out of "next up", add what it shipped, and re-run the
    three counts in the footer: merged PRs (`gh pr list --state merged`), unit tests (Core + Api +
    Ui), and browser journeys (`[Test]` in `E2E.Tests`). If a slice answered one of the three open
    questions, mark it answered there too.

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

**Merged into `develop`** (PRs #1–#19): the domain model and both tenancy walls, the seed catalog,
browse, detail, the measurement preference, the shelf, the makeable engine, one-ingredient-away, the
recipe's own makeability, custom ingredients, the catalog filters, forking, authoring, and the
brand-token stylesheet fixes. **Every flow in `FEATURES.md` §8–§15 is covered.** §7, the onboarding
wizard, was the last one unbuilt — it is the uncommitted slice below.

The same stylesheet fixes went upstream the same day: `perezosoft-platform` #222 and `vuelto` #57.
All three apps inherited the defect from the platform's `app.css`.

**Merged since** (PRs #20–#24): the UI wave's plan, ALMOST-2's ranking query, Marga's component, and
**MARGA-2**, the signed-in home screen — which closed the `<!-- TODO -->` the platform's welcome card
had carried since day one.

**Merged since** (PRs #25–#28): **INV-3**, the shelf rework; **MARGA-3**, the two empty states, which
settled what a first bottle is and closed the `MARGA` epic; **SHELL-1**, the responsive shell, which
settled the wide-screen question; and **SHELL-2**, the boot state. **The UI wave is complete.**

**Built, verified, uncommitted** on `feat/ONBOARD-1-wizard` (branched from `develop`):

- **ONBOARD-1**: the first minute, and **the last unbuilt flow in `FEATURES.md`**. Two steps over the
  same data and the same control as the shelf — guided, not a second way to record what you own.
- **The staples needed no second curated list.** `MARGA-3` had already settled the honest definition,
  so the wizard asks `/api/cocktails/starters` for twelve and puts them on screen already ticked.
- **Nothing is written until Finish**, which is why the write is new: `PUT /api/inventory` takes the
  whole shelf in one request. The per-ingredient write stays for the shelf screen, where a tick *is*
  the decision.
- **Offered, never forced.** No redirect, no dismissal flag, and therefore no "has this household been
  onboarded" fact to store — which matters, because `Tenant` is the platform's and holding one bit of
  app state there is the wrong direction (golden rule 8).

## The UI wave — 2026-09-10

A design proposal arrived as three Claude Design documents: the current UI recreated, a bug list
(implemented, PR #19), and **nine screens at three widths in both themes**, which introduce a
character called **Marga**.

**Read this before planning around her.** Marga is a drawn bartender who says **fixed lines with real
data in them**. There is no model behind her, she generates nothing, and every sentence is a localized
resource string with placeholders. Reading her as an assistant makes the wave look four times larger
than it is.

Sorted by what the data has to supply, only one thing in the whole proposal is new engine work:

| Her line | Where the data comes from |
|---|---|
| "Twelve tonight" | exists — `makeable=true` already returns the total |
| "Kahlúa's fine, that's what I'd pour" | exists since MAKE-1, reworded |
| "Pick up triple sec and I can make you four more" | **new** — `ALMOST-2` |

Everything else is presentation over data already on the page. **Eight of the nine screens already
exist and ship today**; the proposal changes them. `Home.razor` is the exception at 34 lines, and it
still carries `<!-- TODO: app-specific content goes here -->`.

## What is next, in order

Each is one branch off `develop`, one PR, after the previous one is merged.

| # | Slice | Flow / screen | What it is |
|---|---|---|---|
| 1 | **ONBOARD-1** | §7 | The uncommitted work above. Waiting on C+P+PR. **The last unbuilt flow** — with it merged, every flow in `FEATURES.md` §7–§15 is covered. |

**After this one the list is empty, and what follows is a decision rather than a queue.** Candidates,
none of which blocks another: the 969-recipe catalog behind a flag (a flag, not a slice); the Savoy
transcription-source question (JJ-032, open, blocks nothing); the two scrape-merged recipe lines;
**`AUTHORING-2`** — editing a cocktail, including a fork, the one outstanding story inside an epic
marked complete; and a QA pass over the whole app, since the UI wave changed every screen it has.

**Three questions to settle before the slices that need them. All three are now settled, each by the
slice that needed it — the answers are kept here because the reasoning outlives the slice.**

1. ~~**What is the best first bottle for an empty shelf?**~~ **Answered by `MARGA-3`.** It is the
   ingredient the most recipes **ask for**, among those the household does not already have — required
   lines only, substitutions ignored, served by `GET /api/cocktails/starters`. The two readings that
   lost: "the bottle that makes the most drinkable on its own" is useless, because one bottle alone
   makes very nearly nothing; and a starter *set* is better advice but is `ONBOARD-1`, not an empty
   state. The choice carries an honesty constraint into the copy — the number is how many recipes ask
   for the bottle, never how many it would unlock.
2. ~~**How does the shelf footer stay current?**~~ **Answered by `INV-3`.** The write cannot carry the
   total: a feature slice may not reference another slice (R7/TR-9), and copying the makeability query
   into the inventory endpoint to get around that would leave the app with two definitions of makeable.
   So the screen re-asks `?makeable=true&pageSize=1` — but only once the ticking stops. Each tick
   cancels the pending ask, and an answer overtaken by a later tick is discarded rather than written
   over a fresher one, so a burst costs one request rather than one per checkbox.
3. ~~**Do wide screens change too?**~~ **Answered by `SHELL-1`: yes.** Lifting the destinations only
   inside the tab bar would have moved the inconsistency rather than settled it, so the app's own
   links are raised at every width — 85% white, full white and bold for the current one. The account
   cluster is deliberately untouched: the complaint was that the destinations read as less important
   than the furniture, and the fix is to raise the destinations, not to dim shared platform chrome
   this slice has no business redesigning.

**One thing that is already decided.** Selected shelf pills are **filled**, not outlined: the catalog
screen already uses outlined pills for filters, and the same shape one screen apart must not mean two
different things.

**One number worth watching.** The illustration is 2 MB as delivered, at 1254×1254. It must be
optimized in `MARGA-1`, before `SHELL-2` puts it in the boot path where it is fetched before the app
is usable.

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

**Changing a control and updating only the fixture named after the screen.** INV-3 turned the shelf
checkboxes into pills, which are hidden inputs driven through their labels — so `CheckAsync` on the
input stops working. `ShelfJourneyTests` was updated to click the label; `MakeableJourneyTests` and
`CocktailBrowseJourneyTests` both stock a shelf before they can test anything of their own, and both
had their own private copy of "tick an ingredient". Three journeys went red in CI for one change that
had been made and verified correctly. **Before changing a shared control, grep the E2E project for
everything that drives it** — and if more than one fixture drives it, the helper belongs in
`E2ETestBase`, which is where `SetShelfAsync` now lives.
