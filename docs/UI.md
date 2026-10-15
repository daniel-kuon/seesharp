# Blazor shell and gating (task 5)

The trainee-facing UI in `src/SeeSharp.App`, launched by `seesharp` / `seesharp start`.
`AGENT_TASKS.md` task 5 as amended by `SKILL-MODEL.md` §2 and §4. The user was
away; everything not spelled out there is marked **Decision**.

## Starting

```
seesharp                                   # same as `seesharp start`
seesharp start [--course <path>] [--port <n>] [--no-browser] [--admin]
seesharp --admin                           # supervisor editor, see "Admin mode (task 7)"
dotnet run --project src/SeeSharp.App -- [<course-path>]   # dev, no browser
```

`StartCommand` (CLI) parses the flags and calls `AppHost.RunAsync`, which binds
Kestrel to `http://127.0.0.1:<port>` (`0` = any free port), prints the URL,
opens it with `Process.Start(UseShellExecute = true)` and blocks until Ctrl+C.
ASP.NET logging is capped at Warning so the URL is the only line printed.

**Course path** (`CoursePaths.Resolve`, tested): `--course`, else
`SEESHARP_COURSE`, else `coursePath` from `%LOCALAPPDATA%\SeeSharp\settings.json`,
else `../seesharp-course` relative to the working directory if it exists.
**Decision — an explicit source wins even when the folder is missing**, so the
settings page can say which path is wrong instead of silently falling back.
Without a course every page except `/settings` shows a pointer to the settings
page (the layout decides, so pages can assume a course is loaded).

**Book** (`CoursePaths.BookPdf`): the `bookPdfPath` setting, else
`../csharp14.pdf` next to the course folder. When the file exists it is served
at `/book.pdf` and reading pointers render as `book.pdf#page=<physical>` links
(`target=_blank`; the browser's PDF viewer honours `#page=`). When it is missing
the pointer is plain text ("Abschnitt 5.4.3 „…“, Seite 340"). The offset lives
in `Reading.PrintedToPhysicalOffset` (`SeeSharp.Content/Model.cs`) with its
comment; the UI only calls `Reading.PhysicalPage`. Note that with the course at
`D:\Rider Projects 2\seesharp-course` the default resolves to
`D:\Rider Projects 2\csharp14.pdf`, which does not exist — the repo's copy is
one folder deeper, so set `bookPdfPath` on the settings page to use it.

## Loading and state

`CourseState` (singleton) holds settings, the resolved paths, the loaded
`Course`, the validator findings and the `ProgressStore`. It loads through
`CourseValidator.ValidateAsync(root, RunStarterCheck: false)` — structural and
skill-graph checks are cheap and the module list shows any *errors* at the top
for the content author; the course is rendered regardless. The starter check
(one `dotnet test` per exercise) stays in `seesharp validate`.

**Decision — "Kurs neu laden" on the settings page re-reads the course from
disk** without restarting; the content author works in the sibling repo while
the app runs. Pages call `State.Progress()` (`store.Query(course)`) on every
render — cheap at this scale and always consistent with the store.

**Decision — the progress DB is the real one** (`ProgressStore.DefaultPath`);
there is no override flag. Delete the file to start over.

## Screens

- `/` dashboard: modules passed, exercises passed, skills practiced/applied,
  the next exercise (first unlocked, unpassed in published order), passed list.
- `/modules`: every module with lock badge, passed/total exercises and — when
  locked — the union of its exercises' missing skills, each with "vermittelt
  in Level NN" linking to the module that first teaches it (or a note that
  nothing teaches it yet).
- `/module/{id}`: reading link, rendered `summary.de.md`, the skills the level
  teaches as a disabled checklist (checked = practiced or better, state text
  next to it), the "Gelesen" button (`MarkSummaryRead`; shown as done once
  every taught skill has a ledger row), the exercise list with lock state,
  item progress, reading link and missing skills.
  **Decision — module pages are always openable**, even when every exercise in
  them is locked: the summary is reading material and `introduced` unlocks
  nothing (PROGRESS-STORE.md). Only exercises are gated.
- `/exercise/{id}`: locked → title, reading link and the missing skills with
  module links, nothing else. Unlocked → rendered `instructions.de.md`, the
  `requires` skills with held state and the module that teaches each, then one
  card per item (`ChoiceQuestion`, `OpenQuestion`, `ReviewTask`, `CodingTask`).
  Each item card prefills from the last attempt (`store.Attempts(exerciseId)`)
  so a reload shows the last answer or test run again.
- `/settings`: course path, book PDF path, save (writes `settings.json`, then
  reloads), reload.

## Items

- Multiple choice: radios when `correct` has one index, checkboxes otherwise;
  "Abgeben" records `Attempts.MultipleChoice`; feedback is right/wrong plus the
  explanation after any submission. **Decision — a wrong answer does not reveal
  the correct options**; the trainee can retry.
