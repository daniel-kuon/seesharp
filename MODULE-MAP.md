# SeeSharp — Module Map

Derived from *Einführung in das Programmieren mit C# 14* (Baltes-Götz /
Frischknecht, 1746 pp.) and the accompanying sample archive (391 projects
across 21 topic folders).

References use **chapter and section numbers**, not page numbers — the PDF's
physical page is the printed page + 32, and section numbers are unambiguous.

---

## Ground rules this map assumes

**Instructions point at the book; they never copy it.** An exercise's
`instructions.de.md` says which section to read, then states a task written by
the supervisor. The PDF ships beside SeeSharp as the reading material. This
keeps the content package small, keeps the licensing clean, and roughly halves
authoring effort.

**Only auto-gradable work becomes a coding exercise.** A coding exercise must
be a console or library project whose behaviour is assertable from a test
project. GUI work, database work against a live server, and anything requiring
a network are marked `review` below and are graded by the supervisor, not by
tests.

**The samples are reference, not content.** Every `starter/` and `tests/` pair
must be authored. Use the sample project as the model answer and work backwards
to a skeleton.

---

## Level ladder (core track)

The core track is 15 levels. The book's own chapter order is *not* followed
exactly — the deviations are listed at the end and each has a reason.

| # | Level | Book | Sample folder | Gradable |
|---|-------|------|---------------|----------|
| 0 | Werkzeuge und erstes Projekt | — (replaces ch. 3) | `Entwicklungswerkzeuge` | yes |
| 1 | Was ist OOP / was ist .NET | 1, 2 | — | quiz only |
| 2 | Elementare Sprachelemente | 4.1–4.5 | `Elementare Sprachelemente` | yes |
| 3 | Ablaufsteuerung | 4.6–4.7 | `Elementare Sprachelemente` | yes |
| 4 | Klassen und Objekte | 5.1–5.9 | `Klassen und Objekte/Bruch` | yes |
| 5 | Komposition, Indexer, Schutzstufen | 5.10–5.13 | `Klassen und Objekte/Bruch` | yes |
| 6 | Weitere Typen | 6.1–6.8 | `Weitere .NETte Typen` | yes |
| 7 | Vererbung und Polymorphie | 7 | `Vererbung und Polymorphie` | yes |
| 8 | Typgenerisches Programmieren | 8 | `Typgenerisches Programmieren` | yes |
| 9 | Interfaces | 9 | `Interfaces` | yes |
| 10 | Kollektionen | 11 | `Kollektionen` | yes |
| 11 | Ausnahmebehandlung | 13 | `Ausnahmebehandlung` | yes |
| 12 | Delegaten und Ereignisse | 10 | `Delegaten und Ereignisse` | yes |
| 13 | LINQ-to-Objects | 18 | `LINQ-to-Objects` | yes |
| 14 | C# für Fortgeschrittene | 15 | `C# für Fortgeschrittene` | yes |
| 15 | Dateien und Serialisierung | 16 | `Ein- und Ausgabe über Datenströme` | mostly |

### Level 0 — Werkzeuge und erstes Projekt
Book chapter 3 covers Visual Studio 2026 and VS Code and must be **rewritten
for Rider**. Needed: SDK install check, Rider install, `dotnet new`, solution
anatomy, build/run, breakpoint debugging, reading a compiler error. Also the
moment to teach the SeeSharp workflow itself (open in Rider → edit → submit →
read test output). Grading: one trivial exercise that proves the toolchain
works end to end.

### Level 1 — Konzepte
Chapters 1 and 2 are prose. Pure multiple-choice and open-question level: OOP
vocabulary, assemblies, IL, CLR, JIT vs. AOT, BCL and namespaces. No coding
exercise. Good place to set the expectation that not every level ends in code.

### Levels 2–3 — Elementare Sprachelemente
Section 4.3 (types), 4.5 (operators) and 4.7 (control flow) carry the weight.
Sections 4.3.5 (floating-point representation) and 4.6 (overflow) are the ones
trainees reliably get wrong later — build at least one exercise that makes a
`double` comparison fail visibly. Sample projects `FloP`, `DoubleDecimal`,
`Bits/*` and `GGT.Diff` / `GGT.Mod` map directly onto exercises.

### Levels 4–5 — Klassen und Objekte
**Build these first.** `Bruch/b0` … `b11` is already an ordered progression over
one `Bruch` class: methods and overloading (b1), constructors (b2), duplicate
values (b3), cloning (b4), static members (b5), recursion (b6), operator
overloading (b7), aggregation (b8), `ToString` (b9), `IComparable`/`IClonable`
(b10), `IAdditionOperators` (b11). Each step is a natural exercise with an
obvious test surface. `FractionLib` gives you the library/consumer split for
free.

Skip 5.14 (WPF fraction UI) in the core track — see the review-only list.

