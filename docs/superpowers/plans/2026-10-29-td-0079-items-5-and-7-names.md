# TD-0079 items 5 and 7: parameter names and SQL Server's names, each in one place

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compare parameter names through one named rule, `SqlParameterName.Comparer`, `Comparison` and `IndexOf`, and keep SQL Server's naming rules, its brackets and its CLR system types, in one `SqlServerNames` in the generator that the tool uses, with no change to what is generated or to what the tool writes.

**Architecture:** `SqlParameterName` (`src/SqlSource/Parsing/`) gains the comparer of parameter names and one search of a query's parameters by name; every site that compares a parameter's name, in the generator and the tool, uses them.  A new `src/SqlSource/SqlServerNames.cs`, beside `SystemSchemas`, holds the two forms of a bracketed name and the three CLR types of `sys`; `SqlServerBinding`, `SqlServerTypeMap` and the tool's `SqlServerLive` and `SqlServerTypes` use it.

**Tech Stack:** C# (`netstandard2.0` generator, `net8.0` tool), xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md`, items 5 and 7 and their proposed fixes.

## Global Constraints

- No change to what is generated or to what the tool writes: every existing test, snapshot and sidecar stays as it is.
- The generator is `netstandard2.0` with Roslyn 4.8.0 (`src/SqlSource/AGENTS.md`); allocate nothing new on a parse or generation path.
- The tool shares the generator's code and never copies it (`src/SqlSource.Tool/AGENTS.md`).
- `src/SqlSource/AGENTS.md` has about 30 bytes of its 8 KB left: add no rule there.
- Only parameter names change comparer.  A column's name (`SqlMarkerScope.AddColumn`, `ColumnMarker`, `ModelReader`), a keyword, a type name or a path keeps its own comparison.

## Where merged code differs from the spec

