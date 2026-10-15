# SeeSharp — Skill Model and Condensed Content

This document **amends** `AGENT_TASKS.md`. It must be read before task 3, and it
changes tasks 3, 4, 5, 7, 8 and 9. Where the two documents disagree, this one
wins.

---

## 1. The skill vocabulary

A **skill** is one atomic, observable competency: `types.int`,
`exceptions.throw`, `exceptions.catch-specific`, `oop.constructor-overload`,
`linq.where`. Granularity is deliberately fine — roughly 10–25 skills per level,
~250 across the core track.

### `skills.yaml` — a closed, curated list

All skills live in one file at the root of the content repo. A skill used in a
manifest but absent from this file is a **validation error**, not an
auto-registration. This is the single rule that keeps the graph from rotting
into near-duplicate ids.

```yaml
skills:
  - id: types.int
    category: types
    label: { de: "Ganzzahltyp int verwenden", en: "Use the int type" }
  - id: exceptions.catch-specific
    category: exceptions
    label: { de: "Spezifische Ausnahme abfangen", en: "Catch a specific exception" }
    supersedes: exceptions.catch-any
```

**Two rules for admitting a skill.** It must be *teachable* — some exercise
credits it — and *observable* — a unit test could plausibly fail if the trainee
lacked it. "Understands polymorphism" fails the second test and is not a skill;
`oop.override-virtual` passes.

**Ids are permanent.** They are English, namespaced, lowercase, and never
renamed once published, because progress rows reference them. A rename is a new
id plus an `aliases` entry on it. Labels are translated; ids are not.

---

## 2. Two relations on every exercise

Replace the current `requires: [exercise-id]` with:

```yaml
id: klassen-04-konstruktoren
teaches: [oop.constructor-explicit, oop.constructor-overload, oop.this-chaining]
requires: [types.int, oop.field-private, oop.method-instance]
```

`teaches` — skills credited when the exercise is passed.
`requires` — skills that must already be held before it unlocks.

**Module and level prerequisites are now derived, not authored.** A level
unlocks when the union of its exercises' `requires` is satisfied. Delete
hand-written dependency lists; they are a second source of truth and will drift.

### Three skill states, not a boolean

| State | Earned by |
|---|---|
| `introduced` | the skill appears in a summary the trainee has read |
| `practiced` | an exercise that `teaches` it was passed |
| `applied` | a *later* exercise that `requires` it was passed |

`applied` is what "referencing back to earlier chapters" means mechanically, and
it gives spaced repetition for free: the app can surface skills that are
`practiced` but never `applied`, and the exam generator can prioritise them.

Record every transition with its timestamp and the exercise that caused it. The
supervisor review bundle gets a skill matrix: every skill, its state, when it
was last exercised.

---

## 3. New validator rules (amends task 3)

On top of the existing checks:

- Every skill id in any manifest exists in `skills.yaml`.
- Every skill in any `requires` is taught by at least one exercise — **no
  dangling skills**. This is the rule that catches a curriculum gap
  automatically, and it is the most valuable one here.
- The derived skill graph is acyclic.
- Every exercise's `requires` is satisfiable by exercises that come before it in
  the published order — no forward references.
- Warn on a skill taught by more than three exercises (too coarse — probably
  needs splitting) and on a skill taught but never required (a dead end — fine
  for terminal topics, suspicious elsewhere).

`seesharp validate` should print the curriculum as a skill graph summary:
skills per level, dangling skills, dead ends.

---

## 4. Condensed summaries with links to the book (amends task 5)

Each level carries `summary.de.md`: the short version the trainee reads in the
app, targeting roughly 400–800 words, with the book as the deep dive.

**Do not extract the book's text programmatically.** It's a Word export with
syntax diagrams as embedded graphics and heavy inline formatting; `pdftotext`
output is unusable prose. Summaries are written, with the PDF as the reference.

### Linking into the PDF

The manuscript's **printed page number is the physical page minus 32** —
chapter 1 starts at printed page 1, which is physical page 33. Deep links use
the *physical* page. Store printed section references in the manifest and
convert at render time, so that the offset lives in exactly one constant:

```yaml
reading:
  section: "5.4.3"
  title: { de: "Einsatzvorbereitung" }
  printedPage: 372
```

Render as a link to the bundled PDF at `#page=404`. Keep the offset in one
named constant with a comment, because a new edition of the book will change it.

Optionally render those pages inline with PDF.js so the trainee doesn't leave
the app. Nice, not required — ship the link first.

### Showing skills in the UI

Each summary displays the skills the level teaches as a checklist, and each
exercise shows which previously-earned skills it builds on, linking back to the
level where each was learned. That back-reference is the feature; the skill
graph is what makes it possible.

---

## 5. Downstream effects

**Task 4 (progress store).** Add a skill ledger: one row per skill-state
transition, with timestamp and causing exercise. Current state is derived.
Include the skill matrix in the review export.

**Task 7 (admin mode).** The exercise editor picks skills from `skills.yaml` via
autocomplete — never free text. Add a skill-graph view showing coverage and
dangling skills, and make editing `skills.yaml` a deliberate, separate action.

**Task 8 (exam mode).** An exam is defined by *skill coverage*, not a hand-picked
item list: "cover every skill in levels 1–6, prioritising those not yet
`applied`." Practice exams can then be generated fresh each time instead of
being authored.

**Task 9 (AI tutor).** The tutor's prompt receives the trainee's currently-held
skills and the skills the current exercise teaches, with an explicit
instruction: do not introduce concepts outside that set, and when a trainee's
approach needs an unlearned concept, say which level covers it rather than
teaching it early. This is the main reason the skill graph is worth building.

---

## 6. Licensing (now that both are open source)

The manuscript can be bundled and displayed. Still required: ship its licence
text alongside the PDF, attribute Baltes-Götz and Frischknecht on any screen
showing their content, and state in SeeSharp's own README that the summaries are
condensed derivative works of the manuscript with the licence that implies.
Check whether the book's licence is share-alike before choosing SeeSharp's — if
it is, the content repo's licence is constrained even if the tool repo's is not.

---

## 7. Build order change

The skill vocabulary for levels 0–6 must exist before task 3 defines the schema,
and the full vocabulary before task 5 builds gating. Insert as **task 3a**:

> Draft `skills.yaml` for levels 0–6 from the module map, with ids, categories
> and German labels. Map each `Bruch/b*` sample onto a `teaches` set. Output the
> skill graph as a diagram for review. No application code.

This is a review gate. The vocabulary is the hardest thing here to change later —
every progress row in every trainee's database points at it.
