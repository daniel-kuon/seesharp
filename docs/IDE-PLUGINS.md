# IDE plugins: local API and VS Code extension

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
test output). Without a loaded course: **503** `{ "error": "Kein Kurs geladen." }`. Errors are
always `{ "error": "<message in the UI locale>" }`.

| Request | Response |
|---|---|
| `GET /api/course` | `{ locale, modules: [Module] }` |
| `GET /api/exercise/{id}` | `{ exercise: Exercise, instructionsHtml, instructionsFellBack }`; `instructionsHtml` is `null` while locked (like the page); 404 for an unknown id |
| `POST /api/exercise/{id}/grade` | `{ passed, result: GradingResult }`; 404 unknown / no coding item, 409 locked |
| `POST /api/exercise/{id}/workspace` | `{ directory, files: ["relative/path", …] }`; same errors |

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
npx @vscode/vsce package          # → seesharp-0.1.0.vsix (runs tsc via vscode:prepublish)
npm test                          # tsc + node:test; the API part runs only with SEESHARP_SERVER_JSON set
```

`node_modules/`, `out/` and `*.vsix` are git-ignored. The API tests need a running SeeSharp
whose `server.json` path is in `SEESHARP_SERVER_JSON` — use a host on a *copy* of the course
(grade and workspace write into the course folder).

### Install and use

```
code --install-extension "D:\Rider Projects 2\SeeSharp\ide\vscode\seesharp-0.1.0.vsix"
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

### Not done, on purpose

- Rider plugin: the API is ready for it (same four calls); not started.
- No push from the server (the tree refreshes after a grade and on demand), no quiz answering
  in VS Code (the link opens the browser), no relative images in the webview.
