# SeeSharp — Inventory

Task 0 catalogue of `D:\Rider Projects 2\SeeSharp` as of 2026-10-15.

## 1. Folder contents

| File | Type | Size |
|---|---|---:|
| `AGENT_TASKS.md` | task set (Markdown) | 13.9 KB |
| `MODULE-MAP.md` | module map draft (Markdown) | 9.2 KB |
| `SKILL-MODEL.md` | amendment to the task set (Markdown) | 7.4 KB |
| `csharp14.pdf` | the book (see §1.1) | 35.6 MB |
| `csharp14.zip` | sample-code archive, 3564 entries | 22.1 MB |
| `CLAUDE.md` | *added by task 0* — shared context for agent sessions | — |
| `samples/` | *added by task 0* — cleaned extract of the zip | 110 MB |
| `docs/` | *added by task 0* — this file, module map, gaps | — |

Nothing extra was found in the folder beyond the files the module map describes.

### 1.1 The book

*Einführung in das Programmieren mit C# 14*, Baltes-Götz / Frischknecht,
© 2026. 1746 physical pages: 32 front-matter pages (title, licence, preface,
table of contents), then 23 chapters on printed pages 1–1690, then the
bibliography (1691) and index.

- **Licence: CC BY-NC-SA 4.0** (stated on physical page 2). It is share-alike
  *and* non-commercial. Derived summaries must carry the same licence or a
  compatible one, and must keep the authors' names and the copyright line. Any
  other use needs the authors' permission (contact address on page 2). This
  answers the open question in `SKILL-MODEL.md` §6. Whether internal training
  at a company counts as non-commercial use isn't settled by the licence text.
- **Page offset: verified.** Printed page = physical page − 32 on all 1678
  pages that carry a running header. Chapter 1 starts on physical page 33.
- **The table of contents is stale in places.** I compared 784 TOC entries
  with the page where each heading actually appears in the text. 90 differ,
  always by appearing *earlier* than the TOC says:
  - chapter 4, from 4.3.8 to 4.7.3: 1–7 pages early;
  - chapter 17, 17.2.5–17.8: 1–10 pages early;
  - chapter 18, 18.1–18.2.10: 1–3 pages early;
  - chapter 20, 20.9.8–20.16.2: 1–2 pages early.

  So `printedPage` values in manifests must come from the body text, not the TOC.
  `MODULE-MAP.md` lists verified pages for every level.
- The chapter 18 opener notes that the chapter is still at the C# 12 /
  .NET 8 level and will be updated later.
- Chapter 3.6 says the authors' own sample archive is `BspUeb.zip`.
  `csharp14.zip` matches its folder layout.

## 2. Archive cleanup (`csharp14.zip` → `samples/`)

The zip is left untouched as the original. `samples/` is the working copy.

- **Encoding.** Entry names carry no UTF-8 flag; they are CP437/CP850
  (`0x84` = ä, `0x81` = ü). Decoded correctly, the umlauts survive extraction.
  Tools that guess wrong show `Tempor�re Verbindung`.
- **Removed (251 files, 4.7 MB):** 124 under `obj/`, 52 under `bin/`, 51 under
  `.vs/`, 22 `.user`, 2 stray `.dll`. No `.suo`, `.cache`, `.vsidx`, `.pdb` or
  `.exe` survived outside those folders. (The module map's "355 of 3564" counted
  directory entries too.)
- **Kept:** 2469 files, 110.7 MB.
- **Solutions normalised:** all 176 `.sln` migrated with `dotnet sln migrate` to
  `.slnx`, originals removed — 389 `.slnx` total, 0 failures. `.slnx` chosen
  because it is the newer format, 213 of 389 already used it, and Rider reads it.

### Path hazards (belong in grading-runner fixtures)

Of the 2469 kept files, **1869** have a space in the path, **370** an umlaut and
**7** a comma. Representative worst cases:

- `C# für Fortgeschrittene/Musterabgleich/Tupelmuster/Schere, Stein, Papier/`
- `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/using-Interpretation des Compilers/`
- `Entity Framework Core/Nachverfolgung und Identitätsauflösung/streaming vs. buffering/`
- `ADO.NET/Temporäre Verbindung/Temporäre Verbindung.csproj`
- `#` in a folder name: `C# für Fortgeschrittene` (breaks naive URI building)

**Source encoding is mixed.** Of 731 `.cs` files, 421 are UTF-8 with BOM, 236
UTF-8 without, and **74 are Windows-1252** (umlauts in comments show as `�`
when read as UTF-8, e.g. `Klassen und Objekte/Rekursion/Rekursion.cs`).
Anything that copies or renders sample code into a `starter/` must decode with
the right encoding or re-save as UTF-8 first.

## 3. Sample code overview

**391 C# projects + 1 VB project** in 21 topic folders. No project contains
tests; no project references a test framework.

| Property | Values |
|---|---|
| TFM | `net10.0` 162, `net8.0` 158, `net10.0-windows(7.0)` 54, `net8.0-windows(7.0)` 14, `net5.0(-windows)` 3 |
| Output | `Exe` 311, `WinExe` 67, library 13 |
| SDK | `Microsoft.NET.Sdk` 379, `.Web` 11, `.WindowsDesktop` 1 |
| Packages | EF Core SqlServer/Tools (92 projects), `Microsoft.Data.SqlClient` 12, `BenchmarkDotNet` 4, `System.Linq.Async` 3, misc. |
| File types | 740 `.cs`, 112 `.xaml`, 93 `.cshtml`, 616 web assets (`.css/.js/.map`) in ASP.NET wwwroot, 1 SQL Server DB (`ADO.NET/Northwind`, 16 MB `.mdf/.ldf` + 2 MB `.sql`) |

### Worked example, skeleton or solution?

Most samples are **worked examples**: a small, complete, runnable console
program that demonstrates one book section and prints its result. 30 projects are
**solutions to the book's own exercises** (listed per exercise in §4). This
matters for authoring: a `starter/` must never be copied from one of them,
because it would already pass its tests. That is exactly what the validator's
"tests pass against untouched starter" rule exists to catch. None of the
samples are skeletons with `TODO`s. Other special cases:

| Path | Role |
|---|---|
| `ASP.NET Core/Razor/PartPref/Gerüst` + `.../Fertig` | skeleton / solution pair |
| `Klassen und Objekte/Bruch/b0` … `b11` | ordered progression — each step is the solution to the previous one's next exercise |
| `Entwicklungswerkzeuge/Verweise und Abhängigkeiten/PRDemo/{KonsPro,LibPro}` | library/consumer split |
| `Klassen und Objekte/Bruch/FractionLib` | library only — reusable as the system under test |
| `*/…Benchmark*`, `Kollektionen/ImmutableList-Performance`, `Span-Benchmark` | BenchmarkDotNet / timing demos — not gradable |
| `Elementare Sprachelemente/VB` | VB.NET, demonstrates `Option Explicit Off` as a contrast — not gradable |

Because the samples are worked examples that print rather than return,
almost every exercise needs its logic lifted into a testable method before a
`tests/` project can assert on it. This is authoring work, not conversion.

## 4. Existing exercises and questions

Chapters 1–18 each end with an exercise section; chapters 19–23 have none.
Below, every exercise is summarised (not quoted). Pages are verified printed
pages; add 32 for the physical page.

Type: **TF** = true/false statements (→ `multipleChoice`), **Q** = explanation
question (→ `open`), **Fix** = find/fix the bug in given code, **Code** =
write a program, **GUI** = WPF / Visual Studio task, **Bench** = timing
measurement. *Sample* = solution present in `samples/`; — = none.

