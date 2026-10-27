# Content format (task 3 / 3a)

The on-disk format of the `seesharp-course` repository, as implemented in
`src/SeeSharp.Content` and checked by `seesharp validate <course-path>`. The
review gates for tasks 3 and 3a were waived; every decision that was not
spelled out in `AGENT_TASKS.md` or `SKILL-MODEL.md` is marked **Decision** so
it can be revisited before the UI (task 5) builds on it.

## Layout

```
<course>/
  skills.yaml                 closed skill vocabulary (SKILL-MODEL §1)
  global.json                 pins the SDK used when grading (copied from the tool repo)
  modules/
    04-klassen-und-objekte/   NN-slug; NN is the level number and the published order
      module.yaml
      summary.de.md           condensed reading for the level (SKILL-MODEL §4)
      01-konstruktoren/       NN-slug; published order within the level
        manifest.yaml
        instructions.de.md    rendered to the trainee
        rubric.md             supervisor only, never rendered
        solution/             optional model answer, same layout as starter/; supervisor only, ignored by loader and grading
        starter/              the project the trainee edits (coding exercises only)
        tests/                the authoritative test project (exactly one .csproj)
```

**Decision — order and level come from folder names only.** `module.yaml` has
no `level` or `order` field; the two-digit prefix is the single source. Folder
names must match `NN-[a-z0-9-]+`; anything else under `modules/` or a module
folder is an error, not ignored, so a typo cannot silently drop an exercise.

**Decision — `summary.de.md` and `instructions.de.md` are named by locale**
and the default locale is `de`. An English summary is `summary.en.md`, an
English instruction file `instructions.en.md`; the validator only requires the
default locale's file. The UI shows the file for the current language and
falls back to the German one with a visible note (docs/UI.md, *Localisation*).

**Decision — `rubric.md` missing is a warning**, not an error. It is never
rendered, so a missing one breaks nothing for the trainee.

**Decision — `tests/` is the overlay for `GradingRunner`** exactly as task 2
defined it: `tests/<Test Project>/x.Tests.csproj` referencing
`../<Trainee Project>/x.csproj`, where `<Trainee Project>` lives under
`starter/`. The validator runs `GradingRunner.RunAsync(tests, starter)` for the
starter check, so the same layout is used for grading later. Project folder
names may contain spaces and umlauts (`b7 Operatoren-Überladung`).

## `skills.yaml`

```yaml
skills:
  - id: oop.constructor-explicit        # category.name, lowercase, permanent
    category: oop
    label: { de: "…", en: "…" }         # de required, en optional
    supersedes: oop.constructor          # optional, must exist
    aliases: [oop.ctor-explicit]        # optional, old ids that resolve to this one
```

- Id pattern: `^[a-z][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)+$`.
- **Decision — no `level` field on a skill.** The level is derived from the
  exercises that teach it, so the vocabulary cannot drift from the content.
  `docs/SKILLS.md` in the course repo groups the draft by level for review.
- **Decision — aliases resolve.** A manifest may reference an alias; the
  validator maps it to the canonical id for all graph checks. An alias that
  equals another id or alias is an error.
- The vocabulary for levels 0–6 (148 skills) is drafted; the b0–b9 mapping and
  a Mermaid graph are in the course repo's `docs/SKILLS.md`.

## `module.yaml`

```yaml
id: klassen-und-objekte                  # ^[a-z0-9][a-z0-9-]*$, unique
title: { de: "Klassen und Objekte" }
reading:                                 # optional, same shape as on exercises
  section: "5"
  title: { de: "Klassen und Objekte" }
  printedPage: 286
```

**Decision — `reading` on modules is optional** and points at the chapter the
summary condenses; exercises carry their own section-level pointer.

## `manifest.yaml`