- Open: textarea, "Abgeben" records `Attempts.Open` (blank = not answered).
- Review: optional note, "Als erledigt markieren" records `Attempts.Review`.
- Coding: absolute path of `starter/<openFile>` (normalised to backslashes)
  with line number and a copy button (`navigator.clipboard.writeText` through
  `IJSRuntime`), then one "In … öffnen" button per detected IDE (see
  *IDE launch* below). "Prüfen" runs
  `GradingRunner.RunAsync(exercise.TestsDirectory, exercise.StarterDirectory)`
  — the same call as `seesharp validate` — awaited inside the Blazor event
  handler, so the circuit stays responsive and the button shows "Tests laufen …"
  until the run ends. The result is recorded with `Attempts.Coding` (passed per
  `requiredTests`) and shown as outcome, duration, build errors (or raw output
  when none were extracted), or a per-test table with messages.
  The trainee edits `starter/` in place inside the course checkout; grading
  copies it out, so the checkout is never written by the tool.

Time spent per attempt is a `Stopwatch` started when the item card was created
and restarted after each submission.

## IDE launch (task 6)

`src/SeeSharp.Ide` (BCL only): `IdeKind { Rider, VsCode, VisualStudio }`,
`IdeResolver.Resolve(kind, configured, probe)` and `IdeLauncher`.
**Decision — the enum is `IdeKind`, not `Ide`**: inside `SeeSharp.App` the
name `Ide` resolves to the sibling namespace first.

**Resolution order** per IDE: the settings value (`riderPath`, `vsCodePath`,
`visualStudioPath`) when the file still exists, else the known install
locations, else PATH (honouring `PATHEXT`). Rider: newest
`%LOCALAPPDATA%\JetBrains\Toolbox\apps\Rider\**\bin\rider64.exe`,
`%LOCALAPPDATA%\Programs\Rider\bin\rider64.exe` (where Toolbox installs
today), `%LOCALAPPDATA%\JetBrains\Toolbox\scripts\rider.cmd`,
`%ProgramFiles%\JetBrains\**\rider64.exe`, then `rider64`/`rider` on PATH.
VS Code: `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`,
`%ProgramFiles%\Microsoft VS Code\Code.exe`, `code` on PATH. Visual Studio:
`vswhere.exe -latest -products * -property productPath`, then `devenv` on PATH.
`Resolve` never throws; any probe failure means "not found". The machine
probes (`Env`, `FileExists`, `FindFiles`, `OnPath`, `VsWhere`) are an
`IdeProbe` record of delegates with real defaults, so the tests fake a whole
machine without an interface per probe.

**Decision — the settings file *is* the cache.** `CourseState.DetectIdes`
runs once at app start (`AppHost`, after `LoadAsync`) and writes the three
resolved paths back to `settings.json`; later starts only check that those
files still exist. A stale path (Rider updated, Toolbox moved it) falls through
to detection automatically. "Nicht gefunden" is not cached, so a machine
without an IDE re-probes at every start — three `File.Exists` plus one
`vswhere` run, cheap. The settings page lists every IDE with its path or
"nicht gefunden" and a "Neu erkennen" button (`DetectIdes(force: true)` in a
`Task.Run`, because `vswhere` is a child process). Editing the path by hand in
`settings.json` works too; there is no text field for it.

**Launch** (`IdeLauncher.Launch`): `Process.Start` with
`UseShellExecute = false`, arguments through `ArgumentList` (spaces, umlauts
and commas in paths are quoted by the runtime), never awaited; the process
handle is disposed right away and a failure comes back as a message that the
card shows in red. The project is the nearest `.csproj` walking up from the
file (`IdeLauncher.NearestProject`); without one the file's folder is used.

| IDE | Command line |
|---|---|
| Rider | `rider64.exe "<csproj>" --line <n> "<file>"` |
| VS Code | `Code.exe "<project dir>" -g "<file>:<n>"` |
| Visual Studio | `devenv.exe "<csproj>" "<file>" /Command "Edit.GoTo <n>"` |

Without `openLine` the line arguments are left out. When no IDE is found the
card keeps the path and copy button and adds a hint (resx `IdeNoneFoundHint`)
pointing at Rider's Tools → Create Command-line Launcher and VS Code's Shell
Command, then "Neu erkennen".

**Tests** (`tests/SeeSharp.Ide.Tests`): argument lists for all three IDEs over
a real temp tree with spaces, umlauts and a comma; nearest-`.csproj` lookup;
resolver precedence with a faked `IdeProbe`; a throwing probe yields null; a
missing executable yields a message instead of an exception.

