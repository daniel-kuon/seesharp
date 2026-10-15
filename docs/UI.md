# Blazor shell and gating (task 5)

The trainee-facing UI in `src/SeeSharp.App`, launched by `seesharp` / `seesharp start`.
`AGENT_TASKS.md` task 5 as amended by `SKILL-MODEL.md` §2 and §4. The user was
away; everything not spelled out there is marked **Decision**.

## Starting

```
seesharp                                   # same as `seesharp start`
seesharp start [--course <path>] [--port <n>] [--no-browser]
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

## Not done, on purpose

- No admin mode, no exam mode.
- No inline PDF viewer; the link is enough (SKILL-MODEL §4: "ship the link first").
- No per-attempt history view; the last attempt is shown, the export has the rest.
- Navigating away during a test run still records the attempt (the store is a
  singleton); only the result panel is lost until the page is reopened.
