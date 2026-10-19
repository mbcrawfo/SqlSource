# Query generation, phase 5.3: models - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Emit the input and output models of every query whose output is `Models` or `CodeGen`, once per full name, with the shared-name rule, table models and the model settings in effect.

**Architecture:** A per-file step turns each usable entry and its C# types into the properties of the query's two models and reports the name errors once per file.  A per-type step resolves each model's name, namespace, accessibility, shape and order into a value-equal request.  The requests are collected, grouped by full name, checked against each other and against the compilation's types, then fanned back out with `SelectMany`, one node per model, so that an edit re-emits only the models whose record changed.

**Tech Stack:** C# source generator on `netstandard2.0`, Roslyn 4.8.0 floor, xunit v3 with Shouldly.

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-3-models-design.md`](../specs/2026-10-18-query-generation-phase-5-3-models-design.md), within the [epic outline](../specs/2026-10-07-query-generation-epic-design.md)'s Models, Types in C# and Phase 5 sections.

## Global Constraints

- Every rule of the root `AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md` and the `AGENTS.md` of each `docs/` folder touched.  They win over this plan.
- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  One commit per task.
- Generated code compiles with no warning at the harness's highest warning level, under `#nullable enable`, with documentation diagnosed.  Every model test asserts `run.GeneratedCodeWarnings.ShouldBeEmpty()` and `run.CompilationErrors.ShouldBeEmpty()`.
- Generated code uses nothing newer than C# 12, and the generator nothing newer than Roslyn 4.8.0.
- No `Collect()` without a `Select` that gives an `EquatableArray<T>` after it.  No Roslyn object between steps.  Only `TargetTypeReader`, the `SupportedFramework` step, `NpgsqlProbe`, `MappedTypeResolver` and the new `ModelNameChecker` touch symbols.
- Nothing a user writes may make the generator throw.
- Input model properties are PascalCase and nothing more for Dapper: no `ToParameters()` (deferred as D-0004, already recorded).
- Ids: `SQLSRC253` to `SQLSRC259` as the spec numbers them, and `SQLSRC251` extended.  Each new descriptor touches its `All`, `AnalyzerReleases.Unshipped.md` and its section of `docs/manual/diagnostics.md` in the commit that adds it.
- Every test that runs the generator states its output; model tests use `OutputKind.Models` under the dialect `postgres`.
- Every test class is `sealed`, every test in `tests/SqlSource.Tests/Generator/` starts with the shared line and ends in `Tests.cs`, names are `Method_Scenario_Outcome`.

## Where this plan follows the code rather than the spec

The spec says what 5.1 and 5.2 would leave.  What they left differs in these ways, and the plan follows the code:

1. **The architecture page's placeholder** is `### Models` under `## Not built yet` of `docs/architecture/generator.md`, beside an existing `### Types` section; the spec calls it "Type map and models".  Task 7 replaces `### Models`.
2. **`PropertyType` has no place.**  The second case of `SQLSRC251` names the place that wrote the override's entry, as the first does, so `PropertyType` gains `string? Place`, the place as `SQLSRC250` to `SQLSRC252` word it, null for the base map.  5.2's tests that build a mapped `PropertyType` change with it.
3. **`SQLSRC251`'s message has one fixed ending.**  A descriptor has one message format, so its format becomes `The type override maps '{1}' to '{2}' for {0}, in {3}, and {4}.`, `{4}` being the old ending for the first case, word for word, or the second case's.  The first case's message is unchanged.
4. **The model's own name** (the `CS0542` row of `SQLSRC256`) depends on the model's name, which the type's suffix settings decide, so it is checked once per model in the collection step, not per file.  The other rows of the names table are per file, as the spec says.
5. **The typing step is split.**  `QueryTyper.Type` runs once in a step that keeps its input beside its output (`TypedFile`), so that the per-file models step reads the entries and the parsed file without typing again; `FileTypes` is a small `Select` of it and keeps its tracking name.
6. **Hint names.**  A model's hint name ends in `-Model.g.cs`, which no type's can, since a type's has `-` only before its arity's digits.  So `HintName.FindAmbiguous` runs over the models' hint names alone, in the collection step, and the types' step is untouched.
7. **The header.**  `TypeEmitter`'s header carries a pragma for `CS0108` and `CS0114` that a model never needs, so `ModelEmitter` writes its own: `// <auto-generated/>`, `#nullable enable`, and `#pragma warning disable CS0612, CS0618` only when a property's type is obsolete.
8. **Accessibility** is `TargetType.IsPublic`, true when the type and every type it is nested in are declared `public`; a model is `public` or `internal`, so nothing finer is needed.
9. **The marker positions.**  `SqlQuery.InputModelName` and `OutputModelName` become `ModelMarker? InputModel` and `OutputModel`, each the name and the marker's location; `SqlBlock` the same with a `TextSpan`.  The record is allocated only for a query that has the marker, so the parser's allocation budget holds.
10. **Ids.**  5.2 ended at `SQLSRC252`, as the spec assumed: 5.3 takes `SQLSRC253` to `SQLSRC259` in the spec's order.

