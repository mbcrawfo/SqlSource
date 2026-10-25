# Query generation 8.2: converters - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a project convert a type override's C# type to and from the type the base map reads with static methods marked `[SqlSourceConverter]`, found in its own source and on the mapped types, validated, and applied by 8.1's generated methods to columns, parameters and PostgreSQL arrays.

**Architecture:** A new attribute in the post-initialization file.  Two sources of converters: a `ForAttributeWithMetadataName` step over the project's source (`ConverterReader`), and the members of each mapped type of a referenced assembly, read by `MappedTypeResolver` beside the `MappedType` records but given to the pipeline as a value of their own.  Both merge into one sorted `ConverterSet`, which gives `SQLSRC277`/`SQLSRC278` at the project's methods.  The typer records for each overridden property its pure base-map type (`PropertyType.Database`); `MethodsInput` takes the slice of the converter set that the type's overridden properties touch; `ConversionPlanner` decides each column's read and each parameter's write (`SQLSRC277` to `SQLSRC280` at the query); `QueryMethodWriter` and `ParameterBinder` wrap the read and the bound value in the converter's call, leaving the binding step's shape as 8.1 wrote it.

**Tech Stack:** C# (`netstandard2.0` generator, `LangVersion` preview, Roslyn 4.8.0 floor), .NET 10 SDK, xunit v3, Shouldly; generated code is C# 12 on .NET 8+, compiled in tests against Npgsql 8.0.3, 9.0.3, 10.0.3 and Microsoft.Data.SqlClient 6.1.7, 7.1.1 (`NpgsqlReferences`, `SqlClientReferences`).

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-2-converters-design.md`](../specs/2026-10-24-query-generation-phase-8-2-converters-design.md), with [the converter research](../specs/2026-10-24-query-generation-phase-8-converter-research.md), [8.0's spike](../specs/2026-10-24-query-generation-phase-8-spike.md) and the epic's phase 8 section.

## Global Constraints

- Generated code uses nothing newer than C# 12 and runs on .NET 8; the attribute file stays C# 7.3 with no `#nullable` and no nullable annotation (`src/SqlSource/AGENTS.md`).
- `SqlSourceConverterAttribute`: `internal sealed`, `[global::System.AttributeUsage(global::System.AttributeTargets.Method)]`, namespace `SqlSource`, **not** `[Conditional]`, metadata name `SqlSource.SqlSourceConverterAttribute`, in `AttributeSource.GeneratedTypes`.
- Converter types are compared as text in the form `ClrTypeName.Text` and the base maps write: a keyword bare (`int`, `string`, `byte[]`), every other type from `global::`, `, ` between type arguments, `?` written where annotated.  `SymbolDisplayFormat.FullyQualifiedFormat` with `UseSpecialTypes`, `IncludeNullableReferenceTypeModifier` and `EscapeKeywordIdentifiers` gives it.
- A converter's call is written `global::<containing type>.<method name>(...)`, the name with `@` when it is a keyword; every framework type from `global::`.
- The converter never sees null; a not-null column keeps the driver's throw as the oracle, except `SqlVector<float>`, checked by `IsNull` before the converter.
- Diagnostics are `SQLSRC277` to `SQLSRC280`, the ids the spec plans: 8.1 took exactly `SQLSRC272`-`SQLSRC276`, so these are the next free ones.  Each is an Error tagged `NotConfigurable`, written in full with a literal id, in `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md` and a section of `docs/manual/diagnostics.md`.
- `SQLSRC277` and `SQLSRC278` at a method of the project's source in any project; every diagnostic at a query only under `CodeGen`, from `MethodPlanner`, and the query then gets no method and keeps its models.
- Values between steps are records with `EquatableArray<T>`; no Roslyn object crosses a step; every `Collect()` is followed by a `Select` that gives an `EquatableArray<T>`; every named step is in `TrackingNames`; nothing a user writes makes the generator throw; a new file that touches symbols is added to the list in `src/SqlSource/AGENTS.md`.
- A new local or lambda parameter of a query's method takes its name from `LocalNames`, with a row in `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` (`tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`) and `Run_TokenWithAnAwkwardNameInAQueryMethod_CompilesOnTheNewestCSharp` (`tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs`).
- Every commit passes `./pre-commit-validation.sh` run after its last edit.  No describer changes, so no floor-image run is needed.
- Tests: `Method_Scenario_Outcome`, Shouldly, `sealed` classes; a file in `tests/SqlSource.Tests/Generator/` starts with the shared line, ends in `Tests.cs`, uses C# 12 at most and Roslyn 4.8.0 API, and states its `OutputKind`; helpers go in `tests/SqlSource.Testing/Generator/`.

## Calls the spec left open, settled

Every call of the spec's "Calls made in this spec" stands as the spec makes it; the code gave no reason to change one.  The builder's choices:

