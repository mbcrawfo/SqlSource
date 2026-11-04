# Placement and Visibility in the File Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `SqlLocation`, `MethodLocation` and `NestedClass` leave `[SqlSourceGenerate]`, which keeps `Source` alone; the `.sql` file's markers and its settings files give each query a member location, a visibility and a nested class.

**Architecture:** Three new settings travel the ordinary chain (query marker, header marker, nearest settings file, farther ones, default) through `SettingsLevel` and `QuerySettings`, each value carrying the `SettingPlace` it was written in.  The type-dependent part (default visibility, the shared nested class's visibility, the checks that need the type) moves after parsing, into one per-type placement analysis in `Generation/Placement/` that `UsableQueries` runs, so that `TypeEmitter` and `MethodPlanner` see the same answer from the same `TypeQueries`.  Each query method carries its own host, so one type's methods file can hold methods on the type, in nested classes and in the extension class.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.14.0 floor), xUnit v3 + Shouldly, Testcontainers database tests.

**Spec:** `docs/superpowers/specs/2026-11-03-file-owned-settings-design.md`, section "Pull request 2: placement and accessibility belong to the file", with "Documentation and examples" and "Left to the builder".

## Global Constraints

- The attribute keeps `Source` alone; `SqlSourceConverterAttribute` stays.  No alias, no obsolete property, no diagnostic for a removed property beyond the compiler's own.
- Values match ignoring case, hyphens and spaces (`SettingValue.TryReadChoice`), so `nested-class` and `NestedClass` are one value.
- Markers: `-- member-location:`, `-- visibility:`, `-- nested-class:`, each allowed in the file header and inside a query.  Settings-file keys: `memberLocation`, `visibility`, `nestedClass`.
- Member location values `ExtensionClass`, `Class`, `NestedClass`; default `NestedClass` for `sql` and `models`, `ExtensionClass` for `codegen`.
- Visibility values `Public`, `Internal`, `Protected`, `Private`; default by resolved location: `NestedClass` → `Private`; `Class` → `Public` when the type and every type around it are public, else `Internal`; `ExtensionClass` → `Internal`.
- Nested class: a C# identifier that is not a keyword (`SettingValue.IsTypeName`), default `Sql`; ignored where the location is not `NestedClass`.
- No hidden fallback: a header or settings-file value reaches every query; a combination that does not fit is an error whose message names where the value came from and suggests `-- visibility:` or `-- member-location:` on the query.
- Errors leave the file without members for that type, and are reported at the query's own marker when it has one, else at its `-- name:` line.
- Under `NestedClass`, `visibility` is the nested class's own accessibility and its members are always `public`; `codegen` methods there are static methods that take the connection.
- An extension class takes the widest accessibility of its type's methods; across two types it stays `SQLSRC276`.
- `protected` is an error only where C# forbids it: under `Class` on a static class (CS1057) or a struct or record struct (CS0666); under `NestedClass` on a struct or record struct (CS0666); always under `ExtensionClass`.  On a sealed type it is allowed, and generated code disables CS0628.
- A written visibility beats a default for a shared nested class; two different written ones are an error that removes every involved file's members.
- Repository rules: every commit passes `./pre-commit-validation.sh` (run `./format.sh` first); a switch over an enum names every member with no `_ =>`; models are records with `EquatableArray<T>`; nothing a user writes makes the generator throw; a hot-path change states time and allocation before and after in the PR.

## Decisions the spec left to the builder, and where the code differs from it

- **One pull request.**  The repository has no size rule that forces a split.
- **`Reporting.AccountReports` keeps its claim** of `Queries/Accounts.sql` with `SqlLocation` dropped: every query of that file is `codegen`, so `Direct` changed nothing, and the claim is the test of a second namespace claiming one file.  Asked of the owner as Q1; if the answer differs, Task 5 changes.
- **Diagnostic ids.**  `SQLSRC006` is removed outright, as `SQLSRC010`, `011` and `014` were: nothing has shipped (`AnalyzerReleases.Shipped.md` is empty), so it leaves `Unshipped.md`'s New Rules and its manual section, and the id is not reused.  New, in the `.sql` file range after `SQLSRC121`:
  - `SQLSRC122` "Member location does not fit the query": `ExtensionClass` on a `sql` or `models` query.
  - `SQLSRC123` "Visibility does not fit the member location": `ExtensionClass` with `Private` or `Protected`; `ExtensionClass` with `Public` on a type that is not effectively public; `Protected` where C# forbids it.  One id, a reason argument.
  - `SQLSRC124` "Nested class has two visibilities".
  - `SQLSRC125` "Query's member is named like a nested class": a constant or token method under `Class` named like a nested class of the same type (CS0102 otherwise).
