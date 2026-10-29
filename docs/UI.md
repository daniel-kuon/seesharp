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

## Update notice

On start the layout asks nuget.org once (`UpdateCheck`, `GET v3-flatcontainer/seesharp.cli/index.json`, after
the first render) for the newest stable `SeeSharp.Cli`. When it is newer than the running tool's version, a
banner above every page names both versions and `dotnet tool update -g SeeSharp.Cli` (user decision
2026-10-27). Only a version lookup: nothing about the trainee is sent. Offline, an odd reply or a dev build
(csproj version `0.0.0-dev`; releases stamp the tag's version) show nothing.

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

Every page except an open exam has a **side menu** (`Layout/SideMenu.razor`, user
request 2026-10-28): on `/module/{id}` and `/exercise/{id}` it lists that module's
exercises (current one bold, passed ✓, locked ones greyed and not links, optional
ones marked) under a link back to all modules; everywhere else it lists every
module. Below 64rem it is hidden behind a "☰ Menü" button in the top bar and closes
again on navigation. It refreshes on navigation only, so a ✓ earned on the current
page shows after the next click.

- `/` dashboard: modules passed, mandatory exercises passed, skills practiced/applied,
  "Als Nächstes" (the next exercise — first unlocked, unpassed **mandatory** one in
  published order — with the other open mandatory exercises of its module), the
  unlocked, unpassed optional exercises of unlocked modules, and the passed list.
  Every list is grouped under its module: the module title links the module page,
  the exercises below link the exercises.
- `/modules`: every module with lock badge, passed/total mandatory exercises,
  "n von m Zusatzaufgaben bestanden" when the module has optional ones, and — when
  locked — the union of its exercises' missing skills, each with "vermittelt
  in Level NN" linking to the module that first teaches it (or a note that
  nothing teaches it yet).
- `/module/{id}`: reading link (book links render as a "📖" pill, `.book`), rendered `summary.de.md`, the skills the level
  teaches as a disabled checklist (checked = practiced or better, state text
  next to it), the "Gelesen" button (`MarkSummaryRead`; shown as done once
  every taught skill has a ledger row), the exercise list with lock state,
  item progress, reading link and missing skills. Optional exercises follow in
  their own section "Zusatzaufgaben (optional)", each with an "optional" badge.
  **Decision — module pages are always openable**, even when every exercise in
  them is locked: the summary is reading material and `introduced` unlocks
  nothing (PROGRESS-STORE.md). Only exercises are gated.
- `/exercise/{id}`: an optional exercise carries an "optional" badge next to
  its title. Locked → title, reading link and the missing skills with
  module links, nothing else. Unlocked → rendered `instructions.de.md`, the
  `requires` skills with held state and the module that teaches each, then one
  card per item (`ChoiceQuestion`, `OpenQuestion`, `ReviewTask`, `CodingTask`).
  Each item card prefills from the last attempt (`store.Attempts(exerciseId)`)
  so a reload shows the last answer or test run again.
- `/settings`: course path, book PDF path, save (writes `settings.json`, then
  reloads), reload; the IDE list; the AI tutor section (task 9).

## Items

- Multiple choice: radios when `correct` has one index, checkboxes otherwise;
  "Abgeben" records `Attempts.MultipleChoice`; feedback is right/wrong plus the
  explanation after any submission. **Decision — a wrong answer does not reveal
  the correct options**; the trainee can retry.
- Open: textarea, "Abgeben" records `Attempts.Open` (blank = no attempt, just
  the "Bitte eine Antwort eingeben" note). Outside an exam the AI tutor judges
  the answer first (see *AI tutor*, "Judged open answers").
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

## Exercise workspace files (IDE plugins, option 1)

`SeeSharp.App/Workspace.cs`. Whenever an exercise is opened in any IDE ("In … öffnen"),
on "Arbeitsbereich vorbereiten" in the coding card, and on
`POST /api/exercise/{id}/workspace` (docs/IDE-PLUGINS.md), SeeSharp writes these files into
the **starter project folder** — the folder of the `.csproj` that holds `openFile`
(`IdeLauncher.NearestProject`), which is what Rider and VS Code open. They are rewritten every
time, so hand edits are lost by design.