```yaml
id: klassen-konstruktoren                # ^[a-z0-9][a-z0-9-]*$, unique across the course
title: { de: "Konstruktoren" }
reading:                                 # optional
  section: "5.4.3"
  title: { de: "Einsatzvorbereitung" }
  printedPage: 340                       # printed page; physical = printed + 32 (Reading.PrintedToPhysicalOffset)
teaches: [oop.constructor-explicit, …]   # skill ids credited on pass
requires: [oop.method-instance, …]       # skill ids that must be held to unlock
optional: true                           # optional, default false: a Zusatzaufgabe (see below)
items:
  - type: coding
    id: bruch                            # item id, unique within the exercise
    openFile: "b2 Konstruktoren/Bruch.cs" # relative to starter/
    openLine: 8                          # optional
    testProject: "b2 Tests/b2.Tests.csproj" # relative to tests/
    requiredTests: [BruchTests.Kuerze_teilt_durch_den_ggT] # optional; absent = all
  - type: multipleChoice
    id: q1
    prompt: { de: "…" }
    options: [{ de: "…" }, { de: "…" }]
    correct: [1]                         # indices into options; several = multi-select
    explanation: { de: "…" }             # optional
  - type: open
    id: q2
    prompt: { de: "…" }                  # gates on answered, never graded
  - type: review
    id: r1
    prompt: { de: "…" }                  # supervisor-graded; trainee marks done
    openFile: "…"                        # optional, relative to starter/
```

- `teaches` / `requires` hold **skill ids**, not exercise ids (SKILL-MODEL §2).
  Module and level prerequisites are derived, never authored.
- **Decision — every item has an `id`.** Progress rows (task 4) need to
  reference items, not just exercises.
- **Decision — `requiredTests` is a list or absent.** The spec's `all` keyword
  is expressed by omitting the field; a list of test names restricts the gate.
  Names are not checked by the validator (they only exist after a test run).
- **Decision — item types are a closed set per tool version**, resolved from
  the `type` key via `ItemTypes.ByName`. Adding a type is one subclass of
  `ExerciseItem` plus one dictionary line; old manifests are unaffected. An
  unknown type, an unknown key, or a wrong value type is a parse error for the
  whole manifest, so typos are caught rather than ignored.
- **Decision — one `tests/` folder per exercise.** `testProject` only picks
  the project file inside it for the IDE; the grader finds the single
  `.csproj` itself. Several coding items in one exercise share the test
  project.
- Multiple choice: at least two options, `correct` non-empty and in range.

### Optional exercises (Zusatzaufgaben)

`optional: true` marks extra practice a trainee may do but never has to. An
optional exercise unlocks like any other (by its `requires`), records attempts
and credits skills normally when passed (`teaches` → practiced, `requires` →
applied), but it never blocks a module from counting as passed, never locks an
exam and is never picked for one. The UI lists it under "Zusatzaufgaben
(optional)" on the module page.

- **Folder convention:** optional exercises come after the mandatory ones of
  their module and are numbered from 10 (`10-…`, `11-…`), so the mandatory
  sequence keeps its numbers when extras are added.
- **Rule — mandatory progression never depends on optional work.** A mandatory
  exercise whose `requires` skill is taught, earlier in published order, only
  by optional exercises is an error (`SkillRequiresOptionalOnly`).
- **Rule — optional exercises deepen, they never introduce.** An optional
  exercise teaching a skill that no mandatory exercise teaches anywhere is an
  error (`OptionalTeachesNewSkill`).
- Forward-reference, dangling and dead-end checks treat optional exercises like
  any other. The "taught by more than three exercises" warning counts
  mandatory teachers only.

### Deep Dive (topics the book does not cover)

User decision 2026-10-27: material the book does not discuss stays in the
course but is optional and presented as a pointer for further study. Every
mandatory exercise and quiz must be passable with book material plus what
earlier levels taught.

- **Summary:** a final `## Deep Dive (optional)` section opening with
  `> Nicht im Buch. Diese Themen sind optional – Beispiele dafür, was du dir als
  Nächstes ansehen kannst.` (English: `> Not in the book. These topics are
  optional – examples of what to look into next.`).
