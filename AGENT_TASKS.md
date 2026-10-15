# SeeSharp — Agent Task Set

How to use this file: the **Shared Context** section below should be copied into
`CLAUDE.md` at the repo root so every agent session starts with it. Then run the
tasks one at a time, one session each, in order. Do not hand an agent more than
one task at a time — tasks 0, 2 and 3 each have a human review gate and the
later tasks depend on decisions made at those gates.

---

## Shared Context (→ `CLAUDE.md`)

**Product.** SeeSharp is an interactive C# training platform. A trainee installs
it as a .NET global tool, runs `seesharp`, and works through modules of reading,
questions and coding exercises. Coding exercises are graded by running unit
tests. The trainee writes code in JetBrains Rider; SeeSharp opens the relevant
file for them. A supervisor reviews completed work with the trainee afterwards.

**Audience and scale.** 1–5 trainees at a time, all at CargoNerds. Everything
runs locally on the trainee's own Windows machine. There is no server, no user
accounts, no authentication, no multi-tenancy. Do not add any.

**Repositories.**
- `seesharp` — this repo. The tool itself. Published as a `dotnet tool`.
- `seesharp-course` — content. A separate Git repo, cloned by the trainee. The
  tool reads it from disk at runtime.

Content is **never** compiled into the tool and the tool contains **no** course
material. Adding a module must require zero changes to this repo.

**Stack.**
- Blazor Server, launched by the CLI, served on localhost, browser opened automatically
- SQLite for progress (via `Microsoft.Data.Sqlite`)
- `YamlDotNet` for manifests, `Markdig` for instruction rendering
- `LibGit2Sharp` for admin-mode Git operations
- Target framework: match the TFM used by the existing sample code in the
  folder. If the samples don't set one, use the newest LTS installed.

**Language.** Source material is German. All UI strings go through `.resx` from
day one — never hardcode display text. Content manifests are locale-keyed so an
English translation can be added later without restructuring. German is the
default locale.

**Grading integrity.** Exercise tests are copied from the content package into a
clean temp directory at grade time and run against a copy of the trainee's
source. The trainee's working copy of the test project is never trusted and
never used for grading.

**Out of scope.** Cloud anything. Docker. Telemetry. A Rider plugin. Auto-grading
of free-text answers.

---

## Task 0 — Inventory and module map

**This task writes no application code.** Its only output is documentation and
draft content.

**Goal.** Work out what course material already exists in this folder and
propose how it maps onto SeeSharp modules.

**Steps.**
1. Walk the folder. List every file, with type and size.
2. Extract the text of the German PDF. Record its table of contents, chapter
   structure, and any exercises or questions it already contains.
3. Catalogue the sample code: for each project or file, what C# topic does it
   demonstrate, and does it look like a worked example, a starting skeleton, or
   a solution?
4. Cross-reference: which PDF chapters have matching sample code, which have
   none, and which sample code has no chapter.

**Deliverables.**
- `docs/INVENTORY.md` — the catalogue from steps 1–3.
- `docs/MODULE-MAP.md` — proposed module and exercise breakdown, ordered by
  dependency (what must be understood before what). For each proposed exercise,
  note which existing sample file it would be built from, and flag the ones
  with no existing material.
- `docs/GAPS.md` — topics the PDF covers that have no code, and vice versa.

**Constraints.** Do not reorganise, move, rename or delete anything in the
folder. Do not write any `.cs` files. Quote the PDF only in short excerpts —
summarise rather than reproducing it.

**Gate.** Stop. The module map is reviewed by hand before task 1 begins.

---

## Task 1 — Solution scaffold

**Goal.** Create the empty skeleton everything else slots into.

**Deliverable.**
```
src/
  SeeSharp.Cli/           # entry point, packs as dotnet tool "seesharp"
  SeeSharp.Core/          # domain: modules, items, progress, results
  SeeSharp.Content/       # manifest loading + validation
  SeeSharp.Grading/       # test runner
  SeeSharp.Ide/           # Rider integration
  SeeSharp.App/           # Blazor Server UI
tests/
  <one test project per src project>
```