| File | Content |
|---|---|
| `.vscode/tasks.json` | `build` (`dotnet build ${workspaceFolder}`, `$msCompile`) and `SeeSharp: Prüfen` = `seesharp grade "<abs tests dir>" "<abs starter dir>"`. Both are `process` tasks, so no shell parses the paths (spaces, umlauts, commas). |
| `.vscode/launch.json` | one `coreclr` config "<Assembly> starten": preLaunchTask `build`, `program` = `${workspaceFolder}/bin/Debug/<TFM>/<AssemblyName>.dll` (read from the `.csproj`), `console: integratedTerminal`. Only for `Exe` projects; a class-library starter gets none. No test config: the exercise's tests live outside the starter, the grade task replaces it. |
| `.vscode/extensions.json` | recommends `ms-dotnettools.csdevkit`, `ms-dotnettools.csharp`, `seesharp.seesharp` |
| `.vscode/settings.json` | `github.copilot.enable: {"*": false}`, `github.copilot.nextEditSuggestions.enabled: false`, `editor.inlineSuggest.enabled: false`, nothing else (chat stays usable) |
| `AGENTS.md`, `.github/copilot-instructions.md`, `CLAUDE.md`, `.junie/guidelines.md`, `.aiassistant/rules/seesharp.md` | the same text: exercise title, id, level, a link to `instructions.<locale>.md`, the tutor's rules, the skills |

The assistant text reuses `Tutor.Rules(locale, hintRule)`, which `Tutor.SystemPrompt` uses too
(one source). Differences to the in-app tutor: the hint rule asks for escalation on its own
(guiding question → conceptual pointer → worked example on a *different* problem), because an
IDE chat has no level button; rule 6 forbids creating or editing code in the project (agent
modes can write files); "held skills" are the exercise's `requires`, "teaches" its `teaches`,
both with labels in the UI locale (German by default); "later levels" are the modules after
this one with the skills they teach. Readers: `AGENTS.md` — Copilot agent mode, JetBrains
Junie, Codex, Cursor and others; `.github/copilot-instructions.md` — Copilot chat;
`CLAUDE.md` — Claude Code; `.junie/guidelines.md` — Junie; `.aiassistant/rules/*.md` —
JetBrains AI Assistant project rules.

The course repo ignores the files (`**/starter/**/.vscode/` etc. in `seesharp-course/.gitignore`;
**Decision — prefixed with `**/starter/**/`**, because a pattern like `starter/**/AGENTS.md`
is anchored at the repo root and would match nothing, and an unanchored `.github/` would
also hide a future CI folder of the course repo). Grading never sees the folders:
`GradingRunner.IsArtifact` skips `.vscode`, `.github`, `.junie`, `.aiassistant` like `bin`
and `obj` (the two root Markdown files are copied along and harmless).

**What cannot be enforced** — the files are guidance, not a lock:

- Copilot completion is off per workspace only. The trainee can switch it back on (Copilot's
  status bar menu, or by editing `.vscode/settings.json` until the next regeneration). Chat
  stays on deliberately.
- JetBrains has no per-project file for completion: Full Line code completion (local) and AI
  Assistant's cloud completion are IDE settings — *Settings → Editor → General → Inline
  Completion* (both, in current Rider versions) and *Settings → Tools → AI Assistant*. The
  supervisor switches them off on the trainee's machine.
- JetBrains AI Assistant applies a project rule according to its rule type (*Settings → Tools →
  AI Assistant → Project Rules*); check once that `seesharp` is set to *Always*.
- Any assistant may ignore instruction files, and a trainee can paste the exercise into any
  chat. The rules make the default helpful-but-not-solving; the supervisor review is the check.
- **Decision — no `.aiignore`.** It would hide the exercise files from JetBrains AI Assistant
  entirely, which also stops it from explaining a compiler error or a failing test.

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
  or the trainee's "Bericht erstellen" (`ReviewBundle.Read` in
  `SeeSharp.Progress` parses `attempts.csv` and `skills.csv`, round-trip tested
  with quotes, commas and newlines, plus `trainee.txt` and the source trees).
  Exercises with attempts are listed on the left; the selected one shows its
  rubric, every item's attempts (latest first selected) with answer, tutor
  badge and code view, and remarks — see "Reviewer mode" below. The skill
  matrix follows as a table. Attempts for exercises missing from the course are
  only counted.