- **Instructions:** a beyond-book part sits in a `> **Deep Dive (optional):** …`
  block (the UI styles blockquotes as a box). Its tests stay out of the gate via
  `requiredTests`, or the part moves to an optional exercise.
- **Optional exercises** that are mainly beyond the book get the title prefix
  `Deep Dive: `.
- **Quizzes and rubrics:** no required point needs beyond-book knowledge; such
  points are marked "Bonus (Deep Dive)".
- A one-off API call the instructions hand over completely is not a topic; it
  is marked "(nicht im Buch, hier vorgegeben)".

## Validator rules

`seesharp validate <course-path> [--skip-starter-check]`. Exit 0 with no
errors, 1 with errors, 2 on usage. Every rule has a `FindingCode`; the message
text is in `SeeSharp.Content/Strings.resx` (German), tests assert on the code.

Errors:

- `skills.yaml` missing or unparseable; skill id invalid or duplicate; missing
  category; missing `de` label; `supersedes` unknown; alias conflict.
- `modules/` missing; misnamed module or exercise folder; `module.yaml` or
  `manifest.yaml` missing or unparseable; invalid or duplicate module,
  exercise, or item id; title or prompt or option or explanation missing in
  `de`; `summary.de.md` or `instructions.de.md` missing; incomplete `reading`.
- `items` empty; too few options; invalid `correct`; `starter/` or `tests/`
  missing; `tests/` without exactly one `.csproj`; `openFile` or
  `testProject` not found (paths escaping their folder count as not found).
- **Starter passes its tests** (`StarterPassesTests`). The solution shipped.
- Starter test run timed out.
- Skill referenced but not in `skills.yaml`; skill in both `teaches` and
  `requires` of one exercise; **dangling** (required, taught nowhere);
  **forward reference** (required, taught only later in published order);
  cycle in the derived skill graph.
- A mandatory exercise requires a skill taught earlier only by optional
  exercises (`SkillRequiresOptionalOnly`); an optional exercise teaches a skill
  no mandatory exercise teaches (`OptionalTeachesNewSkill`).

Warnings:

- `rubric.md` missing; module without exercises; `teaches` empty.
- Starter does not compile together with the tests (legitimate for a
  "fix the compiler error" exercise).
- Skill taught by more than three mandatory exercises; skill taught but never required
  (dead end).
- `TranslationMissing`: a module or exercise lacks `summary.xx.md` /
  `instructions.xx.md` or an `xx` title key for a non-default locale that at
  least one other module or exercise provides. A course with no translation at
  all gets no such warning, so translating can start one file at a time.

**Decision — dead ends are warnings even for the last exercises of the
course**, so a validated course is "no errors", not "no output". The two
level-4 exercises currently produce five dead-end warnings because nothing
later requires them yet.

**Decision — the starter check uses `SeeSharp.Grading`**, so
`SeeSharp.Content` references `SeeSharp.Grading` (which only references
`SeeSharp.Core`). The alternative, running the check in the CLI, would have
left the most valuable rule untested in the content test suite.

The summary printed after the findings lists, per level, the mandatory exercise
count and the number of optional exercises (Zusatzaufgaben), skills taught (and how many for the first time), skills required, then the
totals, the dangling list and the dead-end list.

## `exams/<slug>.yaml` (task 8)

Optional folder; every `*.yaml` in it is one exam definition. An exam is
defined by **skill coverage** (SKILL-MODEL.md §5), never by an item list:

```yaml
id: probepruefung-level-0-4             # ^[a-z0-9][a-z0-9-]*$, unique
title: { de: "Probeprüfung Level 0–4" }
kind: practice                           # practice | real
coverage:
  levels: [0, 1, 2, 3, 4]                # module folder prefixes
skills: [oop.constructor-explicit]       # optional, in addition to or instead of levels
timeLimitMinutes: 60
maxItems: 20                             # optional cap on the generated run
```

