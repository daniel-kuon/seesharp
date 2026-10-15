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
business-application work, so they're the ones worth authoring properly first. Chapter 17
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

## Section-number check (against `csharp14.pdf`)

Every chapter and section number used in this map exists in the book, and its
title matches the topic claimed. The table gives the verified **printed** start
page of each level's reading. These come from the body text, because the TOC is
stale in chapters 4, 17, 18 and 20 (see `INVENTORY.md` §1.1). The physical
page is the printed page + 32.

| Level | Book | Book title | Printed | Physical |
|---|---|---|---:|---:|
| 0 | 3 | Werkzeuge zum Entwickeln von C#-Software | 61 | 93 |
| 1 | 1 / 2 | Einstieg in die OO-Software-Entwicklung / Grundzüge der .NET-Technologie | 1 / 19 | 33 / 51 |
| 2 | 4.1–4.5 | Elementare Sprachelemente | 161 | 193 |
| 3 | 4.6–4.7 | Über- und Unterlauf / Anweisungen | 244 | 276 |
| 4 | 5.1–5.9 | Klassen und Objekte | 286 | 318 |
| 5 | 5.10–5.13 | Aggregation … Schutzstufen | 388 | 420 |
| 6 | 6.1–6.8 | Weitere .NETte Typen | 431 | 463 |
| 7 | 7 | Vererbung und Polymorphie | 543 | 575 |
| 8 | 8 | Typgenerisches Programmieren | 589 | 621 |
| 9 | 9 | Interfaces | 609 | 641 |
| 10 | 11 | Kollektionen | 691 | 723 |
| 11 | 13 | Ausnahmebehandlung | 885 | 917 |
| 12 | 10 | Delegaten und Ereignisse | 657 | 689 |
| 13 | 18 | LINQ-to-Objects | 1209 | 1241 |
| 14 | 15 | C# für Fortgeschrittene | 949 | 981 |
| 15 | 16 | **Dateiverarbeitung** | 1013 | 1045 |

Electives: 14 Attribute und Reflexion (921), 17 Multithreading (1061),
20 EF Core (1399), 19 Datenbankgrundlagen und ADO.NET (1257), 12 WPF (725),
21 Netzwerkprogrammierung (1497), 22 ASP.NET Core Razor Pages (1537),
23 Veröffentlichung (1637).

Spot-checked sub-references, all correct: 3.3.7 (GUI project, p. 103), 4.3.5
(floating-point representation, 189), 4.6 (overflow, 244), 5.14 (WPF fraction
program, 400), 6.1.6 (boxing, 450), 6.9 (RSS reader, 505), 7.15 / 8.6
(extension members, 581 / 604), 9.5 (`IEnumerable<T>`, 647), 15.1 / 15.2 / 15.4
/ 15.5 (null safety, patterns, `Span<T>`, inline arrays), 16.1 / 16.2 / 16.5,
19.4 / 19.5 (SSMS 1279, Azure Data Studio 1288). Chapter 12 does span 160
printed pages (725–884).

**Corrections:**
1. **Level count.** The ladder is levels 0–15, so the core track has **16**
   levels, not 15.
2. **Level 14 patterns.** The book covers ten pattern kinds (15.2.1–15.2.10).
   The `Musterabgleich` folder also has ten projects, but not one per kind:
   - no project for the constant pattern (15.2.1) or residual patterns (15.2.2);
   - the tuple pattern has two projects;
   - `TollCalculator` combines several kinds.
3. **`SKILL-MODEL.md` §4 example.** Section 5.4.3 "Einsatzvorbereitung" is on
   **printed** page 340. 372 is its *physical* page. As written, the example
   would link to `#page=404`; the correct link is `#page=372`.
4. **Level 15 title.** The book chapter is "Dateiverarbeitung". "Ein- und
   Ausgabe über Datenströme" is only the sample folder's name.
5. **Level 6 omissions.** 6.5 (anonymous classes) and 6.8 (`using` alias) were
   missing from the exercise list; added as 6.10.
6. **Overflow (4.6) belongs to level 3** by the ladder. The overflow exercise
   is 3.6, not 2.5.