Verified 2026-10-16 against a scratch copy of the course (git init, local bare
remote): reorder, create exercise through the form (unknown skill refused),
edit it (new multiple-choice item, instructions with preview), save with no
findings, commit blocked on an injected unknown skill plus a solved starter
(2 errors), commit `956cf69` after fixing both, push landed on the bare
remote, pull reported up to date, review bundle rendered next to the rubric.

## Exam mode (task 8)

`AGENT_TASKS.md` task 8 as amended by `SKILL-MODEL.md` §5: exams are defined
by skill coverage in `exams/<slug>.yaml` (CONTENT-FORMAT.md); the store rules
and the generator are in PROGRESS-STORE.md, *Exam runs*. The UI is three
pieces, German default, every string in both resx files:

- `/exams` ("Prüfungen", fourth nav link): one card per definition with kind
  (Probeprüfung / Prüfung), state badge from `Exams.Status` — *gesperrt*
  (a covered exercise is not passed; the card says so), *verfügbar* with
  "Starten", *läuft* with the remaining time and "Zur Prüfung", *abgegeben*
  with the submission time and, for practice exams, "Ergebnis ansehen" plus
  "Erneut starten". A real exam that was taken shows only that the result goes
  to the supervisor through the export bundle. "Starten" calls `Exams.Start`
  and navigates to the run; the store's refusal (second real run, another run
  still open) is shown in red. Start buttons are disabled while any run is open.
- `/exam/{runId}`: **all items at once**, grouped by exercise with the
  exercise's instructions in a collapsed `<details>` (the exercise page itself
  is closed during the run, see the gate below), numbered "Aufgabe n von m".
  The sticky timer at the top ("Verbleibend: mm:ss", red under five minutes)
  is re-rendered by a `PeriodicTimer` every second; when it reaches zero the
  page re-reads the run through `ExamRuns()`, whose expiry step has submitted
  it, and switches to the submitted view. The item cards are the regular
  `ChoiceQuestion` / `OpenQuestion` / `CodingTask` with a `Run` parameter:
  the attempt is recorded with `ExamRunId` and no manifest, and the feedback
  is only "abgegeben" — no right/wrong, no explanation, and for coding items
  the grading result is stored but **withheld** (`ExamPageTests` asserts the
  prerendered page contains neither the test names nor the explanation before
  submission and both after). "Prüfung abgeben" asks once more, then
  `SubmitExamRun`. Submitted practice run: score line, then per item the
  chosen vs. correct options with the explanation, the open answer, or the
  stored grading result (`GradingResultView`, extracted from `CodingTask`).
  Submitted real run: the submission time and the pointer to the supervisor,
  nothing else.
- **The gate** (`MainLayout`): while `State.Store.ActiveRun()` is set, every
  page outside `exams` / `exam/…` shows "Eine Prüfung läuft …" with a link to
  the run instead of its body. Without it the exercise page would run the
  same tests on the same starter and show the output. The AI tutor panel
  checks `Store.ActiveRun()` itself as well (see *AI tutor (task 9)*).

**Decision — all items on one page**, not one after another: the trainee can
go back to a coding item after a quiz question, the timer stays visible, and
there is no navigation state to lose on reload. Reloading the run page shows
the last attempt of every item again (`ExamAttempts(runId)`).

**Decision — no confirm dialog from the browser** for the submit; a second
button pair in the card does the same without JS.

Verified 2026-10-16 on port 5196 against a scratch copy of the course with
the two level 0–4 definitions, hosted through `AppHost.BuildAsync` on a
seeded scratch store (every level 0–4 item passed; the app itself always
opens the real DB and `LOCALAPPDATA` is not honoured by `GetFolderPath` on
Windows): the practice exam started with 20 items (`maxItems`), one question
answered and shown only as "abgegeben", the sticky timer counted down,
`/modules` showed the gate, submitted, the result page showed the chosen
option, the explanation and "falsch"; the real exam started, submitted,
showed no result, and its card offered no second start.

## AI tutor (task 9)

`AGENT_TASKS.md` task 9 as amended by `SKILL-MODEL.md` §5. Code in
`SeeSharp.App/Tutor.cs` (static, plain `HttpClient` + `System.Text.Json.Nodes`,
no SDK), UI in `Components/Items/TutorPanel.razor` (one line in
`ExercisePage`), the judgement of open answers in `OpenQuestion.razor`, and
`TutorSettings.razor` (one line in `SettingsPage`).