- **`SQLSRC007`** stays at the type's attribute, once for each taken name, and now leaves out every file whose queries put members in a nested class of that name, instead of the whole type.
- **Empty nested class.**  A type with no usable query writes an empty `private static partial class`, named by its first file's level `NestedClass` else `Sql`, unless the type has a member of that name, so that a reference to it reports the missing member and not a missing type (today's behaviour).  A type with a usable query writes only the nested classes its queries use.
- **The nested class is `static partial`** in every file that writes it, the type's file and the methods file, with the same accessibility from the same analysis.
- **`TakenNames` moves into `TargetType`** and is read for every type, since the nested class and the location are known only after parsing.  `DeclaredType`, `NestedClassCheck`, `TypeMemberNames`, `MethodsInput.MemberNames` and their tracking names go.  An edit that adds a member to a claiming type now runs that type's later steps again; the caching tests that pinned the old shield change on purpose, and the PR says so.
- **The harness's sample type** (`GeneratorHarness.SampleSource`) loses `SqlLocation = SqlLocation.Direct`.  `ProjectSettings` writes `"memberLocation": "class"` when the run's output is `Sql` or `Models`, so that the tests that read `Sample.Query` keep reading it; a `CodeGen` run writes none, so its methods go to the extension class as before.  `settings:` can replace the key.
- **`MethodAccessibility` goes**; `Settings.Visibility` is the accessibility everywhere.

## Review Focus

1. A file with `sql` and `codegen` queries and a header `-- visibility: private`: the `sql` constants go to a private nested class, the `codegen` query gets `SQLSRC123` naming the file header, and the message suggests a query-level marker.  Test: Task 3, `Run_HeaderPrivateWithACodeGenQuery_ReportsSqlsrc123NamingTheHeader`.
2. A folder-wide settings file `"memberLocation": "ExtensionClass"` above `sql` queries: `SQLSRC122` at each query's name line, naming the settings file's path.  Test: Task 3, `Run_SettingsFileExtensionClassOverSqlQueries_ReportsSqlsrc122AtEachQuery`.
3. Two files of one type feeding one nested class, one writing `public` and the other nothing: the class is public, no error; both written differently: `SQLSRC124` and neither file's members.  Tests: Task 3, `Run_SharedNestedClassWithOneWrittenVisibility_TakesIt` and `Run_SharedNestedClassWithTwoWrittenVisibilities_ReportsSqlsrc124AndDropsBothFiles`.
4. A `codegen` method in a nested class with `async-method-suffix` named like a constant of that class from another file: `SQLSRC274`.  Test: Task 3, `Run_NestedMethodNamedLikeAConstantOfItsClass_ReportsSqlsrc274`.
5. `protected` members, constants and methods, on a sealed class compile with no warning in both generated files.  Test: Task 3, `Run_ProtectedOnASealedClass_CompilesWithoutWarnings`.

---

### Task 1: The three settings are read, from markers and settings files, with no effect yet