| Book | # | Type | Task | Sample |
|---|---|---|---|---|
| 1.8 (p. 17) | 1 | TF | 5 statements on encapsulation, methods, properties, `Main` | — |
| | 2 | Q | Why OOP raises productivity | — |
| 2.8 (p. 59) | 1 | TF | 5 statements on .NET platforms, BCL, language interop, WPF portability | — |
| | 2–3 | Q | Assemblies vs namespaces; meaning of IL, BCL, JIT | — |
| 3.7 (p. 158) | 1 | GUI | Install Visual Studio / VS Code | — |
| | 2 | Fix | Fix all errors in a broken Hallo program | — |
| | 3 | TF | 4 statements on SDK selection, VS licensing, VS Code, F1 help | — |
| | 4 | Q | Which build-output files must ship to customers | — |
| 4.1.7 (p. 175) | 1–3 | TF | Valid `Main` signatures, valid method heads, valid identifiers | — |
| 4.2.4 (p. 179) | 1 | Fix | Explain `"…" + 3.3 + 2` string concatenation; fix it with brackets | — |
| | 2 | Code | Right- and left-aligned output with format alignment | `Elementare Sprachelemente/FormAus` |
| 4.3.11 (p. 210) | 1 | Q | Why a variable from an inner block is unknown (scope) | — |
| | 2 | Fix | Fix a program with `main`, comma literals, wrong quotes | — |
| | 3 | Code | Print a string literal containing quotes | — |
| | 4 | Q | Why `char` → `int` is implicit; Unicode escape for 'c' | — |
| | 5 | GUI | Switch the DM/Euro converter to `decimal` | `Elementare Sprachelemente/DmToEuroDec` (WPF) |
| 4.5.11 (p. 241) | 1–3 | Q | Value and type of mixed arithmetic; `++` and `?:`; boolean operators / De Morgan | — |
| | 4 | Code | Compute e^x for user input via `Math` | `Elementare Sprachelemente/Exp` |
| | 5 | Q | Evaluation order of operands and operators | — |
| | 6 | GUI | DM+Pfennig → Euro+Cent converter with rounding, focus, alignment | `Elementare Sprachelemente/DmToEuroSep` (WPF) |
| 4.7.4 (p. 281) | 1 | Fix | Dangling `else` in a lottery payout rule | — |
| | 2 | Fix | `if (b = false)` assignment-in-condition bug | — |
| | 3 | Code | Prime test without `break`/`continue` | — |
| | 4 | Q | `while (…);` empty-statement loop | — |
| | 5 | Code | Largest i with 1.0 + 2^−i ≠ 1.0 in `double` | `Elementare Sprachelemente/FloP` |
| | 6 | Code+Bench | GCD by subtraction vs modulo, timed with `Stopwatch` | `GGT.Diff`, `GGT.Mod`; `Entwicklungswerkzeuge/…/GgtDiffBenchmark` |
| 5.15 (p. 424) | 1 | TF | 6 statements on fields, overloading, constructors, encapsulation, parameters, signatures | — |
| | 2 | Q | `readonly` field vs `init`-only property | — |
| | 3 | Fix | Property getter that recurses into itself (stack overflow) | — |
| | 4 | Code | R² vector class: length, normalise, add, dot product, angle, rotation | `Klassen und Objekte/R2Vek` |
| | 5 | Code | Recursive factorial; optional recursive Fibonacci with timing | `Klassen und Objekte/Rekursion` (Fibonacci) |
| | 6 | GUI | WPF fraction app: local `Bruch` → instance field | `Einleitungsbeispiel Bruchrechnen/SimplifyFraction (mit Bruch-Instanzvariable)` |
| | 7 | Q | Find 12 OOP terms in a `Bruch` listing | `Klassen und Objekte/Bruch/Begriffe lokalisieren.pdf` (fillable form) |
| 6.10 (p. 537) | 1 | Q | Why two boxed `1`s are not `==` | — |
| | 2 | Code | Lotto 6 of 49 with Fisher–Yates and selection sort | `Weitere .NETte Typen/Arrays/Lotto` |
| | 3 | Code | Sieve of Eratosthenes (console or WPF) | `Arrays/Eratosthenes` |
| | 4 | Code | `float` matrix class with transpose | `Arrays/FloatMatrix` |
| | 5 | Code | "Lucky number" from command-line name via seeded `Random` | `Zeichenfolgen/PerZuf` |
| | 6 | Code | `StringUtil.WrapLine()` with width and hyphen options | `Zeichenfolgen/StringUtil` |
| 7.16 (p. 586) | 1 | Fix | Derived class lacks a base-constructor call | — |
| | 2 | Fix | Derived class can't reach private base fields | — |
| | 3 | Q | Overloading vs hiding vs overriding; which fails for static methods | — |
| | 4 | Fix | Record hierarchy loses positional values | `Vererbung und Polymorphie/Record-Figuren` |
| | 5 | Code | Override `ToString()` in `Bruch` | `Klassen und Objekte/Bruch/b9 ToString-Überschreibung` |
| 8.8 (p. 607) | 1 | TF | 4 statements on type parameters, generic methods, variance, inheritance | — |
| | 2 | Bench | `ArrayList` vs `List<int>` insert timing | `Typgenerisches Programmieren/Listenwerte` |
| | 3 | Code | `Max<T>()` with a generic `params` parameter | — |
| 9.6 (p. 655) | 1 | Code | `Bruch` implements `IComparable<T>` and `ICloneable` | `Bruch/b10 IComparable, IClonable` |
| | 2 | Q | Interfaces vs abstract classes | — |
| | 3 | Code | `operator checked +` on `Bruch` throwing on overflow | `Bruch/b11 IAdditionOperators` |
| | 4 | Code | Iterator `RangeItUpDown()` that picks its direction | `Interfaces/Enumeratoren` |
| 10.3 (p. 688) | 1 | TF | 4 statements on delegates, capture, variance | — |
| | 2–3 | Code | `Predicate<string>` lambda for `FindAll`; factory method `ListLE(k)` | `Delegaten und Ereignisse/Delegaten/FindAll` |
| | 4 | TF | 4 statements on events | — |
| 11.8 (p. 723) | 1 | TF | 5 statements on List/LinkedList/HashSet/Dictionary/SortedDictionary | — |
| | 2 | Code | Lotto again, with a collection class | `Kollektionen/Mengenlotto` |
| 12.14 (p. 884) | 1 | TF | 5 statements on WinUI, WPF events, XAML, layout | — |
| | 2 | GUI | Bidirectional Euro/DM converter | — |
| 13.8 (p. 920) | 1 | Q | Draw a syntax diagram for `try` | — |
| | 2 | Q | Is an uncaught `OverflowException` in the sequence demo unhandled? | — |
| | 3 | Code | `Log2()` throwing `ArgumentOutOfRangeException` instead of NaN | `Ausnahmebehandlung/DuaLog/ArgumentOutOfRangeException` |
| | 4 | Code | Generic stack whose constructor rejects non-positive sizes | `Ausnahmebehandlung/SimpleStackEx` |
| 14.9 (p. 947) | 1 | Code | Read custom attributes of a class via reflection | `Attribute/NonsenseAttribute` |
| 15.7 (p. 1012) | 1 | Fix | Remove a nullable warning on `Reverse()` with an attribute | `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/PalindromReverse` |
| | 2 | Code | Recode age into 5 categories by pattern matching | — |
| | 3 | Code | `PKW.Zugelassen()`: ≥ 3 passengers or electric | — (`Musterabgleich/Merkmalsmuster` is a related example) |
| 16.7 (p. 1060) | 1 | Code | Column means of a `;`-separated file, warning on bad tokens | `Ein- und Ausgabe über Datenströme/Mittelwerte` |
| | 2 | Q | Simplify and speed up a `StreamWriter` loop with `AutoFlush` | — |
| 17.8 (p. 1208) | 1 | TF | 5 statements on WPF threads, cancellation, async/await, TAP | — |
| | 2 | Code | Countdown with `CountdownEvent` | — |
| | 3 | GUI | Cancellable folder-size calculation in WPF | — |
| 18.6 (p. 1255) | 1 | TF | 5 statements on query syntax, SelectMany, PLINQ, `Zip` | — |
| | 2 | Code | Find duplicate files in a folder tree with LINQ | `LINQ-to-Objects/FindDuplicate` |

Totals: 79 numbered items, counting grouped rows separately. 4.7.4/6 counts
as Code.

| Type | Items | With sample | Gradable by SeeSharp |
|---|---:|---:|---|
| TF | 14 | 0 | yes — `multipleChoice` |
| Q | 19 | 1 | no — `open` (supervisor) |
| Fix | 10 | 2 | yes, as a coding exercise |
| Code | 29 | 23 | yes, after lifting logic out of `Console` I/O |
| GUI | 6 | 3 | no — `review` |
| Bench | 1 | 1 | no — timing isn't assertable |

Without a sample solution:
- **Fix** (8): 3.7/2, 4.2.4/1, 4.3.11/2, 4.7.4/1, 4.7.4/2, 5.15/3, 7.16/1, 7.16/2.
- **Code** (6): 4.3.11/3, 4.7.4/3, 8.8/3, 15.7/2, 15.7/3, 17.8/2.

## 5. Project catalogue

Per project: path under `samples/`, TFM, kind, number of `.cs` files (excluding
designer files and EF migrations) and lines of C#.

Kinds: `console`, `library`, `WPF`, `WinForms`, `web`; `+EF` = EF Core,
`+SQL` = SqlClient (both need a live SQL Server).