## Review Focus

1. **A query claimed by two types whose outputs differ**, `Sql` for one and `Models` for the other: one set of models, in the `Models` type's namespace, and nothing for the other.  Test in Task 3.
2. **Two model full names that differ only by case**, `UserDto` and `UserDTO`: both compile, since C# tells them apart, and their hint names are made unique.  Test in Task 4.
3. **Non-ASCII column names**, `café` and `名前`: valid identifiers after PascalCase (`Café`, `名前`), not errors.  Test in Task 2.
4. **A type excluded by `SQLSRC008` or `SQLSRC009`, or with a diagnostic of its own**, gets no models, while another type claiming the same file still does.  Test in Task 3.
5. **The global namespace with a class model, sorting and an obsolete mapped type** at once: the pragma, `= default!` and the order all compile without a warning.  Test in Task 3.

---

## File structure

New folder `src/SqlSource/Generation/Models/` (each a record or a static class, one per file):

| File | Holds |
|----|----|
| `ModelProperty.cs` | One property: name, `PropertyType`, nullability, documentation XML, the subject a message names |
| `QueryModels.cs` | One query's two property lists, its markers, summary, matched table and where it is |
| `FileModels.cs` | The `QueryModels` of one file and its name diagnostics |
| `PropertyNames.cs` | PascalCase, the identifier check, the reserved names |
| `ModelReader.cs` | `TypedFile` to `FileModels`, with `SQLSRC253`-`256` and `SQLSRC259` |
| `ModelRole.cs` | `Input`, `Output` |
| `ModelSource.cs` | The query a request comes from, and its order |
| `ModelRequest.cs` | What one type needs of one model |
| `TypeModels.cs` | `TypeQueries` + `FileModels` to the type's requests |
| `ModelGroup.cs` | The requests of one full name, in order |
| `TakenName.cs`, `TakenReason.cs` | A full name the compilation takes, and how |
| `ModelNameChecker.cs` | The compilation lookup of the collected names |
| `Model.cs` | Everything one model's file is written from, and its hint name |
| `ModelSet.cs` | The models to emit and the collection's diagnostics |
| `ModelCollector.cs` | Grouping, the shared-name rule, the per-model checks |

Elsewhere: `Generation/Types/TypedFile.cs`, `Generation/ModelMarker.cs`, `Parsing/SqlModelName.cs`, `Generation/Emitting/ModelEmitter.cs`.  Tests: `tests/SqlSource.Tests/Generator/ModelTests.cs`, `ModelNameTests.cs`, `SharedModelTests.cs`, `TableModelTests.cs`, cases added to `CachingTests.cs`; unit tests `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`.

---

### Task 1: The marker positions, the warning level, and the epic's row

**Files:**
- Create: `src/SqlSource/Parsing/SqlModelName.cs`, `src/SqlSource/Generation/ModelMarker.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerScope.cs`, `SqlFileParser.cs`, `SqlBlock.cs`, `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`, `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (5.3 row: `In progress`), `docs/architecture/generator.md` where it names the two members
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`

**Interfaces:**
- Produces: `internal sealed record SqlModelName(string Name, TextSpan Span)` (the span is the whole marker comment, as `InputModelWithoutParameters` uses); `SqlBlock.InputModel`/`OutputModel` of type `SqlModelName?` replacing the two names.  `internal sealed record ModelMarker(string Name, LocationInfo Location)`; `SqlQuery.InputModel`/`OutputModel` of type `ModelMarker?` replacing `InputModelName`/`OutputModelName`.
- Produces: `GeneratorHarness.CreateCompilation` compiles with `warningLevel: 9999`.

- [ ] **Step 1: Failing parser test** `Parse_ModelMarkers_KeepTheirSpans`: a query with `-- input-model: FindArgs` and `-- output-model: App.Shared.UserRow` on separate lines; assert `block.InputModel.ShouldBe(new SqlModelName("FindArgs", <span of the whole first marker>))` and the same for the output marker.  Update the two existing assertions on `InputModelName`/`OutputModelName` to `.InputModel!.Name`/`.OutputModel!.Name`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlFileParserTests"`; expect a compile failure.
- [ ] **Step 3: Implement.**  `SqlMarkerScope.OutputModel` becomes `(string Name, SqlMarker Marker)?` set like `InputModel`, with the first marker that set the value; `SqlFileParser.ReadBlock` builds `SqlModelName` from each, only when present.  `SqlFileReader` maps each to `ModelMarker` with `LocationInfo.From(file.Path, text, span)`.  Docs of the members say "the marker in effect, with where it is".
- [ ] **Step 4: Highest warning level.**  Add `warningLevel: 9999` to the `CSharpCompilationOptions` in `CreateCompilation`, with a comment that every warning wave is reported, as `CS8981` is only from level 7.  Add `Run_GeneratedCode_IsCompiledAtTheHighestWarningLevel` to `GeneratedSourceTests`: a source declaring `internal class lowercase {}` (user code) built with `GeneratorHarness.CreateCompilation`; assert its diagnostics contain `CS8981`.
- [ ] **Step 5: Run the whole test suite** (`dotnet test --solution SqlSource.slnx`); fix every test that the new warning level breaks, in the test, never by lowering the level.
- [ ] **Step 6: Epic row.**  5.3's Status cell `Specified` becomes `In progress`.
- [ ] **Step 7: Validate and commit.**  `./pre-commit-validation.sh`, then `git commit -m "Keep where the two model markers are, and compile tests at the highest warning level"`.

