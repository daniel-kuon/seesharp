# Handover

What exists, what you still need to decide, and how to pick up from here.
Everything is on branch `dev`; `main` still holds only Task 1.
Last updated 2026-10-24 (second autonomous run).

## State of the task set

| Task | State | Commits on `dev` | Notes |
|---|---|---|---|
| 0 Inventory and module map | done, gate passed | (on `main`) | `docs/INVENTORY.md`, `MODULE-MAP.md`, `GAPS.md` |
| 1 Scaffold | done, gate passed | `9e84fe3` (on `main`) | |
| 2 Grading runner | done, gate waived by you | `eccd460`, `14f3cee` | `seesharp grade`; skips IDE caches (`_ReSharper.Caches`, `.idea`, `.vs`, …) |
| 3 / 3a Content model, skills | done, **gate not reviewed** | `6098493` | `docs/CONTENT-FORMAT.md`, `seesharp-course/skills.yaml` |
| 4 Progress store | done | `617b9cc` | `docs/PROGRESS-STORE.md` |
| 5 Blazor UI | done | `13788b0`, `90044ec`, `abd41e7`, `bf09693` | quiz: one retry, then locked with the answer shown; "Weiter mit …" after passing; quiz text rendered as Markdown |
| 6 IDE launch | done: Rider, VS Code, Visual Studio, Linux | `07faa48`, `da2d8f0`, `c5ad8a1`, `ac99756` | per-exercise workspace files: VS Code build/run/debug + "Prüfen" task, Copilot inline completion off, AI rule files (`AGENTS.md`, Copilot, Claude, Junie, AI Assistant) |
| 7 Admin mode | done | `d75173e` | `seesharp --admin` |
| 8 Exam mode | done | `60d788f` | practice + real exam for levels 0–4 in the course |
| 9 AI tutor | done | `abe3b57`, `069d4f1` | Ollama (free, local), OpenAI, OpenAI-compatible (Groq, OpenRouter, Gemini), Anthropic; **untested against a real model** |
| 10 English | done | `29fd442`, `fded518` | UI switch; course text translated (see below); `csharp14.en.pdf` served when present |
| Optional exercises | done | `d033f55`, `f809432` | `optional: true`; never gate modules, unlocks or exams |
| IDE plugins | done | `f30b33c`, `123983f`, `c7d099e`, `7dce12f` | local `/api` (token in `server.json`); VS Code extension `ide/vscode/seesharp-0.1.0.vsix`; Rider plugin in your local feed (`dev.seesharp.rider`). See `docs/IDE-PLUGINS.md` |
| Translation tooling | done | `4540d20`, `646b62b`, `19e6551` | `tools/translate_course.py` (DeepL), `tools/book_pdf.py` (split/merge the book for Google Translate) |

## Course content

`D:\Rider Projects 2\seesharp-course` (no remote; commits are local):

- **Levels 0–23**, i.e. every chapter of the book: 0–15 core, 16–23 electives
  (Attribute/Reflexion, Multithreading, ADO.NET with SQLite, EF Core with SQLite,
  WPF via view-models, Netzwerk, ASP.NET Core Razor Pages, Veröffentlichung).
- 24 levels, 211 exercises: 135 mandatory plus 76 optional ones (`10-…`, 2–4 per level).
  Every coding exercise has `starter/`, `tests/` and a model answer in
  `solution/`; each author checked that the starter fails and the solution
  passes.
- `skills.yaml`: 494 skills. Plan for levels 10–23: `docs/PLAN-10-23.md`.
- Everything is in German and English. The English was machine-translated with
  DeepL (424,538 of the 500,000 free characters used this month) and then
  reviewed for code spans, identifiers and titles — not proofread as prose.
- Exercises in 18, 19 and 22 restore NuGet packages at grade time, so grading
  them needs internet access (or a warm NuGet cache).
- Your own edits in `modules/00-werkzeuge/01-hallo-seesharp/starter/Hallo/Hallo.cs`
  and `02-fehler-beheben/starter/Reparatur/Hallo.cs` are untouched and
  uncommitted. They are why `seesharp validate` reports two "tests already pass
  against the starter" errors — expected while your solutions sit there.

## Decisions you still own

1. **Merge `dev` into `main`.** I'm not allowed to merge without your review.
2. **Licence.** CC BY-NC-SA 4.0 like the book; forbids commercial use of the
   tool. Usual split: MIT for `src/`, CC BY-NC-SA for course content.
3. **Company name in git history** (Task 0/1 commits). Rewriting is your call.
4. **Content schema and skill vocabulary** were never gate-reviewed. Skill ids
   are permanent once trainees have progress rows. 288 of the 494 skills are
   new and many are fine-grained; merging some before real use is cheap now.
5. **Course repo hosting.** `seesharp-course` has no remote yet.
6. **Book PDF in English.** I couldn't drive Google Translate (no Chrome
   connected; the built-in browser can't upload files). The 11 chunks are in
   `translation\de\` (each < 9.5 MB, ≤ 300 pages). Translate them, put the
   results in `translation\en\` (same order), then
   `python tools\book_pdf.py merge translation\en csharp14.en.pdf` (needs
   `pip install pypdf`).

## How to run

```bash
dotnet run --project src/SeeSharp.Cli -- start
```

Settings: `%LOCALAPPDATA%\SeeSharp\settings.json` (course path, book PDF, IDE
paths, locale, tutor provider/key, Git credentials). `--admin` enables the
editor. `seesharp validate <course>`, `seesharp grade <exercise>/tests <source>`
and `seesharp export-review` work from the CLI.

IDE plugins: install the VS Code extension with
`code --install-extension "ide\vscode\seesharp-0.1.0.vsix"`; in Rider,
Settings → Plugins → Marketplace shows "SeeSharp" from your local feed
(restart Rider after installing). Neither plugin's UI was clicked through by me.

## Linux

Verified on the Ubuntu VM: .NET 10 SDK, full test suite green, `seesharp
validate` clean, "In Rider öffnen" starts Rider with the right arguments.
`server.json` is owner-only there.

## Known rough edges

- Rider "Start Failed … still running and does not respond": a running Rider
  that doesn't answer its activation socket. Close it; see `docs/UI.md`.
- `seesharp validate` warnings are dead-end skills (taught, never required
  later) and the intentional "starter does not build" ones.
- AI rule files can't force IDE assistants: Copilot can be re-enabled by the
  trainee; JetBrains completion settings are IDE-level (`docs/UI.md`).
- Admin Git page supports HTTPS remotes only.