| Topic folder | Projects | Kinds |
|---|---:|---|
| ADO.NET | 12 | 11 console+SQL, 1 WPF+SQL |
| ASP.NET Core | 11 | 8 web, 3 web+EF |
| Attribute | 16 | 13 console, 3 WPF |
| Ausnahmebehandlung | 15 | 14 console, 1 WPF |
| C# für Fortgeschrittene | 23 | 23 console |
| Delegaten und Ereignisse | 8 | 7 console, 1 WPF |
| Ein- und Ausgabe über Datenströme | 22 | 22 console |
| Einleitungsbeispiel Bruchrechnen | 5 | 3 console, 2 WPF |
| Elementare Sprachelemente | 19 | 15 console, 2 WPF, 2 WinForms |
| Entity Framework Core | 46 | 46 console+EF |
| Entwicklungswerkzeuge | 7 | 5 console, 1 WPF, 1 library |
| Interfaces | 17 | 17 console |
| Klassen und Objekte | 22 | 21 console, 1 library |
| Kollektionen | 5 | 5 console |
| LINQ-to-Objects | 19 | 19 console |
| Multithreading | 53 | 41 console, 12 WPF |
| Netzwerk | 12 | 10 console, 2 WPF |
| Typgenerisches Programmieren | 5 | 5 console |
| Vererbung und Polymorphie | 16 | 16 console |
| Weitere .NETte Typen | 16 | 16 console |
| WPF | 42 | 41 WPF, 1 console |

### ADO.NET

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `ADO.NET/ConnectionStringBuilder` | ConnectionStringBuilder | net8.0 | console+SQL | 1 | 43 |
| `ADO.NET/DataAdapter` | DataAdapter | net8.0 | console+SQL | 1 | 87 |
| `ADO.NET/DataReader` | DbDataReader | net8.0 | console+SQL | 1 | 23 |
| `ADO.NET/DataSetTableRowColumn` | DataSetTableRowColumn | net8.0 | console+SQL | 1 | 294 |
| `ADO.NET/DbProviderFactory` | DbProviderFactoryDemo | net8.0 | console+SQL | 1 | 47 |
| `ADO.NET/Einstieg` | AdoNetEinstieg | net8.0 | console+SQL | 1 | 43 |
| `ADO.NET/LINQ-to-DataSet` | LINQ-to-DataSet | net8.0 | console+SQL | 1 | 76 |
| `ADO.NET/Parametrisierte Abfragen` | Parametrisierte Abfragen | net8.0 | console+SQL | 1 | 32 |
| `ADO.NET/Temporäre Verbindung` | Temporäre Verbindung | net8.0 | console+SQL | 1 | 110 |
| `ADO.NET/Transaction` | Transaction | net8.0 | console+SQL | 1 | 83 |
| `ADO.NET/TransactionScope` | TransactionScope | net8.0 | console+SQL | 1 | 66 |
| `ADO.NET/Typisiertes DataSet` | Typisiertes DataSet | net8.0-windows | WPF+SQL | 3 | 75 |

### ASP.NET Core

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `ASP.NET Core/Razor/AllowEmptyStrings` | AllowEmptyStrings | net8.0 | web | 4 | 112 |
| `ASP.NET Core/Razor/ArrayBindSimple` | ArrayBindSimple | net8.0 | web | 5 | 109 |
| `ASP.NET Core/Razor/ArrayBindSimpleExplicitIndex` | ArrayBindSimpleExplicitIndex | net8.0 | web | 5 | 106 |
| `ASP.NET Core/Razor/DictionaryBindSimple` | DictionaryBindSimple | net8.0 | web | 5 | 108 |
| `ASP.NET Core/Razor/Fehlende Werte` | Fehlende Werte | net8.0 | web | 4 | 91 |
| `ASP.NET Core/Razor/ListBindComplex` | ListBindComplex | net8.0 | web | 7 | 144 |
| `ASP.NET Core/Razor/ListBindSimple` | ListBindSimple | net8.0 | web | 5 | 107 |
| `ASP.NET Core/Razor/Overposting` | Overposting | net8.0 | web | 6 | 125 |
| `ASP.NET Core/Razor/PartPref/Fertig` | PartPref | net8.0 | web+EF | 10 | 291 |
| `ASP.NET Core/Razor/PartPref/Gerüst` | PartPref | net8.0 | web+EF | 7 | 236 |
| `ASP.NET Core/Razor/RazorPagesMovie` | RazorPagesMovie | net8.0 | web+EF | 12 | 523 |

### Attribute

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Attribute/Aufruferinformationsattribute` | Aufruferinformationsattribute | net10.0 | console | 1 | 16 |
| `Attribute/ConditionalAttribute` | ConditionalAttribute | net10.0 | console | 1 | 20 |
| `Attribute/DebuggerStepThrough` | DebuggerStepThrough | net10.0 | console | 1 | 20 |
| `Attribute/Flags` | Flags | net10.0-windows | WPF | 2 | 45 |
| `Attribute/Generische Attribute/typeof` | typeof | net10.0 | console | 1 | 44 |
| `Attribute/Generische Attribute/Typformalparameter` | Typformalparameter | net10.0 | console | 1 | 33 |
| `Attribute/GetCustomAttribute` | GetCustomAttribute | net10.0 | console | 1 | 31 |
| `Attribute/GetCustomAttributes` | GetCustomAttributes | net10.0 | console | 1 | 15 |
| `Attribute/IsDefined` | IsDefined | net10.0 | console | 1 | 25 |
| `Attribute/Lambda-Attribute` | Lambda-Attribute | net10.0 | console | 1 | 45 |
| `Attribute/nameof` | NameofDemo | net10.0 | console | 1 | 23 |
| `Attribute/NonsenseAttribute` | NonsenseAttribute | net10.0 | console | 1 | 20 |
| `Attribute/ObsoleteClass` | ObsoleteClass | net10.0 | console | 1 | 23 |
| `Attribute/ObsoleteMethod` | ObsoleteMethod | net10.0 | console | 1 | 16 |
| `Attribute/WpfApp` | WpfApp | net10.0-windows | WPF | 3 | 54 |
| `Attribute/WpfOhneSTAThreadAttribute` | WpfOhneSTAThreadAttribute | net10.0-windows | WPF | 1 | 15 |

### Ausnahmebehandlung

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Ausnahmebehandlung/BadFaculArgException/Eigene Ausnahme-Eigenschaften` | Eigene Ausnahme-Eigenschaften | net10.0 | console | 2 | 74 |
| `Ausnahmebehandlung/BadFaculArgException/Zusatzinformationen in der Data-Kollektion` | BadFaculArgException | net10.0 | console | 2 | 73 |
| `Ausnahmebehandlung/catch` | catch | net10.0 | console | 1 | 48 |
| `Ausnahmebehandlung/DuaLog/ArgumentOutOfRangeException` | ArgumentOutOfRangeException | net10.0 | console | 1 | 17 |
| `Ausnahmebehandlung/DuaLog/NaN` | NaN | net10.0 | console | 1 | 7 |
| `Ausnahmebehandlung/finally/Ausnahmenersetzung` | Ausnahmenersetzung | net10.0 | console | 1 | 16 |
| `Ausnahmebehandlung/finally/Explizit` | finally | net10.0 | console | 1 | 37 |
| `Ausnahmebehandlung/finally/Implizit` | finallyImplizit | net10.0 | console | 1 | 22 |
| `Ausnahmebehandlung/HandleUnhandledException` | HandleUnhandledException | net10.0-windows7.0 | WPF | 3 | 53 |
| `Ausnahmebehandlung/RcVsException` | RcVsException | net10.0 | console | 1 | 69 |
| `Ausnahmebehandlung/Sequenzen` | Sequenzen | net10.0 | console | 1 | 28 |
| `Ausnahmebehandlung/SimpleStackEx` | SimpleStack | net10.0 | console | 2 | 44 |
| `Ausnahmebehandlung/throw` | throw | net10.0 | console | 1 | 31 |
| `Ausnahmebehandlung/throw-Ausdruck` | throw-Ausdruck | net10.0 | console | 1 | 46 |
| `Ausnahmebehandlung/Unbehandelt` | Unbehandelt | net10.0-windows | console | 1 | 25 |

