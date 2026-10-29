# Progress store (task 4)

Local, append-only record of what the trainee did, as implemented in
`src/SeeSharp.Core/Progress.cs` (domain, BCL only) and `src/SeeSharp.Progress`
(SQLite store, review export). `AGENT_TASKS.md` task 4 as amended by
`SKILL-MODEL.md` §2 and §5. The user was away; everything not spelled out there
is marked **Decision**.

## Where

`%LOCALAPPDATA%\SeeSharp\progress.db`, created with its folder on first use.
`new ProgressStore(path)` overrides the location (tests use a temp file; the
real one is never touched by tests). Microsoft.Data.Sqlite, hand-written SQL,
one open connection guarded by a lock — one trainee, one browser.

**Decision — connection pooling is off** (`Pooling=false`) so disposing the
store releases the file immediately; deleting the file while the app is closed
simply yields an empty store on the next start.

## Schema and migrations

`PRAGMA user_version` counts the scripts in `ProgressStore.Migrations` that have
run; each new schema change is one more script appended to that array, run in a
transaction. Running the store twice on the same file is a no-op the second
time (tested). Never edit a published script.

```
attempt          id, exercise_id, item_id, timestamp (ISO 8601 UTC), time_spent_ms,
                 passed, answer, source (zip BLOB), grading_json, trx, exam_run_id (migration 2)
skill_transition id, skill_id, state (1 introduced, 2 practiced, 3 applied), timestamp, cause
exam_run         id, exam_id, real, started, deadline, seed, items ("exercise/item" per line), submitted
tutor_exchange   id, exercise_id, timestamp, level (0 judgement, 1–3 hints), question, answer, provider, model,
                 input_tokens, output_tokens, cost_usd (null = price unknown)            (migration 3)
                 item_id, verdict ("pass"/"revise" for a judgement, else null)           (migration 4)
remark           id, trainee, exercise_id, item_id, attempt_id, file, line, text, shared, received,
                 timestamp                                                               (migration 5)
```

Attempts are never updated or deleted. There is no `exercise passed` column,
no `item passed` column, no `skill` table: all of that is derived. Remarks are
not progress and not append-only: they are the supervisor's notes, shared or
deleted at will (docs/UI.md "Reviewer mode").

## What an attempt stores

`Attempt(ExerciseId, ItemId, Timestamp, TimeSpent, Passed, Answer?, Source?, GradingJson?, Trx?)`

- **multipleChoice**: `Answer` = chosen indices, `"0,2"`. Passed when the chosen set
  equals `correct`.
- **open**: `Answer` = the text. Passed when non-blank and the AI tutor's verdict
  is not `revise`; without a verdict (no tutor, tutor failed, exam) non-blank
  passes as before. Two failed attempts close the item like a quiz question
  (`ProgressDerivation.PassedItems`), see UI.md "AI tutor".
- **review**: `Answer` = optional note. Passed when the trainee marks it done.
- **coding**: `Source` = zip of the trainee's source directory without
  `bin`/`obj`; `GradingJson` = the `GradingResult` serialised like
  `seesharp grade --json`; `Trx` = raw `results.trx` when the run produced one.
  Passed when the run passed or, with `requiredTests`, when every named test
  passed.

**Decision — `Passed` is decided when the attempt is made**, by
`Attempts.MultipleChoice/Open/Review/Coding` in `SeeSharp.Progress`, and stored.
The rule per item type is the content model's, not the store's; storing the
verdict keeps derivation independent of the manifest version that produced it.

**Decision — `GradingResult` gained a `Trx` property** (`[JsonIgnore]`, so
`seesharp grade --json` is unchanged). The runner reads `results.trx` before it
deletes the work directory; nothing else had access to the file.

## Skill ledger

One row per transition, as SKILL-MODEL §5 asks. `ProgressStore.RecordAttempt(attempt, manifest)`
appends the attempt and, if every item of the exercise now has a passing
attempt, appends `practiced` for each skill in `teaches` and `applied` for each
in `requires` — only for skills currently below that state. `MarkSummaryRead(module)`
appends `introduced` for every skill the module's exercises teach.

**Decision — re-passing adds no ledger rows.** A transition is a change of
state; the ledger would otherwise be an attempt log in disguise. "Last
exercised" in the matrix is therefore the time of the latest transition. If
spaced repetition later needs "last time this skill was touched", derive it
from attempts plus manifests rather than adding rows.