---

### Task 2: Each query's properties, their names and nullability (`SQLSRC253`-`256`, `SQLSRC259`)

**Files:**
- Create: `src/SqlSource/Generation/Types/TypedFile.cs`, `Generation/Models/ModelProperty.cs`, `QueryModels.cs`, `FileModels.cs`, `PropertyNames.cs`, `ModelReader.cs`
- Modify: `src/SqlSource/Generation/Types/PropertyType.cs` (add `Place`), `QueryTyper.cs`, `src/SqlSource/SqlSourceGenerator.cs`, `Generation/TrackingNames.cs` (`TypedFile`, `FileModels`), `Diagnostics/SqlDiagnostics.cs`, `AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (index rows and sections 253-256, 259), `src/SqlSource/AGENTS.md` (`SQLSRC200` to `SQLSRC252` → `SQLSRC259`), `docs/architecture/AGENTS.md` (the `Generation` row gains `Models/`)
- Test: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`, `tests/SqlSource.Tests/Generator/ModelNameTests.cs`; existing `QueryTypesTests`, `TypeOverrideLevelTests`, `TypeDiagnosticsTests` for `Place`

**Interfaces:**
- Consumes: `FileTypesInput` (`Check`, `File`), `FileTypes`, `QueryTypes`, `PropertyType`, `SqlQuery.InputModel`/`OutputModel` (Task 1).
- Produces:
  - `PropertyType(string Text, bool IsValueType, bool IsPublic, bool IsObsolete, string Key, string? Place)`; `Place` is `QueryTyper.OverrideLookup.PlaceOf(entry.Place)` for a mapped type of the override, null for the base map.
  - `internal sealed record TypedFile(FileTypesInput Input, FileTypes Types)`.
  - `internal sealed record ModelProperty(string Name, PropertyType Type, bool IsNullable, string Documentation, string Subject)`.  `Documentation` is escaped XML; `Subject` is `column 'id'` or `parameter 'id'`.  `bool IsSameShape(ModelProperty other)`: ordinal name, ordinal `Type.Text`, nullability.
  - `internal sealed record QueryModels(string Name, int Index, LocationInfo NameLocation, string? Summary, ModelMarker? InputMarker, ModelMarker? OutputMarker, EquatableArray<ModelProperty>? Input, EquatableArray<ModelProperty>? Output, SidecarTable? Table)`: `Input` null without parameters, `Output` null without rows, `Index` the query's index in the file, `Table` the entry's `MatchesTable`.
  - `internal sealed record FileModels(string NormalizedPath, string FileName, EquatableArray<QueryModels> Queries, EquatableArray<DiagnosticInfo> Diagnostics)`.
  - `PropertyNames.PascalCase(string name) : string`, `PropertyNames.IsIdentifier(string name) : bool`, `PropertyNames.IsReserved(string name) : bool`, `PropertyNames.FromTable(SidecarTable table) : string?` (used by Task 5; may be added there).
  - `ModelReader.Read(TypedFile file) : FileModels`.
  - Descriptors `ColumnHasNoName` (253), `PropertyNameNotIdentifier` (254), `PropertyNameRepeated` (255), `PropertyNameTaken` (256), `OutputModelWithoutRows` (259).

**The rules, exactly** (spec, Properties):