**Files:**
- Create: `src/SqlSource/Settings/MemberLocation.cs`, `src/SqlSource/Settings/Visibility.cs`, `src/SqlSource/Settings/Placed.cs`
- Modify: `src/SqlSource/Settings/SettingsLevel.cs`, `SettingId.cs`, `SettingsLevelReader.cs`, `QuerySettings.cs`; `src/SqlSource/SettingsFiles/SettingsFileKeys.cs`; `schemas/settings-v1.schema.json`
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlMarkerScope.cs`; `src/SqlSource/Parsing/SqlBlock.cs`, `SqlFileParser.cs`; `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`
- Test: `tests/SqlSource.Tests/Settings/`, `tests/SqlSource.Tests/SettingsFiles/`, `tests/SqlSource.Tests/Parsing/` (the marker tests and `Marker_OfEveryKind_HasAKeywordAndAScope`), the schema tests, `tests/SqlSource.Testing/` `SettingLevel` rows, and a tool test in `tests/SqlSource.Tool.Tests/` that a project whose `.sql` header and queries carry the three markers is described without error.

**Interfaces:**
- Produces:
  - `internal enum MemberLocation { ExtensionClass, Class, NestedClass }` and `internal enum Visibility { Public, Internal, Protected, Private }` in `SqlSource.Settings`.
  - `internal readonly record struct Placed<T>(T Value, SettingPlace Place) where T : struct`.
  - `SettingsLevel.MemberLocation : Placed<MemberLocation>?`, `SettingsLevel.Visibility : Placed<Visibility>?`; `SettingsLevel.NestedClass : string?` now also from markers.  `Over` takes each whole.
  - `SettingId.MemberLocation`, `SettingId.Visibility` (appended after `NestedClass`); keys `SettingsFileKeys.MemberLocation = "memberLocation"`, `SettingsFileKeys.Visibility = "visibility"` in `Settings` and `All`.
  - `QuerySettings` gains `Placed<MemberLocation>? WrittenMemberLocation`, `Placed<Visibility>? WrittenVisibility`, `string NestedClass`, and the property `MemberLocation MemberLocation => WrittenMemberLocation?.Value ?? (Output == OutputKind.CodeGen ? MemberLocation.ExtensionClass : MemberLocation.NestedClass)`.  `Resolve` fills them markers over file over default (`DefaultNestedClass`).
  - `SqlMarkerKind.MemberLocation`, `Visibility`, `NestedClass` (appended), keywords `member-location`, `visibility`, `nested-class`, allowed in the preamble and in a query.  The scope's place is `FileHeader` for the preamble, `Query` otherwise, as its type override already does.
  - `SqlQuery.MemberLocationMarker` and `SqlQuery.VisibilityMarker`: `LocationInfo?` init properties, the query's own marker (never the header's), from new `SqlBlock` spans.

- [ ] **Step 1: Write the failing tests.**  `SettingValue` reads `class`, `Class`, `nested-class`, `NestedClass`, `extension class`; `nested-class: Sql2` is a valid type name and `nested-class: class` (a keyword) is `MarkerValueNotValid`; a second different value in one scope is the existing conflict error; a settings file with `"memberLocation": "nested-class"` and `"visibility": "protected"` gives a level whose places are that file's `SettingPlace.OfSettingsFile(path, depth)`; a header marker's place is `FileHeader`, a query's `Query`; `QuerySettings.Resolve` takes the query over the header over the file, and the defaults of `MemberLocation` by output; the schema test passes for both new keys; the SettingLevel theory has a row for each new setting at each level.
- [ ] **Step 2: Run them and see them fail.**  `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Settings|FullyQualifiedName~Parsing"`: fails to compile on the new members.
- [ ] **Step 3: Implement**, following `src/SqlSource/Settings/AGENTS.md` "A new setting" and `src/SqlSource/Parsing/AGENTS.md`'s marker rule.  The schema's two keys list the choices in the enums' order and the pattern `SettingsFileSchemaTests` expects; `nestedClass`'s description stops saying "a type's queries".
- [ ] **Step 4: Add the tool test**, a `.sql` file with `-- member-location: class`, `-- visibility: internal` and `-- nested-class: Users` in its header and inside a query, described with no diagnostic.
- [ ] **Step 5: Run** `dotnet test tests/SqlSource.Tests` and the tool test's class: pass.  If a documentation test asks for the new keys or markers in `docs/manual/settings.md` or `sql-files.md`, add their rows now with the status "accepted; no effect until a later change", which Task 6 replaces.
- [ ] **Step 6: Format, validate, commit** `feat: read member-location, visibility and nested-class from markers and settings files`.

### Task 2: The generator places members from the file (source, no commit)

This task changes `src/` and fixes only what stops the test project from compiling; Task 3 brings the suites back to green and commits both.  Hand back with the generator building and the new tests of `Generation/Placement/` passing.

