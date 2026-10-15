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
                 passed, answer, source (zip BLOB), grading_json, trx
skill_transition id, skill_id, state (1 introduced, 2 practiced, 3 applied), timestamp, cause
```

Attempts are never updated or deleted. There is no `exercise passed` column,
no `item passed` column, no `skill` table: all of that is derived.

## What an attempt stores

`Attempt(ExerciseId, ItemId, Timestamp, TimeSpent, Passed, Answer?, Source?, GradingJson?, Trx?)`

- **multipleChoice**: `Answer` = chosen indices, `"0,2"`. Passed when the chosen set
  equals `correct`.
- **open**: `Answer` = the text. Passed when non-blank (gates on *answered*).
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
unlocked = any exercise unlocked. Module passed = all exercises passed.

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
```

`ProgressStore` is not an interface; the UI can hold the concrete type.

## Review bundle (`seesharp export-review [--out <zip>]`)

Default name `seesharp-review-<yyyyMMdd-HHmm>.zip` in the current directory.

```
attempts.csv                                    id, exercise, item, timestamp, timeSpentSeconds, passed, answer
skills.csv                                      skill, state, lastExercised, exercise
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/answer.txt
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/source/...   the submitted source tree
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/grading.json
<exercise>/<item>/<id>_<yyyyMMdd-HHmmss>/results.trx
```

**Decision — sources are unpacked into the bundle**, not nested as zips, so
the supervisor can open `Bruch.cs` directly. Every attempt is exported, not
only the last one per item; the CSV and folder names carry the timestamps.

## Tests

- `tests/SeeSharp.Core.Tests/ProgressDerivationTests.cs` — the four derivation rules.
- `tests/SeeSharp.Progress.Tests/ProgressStoreTests.cs` — temp DB; migrate
  twice, delete and recreate, column round trip, ledger transitions, summary
  read, `Query` lock state.
- `tests/SeeSharp.Progress.Tests/ReviewExportTests.cs` — CSV escaping and the
  skill matrix; and the one end-to-end run: `GradingRunner` on
  `../seesharp-course/modules/04-klassen-und-objekte/01-konstruktoren` with its
  own `starter/` as the trainee source (fails, as the validator guarantees),
  recorded, exported, zip asserted to contain `results.trx`, `grading.json` and
  `source/b2 Konstruktoren/Bruch.cs`.

**Decision — the end-to-end test returns early when `../seesharp-course` is
absent.** The course repo has no remote, so CI cannot clone it; locally it
always runs.

## Not done, on purpose

- No tamper protection (task 4 constraint).
- No per-attempt transaction with the ledger, no WAL, no vacuum.
- No `--since` or per-exercise filter on the export; the bundle is small.
- Exam attempts (task 8) and tutor logs (task 9) will be new tables, new
  migration scripts.