- PascalCase: drop one trailing `!` or `?` from a column's name (it sets nullability); split at `_`; drop empty parts; upper-case each part's first character with `char.ToUpperInvariant`; join.  `created_at`→`CreatedAt`, `userId`→`UserId`, `ID`→`ID`, `api_v2_url`→`ApiV2Url`.  A parameter's name is the sidecar's bare name.
- Per property, the first that applies: `?column?` (a column) → 253; not `SqlIdentifier.IsValid` → 254; equal ignoring case to an earlier property's name of the same model → 255; one of `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct`, `Clone` (ordinal) → 256.  Confirm the list against the compiler in Task 3's tests (every name a record or a class model would warn or err on); add any name the compiler finds.
- Nullability: a column ending `?` nullable, `!` not, otherwise nullable unless `Nullable == false`.  A parameter nullable only when `Nullable == true`.
- `SQLSRC259`: a query with an `OutputMarker` whose entry has no rows, at the marker.
- A query with any error has no `QueryModels`.  All its errors are reported, each at the query's name (259 at its marker).
- Documentation of a property, escaped with `XmlDocWriter.Escape`: `The column <c>{name as written}</c>, <c>{type.Name}</c>, from <c>{schema}.{table}.{column}</c>.` without the origin clause when there is none or a part of it is null, then `  An identity column.` and `  A computed column.` when `true`.  A parameter: `The parameter <c>{name}</c>, <c>{type.Name}</c>.`  A parameter whose type is null has no `QueryTypes`, so never reaches here.

**Messages** (`{n}` as listed; help sentences end without a period inside the argument):

| Id | Title | Format |
|----|----|----|
| 253 | `Column has no name` | `The column at position {0} of the query '{1}' has no name.  Give it an alias.` |
| 254 | `Name is not an identifier after PascalCase` | `The {0} gives the property name '{1}', which is not a C# identifier.  {2}.` (`Give it an alias` for a column, `Rename it` for a parameter) |
| 255 | `Two names give one property name` | `The {0} and the {1} give one property name, '{2}', ignoring case.  {3}.` (subjects with ` at position N`, 1-based; `Give one of them an alias` / `Rename one of them`) |
| 256 | `Property name is taken` | `The {0} gives the property name '{1}', which {2}.  {3}.` (`a record or object already has` here; `is the name of its model` in Task 4) |
| 259 | `Output model on a query without rows` | `The query '{0}' names an output model, and its statement returns no rows.  Remove the -- output-model: marker.` |

- [ ] **Step 1: Unit tests** `PropertyNamesTests`: `PascalCase_Name_IsPascalCase` theory over the four spec examples plus `café`→`Café`, `名前`→`名前`, `_a__b_`→`AB`, `2fa`→`2fa`, `___`→empty; `IsIdentifier` theory (`2fa` false, empty false, `Café` true); `IsReserved_EachName_IsTrue` over the eleven names, `IsReserved_Name_IsFalse` for `Id`.
- [ ] **Step 2: Generator tests** `ModelNameTests` (Models, postgres, entries built as `QueryTypesTests` builds them), each reading `StepOutputs.Single<FileModels>(run, TrackingNames.FileModels)`:
  - `Run_ColumnNames_ArePascalCase` (the four examples as columns), `Run_ParameterName_IsItsBareName`.
  - `Run_ColumnNamedQuestionColumn_IsSqlsrc253`, `Run_ColumnNotAnIdentifier_IsSqlsrc254`, `Run_ParameterNotAnIdentifier_IsSqlsrc254WithRename`, `Run_TwoColumnsOneName_IsSqlsrc255` (`id`, `id`), `Run_UserIdBesideUserId_IsSqlsrc255` (`user_id`, `userId`), `Run_TwoParametersOneName_IsSqlsrc255` (`a_b`, `aB`), `Run_ReservedName_IsSqlsrc256` (theory over the eleven), `Run_OutputModelOnNoRows_IsSqlsrc259AtTheMarker`; each asserts the exact message and location, and that the query has no `QueryModels`.
  - `Run_ColumnSuffixes_SetNullability`: `a!` nullable column → not nullable, name `A`; `b?` not-null column → nullable, name `B`; `c` with `nullable: null` → nullable; `d` with `false` → not.  `Run_ParameterNullability_IsTrueOnly`: `true`, `false`, `null`.
  - `Run_PropertyDocumentation_NamesTypeOriginAndFlags`: a column with origin and `identity: true`, a computed one, one without origin, a parameter; assert each `Documentation` exactly.
  - `Run_NoParameters_HasNoInputModel`, `Run_NoRows_HasNoOutputModel`, `Run_NoColumns_HasAnEmptyOutputModel`.
  - Type-error query (an unknown type, `SQLSRC246`) gives no `QueryModels` and no name diagnostic.
- [ ] **Step 3: Run** the two classes; expect compile failures.
- [ ] **Step 4: Implement** `PropertyType.Place`, `TypedFile`, the records, `PropertyNames`, `ModelReader`, the descriptors, the release rows, the diagnostics sections (each section says what to write instead: an alias, a new name, removing the marker; with a `See:` line to `generator.md#models` once Task 7 writes it — add the See lines in Task 7).  In `SqlSourceGenerator`: `typedFiles = fileTypesInputs.Select(input => new TypedFile(input, QueryTyper.Type(input))).WithTrackingName(TypedFile)`; `fileTypes = typedFiles.Select(file => file.Types).WithTrackingName(FileTypes)`; `fileModels = typedFiles.Select(ModelReader.Read).WithTrackingName(FileModels)`, and a `RegisterSourceOutput(fileModels, ...)` reporting its diagnostics, with a comment that a change to the models alone reports the types of no file again.  Update `CachingTests` expectations only where the split changes a reason, and say why in the commit.
- [ ] **Step 5: Run** `dotnet test --solution SqlSource.slnx`; all pass.
- [ ] **Step 6: Validate and commit** `"Give each query's models their properties, names and nullability"`.