**Files:**
- Create: `src/SqlSource/Generation/Placement/QueryPlacement.cs`, `NestedClassPlacement.cs`, `TypePlacement.cs`, `PlacementChecks.cs`, `PlaceText.cs`
- Delete: `src/SqlSource/Settings/MemberPlacement.cs`, `MethodPlacement.cs`; `src/SqlSource/Generation/NestedClassCheck.cs`, `DeclaredType.cs`; `src/SqlSource/Generation/Methods/MethodAccessibility.cs`, `TypeMemberNames.cs`
- Modify: `src/SqlSource/Generation/AttributeSource.cs`, `TargetType.cs`, `TargetTypeReader.cs`, `UsableQuery.cs`, `UsableQueries.cs`, `UsableQuerySequence.cs`, `TrackingNames.cs`; `src/SqlSource/SqlSourceGenerator.cs`; `src/SqlSource/Settings/SettingPlace.cs`, `SettingPlaces.cs`, `QuerySettings.cs` (`ResolveNestedClass` goes), `SettingsLevel.cs` docs; `src/SqlSource/Generation/Emitting/TypeEmitter.cs`, `MethodEmitter.cs`, `MethodWriter.cs`, `QueryMethods/HeaderWriter.cs`, `QueryMethods/QueryMethodContext.cs`, `QueryMethods/QueryMethodWriter.cs`; `src/SqlSource/Generation/Methods/MethodHost.cs`, `QueryMethod.cs`, `TypeMethods.cs`, `MethodSignature.cs`, `MethodsInput.cs`, `MethodPlanner.cs`, `MethodConflicts.cs`, `MethodArguments.cs`; `src/SqlSource/Diagnostics/SqlDiagnostics.cs`; `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Test: `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs` (unit tests of the analysis over hand-built `TypeQueries`)

**Interfaces:**
- Consumes: Task 1's `MemberLocation`, `Visibility`, `Placed<T>`, `QuerySettings.WrittenMemberLocation`/`WrittenVisibility`/`NestedClass`/`MemberLocation`, `SqlQuery.MemberLocationMarker`/`VisibilityMarker`.
- Produces:
  - `TargetType(string Namespace, EquatableArray<TypeDeclaration> Types, bool IsPublic, bool IsStatic, bool IsSealed, string? Source, string FilePath, LocationInfo AttributeLocation, EquatableArray<string> TakenNames, EquatableArray<DiagnosticInfo> Diagnostics)`, with `bool IsStruct` from the last declaration's keyword (`struct` or `record struct`).  `TargetTypeReader.Read` returns `TargetType?`; the pipeline's `NestedClassCheck` step and the `TypeMemberNames` step go.
  - `QueryPlacement(MemberLocation Location, Visibility Visibility, string? NestedClass)`: for `NestedClass`, `Visibility` is the class's; its members are public.
  - `NestedClassPlacement(string Name, Visibility Visibility)`.
  - `TypePlacement.Of(TargetType type, EquatableArray<ParsedSqlFile> files, EquatableArray<SettingsLevel> fileSettings, ImmutableArray<DiagnosticInfo>.Builder? diagnostics) : TypePlacement`, holding `bool[] Usable`, `IReadOnlyList<NestedClassPlacement> NestedClasses` (in first use, of usable files), `string? EmptyNestedClass`, and `QueryPlacement PlacementOf(QuerySettings settings)`.  `UsableQueries.FindUsableFiles` becomes it; `UsableQuery` gains `QueryPlacement Placement`; `UsableQueries.Of` returns a sequence that also exposes the `TypePlacement`.
  - `PlacementChecks.DefaultVisibility(MemberLocation location, bool typeIsPublic) : Visibility`.
  - `PlaceText.Of(SettingPlace place) : string`: "its own marker", "the file header", "the settings file '<path>'".
  - `MethodHost(Visibility Accessibility, MemberLocation Location, string? NestedClass)` with `IsExtension`; `QueryMethod.Host`; `TypeMethods` loses `Host` and `IsPublic` and gains `bool ExtensionClassIsPublic`, `EquatableArray<NestedClassPlacement> NestedClasses`, `bool DisablesProtectedWarning`.  `MethodSignature` is made only for extension methods; its `IsPublic` is `ExtensionClassIsPublic`.
  - `MethodsInput.Gather` loses its `memberNames` parameter; the planner reads `TargetType.TakenNames`.
  - `SqlDiagnostics.MemberLocationNotFit` (`SQLSRC122`), `VisibilityNotFit` (`SQLSRC123`), `NestedClassVisibilities` (`SQLSRC124`), `MemberNamedLikeNestedClass` (`SQLSRC125`); `InvalidAttributeValue` (`SQLSRC006`) goes.

- [ ] **Step 1: Shrink the attribute and the type.**  `AttributeSource.Text` keeps `SqlSourceConverterAttribute` and `SqlSourceGenerateAttribute` with `Source` alone; the enums, `LocationMetadataName`, `MethodLocationMetadataName`, `LocationProperty`, `MethodLocationProperty`, `NestedClassProperty` go.  `TargetTypeReader` reads `Source`, `IsStatic` and `IsSealed` from the symbol, and `TakenNames` for every type; `AttributeSettings` goes, and with it `SettingPlaceKind.Attribute`, `SettingPlace.Attribute` and its arm of `SettingPlaces.Specificity`.  `SettingsLevelReader` is now the reader of the settings files alone; fix its comments.
- [ ] **Step 2: Write the failing tests of the analysis** in `TypePlacementTests`, one per rule below, each asserting `Usable`, `NestedClasses` and the diagnostics' ids and locations.
- [ ] **Step 3: Implement `TypePlacement.Of`.**  Resolve each query's settings once in the analysis.  The order is fixed so diagnostics are stable:
  1. For each file, each query: `SQLSRC009` (a non-`codegen` query named like its container: the nested class under `NestedClass`, the type under `Class`; a `codegen` query under `NestedClass` whose method name, with `Async` when `async-method-suffix` is on, is the nested class's), `SQLSRC008`, the kept errors, then `SQLSRC122`, then `SQLSRC123` for the query's own visibility (written, or the default for its location, which never fails).  Any one leaves the file out.
  2. Over the usable files, by nested class name: the class's visibility is its first written visibility, else `Private`.  A second, different written one is `SQLSRC124` at each query whose written value differs from the first, at its own `-- visibility:` marker else its name, naming both places and queries; every file with a query in that class is left out.  A class's written `Protected` on a struct is `SQLSRC123` at the query that wrote it.
  3. `SQLSRC007` at the attribute, once for each nested class name of a usable query that is in `TakenNames`; every file with a query in that class is left out.
  4. `SQLSRC125` at the name of each non-`codegen` query under `Class` named like a nested class that a usable query uses; its file is left out.
  `SQLSRC123`'s reason is one of: an extension method is public or internal; '<type>' is not public, so the models a public extension method takes are internal; '<type>' is a static class, which cannot have protected members; '<type>' is a struct, which cannot have protected members.  `SQLSRC122` and `SQLSRC123` are at the query's own marker of the setting at fault (`VisibilityMarker` for `SQLSRC123`, else `MemberLocationMarker`, else `NameLocation`).  Messages name the place with `PlaceText`, and end "Write -- visibility: or -- member-location: on the query."  `EmptyNestedClass` is set only when no query is usable, per the decision above.
- [ ] **Step 4: Change the diagnostics.**  `SQLSRC007`: "'{0}' already has a member named '{1}', which a file of it names as its nested class.  Rename the member, or set -- nested-class: or -- member-location: in the file."  `SQLSRC276`'s fix: "or set -- member-location: to put the methods on the type."  `SQLSRC274` gains the reasons "the class of the queries '<name>' of '<type>' has a constant or method of that name" (a method in a nested class named like another member of that class, from any file of the type) and "'<type>' has a nested class of that name" (a method under `Class`).  Add the four new descriptors written out in full, update `All` and `AnalyzerReleases.Unshipped.md`, remove `SQLSRC006` from both.
- [ ] **Step 5: Emit the constants.**  `TypeEmitter` writes members under `Class` on the type with their own accessibility (`<vis> const string`, `<vis> static string` for a token method: `MethodWriter.Append` takes the accessibility), then each nested class of `NestedClasses` that holds a member, as `<vis> static partial class <Name>` with public members, then `EmptyNestedClass` if set.  A type whose `IsSealed` is true and that writes any `protected` member or nested class adds `#pragma warning disable CS0628 // A file asks for protected members of a sealed type.` after the header lines.
- [ ] **Step 6: Plan and emit the methods.**  `MethodPlanner` gives each method its `MethodHost` from its `UsableQuery.Placement` (`NestedClass` → `Public` in that class), `ExtensionClassIsPublic` when any extension method is `Public`, `NestedClasses` of the classes its methods use, and `DisablesProtectedWarning` for a sealed type with a protected method or nested class.  `FindTakenReason` checks by host: extension as today; `Class` as today's `Direct` branch, written names being those under `Class`, plus the type's nested class names; `NestedClass` against the written names of that class.  `MethodEmitter` writes, in one file, the partial type with the `Class` methods and then each nested class's methods (as `<vis> static partial class`), then the extension class; it starts with `TypeEmitter.HeaderLines` when the type part is written, else `GeneratedText.Header`; `extensionClassBlocked` drops the extension methods alone.  `HeaderWriter` spells `protected static`.  `MethodConflicts` compares only extension methods, by `ExtensionClassIsPublic`.
- [ ] **Step 7: Wire the pipeline.**  `ClaimFiles` takes `TargetTypeReader.Read` straight into `PathResolver`; drop `typeMemberNames` from `ClaimedFiles` and from `MethodsInput.Gather`'s call; drop `TrackingNames.NestedClassCheck` and `TypeMemberNames`.
- [ ] **Step 8: Make the test project compile** where it names removed types (`MemberPlacement`, `MethodPlacement`, `MethodAccessibility`, `DeclaredType`, the old `TargetType`, `TypeMethods` or `MethodsInput.Gather` shapes, `AttributeSourceTests`' enum comparisons, which go).  Run `dotnet build` and `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Placement`: pass.  Do not commit; hand back.

### Task 3: The generator's tests describe the new model; commit the core

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, `CorpusTypeScenarios.cs`, `MethodRuns.cs`; every test of `tests/SqlSource.Tests/Generator/` and `tests/SqlSource.Tests/Generation/` that sets `SqlLocation`, `MethodLocation` or `NestedClass`, or whose output the new defaults change; the golden files of `tests/SqlSource.Tests/Generator/Golden/` (rewrite with `SQLSOURCE_UPDATE_GOLDEN=1` and read the diff); `tests/SqlSource.Tests/EndToEnd/`; the tool tests that the harness change reaches; `docs/manual/diagnostics.md` (the sections the diagnostics test checks: `SQLSRC006` out, `122` to `125` in, `007`, `009`, `274`, `276` true).
- Test: new driver tests in `tests/SqlSource.Tests/Generator/` (`PlacementTests.cs`, `PlacementDiagnosticsTests.cs`, or folded into `NestedClassTests.cs` and `MethodTests.cs` where they belong).

**Interfaces:**
- Consumes: Task 2's whole surface.
- Produces: green `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests`; one commit holding Tasks 2 and 3.

- [ ] **Step 1: Change the harness** as the decision above says, and its comments.  Turn `CorpusTypeScenarios`' attribute arguments into markers in their files (`-- member-location: class` with `-- visibility: public|internal|private`).
- [ ] **Step 2: Write the driver tests**, each stating its output and asserting the generated text or diagnostic and that the compilation has no error and `GeneratedCodeWarnings` is empty:
  - one per default: `sql` in a private `Sql` nested class; `codegen` in an internal `<Type>Extensions`; `Class` on a public type public, on an internal type internal; `nested-class` ignored under `Class`.
  - `codegen` under `NestedClass`: `Sample.Sql.GetUser(connection, …)` compiles and its class is `static partial`, with a constant of another query in the same class from the type's file.
  - one per error of `SQLSRC122` to `SQLSRC125`, the new `SQLSRC009` and `SQLSRC274` cases and the new `SQLSRC007` trigger (a header `-- nested-class: Name` that the type's member takes), each with its location and that the file has no members while the type's other files keep theirs.
  - the Review Focus tests 1 to 5, under the names given there.
  - allowed without a diagnostic: `Class` + `Public` on an internal type; `NestedClass` + `Public`/`Internal` (`UserRepository.Sql.GetUser` reachable from outside); `NestedClass` + `Protected` in a static class; `Protected` on a `sealed record`.
  - an extension class widens to `public` when one method of the type is public and another internal; two types sharing an extension class with different widest accessibilities stay `SQLSRC276`.
  - a file that mixes locations: some constants on the type, some in a nested class, two nested classes `Users` and `Orders` from one file.
  - a type with a parse error in its only file still gets its empty `Sql` class.
- [ ] **Step 3: Migrate the existing tests.**  Each `SqlLocation`, `MethodLocation` and `NestedClass` argument becomes a marker or settings-file key that gives the same output, except where the test's point was the attribute, which goes with the property (`SQLSRC006`'s tests, the attribute's level in `SettingLevelTests`, `AttributeConflictTests` cases of the removed properties).  The caching tests of the removed steps change on purpose: an edit that adds a member to a claiming type now reaches `TypeFiles` and its later steps for that type alone; name the test that says so.
- [ ] **Step 4: Measure.**  On `origin/main` and on this tree, a `Release` `Stopwatch` loop (outside the repository, or a test that is not committed) over `TypeEmitter.Emit` and `MethodPlanner.Plan` for a type of 200 queries across 4 files: time per call and `GC.GetAllocatedBytesForCurrentThread` per call.  Put the four numbers in the hand-back for the PR description.  The allocation-budget tests stay where they are.
- [ ] **Step 5: Update `docs/manual/diagnostics.md`** for the descriptors (sections and index rows in id order), so `DiagnosticsDocumentTests` passes.
- [ ] **Step 6: Run** `dotnet test tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests`: pass.
- [ ] **Step 7: Format, validate, commit** `feat!: place members by the file's member-location, visibility and nested-class`.

### Task 4: AGENTS.md and the architecture follow the code

**Files:**
- Modify: `src/SqlSource/AGENTS.md` (the stage-two rule's nested-class clause), `src/SqlSource/Settings/AGENTS.md` (the `MemberPlacement`/`MethodPlacement` rule goes; "the attribute" leaves the reader rule), `src/SqlSource/Generation/AGENTS.md`, `src/SqlSource/Generation/Emitting/AGENTS.md` (the CS0628 line and the methods file's start), every other `AGENTS.md` that names a removed type (`grep -rn -E "NestedClassCheck|DeclaredType|MemberPlacement|MethodPlacement|TypeMemberNames|MethodAccessibility|SqlLocation|MethodLocation" --include=AGENTS.md`); `docs/architecture/generator.md` and `docs/architecture/tool.md`.
- Create, if its rules are not obvious from the code: `src/SqlSource/Generation/Placement/AGENTS.md`, linking to the architecture section (the one-analysis rule: `TypeEmitter` and `MethodPlanner` read placement only from `TypePlacement`, so the two files agree on a nested class's accessibility).

- [ ] **Step 1:** Rewrite each place to what the code does now; the architecture page gets a section on placement (resolution per query, the per-type analysis, the order of its checks, the two files writing one partial nested class) replacing the nested-class check's.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Documentation`: pass (the AGENTS limits and links).
- [ ] **Step 3: Format, validate, commit** `docs: the architecture and AGENTS.md files describe placement from the file`.

### Task 5: Database tests, examples and the package-install project use the file

**Files:**
- Modify: every claim under `tests/DatabaseTests/` that the grep in Step 1 finds; `tests/DatabaseTests/Postgres/EndToEnd/Queries/Locations.sql`, `LocationQueries.cs`; `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Locations.sql`, `LocationQueries.cs`, `MethodLocationTests.cs`; both engines' `VariantCopyTests.cs`; `Reporting/AccountReports.cs` (drop `SqlLocation` only); `examples/*/Sql/CountQueries.cs` and its `.sql`; a new file in each example project; `examples/*/README.md`; `tools/package-install/Queries.cs` and `Users.sql`; the sidecars, by `tools/describe-end-to-end.sh`; `tests/DatabaseTests/*/README.md`, `tests/DatabaseTests/*/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer.SqlClient61/AGENTS.md` where they name the properties.

- [ ] **Step 1:** `grep -rn -E "SqlLocation|MethodLocation|NestedClass *=" tests/DatabaseTests examples tools --include='*.cs'` lists every claim to change.
- [ ] **Step 2: `Locations.sql`, one claim each.**  PostgreSQL: `LocationQueries` claims it; `GetLocatedName` is copied as `GetLocatedNameInternal` (`-- member-location: class`, `-- visibility: internal`) and `GetLocatedNamePrivate` (`class`, `private`), and `GetLocatedNameNested` (`-- member-location: nested-class`), whose method is `LocationQueries.Sql.GetLocatedNameNested(connection, …)`; the private wrapper calls `GetLocatedNamePrivate`.  SQL Server: `LocationQueries` claims it; `ListAccountNames` keeps the extension class and gets copies `…Public`, `…Internal`, `…Private` (`class` with that visibility) and `…Nested`; `ListAccountNamesWithSuffix` stays once.  Each copy is a row of `VariantCopyTests`.  The tests call the copies by their names.
- [ ] **Step 3: The other claims.**  `SqlLocation = SqlLocation.Direct` becomes `-- member-location: class` on the non-`codegen` queries that a test reads on the type (the `ForDapper` copies, the constants of `ConstantQueries`, `ShapeQueries`, …), in the header only when the file has no `codegen` query; `MethodLocation.X` becomes `-- member-location: class` with `-- visibility: x` on those queries.  `NestedClass = "N"` becomes `-- nested-class: N`.  A type whose methods are now internal by default needs nothing when it is internal.
- [ ] **Step 4: Examples.**  `CountQueries`' `NestedClass = "Counts"` becomes `-- nested-class: Counts` in its file's header.  Each example project gains one file under `CodeGen/` whose queries use `-- member-location: class` and `-- visibility: public` (methods on the type, called as `Type.Method(connection, …)`) and one query under `-- member-location: nested-class`, with the example's program or tests calling them, and its README lists them.
- [ ] **Step 5: `tools/package-install`.**  `Queries.cs` loses `SqlLocation.Direct`; `Users.sql` gets `-- member-location: class` where its non-`codegen` queries are read on the type.  `expected-output.txt` does not change.
- [ ] **Step 6: Sidecars.**  Run `tools/describe-end-to-end.sh` (Docker running).  If it is known to take longer than 10 minutes, do not run it: say so.
- [ ] **Step 7: Run** the database test projects touched (`tests/DatabaseTests/Postgres`, `tests/DatabaseTests/SqlServer`, the driver-floor and allocation projects whose claims changed), the examples' build, and `tools/check-package-install.sh` if it runs locally in under 10 minutes: pass.
- [ ] **Step 8: Format, validate, commit** `test: placement differences become per-query markers in the database tests and examples`.

### Task 6: The user documentation describes placement in the file

**Files:**
- Modify: `docs/manual/settings.md` ("All settings" rows; "Which place wins"; "Where the SQL goes" and "Where the methods go" merge into one section, anchored `setting-member-location` with `setting-visibility` beside it; "Name of the nested class", whose folder is now the `.sql` file's, saying that this is the same folder for a type that reads its default file; the "exist only on the attribute" exception reduced to `Source`; the rule headline's list of what differs per type shrinks), `docs/manual/generator.md` (the attribute's table to `Source`; "Where the members go" and "Where the methods go" on the new model; models follow the type's accessibility while members follow `visibility`; the warning that a folder-wide `memberLocation: ExtensionClass` is an error on every `sql` query below it; the warning that a header or folder-wide `visibility` reaches every query, with the mixed-output example and its fix), `docs/manual/sql-files.md` (the marker index), `docs/manual/recipes.md` and `README.md` where they show attribute properties, `docs/manual/README.md`'s "What works today" if it names placement; `docs/tech-debt/TD-0089` checked for the names it uses.

- [ ] **Step 1:** Write the pages to the rules of root `AGENTS.md` rule 4: effects, never internals; `sql` samples name their dialect; no raw HTML in `README.md`.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Documentation`: pass.
- [ ] **Step 3: Format, validate, commit** `docs: member location, visibility and nested class are settings of the file`.

### Task 7: Full validation (the controller)

- [ ] **Step 1:** `./format.sh`, then `./format.sh --check`, then `./pre-commit-validation.sh --full` on the branch as it will be pushed: pass.
- [ ] **Step 2:** The describers are untouched; if Task 5 touched a describer's tests, run that project on its floor image as root `AGENTS.md` says.
- [ ] **Step 3:** Write `placement-notes.md` in the shared notes folder: the new ids, the removed types and steps, the harness's `memberLocation` rule.
