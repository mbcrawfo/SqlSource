# TD-0086 short name binds to a package's type: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0086 with a new warning, `SQLSRC284`, reported when a short name in a type override, a column type or a `-- param:`/`-- column:` marker finds a type of an assembly that the project has only through a NuGet package it does not reference itself.

**Architecture:** A new target of `build/SqlSource.targets` shows the compiler a second property, `SqlSourceTransitivePackageAssemblies`: the file names of the `ReferencePath` items that come from a package (`NuGetPackageId` set, no `FrameworkReferenceName`, not a `ProjectReference`) whose id is not among the project's own `PackageReference` items.  The generator reads it into a `TransitivePackageAssemblies` value, the resolver marks a `MappedType` whose short name found a type of one of those assemblies, and `QueryTyper` adds `SQLSRC284` at the query for each column and parameter whose type it chose, without failing it: models and methods are generated as before.

**Tech Stack:** C# (netstandard2.0, Roslyn 4.8.0), MSBuild, xunit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0086-short-name-binds-to-a-referenced-packages-type.md`, with the owner's answers (run `notes.md`, Decisions, td-0086): Q1 approach A (package-brought assembly, the AGENTS rule and test of one compiler-visible property changed to allow a second), Q2 a configurable warning, Q3 `SQLSRC284`, Q4 both off-switches: the full name, which the message says, and `NoWarn` or an analyzer config severity.

## Global Constraints

