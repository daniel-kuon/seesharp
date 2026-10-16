# Handover — overnight autonomous run, 2026-10-15/16

What was built while you were away, what you need to decide, and how to pick
up from here. Everything is on branch `dev`; `main` still holds only Task 1.

## State of the task set

| Task | State | Commit on `dev` | Notes |
|---|---|---|---|
| 0 Inventory and module map | done, gate passed | (Task 0 docs in `main`) | `docs/INVENTORY.md`, `MODULE-MAP.md`, `GAPS.md` |
| 1 Scaffold | done, gate passed | `9e84fe3` (on `main`) | |
| 2 Grading runner | done, gate waived by you | `eccd460` | `seesharp grade` |
| 3 / 3a Content model, skills | done, **gate not reviewed** | `6098493` | `docs/CONTENT-FORMAT.md`, `seesharp-course/skills.yaml` |
| 4 Progress store | done | `617b9cc` | `docs/PROGRESS-STORE.md` |
| 5 Blazor UI | done | `13788b0` | `docs/UI.md` |
| 6 IDE launch | done, extended to VS Code + VS, Linux | `07faa48`, `da2d8f0`, `c5ad8a1` | |
| 7 Admin mode | done | `d75173e` | `docs/UI.md` "Admin mode" |
| 8 Exam mode | done | `60d788f` | `exams/*.yaml` in the course; practice + real exam for levels 0–4 added |
| 9 AI tutor | **not started** | | needs an API-key decision (see below) |
| 10 English | done | `29fd442` | switcher in the nav; proof files in course level 4 |

Course content (`D:\Rider Projects 2\seesharp-course`, no remote): levels 0–9,
55 exercises (29 coding with `starter/`, `tests/`, `solution/`), `skills.yaml`
with 206 ids, `seesharp validate` → 0 errors. Levels 10–15 and the electives
are not authored.

## Decisions you still own

1. **Merge `dev` into `main`.** I'm not allowed to merge without your review.
   `git log --oneline main..dev` lists everything.
2. **Licence.** The repo is CC BY-NC-SA 4.0 like the book. That forbids
   commercial use of the *tool*. Usual split would be MIT for `src/` and
   CC BY-NC-SA for the course content. Also unsettled: whether internal
   company training counts as non-commercial.
3. **Company name in git history.** Removed from all files, still in the Task
   0/1 commits. Rewriting history on a published repo is your call.
4. **Content schema and skill vocabulary** (`docs/CONTENT-FORMAT.md`,
   `skills.yaml`) were built without the Task 3/3a review gate. Skill ids are
   permanent once trainees have progress rows, so review before anyone real
   uses it.
5. **Course repo hosting.** `seesharp-course` has no remote. Trainees are
   meant to clone it; the admin Git page pushes over HTTPS with a token in
   `settings.json`.
6. **AI tutor (Task 9).** Which provider/key handling you want; nothing built.

## How to run

```bash
dotnet run --project src/SeeSharp.Cli -- start
```

Settings live in `%LOCALAPPDATA%\SeeSharp\settings.json` (course path, book
PDF, IDE paths, locale, Git credentials). `--admin` enables the editor.
`seesharp validate <course>` and `seesharp grade <exercise>/tests <source>`
work from the CLI; `seesharp export-review` writes the supervisor bundle.

Your local progress DB (`%LOCALAPPDATA%\SeeSharp\progress.db`) contains my
smoke-test attempts (exercise 0.1 passed, one wrong quiz answer). Delete the
file for a clean start; the course starter for 0.1 is already reverted.

## Linux

Verified on the Ubuntu VM "Claubuntu": .NET 10 SDK installed in `~/.dotnet`,
full test suite green, `seesharp validate` clean, and "In Rider öffnen" from
the Linux UI started Rider with the right arguments. Rider there has never
finished its first-run wizard, so it does not get as far as opening the
project; that's the VM's state, not SeeSharp's.

## Known rough edges

- Rider "Start Failed … still running and does not respond": a running Rider
  that doesn't answer its activation socket. Close it; see `docs/UI.md`.
- `seesharp validate` prints 217 warnings on the course: dead-end skills in
  the last authored level, two starters that intentionally don't compile, and
  128 missing English translations (expected once the level-4 proof files
  exist — either translate or delete `*.en.md` to silence them).
- Admin Git page supports HTTPS remotes only (LibGit2Sharp has no SSH).
- The exam/tutor flags in the UI are only as far as Task 8 got (below).