- **A faulty or doubled converter of the project's own source still counts as an edge.**  A query that would use it gets no method and no diagnostic of its own, since `SQLSRC277` or `SQLSRC278` at the method already fails the build; dropping the edge would add a misleading `SQLSRC280` or `SQLSRC279` at the query.
- **Two converters of one edge on a mapped type of a referenced assembly** are `SQLSRC278` at each query that would use the edge, naming both, since they have no position in the project.
- **Element by element is the rule for every array whose `T` is `X[]` and whose `B` is `Y[]` when no edge touches `T` itself**, not only for an element's override and a foreign `X`.  The spec's two cases are this rule's; a third, an array override to `X[]` of a non-foreign `X` with an `X`↔`Y` edge, is converted too rather than read as 8.1 reads it, which is what such an edge asks for.  It also needs no record of which level chose the element.
- **The methods file disables the warnings of every converter it calls and of every property type it reads or binds**, after its header, as a model's file disables its mapped types'.  8.1's methods name a mapped type in `GetFieldValue<T>`, and an `[Obsolete]` mapped type under `CodeGen` gives CS0618 in the methods file today; Task 4 pins that with a failing test first.  The same pragma serves the converters, so the fix belongs here.
- **A converter in a file-local type is `SQLSRC277`, not accessible**, since generated code in another file cannot name it.  Use `IsFileLocal` of the containing types if Roslyn 4.8.0 has it, and otherwise the `file` modifier of their declarations' syntax.

## Where merged code differs from the spec

- **`PropertyType` has no base-map type**, so the typer gains one (`PropertyType.Database`), set only for a type an override chose.  Its type is computed by the base map with no override lookup, which is the spec's "pure" `B`.
- **8.1's vector check keys on `MethodColumn.TypeText`**; it moves to the type the method reads (`MethodColumn.ReadText`), as 8.0's review finding R3-F1 already says it must.
- **The spec's step 4 is two tasks here** (4: the decision and its diagnostics; 5: the emitted read and write), and step 5, arrays, is Task 6, so that each task has a test cycle a reviewer can judge apart.

## Review Focus

1. **A nullable value-type `T` over a reference `B`** (`Email` record struct over `text`, column and parameter nullable): the read is `IsDBNull ? default(T?) : Convert(GetFieldValue<B>)` and the write `x is { } v ? (object)Convert(v) : DBNull.Value`, compiling with no nullable warning.  Task 5 pins the text and compiles it.
2. **A token named like a new local** (`value`, `element`, `item`) must not hide it.  Task 5 and Task 6 add the rows.
3. **A converter of one type to itself** (`DateTime`→`DateTime` under `timestamp=System.DateTime`) serves both directions and is not `SQLSRC278` with itself.  Task 4 tests the decision and Task 5 the text.
4. **A converter on a plain `timestamptz` column's type** (`DateTimeOffset`↔`MyApp.When`) never touches a column the base map types: no `Place`, no conversion, no diagnostic.  Task 4 tests it.
5. **An array of a value-type `T` with a `NULL` element** throws `InvalidCastException` naming the column, never Npgsql's message.  Task 6 pins the text.

---

### Task 1: The attribute

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.2: `In progress`)
- Add: this plan
- Modify: `src/SqlSource/Generation/AttributeSource.cs`
- Modify: `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md` (the list of types in **Problem**)
- Test: `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`, `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs`

**Interfaces:**
- Produces: `AttributeSource.ConverterAttributeMetadataName = "SqlSource.SqlSourceConverterAttribute"`, added to `GeneratedTypes`.

- [ ] **Step 1: Write the failing tests**
  - `AttributeSourceTests.Text_ConverterAttribute_TargetsMethodsAndIsNotConditional`: compile `AttributeSource.Text` at C# 7.3 and find `SqlSource.SqlSourceConverterAttribute`: `DeclaredAccessibility` internal, sealed, its `AttributeUsage` is `AttributeTargets.Method`, and it has no `ConditionalAttribute`.
  - `AttributeSourceTests.Text_ConverterAttributeOnAMethod_StaysInMetadata`: compile a library with the attribute text and `public static class C { [SqlSource.SqlSourceConverter] public static int F(long v) => (int)v; }`, emit it, reference the image from a second compilation, and find the attribute on `C.F` by `AttributeClass.ToDisplayString() == "SqlSource.SqlSourceConverterAttribute"`.
  - `AttributeConflictTests`: the theory or list of the types suppressed gains `SqlSourceConverterAttribute`, used in the friend project as `[SqlSource.SqlSourceConverter]` on a static method, with no CS0436.