**Acceptance.**
- `dotnet build` and `dotnet test` both succeed on a clean clone.
- `dotnet pack` produces an installable tool; `seesharp --version` works after
  `dotnet tool install --global --add-source ./nupkg SeeSharp.Cli`.
- `SeeSharp.Core` references nothing but the BCL.
- A GitHub Actions workflow builds, tests and packs on push.

**Constraints.** No feature logic yet. Empty projects with one placeholder test
each is the correct output.

---

## Task 2 — Grading runner

This is the riskiest component. It is built and proven before any UI exists.

**Goal.** Given a trainee's source directory and an exercise's test project,
produce a structured pass/fail result.

**Behaviour.**
1. Create a temp working directory.
2. Copy the trainee's source into it.
3. Copy the *pristine* test project from the content package over the top,
   overwriting anything of the same name.
4. Run `dotnet test --logger "trx;LogFileName=results.trx"` as a child process
   with a configurable timeout (default 120s) and a hard kill on expiry.
5. Parse the TRX into a result object: overall outcome, duration, and per-test
   name, outcome, message, stack trace.
6. Delete the temp directory unless a `--keep-artifacts` flag is set.

**Deliverable.** `SeeSharp.Grading` with a `GradingRunner` and a `GradingResult`
model, plus a `seesharp grade <exercise-path> <source-path>` CLI verb that
prints the result as text and JSON.

**Acceptance.** Integration tests over fixture exercises covering: all tests
pass; some tests fail with assertion messages surfaced; the source doesn't
compile; the test run hangs and is killed by the timeout; the trainee has edited
their copy of the test file to make it trivially pass, and grading still fails
because the pristine copy wins.

**Constraints.** No MSBuild API, no in-process test host — shell out to
`dotnet`. Capture stdout and stderr; never let the child process inherit the
console. Assume paths contain spaces (`D:\Rider Projects 2\...`) and quote
accordingly.

**Gate.** Demonstrate the CLI verb against two real exercises derived from the
sample code before continuing.

---

## Task 3 — Content model, loader and validator

**Goal.** Define the on-disk content format and load it safely.

**Layout.**
```
modules/
  01-<slug>/
    module.yaml
    01-<slug>/              # exercise
      manifest.yaml
      instructions.de.md
      rubric.md             # supervisor only, never rendered to the trainee
      starter/              # the project the trainee edits
      tests/                # authoritative test project
```

**Manifest shape.** `id`, locale-keyed `title`, `requires` (list of ids), and
`items`. Item types: `multipleChoice` (prompt, options, correct indices,
optional explanation), `open` (prompt, gates on *answered*, not correctness),
`coding` (openFile, openLine, test project path, requiredTests: `all` or a named
list). Design the schema so new item types can be added without breaking
existing manifests.

**Validator.** A `seesharp validate <course-path>` verb that fails on: schema
violations, a cycle or dangling reference in the `requires` graph, a referenced
file that doesn't exist, a missing locale for the default language, and — most
importantly — **an exercise whose tests already pass against the untouched
`starter/`**. That last check catches exercises accidentally shipped with the
solution in them, and it is the single most valuable rule here.

**Acceptance.** Validator unit tests for each failure mode. The two real
exercises from task 2 are restructured into this format and validate clean.

**Gate.** Schema reviewed before the UI is built against it.

---

## Task 4 — Progress store

**Goal.** Persist attempts locally.

**Behaviour.** SQLite under `%LOCALAPPDATA%\SeeSharp\progress.db`, created and
migrated on first run. Record *attempts*, not booleans: every submission stores
the item id, timestamp, the submitted answer or a copy of the submitted source,
the grading result, and the time spent. Current state is derived from attempt
history.

Add `seesharp export-review` producing a single zip containing submitted
sources, TRX files, answers to open questions and timestamps — this is what the
trainee hands the supervisor.

**Acceptance.** Migration runs idempotently. Deleting the DB loses progress but
doesn't crash the app. The export zip opens and contains real graded artifacts.

**Constraints.** Do not attempt to make the DB tamper-proof. Integrity comes
from the export bundle, which contains the evidence.

---

## Task 5 — Blazor shell and gating

**Goal.** The trainee-facing UI.