### Level 6 — Weitere Typen
Structs and value semantics (6.1), arrays (6.2), strings and `StringBuilder`
(6.3), enums (6.4), tuples (6.6), records (6.7). Boxing (6.1.6) deserves its own
exercise. Skip 6.9 (RSS reader, WPF).

### Levels 7–9 — Vererbung, Generics, Interfaces
`Figuren` / `Basisklassenreferenzen` is the canonical shape hierarchy and tests
cleanly. Note that 7.15 (Erweiterungsmitglieder) and 8.6 cover the new extension
members — C# 14 material with no legacy equivalent, so expect trainees to find
no help online. Chapter 9's `IEnumerable<T>` / `IEnumerator<T>` section (9.5) is
the hardest in the early track; budget two exercises.

### Levels 10–13 — Kollektionen, Ausnahmen, Delegaten, LINQ
Collections before exceptions before delegates before LINQ. Each is a clean
test target. LINQ in particular is almost perfect for auto-grading — pure
input/output over in-memory sequences, no I/O.

### Level 14 — C# für Fortgeschrittene
Null-safety (15.1) and pattern matching (15.2) are the two that change how
trainees write everyday code; `Span<T>` (15.4) and inline arrays (15.5) are
nice-to-have. The `Musterabgleich` folder has ten small projects, one per
pattern kind — nearly one exercise each.

### Level 15 — Dateien und Serialisierung
Streams (16.1), `using` and resource release (16.2), JSON serialisation (16.5).
Gradable with a temp directory per test. File-system watcher and drive-info
samples are demo-only.

---

## Elective tracks (after the core)

| Track | Book | Volume | Gradable |
|-------|------|--------|----------|
| Attribute und Reflexion | 14 | 71 entries | yes |
| Multithreading und async/await | 17 | 271 entries | partly |
| Entity Framework Core | 20 | 269 entries | integration only |
| ADO.NET und SQL-Grundlagen | 19 | 64 entries | integration only |
| WPF | 12 | 512 entries | review only |
| Netzwerkprogrammierung | 21 | 63 entries | review only |
| ASP.NET Core Razor Pages | 22 | 1119 entries | review only |
| Veröffentlichung | 23 | — | review only |

Attribute/Reflexion and EF Core are the two electives closest to everyday
CargoNerds work, so they're the ones worth authoring properly first. Chapter 17
is large and deceptively hard — async exercises need deterministic tests, which
means no `Thread.Sleep` in assertions and no wall-clock timing. Budget extra
time or make it review-only.

ASP.NET Core is a third of the whole archive but is review-only under this
grading model. Decide deliberately whether it belongs in SeeSharp at all or
stays a supervised project.

---

## Review-only exercises (no unit tests)

These stay in the curriculum because they motivate, but they're graded by you:

- 3.3.7 / 5.14 — Bruchkürzungsprogramm with a WPF UI
- 6.9 — RSS-Feed reader
- all of chapter 12
- chapter 19's SSMS / Azure Data Studio walkthroughs
- chapter 23 — publishing and deployment

Model these in the manifest as a `review` item type: the trainee marks it done,
SeeSharp captures the source into the export bundle, and the level unlocks.
Don't fake tests for them.

---

## Deviations from the book's order

- **Exceptions (13) moved before delegates (10) and LINQ (18).** Trainees hit
  exceptions the moment they do collection or file work; leaving them until
  after chapter 12 is too late.
- **LINQ (18) moved before advanced C# (15) and multithreading (17).** LINQ is
  easier than pattern matching and far easier than async, and it pays off
  immediately.
- **Multithreading (17) moved out of the core track entirely.** It's the single
  biggest gradability problem in the book.
- **Chapter 12 (WPF) removed from the core track.** 160 pages that cannot be
  auto-graded, in a framework your trainees may never touch.
- **Chapters 19–23 are electives,** not a continuation. They're application
  domains, not C# language learning.

---

## What's left of Task 0

The inventory work is done for the two uploaded files. The agent still needs to:

1. Confirm that the folder on disk matches these two artifacts and note anything
   extra that was in `D:\Rider Projects 2\SeeSharp` but not uploaded.
2. Strip the archive: 355 of 3564 entries are `bin/`, `obj/` and `.vs/`; also
   drop `.suo`, `.user`, `.cache`, `.vsidx`, `.pdb`, `.dll` and `.exe`.
3. Normalise the mixed `.sln` (176) and `.slnx` (213) solution files to one
   format.
4. Flag that folder and file names contain umlauts and spaces throughout
   (`Entitäten löschen`, `Schere, Stein, Papier`) — every path handling in the
   grading runner must survive this, and it belongs in the test fixtures.
5. Produce `docs/GAPS.md`: which levels above have no usable sample project and
   therefore need an exercise written from nothing.

Then start on level 4 (`Bruch`), not level 0 — it's the richest material and it
will shake out the content schema fastest.