---

### Task 3: Emitting one type's models

**Files:**
- Create: `Generation/Models/ModelRole.cs`, `ModelSource.cs`, `ModelRequest.cs`, `TypeModels.cs`, `ModelGroup.cs`, `Model.cs`, `ModelSet.cs`, `ModelCollector.cs`, `Generation/Emitting/ModelEmitter.cs`
- Modify: `Generation/TargetType.cs` (`IsPublic`), `TargetTypeReader.cs`, `Generation/Emitting/TypeEmitter.cs` (extract `FindUsableFiles`), `Generation/Emitting/HintName.cs` (`ForModel`), `SqlSourceGenerator.cs`, `TrackingNames.cs` (`AllFileModels`, `TypeModels`, `ModelGroups`, `Models`, `ModelOutput`)
- Test: `tests/SqlSource.Tests/Generator/ModelTests.cs`

**Interfaces:**
- Consumes: `FileModels`, `QueryModels`, `ModelProperty` (Task 2); `TypeQueries`, `QuerySettings.Resolve`.
- Produces:
  - `TargetType.IsPublic : bool` (positional, after `Settings`): the type and every containing type declared `public`.
  - `TypeEmitter.FindUsableFiles(TargetType type, EquatableArray<ParsedSqlFile> files, ImmutableArray<DiagnosticInfo>.Builder? diagnostics) : bool[]`, used by `SelectMembers` and `TypeModels`.
  - `enum ModelRole { Input, Output }`.
  - `record ModelSource(string NormalizedPath, string FileName, int QueryIndex, string QueryName, string ClaimingType, LocationInfo NameLocation, LocationInfo? MarkerLocation, string? Summary)`; `ClaimingType` is the type's namespace and names joined by `.`, value names.  `static Comparison` orders by path (`SqlPath.Comparer`), then `QueryIndex`, then `ClaimingType` ordinal.
  - `record ModelRequest(string Namespace, string NamespaceText, string Name, ModelRole Role, ModelKind Kind, bool IsSorted, bool IsPublic, SidecarTable? Table, EquatableArray<ModelProperty> Properties, ModelSource Source)`, with `FullName` = `Namespace` + `.` + `Name`, or `Name` in the global namespace.  `Namespace` is the value form (no `@`), `NamespaceText` as written; `Properties` already sorted when `IsSorted`; `Table` set only for a table model (Task 5).
  - `TypeModels.Of(TypeQueries type, EquatableArray<FileModels> files, SettingsLevel property) : EquatableArray<ModelRequest>`.
  - `record ModelGroup(string FullName, EquatableArray<ModelRequest> Requests)` in `ModelSource` order; `record Model(string HintName, string NamespaceText, string Name, ModelRole Role, ModelKind Kind, bool IsPublic, EquatableArray<ModelProperty> Properties, string SummaryXml)`; `record ModelSet(EquatableArray<Model> Models, EquatableArray<DiagnosticInfo> Diagnostics)`.
  - `ModelCollector.Group(ImmutableArray<ModelRequest> requests) : EquatableArray<ModelGroup>` (sorted by full name, ordinal); `ModelCollector.Resolve(EquatableArray<ModelGroup> groups) : ModelSet` (in this task: the first request of each group, no checks; Task 4 adds them and a second parameter).
  - `HintName.ForModel(string fullName) : string` = `fullName + "-Model.g.cs"`.
  - `ModelEmitter.Emit(Model model) : string`.

**What a type asks for** (`TypeModels.Of`): nothing for a type with diagnostics; for each usable file (`FindUsableFiles`) and each of its `QueryModels`, the settings `QuerySettings.Resolve(query markers, attribute, metadata, property)`; skip unless `Output` is `Models` or `CodeGen`.  An input request when `Input` is not null, an output request when `Output` is not null.  Name: the marker's when present (a name with a period is a full name: namespace before the last period, ignoring the namespace setting), else the query's name and the role's suffix.  Namespace: `ModelNamespace ?? type.Namespace`.  `IsSorted` from `SortInput`/`SortOutput`; sort with `StringComparer.Ordinal` on `Name`.  The query's markers are found through `ParsedSqlFile.Queries[QueryModels.Index].Markers`.

**Shape** (spec, Shape and Documentation):

