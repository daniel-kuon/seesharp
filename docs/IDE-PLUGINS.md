# IDE plugins: local API, VS Code extension and Rider plugin

Requested by the user on 2026-10-24 (CLAUDE.md, *Out of scope*): IDE plugins are thin
clients of a JSON API that the running SeeSharp app serves. All logic — gating, grading,
recording attempts, workspace files — stays in the tool; a plugin lists, opens and calls.
The workspace files themselves are described in UI.md, *Exercise workspace files*.

## Local API (`SeeSharp.App/PluginApi.cs`)

**Finding the server.** On start, `seesharp start` writes
`%LOCALAPPDATA%\SeeSharp\server.json` (Linux `~/.local/share/SeeSharp`, macOS
`~/Library/Application Support/SeeSharp` — wherever .NET's `LocalApplicationData` points):

```json
{ "url": "http://127.0.0.1:52344", "token": "<64 hex chars = 32 random bytes>", "pid": 12345 }
```

The token is new per start (`CourseState.ApiToken`). On shutdown the file is deleted — only
when its `pid` is still ours, so a second instance keeps its file. After a crash the file is
stale; a plugin treats a refused connection as "not running".

**Auth.** Every `/api` request needs `X-SeeSharp-Token: <token>`; a missing or wrong token
(compared in constant time) gives **403**. A request with an `Origin` header other than the
app's own origin (`http://127.0.0.1:<port>`) gives **403** too, CORS preflights included.
Reason: any website in the trainee's browser can send requests to localhost; it can neither
read `server.json` nor send the header without a preflight. Plugins use plain HTTP clients,
which send no `Origin`. The Blazor UI itself needs no token.

**Gates.** While an exam run is open every `/api` call answers **409**
`{ "error": "Eine Prüfung läuft …" }` (the same gate as `MainLayout`: no instructions, no
test output). Without a loaded course: **503** `{ "error": "Kein Kurs geladen." }` (except
`POST /api/reload`). Errors are
always `{ "error": "<message in the UI locale>" }`.

| Request | Response |
|---|---|
| `GET /api/course` | `{ locale, modules: [Module] }` |
| `GET /api/exercise/{id}` | `{ exercise: Exercise, instructionsHtml, instructionsFellBack }`; `instructionsHtml` is `null` while locked (like the page); 404 for an unknown id |
| `POST /api/exercise/{id}/grade` | `{ passed, result: GradingResult }`; 404 unknown / no coding item, 409 locked |
| `POST /api/exercise/{id}/workspace` | `{ directory, files: ["relative/path", …] }`; same errors |
| `POST /api/reload` | `{ loaded }`: re-reads `settings.json` and the course (like saving the settings page); answers without a loaded course too, so *Set up SeeSharp* can point a running server at the course `seesharp init` just wrote |

```
Module   = { id, level, title, unlocked, passed, exercises: [Exercise] }
Exercise = { id, title, optional, unlocked, passed,
             missingSkills: [{ id, label, level | null }],      // level = module that teaches it
             items: { total, passed, coding },
             starterDirectory | null, workspaceDirectory | null, // absolute; null without a coding item
             openFile | null, openLine | null,                   // absolute path of the coding item's file
             instructionsPath }                                  // instructions.<locale>.md, German fallback
GradingResult = { outcome: "Passed"|"Failed"|"BuildFailed"|"TimedOut", duration: "hh:mm:ss.fffffff",
                  tests: [{ name, outcome: "Passed"|"Failed"|"Skipped", duration, message, stackTrace }],
                  buildErrors: [string], output, artifactsDirectory }
```

Titles and labels are in the UI locale (German default). `workspaceDirectory` is the folder
an IDE should open (the starter project's `.csproj` folder). **Grade** does exactly what
the "Prüfen" button does: `GradingRunner.RunAsync(tests, starter)`, `Attempts.Coding`,
`RecordAttempt(attempt, manifest)` — so the attempt, the source zip, the TRX and the skill
credit land in the progress DB. The exercise's first (in practice: only) coding item is
graded. Time spent is measured from the exercise's last `GET`/workspace call or grade in this
server process, else 0.

Tests: `tests/SeeSharp.App.Tests/PluginApiTests.cs` — token and origin required; the course
listing's shape and paths (course in a folder with spaces, an umlaut and a comma); a locked
exercise gives no instructions and 409 on grade; grade records a passing attempt and credits
the skill (the grading fixture `Übung #1, Rechner`, real `dotnet test`); workspace writes all
nine files with the no-solution rule, the absolute grade paths and the launch config;
`server.json` is only deleted by its own process.

## Setting up from the plugin ("SeeSharp einrichten" / "Set up SeeSharp")

Both plugins can take a trainee from nothing to a running SeeSharp. The action is offered in the
empty view when SeeSharp is not running ("Zum ersten Mal hier?") or runs without a course (503),
and as a command (VS Code) / toolbar button (Rider). Steps:

1. `dotnet --list-sdks` must list a 10.x SDK; otherwise an error with a link to
   https://dotnet.microsoft.com/download/dotnet/10.0 and nothing else happens.
2. The `seesharp` tool is looked up on `PATH`, then in `~/.dotnet/tools` (the IDE's `PATH` may
   predate the install). Missing: a modal shows the exact command
   `dotnet tool install -g SeeSharp.Cli --add-source https://api.nuget.org/v3/index.json
   [--add-source <extra source>]` and the package source (nuget.org, plus the extra one) and asks to
   install. Present: the same modal offers
   `dotnet tool update -g SeeSharp.Cli` or keeping the version. An update fails while SeeSharp runs
   (the `.exe` is in use); setup then carries on with the installed version.
3. Course folder, default `%USERPROFILE%\seesharp-course` (`~/seesharp-course`), then
   `seesharp init --dir <folder>`: clone or fast-forward the course
   (https://github.com/daniel-kuon/seesharp-course), download both books into `<folder>/book`,
   write `coursePath`/`bookPdfPath` into `settings.json`, validate. Never discards the trainee's
   changes; a conflicting update stops with a message.
4. A running server gets `POST /api/reload`; otherwise `seesharp start --no-browser` as before.
   Then the tree refreshes.

Every process runs without a shell (argument list; paths with spaces, umlauts and commas need no
quoting) and in the user's home folder, never the workspace/project: a `NuGet.config` or `global.json`
there (or in a parent folder) could otherwise redirect the tool install to another feed or SDK. The
sources are named explicitly with `--add-source`. Output: VS Code's output channel "SeeSharp" plus a progress notification; Rider's *Run*
console "SeeSharp einrichten" plus a background task. `seesharp` writes UTF-8 when its output is
piped, so umlauts survive.

**Extra package source** (VS Code `seesharp.extraPackageSource`, Rider *Settings → Tools →
SeeSharp → Zusätzliche Paketquelle*): a folder with `SeeSharp.Cli.*.nupkg` (e.g.
`D:\Rider Projects 2\SeeSharp\nupkg` after `dotnet pack src/SeeSharp.Cli -c Release -o nupkg`) or
a feed URL, passed as `--add-source`; makes setup work before the nuget.org release (docs/RELEASING.md).

**Settings that start processes come only from the user.** A repository's `.vscode/settings.json`
could otherwise run `dotnet run --project <its project>` (`seesharp.cliPath`) or install from its
own feed (`seesharp.extraPackageSource`). Both are `"scope": "machine"`, listed under
`capabilities.untrustedWorkspaces.restrictedConfigurations`, and read via `inspect().globalValue`
only (unit-tested: a workspace value is ignored). Rider's settings are application-level
(`PropertiesComponent.getInstance()`), never per project.

Unit tests (no IDE): SDK parsing, tool lookup (PATH first, then `~/.dotnet/tools`, Windows and
Unix names), argument lists with spaces/umlauts/commas, `run` passing arguments without a shell
(VS Code), the user-only setting and the manifest's scope/restrictions (VS Code), the home folder as
working directory (both).

## VS Code extension (`ide/vscode`)

TypeScript, no UI framework, no runtime dependencies. `src/api.ts` is the client (plain
`node:http`, so no `Origin`), `src/extension.ts` the rest. Publisher `seesharp`, id
`seesharp.seesharp` (recommended by the generated `.vscode/extensions.json`).

- **Activity bar "SeeSharp"** with a tree: modules (`NN Title`, the first unlocked-unpassed one
  expanded) → exercises. Icons: lock = locked, green check = passed, circle = open;
  "optional" as description; the tooltip shows tasks done and, when locked, the missing skills
  with the level that teaches them. Toolbar: refresh.
- **Not running** (no `server.json` or connection refused): the view shows "SeeSharp läuft
  nicht" with *SeeSharp starten* and *Aktualisieren*. Start runs `seesharp start --no-browser`
  in a terminal "SeeSharp" — or, when the setting `seesharp.cliPath` points to a checkout (or
  its `src/SeeSharp.Cli`), `dotnet run --project <that> -- start --no-browser` — as the
  terminal's process (no shell, no quoting), then polls `server.json` for up to two minutes.
  API errors such as the exam gate show as the view's message.
- **Selecting an exercise**: `GET /api/exercise`, for an unlocked coding exercise
  `POST …/workspace`; if `workspaceDirectory` is not a folder of the current window, it asks
  "Den Projektordner von „…“ in VS Code öffnen?" (*Ordner öffnen* / *Neues Fenster* / dismiss).
  The window that opens the folder resumes from `globalState` and opens `openFile` at
  `openLine`; dismissing opens the file in the current window. The instructions open in a
  webview beside the editor with a **Prüfen** button. A quiz-only exercise shows its
  instructions and a link to the exercise in the browser.
- **Prüfen** (button, command *SeeSharp: Prüfen*, editor title button while an exercise is
  current): saves all editors, `POST …/grade` with a progress notification, then a summary
  notification ("Bestanden! 2 von 2 Tests bestanden." / "Noch nicht bestanden. …"), the
  per-test table (or build errors, or the raw output) in the webview, and a tree refresh.
  The exercise is "current" after selecting it, or when the window's folder is an exercise's
  `workspaceDirectory`.
- **Strings**: `package.nls.json` (English) / `package.nls.de.json` for contributions,
  `vscode.l10n.t` with `l10n/bundle.l10n.de.json` at runtime. They follow VS Code's display
  language, not SeeSharp's locale; course content (titles, instructions, error messages)
  follows SeeSharp's. `npm test` checks that every `t('…')` has a German entry.
- **Webview**: CSP `default-src 'none'`, scripts only with a per-render nonce, so scripts in
  course Markdown do not run; test names and messages are HTML-escaped.

### Build

```
cd ide/vscode
npm install
npx @vscode/vsce package          # → seesharp-0.2.0.vsix (runs tsc via vscode:prepublish)
npm test                          # tsc + node:test; the API part runs only with SEESHARP_SERVER_JSON set
```

`node_modules/`, `out/` and `*.vsix` are git-ignored. The API tests need a running SeeSharp
whose `server.json` path is in `SEESHARP_SERVER_JSON` — use a host on a *copy* of the course
(grade and workspace write into the course folder).

### Install and use

```
code --install-extension "D:\Rider Projects 2\SeeSharp\ide\vscode\seesharp-0.2.0.vsix"
```

(or *Extensions → … → Install from VSIX…*). Needs VS Code ≥ 1.95 and the C# Dev Kit. Then
start SeeSharp (`seesharp`, or *SeeSharp starten* in the SeeSharp view), pick an exercise, let
VS Code open its project folder, edit, and press *Prüfen*. The `SeeSharp: Prüfen` task in
`.vscode/tasks.json` grades too, but only prints the result — it needs `seesharp` on PATH and
records nothing; the extension's button records the attempt.

### Verified (2026-10-24)

Compiled with `tsc` (strict); `npm test` against a scratch host on port 5192
(`AppHost.BuildAsync` on a copy of the course, temp progress DB with level 0 passed):
wrong token 403, course listing with absolute existing paths, instructions HTML, 404,
workspace files with the rule, a real grade of `werkzeuge-hallo-seesharp`, unreachable
server = status 0. `curl`: no token 403, foreign `Origin` 403, CORS preflight 403. The
generated `build` task command built the starter and produced exactly the `program` path of
`launch.json`. "Arbeitsbereich vorbereiten" in the browser wrote the files and showed the
folder. **Not verified:** the extension inside VS Code (tree, webview, folder switch,
terminal start) — no VS Code UI automation here; it was not installed into the user's VS Code.

### Verified: setup (2026-10-24)

`npm test` (setup unit tests), `.vsix` 0.2.0 packaged. The flow's commands themselves ran end to end
outside the IDE: `dotnet --list-sdks` parsed on this machine (10.x found), the packed tool installed
with `dotnet tool install --tool-path <scratch> --add-source nupkg SeeSharp.Cli`, and
`seesharp init --dir "<scratch>\Kurs Ü, 1"` cloned the GitHub course, downloaded both books and wrote
the settings file named by `SEESHARP_SETTINGS`; a second run fast-forwarded (up to date) and skipped
both books. `POST /api/reload` has an API test. **Not verified:** the dialogs, output channel and
progress inside VS Code (no UI automation here).

### Not done, on purpose

- No push from the server (the tree refreshes after a grade and on demand), no quiz answering
  in VS Code (the link opens the browser), no relative images in the webview.

## Rider plugin (`ide/rider`)

Kotlin, frontend only (no ReSharper backend part), id `dev.seesharp.rider`, name and vendor
"SeeSharp", `since-build` 262 (Rider 2026.2 and later), no `until-build`. Same behaviour as the
VS Code extension, on the same four calls. `Api.kt` is the client (`java.net.http`, HTTP/1.1, no
`Origin`; JSON via the platform's bundled Gson), `Text.kt` the strings and the exercise HTML,
`SeeSharpToolWindow.kt` the rest. The token is read from `server.json` at each refresh and never
logged.

- **Tool window "SeeSharp"** (right): tree modules (`NN Title`, the first unlocked-unpassed one
  expanded) → exercises; icons lock / green check / open circle, "optional" in grey; the tooltip
  shows tasks done and, when locked, the missing skills with their level. Toolbar: *Aktualisieren*,
  *SeeSharp starten*.
- **Not running** (no `server.json` or connection refused): the tree's empty text says "SeeSharp
  läuft nicht" with the links *SeeSharp starten* and *Aktualisieren*. Start runs
  `seesharp start --no-browser` (`~/.dotnet/tools/seesharp.exe` when it exists, else from `PATH`)
  or, when *Settings → Tools → SeeSharp → SeeSharp-Checkout* is set, `dotnet run --project
  <checkout>/src/SeeSharp.Cli -- start --no-browser` — a plain background process, argument list,
  no shell — then polls for up to two minutes; on failure a balloon shows the output's tail. API
  errors such as the exam gate show as the tree's empty text.
- **Selecting an exercise**: `GET /api/exercise`; for an unlocked coding exercise `POST
  …/workspace`. If `openFile` is neither under the project's base path nor in the project's
  content, it asks "Den Projektordner von „…“ in Rider öffnen?" (*Projekt öffnen* / *Neues Fenster*
  / *Nicht jetzt*) and opens the starter's single `.csproj` (else the folder) with
  `ProjectUtil.openOrImport`; the new project's SeeSharp tool window continues with that exercise
  (application-wide `PropertiesComponent` key). Otherwise it opens `openFile` at `openLine`.
- **Details** below the tree: title, **Prüfen** button, and the HTML (last result, then the
  instructions) in a `JBCefBrowser` when `JBCefApp.isSupported()`, else in a read-only
  `JEditorPane`. CSP `default-src 'none'` (no scripts), test names, messages, build errors and
  output HTML-escaped; links open in the system browser.
- **Prüfen** saves all documents, `POST …/grade` as a background task, then a balloon
  ("Bestanden! 2 von 2 Tests bestanden." / "Noch nicht bestanden. …"), the per-test table and a
  tree refresh.
- **Strings**: `messages/SeeSharp.properties` (English fallback) and `SeeSharp_de.properties`. Unlike
  VS Code they follow **SeeSharp's** locale (from `GET /api/course`, German until known), not the
  IDE's language.
- **JCEF** is a bundled plugin in 2026.2 whose classes live in content modules, so `plugin.xml` uses
  the module form `<dependencies>` (`com.intellij.modules.rider`, `intellij.libraries.jcef`,
  `intellij.platform.ui.jcef`).

### Build

```
cd ide/rider
./gradlew buildPlugin                       # build/distributions/SeeSharp-0.2.0.zip
./gradlew unitTest [-PserverJson=<path>]    # JUnit 5 without the platform; the API test needs a running host
./gradlew runIde [-PserverJson=<path>]      # sandbox Rider; -PserverJson talks to that host instead
./gradlew publishLocal                      # stamps 0.2.yyMMdd.HHmm and publishes to the local feed
```

Gradle 9.1.0 wrapper, IntelliJ Platform Gradle plugin 2.19.0, Kotlin 2.4.21, Rider 2026.2.3 to
build against; JDK 21 comes from the `me.filippov.gradle.jvm.wrapper` plugin (use `./gradlew`, not a
system Gradle/Java). `build/`, `.gradle/`, `.intellijPlatform/` and `.kotlin/` are git-ignored.
`-PserverJson` sets `SEESHARP_SERVER_JSON`, which the plugin (like the VS Code client) prefers over
`%LOCALAPPDATA%\SeeSharp\server.json` — use a host on a *copy* of the course.

**Local feed.** `publishLocal` copies the zip to `C:\Users\<you>\.lamarider\plugin-repo` (override
with `-PlocalRepoDir=`), keeps the three newest `SeeSharp-*.zip` there, and in `updatePlugins.xml`
replaces only the `dev.seesharp.rider` entry (or adds it before `</plugins>`); the other plugins'
entries and zips in that shared feed stay as they are.

### Install and use

The feed `file:///C:/Users/<you>/.lamarider/plugin-repo/updatePlugins.xml` is already a custom
plugin repository in Rider here (else: *Settings → Plugins → ⚙ → Manage Plugin Repositories…*).
*Settings → Plugins → Marketplace* then lists **SeeSharp**; install it and restart Rider. Updates
appear there too after a `publishLocal`; restart Rider after each update. Then open the SeeSharp
tool window (right), start SeeSharp if needed, pick an exercise, let Rider open its project, edit,
press **Prüfen**.

### Verified (2026-10-24)

`buildPlugin` against Rider 2026.2.3; `unitTest` (JSON mapping incl. nulls and a path with comma and
umlaut, escaping of test names/messages/build errors, locked exercise without instructions, both
property files with the same keys and the German/English/fallback lookup, broken or missing
`server.json`, unreachable server = status 0) and, against a scratch host on port 5191
(`AppHost.BuildAsync` on a copy of the course, temp progress DB): wrong token 403, course listing
with absolute existing paths, instructions HTML, 404 with the server's message, 409 on grading a
locked exercise, workspace files, a real grade of `werkzeuge-hallo-seesharp`. `runIde`: the sandbox
Rider loaded the plugin ("Loaded custom plugins: SeeSharp") with the module dependencies resolved
and opened a starter `.csproj` through `ProjectUtil` / `RiderProjectOpenProcessor`. Published as
`0.1.261024.0254`. **Not verified:** the tool window itself (tree, dialogs, JCEF view, *Prüfen*,
*SeeSharp starten*) — no UI automation on the desktop here.

### Verified: setup (2026-10-24)

`unitTest` (incl. `SetupTest`), `buildPlugin` against Rider 2026.2.3, published as `0.2.261024.2132`
(only the `dev.seesharp.rider` entry of the shared feed changed). **Not verified:** the setup dialogs,
console and background task inside Rider (no UI automation on the desktop).

### Limits

- Same as VS Code: no push from the server, no quiz answering (the link opens the browser), no
  relative images in the instructions.
- Rider may ask itself whether to open the project in this or a new window, depending on
  *Settings → Appearance & Behavior → System Settings → Project → Open project in*.
- The Swing fallback (no JCEF) renders simpler CSS.