### C# für Fortgeschrittene

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `C# für Fortgeschrittene/Musterabgleich/Listenmuster` | Listenmuster | net10.0 | console | 1 | 49 |
| `C# für Fortgeschrittene/Musterabgleich/logisches Muster` | logisches Muster | net10.0 | console | 1 | 22 |
| `C# für Fortgeschrittene/Musterabgleich/Merkmalsmuster` | Merkmalsmuster | net10.0 | console | 1 | 68 |
| `C# für Fortgeschrittene/Musterabgleich/Positionsmuster` | Positionsmuster | net10.0 | console | 1 | 25 |
| `C# für Fortgeschrittene/Musterabgleich/Relationsmuster` | Relationsmuster | net10.0 | console | 1 | 13 |
| `C# für Fortgeschrittene/Musterabgleich/TollCalculator` | TollCalculator | net10.0 | console | 1 | 44 |
| `C# für Fortgeschrittene/Musterabgleich/Tupelmuster/Langstreckenlauf` | Langstreckenlauf | net10.0 | console | 1 | 18 |
| `C# für Fortgeschrittene/Musterabgleich/Tupelmuster/Schere, Stein, Papier` | SchereSteinPapier | net10.0 | console | 1 | 17 |
| `C# für Fortgeschrittene/Musterabgleich/Typmuster` | Typmuster | net10.0 | console | 1 | 26 |
| `C# für Fortgeschrittene/Musterabgleich/var-Muster` | var-Muster | net10.0 | console | 1 | 11 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/AllowNull` | AllowNull | net10.0 | console | 1 | 13 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/MemberNotNull` | MemberNotNull | net10.0 | console | 1 | 19 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/MemberNotNullWhen` | MemberNotNullWhen | net10.0 | console | 1 | 29 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/NotNullIfNotNull` | NotNullIfNotNull | net10.0 | console | 1 | 28 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/NotNullWhen` | NotNullWhen | net10.0 | console | 1 | 13 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/null-bedingte Zuweisung` | NullConditionalAssignment | net10.0 | console | 1 | 44 |
| `C# für Fortgeschrittene/Nullable-Kontext für Referenztypen/PalindromReverse` | PalindromReverse | net10.0 | console | 1 | 18 |
| `C# für Fortgeschrittene/ref-Rückgabe` | ref-Rückgabe | net10.0 | console | 2 | 42 |
| `C# für Fortgeschrittene/Serienparameter mit Kollektionstyp` | HeapStackVarargs | net10.0 | console | 1 | 28 |
| `C# für Fortgeschrittene/Span-Typen/Integrierte implizite Span-Konvertierungen` | IntegrierteSpanKonvertierungen | net10.0 | console | 1 | 36 |
| `C# für Fortgeschrittene/Span-Typen/Span-Benchmark` | SpanBenchmark | net10.0 | console | 1 | 127 |
| `C# für Fortgeschrittene/Span-Typen/Span-Erweiterungsmitglieder` | SpanErweiterungsmitglieder | net10.0 | console | 1 | 39 |
| `C# für Fortgeschrittene/Span-Typen/Verkettung von impliziten Konvertierungen` | ImpliziteKonvertierungenVerketten | net10.0 | console | 1 | 28 |

### Delegaten und Ereignisse

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Delegaten und Ereignisse/Delegaten/Closure` | Closure | net10.0 | console | 1 | 36 |
| `Delegaten und Ereignisse/Delegaten/DemoGate` | DemoGate | net10.0 | console | 1 | 62 |
| `Delegaten und Ereignisse/Delegaten/FindAll` | FindAll | net10.0 | console | 1 | 19 |
| `Delegaten und Ereignisse/Delegaten/Kontravarianz` | Kontravarianz | net10.0 | console | 3 | 45 |
| `Delegaten und Ereignisse/Delegaten/Kovarianz` | Kovarianz | net10.0 | console | 3 | 35 |
| `Delegaten und Ereignisse/Delegaten/OpChain` | OpChain | net10.0 | console | 1 | 22 |
| `Delegaten und Ereignisse/Ereignisse/ApplicationExit` | ApplicationExit | net10.0-windows | WPF | 3 | 48 |
| `Delegaten und Ereignisse/Ereignisse/EventPublisher` | EventPublisher | net10.0 | console | 2 | 53 |

### Ein- und Ausgabe über Datenströme

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Ein- und Ausgabe über Datenströme/Ansi-Text Lesen` | AnsiTextLesen | net8.0 | console | 1 | 22 |
| `Ein- und Ausgabe über Datenströme/BinWrtRd` | BinWrtRd | net8.0 | console | 1 | 20 |
| `Ein- und Ausgabe über Datenströme/Dateiverwaltung` | Dateiverwaltung | net8.0 | console | 1 | 42 |
| `Ein- und Ausgabe über Datenströme/DriveInfo` | DriveInfo | net8.0 | console | 1 | 9 |
| `Ein- und Ausgabe über Datenströme/Einstieg` | FSDemo | net8.0 | console | 1 | 17 |
| `Ein- und Ausgabe über Datenströme/FileShare/FileShareMultipleWriters` | FileShareMultipleWriters | net8.0 | console | 3 | 70 |
| `Ein- und Ausgabe über Datenströme/FileShare/FileShareMultipleWritersSync` | FileShareMultipleWritersSync | net8.0 | console | 3 | 85 |
| `Ein- und Ausgabe über Datenströme/FileShare/FileShareOneWriter` | FileShareOneWriter | net8.0 | console | 3 | 48 |
| `Ein- und Ausgabe über Datenströme/FileSystemWatcher` | FileSystemWatcher | net8.0 | console | 1 | 39 |
| `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/Geschachtelte try-Anweisungen` | Geschachtelte try-Anweisungen | net8.0 | console | 1 | 27 |
| `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/Geschachtelte Using-Anweisungen` | NestedUsingStatements | net8.0 | console | 1 | 34 |
| `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/Kombination von try und using` | TryUsingCombination | net8.0 | console | 1 | 21 |
| `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/using-Deklaration` | using-Deklaration | net8.0 | console | 1 | 18 |
| `Ein- und Ausgabe über Datenströme/Freigabe von Ressourcen/using-Interpretation des Compilers` | UsingInterpretation | net8.0 | console | 1 | 23 |
| `Ein- und Ausgabe über Datenströme/JSON-Serialisierung/Standard` | Json-Serialisierung | net8.0 | console | 2 | 82 |
| `Ein- und Ausgabe über Datenströme/JSON-Serialisierung/Zirkuläre Referenz` | ZirkelSerialisierungJson | net8.0 | console | 1 | 46 |
| `Ein- und Ausgabe über Datenströme/Mittelwerte` | Mittelwerte | net8.0 | console | 1 | 48 |
| `Ein- und Ausgabe über Datenströme/Ordnerverwaltung` | Ordnerverwaltung | net8.0 | console | 1 | 38 |
| `Ein- und Ausgabe über Datenströme/Path` | Path | net8.0 | console | 1 | 12 |
| `Ein- und Ausgabe über Datenströme/Schreiben und Lesen von kleinen Dateien/WriteReadAllBytes` | WriteReadAllBytes | net8.0 | console | 1 | 15 |
| `Ein- und Ausgabe über Datenströme/Schreiben und Lesen von kleinen Dateien/WriteReadAllLines` | WriteReadAllLines | net8.0 | console | 1 | 14 |
| `Ein- und Ausgabe über Datenströme/StreamWrtRd` | StreamWrtRd | net8.0 | console | 1 | 17 |

### Einleitungsbeispiel Bruchrechnen

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Einleitungsbeispiel Bruchrechnen/CLI` | Bruchaddition | net10.0 | console | 2 | 81 |
| `Einleitungsbeispiel Bruchrechnen/SimplifyFraction` | SimplifyFraction | net10.0-windows | WPF | 3 | 81 |
| `Einleitungsbeispiel Bruchrechnen/SimplifyFraction (mit Bruch-Instanzvariable)` | SimplifyFraction | net10.0-windows | WPF | 3 | 83 |
| `Einleitungsbeispiel Bruchrechnen/VS` | Bruchaddition | net10.0 | console | 2 | 81 |
| `Einleitungsbeispiel Bruchrechnen/VS Top-Level Statements` | Bruchaddition | net10.0 | console | 2 | 75 |

### Elementare Sprachelemente

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Elementare Sprachelemente/Bits/CharBits` | CharBits | net10.0 | console | 1 | 6 |
| `Elementare Sprachelemente/Bits/DecimalBits` | DecimalBits | net10.0 | console | 1 | 82 |
| `Elementare Sprachelemente/Bits/DoubleBits` | DoubleBits | net10.0 | console | 1 | 27 |
| `Elementare Sprachelemente/Bits/FloatBits` | FloatBits | net10.0 | console | 1 | 26 |
| `Elementare Sprachelemente/DmToEuroDec` | DmToEuroDec | net10.0-windows | WPF | 3 | 64 |
| `Elementare Sprachelemente/DmToEuroSep` | DmToEuroSep | net10.0-windows | WPF | 3 | 70 |
| `Elementare Sprachelemente/DoubleDecimal` | DoubleDecimal | net10.0 | console | 1 | 25 |
| `Elementare Sprachelemente/Exp` | Exp | net10.0 | console | 1 | 5 |
| `Elementare Sprachelemente/FloP` | FloP | net10.0 | console | 1 | 14 |
| `Elementare Sprachelemente/FormAus` | FormAus | net10.0 | console | 1 | 3 |
| `Elementare Sprachelemente/GGT.Diff` | GGT.Diff | net10.0 | console | 1 | 26 |
| `Elementare Sprachelemente/GGT.Mod` | GGT.Mod | net10.0 | console | 1 | 22 |
| `Elementare Sprachelemente/InputBox` | InputBox | net10.0-windows | WinForms | 1 | 5 |
| `Elementare Sprachelemente/MessageBox` | MessageBox | net10.0-windows | WinForms | 1 | 1 |
| `Elementare Sprachelemente/OverflowCheckBenchmark` | OverflowCheckBenchmark | net10.0 | console | 1 | 15 |
| `Elementare Sprachelemente/PerST` | PerST | net10.0 | console | 1 | 21 |
| `Elementare Sprachelemente/Primitiv` | Primitiv | net10.0 | console | 1 | 33 |
| `Elementare Sprachelemente/PrimitivOBC` | PrimitivOBC | net10.0 | console | 1 | 44 |
| `Elementare Sprachelemente/Program` | Program | net10.0 | console | 1 | 6 |