7. **LINQ is dated.** The book flags chapter 18 as still at C# 12 / .NET 8.
   Level 13 summaries should be checked against current LINQ (e.g.
   `CountBy`, `Index`) when written.

---

## Exercise breakdown (core track)

Source paths are relative to `samples/`. **NEW** = no usable sample; the
exercise is written from nothing. *demo* = the sample only prints or times
something. Most samples print rather than return, so for nearly every exercise
the logic has to be lifted into a testable method (see `INVENTORY.md` §3).
`quiz` = multiple-choice / open items, no code.

Section numbers have been checked against the book; see "Section-number check"
above. "Book" references like `4.7.4/5` mean exercise 5 in book section 4.7.4
(catalogued in `INVENTORY.md` §4).

### Level 0 — Werkzeuge und erstes Projekt (ch. 3, rewritten for Rider)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 0.1 | Hallo SeeSharp: method returns a greeting | `Entwicklungswerkzeuge/Hallo/CLI` | — |
| 0.2 | Use a class library from a console project | `Entwicklungswerkzeuge/Verweise und Abhängigkeiten/PRDemo/{KonsPro,LibPro}` | 3.3.10 |
| 0.3 | Fix a broken Hallo program (starter does not compile) | **NEW** | 3.7/2 |
| 0.4 | Debugger walkthrough in Rider (`review`) | **NEW** — book covers VS / VS Code only | 3.3.9 |
| 0.5 | Toolchain quiz: SDK selection, build output, what ships | **NEW** | 3.7/3–4 |
| 0.6 | Optional: run the Bruch intro example end to end | `Einleitungsbeispiel Bruchrechnen/VS Top-Level Statements` | 1.3 |

### Level 1 — Konzepte (ch. 1–2)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 1.1 | OOP vocabulary (`quiz`) | **NEW** | 1.8/1–2 |
| 1.2 | .NET platform, assemblies vs namespaces, IL / BCL / JIT (`quiz`) | **NEW** | 2.8/1–3 |

### Level 2 — Elementare Sprachelemente (4.1–4.5)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 2.1 | Primitive types, literals, scope, `char`→`int` (`quiz` + fix) | `Elementare Sprachelemente/Primitiv`, `PrimitivOBC` | 4.1.7/1–3, 4.3.11/1–4 |
| 2.2 | Floating-point trap: find the largest i with 1 + 2^−i ≠ 1; `double` vs `decimal` | `FloP`, `DoubleDecimal` | 4.7.4/5, 4.3.5 |
| 2.3 | Bit patterns of `char` / `float` / `double` / `decimal` | `Bits/*` | 4.3.5 |
| 2.4 | String concatenation trap and aligned formatting | `FormAus`, `Exp` | 4.2.4/1–2, 4.5.11/4 |
| 2.5 | Operators: precedence, `++`, `?:`, boolean / De Morgan (`quiz`) | **NEW** | 4.5.11/1–3, 5 |

### Level 3 — Ablaufsteuerung (4.6–4.7)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 3.1 | GCD by subtraction (loops) | `GGT.Diff` | 4.7.4/6 |
| 3.2 | GCD by modulo | `GGT.Mod` | 4.7.4/6 |
| 3.3 | `switch` on strings | `PerST` (args-driven; recast as a method) | 4.7.2 |
| 3.4 | Control-flow bugs: dangling `else`, `if (b = false)`, `while (…);` | **NEW** | 4.7.4/1, 2, 4 |
| 3.5 | Prime test without `break` / `continue`; `switch` expression, `foreach`, `do` | **NEW** | 4.7.4/3 |
| 3.6 | Integer overflow and `checked` | **NEW** — only `OverflowCheckBenchmark` (benchmark) exists | 4.6.1 |