- [ ] **Step 2: Run them; they fail** — `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~AttributeSourceTests|FullyQualifiedName~AttributeConflictTests"`.
- [ ] **Step 3: Add the class to `AttributeSource.Text`**, with a summary that says it marks a static method of one argument as a conversion from its argument's type to its return type that the generated query methods call, and why it is not `[Conditional]` (a library's converters must keep it in metadata); add the constant and its place in `GeneratedTypes`.  TD-0006 lists the type in its **Problem**.
- [ ] **Step 4: Run them; they pass.**  Run `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Begin 8.2: the converter attribute".

---

### Task 2: Converters in the project, `SQLSRC277` and `SQLSRC278`

**Files:**
- Create: `src/SqlSource/Generation/Converters/Converter.cs`, `ConverterReader.cs`, `ConverterSet.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (a stage method `FindConverters`, called after `TypeFiles`), `src/SqlSource/Generation/TrackingNames.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (sections `SQLSRC277`, `SQLSRC278`), `src/SqlSource/AGENTS.md` (`ConverterReader.cs` among the files that touch symbols)
- Test: `tests/SqlSource.Tests/Generator/ConverterDiagnosticsTests.cs` (new), `tests/SqlSource.Tests/Generation/Converters/ConverterSetTests.cs` (new)

**Interfaces:**
- Produces:
  - `internal sealed record Converter(string Method, string DisplayName, string ArgumentText, bool ArgumentIsValueType, string ReturnText, bool ReturnIsValueType, EquatableArray<string> WarningIds, string? Fault, ConverterOrigin Origin, LocationInfo? Location)`.  `Method` is the call target, `global::App.Email.FromDatabase`; `DisplayName` is `App.Email.FromDatabase(string)` as a message names it; `Fault` is the first fault's clause as the message words it (below), or null; `Location` is the method's name, null for one of a referenced assembly.
  - `internal enum ConverterOrigin { Project, MappedType }`.
  - `internal static class ConverterReader` with `public static Converter Read(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)` and `public static Converter Read(IMethodSymbol method, Compilation compilation, ConverterOrigin origin, Location? location)`, the one validation both sources use.
  - `internal sealed record ConverterSet(EquatableArray<Converter> Converters, EquatableArray<DiagnosticInfo> Diagnostics)` with `public static ConverterSet Of(EquatableArray<Converter> project, EquatableArray<Converter> mappedTypes)` and `public EquatableArray<Converter> Touching(string typeText)`.  `Converters` sorted ordinally by `ArgumentText`, `ReturnText`, `Method`; a `MappedType` converter whose edge (`ArgumentText`, `ReturnText`) a `Project` converter has is left out.  `Touching` gives those whose argument or return is the text, by search, in that order.
  - `SqlDiagnostics.NotAConverter` (`SQLSRC277`, title "Method marked as a converter is not one") and `SqlDiagnostics.DuplicateConverter` (`SQLSRC278`, title "Two converters convert between the same types").
  - `TrackingNames.ProjectConverters` (the attribute step), `TrackingNames.Converters` (the collected set; in this task over the project's alone, an empty array for the mapped types until Task 3).
- Faults, in this order, the first one the method's: `"it is not static"`; `"it is generic"`; `"it is in a generic type"`; `"it does not take exactly one argument"`; `"its argument is ref, out, in, params or optional"`; `"it returns nothing"` (void, `Task`, `ValueTask`, generic or not); `"its return type is annotated as nullable"` (a `?` reference or `Nullable<T>`); `"this project cannot call it"` (`!compilation.IsSymbolAccessibleWithin(method, compilation.Assembly)`, or a file-local containing type); `"it is marked [Obsolete] as an error"` (`MappedTypeWarnings.Add` false for the method or a containing type).  `WarningIds` from `MappedTypeWarnings.Add` over the method and its containing types, `MappedTypeWarnings.Distinct`.
- Messages: `SQLSRC277` "The method '{0}' is marked [SqlSourceConverter], but {1}.  A converter is a static method of one argument that returns the converted value." — `{0}` `DisplayName`, `{1}` the fault.  `SQLSRC278` "The converters '{0}' and '{1}' both convert '{2}' to '{3}'.  Keep one." — types as `PostgresTypeMap.AsWritten` writes them.  At a query (Task 4) `SQLSRC277` and `SQLSRC278` take the same arguments.

- [ ] **Step 1: Write the failing tests** in `ConverterDiagnosticsTests`, each a `GeneratorHarness.Run` at `OutputKind.Models` with a source of converters and no `.sql` file the converters need:
  - `Run_ValidConverters_ReportNothing`: a record struct `Email` with `FromDatabase(string)` and `ToDatabase(Email)`, and a static class with `ToUserId(long)` and `FromUserId(UserId)`.
  - `Run_MarkedMethodWithAFault_ReportsSQLSRC277AtIt` — a theory over each fault above, one method each, asserting the id, the location's line and the message's fault clause.
  - `Run_ObsoleteAsAWarning_ReportsNothing`.
  - `Run_TwoConvertersOfOneEdge_ReportSQLSRC278AtBoth`, and `Run_OneTypeToItself_IsNotADuplicate` (`static DateTime Kind(DateTime value)` alone).
  - `Run_ConverterUnderCodeGenWithoutQueries_ReportsSQLSRC277` (the same at `OutputKind.CodeGen`, proving it does not depend on the output).
  In `ConverterSetTests`: `Of_ProjectAndMappedTypeOfOneEdge_KeepsTheProjects`, `Of_Converters_AreSortedByArgumentThenReturn`, `Touching_Type_GivesTheEdgesFromAndToIt`.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.**  The attribute step is `ForAttributeWithMetadataName(AttributeSource.ConverterAttributeMetadataName, static (node, _) => node is MethodDeclarationSyntax, ConverterReader.Read)`, then `.Collect().Select` to a sorted `EquatableArray<Converter>`, combined with the mapped types' (empty here) into `ConverterSet.Of`.  `SQLSRC278` is reported at each of two `Project` converters of one edge that both have a `Location`; a `Converter` of the same `Method` and types is one converter (a partial method's two declarations).  One `RegisterSourceOutput` reports `ConverterSet.Diagnostics`.  Add the descriptors, the release rows and the two sections of `diagnostics.md` (what it means, each fault, how to fix).
- [ ] **Step 4: Run them; they pass.**  `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Find the project's converters, with SQLSRC277 and SQLSRC278".