- Generator: `netstandard2.0` only, Roslyn pinned at 4.8.0, no collection expression targeting `ImmutableArray<T>`, records with `EquatableArray<T>` for anything the pipeline caches, no Roslyn object between steps, nothing a user writes may make the generator throw (`src/SqlSource/AGENTS.md`).
- Every MSBuild property and item type of the package starts with `SqlSource` (`src/SqlSource/build/AGENTS.md`); an SDK property read keeps its name and is listed in `SdkProperties` of `BuildFileTests`.
- The descriptor is written out in full with a literal id; it is `DiagnosticSeverity.Warning` and has no `NotConfigurable` tag, with a comment above it saying why a project may configure it, as `SQLSRC240`'s does (`src/SqlSource/Diagnostics/AGENTS.md`).
- Adding the diagnostic touches four places: the descriptor and `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, and `SqlDiagnosticsTests`' `Configurable` list (`SQLSRC284` → `Warning`).
- User documentation describes effects, never internals (root `AGENTS.md` rule 4): `docs/manual/` never names the target, the property, `ReferencePath` or a TD id.
- Resolving follows `docs/tech-debt/AGENTS.md`: delete the item file, its row in `docs/tech-debt/README.md`, every `TD-0086` outside plan and spec history, and the known issue in `docs/manual/settings.md`.
- `docs/manual/diagnostics.md` and `docs/tech-debt/README.md` are also edited by td-0077 beside this unit: keep to the `SQLSRC284` section and the TD-0086 row.
- Every commit passes `./pre-commit-validation.sh`; run the formatter, `./format.sh`, before it.

## Review Focus

1. A short name that is a type argument, `System.Collections.Generic.List<AccountId>`, binds to a package's type: warns, naming `AccountId` and its full name, not the outer type.  Test in Task 2 and Task 3.
2. An entry for an array's element (`uuid=AccountId` read by a `uuid[]` column) or a domain over the type: the array's property gets the type and the warning.  Test in Task 3.
3. A package referenced both directly and through another package (`Microsoft.Data.SqlClient` listed and brought by another), or a `PackageReference` whose id differs in case from the package's: no warning.  Test in Task 1.
4. A build that gives no property, an empty one, or one with an empty or garbled entry (`||`, a stray `%`): no warning, no throw.  Test in Task 1.
5. A query whose output is `models`, not `codegen`, warns too; one whose output is `sql` does not, since it is not typed.  Test in Task 3.

---

### Task 1: The build's list of package-brought assemblies

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets` (a new target after `SqlSourceFindBuildFolders`)
- Modify: `src/SqlSource/build/SqlSource.props` (a second `CompilerVisibleProperty`, and its comment)
- Create: `src/SqlSource/Generation/Types/MappedTypes/TransitivePackageAssemblies.cs`
- Modify: `src/SqlSource/Generation/Chains/BuildFolders.cs` (share its `Unescape`, made `internal`)
- Modify: `src/SqlSource/AGENTS.md`, `src/SqlSource/build/AGENTS.md` (the one-property rules become two)
- Modify: `docs/architecture/msbuild-and-manifest.md` (the build's folders section gains the new property)
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Projects/PackageAssemblies/PackageAssemblies.csproj`
- Create: `tests/SqlSource.Tool.Tests/Projects/TransitivePackageAssembliesTargetTests.cs`
- Create: `tests/SqlSource.Tests/Generation/Types/MappedTypes/TransitivePackageAssembliesTests.cs`
- Modify: `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Produces: MSBuild property `SqlSourceTransitivePackageAssemblies`, set by target `SqlSourceFindTransitivePackageAssemblies` with `BeforeTargets="GenerateMSBuildEditorConfigFileCore"` and no `DependsOnTargets` (a build resolves its references before that target; run by name with nothing resolved, the property is empty, so no warning, which is the safe side).  Value: each assembly's `%(Filename)` escaped as `SqlSourceFindBuildFolders` escapes a folder (`%`, `|`, `#`, `;`), joined by `|`, each once.
- Produces: `internal sealed record TransitivePackageAssemblies(EquatableArray<string> Names)` with `const string PropertyName = "SqlSourceTransitivePackageAssemblies"`, `static TransitivePackageAssemblies None`, `static TransitivePackageAssemblies Read(AnalyzerConfigOptions globalOptions)`, `static TransitivePackageAssemblies Of(string? value)` and `bool Contains(string assemblyName)`, compared `StringComparer.OrdinalIgnoreCase`.  `Names` sorted and distinct ignoring case, so that two builds of one list are equal.

- [ ] **Step 1: Write the failing target tests** in `TransitivePackageAssembliesTargetTests`, each running `FixtureProjects.RunMSBuildAsync(project, ["-t:SqlSourceFindTransitivePackageAssemblies", "-getProperty:SqlSourceTransitivePackageAssemblies"])` on a copy of the `PackageAssemblies` fixture, which imports the package's props and targets as the other fixtures do, lists `<PackageReference Include="Direct.Package" />` and `<PackageReference Include="mixed.case.package" />`, and declares static `ReferencePath` items:

  | Item | `NuGetPackageId` | `FrameworkReferenceName` | `ReferenceSourceTarget` |
  |---|---|---|---|
  | `lib/Direct.dll` | `Direct.Package` | | `ResolveAssemblyReference` |
  | `lib/Mixed.dll` | `Mixed.Case.Package` | | `ResolveAssemblyReference` |
  | `lib/Brought.Client.dll` | `Brought.Client` | | `ResolveAssemblyReference` |
  | `lib/Brought.Client.Extra.dll` | `Brought.Client` | | `ResolveAssemblyReference` |
  | `ref/System.Runtime.dll` | `Microsoft.NETCore.App.Ref` | `Microsoft.NETCore.App` | `ResolveAssemblyReference` |
  | `../Other/Other.dll` | `Other` | | `ProjectReference` |
  | `lib/Plain.dll` | | | `ResolveAssemblyReference` |

  - `Run_ReferencesOfAPackageNotListed_AreTheValue`: the value split at `|` is exactly `["Brought.Client", "Brought.Client.Extra"]`.
  - `Run_PackageListedInAnotherCase_IsNotInTheValue`: no entry is `Mixed`.
  - `Run_ProjectWithNoReferences_HasAnEmptyValue`: on the `Single` fixture, the value is `""`.
  - Add a `ReferencePath` `lib/Odd#Name.dll` of package `Brought.Client` to one copy: `Run_AssemblyNameWithACharacterTheCompilerCuts_IsEscaped` asserts the value holds `Odd%23Name`.

- [ ] **Step 2: Run them to see them fail**

  Run: `dotnet test tests/SqlSource.Tool.Tests -- --filter-class '*TransitivePackageAssembliesTargetTests'` (or the filter syntax `CONTRIBUTING.md` gives).
  Expected: FAIL, the target does not exist (MSB4057).

- [ ] **Step 3: Write the target** in `SqlSource.targets`, with an XML comment above it in the file's style saying what it gives the compiler and why it has no `DependsOnTargets`.  Item types it makes start with `SqlSource`; it never passes item metadata to a property function (`Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction`).  Filter `ReferencePath` by its metadata into a `SqlSource…` item, transform it to the package id, `Remove="@(PackageReference)"` (if the removal turns out to be case-sensitive on macOS, compare with `MatchOnMetadata` or a lower-cased copy, so the mixed-case test passes), then join the remaining `%(Filename)`s, escaped, with `|`.  Add `<CompilerVisibleProperty Include="SqlSourceTransitivePackageAssemblies" />` to the props' `ItemGroup` and widen its comment to say what the second property is and that it is no setting either.

- [ ] **Step 4: Run the target tests to see them pass.**  Same command; expected PASS.

- [ ] **Step 5: Update `BuildFileTests`**: `Props_OnlyPropertyTheCompilerIsShown_IsTheBuildsFolders` becomes `Props_PropertiesTheCompilerIsShown_AreTheBuildsFoldersAndThePackageAssemblies`, asserting exactly `["SqlSourceBuildFolders", "SqlSourceTransitivePackageAssemblies"]`; the escaping test of the build's folders gets a sibling asserting the new target's value uses the same four `Replace` calls.  Add any SDK property the target reads to `SdkProperties`.  Run `dotnet test tests/SqlSource.Tests -- --filter-class '*BuildFileTests'`; expected PASS.

- [ ] **Step 6: Write the failing tests of `TransitivePackageAssemblies`**:
  - `Of_Null_IsNone`, `Of_Empty_IsNone`: equal to `None`, `Names.Count == 0`.
  - `Of_Entries_AreDecodedSortedAndDistinct`: `Of("Zeta|Odd%23Name|zeta||Alpha")` has `Names` `["Alpha", "Odd#Name", "Zeta"]` (first spelling kept).
  - `Contains_IgnoresCase`: `Of("Microsoft.Identity.Client").Contains("microsoft.identity.client")` is true; `Contains("Microsoft.Identity")` false.
  - `Of_EntryWithABrokenEscape_IsKeptAsWritten` or left out, whichever `BuildFolders.Unescape` does; never throws.
  - `Read_GlobalOption_IsTheProperty`: `build_property.SqlSourceTransitivePackageAssemblies` read through a test `AnalyzerConfigOptions`, as `BuildFoldersTests` does.

- [ ] **Step 7: Run them to see them fail**, then implement `TransitivePackageAssemblies` with `BuildFolders.Unescape` made `internal static`, and run them to see them pass: `dotnet test tests/SqlSource.Tests -- --filter-class '*TransitivePackageAssembliesTests'`.

- [ ] **Step 8: Update the docs of this task.**  `src/SqlSource/AGENTS.md`: "The build shows the compiler two properties, `SqlSourceBuildFolders` and `SqlSourceTransitivePackageAssemblies`, and no item metadata; a setting is never a property or metadata of the build."  `src/SqlSource/build/AGENTS.md`: the rule "The props show the compiler one property…" names both and says `SqlSourceFindTransitivePackageAssemblies` sets the second; one more bullet: its escaping and `TransitivePackageAssemblies.Of` change together.  `docs/architecture/msbuild-and-manifest.md`: a short subsection after the build's folders: which references are listed, why (a short name's binding, `SQLSRC284`), why no `DependsOnTargets`, and that the manifest does not carry it, since the tool does not type.