- **Covered skills** = every skill the exercises of the listed levels teach,
  plus the explicit `skills` list. **Covered exercises** = the exercises of
  those levels plus every exercise teaching a listed skill (`ExamScope` in
  `Model.cs`; the validator and the exam generator share it). The trainee must
  have passed every covered exercise before the exam unlocks. Optional
  exercises are never covered: they neither lock an exam nor supply items, and
  their `teaches` do not add covered skills.
- **Decision — the file name is free, the `id` is the key.** Progress rows
  reference the id (a real exam is "taken once per id"), so renaming the file
  is harmless and renaming the id is a new exam.
- **Decision — review items are never part of an exam.** The supervisor
  grades them; an exam needs an automatic verdict. Multiple choice, open and
  coding items are picked alike.

Validator rules (errors): file unparseable (`ExamManifestInvalid`), invalid or
duplicate id, title missing in `de`, `kind` not `practice`/`real`,
`timeLimitMinutes` < 1, a level no module has (`ExamLevelUnknown`), a skill not
in `skills.yaml` (`ExamSkillUnknown`), and coverage that resolves to no skill
at all (`ExamCoverageEmpty`). `seesharp validate` prints one line per exam:
kind, levels, covered skills, covered exercises, pickable items, time limit.

## Translating the course

`tools/translate_course.py` machine-translates German content to English with
the DeepL API (free tier, standard library only):

```
python -I tools/translate_course.py <course> --key-file <deepl.key> [--dry-run] [--only GLOB ...] [--max-chars N] [--ca-file BUNDLE]
```

- **Markdown:** every `summary.de.md` / `instructions.de.md` without an
  `.en.md` twin gets one. Fenced code is copied verbatim; inline code, URLs and
  link targets are sent as `<code translate="no">` / restored from a side
  table, so test names and identifiers come back byte for byte (a unit whose
  code went missing aborts the run). Hard-wrapped lines are joined into one
  unit per paragraph or list item; the English files are not re-wrapped.
- **YAML:** in `module.yaml`, `manifest.yaml` and `exams/*.yaml`, every locale
  map with `de` and no `en` gets an `en` value. A line-based editor inserts it
  (`, en: "…"` into a flow map, an `en:` line after a block-map `de:`) and
  leaves every other byte alone; values are Markdown-protected like the files.
- **Idempotent and resumable:** existing `.en.md` files and `en` keys are never
  touched, each file is written atomically, so a rerun continues where an
  interrupted run stopped. `--max-chars` stops before a batch would exceed it.
- **Budget:** DeepL bills only the text outside tags and outside
  `translate="no"` elements (checked against `/v2/usage` to the character);
  `--dry-run` prints exactly that per file type and module, calling only
  `/v2/usage`. Identical texts are sent once per run.
- **Glossary:** `GLOSSARY` in the script pins course terms DeepL got wrong
  (`Zusatz:` → `Extra:`, `Zusatzaufgabe` → `optional exercise`, `ggT` → `GCD`).
- **Review the output.** DeepL places protected code by alignment and
  occasionally puts a span in the wrong spot; a span glued inside a word is
  printed as a warning. Book section titles are translated too (the book is
  German), and the result needs a read-through before it is committed.
- If Python's Windows certificate store fails (`certificate has expired`),
  pass `--ca-file` with a CA bundle, e.g. Git's
  `C:\Program Files\Git\mingw64\etc\ssl\certs\ca-bundle.crt`.

## Known gaps

- With only levels 4.3 and 4.8 authored, the manifests cannot require
  level 0–3 skills (that would be dangling). Their intended `requires` is
  documented in the course repo's `docs/SKILLS.md`; fill them in as the earlier
  levels get written.
- `requiredTests` names are unverified.
- No `--json` output for `validate`; the admin UI (task 7) calls the validator in-process instead.