---

### Task 3: Converters on the mapped types

**Files:**
- Modify: `src/SqlSource/Generation/Types/MappedTypeResolver.cs`
- Create: `src/SqlSource/Generation/Types/ResolvedTypes.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`, `src/SqlSource/Generation/TrackingNames.cs`
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypeResolverTests.cs`, `tests/SqlSource.Tests/Generator/CachingMappedTypeTests.cs`

**Interfaces:**
- Consumes: `ConverterReader.Read(IMethodSymbol, Compilation, ConverterOrigin.MappedType, null)`, `ConverterSet.Of`.
- Produces:
  - `internal sealed record ResolvedTypes(EquatableArray<MappedType> Types, EquatableArray<Converter> Converters)`.
  - `MappedTypeResolver.Resolve` returns `ResolvedTypes`.  For each found name, the named type it resolved (an array's element type, a generic's constructed type), when it is not of `compilation.Assembly`, gives each of its methods whose attributes have one of class `SqlSource.SqlSourceConverterAttribute` by `ToDisplayString()`, read by `ConverterReader`; a type named twice gives its converters once; sorted as `ConverterSet` sorts.  A type of the project's own assembly gives none, since the attribute step reads it.
  - The pipeline: `resolvedTypes` (the old `MappedTypes` step, renamed `TrackingNames.ResolvedTypes`), then `.Select(r => r.Types)` named `TrackingNames.MappedTypes` and `.Select(r => r.Converters)` named `TrackingNames.MappedTypeConverters`, which feeds `ConverterSet.Of`.

- [ ] **Step 1: Write the failing tests** in `MappedTypeResolverTests`:
  - `Resolve_TypeOfAReferenceWithConverters_GivesThem`: a library compiled with the attribute's text and `public readonly record struct Email(string Value) { [SqlSource.SqlSourceConverter] public static Email From(string v) => new(v); [SqlSource.SqlSourceConverter] public static string To(Email e) => e.Value; }`, referenced as an image; resolving `global::Lib.Email` gives two converters, `Origin` `MappedType`, no `Location`, `Method` `global::Lib.Email.From`.
  - `Resolve_ArrayOfSuchAType_GivesTheElementsConverters` (`global::Lib.Email[]`).
  - `Resolve_ConverterOfAReferenceWithAFault_KeepsTheFault` (an `internal` converter without `InternalsVisibleTo`: "this project cannot call it").
  - `Resolve_TypeOfTheProject_GivesNoConverters`.
  - `Resolve_TypeWithoutConverters_GivesNone`, and `Resolve_NoNames_GivesEmpty`.
  In `ConverterDiagnosticsTests` (Task 2's class): `Run_ProjectConverterOfAnEdgeAReferenceHas_ReportsNothing`.  `CachingMappedTypeTests` keeps its cases, now on `MappedTypes` after `ResolvedTypes`, and gains `Run_EditOfACommentOutsideTheMappedType_LeavesMappedTypeConvertersUnchanged`.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement**, the resolver keeping the found definition of `Use.Whole` beside its resolution for the converters, never in a result.
- [ ] **Step 4: Run them; they pass.**  `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Find converters on the mapped types".

---

### Task 4: Deciding each conversion, `SQLSRC279` and `SQLSRC280`