### Level 4 — Klassen und Objekte (5.1–5.9)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 4.0 | Name the 12 OOP terms in a short `Bruch` (`quiz`) | `Klassen und Objekte/Bruch/Begriffe lokalisieren.pdf` | 5.15/7 |
| 4.1 | Fields, properties, validation; fix a self-recursing getter | `Bruch/b0` | 5.15/1–3 |
| 4.2 | Method overloading | `Bruch/b1 Methoden-Überladung` | 5.3 |
| 4.3 | Constructors and `this` chaining | `Bruch/b2 Konstruktoren` | 5.4.3 |
| 4.4 | Duplicate values / reference semantics | `Bruch/b3 DuplWerte` | 5.4.4 |
| 4.5 | Cloning | `Bruch/b4 Klone` | 5.4 |
| 4.6 | Static members | `Bruch/b5 Statische Member` | 5.8 |
| 4.7 | Recursion: factorial, Fibonacci | `Bruch/b6 Rekursion`, `Klassen und Objekte/Rekursion` | 5.15/5 |
| 4.8 | Operator overloading | `Bruch/b7 Operatoren-Überladung` | 5.9.3 |
| 4.9 | `required` members, local methods, `ref readonly` parameters | `required`, `Lokale Methoden`, `ref readonly - Parameter` | 5.7, 5.9 |

### Level 5 — Komposition, Indexer, Schutzstufen (5.10–5.13)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 5.1 | Aggregation | `Bruch/b8 Aggregation` | 5.10 |
| 5.2 | Override `ToString` | `Bruch/b9 ToString-Überschreibung` | 7.16/5 |
| 5.3 | Indexer | `Indexer` | 5.12 |
| 5.4 | Nested classes | `Geschachtelte Klassen` | 5.11 |
| 5.5 | R² vector class: length, normalise, add, dot, angle, rotate | `R2Vek` | 5.15/4 |
| 5.6 | Library vs consumer, `public` / `internal` | `Bruch/FractionLib` | 5.13 |

`b10` and `b11` need interfaces and generic math, so they move to level 9,
where they double as `applied` credit for the level 4–5 Bruch skills.
`Finalisierer` is non-deterministic (GC): *demo*, not gradable.

### Level 6 — Weitere Typen (6.1–6.8)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 6.1 | Struct value semantics | `Weitere .NETte Typen/Strukturen/Punkt` | 6.1.1 |
| 6.2 | Boxing: why two boxed `1`s are not `==` | **NEW** — `Aufwandsvergleich` / `Clr Heap Allocation Analyzer` are timing / analyzer demos | 6.1.6, 6.10/1 |
| 6.3 | Sieve of Eratosthenes | `Arrays/Eratosthenes` | 6.10/3 |
| 6.4 | `float` matrix with transpose | `Arrays/FloatMatrix` | 6.10/4 |
| 6.5 | Lotto: Fisher–Yates shuffle and selection sort (seeded `Random`) | `Arrays/Lotto`, `SortSearch`, `UniRand` | 6.10/2 |
| 6.6 | `StringUtil.WrapLine()`; lucky number from a name | `Zeichenfolgen/StringUtil`, `PerZuf`, `StringBuilder` | 6.10/5–6 |
| 6.7 | Enums | `Enumerationen` | 6.4 |
| 6.8 | Tuples and `Deconstruct` | `Struktur-Tupel/*` | 6.6 |
| 6.9 | Records (value equality, `with`) | **NEW** here — only `Vererbung und Polymorphie/Record-Figuren` (inheritance angle) | 6.7 |
| 6.10 | Anonymous classes, `using` alias (`quiz`) | **NEW** | 6.5, 6.8 |

### Level 7 — Vererbung und Polymorphie (7)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 7.1 | Shape hierarchy, base-class references | `Figuren`, `Basisklassenreferenzen` | 7.3, 7.7 |
| 7.2 | `virtual` / `override` | `Polymorphie`, `Verdeckte Virtualität` | 7.9 |
| 7.3 | Abstract classes | `abstract` | 7.13 |
| 7.4 | Constructors and `protected`: fix a derived class that can't call base / reach base fields | `protected` | 7.4, 7.5, 7.16/1–2 |
| 7.5 | Hiding with `new`; overload vs hide vs override (`quiz`) | `Methoden ausblenden/*`, `Felder ausblenden` | 7.6, 7.16/3 |
| 7.6 | C# 14 extension members | `Erweiterungsmitglieder/*`, `DoubleExtensions` | 7.15 |
| 7.7 | Record hierarchies: fix lost positional values | `Record-Figuren` | 7.16/4 |