**Providers** (`settings.json`: `tutorProvider`, `tutorModel`, `tutorBaseUrl`,
`tutorApiKey`; blank model/URL = the default below). Two wire formats:

| `tutorProvider` | Wire | Default base URL | Default model | Auth | Cost shown |
|---|---|---|---|---|---|
| `off` / unset | – | – | – | – | panel hidden |
| `ollama` | Chat Completions | `http://localhost:11434/v1` | `qwen2.5-coder:7b` | none | "kostenlos (lokal)" |
| `openai` | Chat Completions | `https://api.openai.com/v1` | `gpt-5.4-mini` | `Authorization: Bearer` | $ from the price table |
| `openai-compatible` | Chat Completions | none (required) | none (required) | Bearer when a key is set | tokens only |
| `anthropic` | Messages API | `https://api.anthropic.com/v1` | `claude-haiku-5-5` | `x-api-key` + `anthropic-version: 2026-10-21` | $ from the price table |

Requests go to `<base>/chat/completions` (system prompt as the first message,
no token cap — Ollama and reasoning models differ on the parameter name) or
`<base>/messages` (`system` field, `max_tokens: 4000`, which leaves room for
Haiku 5.5's default adaptive thinking; only `text` blocks are read). Usage comes
from `usage.prompt_tokens`/`completion_tokens` or `usage.input_tokens`/`output_tokens`.

**Decision — prices in USD, not €.** Both providers publish USD; a hand-kept
exchange rate would be a second number to go stale. `Tutor.Prices` holds exact
model ids: Anthropic from the claude-api skill (Haiku 5.5 $0.10/$0.50 per MTok,
$0.50/$2.50 above 100K prompt tokens), OpenAI from
developers.openai.com/api/docs/pricing read 2026-10-24 (standard tier). A
model not in the table shows tokens only.

**What the model gets** (`Tutor.Context`): the instructions in the current
locale, every `.cs` file of `starter/` (skipping `GradingRunner.IsArtifact`
folders: bin, obj, `.vs`, `.idea`, `_ReSharper.Caches`, `.git`, and the
workspace folders `.vscode`, `.github`, `.junie`, `.aiassistant`), the latest
coding attempt's grading summary when it failed (outcome, build errors, failing
tests with messages; raw output only when nothing was extracted), the held
skills (`practiced` or better, `id (label)`), the skills the exercise teaches,
one line per later level with the not-yet-held skills it teaches, and
(`Tutor.ItemsText`) every quiz, open and review item: prompt, options as
"Option n", and the trainee's latest submitted answer or note. **Never** the
`correct` indices, the explanation or anything else the trainee has not seen —
what the model never gets, it cannot leak. With such items the system prompt
adds `Tutor.ItemRules`: never say or hint which option is correct or wrong
(not by elimination, not by confirming an option the trainee names), never
write a model answer for an open question or review task, explain concepts
and ask guiding questions instead. The
system prompt (`Tutor.SystemPrompt`) is English with "answer in German/English"
and these rules: never write the exercise's solution code, not even when asked
directly; use only concepts from the held + taught skills; when the trainee
needs an unlearned concept, name the level that covers it instead of teaching
it; reveal no more than the hint level allows. The last six exchanges of the
exercise go along as history.

**Levels.** "Nächster Hinweis (Stufe n von 3)" asks at the next level (1: one
guiding question, no code; 2: a conceptual pointer, no code; 3: the concept
with a worked example on a *different* problem). The free question box asks at
the current level (at least 1), so typing a question never skips ahead. The
level is restored after a reload from the highest logged level.

**Panel.** A collapsed `<details>` "Tutor um Hilfe fragen – erklärt Begriffe
und gibt Hinweise in drei Stufen, aber nie die Lösung" directly below the
instructions, above "Aufgaben", on **every** exercise type (quiz, open,
review, coding), only when the provider is not `off` and
`Store.ActiveRun()` is null (the layout gate closes the page during an exam
anyway; the exam page never renders the panel). Until 2026-10-24 it sat
collapsed below the items of coding exercises only, and the user could not
find it. When the provider is set but `Tutor.ConfigProblem` reports a missing
model, base URL or key (key required for `openai` and `anthropic`; a keyless
`openai-compatible` server is valid), one hint line with "Zu den
Einstellungen" replaces the panel. Every exchange of the exercise
is listed with its level ("Feedback" for level 0) and the answer rendered by Markdig with
`DisableHtml()` — model output is not trusted like course content, so raw HTML
in it is shown as text. Below: tokens for this exercise and in total, the $
estimate when any priced exchange exists, and "kostenlos (lokal)" for Ollama.