### Entity Framework Core

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Entity Framework Core/ALTER DATABASE` | ALTER DATABASE | net8.0 | console+EF | 2 | 90 |
| `Entity Framework Core/Blogging-Beispiel` | Blogging | net8.0 | console+EF | 2 | 82 |
| `Entity Framework Core/Datenbankaktualisierung/Entitäten erstellen` | Entitäten erstellen | net8.0 | console+EF | 2 | 82 |
| `Entity Framework Core/Datenbankaktualisierung/Entitäten löschen/Obligatorische Master-Details - Beziehung` | Entitäten löschen | net8.0 | console+EF | 2 | 84 |
| `Entity Framework Core/Datenbankaktualisierung/Entitäten löschen/Optionale Master-Details - Beziehung` | Entitäten löschen bei optionaler Beziehung | net8.0 | console+EF | 2 | 80 |
| `Entity Framework Core/Datenbankaktualisierung/Entitäten ändern` | Entitäten ändern | net8.0 | console+EF | 2 | 74 |
| `Entity Framework Core/EntityState` | EntityState | net8.0 | console+EF | 2 | 96 |
| `Entity Framework Core/EntityTypeConfiguration` | EntityTypeConfiguration | net8.0 | console+EF | 3 | 95 |
| `Entity Framework Core/Ereignisse und Interzeptoren/SaveChangesInterceptor` | SaveChangesInterceptor | net8.0 | console+EF | 3 | 101 |
| `Entity Framework Core/Ereignisse und Interzeptoren/SavingChanges Event` | SavingChanges Event | net8.0 | console+EF | 2 | 85 |
| `Entity Framework Core/Erster Eindruck` | Erster Eindruck | net8.0 | console+EF | 2 | 54 |
| `Entity Framework Core/Explizite Transaktion/IDbContextTransaction` | Explizite Transaktion | net8.0 | console+EF | 2 | 94 |
| `Entity Framework Core/Explizite Transaktion/Voreingestelltes Transaktionsverhalten` | Implizite Transaktion | net8.0 | console+EF | 2 | 81 |
| `Entity Framework Core/Generierte Werte` | Generierte Werte | net8.0 | console+EF | 2 | 64 |
| `Entity Framework Core/Komplexe Typen/Kein Ärger mit Schattenidentitäten` | Komplexe Typen | net8.0 | console+EF | 2 | 70 |
| `Entity Framework Core/Komplexe Typen/Wertobjekte per Record` | Wertobjekte per Record | net8.0 | console+EF | 2 | 71 |
| `Entity Framework Core/Laden assoziierter Entitäten/AsSplitQuery` | AsSplitQuery | net8.0 | console+EF | 2 | 142 |
| `Entity Framework Core/Laden assoziierter Entitäten/AutoInclude` | AutoInclude | net8.0 | console+EF | 2 | 67 |
| `Entity Framework Core/Laden assoziierter Entitäten/Eager Loading` | Eager Loading | net8.0 | console+EF | 2 | 101 |
| `Entity Framework Core/Laden assoziierter Entitäten/Explicit Loading` | Explizites Laden | net8.0 | console+EF | 2 | 73 |
| `Entity Framework Core/Laden assoziierter Entitäten/Lazy Loading` | Lazy Loading | net8.0 | console+EF | 2 | 71 |
| `Entity Framework Core/Laden assoziierter Entitäten/Multiples Include` | Multiples Include | net8.0 | console+EF | 2 | 94 |
| `Entity Framework Core/LINQ-to-Entities/AsEnumerable` | AsEnumerable | net8.0 | console+EF | 2 | 69 |
| `Entity Framework Core/LINQ-to-Entities/Ausdrucksbäume` | ExpressionTree | net8.0 | console+EF | 2 | 97 |
| `Entity Framework Core/LINQ-to-Entities/Einfache Beispiele` | Simple Queries | net8.0 | console+EF | 2 | 92 |
| `Entity Framework Core/Massenaktualisierung` | ExecuteUpdateDelete | net8.0 | console+EF | 2 | 68 |
| `Entity Framework Core/Master-Details - Beziehung` | Master-Details - Beziehung | net8.0 | console+EF | 2 | 86 |
| `Entity Framework Core/Migration per Programm` | Laufzeit-Migration | net8.0 | console+EF | 2 | 64 |
| `Entity Framework Core/Nachverfolgung und Identitätsauflösung/streaming vs. buffering` | Tracking | net8.0 | console+EF | 2 | 83 |
| `Entity Framework Core/Nachverfolgung und Identitätsauflösung/Tracking` | Tracking | net8.0 | console+EF | 2 | 85 |
| `Entity Framework Core/Nicht-eigenständige Entitäten/Mitgliedskollektionen/Entity Posts` | Entity Posts | net8.0 | console+EF | 2 | 60 |
| `Entity Framework Core/Nicht-eigenständige Entitäten/Mitgliedskollektionen/Owned Posts` | Owned Posts | net8.0 | console+EF | 2 | 62 |
| `Entity Framework Core/Nicht-eigenständige Entitäten/Mitgliedsobjekte` | Owned Entity | net8.0 | console+EF | 2 | 48 |
| `Entity Framework Core/Paginierung ohne Skip` | Paginierung ohne Skip | net8.0 | console+EF | 2 | 77 |
| `Entity Framework Core/Reverse Engineering/Scaffold-DbContext` | Reverse Engineering | net8.0 | console+EF | 4 | 186 |
| `Entity Framework Core/Reverse Engineering/Update/Migration` | Migration nach Reverse Engineering | net8.0 | console+EF | 4 | 188 |
| `Entity Framework Core/Reverse Engineering/Update/Reserse Engineering wiederholen` | Reverse Engineering wiederholen | net8.0 | console+EF | 4 | 188 |
| `Entity Framework Core/Schatteneigenschaften` | Schatteneigenschaften | net8.0 | console+EF | 2 | 63 |
| `Entity Framework Core/Testfälle` | Testfälle | net8.0 | console+EF | 2 | 65 |
| `Entity Framework Core/TPH` | TPH | net8.0 | console+EF | 2 | 49 |
| `Entity Framework Core/Verbindungs-Rersilienz` | Verbindungs-Rersilienz | net8.0 | console+EF | 2 | 83 |
| `Entity Framework Core/Verbindungszeichenfolge aus appsettings.json` | Verbindungszeichenfolge aus appsettings.json | net8.0 | console+EF | 2 | 77 |
| `Entity Framework Core/Voreiliges Dispose` | Voreiliges Dispose | net8.0 | console+EF | 2 | 37 |
| `Entity Framework Core/Wertkonverter` | Wertkonverter | net8.0 | console+EF | 2 | 82 |
| `Entity Framework Core/Zugriffskonflikte/ConcurrencyCheck` | ConcurrencyCheck | net8.0 | console+EF | 2 | 82 |
| `Entity Framework Core/Zugriffskonflikte/RowVersion` | RowVersion | net8.0 | console+EF | 2 | 83 |

### Entwicklungswerkzeuge

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Entwicklungswerkzeuge/DmToEuro` | DmToEuro | net10.0-windows | WPF | 3 | 54 |
| `Entwicklungswerkzeuge/Hallo/CLI` | Hallo | net10.0 | console | 1 | 7 |
| `Entwicklungswerkzeuge/Hallo/HalloVS` | HalloVS | net10.0 | console | 1 | 12 |
| `Entwicklungswerkzeuge/Hallo/HalloVSC` | HalloVSC | net10.0 | console | 1 | 2 |
| `Entwicklungswerkzeuge/Verweise und Abhängigkeiten/GgtDiffBenchmark` | GgtDiffBenchmark | net10.0 | console | 1 | 35 |
| `Entwicklungswerkzeuge/Verweise und Abhängigkeiten/PRDemo/KonsPro` | KonsPro | net10.0 | console | 1 | 1 |
| `Entwicklungswerkzeuge/Verweise und Abhängigkeiten/PRDemo/LibPro` | LibPro | net10.0 | library | 1 | 10 |