**Files:**
- Create: `src/SqlSource/Generation/Types/DatabaseType.cs`, `src/SqlSource/Generation/Methods/ConversionPlanner.cs`, `src/SqlSource/Generation/Methods/Conversion.cs`
- Modify: `src/SqlSource/Generation/Types/PropertyType.cs`, `QueryTyper.cs`, `PostgresTypeMap.cs`, `SqlServerTypeMap.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodsInput.cs`, `MethodPlanner.cs`, `QueryMethod.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (the converter set into `MethodsInput.Gather`)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (sections `SQLSRC279`, `SQLSRC280`; `SQLSRC277` and `SQLSRC278` say they are also reported at a query for a converter of a referenced assembly)
- Test: `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs` (new), `tests/SqlSource.Tests/Generation/Methods/ConversionPlannerTests.cs` (new), `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`, `TypeOverrideLevelTests.cs` (expectations gain `Database`), `tests/SqlSource.Tests/Generation/Types/PostgresTypeMapTests.cs`, `SqlServerTypeMapTests.cs`

**Interfaces:**
- Consumes: `ConverterSet.Touching`, `Converter`.
- Produces:
  - `internal sealed record DatabaseType(string? Text, bool IsValueType, bool ElementIsValueType)` — `B`: the base map's type of the property's database type under the project's driver with no override lookup (`PostgresTypeMap.Map(type, column)`, `SqlServerTypeMap.Map(type, column, null)`), `Text` null when that is not `Mapped`; `ElementIsValueType` the element's, for an array or a domain over one (the element mapped the same way), false otherwise.
  - `PropertyType` gains a last member `DatabaseType? Database = null`, set by `QueryTyper.TypeOf` exactly when `Place` is.  `ModelCollector.FindDifference` does not compare it (it changes no model's C#).
  - `PostgresTypeMap.Gives(string text)` and `SqlServerTypeMap.Gives(string text)`: whether any row of the map, under any driver column, or a kind's rule (`bool` and `BitArray` for bits, `string` for an enum), gives the text, with every trailing `[]` taken off it first.  A `T` is foreign when its engine's map does not give it.
  - `internal sealed record Conversion(string Method, string DatabaseText, bool DatabaseIsValueType, bool IsByElement, string? ElementText, bool ElementIsValueType)` — `Method` the converter's call target; `DatabaseText` `B` (for an array by element, `B` whole, `Y[]`); `ElementText` and `ElementIsValueType` the C# element `X` of `T` for one by element, null and false otherwise.
  - `MethodColumn` gains `Conversion? Read = null` and the property `ReadText => Read?.DatabaseText ?? TypeText`; `MethodParameter` gains `Conversion? Write = null`.
  - `MethodsInput` gains `EquatableArray<Converter> Converters`: `Gather` takes the `ConverterSet` and keeps, for each property of the type's files' `QueryModels` with `Type.Place` set, `Touching(T)` and, for `T` of the form `X[]`, `Touching(X)`, each converter once, in the set's order.
  - `internal static class ConversionPlanner` with `public static Conversion? Plan(PropertyType type, bool isRead, DatabaseEngine engine, EquatableArray<Converter> converters, string subject, LocationInfo at, List<DiagnosticInfo> problems, out bool blocked)`; `blocked` true when the query gets no method, with or without a problem added.
  - `SqlDiagnostics.ConverterTypeMismatch` (`SQLSRC279`, title "Converters do not convert from the database type") and `SqlDiagnostics.ConverterDirectionMissing` (`SQLSRC280`, title "Converter is missing for one direction").
- The decision, for `T` = `type.Text`, `B` = `type.Database`, read or write, in this order; "an edge" means one or more converters of that argument and return:
  1. `Place` null → null, nothing else.
  2. `B.Text` null: an edge touching `T`, or `X` when `T` is `X[]`, → `SQLSRC279` (no type to convert from); else null.
  3. The needed edge, `B → T` to read, `T → B` to write: use it (step 7).
  4. The other direction's edge → `SQLSRC280`.
  5. `T` is `X[]` and `B` is `Y[]`, and no converter touches `T`: the same steps 3, 4 and 6 for `X` and `Y`, an edge found giving a `Conversion` with `IsByElement`.
  6. `T` foreign and a converter touches it → `SQLSRC279`, naming the types its converters take and `B`.
  7. Using an edge: two or more converters of it → blocked, with `SQLSRC278` at the query when none is of the project; a converter with a `Fault` → blocked, with `SQLSRC277` at the query when it is of a mapped type; otherwise the `Conversion`.
  Anything else → null, read and bound as 8.1 does it.
- Messages: `SQLSRC279` "The {0} has type '{1}', whose converters convert from and to {2}, and not from and to '{3}', the type the database type '{4}' is read as.  Add a converter between '{1}' and '{3}'." — `{0}` the subject (`column 'id'`), `{2}` the other types as `'int'`, `'int' and 'short'`; its no-`B` form "The {0} has type '{1}', which has converters, but the database type '{2}' has no C# type to convert from.  Remove the type override, or the converters of '{1}'."  `SQLSRC280` "The {0} has type '{1}', which has a converter {2} '{3}' but none {4} it, which a {5} needs.  Add a static method marked [SqlSourceConverter] that takes '{6}' and returns '{7}'." — `{2}`/`{4}` "from"/"to", `{5}` "column" or "parameter".  Types as `PostgresTypeMap.AsWritten` writes them.
- `MethodPlanner.TryReadInput` and `TryReadOutput` call the planner for each property, with `query.NameLocation`; every problem is added to the query's, and any problem or block leaves it no method.

- [ ] **Step 1: Write the failing tests.**
  - `QueryTypesTests.Type_ColumnOfAnOverriddenType_HasItsBaseMapType`: `public.email=MyApp.Email` over a domain of `text` gives `Database` `("string", false, false)`; `timestamptz=MyApp.When` on Npgsql 10 under `timestamptz=System.DateTimeOffset` at the property and the marker on the domain gives `"global::System.DateTime"`; `public.email[]`'s element override gives `("string[]", false, false)`; a composite gives `Text` null; a base-map column gives `Database` null.  Update the existing expectations with a `Place` to carry their `Database`.
  - `PostgresTypeMapTests.Gives_TypesOfTheMap_AreGiven` (`long`, `string`, `global::System.DateOnly`, `global::System.DateTime`, `int[]`, `global::NpgsqlTypes.NpgsqlRange<int>`) and `Gives_ForeignTypes_AreNot` (`global::MyApp.Email`, `global::MyApp.Email[]`); the same for SQL Server (`global::Microsoft.Data.SqlTypes.SqlVector<float>` given).
  - `ConversionPlannerTests`, one case for each line of the decision, with a hand-built `PropertyType` and converters: `Plan_ReadWithTheNeededEdge_GivesItsConversion`; `Plan_ReadWithOnlyTheWriteEdge_ReportsSQLSRC280`; `Plan_WriteWithOnlyTheReadEdge_ReportsSQLSRC280`; `Plan_LongColumnWithAnIntConverter_ReportsSQLSRC279` (the spec's case, asserting `'int'` and `'long'` and `int8` in the message); `Plan_ForeignTypeWithoutConverters_GivesNull`; `Plan_NotForeignTypeWithConvertersOfAnotherType_GivesNull` (`DateTimeOffset` over `timestamptz` with `DateTimeOffset`↔`MyApp.When`); `Plan_NoPlace_GivesNull`; `Plan_NoBaseTypeWithAConverter_ReportsSQLSRC279`; `Plan_OneTypeToItself_GivesItForBothDirections`; `Plan_ArrayByElement_GivesAnElementConversion`; `Plan_ArrayOverrideWithItsOwnEdge_DoesNotUseTheElements`; `Plan_DoubledEdgeOfTheProject_IsBlockedSilently`; `Plan_DoubledEdgeOfAReference_ReportsSQLSRC278`; `Plan_FaultOfTheProject_IsBlockedSilently`; `Plan_FaultOfAReference_ReportsSQLSRC277`.
  - `ConverterMethodTests` (driver tests, `MethodRuns.Run` at `CodeGen` with a `-- type-override:` marker): `Run_ColumnWithoutTheReadConverter_ReportsSQLSRC280AndGivesNoMethod` (the query has no method in `MethodRuns.MethodsFile` and its models are generated), `Run_ParameterWithoutTheWriteConverter_ReportsSQLSRC280`, `Run_BigintColumnWithAnIntConverter_ReportsSQLSRC279`, `Run_ModelsProjectWithConverters_ReportsNoQueryDiagnostic` (the same source at `OutputKind.Models`), `Run_ProjectWithoutConverters_GeneratesAs81` (an override with no converter: the method's text is unchanged, compared with the text of the same run before this task, pinned as a literal), `Run_ReferencedConverterWithAFault_ReportsSQLSRC277AtTheQuery`.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement** the typer's `DatabaseType`, the maps' `Gives` (a set built once from the rows), the slice in `MethodsInput.Gather` (a search of the set for each text, so a type without overrides costs a pass over its properties and no search), the planner and its wiring into `MethodPlanner`, the descriptors, release rows and sections.  The emitted code is not changed yet: `Read` and `Write` are set and unused.
- [ ] **Step 4: Run them; they pass.**  `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Decide each column's and parameter's converter, with SQLSRC279 and SQLSRC280".