- The spec counts "about 25 sites".  The search finds these comparisons of a parameter's name: `SqlParameterList.IndexOf`, `SqlMarkerScope` (declarations), `QueryTyper.MarkedParameter`, the tool's `EntryBuilder.Find` (two), `NpgsqlCheck.OrderOf`, `ParameterPlaceholders.OrdinalOf`, `UncastParameters.Uncast` (not in the spec's list), `SqlServerErrors.NamedIn`, `SqlServerSession` (four), `SqlServerSession.Builders` (two) and `ParameterCopies` (ten).
- Item 7's two bracket forms must both stay.  `UdtTypeName` brackets a part only where SqlClient's split at dots needs it, and the generated code already writes that; a declaration of `@params` is T-SQL, where a keyword such as `User` needs brackets though it is all letters.  Both go behind `SqlServerNames`, named for their use.
- `TypeKeyNames.SqlServerNames` lists every SQL Server type name a key may give, the CLR ones among them, and `CastTargets.BuiltIn` lists the built-in types that are no CLR type; neither is a copy of the CLR list, and both stay.

## Review Focus

1. A parameter whose name differs only in case from another's use (`@Id` declared, `@id` used): every site still finds it.  Existing parse and describer tests pin this; Task 1 adds `IndexOf` tests for case.
2. `SqlParameterList`'s search runs on a builder during the parse: `IndexOf` must take it without boxing or a copy (generic over `IReadOnlyList<SqlQueryParameter>`).
3. A user CLR type whose name holds a dot, a space or a `]`: `UdtTypeName` unchanged (`SqlServerBindingTests` pins it); Task 2 tests `SqlServerNames.UdtPart` directly.
4. A declared user type named by a keyword (`[dbo].[User]`): still bracketed always (`SqlServerTypesTests.Declared_*`).
5. A CLR system type name in another case (`GEOMETRY`): `MayBeClr` still true, the binding row still found.

---

### Task 1: `SqlParameterName.Comparer`, `Comparison` and `IndexOf` at every site (item 5)

**Files:**
- Modify: `src/SqlSource/Parsing/SqlParameterName.cs`
- Modify: `src/SqlSource/Parsing/SqlParameterList.cs`, `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` (the declarations' loop only), `src/SqlSource/Generation/Types/QueryTyper.cs`
- Modify: `src/SqlSource.Tool/Describing/EntryBuilder.cs`, `Describing/Postgres/NpgsqlCheck.cs`, `Describing/Postgres/Statements/ParameterPlaceholders.cs`, `Describing/Postgres/Statements/UncastParameters.cs` (the set of names only), `Describing/SqlServer/SqlServerErrors.cs`, `SqlServerSession.cs`, `SqlServerSession.Builders.cs`, `ParameterCopies.cs` (paths as merged; `td-0079-describers` may move `ParameterCopies` to `SqlServer/Statements/`)
- Test: `tests/SqlSource.Tests/Parsing/SqlParameterNameTests.cs`

**Interfaces:**
- Produces, in `SqlParameterName`:
  - `public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;`
  - `public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;`
  - `public static int IndexOf<TList>(TList parameters, ReadOnlySpan<char> name) where TList : IReadOnlyList<SqlQueryParameter>`: the index of the parameter of that name, by `Comparison`, or -1.  Generic so that `EquatableArray<SqlQueryParameter>` and `ImmutableArray<SqlQueryParameter>.Builder` pass without boxing.
- The class's summary says it is also the one rule for comparing a parameter's name: two names that differ only in case are one parameter, as the server and the drivers take them.

- [ ] **Step 1: Tests** in `SqlParameterNameTests`:
  - `IndexOf_NameInAnotherCase_FindsTheParameter`: list `[id, Name]`, `IndexOf(list, "NAME")` is 1.
  - `IndexOf_NameNotInTheList_IsMinusOne`: `IndexOf(list, "other")` is -1; empty list is -1.
  - `Comparer_NamesDifferingOnlyInCase_AreEqual`: `Comparer.Equals("Id", "iD")` true, `"Id"` vs `"Idx"` false; `Comparison` is `StringComparison.OrdinalIgnoreCase`.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~SqlParameterNameTests`; expect a build failure.
- [ ] **Step 3:** Add the three members.
- [ ] **Step 4:** Replace each site:
  - a loop that finds a query parameter's index by name (`SqlParameterList.IndexOf`, `EntryBuilder.Find` over `SqlQueryParameter`, `ParameterPlaceholders.OrdinalOf`) calls `SqlParameterName.IndexOf`; `SqlParameterList`'s private `IndexOf` goes.
  - `NpgsqlCheck.OrderOf` and `QueryTyper.MarkedParameter` find by `IndexOf` (names in a query's list are distinct ignoring case, so the first match is the one).
  - every other `StringComparison.OrdinalIgnoreCase` on a parameter's name becomes `SqlParameterName.Comparison`, and every `StringComparer.OrdinalIgnoreCase` keyed by one `SqlParameterName.Comparer`.
- [ ] **Step 5:** `grep -rn OrdinalIgnoreCase src --include='*.cs'` shows no parameter name left.  Run `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tool.Tests`; all pass.
- [ ] **Step 6:** Commit `refactor: compare parameter names through SqlParameterName (TD-0079 item 5)`.

### Task 2: `SqlServerNames` (item 7)

**Files:**
- Create: `src/SqlSource/SqlServerNames.cs`
- Modify: `src/SqlSource/Generation/Emitting/SqlServerBinding.cs` (`UdtTypeNameOf`, `Part`, the loop of `BuildRows`), `src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs` (the three `NeedsTypes` rows' keys)
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerLive.cs` (`Bracketed` goes; `TableSelect` uses `SqlServerNames.Bracketed`), `SqlServerTypes.cs` (`ClrSystemTypes` goes; `Declared` and `MayBeClr` use `SqlServerNames`)
- Test: `tests/SqlSource.Tests/SqlServerNamesTests.cs`

**Interfaces:**
- Produces, `internal static class SqlServerNames` in namespace `SqlSource`:
  - `public const string HierarchyId = "hierarchyid"`, `Geography = "geography"`, `Geometry = "geometry"`.
  - `public static IReadOnlyList<string> ClrSystemTypes`: those three.
  - `public static bool IsClrSystemType(string name)`: one of them, ignoring case, as the server matches a type's name.
  - `public static string Bracketed(string name)`: always in brackets, a `]` written twice, for T-SQL text the server reads.
  - `public static string UdtPart(string part)`: as it is when all letters, digits and underscores, else `Bracketed(part)`, for a part of a `UdtTypeName`, which SqlClient splits at dots outside brackets.
- The summary says why both forms exist (the reason in "Where merged code differs").

- [ ] **Step 1: Tests** in `SqlServerNamesTests`:
  - `Bracketed_Name_IsInBracketsWithEachBracketDoubled`: `User` → `[User]`, `a]b` → `[a]]b]`, `` → `[]`.
  - `UdtPart_PlainName_IsAsItIs`: `Point`, `my_type1`.
  - `UdtPart_OtherCharacters_AreInBrackets`: `my type` → `[my type]`, `a.b` → `[a.b]`, `a]b` → `[a]]b]`.
  - `IsClrSystemType_*`: the three, and `GEOMETRY`, true; `int`, `geometryx` false; `ClrSystemTypes` is exactly the three.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~SqlServerNamesTests`; expect a build failure.
- [ ] **Step 3:** Create `SqlServerNames`; switch the four files to it.
- [ ] **Step 4:** Run `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tool.Tests`; all pass, `SqlServerBindingTests`, `SqlServerLiveTests` and `SqlServerTypesTests` unchanged.
- [ ] **Step 5:** Commit `refactor: keep SQL Server's naming rules in SqlServerNames (TD-0079 item 7)`.

### Task 3: Documents

**Files:**
- Modify: `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md` (items 5 and 7 struck with "Fixed: …", their fixes "Done."), its row in `docs/tech-debt/README.md` (drop what 5 and 7 named)
- Modify: `docs/architecture/tool.md` (the `Describing/SqlServer/` row names `SqlServerNames` among what it takes from the generator; both describer rows say parameter names are compared by `SqlParameterName`), `docs/architecture/generator.md` (`UdtTypeName`'s bracket rule names `SqlServerNames.UdtPart`), `docs/architecture/sqlserver-describer.md` (`MayBeClr` uses `SqlServerNames`' list)
- Modify: `src/SqlSource.Tool/Describing/SqlServer/AGENTS.md` if it names `Bracketed`; no `src/SqlSource/AGENTS.md` rule.

- [ ] **Step 1:** Edit the documents.
- [ ] **Step 2:** `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Documentation` passes.
- [ ] **Step 3:** Commit `docs: TD-0079 items 5 and 7 fixed`.