- [ ] **Step 9: Format, validate and commit**

```bash
./format.sh
./pre-commit-validation.sh
git add -A && git commit -m "Show the compiler the assemblies that only a package brings"
```

### Task 2: The resolver marks a short name that found a package's type

**Files:**
- Create: `src/SqlSource/Generation/Types/MappedTypes/PackageBinding.cs`
- Modify: `src/SqlSource/Generation/Types/MappedTypes/MappedType.cs`, `MappedTypeResolver.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (the `resolvedTypes` step), `src/SqlSource/Generation/TrackingNames.cs`
- Modify: callers of `MappedTypeResolver.Resolve` and of `new MappedType(` in `tests/`
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeResolverTests.cs`, `tests/SqlSource.Tests/Generator/CachingMappedTypeTests.cs`

**Interfaces:**
- Consumes: `TransitivePackageAssemblies` (Task 1).
- Produces: `internal sealed record PackageBinding(string ShortName, string FullName, string Assembly)`: the short name as written with its arity, `AccountId` or `Range<int>`'s `Range`; the full name of the type it found as a user writes it, from `FullNames` (`Microsoft.Identity.Client.AccountId`); the assembly's `Identity.Name`.
- Produces: `MappedType` gains a last positional parameter `PackageBinding? Package`: for a found name, the first short name in it, the name's own before its arguments' in order, whose type's assembly `TransitivePackageAssemblies.Contains`; null for a name with no such short name, a full name, a keyword, a type of the project, or a name that is not found.
- Produces: `MappedTypeResolver.Resolve(Compilation compilation, EquatableArray<string> names, TransitivePackageAssemblies packageAssemblies, CancellationToken cancellationToken)`.
- Produces: tracking name `TrackingNames.TransitivePackageAssemblies`, the `AnalyzerConfigOptionsProvider` select that reads it, combined into `resolvedTypes`.

- [ ] **Step 1: Write the failing resolver tests**, a library compiled to a metadata reference whose assembly is named `Brought.Client` with `namespace Brought.Client { public sealed class AccountId { } public sealed class Range<T> { } }`, and `TransitivePackageAssemblies.Of("Brought.Client")`:
  - `Resolve_ShortNameOfATransitivePackagesType_HasItsBinding`: `AccountId` is `Found`, `Package` is `new PackageBinding("AccountId", "Brought.Client.AccountId", "Brought.Client")`.
  - `Resolve_FullNameOfATransitivePackagesType_HasNoBinding`: `Brought.Client.AccountId` → `Package` null.
  - `Resolve_ShortNameOfADirectReferencesType_HasNoBinding`: same library, `TransitivePackageAssemblies.None` → null.
  - `Resolve_ShortNameAsATypeArgument_HasTheArgumentsBinding`: `System.Collections.Generic.List<AccountId>` → `Package.ShortName == "AccountId"`.
  - `Resolve_AssemblyNameInAnotherCase_HasItsBinding`: `Of("brought.client")` → binding set.
  - `Resolve_ShortNameNotFound_HasNoBinding`: `Missing` → `NotFound`, null.

- [ ] **Step 2: Run them to see them fail** (compile errors: no such parameter).  `dotnet test tests/SqlSource.Tests -- --filter-class '*MappedTypeResolverTests'`.

- [ ] **Step 3: Implement.**  In `ResolveType`'s short-name case 1, after `Find`, record the binding when `type.ContainingAssembly` is not the compilation's and its `Identity.Name` is in the set; carry it through `Resolution` and take the first from the arguments' resolutions when the name's own is null.  A failed name gives null.  Update every caller; the generator's `resolvedTypes` step becomes `CompilationProvider.Combine(mappedTypeNames).Combine(transitivePackageAssemblies)`, the latter `AnalyzerConfigOptionsProvider.Select(static (options, _) => TransitivePackageAssemblies.Read(options.GlobalOptions)).WithTrackingName(TrackingNames.TransitivePackageAssemblies)`.  Update the `<remarks>` of `MappedTypeResolver` and the `<param>` docs of `MappedType`.

- [ ] **Step 4: Add the caching test** `Run_CSharpEdit_ReadsTheSamePackageAssembliesAndResolvesToAnEqualValue` in `CachingMappedTypeTests`: with the global option set and a short name bound to a package's type, an edit of a C# comment gives `TransitivePackageAssemblies` `Unchanged` (or `Cached`) and `MappedTypes` `Unchanged`, by `StepReasons` as the class's other tests read them.

- [ ] **Step 5: Run them to see them pass**: the resolver tests, `CachingMappedTypeTests`, `ShortNameTests`.

- [ ] **Step 6: Format, validate and commit**: `git commit -m "Mark a short name that finds a type only a package brings"`.

### Task 3: SQLSRC284 at the query

**Files:**
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/Diagnostics/AGENTS.md`
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs`, `src/SqlSource/Generation/Types/Maps/TypeMapping.cs` (or wherever `TypeMapping` is declared) and the override lookup that builds an array's or a domain's mapping from its element's
- Modify: `docs/manual/diagnostics.md` (a `SQLSRC284` section, in id order)
- Test: `tests/SqlSource.Tests/Generator/ShortNameTests.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `MappedType.Package` (Task 2).
- Produces: `SqlDiagnostics.ShortNameFindsAPackagesType`, id `SQLSRC284`, title `Short name finds a type that only a package brings`, category `SqlDiagnostics.Category`, `DiagnosticSeverity.Warning`, enabled, help `HelpLinkBase + "sqlsrc284"`, no custom tags, message format exactly:

  `'{1}' is mapped to '{2}' for {0}, in {3}, and the short name '{4}' finds '{5}', of the assembly {6}, which this project has only through a package that it does not reference itself.  If that is the type meant, write its full name, '{5}'; otherwise correct the name.`

  `{0}` the subject (`column 'account_id'`), `{1}` what is mapped (the key or `@author`), `{2}` the C# type as a user writes it, `{3}` the place, `{4}` `PackageBinding.ShortName`, `{5}` `FullName`, `{6}` `Assembly`; `{0}` to `{3}` exactly as `SQLSRC250` fills them.
- Produces: `TypeMapping` carries the `PackageBinding?` of a mapped type, and a mapping built from an element's or a base's keeps it.

- [ ] **Step 1: Write the failing driver tests** in `ShortNameTests`, with a library `Brought.Client` as in Task 2 given to the run as a metadata reference and the global option `build_property.SqlSourceTransitivePackageAssemblies = Brought.Client` (extend the harness's options if it has no way to set a global option; `BuildFolders`' driver tests show how):
  - `Run_ShortNameOfATransitivePackagesType_IsSqlsrc284AndKeepsTheType`: override `int8=AccountId`; one diagnostic, id `SQLSRC284`, severity `Warning`, at the query's name, message exactly `'int8' is mapped to 'AccountId' for column 'id', in <Place>, and the short name 'AccountId' finds 'Brought.Client.AccountId', of the assembly Brought.Client, which this project has only through a package that it does not reference itself.  If that is the type meant, write its full name, 'Brought.Client.AccountId'; otherwise correct the name.`; the model still holds `global::Brought.Client.AccountId Id`.
  - `Run_FullNameOfATransitivePackagesType_IsClean`: override `int8=Brought.Client.AccountId` → `ShouldBeClean()`.
  - `Run_ShortNameOfADirectReferencesType_IsClean`: no global option → clean.
  - `Run_ShortNameInATypeArgument_IsSqlsrc284ForTheArgument`: `int8=System.Collections.Generic.List<AccountId>` is not a valid property for `int8` reading, so use a `-- column:` marker on a `text[]` or the shape `ShortNameTests` already uses for arguments; the message's `{4}` is `AccountId`.
  - `Run_ArrayColumnOfAnElementsEntry_IsSqlsrc284`: entry `int8=AccountId`, column of `int8[]` → one `SQLSRC284`, subject the column.
  - `Run_ColumnMarkerAndParamMarker_EachIsSqlsrc284`: a `-- column: id -> AccountId` and a `-- param: @id AccountId` → two, subjects `column 'id'` and `parameter '@id'` with their places.
  - `Run_CodeGenQuery_IsSqlsrc284AndGetsItsMethod`: output `codegen`; the warning, and the method is generated.
  - `Run_SqlOutputQuery_IsClean`: output `sql` → clean.

- [ ] **Step 2: Run them to see them fail.**  `dotnet test tests/SqlSource.Tests -- --filter-class '*ShortNameTests'`.

- [ ] **Step 3: Add the descriptor** after `SQLSRC280`'s, with a comment above it: its arguments as `SQLSRC250`'s plus `{4}` to `{6}`, and why it is a warning a project may configure (the type may be meant; a package update that brings a new assembly should not break a build; the full name or `NoWarn` turns it off).  Add it to `All`; add `SQLSRC284 | SqlSource | Warning | Short name finds a type that only a package brings` to `AnalyzerReleases.Unshipped.md`; add `["SQLSRC284"] = DiagnosticSeverity.Warning` to `SqlDiagnosticsTests.Configurable`.  `src/SqlSource/Diagnostics/AGENTS.md`: the taken ids become "`SQLSRC200` to `SQLSRC280` and `SQLSRC284` are taken, and phase 7 takes `SQLSRC281` to `SQLSRC283`, which its spec … assigns".

- [ ] **Step 4: Report it.**  Where `QueryTyper` accepts a `Mapped` mapping whose place is set (`Chosen`, and `TypeOf` for an override's), add the warning to `typing.Diagnostics` and still return the `PropertyType`.  Carry `PackageBinding?` on `TypeMapping` from `MappingOf`, and keep it where the map builds an array's or a domain's mapping from its element's or base's.  A warning must not count as the query's failure: check that whatever reads `typing.Diagnostics` to decide that a query has no types reads errors only, and change that test if it does not.

- [ ] **Step 5: Write the `SQLSRC284` section of `docs/manual/diagnostics.md`**, in the shape of `SQLSRC240`'s: the title in bold; what it means in a user's words (a short name found a type of an assembly that the project has only because a package it references brings it, so it may not be the type meant); a `console` sample of the message from Step 1's first test, with a `.sql` path and position as the other samples have; the fix (write the full name, or correct the name, or reference the package itself if the type is meant); and that it is a warning, turned off with `NoWarn` or kept a warning with `WarningsNotAsErrors`, with the `xml` snippet `SQLSRC240` uses, and with `dotnet_diagnostic.SQLSRC284.severity = none` in a global analyzer config only if Task 4 Step 5 shows it works.  Never name the target, the property or the TD id.

- [ ] **Step 6: Run the tests to see them pass**: `ShortNameTests`, `SqlDiagnosticsTests`, `DiagnosticsDocumentTests`, `QueryTyperTests`, `TypeDiagnosticsTests`.

- [ ] **Step 7: Format, validate and commit**: `git commit -m "Report SQLSRC284 for a short name that finds a package's type"`.

### Task 4: Docs, the item's removal and the examples

**Files:**
- Modify: `docs/manual/settings.md` (the short-name bullet of type overrides; the known issue removed)
- Modify: `docs/architecture/generator.md` (the resolution of mapped types; the first-failure paragraph, which gains the warning beside the errors; the caching list)
- Delete: `docs/tech-debt/TD-0086-short-name-binds-to-a-referenced-packages-type.md`
- Modify: `docs/tech-debt/README.md` (the TD-0086 row only)
- Modify, only if the warning fires in them: `examples/`

- [ ] **Step 1: `settings.md`.**  Delete the known issue "A short name can name a type of a package your project did not mean."  In the type overrides' bullet "A short name is the one type of that name…", add one sentence: a short name that finds a type of an assembly your project has only through a package it does not reference itself is the warning [SQLSRC284](diagnostics.md#sqlsrc284), since it may not be the type you meant; write the full name to use it without the warning.  Check `README.md`'s short-name sentence (line ~263) still holds; it does unless it claims no warning.

- [ ] **Step 2: `generator.md`.**  In "The resolution of mapped types", one or two sentences: the step reads `TransitivePackageAssemblies` beside the compilation, and a short name whose type's assembly it holds gives the record its `Package`.  In the typing section, `SQLSRC284` is added for each column and parameter whose chosen type has one, a warning that leaves the query typed.  In the caching paragraph, the new test of `CachingMappedTypeTests`.

- [ ] **Step 3: Resolve the item.**  Delete the file, its row, and run `grep -rn "TD-0086" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj`: only `docs/superpowers/plans/` remains.

- [ ] **Step 4: Build the solution and the examples**: `dotnet build` at the root.  Expected: no `SQLSRC284`.  If one fires in `examples/`, write the full name there and say so in the hand-back.

- [ ] **Step 5: Check the original case by hand** (not a committed test): in a scratch copy of `tests/DatabaseTests/SqlServer.SqlClient61`, link a query whose column is mapped to `AccountId`; build; expected `warning SQLSRC284` naming `Microsoft.Identity.Client.AccountId` and the assembly `Microsoft.Identity.Client`.  Then add `<NoWarn>$(NoWarn);SQLSRC284</NoWarn>`: no warning.  Then, instead, a `.globalconfig` with `is_global = true` and `dotnet_diagnostic.SQLSRC284.severity = none`: record whether it turns it off, which decides the sentence of Task 3 Step 5.  Revert the scratch changes.

- [ ] **Step 6: Format, validate and commit**: `git commit -m "Resolve TD-0086"`.