**Decision — `introduced` never unlocks anything.** Unlocking needs
`practiced`, per the task brief. Reading a summary only marks skills as seen.

**Decision — the ledger is written inside the attempt lock but not in one
transaction with the attempt.** At this scale a crash between the two inserts
costs one ledger row that the next pass of the same exercise recreates.

## Derivation (`SeeSharp.Core.ProgressDerivation`, pure, tested)

- `HeldSkills(ledger)` — highest state per skill, latest timestamp and cause.
- `PassedItems(attempts, exerciseId)` — items with at least one passing attempt.
- `MissingSkills(requires, held)` — required skills below `practiced`; empty = unlocked.
- `Transitions(skills, state, cause, timestamp, held)` — rows to append.

Exercise passed = every item in the manifest has a passing attempt. Module
unlocked = any exercise unlocked. Module passed = all **mandatory** exercises
passed; exercises with `optional: true` never block it (`ModuleProgress.Mandatory`
/ `.Optional` split the list). Passing an optional exercise still appends
attempts and ledger rows like any other — it is just not required.

## API the UI calls

```csharp
using var store = new ProgressStore();                 // default path; one instance for the app lifetime
CourseProgress p = store.Query(course);                // Course from CourseLoader / CourseValidator
p.Modules[i].Unlocked / .Passed / .Module
p.Modules[i].Exercises[j].Unlocked / .Passed / .PassedItems / .MissingSkills / .Exercise
p.HeldSkills["oop.constructor-explicit"].State / .LastExercised / .Cause

store.RecordAttempt(Attempts.Coding(exercise.Id, codingItem, sourceDir, gradingResult, timeSpent), exercise.Manifest);
store.RecordAttempt(Attempts.MultipleChoice(exercise.Id, mcItem, selectedIndices, timeSpent), exercise.Manifest);
store.RecordAttempt(Attempts.Open(exercise.Id, openItem, text, timeSpent), exercise.Manifest);
store.RecordAttempt(Attempts.Review(exercise.Id, reviewItem, note, timeSpent), exercise.Manifest);
store.MarkSummaryRead(module);

store.Attempts(exerciseId?)   // history, newest last, for a results panel
store.PassedItems(exerciseId)
store.HeldSkills()
store.SkillLedger()
ReviewExport.Write(store, zipPath);

// Exam mode (task 8); see "Exam runs" below
Exams.Status(store, course, progress, examDefinition)   // Locked / Available / Running / Submitted + the run
Exams.Start(store, course, examDefinition)              // generates the items, opens the run
store.ActiveRun() / store.ExamRuns(examId?) / store.ExamRun(id)
store.RecordAttempt(attempt with { ExamRunId = run.Id })   // no manifest: exam attempts credit no skill
store.ExamAttempts(run.Id)
store.SubmitExamRun(run.Id)

// AI tutor (task 9); written by Tutor.AskAsync in SeeSharp.App, see UI.md
store.RecordTutorExchange(exchange) / store.TutorExchanges(exerciseId?)
```

`ProgressStore` is not an interface; the UI can hold the concrete type.

## Exam runs (task 8)

`exam_run` is one sitting of one exam definition (`exams/<slug>.yaml`,
CONTENT-FORMAT.md). `ProgressStore.StartExamRun` writes the row — including
the **deadline** — before the UI shows a single item, so closing the app
neither resets nor extends the timer. `ExamRuns`/`ActiveRun` first run
`UPDATE exam_run SET submitted = deadline WHERE submitted IS NULL AND deadline <= now`,
which is the whole auto-submit: the timer expiring submits on the next request,
no background job. `RecordAttempt` refuses an attempt whose run is no longer
active, so a late answer cannot slip in.

Rules enforced by the store, not the UI: at most one open run at a time; a
`real` exam gets one run per exam id, ever (a second `StartExamRun` throws,
submitted or not); practice exams can be started again once the previous run
is submitted.

**Decision — exam attempts live in `attempt` with `exam_run_id` set** and are
invisible to everything regular: `Attempts()`, `PassedItems`, `Query` and the
skill ledger only see rows with `exam_run_id IS NULL`, and exam attempts are
recorded without a manifest, so an exam never credits a skill or passes an
exercise. `ExamAttempts(runId)` reads them.

**Decision — attempts are persisted as they are made, not at submission.**
The brief's "written as one unit" is the run row plus its attempts linked by
`exam_run_id` and exported together under `exams/<run id>/`; writing them only
at submit would lose every answer when the app closes mid-exam, which is the
case the deadline rule exists for.