---

### Task 5: Reading and binding through a converter

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`, `ParameterBinder.cs`, `MethodEmitter.cs`
- Modify: `src/SqlSource/Generation/Methods/TypeMethods.cs` (`EquatableArray<string> WarningIds`), `MethodPlanner.cs` (fills it)
- Test: `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs`, `ConverterCompileTests.cs` (new), `GeneratedSourceMethodTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs`

**Interfaces:**
- Consumes: `MethodColumn.Read`, `MethodColumn.ReadText`, `MethodParameter.Write`, `Conversion`.
- Produces:
  - `TypeMethods.WarningIds`: the ids of the warnings of every converter its methods call and every property type they read or bind, ordinal and distinct; `MethodEmitter` writes `#pragma warning disable <ids>` after the header when it is not empty.
  - `ParameterBinder.Append` keeps its signature.  `AppendByValue` writes `.Value =` from a value expression that the converted case builds: for `Write` set, `parameters.X is { } <value> ? (object)<Method>(<value>) : global::System.DBNull.Value` when the property is nullable or of a reference type, and `<Method>(parameters.X)` otherwise; `<value>` from `LocalNames` as `value`.  8.3 and 8.4 bind `Write.DatabaseText` when it is set.
- The read, for a column with `Read` set and `IsByElement` false: `<Method>(reader.GetFieldValue<B>(i))`; nullable `reader.IsDBNull(i) ? default(T?) : <Method>(reader.GetFieldValue<B>(i))`; not-null with `ReadText` the vector type, the local and its `IsNull` check as 8.1 writes them, then `<Method>(<local>)` in the row.