**Linux / macOS.** `IdeProbe.Windows` picks the candidate set: on Unix the
resolver tries Toolbox under `~/.local/share/JetBrains/Toolbox/apps` (newest
`rider.sh`), the Toolbox `scripts/rider` shim, `/opt/jetbrains/rider`,
`/opt/rider`, `/snap/bin/rider`, the macOS app bundle, then `rider`/`rider.sh`
on PATH; VS Code via `/usr/bin/code`, `/usr/share/code/bin/code`, snap, the
macOS bundle, then PATH; Visual Studio never. PATH lookup uses no PATHEXT off
Windows. The command lines are identical (`rider.sh <csproj> --line n <file>`).
Verified 2026-10-16 on an Ubuntu VM with Rider 2026.3 EAP under Xvfb: the
whole test suite passes on Linux, and clicking "In Rider öffnen" started Rider
with exactly those arguments (`AppStarter - args:` in `idea.log`). A Rider that
has never completed its first-run wizard stops there; that is Rider's state, not
ours. The browser is opened with `xdg-open` on Linux.

**Known Rider failure.** If a Rider instance is already running but does not
answer its activation socket (seen with a hung or plugin-sandbox instance),
Rider shows "Start Failed … is still running and does not respond" for the
second process. Nothing SeeSharp can do: close that instance. The launcher
returns no error in that case because the second process stays alive showing
the dialog.

## Rendering

Global `InteractiveServer` render mode with prerendering, one `MainLayout`
with a three-link nav, all CSS inline in `App.razor` (no wwwroot, no component
library, no JS beyond the clipboard call). Markdown via Markdig with
`UseAdvancedExtensions`, rendered unsanitised: the course is the trainee's own
checkout. `DetailedErrors` is on — local tool, one trainee.

**Decision — `blazor.web.js` is embedded.** The script is a static web asset
of the ASP.NET Core SDK pack; the CLI that hosts the app is a plain console
tool with no `wwwroot` and no static-web-assets manifest, so the
`EmbedBlazorScript` target in `SeeSharp.App.csproj` embeds it and `AppHost`
serves it at `/_framework/blazor.web.js`. Verified in the packed tool.

## Strings

`SeeSharp.App/Strings.resx` (German) with a generated `Strings` class, one
property per key, plus `ForSkill` / `ForOutcome` / `ForTest` mappers. The CLI
gained `StartUsage` and the extended `Usage`.

## Localisation (task 10)

German is the neutral resource (`NeutralLanguage=de`); English lives in
`Strings.en.resx` next to each `Strings.resx` (App, Cli, Content) and ships as
the `en/` satellite assembly. A test per project asserts the English key set
equals the German one, so a new key without a translation fails the build's
tests, not the trainee.

**Decision — the culture is process-wide.** One trainee, one machine: `Locale.Apply`
sets `CultureInfo.DefaultThreadCurrentUICulture` and nothing else (an explicit
per-thread culture would flow into every later request and pin it). Resolution
(`Locale.Resolve`, tested): `SEESHARP_LOCALE`, else `locale` in
`settings.json`, else `de`; unknown values mean German. The CLI applies it in
`Program.cs`, so `seesharp validate` prints its findings in the chosen language
too. The switcher in the nav ("Deutsch | English", `MainLayout`) writes the
setting, applies the culture and force-reloads the page, which starts a fresh
circuit in the new language; `<html lang>` follows `Locale.Current`.