```csharp
// <auto-generated/>
#nullable enable

namespace App
{
    /// <summary>
    /// Loads one user by id.
    /// A row of the query <c>GetUser</c> from <c>Users.sql</c>.
    /// </summary>
    /// <param name="Id">The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column.</param>
    public sealed record GetUserDto(int Id, string? Name);
}
```

A class: `public sealed class GetUserDto` with, per property, a `<summary>` and `public string Name { get; set; } = default!;` (`= default!` only for a non-nullable reference type).  An empty record is `public sealed record X();`; an empty class has an empty body.  Keywords by `ModelKind`: `record`, `sealed record`, `class`, `sealed class`.  Accessibility `public` or `internal`.  No namespace block in the global namespace.  `#pragma warning disable CS0612, CS0618 // A mapped type is obsolete.` after `#nullable enable` only when a property's `Type.IsObsolete`.  The summary sentence: `The parameters of the query <c>Q</c> from <c>F.sql</c>.` or `A row of the query ...`; several queries: `the queries <c>A</c> from <c>F.sql</c> and <c>B</c> from <c>G.sql</c>` (commas between more than two, `and` before the last); a `-- summary:` first, escaped, on its own lines.  A table model's sentence is Task 5's.

**Pipeline** (`SqlSourceGenerator`): `allFileModels = fileModels.Collect().Select(ToSorted by path).WithTrackingName(AllFileModels)`; `typeModels = typeQueries.Combine(allFileModels).Combine(projectLevel).Select(TypeModels.Of).WithTrackingName(TypeModels)`; `modelGroups = typeModels.SelectMany(x => x).Collect().Select(ModelCollector.Group).WithTrackingName(ModelGroups)`; `models = modelGroups.Select(ModelCollector.Resolve).WithTrackingName(Models)`; `RegisterSourceOutput` of `models.Select(set => set.Diagnostics)`; `models.SelectMany(set => set.Models).Select(model => (model.HintName, ModelEmitter.Emit(model))).WithTrackingName(ModelOutput)` into `RegisterSourceOutput` that adds the source.  Comments say why each step is where it is, as the file's other comments do.

