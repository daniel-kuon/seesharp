# SeeSharp — Shared Context

_Copied from the Shared Context section of `AGENT_TASKS.md`. `SKILL-MODEL.md` amends tasks 3, 4, 5, 7, 8 and 9 and wins where the two disagree; read it before task 3._

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

**Repo facts (from task 0).**
- `samples/` is a cleaned extract of `csharp14.zip` (the zip stays untouched as the
  original). bin/obj/.vs and IDE/binary junk removed; every solution is `.slnx`.
  It is reference material only — never content, never compiled into the tool.
- Sample TFMs are `net8.0` and `net10.0`; the newest is `net10.0` (LTS), so the tool
  targets `net10.0`.
- Sample paths contain spaces, umlauts and commas (`Schere, Stein, Papier`). Every
  path-handling code path must survive them.
- `csharp14.pdf` is the book. Printed page = physical page − 32 (verified). Its
  table of contents is stale in chapters 4, 17, 18 and 20 — take page numbers from
  the body text. Licence CC BY-NC-SA 4.0: attribute the authors, share-alike.
- Inventory, module map and gaps live in `docs/`; per-task design notes in
  `docs/CONTENT-FORMAT.md`, `docs/PROGRESS-STORE.md`, `docs/UI.md`.
- The course content repo is the sibling folder `../seesharp-course` (own git repo,
  no remote). Model answers live in `<exercise>/solution/`, supervisor-only.
- Work lands on branch `dev`; `main` is merged only after the user's review.
- Run the app: `dotnet run --project src/SeeSharp.Cli -- start` (course and book
  paths come from `%LOCALAPPDATA%\SeeSharp\settings.json`).