Content falls back explicitly: `exercise.Instructions(locale)` and
`module.Summary(locale)` return the `*.en.md` file, or the German one with
`FellBack = true`, and the page shows `TranslationFallback` ("No English
version yet, showing German") above the article. Locale-keyed titles, prompts
and skill labels go through `Localized.Text` and fall back to German silently.
The validator warns (`TranslationMissing`) once any module or exercise provides
a non-default locale that another one lacks; a course without translations gets
no warning (CONTENT-FORMAT.md).

## Tests

`tests/SeeSharp.App.Tests/CoursePathsTests.cs`: resolution order, missing
sibling, explicit-but-missing path, book default, and the `#page=` link using
the physical page. No bUnit; the components have no logic beyond what the store
and the runner already test.

## Admin mode (task 7)

`seesharp --admin` (or `seesharp start --admin`; the dev entry point takes
`--admin` too) turns the app into the supervisor's editor over the course
working copy. Without the flag nothing admin-related exists: `AppHost` answers
every `/admin*` request with 404 before routing, `Routes.razor` refuses to
render a page from the `SeeSharp.App.Components.Admin` namespace inside a
circuit, and the nav shows no admin links. `AppHost.BuildAsync` returns the
configured, unstarted app so a test can host it on a temp progress store;
`AdminTests` asserts 404/200 per route and the absence/presence of the links.

**Decision — the editor works on files, the course object is read-only.** Every
admin edit goes through `SeeSharp.Content/CourseEditor` (create, swap, save,
save skills) or writes a file directly, then calls `State.LoadAsync()`, so the
trainee pages and the structural findings follow the disk. The exercise editor
deserialises a fresh `ExerciseManifest` from `manifest.yaml`, never the loaded
course's instance.

Screens (German default, every string in both resx files):

- `/admin` course tree: modules and exercises with ↑/↓ (`CourseEditor.Swap`
  renames the two `NN-` prefixes — the module number is also the level), delete
  with a browser `confirm`, "Neues Modul" (`module.yaml` + `summary.de.md`) and
  "Neue Übung" (`manifest.yaml` with id/title/teaches/requires,
  `instructions.de.md`, `rubric.md`, and a `starter/<Project>` console project
  plus `tests/<Project>.Tests` xunit project; the starter throws
  `NotImplementedException`, so the template compiles and fails its one test —
  `seesharp validate` with the starter check passes on it, tested). The project
  name is the slug in PascalCase (`hallo-welt` → `HalloWelt`).
- `/admin/exercise/{id}`: id, title, optional reading, teaches/requires via
  `SkillPicker` (a `<datalist>` of `skills.yaml` ids; "Hinzufügen" refuses
  anything not in the catalog — never free text), item cards per type with
  ↑/↓/remove and "Aufgabe hinzufügen", the instructions textarea with a live
  Markdig preview (`CourseState.MarkdownText` on `oninput`), the rubric
  textarea, "Speichern". After saving the course reloads and the page lists the
  findings whose location starts with the exercise folder.
  **Decision — German fields only** (`DeText` edits the `de` key); English
  titles and `instructions.en.md` are written by hand as in task 10.
  **Decision — saved manifests are in the serializer's canonical form** (block
  mappings, `type` and `id` first via `YamlMember(Order)`, null fields omitted);
  hand-written flow style is not preserved. `Reading.PhysicalPage` is
  `[YamlIgnore]` so the derived page is never written.
- `/admin/skills`: the validator's `SkillGraphSummary` (per level: exercises,
  skills taught, new, required; coverage; dangling; dead ends), skills in the
  catalog that nothing teaches, and the `Skill*` findings. The link
  "skills.yaml bearbeiten" opens `/admin/skills/edit`, a plain textarea;
  `CourseEditor.SaveSkills` parses first and refuses to write an unparseable
  file, then the course reloads and the skill findings are shown. Nothing
  else writes `skills.yaml` (SKILL-MODEL §5).
- `/admin/git` (`CourseGit`, LibGit2Sharp, one `Repository` per call): branch
  and remote, status table, the patch (index + working directory, untracked
  included) in a `<details>`, "Validieren" (full `CourseValidator` run
  including the starter check, findings listed), the commit message and
  "Validieren und committen", Push, Pull. **The rule lives in
  `CourseGit.CommitAsync`:** it validates with the starter check and returns no
  commit when the report has errors; the page shows "Commit blockiert" with the
  findings (`AdminTests` covers the blocked and the clean path against a scratch
  repo with a local bare remote). Push goes to the first remote with the
  current branch; Pull needs an upstream and reports up to date / fast-forward
  / merge commit; `MergeStatus.Conflicts` shows the "resolve in Rider" message —
  there is no conflict UI. **Decision — HTTPS credentials come from
  `settings.json`** (`gitUserName`, `gitToken`, no UI); LibGit2Sharp's bundled
  native library has no SSH, so SSH remotes must be pushed from the terminal.
  The commit signature comes from the repo's `user.name`/`user.email`; a
  missing one surfaces as the LibGit2Sharp error.
- `/admin/review`: `<InputFile>` for a bundle from `seesharp export-review`
  (`ReviewBundle.Read` in `SeeSharp.Progress` parses `attempts.csv` and
  `skills.csv`, round-trip tested with quotes, commas and newlines). For every
  exercise in published order with answers to `open` or `review` items the
  answers (timestamp + text) are shown in a two-column grid next to the
  rendered `rubric.md`; the skill matrix follows as a table. Attempts for
  exercises missing from the course are only counted.

Verified 2026-10-16 against a scratch copy of the course (git init, local bare
remote): reorder, create exercise through the form (unknown skill refused),
edit it (new multiple-choice item, instructions with preview), save with no
findings, commit blocked on an injected unknown skill plus a solved starter
(2 errors), commit `956cf69` after fixing both, push landed on the bare
remote, pull reported up to date, review bundle rendered next to the rubric.

## Not done, on purpose

- No exam mode.
- Admin: no English fields in the editor, no conflict resolution, no SSH, no
  undo (Git is the undo), no edit of module titles/summaries beyond the files
  in the IDE.
- No inline PDF viewer; the link is enough (SKILL-MODEL §4: "ship the link first").
- No per-attempt history view; the last attempt is shown, the export has the rest.
- Navigating away during a test run still records the attempt (the store is a
  singleton); only the result panel is lost until the page is reopened.