### Level 8 — Typgenerisches Programmieren (8)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 8.1 | Generic stack | `Typgenerisches Programmieren/SimpleStack` | 8.2.1 |
| 8.2 | Sorted list with `where T : IComparable<T>` | `SimpleSortedList` | 8.2 |
| 8.3 | `default(T)` | `default` | 8.7 |
| 8.4 | Generic extension methods | `Generische Erweiterungsmethoden` | 8.6 |
| 8.5 | `Max<T>()` with a generic `params` parameter; further constraints | **NEW** | 8.8/3, 8.5 |
| 8.6 | Generics quiz: value-type arguments, variance, inheritance | **NEW** | 8.8/1 |

`Listenwerte` (List vs ArrayList timing, 8.8/2) is *demo* only.

### Level 9 — Interfaces (9)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 9.1 | Implement an interface | `Interfaces/Interface-Datentypen`, `Fimplement` | 9.2, 9.3 |
| 9.2 | Explicit implementation | `Explizite Implementation` | 9.4 |
| 9.3 | `IComparable<T>` / `ICloneable` on Bruch | `Klassen und Objekte/Bruch/b10 IComparable, IClonable` | 9.6/1 |
| 9.4 | Hand-written `IEnumerator<T>` | `Enumeratoren` | 9.5 |
| 9.5 | Iterator `RangeItUpDown()` with `yield return` | `Enumeratoren/SimpleSortedList.cs` | 9.6/4 |
| 9.6 | Generic math: `IAdditionOperators`, `operator checked +` | `Bruch/b11 IAdditionOperators`, `Statische Mitglieder/Generische Mathematik/*` | 9.1.3, 9.6/3 |
| 9.7 | Interfaces vs abstract classes; variance (`quiz`) | `Typformalparameter in generischen Interfaces/*` | 9.6/2, 9.1.5 |

Static abstract members / CRTP (`Statische Polymorphie/*`): stretch item.

### Level 10 — Kollektionen (11)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 10.1 | `List<T>` operations | **NEW** — no plain List sample in this folder | 11.4 |
| 10.2 | `Dictionary<TKey,TValue>` | `Kollektionen/DictionaryDemo` | 11.6 |
| 10.3 | Lotto with `SortedSet<T>`; `HashSet<T>` | `Mengenlotto`, `HashSetDemo` | 11.8/2, 11.5 |
| 10.4 | Pick the right collection for `Contains` (`quiz` + code) | `ListSetContains` (timing; recast as correctness) | 11.8/1 |
| 10.5 | `Queue<T>`, `Stack<T>`, `LinkedList<T>`; collection expressions | **NEW** | 11.3 |

### Level 11 — Ausnahmebehandlung (13)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 11.1 | `Log2()` throws `ArgumentOutOfRangeException` instead of NaN | `Ausnahmebehandlung/DuaLog/*`, `throw` | 13.8/3 |
| 11.2 | Catch specific exceptions, catch order; is `OverflowException` handled? | `catch`, `Sequenzen` | 13.3, 13.8/2 |
| 11.3 | `finally` | `finally/*` | 13.3 |
| 11.4 | Custom exception type with extra data | `BadFaculArgException/*` | 13.7 |
| 11.5 | `throw` expressions; generic stack constructor validation | `throw-Ausdruck`, `SimpleStackEx` | 13.6, 13.8/4 |
| 11.6 | Return code vs exception (`quiz` + `open`) | `RcVsException` | 13.4 |

### Level 12 — Delegaten und Ereignisse (10)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 12.1 | `Predicate<string>` for `FindAll`; factory `ListLE(k)` | `Delegaten/DemoGate`, `FindAll` | 10.3/2–3 |
| 12.2 | Closures | `Closure` | 10.1 |
| 12.3 | Multicast delegates | `OpChain` | 10.1 |
| 12.4 | Publish and subscribe to an event | `Ereignisse/EventPublisher` | 10.2 |
| 12.5 | Delegates and events (`quiz`) | `Kovarianz`, `Kontravarianz` | 10.3/1, 4 |