### Interfaces

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Interfaces/Enumeratoren` | Enumeratoren | net10.0 | console | 2 | 103 |
| `Interfaces/Explizite Implementation` | Explizite Implementation | net10.0 | console | 1 | 25 |
| `Interfaces/Fimplement` | Fimplement | net10.0 | console | 3 | 70 |
| `Interfaces/Generische Schnittstellen` | Generische Schnittstellen | net10.0 | console | 2 | 38 |
| `Interfaces/Interface-Datentypen` | Interface-Datentypen | net10.0 | console | 1 | 20 |
| `Interfaces/Konkrete Implementation vs. Erweiterung` | Konkrete Implementation vs. Erweiterung | net10.0 | console | 1 | 36 |
| `Interfaces/No Promotion` | No Promotion | net10.0 | console | 1 | 32 |
| `Interfaces/Schnittstellenerweiterung` | Schnittstellenerweiterung | net10.0 | console | 1 | 29 |
| `Interfaces/Statische Mitglieder/Felder und konkrete ausführbare Mitglieder` | Felder und konkrete ausführbare Mitglieder | net10.0 | console | 1 | 40 |
| `Interfaces/Statische Mitglieder/Generische Mathematik/Anwendung` | Generische Mathematik | net10.0 | console | 1 | 22 |
| `Interfaces/Statische Mitglieder/Generische Mathematik/Operatorüberladung` | Operatorüberladung nur CRTP | net10.0 | console | 1 | 33 |
| `Interfaces/Statische Mitglieder/Statische Polymorphie/Abstrakte Methoden` | Abstrakte Methoden | net10.0 | console | 1 | 22 |
| `Interfaces/Statische Mitglieder/Statische Polymorphie/Ohne CRTP` | Statische Polymorphie ohne CRTP | net10.0 | console | 1 | 35 |
| `Interfaces/Statische Mitglieder/Statische Polymorphie/Virtuelle Methoden` | Virtuelle Methoden | net10.0 | console | 1 | 24 |
| `Interfaces/Typformalparameter in generischen Interfaces/Invarianz` | Invarianz | net10.0 | console | 3 | 45 |
| `Interfaces/Typformalparameter in generischen Interfaces/Kontravarianz` | Kontravarianz | net10.0 | console | 3 | 50 |
| `Interfaces/Typformalparameter in generischen Interfaces/Kovarianz` | Kovarianz | net10.0 | console | 3 | 56 |

### Klassen und Objekte

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Klassen und Objekte/Bruch/b0` | b0 | net10.0 | console | 2 | 103 |
| `Klassen und Objekte/Bruch/b0 Aufwand beim Eigenschaftszugriff` | b0 Aufwand beim Eigenschaftszugriff | net10.0 | console | 2 | 158 |
| `Klassen und Objekte/Bruch/b1 Methoden-Überladung` | b1 | net10.0 | console | 2 | 114 |
| `Klassen und Objekte/Bruch/b10 IComparable, IClonable` | b10 | net10.0 | console | 2 | 159 |
| `Klassen und Objekte/Bruch/b11 IAdditionOperators` | b11 | net10.0 | console | 2 | 173 |
| `Klassen und Objekte/Bruch/b2 Konstruktoren` | b2 | net10.0 | console | 2 | 122 |
| `Klassen und Objekte/Bruch/b3 DuplWerte` | b3 | net10.0 | console | 2 | 123 |
| `Klassen und Objekte/Bruch/b4 Klone` | b4 | net10.0 | console | 2 | 123 |
| `Klassen und Objekte/Bruch/b5 Statische Member` | b5 | net10.0 | console | 2 | 139 |
| `Klassen und Objekte/Bruch/b6 Rekursion` | b6 | net10.0 | console | 2 | 129 |
| `Klassen und Objekte/Bruch/b7 Operatoren-Überladung` | b7 | net10.0 | console | 2 | 149 |
| `Klassen und Objekte/Bruch/b8 Aggregation` | b8 | net10.0 | console | 3 | 225 |
| `Klassen und Objekte/Bruch/b9 ToString-Überschreibung` | b9 | net10.0 | console | 2 | 135 |
| `Klassen und Objekte/Bruch/FractionLib` | FractionLib | net10.0 | library | 1 | 139 |
| `Klassen und Objekte/Finalisierer` | Finalisierer | net10.0 | console | 1 | 23 |
| `Klassen und Objekte/Geschachtelte Klassen` | Geschachtelte Klassen | net10.0 | console | 1 | 42 |
| `Klassen und Objekte/Indexer` | PersList | net10.0 | console | 3 | 74 |
| `Klassen und Objekte/Lokale Methoden` | LokaleMethoden | net10.0 | console | 1 | 18 |
| `Klassen und Objekte/R2Vek` | R2Vek | net10.0 | console | 2 | 83 |
| `Klassen und Objekte/ref readonly - Parameter` | ref readonly - Parameter | net10.0 | console | 1 | 29 |
| `Klassen und Objekte/Rekursion` | Rekursion | net10.0 | console | 2 | 78 |
| `Klassen und Objekte/required` | required | net10.0 | console | 1 | 28 |