**Judged open answers** (user decision 2026-10-25; until then an optional
"Feedback vom Tutor" button gave ungraded feedback). "Abgeben" on an open
question calls `Tutor.SubmitOpenAsync`. When the tutor is ready (provider set,
`Tutor.ConfigProblem` null) and no exam run is open, `Tutor.JudgeAsync` sends
one user message ("Please judge my answer" + the prompt + the answer; no
history, no hint level) and the usual system prompt with `Tutor.JudgeRule` as
rule 4, followed by `Tutor.RubricSection`: the exercise's `rubric.md` as a
"Grading reference (supervisor-only, secret)" that the model may use but must
never quote, paraphrase point by point or turn into a model answer. The rule
asks for JSON only, `{"verdict":"pass"|"revise","feedback":"<Markdown>"}`:
`pass` = the essential points are covered correctly (wording slips are fine),
`revise` = something essential is missing or wrong; the feedback (UI locale,
"du" in German) says what is right, or what to improve plus ONE guiding
question, never the answer. `Tutor.ParseJudgement` takes the first JSON object
in the reply (code fences and chatter around it are skipped), lower-cases the
verdict and needs non-blank feedback; anything else is an error.

| Case | Attempt | UI |
|---|---|---|
| `pass` | passed | "Der Tutor hat deine Antwort angenommen.", feedback below, box locked |
| `revise` (first) | failed | "… überarbeiten – du hast noch einen Versuch.", feedback below, box open |
| `revise` (second) | failed; the quiz rule (`QuizAttempts = 2`) closes the item | "Die Frage ist abgeschlossen; die Betreuung sieht sich deine Antwort an.", feedback, box locked |
| provider off / config incomplete | passed (as before) | "Kein KI-Tutor verfügbar – deine Antwort ist gespeichert und gilt als bestanden; die Betreuung sieht sie sich an." |
| offline, timeout, HTTP error, empty or unreadable reply | nothing recorded (user decision 2026-10-27) | "Der KI-Tutor konnte deine Antwort nicht bewerten: <reason> Deine Antwort ist noch nicht gespeichert …", box open, extra button "Ohne Tutor speichern" |
| "Ohne Tutor speichern" | passed, no request | "Ohne KI-Tutor gespeichert – … gilt als bestanden …" |
| reload: latest attempt passed without a verdict, tutor works now | unchanged | "Diese Antwort hat der KI-Tutor noch nicht geprüft …", box open so the tutor can still judge it |
| exam run | recorded as before, never judged | unchanged |