### Level 13 — LINQ-to-Objects (18)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 13.1 | `Where` / `Select`, method syntax | `LINQ-to-Objects/Einstieg` | 18.2 |
| 13.2 | Query syntax, `let`, `into` | `Abfragesyntax/{Einstieg,let,into}` | 18.3 |
| 13.3 | Sorting | `Sortieren` | 18.2.7 |
| 13.4 | `GroupBy`; find duplicate files in a folder tree | `GroupBy`, `FindDuplicate` | 18.2.9, 18.6/2 |
| 13.5 | `Join` / `GroupJoin` | `Join`, `GroupJoin`, `Abfragesyntax/Join` | 18.2.10 |
| 13.6 | `SelectMany`, subqueries | `Abfragesyntax/SelectMany per Abfragesyntax`, `Unterabfrage` | 18.4 |
| 13.7 | `OfType`, `Zip`; LINQ quiz | `OfType`, `Zip` | 18.6/1 |

`PLINQ/*` belongs to the multithreading elective.

### Level 14 — C# für Fortgeschrittene (15)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 14.1 | Nullable reference types; fix the `Reverse()` warning with an attribute | `Nullable-Kontext für Referenztypen/*` (7 projects) | 15.1, 15.7/1 |
| 14.2 | `?.`, `??`, null-conditional assignment | `null-bedingte Zuweisung`, `PalindromReverse` | 15.1.5 |
| 14.3 | Patterns: constant, type, relational, logical; recode age into categories | `Musterabgleich/{Typmuster,Relationsmuster,logisches Muster}` | 15.2.1–3, 15.2.8–9, 15.7/2 |
| 14.4 | Property pattern: `PKW.Zugelassen()` | `Musterabgleich/Merkmalsmuster` | 15.2.4, 15.7/3 |
| 14.5 | Positional, tuple, `var` patterns | `Musterabgleich/{Positionsmuster,Tupelmuster/*,var-Muster}` | 15.2.5–7 |
| 14.6 | List patterns | `Musterabgleich/Listenmuster` | 15.2.10 |
| 14.7 | Pattern capstone: toll calculator | `Musterabgleich/TollCalculator` | 15.2 |
| 14.8 | `Span<T>` and implicit span conversions | `Span-Typen/*` (not `Span-Benchmark`) | 15.4 |
| 14.9 | `ref` returns, `params` collections | `ref-Rückgabe`, `Serienparameter mit Kollektionstyp` | 15.3, 15.6 |
| 14.10 | Inline arrays | **NEW** — no sample | 15.5 |

### Level 15 — Dateiverarbeitung (16)
| # | Exercise | Built from | Book |
|---|---|---|---|
| 15.1 | Read / write text with streams | `Ein- und Ausgabe über Datenströme/Einstieg`, `StreamWrtRd`, `Ansi-Text Lesen` | 16.1, 16.3.2–3 |
| 15.2 | Binary read / write | `BinWrtRd` | 16.3.1 |
| 15.3 | `File.ReadAll*` / `WriteAll*` | `Schreiben und Lesen von kleinen Dateien/*` | 16.4 |
| 15.4 | Column means of a `;`-separated file, with warnings on bad tokens | `Mittelwerte` | 16.7/1 |
| 15.5 | `using` statement / declaration; the `AutoFlush` trap | `Freigabe von Ressourcen/*` | 16.2, 16.7/2 |
| 15.6 | Files, folders, `Path` (in a temp dir) | `Dateiverwaltung`, `Ordnerverwaltung`, `Path` | 16.6 |
| 15.7 | JSON serialisation incl. circular references | `JSON-Serialisierung/*` | 16.5 |

*demo* only: `FileShare/*`, `FileSystemWatcher`, `DriveInfo`.

---

## Task 0 status

1. **Folder vs. artifacts**: matches. The folder holds the three task
   documents, `csharp14.zip` and (added after the first pass) `csharp14.pdf`.
2. **Archive stripped** into `samples/`; the zip itself is untouched.
3. **Solutions normalised**: all `.slnx`.
4. **Path hazards flagged**: `INVENTORY.md` §2, with fixture candidates.
5. **Gaps**: `GAPS.md`.
6. **Book checked**: section numbers verified, pages listed, the book's
   exercises catalogued in `INVENTORY.md` §4 and mapped onto levels above.

Next: gate review of this map, then task 1.