- [ ] **Step 1: Tests** `ModelTests` (Models, postgres; each asserts no generator diagnostic, no compilation error, no generated-code warning, and the exact source of the model's file where it states one):
  - Each row of the which-models table (four tests).
  - `Run_EachModelType_WritesItsShape` theory over the four `ModelKind` values for an output model with a non-nullable `string`, a nullable `int` and a nullable `string`; and the same for an input model.  State the full source for `sealed record` and `class`.
  - `Run_EmptyOutputModel_IsAnEmptyRecord`, and `...IsAnEmptyClass`.
  - Suffixes at each level (property, metadata, attribute, file header marker), the two markers without and with a period.
  - Namespace at each level, the global namespace, a marker with a period ignoring `SqlSourceModelNamespace`.
  - Accessibility: public type, internal type, public nested in internal (`internal`).
  - Order with a declared-only parameter last; `sort-input`; `sort-output`.
  - Documentation of a model with and without `-- summary:`.
  - `Run_ReservedNamesConfirmed_CompileInNoModel`: for each model kind, a model whose properties are named by each name *not* in the reserved list that a record or class declares (`Equals` excluded): confirm none warns; add any name the compiler warns on to `PropertyNames.IsReserved` and to Task 2's theory.
  - Review Focus 1, 4 and 5.
  - `Run_OutputSql_EmitsNoModel`.
- [ ] **Step 2: Run** `ModelTests`; expect failures.
- [ ] **Step 3: Implement** the interfaces above.
- [ ] **Step 4: Run** the whole suite; pass.
- [ ] **Step 5: Validate and commit** `"Emit the models of each type that claims a query"`.

---

### Task 4: The shared-name rule and the names that cannot be used (`SQLSRC257`, `SQLSRC258`, `SQLSRC251`)

**Files:**
- Create: `Generation/Models/TakenName.cs`, `TakenReason.cs`, `ModelNameChecker.cs`
- Modify: `ModelCollector.cs`, `SqlSourceGenerator.cs`, `TrackingNames.cs` (`ModelNames`, `TakenModelNames`), `SqlDiagnostics.cs` (257, 258, 251's format), `QueryTyper.cs` (251's first-case argument), `AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (257, 258, and 251's second case), `src/SqlSource/AGENTS.md` (`ModelNameChecker` among the symbol readers)
- Test: `tests/SqlSource.Tests/Generator/SharedModelTests.cs`; `TypeDiagnosticsTests` keeps 251's first case unchanged

**Interfaces:**
- Produces: `enum TakenReason { Type, Namespace, NamespacePartIsType }`; `record TakenName(string FullName, TakenReason Reason, string Part)`; `ModelNameChecker.FindTaken(Compilation compilation, EquatableArray<string> fullNames, CancellationToken token) : EquatableArray<TakenName>` (empty at once for no names); `ModelCollector.Resolve(EquatableArray<ModelGroup> groups, EquatableArray<TakenName> taken) : ModelSet`; descriptors `ModelsDiffer` (257), `ModelNameUnusable` (258).

**The rules:**
- `ModelNameChecker`: looks in `compilation.Assembly` only.  For each name: a type of that metadata name → `Type`; a namespace of that name in `compilation.Assembly.GlobalNamespace` → `Namespace`; each proper prefix of its namespace that is a type → `NamespacePartIsType` with the prefix as `Part`.  The attributed types and the generated attribute's types are in the compilation, so they are found as types.
- In `Resolve`, for each group, in order:
  1. The first request is the model; each later request is compared with it, the first difference wins: role (`one is an input model and the other an output model`), `Kind` (`it is a '{kind}' here and a '{kind}' there`, kinds as a user writes them, `sealed record`), `IsSorted` (`its properties are sorted here and not there`, or the reverse), count (`it has {n} properties here and {m} there`), and per property name, type text, nullability (`its property {i} is '{a}' here and '{b}' there`, `the property '{p}' is '{t}' here and '{u}' there`, `the property '{p}' is nullable here and not there`).  A difference is `SQLSRC257` at the later request's marker location, or its name; the request is dropped.
  2. Accessibility: public when any request that joined is public.
  3. The model's own name among its property names → `SQLSRC256` (`is the name of its model`, help `Give it an alias` / `Rename it`).
  4. A public model with a property whose `Type.IsPublic` is false → `SQLSRC251`, second case, at each joined query.
  5. `SQLSRC258`, with the reason: taken by the compilation; the full name is another group's namespace (`it is the namespace of another model`); a prefix of its namespace is another group's full name (`'{p}' is another model, and a part of its namespace`); its simple name is ASCII `a`-`z` alone (`the compiler warns on a type name of lower-case letters alone`).
  6. A model failing 3-5 is not emitted; its diagnostics are at each distinct query location that joined it.
  7. Hint names: `HintName.FindAmbiguous` over the emitted models' hint names, then `HintName.MakeUnique` for each.
- Messages:

| Id | Title | Format |
|----|----|----|
| 257 | `Two models of one name differ` | `The {0} model '{1}' of the query '{2}' is not the same as the {3} model of that name of the query {4}: {5}.  Give one of the two queries another model name, or make the two the same.` (`{4}` is `'Q' in F.sql`, with `, for the type T` when the two are one query) |
| 258 | `Model name cannot be used` | `The model '{0}' of the query '{1}' cannot be generated: {2}.  Give the query another model name, or set another model namespace.` (`{2}` `a type of this project has that full name`, `it is a namespace of this project`, `'{p}' is a type of this project, and a part of its namespace`, or the reasons of rule 5) |
| 251 | unchanged | `The type override maps '{1}' to '{2}' for {0}, in {3}, and {4}.`; second case `{4}`: `that type is not public, and the model '{m}' is.  Make the type public, or make each type that claims the query's file internal` |

- **Pipeline:** `modelNames = modelGroups.Select(full names, sorted, distinct).WithTrackingName(ModelNames)`; `takenNames = CompilationProvider.Combine(modelNames).Select(ModelNameChecker.FindTaken).WithTrackingName(TakenModelNames)`; `models = modelGroups.Combine(takenNames).Select(ModelCollector.Resolve)`.

- [ ] **Step 1: Tests** `SharedModelTests`: two queries, one `-- output-model:` name, identical → one model, its summary naming both; each difference of rule 1 → `SQLSRC257` at the marker with the exact message, one model emitted; a later query without a marker differing from an earlier one with it (the later's name location); input and output model of one name → 257; a shared model's documentation is the first query's although the second's origin differs; two types claiming one file in one namespace → one model; in two namespaces → two; a public and an internal type sharing → `public`; Review Focus 2; clash with a class of the project, with the attributed type, with a namespace of the project, with a type that is a part of the model's namespace (`SqlSourceModelNamespace=App.Queries` beside `class App.Queries`), with another model's namespace; `-- output-model: usersdto` → 258; a property named like its model → 256; an internal mapped type in a public model → 251 at the query, and in an internal model → compiles.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole suite; pass.
- [ ] **Step 5: Validate and commit** `"Share a model by its full name, and report the names that cannot be used"`.

---

### Task 5: Table models

**Files:**
- Modify: `PropertyNames.cs` (`FromTable`), `TypeModels.cs`, `ModelCollector.cs` (the table sentence)
- Test: `tests/SqlSource.Tests/Generator/TableModelTests.cs`

**Interfaces:**
- Produces: `PropertyNames.FromTable(SidecarTable table) : string?`: the PascalCase of `Table`, with the PascalCase of `Schema` before it unless the schema is `public`; null when either is null or a part's PascalCase is not an identifier.

**The rules:** an output request with no `-- output-model:` marker, `NoTableModels` off, and `QueryModels.Table` giving a name: named `FromTable(table) + OutputModelSuffix`, `Table` set on the request.  A table model's summary sentence: `A row of the table <c>{schema}.{table}</c>, read by the query ...` (the queries as in Task 3), escaped.  A table that gives no identifier falls back to the query's name, silently.

- [ ] **Step 1: Tests** `TableModelTests`: a match → `UsersDto`, the table sentence; `billing.invoices` → `BillingInvoicesDto`; `no-table-models` at the property, metadata, attribute, file header and query levels; an `-- output-model:` over a match; `"order-lines"` → the query's model, no diagnostic; two queries matching one table → one model naming both; two matching with a type override that differs → `SQLSRC257`; two namespaces matching one table without and with `SqlSourceModelNamespace` → two models, then one; a table model and a query `Users` → one name, the rule of Task 4.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole suite; pass.
- [ ] **Step 5: Validate and commit** `"Name a query's model after the table it matches"`.

---

### Task 6: Caching tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/CachingTests.cs`

Each test runs a driver twice, reading `TrackingNames.ModelOutput`'s output reasons (`Outputs`/`Reasons` helpers there) and the steps it names:

- [ ] **Step 1: Tests:**
  - `Run_SqlFileEditedChangingOneModel_EmitsOnlyThatModelAgain`: three files, each with a model; edit a column of one (its sidecar); only that model's `ModelOutput` is `Modified`, the others `Unchanged`.
  - `Run_SharedModelEdited_EmitsOnlyThatModelAgain`: a model of two files; edit one; only that model is `Modified` (and is `SQLSRC257` or a changed summary as the case gives).
  - `Run_ModelAdded_EmitsTheModelsAfterItAgain`: add a model sorted first; the models after it are `Modified`, pinning the accepted `SelectMany` cost; a comment says so.
  - `Run_CSharpEdit_EmitsNoModelAgain`: a method body edit; `TakenModelNames` runs, `Models` is `Unchanged` or `Cached`, every `ModelOutput` `Unchanged`/`Cached`.
  - `Run_CollectionTypePropertyChanged_EmitsNoModelAgain`: `SqlSourceCollectionType` changes; `TypeModels` runs and gives equal values, no model re-emitted.
  - `Run_AttributeEditedWithoutChangingAModel_EmitsNoModelAgain`.
  - `Run_ProjectWithoutModels_LooksUpNoName`: at `Sql`, `TakenModelNames` gives empty.
- [ ] **Step 2: Run**; fix the pipeline if a test shows more work than the rule allows, never the test.
- [ ] **Step 3: Validate and commit** `"Pin what an edit re-emits of the models"`.

---

### Task 7: The documents

**Files:** `docs/architecture/generator.md`, `docs/manual/generator.md`, `settings.md`, `postgresql.md`, `recipes.md`, `sql-files.md`, `diagnostics.md` (See lines), `README.md` (manual entry page "What works today"), `docs/manual/README.md`, root `README.md`, the epic outline, `tests/AGENTS.md` or `src/SqlSource/AGENTS.md` where a rule changes.

- [ ] **Step 1: Architecture.**  Replace `### Models` under `## Not built yet` with a section under `## The pipeline` describing the steps as built (the per-file step, the per-type step, the collection, the compilation lookup, the fan-out, the accepted `SelectMany` cost), and a `### Models` subsection under `## Emitting` for `ModelEmitter`; add the step names to the records table if it lists them; update `### Caching tests`.
- [ ] **Step 2: Manual**, following `docs/manual/AGENTS.md` "When a phase lands" and its phase 5 row, for what 5.3 delivers: `generator.md` Models section, example first (which models, names, namespace, accessibility, shape, order, nullability, documentation, table models, shared names) and the known issue on Dapper parameters (no id); `settings.md` statuses of the model suffixes, namespace, model types, the two markers, `sort-input`, `sort-output`, `no-table-models` flipped, and Output's `models`; `postgresql.md` Models and the nullability callout replaced with the `!` and `?` suffixes; `recipes.md` the Dapper recipe (models through Dapper, `DefaultTypeMap.MatchNamesWithUnderscores`, `class` models when sorting, camelCase parameters); `sql-files.md` the marker index's callout for the model markers; `diagnostics.md` See lines; `docs/manual/README.md` "What works today" rows.  Run the two greps of `docs/manual/AGENTS.md` and rewrite each hit.
- [ ] **Step 3: README**: the introduction's paragraph on what SqlSource generates, step 4's callout narrowed to methods, "Only want to generate models?", "when generated models arrive", the FAQ on the models' namespace.  The status line waits for 5.7.
- [ ] **Step 4: Epic**: 5.3's row `Done`, and any sentence of the outline this plan's drift makes wrong.
- [ ] **Step 5: Validate and commit** `"Document the models"`.