A blank box records nothing (it would use up the correction round). The
trainee is never blocked: after a failure they retry or save without the tutor. Each judgement
is a `tutor_exchange` at level 0 (`Tutor.FeedbackLevel`) with `item_id` and
`verdict` (migration 4), tokens and cost, shown under the answer with its
tokens and $ estimate; after a reload `ReviewExport.Judgement` finds the
judgement of the latest attempt again. A failed or unreadable request logs
nothing. The review bundle carries the verdict in `attempts.csv`
(`tutorVerdict`) and in the `tutor/<exercise>.md` heading ("judgement of o1:
**revise**"); `/admin/review` shows it as a badge next to the answer. The
supervisor still reads every open answer: the verdict decides progress, not
the review.

**Failure.** `Tutor.AskAsync` / `JudgeAsync` never throw for network trouble: no model /
base URL / key → a settings hint without a request; 401/403 → "lehnt den API-Schlüssel ab";
other non-2xx → "HTTP n";
refused connection, the 30 s timeout of `Tutor.Http`, malformed JSON → "nicht
erreichbar, du kannst ohne ihn weiterarbeiten"; empty text → "keine Antwort".
Nothing is logged then. Grading is never touched.

**Settings check.** The tutor settings check the stored settings after the first render and after each
save: `Tutor.CheckAsync` sends `GET {baseUrl}/models/{model}` with the key (no tokens) and shows
"Verbunden …", a rejected key, an unknown model (404) or "nicht erreichbar".

**Logging.** Each successful exchange is a `tutor_exchange` row (migration 3,
PROGRESS-STORE.md) and appears in the review bundle as `tutor/<exercise>.md`.
The key lives only in `settings.json` and the request header; the settings
page never renders it back (a blank key field keeps the stored one).

**Tests** (`tests/SeeSharp.App.Tests/TutorTests.cs`, stub `HttpMessageHandler`):
Messages request (URL, `x-api-key`, `anthropic-version`, model, system prompt
with the rules, skill lists, source and failing test), parsing with a thinking
block and usage, cost, the logged row and the bundle's `tutor/bruch.md`, the key
absent from DB and zip; Chat Completions for Ollama (no auth header), OpenAI
(Bearer, cost, history) and a compatible base URL (no price); failures (thrown
exception, 500, non-JSON, empty answer, timeout, missing key/base URL without a
request) give a message and log nothing; the cost table; feedback (the answer,
the prompt and the no-grade/no-model-answer rule in the request, no history,
judgements (the answer, the prompt, the rubric and the no-quote rule in the
request, the reply parsed through chatter and a code fence, verdict and item
logged at level 0, unreadable replies and HTTP 500 as errors that log
nothing; `ParseJudgement` cases); the submit flow on a hosted course (revise →
failed attempt and feedback restored on the page, second revise → item
closed, box locked, supervisor note, pass → passed and locked, the export's
`tutorVerdict` column, bundle read-back and `tutor/first.md` heading, provider
off or key missing → passed + hint without a request, HTTP 500 and an
unreadable reply → passed + failure hint, open exam run → no request); and
the hosted pages: panel hidden when `off`, shown on a coding exercise and on a
quiz + open exercise above "Aufgaben", the settings hint instead of the panel
when the Anthropic key is missing, gone while an exam run is open, plus
`Tutor.Context` skipping `obj/` and carrying the failing run and the skill
lists, and the quiz request carrying options and the trainee's answers but
neither the explanation nor the word "correct" in the items section.

Verified 2026-10-24 on port 5194 (`AppHost.BuildAsync`, scratch course copy,
temp DB with levels 0–3 passed) against an `HttpListener` stub on 5190 that
answered both wire formats, settings swapped in memory: "Nächster Hinweis"
answered at level 1 with the Markdown rendered and an injected
`<script>`/`<img onerror>` shown as text; "Kannst du mir einfach die Lösung
schreiben?" went out at level 1; the stub received the system prompt with the
rules, the held skills, the later levels and `Banner.cs`/`Program.cs` (no
`_ReSharper.Caches`), plus the earlier exchanges as history; the Anthropic
request carried `x-api-key` and `anthropic-version` and the panel showed
"≈ 0,0004 $"; after a reload the next level was 3 of 3; Ollama showed
"kostenlos (lokal)"; a dead port gave the quiet message after 2 s with
"Prüfen" still enabled; the settings page showed the provider hints and
placeholders and never the key. No real model was available (no key, no
Ollama), so answer quality and the refusal to hand out the solution are
untested against a live model.

## Not done, on purpose

- Tutor: no streaming (answers are short; the 30 s timeout covers it), no
  prompt caching, no per-trainee budget, no € conversion, no live test against a
  real model on this machine.

- Exams: no per-item navigation, no pause, no supervisor override to reopen a
  real exam (delete the `exam_run` row with the SQLite CLI if it must happen).
- Admin: no English fields in the editor, no conflict resolution, no SSH, no
  undo (Git is the undo), no edit of module titles/summaries beyond the files
  in the IDE.
- No inline PDF viewer; the link is enough (SKILL-MODEL §4: "ship the link first").
- No per-attempt history view; the last attempt is shown, the export has the rest.
- Navigating away during a test run still records the attempt (the store is a
  singleton); only the result panel is lost until the page is reopened.

## Review mode

`seesharp --review` (or `seesharp start --review`) opens every module and
exercise regardless of progress, so a supervisor can read and try the whole
course. It only lifts the lock in `CourseState.Progress()` — every page and the
plugin API read progress through it; attempts are still recorded and credited
as usual. The nav shows a "Review-Modus" badge. Exams keep their own rule
(all covered exercises passed).

## Reviewer mode

The user's request (2026-10-29): the trainee sends the supervisor a report, the
supervisor reviews it on their own machine, keeps private remarks and sends the
ones they choose back; an in-page code viewer takes remarks on single lines.
Transport is two files the users send themselves (mail, chat) — no server, no
accounts.

**Trainee → supervisor: the report.** `/feedback` ("Feedback" in the nav) has
"Bericht erstellen": `ReviewExport.Write` (the same bundle as
`seesharp export-review`) to `Downloads\seesharp-report-<yyyyMMdd-HHmm>.zip`,
with "Im Ordner zeigen" (Explorer `/select`) so the file can be dragged into a
mail. The bundle now carries `trainee.txt` = `ReviewExport.Trainee`
(`user@machine`); remarks are filed under it, which tells 1–5 trainees apart
without accounts. Bundles from before have no such file and file remarks under
the empty name.

**Supervisor: review.** `seesharp --admin`, `/admin/review`, pick the zip. The
loaded bundle lives in `CourseState.ReviewBundle`, so it survives navigating
away. Per exercise: remarks on the exercise; per item an attempt picker, the
answer, remarks on that attempt (= the answer), and for coding attempts the
submitted files (text only — a file with a NUL byte is skipped) as buttons with
their remark count and a `CodeView`: line numbers plus the code, every line's
remarks right under it. Clicking a line number opens the remark form for that
line (click again to close).

A remark (`Remark` record, `remark` table, migration 5) targets the exercise,
or one attempt (`attempt_id` is the trainee's own attempt id from the bundle),
or one `line` of one `file` of that attempt. The form's checkbox "Für Trainee
sichtbar" is **off by default**: a remark is private until shared, so nothing
reaches the trainee by accident. Every remark shows a badge (privat / für
Trainee sichtbar) with "Teilen"/"Privat machen" and "Löschen". Remarks are kept
in the supervisor's own `progress.db`, keyed by trainee, so the next report of
the same trainee shows them again (attempt ids are stable: attempts are
append-only).

**Supervisor → trainee: the reply.** "Antwortdatei erstellen (nur geteilte
Anmerkungen)" writes `RemarksReply.Write` to
`Downloads\seesharp-remarks-<trainee>-<yyyyMMdd-HHmm>.json`: every shared remark
on that trainee's reports, never a private one (`format` =
`seesharp-remarks/1`; `RemarksReply.Read` refuses anything else).

**Trainee: read the remarks.** `/feedback` → pick the `.json`. The reply
replaces whatever the previous one brought (`ReplaceReceivedRemarks`): each
reply is cumulative, so a remark the supervisor deleted or made private
disappears on the next import. The page lists the exercises with remarks; the
exercise page shows "Anmerkungen der Betreuung" under the tutor panel —
exercise remarks, then per attempt its remarks and, for line remarks, the
attempt's source from the trainee's own database in a read-only `CodeView`
with each remark under its line. When the attempt is not in this database
(another machine, a reset) the remarks still show with "Zeile N".

**Decision — files go to Downloads, not a browser download.** The app window
is a WebView (Photino); writing server-side to a known folder and showing the
path works the same in the window and the browser. `CourseState.OutboxFolder`
is the folder; a scratch host can point it elsewhere.

Not built: editing a remark's text (delete and rewrite), remarks on exam
attempts (the report's exam folders are not opened), a CLI for import.

Tests: `RemarksTests` (Progress: `trainee.txt` and sources read back, binary
skipped, line split; only shared remarks in the reply, re-import replaces,
written remarks untouched; foreign JSON refused) and `ReviewerModeTests` (App,
prerendered: the admin page shows code, line buttons, private and shared
remarks and the counts; after importing the reply the trainee's exercise page
shows the shared line remark under its line, read-only, and not the private
one; `/feedback` links the exercise). Verified 2026-10-29 in a scratch host
on temp databases against the real course (`werkzeuge-hallo-seesharp`): line
click opens the form, a shared line remark and a private exercise remark saved,
share/delete toggled the counts, the reply file written, and the trainee host
showed the remark under line 1 of `Hallo.csproj`.