**Screens.** Module list with lock state and progress; exercise view rendering
`instructions.de.md` alongside the item list; question components for multiple
choice and open text; a results panel showing per-test outcomes with failure
messages; a dashboard of what's done and what's next.

**Gating.** An item unlocks when every id in its `requires` has a passing
attempt. Locked items are visible but not enterable, and the UI states which
prerequisite is missing.

**Acceptance.** A trainee can complete a module end to end: read, answer
questions, open the exercise, submit, see test results, unlock the next module.
All display strings resolve from `.resx`.

**Constraints.** Server-side Blazor only, no WASM. No JS framework. Keep it
plain — this is a training tool, not a product.

---

## Task 6 — Rider integration

**Goal.** Open the right file at the right line.

**Behaviour.** Resolve the Rider executable by, in order: a configured path in
settings; the JetBrains Toolbox install location; `rider` on PATH. Launch as
`rider --line <n> "<absolute path>"`. If no executable can be found, show the
absolute path with a copy button and a short note on creating the command-line
launcher from Rider's Tools menu. Cache the resolved path and offer a "re-detect"
action in settings.

**Acceptance.** Works with a Toolbox install where `rider` is not on PATH. Fails
gracefully with the fallback UI when Rider isn't installed at all. Paths with
spaces work.

**Constraints.** Never block the UI on the launch. Never assume PATH.

---

## Task 7 — Admin mode

**Goal.** Let the supervisor author content without leaving the app.

**Behaviour.** `seesharp --admin` enables an editor over the working copy of the
`seesharp-course` repo: create and edit modules and exercises, reorder, edit
instructions with a Markdown preview, manage question items. Git operations via
LibGit2Sharp: show the diff, commit, push, pull. **Validation from task 3 must
pass before a commit is allowed.** Also expose a review view that lists open-text
answers from an imported review bundle alongside the exercise rubric.

**Acceptance.** A new exercise can be created, validated, committed and pushed
entirely from the UI, and a trainee instance picks it up after pulling.

**Constraints.** No merge conflict resolution UI — on a conflict, say so and
point the supervisor at Rider. Admin mode must be unreachable without the flag.

---

## Task 8 — Exam mode

**Goal.** Practice exams and the real exam, using the same content pipeline.

**Behaviour.** An exam is a manifest listing item ids drawn from completed
modules, with a time limit. In exam mode: the AI tutor is disabled, test output
is withheld until submission, a timer runs, and the whole attempt is written to
the export bundle as one unit. Practice exams can be retaken freely and reveal
answers afterwards; the real exam can be taken once and reveals nothing —
results go to the supervisor.

**Acceptance.** Practice and real exams run end to end. Closing the app mid-exam
does not reset the timer or allow a clean retry.

---

## Task 9 — AI tutor

**Goal.** Optional guidance that doesn't hand over answers.

**Behaviour.** Opt-in per exercise. Context given to the model: the exercise
instructions, the trainee's current source, and the failing test output. Hints
escalate across three levels — a question that redirects attention, then a
conceptual pointer, then a worked explanation of the *concept* with different
example code. Never the solution. Every exchange is logged to the progress DB
and included in the export bundle, so the supervisor can see where trainees got
stuck. Disabled entirely in exam mode.

**Acceptance.** The tutor refuses to produce the exercise solution when asked
directly. Logs appear in the export bundle. Works when offline by failing
quietly rather than blocking the exercise.

**Constraints.** API key in user settings, never committed. Add a visible token
cost indicator.

---

## Task 10 — English localisation

**Goal.** Prove the i18n approach works end to end.

**Behaviour.** Add `.resx` for English, a language switcher, and `instructions.en.md`
support with a clear fallback to German when a translation is missing. The
validator warns — but doesn't fail — on missing translations.

**Acceptance.** Switching language changes all UI chrome, and exercises with an
English instruction file show it while others fall back visibly.

---

## Notes for whoever runs these

- Tasks 0, 2 and 3 have review gates for a reason. The module map, the grading
  runner and the content schema are the decisions that are expensive to change
  later.
- If an agent proposes adding a server, a login, Docker or a Rider plugin, that
  is scope drift — refuse it.
- Task 2 before task 5. A beautiful UI over a grading runner that can be cheated
  is worse than no tool at all.