**Generation** (`Exams.Start` → `ExamGenerator.Pick` in Core, pure, tested):
candidates are every non-review item of the covered exercises, each carrying
the covered skills its exercise `teaches`. Greedy set cover: each pick is the
candidate hitting the most still-uncovered skills that are `practiced` but
never `applied`, then the most uncovered skills overall; ties go by a shuffle
from the run's seed (`Random.Shared.Next()` at start, stored in the row), so
the same seed reproduces the exam. It stops when every covered skill is hit or
`maxItems` is reached. Since all items of one exercise carry the same skills,
an exercise contributes one item unless the cap or coverage needs more — a
seven-question quiz yields one question. `Items` are stored on the run so a
reload shows the same exam even after the course changed.

## Review bundle (`seesharp export-review [--out <zip>]`)

Default name `seesharp-review-<yyyyMMdd-HHmm>.zip` in the current directory.

```
trainee.txt                                     user@machine of the trainee (ReviewExport.Trainee)
attempts.csv                                    id, exercise, item, timestamp, timeSpentSeconds, passed, answer, tutorVerdict
skills.csv                                      skill, state, lastExercised, exercise
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/answer.txt
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/source/...   the submitted source tree
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/grading.json
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/results.trx
exams/<run id>/run.csv                          id, exam, kind, started, deadline, submitted, seed, items
exams/<run id>/attempts.csv                     the run's attempts, same columns as the top-level file
exams/<run id>/<exercise>/<item>/<id>_<ts>/...  same layout as above
tutor/<exercise>.md                             every tutor exchange: timestamp, level, judged item and verdict,
                                                provider/model, tokens, cost estimate, question, answer (Markdown)
```

`tutorVerdict` is `pass`/`revise` when the tutor judged that open answer, empty
otherwise; `ReviewExport.Judgement` pairs an attempt with the judgement logged
after the item's previous attempt and before it (the judgement is logged right
before its attempt). `ReviewBundle` reads bundles without the column as before.

**Decision — tutor logs are Markdown, not CSV**: the answers are Markdown and
multi-line, and the supervisor reads them, so one file per exercise opens
directly. Only successful exchanges are rows; a failed request (offline, bad
key) has no answer to log. The API key and base URL are never stored or exported.

Exam attempts never appear in the top-level `attempts.csv`; the supervisor
reads a real exam's result from `exams/<run id>/` (the trainee's app shows
nothing).

**Decision — sources are unpacked into the bundle**, not nested as zips, so
the supervisor can open `Bruch.cs` directly. Every attempt is exported, not
only the last one per item; the CSV and folder names carry the timestamps.

## Tests

- `tests/SeeSharp.Core.Tests/ProgressDerivationTests.cs` — the four derivation rules.
- `tests/SeeSharp.Progress.Tests/ProgressStoreTests.cs` — temp DB; migrate
  twice, delete and recreate, column round trip, ledger transitions, summary
  read, `Query` lock state.
- `tests/SeeSharp.Progress.Tests/ExamRunTests.cs` — deadline survives a reopen,
  expiry submits and refuses late attempts, real exam once / one open run,
  exam attempts outside progress, `Exams.Status` lock state, the bundle's
  `exams/` folder. `tests/SeeSharp.Core.Tests/ExamGeneratorTests.cs` — full
  coverage, never-applied first, same seed same exam, skills nobody teaches.
- `tests/SeeSharp.Progress.Tests/ReviewExportTests.cs` — CSV escaping and the
  skill matrix; and the one end-to-end run: `GradingRunner` on
  `../seesharp-course/modules/04-klassen-und-objekte/01-konstruktoren` with its
  own `starter/` as the trainee source (fails, as the validator guarantees),
  recorded, exported, zip asserted to contain `results.trx`, `grading.json` and
  `source/b2 Konstruktoren/Bruch.cs`.

- `tests/SeeSharp.App.Tests/TutorTests.cs` — the tutor table through `Tutor.AskAsync`,
  and `tutor/<exercise>.md` in the bundle (with the API key absent from DB and zip).

**Decision — the end-to-end test returns early when `../seesharp-course` is
absent.** The course repo has no remote, so CI cannot clone it; locally it
always runs.

## Not done, on purpose

- No tamper protection (task 4 constraint).
- No per-attempt transaction with the ledger, no WAL, no vacuum.
- No `--since` or per-exercise filter on the export; the bundle is small.
- No tutor exchange is ever deleted or edited; there is no per-trainee token budget.