### Kollektionen

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Kollektionen/DictionaryDemo` | DictionaryDemo | net10.0 | console | 1 | 26 |
| `Kollektionen/HashSetDemo` | HashSetDemo | net10.0 | console | 1 | 36 |
| `Kollektionen/ImmutableList-Performance` | ImmutableList-Performance | net10.0 | console | 1 | 55 |
| `Kollektionen/ListSetContains` | ListSetContains | net10.0 | console | 1 | 43 |
| `Kollektionen/Mengenlotto` | Mengenlotto | net10.0 | console | 1 | 9 |

### LINQ-to-Objects

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `LINQ-to-Objects/Abfragesyntax/Einstieg` | Abfrageausdrücke | net8.0 | console | 1 | 44 |
| `LINQ-to-Objects/Abfragesyntax/into` | Folgeabfragen | net8.0 | console | 1 | 49 |
| `LINQ-to-Objects/Abfragesyntax/Join` | JoinAbfragesyntax | net8.0 | console | 1 | 60 |
| `LINQ-to-Objects/Abfragesyntax/let` | Mehrere Bereichsvariablen | net8.0 | console | 1 | 61 |
| `LINQ-to-Objects/Abfragesyntax/SelectMany per Abfragesyntax` | SelectManyPerAbfragesyntax | net8.0 | console | 1 | 67 |
| `LINQ-to-Objects/Einstieg` | LinqToObjectsEinstieg | net8.0 | console | 1 | 51 |
| `LINQ-to-Objects/FindDuplicate` | FindDuplicate | net8.0 | console | 1 | 36 |
| `LINQ-to-Objects/GroupBy` | GroupBy | net8.0 | console | 1 | 44 |
| `LINQ-to-Objects/GroupJoin` | GroupJoin | net5.0 | console | 1 | 60 |
| `LINQ-to-Objects/Join` | JoinSelectMany | net5.0 | console | 1 | 56 |
| `LINQ-to-Objects/OfType` | OfType | net8.0 | console | 1 | 13 |
| `LINQ-to-Objects/PLINQ/Abbrechen` | PLINQ abbrechen | net8.0 | console | 1 | 56 |
| `LINQ-to-Objects/PLINQ/Abfragesyntax` | AbfragesyntaxAsParallel | net8.0 | console | 1 | 40 |
| `LINQ-to-Objects/PLINQ/AsOrdered` | AsOrdered | net8.0 | console | 1 | 12 |
| `LINQ-to-Objects/PLINQ/AsParallel` | AsParallel | net8.0 | console | 1 | 44 |
| `LINQ-to-Objects/PLINQ/ForAll` | ForAll | net8.0 | console | 1 | 14 |
| `LINQ-to-Objects/Sortieren` | Sortieren | net8.0 | console | 1 | 21 |
| `LINQ-to-Objects/Unterabfrage` | Unterabfrage | net8.0 | console | 1 | 40 |
| `LINQ-to-Objects/Zip` | Zip | net8.0 | console | 1 | 6 |

### Multithreading

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Multithreading/async await/Abbrechen` | CancelAsync | net8.0 | console | 1 | 37 |
| `Multithreading/async await/AsyncLambda` | AsyncLambda | net8.0 | console | 1 | 16 |
| `Multithreading/async await/AsyncStreamMethods` | AsyncStreamMethods | net8.0-windows7.0 | WPF | 2 | 77 |
| `Multithreading/async await/AsyncVoidException` | AsyncVoidException | net8.0 | console | 1 | 15 |
| `Multithreading/async await/AsyncVoidExceptionWPF` | AsyncVoidExceptionWPF | net8.0-windows | WPF | 1 | 41 |
| `Multithreading/async await/AsyncWaitDeadlock` | AsyncWaitDeadlock | net5.0-windows | WPF | 2 | 39 |
| `Multithreading/async await/AsyncWebAccess` | AsyncWebAccess | net8.0-windows7.0 | WPF | 2 | 76 |
| `Multithreading/async await/Ausnahmen` | ExInAsync | net8.0 | console | 1 | 49 |
| `Multithreading/async await/await using` | AwaitUsingDeclaration | net8.0 | console | 1 | 18 |
| `Multithreading/async await/DirSize` | DirSize | net8.0-windows | WPF | 2 | 132 |
| `Multithreading/async await/Eratosthenes` | Eratosthenes | net8.0-windows | WPF | 3 | 82 |
| `Multithreading/async await/GetAwaiter` | GetAwaiter | net8.0-windows7.0 | WPF | 2 | 51 |
| `Multithreading/async await/Progress` | Progress | net8.0-windows7.0 | WPF | 2 | 51 |
| `Multithreading/async await/Zustandsmaschine` | Zustandsmaschine | net8.0 | console | 1 | 14 |
| `Multithreading/Thread/DeadLock` | Deadlock | net8.0 | console | 1 | 47 |
| `Multithreading/Thread/Interrupt` | InterruptDemo | net8.0 | console | 1 | 32 |
| `Multithreading/Thread/Join` | Join | net8.0 | console | 1 | 36 |
| `Multithreading/Thread/Monitor/lock-Anweisung` | Lock | net8.0 | console | 3 | 99 |
| `Multithreading/Thread/Monitor/Race Condition` | RaceCondition | net8.0 | console | 3 | 98 |
| `Multithreading/Thread/Monitor/WaitPulse` | WaitPulse | net8.0 | console | 3 | 109 |
| `Multithreading/Thread/ParameterizedThreadStart` | ParameterizedThreadStart | net8.0 | console | 1 | 18 |
| `Multithreading/Thread/ReaderWriterLockSlim` | ReaderWriterLockSlim | net8.0 | console | 1 | 41 |
| `Multithreading/Thread/Signalisierungsobjekte/AutoResetEvent` | AutoResetEvent | net8.0 | console | 1 | 39 |
| `Multithreading/Thread/Signalisierungsobjekte/AutoResetEventOrCancellation` | AutoResetEventOrCancellation | net8.0 | console | 1 | 42 |
| `Multithreading/Thread/Signalisierungsobjekte/CountDownEvent` | CountDownEvent | net8.0 | console | 1 | 32 |
| `Multithreading/Thread/SpinLock` | SpinLockQueue | net8.0 | console | 1 | 89 |
| `Multithreading/Thread/ThreadCancellation` | ThreadCancellation | net8.0 | console | 1 | 32 |
| `Multithreading/Thread/Unbehandelte Ausnahmen/Konsole` | Unbehandelte Ausnahmen | net8.0 | console | 1 | 24 |
| `Multithreading/Thread/Unbehandelte Ausnahmen/WPF` | HandleUnhandledException | net8.0-windows | WPF | 3 | 62 |
| `Multithreading/Thread/volatile` | volatile | net8.0 | console | 1 | 23 |
| `Multithreading/Threadpool/QueueUserWorkItem` | QueueUserWorkItem | net8.0 | console | 1 | 27 |
| `Multithreading/Threadpool/TPL` | TaskPool | net8.0 | console | 1 | 50 |
| `Multithreading/Timer/Threading-Timer` | Threading-Timer | net8.0-windows7.0 | WPF | 1 | 51 |
| `Multithreading/Timer/WPF-Timer` | WPF-Timer | net8.0-windows | WPF | 1 | 49 |
| `Multithreading/TPL/Ausnahmebehandlung/AggregateException` | AggregateException | net8.0 | console | 1 | 37 |
| `Multithreading/TPL/Ausnahmebehandlung/DebuggingTaskExceptions` | DebuggingTaskExceptions | net8.0 | console | 1 | 13 |
| `Multithreading/TPL/Ausnahmebehandlung/Flatten` | Flatten | net8.0 | console | 1 | 43 |
| `Multithreading/TPL/Ausnahmebehandlung/UISyncContext` | UISyncContext | net8.0-windows7.0 | WPF | 2 | 69 |
| `Multithreading/TPL/Ausnahmebehandlung/UnobservedTaskException` | UnobservedTaskException | net8.0 | console | 1 | 33 |
| `Multithreading/TPL/ContinueOnlyOnFaulted` | ContinueOnlyOnFaulted | net8.0 | console | 1 | 18 |
| `Multithreading/TPL/Cooperative Cancellation Framework/Aufgabenfamilie` | CooperativeCancellationPropagation | net8.0 | console | 1 | 63 |
| `Multithreading/TPL/Cooperative Cancellation Framework/Einzelaufgabe` | CooperativeCancellation | net8.0 | console | 1 | 59 |
| `Multithreading/TPL/Fortsetzungsaufgaben/ContinueWhenAll` | ContinueWhenAll | net8.0 | console | 1 | 52 |
| `Multithreading/TPL/Fortsetzungsaufgaben/ContinueWhenAny` | ContinueWhenAny | net8.0 | console | 1 | 31 |
| `Multithreading/TPL/Metaaufgaben/WhenAll` | WhenAll | net8.0 | console | 1 | 28 |
| `Multithreading/TPL/Metaaufgaben/WhenAny` | WhenAny | net8.0 | console | 1 | 30 |
| `Multithreading/TPL/Parallel/ParallelFor` | ParallelFor | net8.0 | console | 1 | 31 |
| `Multithreading/TPL/Parallel/ParallelForEach` | ParallelForEach | net8.0 | console | 1 | 51 |
| `Multithreading/TPL/Parallel/ParallelInvoke` | ParallelInvoke | net8.0 | console | 1 | 37 |
| `Multithreading/TPL/Task` | Task | net8.0 | console | 1 | 27 |
| `Multithreading/TPL/TaskCompletionSource` | TaskCompletionSource | net8.0 | console | 1 | 32 |
| `Multithreading/TPL/TaskResult` | TaskResult | net8.0 | console | 1 | 27 |
| `Multithreading/TPL/TaskResultParam` | TaskResultParam | net8.0 | console | 1 | 24 |

### Netzwerk

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Netzwerk/DNS` | DNS | net8.0-windows | WPF | 3 | 75 |
| `Netzwerk/HTTPS/Anforderungsparameter/GET` | GET | net8.0 | console | 1 | 26 |
| `Netzwerk/HTTPS/Anforderungsparameter/POST` | POST | net8.0 | console | 1 | 30 |
| `Netzwerk/HTTPS/Datei herunterladen` | FileDownload | net8.0 | console | 1 | 45 |
| `Netzwerk/HTTPS/HTML-Code abrufen` | GetHtml | net8.0 | console | 1 | 39 |
| `Netzwerk/HTTPS/HttpMethod.Head` | HttpMethod.Head | net8.0 | console | 1 | 31 |
| `Netzwerk/Socket/TcpClient` | TcpClient | net8.0 | console | 1 | 28 |
| `Netzwerk/Socket/TcpClient mit verzögerter Rückmeldung` | TcpClient | net8.0 | console | 1 | 25 |
| `Netzwerk/Socket/TcpServer` | TcpServer | net8.0 | console | 1 | 47 |
| `Netzwerk/Socket/TcpServerTPL` | TcpServerTPL | net8.0 | console | 1 | 78 |
| `Netzwerk/Uri` | Uri | net8.0 | console | 1 | 20 |
| `Netzwerk/WebView2` | WebView2 | net8.0-windows | WPF | 3 | 124 |

### Typgenerisches Programmieren

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Typgenerisches Programmieren/default` | default | net10.0 | console | 1 | 20 |
| `Typgenerisches Programmieren/Generische Erweiterungsmethoden` | Generische Erweiterungsmethoden | net10.0 | console | 1 | 23 |
| `Typgenerisches Programmieren/Listenwerte` | Listenwerte | net10.0 | console | 1 | 19 |
| `Typgenerisches Programmieren/SimpleSortedList` | SimpleSortedList | net10.0 | console | 2 | 41 |
| `Typgenerisches Programmieren/SimpleStack` | SimpleStack | net10.0 | console | 2 | 39 |