- [ ] **Step 1: Write the failing tests** in `ConverterMethodTests`, each pinning the generated lines of one method by `ShouldContain`:
  - `Run_NotNullColumnOfAValueType_ReadsTheBaseTypeAndConverts`: `global::App.UserId` over `int8` → `global::App.Conversions.ToUserId(reader.GetFieldValue<long>(0))`.
  - `Run_NullableColumnOfAValueType_ChecksForNullFirst` → `reader.IsDBNull(0) ? default(global::App.Email?) : global::App.Email.FromDatabase(reader.GetFieldValue<string>(0))`.
  - `Run_NullableColumnOfAReferenceType_ChecksForNullFirst`.
  - `Run_NotNullParameterOfAValueType_ConvertsTheValue` → `parameter.Value = global::App.Conversions.FromUserId(parameters.Id);`.
  - `Run_NullableParameter_SendsDBNullForNull` and `Run_NotNullParameterOfAReferenceType_SendsDBNullForNull` → `parameter.Value = parameters.Email is { } value ? (object)global::App.Email.ToDatabase(value) : global::System.DBNull.Value;`.
  - `Run_OneTypeToItself_ConvertsBothWays` (`timestamp=System.DateTime`, a `DateTime Kind(DateTime)`).
  - `RunSqlServer_NotNullVectorColumnWithAConverter_ChecksIsNullBeforeConverting` (the local's `IsNull` check, then the converter of the local in the row).
  - `Run_ObsoleteConverter_DisablesItsWarning` (`#pragma warning disable CS0618`, and `run.GeneratedCodeWarnings` empty), and `Run_ObsoleteMappedTypeUnderCodeGen_DisablesItsWarning` (no converter; fails before this task with CS0618, the 8.1 gap).
  - `ConverterCompileTests.Run_ConvertedQueries_CompileWithNoWarning` — a theory over Npgsql 8, 9, 10 and SqlClient 6.1, 7, as `MethodCompileTests` runs: a value-type and a reference-type converted column and parameter, nullable and not, of each shape; `run.ShouldBeClean()` and `GeneratedCodeWarnings` empty.
  - The awkward-name theories gain the token `value`.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement** in `QueryMethodWriter.AppendColumn` and `Locals` (the vector local keyed on `ReadText`), `AppendByValue`'s value expression, the warning ids and the pragma.
- [ ] **Step 4: Run them; they pass.**  `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Read and bind through converters".

---

### Task 6: Arrays

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`, `ParameterBinder.cs`
- Test: `tests/SqlSource.Tests/Generator/ConverterMethodTests.cs`, `ConverterCompileTests.cs`, the two awkward-name theories

**Interfaces:**
- Consumes: `Conversion.IsByElement`, `ElementText`, `ElementIsValueType`, `DatabaseIsValueType` (of `Y`, from `DatabaseType.ElementIsValueType`; Task 4 fills it so for an element conversion).
- The read of a column by element, `X` value type, `Y` its database element:
  `global::System.Array.ConvertAll<Y?, X>(reader.GetFieldValue<Y?[]>(i), static <element> => <element> is { } <item> ? <Method>(<item>) : throw new global::System.InvalidCastException("Column '<name>' holds a null element."))` — `Y?` only when `Y` is a value type, so that Npgsql gives the null to the method, which names the column; for `X` a reference type the null arm is `null!` and the converter is not called.  A nullable column puts `reader.IsDBNull(i) ? default(X[]?) : ` before it.  `<element>` and `<item>` from `LocalNames` as `element` and `item`.
- The write by element: `parameters.X is { } <value> ? (object)global::System.Array.ConvertAll<X, Y>(<value>, static <element> => <Method>(<element>)) : global::System.DBNull.Value` for `X` a value type; for `X` a reference type, `ConvertAll<X?, Y?>` and `<element> is { } <item> ? <Method>(<item>) : null`, a null element sent as a `NULL` element.
- An array override with its own converter (`IsByElement` false) is Task 5's scalar read and write.

- [ ] **Step 1: Write the failing tests**:
  - `Run_ArrayOfAnElementsOverride_ConvertsEachElement` (`public.email=App.Email`, a value type over `text`, column `email[]` not null) pinning the `ConvertAll` read and its throw message `Column 'emails' holds a null element.`.
  - `Run_ArrayOfAReferenceElement_KeepsANullElement` (the `null!` arm).
  - `Run_ArrayOfAValueTypeDatabaseElement_ReadsItNullable` (`App.UserId` over a domain of `int8`, `GetFieldValue<long?[]>`).
  - `Run_ArrayParameter_ConvertsEachElement`, value and reference elements.
  - `Run_ArrayOverrideWithItsOwnConverter_ConvertsTheWhole` (`public.email[]=App.Emails`, `App.Emails`↔`string[]`).
  - `ConverterCompileTests` gains an array column and parameter of each kind.
  - The awkward-name theories gain `element` and `item`.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run them; they pass.**  `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** "Convert PostgreSQL arrays element by element".

---

### Task 7: Caching

**Files:**
- Test: `tests/SqlSource.Tests/Generator/CachingConverterTests.cs` (new), `CachingMethodTests.cs`
- Modify (if a case shows a step running too often): the steps of Tasks 2 to 4

**Interfaces:**
- Consumes: `CachingScene`, `StepReasons`, `TrackingNames.ProjectConverters`, `Converters`, `ResolvedTypes`, `MappedTypeConverters`, `MethodsInput`, `TypeMethods`.

- [ ] **Step 1: Write the tests**, on a scene with two types, one whose file uses an override with converters and one with no override:
  - `Run_EditOfAConvertersBody_LeavesEverythingUnchanged` (the record is equal: `ProjectConverters` Unchanged after its transform).
  - `Run_EditOfAConvertersSignature_RunsTheMethodsOfTheTypeThatUsesIt`: `Converters` Modified, `MethodsInput` and `TypeMethods` Modified for that type and Unchanged for the other, `TypedFile`, `FileModels`, `Models` and `ModelOutput` Unchanged.
  - `Run_EditOfATypeWithNoConverter_RunsNothingOfTheMethods`: `Converters` Unchanged (or Cached), `MethodsInput` Unchanged for both types.
  - `Run_NewConverterOfAnUnusedType_LeavesMethodsInputUnchanged`.
  - `Run_EditOfAConverterAboveAnother_MovesOnlyTheirDiagnostics` (a line added above the methods: the `Location`s change, `MethodsInput` stays Unchanged, since the slice carries no `Location`).  If it fails, the slice in `MethodsInput` drops `Location` (`converter with { Location = null }`) and this plan's Task 4 interface says so.
- [ ] **Step 2: Run them**; make each pass, by the slice or a `Select` that drops what a later step does not read, never by weakening a case.
- [ ] **Step 3: `./pre-commit-validation.sh`.**
- [ ] **Step 4: Commit** "Pin what an edit to a converter runs again".

---

### Task 8: The documents; 8.2 done

**Files:**
- Modify: `docs/manual/settings.md` (the type override's section: converters, where they are found, the database side as the base map's type, null, a library's converters and the project's replacing them, a driver update that can ask for another converter), `docs/manual/generator.md` (Query methods: a converted type's read and bind; its known issue of a mapped type the driver does not read now names converters), `docs/manual/recipes.md` (a strongly-typed id over `bigint`; in each Dapper recipe a three-line `TypeHandler` that calls the converters), `docs/manual/postgresql.md` and `docs/manual/sqlserver.md` (Types: a mapped type the driver does not read takes a converter), `docs/manual/README.md` ("What works today": converters), `README.md` (FAQ "I got an `InvalidCastException`; what does it mean and how do I fix it?": teach the driver the type or add a converter, link to the manual; absolute links)
- Modify: `docs/architecture/generator.md` (the converters' steps: the attribute step, the resolver's converters, `ConverterSet`, `DatabaseType`, the slice, `ConversionPlanner`, the emitted read and write), `src/SqlSource/AGENTS.md` (`SQLSRC200` to `SQLSRC280` taken; a rule that a converter's slice reaches `MethodsInput` and never `TypeOutput` or the models)
- Modify: `docs/deferred/D-0003-custom-type-conversion.md` (rewritten to the types `SQLSRC232` refuses, which have no base-map type), `docs/deferred/README.md` (`D-0003` row; rows of `D-0007` and `D-0008`; `Next id: D-0009`), create `docs/deferred/D-0007-converters-by-convention.md`, `docs/deferred/D-0008-converter-per-file-or-query.md`, each with the five sections of `docs/deferred/AGENTS.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.2: `Done`)
- Test: `tests/SqlSource.Tests/Documentation/` (links and samples), `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

- [ ] **Step 1: Write the documents.**  User pages describe effects, never internals: what a user writes (the attribute on a static method), what they get, what they see (each diagnostic by its id's link).  Every `sql` sample names its dialect.
- [ ] **Step 2: `grep -rn "D-0003\|D-0007\|D-0008" --exclude-dir=bin --exclude-dir=obj docs src tests README.md`** finds no stale reference.
- [ ] **Step 3: `./pre-commit-validation.sh`.**
- [ ] **Step 4: Commit** "Document converters; 8.2 done".
