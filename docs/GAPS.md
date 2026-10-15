# SeeSharp — Gaps

Where the book and the sample code don't line up. Paths are relative to
`samples/`. Book references are verified against `csharp14.pdf`; the book's
own exercises are catalogued in `INVENTORY.md` §4.

## 1. Book topics with no usable code (core track)

These exercises are written from nothing.

| Level | Topic | Book | Note |
|---|---|---|---|
| 0 | Rider workflow: install, `dotnet new`, debugging | 3 (rewritten) | Book uses VS 2026 / VS Code. Nothing Rider-specific exists. |
| 0 | Reading compiler errors | 3.7/2 | Book gives a broken Hallo program; no sample solution. |
| 1 | OOP / .NET concepts | 1, 2 | Prose only. Book supplies 10 true/false statements and 4 open questions (1.8, 2.8) — a start, not enough. |
| 2 | Operators: precedence, `++`, boolean logic | 4.5 | Book has quiz items (4.5.11/1–3, 5); no code sample. |
| 3 | Integer overflow, `checked` / `unchecked` | 4.6 | Only `OverflowCheckBenchmark` (BenchmarkDotNet timing). No book exercise either. |
| 3 | Control-flow bugs; `switch` expressions, loop forms, `break` / `continue` | 4.7 | Book has 4 fix/code items (4.7.4/1–4) with no sample solutions. Control flow appears only incidentally in samples (`GGT.*`, `PerST`). Thinnest core level. |
| 6 | Boxing | 6.1.6 | Only book question 6.10/1; samples time / analyse allocations. |
| 6 | Records (value equality, `with`, positional) | 6.7 | Nothing in `Weitere .NETte Typen`; `Vererbung und Polymorphie/Record-Figuren` covers inheritance only. |
| 6 | Anonymous classes, `using` alias | 6.5, 6.8 | No sample, no book exercise. |
| 7 | Base-constructor call; private base fields | 7.4, 7.16/1–2 | Book fix exercises without sample solutions. |
| 8 | `Max<T>()` with generic `params`; constraints beyond `IComparable<T>` | 8.5, 8.8/3 | 5 projects in the whole folder — smallest core folder. Book exercise has no sample. |
| 10 | `List<T>` basics; `Queue`, `Stack`, `LinkedList`; collection expressions | 11.3, 11.4 | 5 projects, all Set/Dictionary or timing. Book has only a quiz and the Lotto repeat. |
| 14 | Age recoding by patterns; `PKW.Zugelassen()` | 15.7/2–3 | Book exercises without sample solutions (`Merkmalsmuster` is a related example). |
| 14 | Inline arrays | 15.5 | No sample, no book exercise. |

Thin but usable (one tiny sample, expect to extend): composite formatting
(`FormAus`, 3 LOC), events (`EventPublisher` is the only console event sample),
`Exp`.

**Chapters with no exercises at all:** 19–23 (ADO.NET, EF Core, networking,
ASP.NET Core, publishing). Any elective built on them is authored from nothing.

**Book exercises with no sample solution** (14 of 46 fix/code items): 3.7/2,
4.2.4/1, 4.3.11/2–3, 4.7.4/1–3, 5.15/3, 7.16/1–2, 8.8/3, 15.7/2–3, 17.8/2.
The 14 true/false sets and 19 open questions have no model answers anywhere;
the supervisor writes them.

## 2. Code with no core-track home

| Path | Why it's out |
|---|---|
| `Einleitungsbeispiel Bruchrechnen/*` (5) | Intro example for ch. 3; 2 of 5 are WPF. Optional at level 0. |
| `Entwicklungswerkzeuge/DmToEuro`, `Elementare Sprachelemente/DmToEuro*` (3) | WPF. |
| `Elementare Sprachelemente/InputBox`, `MessageBox` | WinForms. |
| `Elementare Sprachelemente/VB` | VB.NET contrast example. |
| `Elementare Sprachelemente/Program` | Empty "space to practise" stub. |
| `*Benchmark*` (3), `Kollektionen/ImmutableList-Performance`, `Typgenerisches Programmieren/Listenwerte`, `Weitere .NETte Typen/Strukturen/{Aufwandsvergleich,Clr Heap Allocation Analyzer}`, `Klassen und Objekte/Bruch/b0 Aufwand beim Eigenschaftszugriff` | Timing / allocation demos. Wall-clock results are not assertable. |
| `Klassen und Objekte/Finalisierer` | GC timing is non-deterministic. |
| `Ein- und Ausgabe über Datenströme/{FileShare/*,FileSystemWatcher,DriveInfo}` | Machine- / timing-dependent. |
| `LINQ-to-Objects/PLINQ/*` (5) | Belongs to the multithreading elective. |
| `Ausnahmebehandlung/HandleUnhandledException`, `Delegaten und Ereignisse/Ereignisse/ApplicationExit`, `Attribute/{Flags,WpfApp,WpfOhneSTAThreadAttribute}` | WPF. |
| `Interfaces/Statische Mitglieder/Statische Polymorphie/*` | Advanced; stretch item at most. |

Elective folders (`Attribute`, `Multithreading`, `Entity Framework Core`,
`ADO.NET`, `WPF`, `Netzwerk`, `ASP.NET Core`) are code without a core level by
design — see the elective table in `MODULE-MAP.md`. Two practical notes:

- `ADO.NET` and all 46 `Entity Framework Core` projects need a live SQL Server
  (`ADO.NET/Northwind` ships an `.mdf`). Gradable only with a LocalDB or
  SQLite substitution — a decision for whoever authors those electives.
- `ASP.NET Core/Razor/PartPref/{Gerüst,Fertig}` is the archive's only
  skeleton/solution pair — a ready-made review-only exercise.

## 3. Mismatches between the module map, the book and the archive

- **Records** sit under chapter 7's folder, not chapter 6's (see §1).
- **`Bruch/b10` and `b11`** depend on interfaces and generic math (ch. 9), so
  they can't be level 5 exercises; moved to level 9 in `MODULE-MAP.md`.
- **`ToString` override** is book exercise 7.16/5 (chapter 7) but sample
  `Bruch/b9` and level 5 in the map. Kept at level 5; it needs no inheritance
  beyond `object`.
- **Junk count**: the module map's "355 of 3564 entries" included directory
  entries; 251 files were actually removed.
- **Stale TOC**: 90 TOC entries (chapters 4, 17, 18, 20) point 1–10 pages past
  the real heading. Page numbers must be taken from the body text.
- **`SKILL-MODEL.md` §4 example**: printed page of 5.4.3 is 340, not 372 (372
  is physical).
- **Chapter 18 (LINQ)** is flagged by the authors as still at C# 12 / .NET 8.
- **Licence is CC BY-NC-SA 4.0**: non-commercial *and* share-alike. See
  `INVENTORY.md` §1.1. Needs a decision before content is published.
- **Level 14 pattern projects** don't map one-to-one onto the book's ten
  pattern kinds; the constant and residual patterns have no project.