### Vererbung und Polymorphie

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Vererbung und Polymorphie/abstract` | abstract | net10.0 | console | 4 | 96 |
| `Vererbung und Polymorphie/Basisklassenreferenzen` | Basisklassenreferenzen | net10.0 | console | 4 | 84 |
| `Vererbung und Polymorphie/DoubleExtensions` | DoubleExtensions | net10.0 | console | 1 | 31 |
| `Vererbung und Polymorphie/Erweiterungsmitglieder/Einleitung` | Einleitung Erweiterungsmethoden | net10.0 | console | 1 | 16 |
| `Vererbung und Polymorphie/Erweiterungsmitglieder/extension-Block` | extension-Block | net10.0 | console | 1 | 20 |
| `Vererbung und Polymorphie/Erweiterungsmitglieder/Kombination` | Kombination | net10.0 | console | 1 | 17 |
| `Vererbung und Polymorphie/Felder ausblenden` | Felder ausblenden | net10.0 | console | 1 | 19 |
| `Vererbung und Polymorphie/Figuren` | Figuren | net10.0 | console | 3 | 36 |
| `Vererbung und Polymorphie/Methoden ausblenden/Figuren` | Methoden ausblenden | net10.0 | console | 3 | 46 |
| `Vererbung und Polymorphie/Methoden ausblenden/Privates Ausblenden` | Privates Ausblenden | net10.0 | console | 1 | 18 |
| `Vererbung und Polymorphie/Methoden ausblenden/Statisches Ausblenden` | Statisches Ausblenden | net10.0 | console | 1 | 14 |
| `Vererbung und Polymorphie/Methoden ausblenden/Vermeintliches Ausblenden` | Vermeintliches Ausblenden | net10.0 | console | 1 | 13 |
| `Vererbung und Polymorphie/Polymorphie` | Polymorphie | net10.0 | console | 4 | 68 |
| `Vererbung und Polymorphie/protected` | protected | net10.0 | console | 3 | 52 |
| `Vererbung und Polymorphie/Record-Figuren` | Record-Figuren | net10.0 | console | 3 | 20 |
| `Vererbung und Polymorphie/Verdeckte Virtualität` | Verdeckte Virtualität | net10.0 | console | 1 | 27 |

### Weitere .NETte Typen

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `Weitere .NETte Typen/Arrays/Eratosthenes` | Eratosthenes | net10.0 | console | 1 | 57 |
| `Weitere .NETte Typen/Arrays/FloatMatrix` | FloatMatrix | net10.0 | console | 2 | 67 |
| `Weitere .NETte Typen/Arrays/Lotto` | Lotto | net10.0 | console | 1 | 31 |
| `Weitere .NETte Typen/Arrays/Schnittstellen-Ausstattung` | Schnittstellen-Ausstattung | net10.0 | console | 1 | 15 |
| `Weitere .NETte Typen/Arrays/SortSearch` | SortSearch | net10.0 | console | 1 | 33 |
| `Weitere .NETte Typen/Arrays/UniRand` | UniRand | net10.0 | console | 1 | 11 |
| `Weitere .NETte Typen/Enumerationen` | Enumerationen | net10.0 | console | 1 | 22 |
| `Weitere .NETte Typen/Struktur-Tupel/Deconstruct in eigenen Typen` | Deconstruct | net10.0 | console | 1 | 13 |
| `Weitere .NETte Typen/Struktur-Tupel/Tupel als Rückgabetyp` | ReturnTuple | net10.0 | console | 1 | 29 |
| `Weitere .NETte Typen/Strukturen/Aufwandsvergleich` | Aufwandsvergleich | net10.0 | console | 2 | 54 |
| `Weitere .NETte Typen/Strukturen/Clr Heap Allocation Analyzer` | ClrHeapAllocationAnalyzer | net10.0 | console | 1 | 18 |
| `Weitere .NETte Typen/Strukturen/Punkt` | Punkt | net10.0 | console | 2 | 65 |
| `Weitere .NETte Typen/Zeichenfolgen/PerZuf` | PerZuf | net10.0 | console | 1 | 13 |
| `Weitere .NETte Typen/Zeichenfolgen/StringBuilder` | StringBuilder | net10.0 | console | 1 | 25 |
| `Weitere .NETte Typen/Zeichenfolgen/StringIntern` | StringIntern | net10.0 | console | 1 | 47 |
| `Weitere .NETte Typen/Zeichenfolgen/StringUtil` | StringUtil | net10.0 | console | 2 | 63 |

### WPF

| Path | Project | TFM | Kind | .cs | LOC |
|---|---|---|---|---:|---:|
| `WPF/Abhängigkeitseigenschaften/Angefügte Eigenschaften` | Angefügte Eigenschaften | net10.0-windows | WPF | 1 | 42 |
| `WPF/Abhängigkeitseigenschaften/DmToEuroBinding` | DmToEuroBinding | net10.0-windows | WPF | 3 | 60 |
| `WPF/Abhängigkeitseigenschaften/Eigenschaftsübertragung` | Eigenschaftsübertragung | net10.0-windows | WPF | 3 | 45 |
| `WPF/ApplicationStartup` | ApplicationStartup | net10.0-windows | WPF | 3 | 48 |
| `WPF/CodeBehind` | CodeBehind | net10.0-windows | WPF | 3 | 44 |
| `WPF/Datenbindung` | Datenbindung | net10.0-windows | WPF | 4 | 87 |
| `WPF/Eratosthenes` | Eratosthenes | net10.0-windows | WPF | 3 | 86 |
| `WPF/Eurokonverter` | Eurokonverter | net10.0-windows | WPF | 3 | 71 |
| `WPF/Konsolenprogramm` | Konsolenprogramm | net10.0 | console | 2 | 141 |
| `WPF/Layoutcontainer/Canvas/Bilder` | Canvas | net10.0-windows | WPF | 3 | 31 |
| `WPF/Layoutcontainer/Canvas/Schalter` | Canvas | net10.0-windows | WPF | 3 | 31 |
| `WPF/Layoutcontainer/DockPanel` | DockPanel | net10.0-windows | WPF | 3 | 31 |
| `WPF/Layoutcontainer/Geschachtelte Layoutcontainer` | Geschachtelte Layoutcontainer | net10.0-windows | WPF | 3 | 67 |
| `WPF/Layoutcontainer/Grid mit ColumnSpan` | Grid mit ColumnSpan | net10.0-windows | WPF | 3 | 32 |
| `WPF/Layoutcontainer/StackPanel` | StackPanel | net10.0-windows | WPF | 3 | 32 |
| `WPF/Layoutcontainer/UniformGrid` | UniformGrid | net10.0-windows | WPF | 3 | 31 |
| `WPF/Layoutcontainer/WindowContent` | WindowContent | net10.0-windows7.0 | WPF | 3 | 36 |
| `WPF/Layoutcontainer/WrapPanel` | WrapPanel | net10.0-windows | WPF | 3 | 32 |
| `WPF/MVVM/Ohne Kommandos` | MVVM ohne Kommandos | net10.0-windows | WPF | 6 | 154 |
| `WPF/MVVM/RelayCommand` | MVVM RC | net10.0-windows | WPF | 8 | 278 |
| `WPF/MVVM/Separate ICommand-Implementationen/Mit CommandParameter` | MVVM mit CommandParameter | net10.0-windows | WPF | 8 | 238 |
| `WPF/MVVM/Separate ICommand-Implementationen/Ohne CommandParameter` | MVVM ohne CommandParameter | net10.0-windows | WPF | 8 | 243 |
| `WPF/MVVM/Toolkit` | Toolkit | net10.0-windows | WPF | 6 | 160 |
| `WPF/Routingereignisse/Beobachtungsstudie` | Beobachtungsstudie | net10.0-windows | WPF | 3 | 44 |
| `WPF/Routingereignisse/Ereignisbehandlung durch statische Methoden` | Ereignisbehandlung durch statische Methoden | net10.0-windows | WPF | 3 | 54 |
| `WPF/Routingereignisse/RoutedEventWrapper` | RoutedEventWrapper | net10.0-windows | WPF | 2 | 39 |
| `WPF/RssFeedReader/BlockedUI` | RssFeedReader | net10.0-windows | WPF | 4 | 106 |
| `WPF/Steuerelemente/Button/Bitmap` | ButtonBitmap | net10.0-windows7.0 | WPF | 3 | 42 |
| `WPF/Steuerelemente/Button/Text` | ButtonText | net10.0-windows | WPF | 3 | 40 |
| `WPF/Steuerelemente/Button/UpDown` | UpDown | net10.0-windows | WPF | 3 | 40 |
| `WPF/Steuerelemente/Einstieg` | Einstieg | net10.0-windows | WPF | 3 | 37 |
| `WPF/Steuerelemente/Menü-Items` | Menü-Items | net10.0-windows7.0 | WPF | 3 | 45 |
| `WPF/Steuerelemente/On-Methoden/OnMouseDown Window` | OnMouseDown Window | net10.0-windows | WPF | 3 | 42 |
| `WPF/Steuerelemente/On-Methoden/Vergessener base-Aufruf` | OnClick Button | net10.0-windows | WPF | 1 | 30 |
| `WPF/Steuerelemente/Selector/AddRemoveItems` | AddRemoveItems | net10.0-windows | WPF | 3 | 53 |
| `WPF/Steuerelemente/Selector/List-und ComboBox` | List-und ComboBox | net10.0-windows | WPF | 3 | 36 |
| `WPF/Steuerelemente/Texterfassung/PasswordBox` | PasswordBox | net10.0-windows | WPF | 3 | 40 |
| `WPF/Steuerelemente/Texterfassung/TextBox` | TextBox | net10.0-windows | WPF | 3 | 63 |
| `WPF/Steuerelemente/ToolTip` | ToolTip | net10.0-windows | WPF | 3 | 29 |
| `WPF/Steuerelemente/Umschalter` | Umschalter | net10.0-windows | WPF | 3 | 48 |
| `WPF/Validierung` | Validierung | net10.0-windows | WPF | 4 | 147 |
| `WPF/WpfOhneXaml` | WpfOhneXaml | net10.0-windows | WPF | 2 | 27 |
