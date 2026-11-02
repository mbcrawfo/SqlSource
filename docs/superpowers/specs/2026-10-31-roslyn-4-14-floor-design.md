# Roslyn 4.14 floor - design

## Intent

Visual Studio 2022 17.14 is the only Visual Studio 2022 still supported, and its compiler is Roslyn 4.14.  The .NET 8 and .NET 9 runtimes reach end of support on 2026-11-10.  Raise the generator's Roslyn floor from 4.8.0 to 4.14.0.  Then remove the problems we worked around because of 4.8.0:

- **TD-0006**, the CS0436 conflict between projects that share internals, which the package now hides with a suppressor.
- **The copy of `SqlSourceRowBuffer`** that every methods file holds.

The minimum requirements must be clear, both in `README.md` and in the build itself.

## Goals

1. `Microsoft.CodeAnalysis.CSharp` is pinned to 4.14.0.  The supported hosts are the .NET SDK 9.0.300 or later and Visual Studio 2022 17.14 or later.
2. A build on an older compiler stops with one clear SqlSource error that names the minimum SDK and Visual Studio.  It does not show the compiler's "could not load the generator" warning followed by missing-method errors.
3. TD-0006 is resolved.  The attribute, its enums, `SqlSourceConverterAttribute` and the library conversions classes are `[Embedded]`, and the suppressor, `SQLSRC901` and the `#pragma` are gone.
   - The two exceptions are not embedded, so code in another project can still catch them by type.  Instead, a project that already sees a copy of them in a referenced assembly reuses it and writes none of its own.
   - They stay internal by default.  The MSBuild property `SqlSourcePublicExceptions` makes them public.
4. The row buffer is one embedded internal type for each project, written only when a method of that project uses it.
5. The rules and comments that exist only because of 4.8.0 are updated or removed.
6. A new tech-debt item records the investigation into what else 4.14 offers.
7. `README.md` states the new minimums plainly.

## Non-goals

- **Adopting newer Roslyn APIs** beyond `AddEmbeddedAttributeDefinition`.  That includes pipeline, caching and allocation work, interceptors and the like.  The new tech-debt item covers it.
- **Rewriting existing code that works.**  For example, `ImmutableArray.Create` calls are not converted to collection expressions.  Only the rule that forbade those expressions goes.
- **Changing the target framework or language floor of consumer projects.**  They still target .NET 8 or later and compile C# 12 or later.  The compiler floor and the project floor are separate things.
- **Shipping several builds of the generator for different Roslyn versions** (`analyzers/dotnet/roslynX.Y/cs`).
- **Changing the tool.**  It already runs on a current Roslyn through `VersionOverride`.

## Anti-goals

These would make the change a failure even if it compiled:

- A build on an old compiler that fails with no SqlSource message saying why.
- CS0436 coming back for any generated type, whether the attribute, an enum, an exception, a library conversions class or the row buffer, except alongside the new error for conflicting copies (section 3).
- A project that sees another's internals losing the ability to catch that project's `UnexpectedRowCountException` or `TransactionRolledBackException` by type.  That works today for a project that does not use SqlSource itself.
- Any change to what a generated method returns or allocates.

## Constraints

- The always-added attribute file (`AttributeSource.Text`) compiles as C# 7.3, even in projects that SqlSource does not support.  It may gain `[global::Microsoft.CodeAnalysis.Embedded]` attributes, which are valid C# 7.3.  It must not gain the row buffer, which needs .NET 8 APIs such as `CollectionsMarshal.SetCount`.
- Every `.cs` file in `tests/SqlSource.Tests/Generator/` is compiled a second time by `tests/SqlSource.Tests.RoslynFloor`, against the pinned Roslyn.
- `src/SqlSource/build/AGENTS.md` sets two rules for the new check:
  - Every package property starts with `SqlSource`.
  - An SDK property the package files read is listed in `SdkProperties` in `tests/SqlSource.Tests/Package/BuildFileTests.cs`.
- The test in `tests/SqlSource.Tests/Diagnostics/` holds `docs/manual/diagnostics.md` and the descriptors equal.
- Repository rules apply: `AGENTS.md`, user docs that describe effects only, the tech-debt resolution steps, `pre-commit-validation.sh --full` before every push, and branch-review before the PR.
- `VersionPrefix` is 0.1.0, and the repository has no `v*` tags, so no version change is needed.

## Approaches

### Old compilers

| Option | What the user sees |
|---|---|
| **A. Build check (chosen)** | `build/SqlSource.targets` reads the SDK's `CompilerApiVersion` (for example `roslyn4.11`, `roslyn4.14`, `roslyn5.9`) and fails the build before `CoreCompile` with one SqlSource error when the version is below 4.14. |
| B. README only | The compiler's CS8032-style warning that the generator could not load, then "does not exist" errors at each call to a generated method. |

A was chosen because the failure under B is hard to trace to its cause, and A costs one target and one test.

Confirmed on this machine:

| SDK | `CompilerApiVersion` |
|---|---|
| 8.0.409 | `roslyn4.11` |
| 9.0.318 | `roslyn4.14` |
| 10.0.401 | `roslyn5.9` |

### The exceptions

| Option | Effect |
|---|---|
| A. Embed them with the rest | The simplest option.  A project that sees another's internals can no longer name that project's exceptions at all, even without SqlSource of its own. |
| **B. Reuse a referenced copy (chosen)** | A project writes the exceptions only when no referenced assembly gives it a copy it can see.  One type of each then serves the project and its friends, and a friend can catch it by type. |
| C. Replace them with `InvalidOperationException` | Ruled out.  Callers in the same project would lose the ability to tell a row-count error or a rollback apart from the drivers' own `InvalidOperationException`s, and would lose `QueryName` and the rollback's inner exception. |

The owner chose B.

- **A project that sees two copies.**  The owner accepts it as a known limitation and agreed it should be an error.  For example, an app sees the internals of two data projects that both use SqlSource.  The workaround is a shared project that uses SqlSource and grants `InternalsVisibleTo` to the others, which then all reuse its copy.
- **Visibility.**  The owner considered making the exceptions public by default and chose internal with a public opt-in.  With public by default, a NuGet library built with SqlSource would put the types in its public API.  An app that referenced two such libraries would then get ambiguous-type errors that it cannot fix.

### Where the row buffer is written

| Option | Effect |
|---|---|
| **A. Once per project, only when a kept method uses it (chosen)** | Written like the library conversions classes.  A project with no pooled `Many` method gets no extra type. |
| B. Once per project, always on a supported framework | A slightly simpler pipeline, but most projects get a type they never use. |
| C. Keep a copy in every file | The status quo, ruled out by the request. |

## The design

### 1. The floor

- In `Directory.Packages.props`, pin `Microsoft.CodeAnalysis.CSharp` to `4.14.0`.  Rewrite the comment above it: 4.14 is the compiler in the .NET SDK 9.0.300 and Visual Studio 2022 17.14, the oldest hosts supported.  Regenerate the affected `packages.lock.json` files.
- `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Testing` follow the pin with no other change.
- Replace "Roslyn 4.8.0" with "Roslyn 4.14.0" in the "Shared:" header of every file in `tests/SqlSource.Tests/Generator/`, and in the string that `TestLayoutTests` checks.
- Update the versions in `tests/README.md` and `tests/DatabaseTests/README.md`.

### 2. The build check

- Add a target in `build/SqlSource.targets` that runs before `CoreCompile`.  It reads `CompilerApiVersion`, takes the version after `roslyn`, and when that version is below 4.14 it raises an MSBuild `<Error>` with a new `SQLSRC` code.
- The message says that SqlSource needs the C# compiler of the .NET SDK 9.0.300 or later, or of Visual Studio 2022 17.14 or later, and names the version it found.
- **My call:** when `CompilerApiVersion` is empty or does not parse, the check does nothing.  A host we cannot identify is not blocked, and every SDK since .NET 6 sets the property.
- **My call:** the check has no opt-out.  On such a compiler the generator cannot load anyway.
- **Code (my call):** use the next free id in the project-setup range, beside `SQLSRC003` (framework) and `SQLSRC012` (language version).
  - Document it in `docs/manual/diagnostics.md` like any other diagnostic, and add it to `AnalyzerReleases.Unshipped.md` if the release tracking analyzer allows a code with no descriptor.
  - The diagnostics test compares the doc's sections with the descriptors.  Teach it that this code comes from MSBuild, with the smallest change that keeps the test strict for every other code.  If that is not clean, bring the choice back.
- **Tests:** in the package tests (`tests/SqlSource.Tests/Package/`):
  - a build with `CompilerApiVersion=roslyn4.11` fails with the code;
  - builds with `roslyn4.14` and `roslyn5.9` pass;
  - a build with the property empty passes.
- Add `CompilerApiVersion` to `SdkProperties`, and describe the target in `docs/architecture/msbuild-and-manifest.md`.

### 3. TD-0006

This follows the item's "Proposed fix" for every type except the two exceptions, which work as below.

- **Mark the types embedded.**
  - In the post-initialization step, call `context.AddEmbeddedAttributeDefinition()`.
  - Mark every type left in `AttributeSource.Text` with `[global::Microsoft.CodeAnalysis.Embedded]`: the attribute, its enums and `SqlSourceConverterAttribute`.
  - `LibraryConverters.SourceOf` marks each library's conversions class the same way.
- **The exceptions move out of the post-initialization text.**
  - `UnexpectedRowCountException` and `TransactionRolledBackException` move to a file of their own, written by a source output.  They keep the C# 7.3 form and are not embedded.
  - That file is written in every project, as today, including unsupported ones and projects with no queries.  The one exception is a project where a referenced assembly already gives it a usable copy.
  - Hint name (builder's call): on the `SqlSource-` pattern, distinct from the attribute's file.
- **When a reference gives a copy.**  A copy counts when the compilation can see it: it is public, or internal with `InternalsVisibleTo` for this assembly.
  - **One referenced assembly gives both exceptions, with the constructors the generated methods call.**  The project writes no exception file.  Its methods name `global::SqlSource.UnexpectedRowCountException` and so throw the referenced copy.  No CS0436 can occur, because no copy is declared in source.
  - **Two or more referenced assemblies give copies.**  Report a new `SQLSRC` error.
    - It names the assemblies and gives the workaround: a shared project that uses SqlSource and grants `InternalsVisibleTo` to the others.
    - The project still writes its own copy, so its generated code binds and the new error is the only error.  CS0436 warnings at its uses are accepted in this failing build (my call).
  - **The copy it sees has a different shape.**  This covers a constructor the methods call that is missing, or one exception present without the other, which happens when the other project uses another SqlSource version.
    - Report a second new `SQLSRC` error that names the assembly and says to use the same SqlSource version in both projects.
    - The project writes its own copy, as above (my call).
  - **Treat the two exceptions as a pair (my call).**  An assembly provides them only when it provides both.
  - **Performance.**  The check reads the compilation's references, so it runs from the compilation provider.  It must `Select` down to a small value-equal record before anything else depends on it, following `src/SqlSource/AGENTS.md`.  How it finds the types is the builder's call; for example, `Compilation.GetTypesByMetadataName` and `IsSymbolAccessibleWithin`.
- **Visibility.**  The exceptions are `internal` unless the project sets `<SqlSourcePublicExceptions>true</SqlSourcePublicExceptions>`, which makes both `public`.
  - The property is shown to the compiler as a `CompilerVisibleProperty`, under the rules of `src/SqlSource/build/AGENTS.md` and `BuildFileTests`.
  - It is project-wide, not a settings-file key, because a type's visibility is one value for the whole project.
  - How a value other than `true` or `false` is handled follows the package's existing boolean properties, if any.  Otherwise, anything but `true`, ignoring case, means internal (my call).
- **Known limitation, user's decision.**  A project with SqlSource that sees copies from two assemblies gets the new error.  The fix is the shared project.  A project without SqlSource that sees two copies gets the compiler's ambiguity error when it names the type; the same workaround applies.
- **Delete what only served the suppressor.**
  - Delete `AttributeConflictSuppressor` and `SQLSRC901`.
  - Delete the `#pragma warning disable CS0436` and its comment in `AttributeSource`.
  - Delete `AttributeSource.GeneratedTypes` and `LibraryConverters.ClassMetadataNames`, along with the `<summary>` references to them in the three conversions classes, unless something else reads them.
- **Tests.**
  - `AttributeConflictTests` keeps every test that expects no CS0436, and those tests now pass with no analyzer loaded.  That includes warnings-as-errors and two projects sharing internals with pooled methods.
  - Add a test that a library conversions class is not visible across the friend boundary.
  - Add a regression test that a converter library's methods marked with its own, now embedded, `[SqlSourceConverter]` are still found from a consuming project.  The reviewer checked that this works.
  - Exceptions:
    - a friend that uses SqlSource writes no exception file, and its code catches the other project's exception by type, including from that project's generated method;
    - a friend without SqlSource still catches by type;
    - a project that sees two copies gets the new error;
    - a copy of another shape gets the other new error;
    - with `SqlSourcePublicExceptions`, a consumer without `InternalsVisibleTo` catches by type;
    - with the property not set, the exceptions are internal.
  - Every test that checks this boundary runs on two cases: a compilation reference, as the IDE uses, and a PE reference, as a build uses.
  - Update `GeneratedSourceWarningTests` and `LibraryConvertersTests` where they name the suppressor or the lists.
- **New diagnostics.**  Each of the two errors gets a descriptor, a section in `docs/manual/diagnostics.md` and a row in `AnalyzerReleases.Unshipped.md`.  The id is the builder's choice, from the range of the project-setup errors.
- **User docs.**
  - Delete the `SQLSRC901` paragraph in `docs/manual/diagnostics.md`, and the known issue on CS0436 in `docs/manual/generator.md`.
  - Rewrite the known issue on catching the exceptions to say three things:
    - Code in another project catches them by type when it sees the copy, through `InternalsVisibleTo` or `SqlSourcePublicExceptions`.
    - A project that sees copies from two projects gets the error, and the shared-project workaround fixes it.
    - Otherwise, catch `InvalidOperationException`.
  - Rewrite these to say that the two projects do not conflict and that the friend reuses the other's exceptions:
    - the section "Projects that share internals" in `docs/manual/generator.md`;
    - the recipe "test-project-sees-internals" in `docs/manual/recipes.md`;
    - the FAQ item "My test project sees the internals of my main project" in `README.md`.
  - Add a recipe for the shared project, for two data projects used by one app, and link it from the known issue.
  - Document `SqlSourcePublicExceptions` where the manual documents the package's MSBuild properties, and add it to the settings table if that table lists MSBuild properties.
  - Add a known issue (my call): a project, or another package, that declares its own `Microsoft.CodeAnalysis.EmbeddedAttribute` in a form that clashes with Roslyn's fails to compile once it references SqlSource.  This is a limit of Roslyn's API, and SqlSource cannot detect it first.
- **Examples.**  If an example project shows catching the exceptions, check that it still builds, and add nothing more.
- **Developer docs.**
  - Rewrite "the suppressor" in `docs/architecture/generator.md` as two things: the embedded types, and the exceptions' reuse of a referenced copy with its errors and the visibility property.  Drop the suppressor from `docs/architecture/README.md`.
  - Describe `SqlSourcePublicExceptions` in `docs/architecture/msbuild-and-manifest.md`.
  - Update `src/SqlSource/Diagnostics/AGENTS.md` and `src/SqlSource/Generation/AGENTS.md` where they name the suppressor.
- **Resolve the item:**
  - delete its file;
  - remove its row from `docs/tech-debt/README.md`;
  - remove every reference to `TD-0006` (`grep -r TD-0006`).

### 4. The row buffer

- **The type.**
  - It is `internal struct SqlSourceRowBuffer<T> where T : class` in namespace `SqlSource`, marked `[global::Microsoft.CodeAnalysis.Embedded]`.
  - It is written once per compilation, in its own file named on the `SqlSource-` pattern of the library classes (for example `SqlSource-RowBuffer.g.cs`).  No type, model or methods file can take that name.
  - Its body is the one `RowBuffer` writes today, unchanged.  The file starts with `GeneratedText.Header`, at C# 12, as a type's file does.
- **When it is written.**  Only when at least one kept method of the project satisfies `RowBuffer.IsUsedBy`.
  - That flag is a collected value with a `Select` to a `bool` after its `Collect()`, as the pipeline rules require, so an edit that does not change the answer does not rewrite the file.
  - It must be computed from the same kept methods that `MethodEmitter.Emit` writes.  A method dropped for a conflict must not be the only reason the buffer exists.
  - It must not be written in a project whose framework or language version is unsupported, since no methods are written there.
  - Where in the pipeline it goes is the builder's call, following `src/SqlSource/AGENTS.md`.
- **The methods.**  They name it `global::SqlSource.SqlSourceRowBuffer<Row>`.
  - Remove `RowBuffer.NameFor`, `RowBuffer.OtherName` and `RowBuffer.TypeTextFor`.
  - Remove the per-file `RowBuffer.Append` call and the `usesRowBuffer` scan in `MethodEmitter.Emit`.
  - The methods file no longer holds the struct.
- **My call:** keep the name `SqlSourceRowBuffer`.  It sits in namespace `SqlSource` beside the attribute's types, so only a user type `SqlSource.SqlSourceRowBuffer` in the same project could clash.  That is the same exposure the attribute's types already have.
- **Tests (`RowBufferTests`, generator and emitting).**
  - "writes the buffer once" becomes "the project gets one buffer file however many files use it".
  - "a file with no pooled method has no buffer" becomes "a project with no pooled method has no buffer file".
  - The test of a type named like the buffer now checks that such a user type, in its own namespace, compiles beside the buffer.
  - `Run_TypeInTheGlobalNamespaceOrUnderAnEscapedKeyword_NamesTheBufferFromGlobal` now expects `global::SqlSource.SqlSourceRowBuffer<`.
  - `AttributeConflictTests.Build_TwoProjectsSharingInternalsWithPooledMethods_HaveNoConflict` stays.
  - Add a caching test that an edit which leaves the answer unchanged does not rerun the buffer's output.
- **Docs.**  Rewrite "The row buffer" in `docs/architecture/generator.md`: where it is written and why it is embedded.
  - The `object[]` and pool reasoning stays.
  - The paragraph "Why `file`" becomes why it is one embedded type.

### 5. Other 4.8.0 workarounds

- **`src/SqlSource/AGENTS.md`**
  - The pin rule names 4.14.0 and its hosts.
  - The rule "No collection expression may target `ImmutableArray<T>`" is removed.  The System.Collections.Immutable that 4.14 brings supports it.  Confirm by compiling one such expression in `src/SqlSource` before removing the rule.
  - The rule against referencing `System.Memory` and its neighbours names 4.14.0.
- **`tests/SqlSource.Tests/Generator/AGENTS.md`:** a test uses only API that Roslyn 4.14.0 has.
- **`ConverterReader.cs:159` comment:** check whether 4.14 names the C# 14 extension block's kind.  If it does not, change the version in the comment; if it does, use the kind.
- **TD-0067** says 4.8.0 has no public call for nullability-preserving conversion.  Check 4.14.  If it still has none, change the version in the item; if it has one, say so in the item and leave the fix to it.
- **Search for leftovers.**  Search again for `4.8`, `4\.8\.0`, `17.8` and `.NET 8 SDK` across the repository, excluding `bin`, `obj`, lock files and `docs/superpowers/`.  Update anything that describes the floor.

### 6. New tech-debt item

`TD-0094`, the next id, titled along the lines of "Roslyn 4.14's features are not used yet".

- **Problem:** the generator was written against 4.8.0, and only `AddEmbeddedAttributeDefinition` was adopted when the floor rose.
- **Why it exists:** the floor raise was kept to removing the 4.8.0 workarounds.
- **Impact:** possibly slower or more allocating pipeline steps than needed.  Low.
- **Proposed fix:** go through the public API added to `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` between 4.8 and 4.14, and the generator's own language features.  List what applies to the pipeline's caching and allocations, then measure each candidate against the allocation budgets before adopting it.  Check TD-0069 and TD-0013 against the same list.
- **Trigger:** the next performance work on the generator, or the next floor raise.

Add its row to the README with status Open and impact Low.  It has no user-visible effect, so it gets no known issue.

### 7. README and other user docs

- **The "Supported environments" paragraph of `README.md`** says plainly:
  - a project targets .NET 8 or later and compiles C# 12 or later;
  - it builds with the .NET SDK 9.0.300 or later, or in Visual Studio 2022 17.14 or later;
  - every .NET 8 SDK and Visual Studio before 17.14 are not supported, and a build on them stops with an error that names the minimum.
  - Keep the existing sentences about the tool, the databases and the drivers.
- Update the "What works today" table in `docs/manual/README.md`, the manual page that lists requirements, and `src/SqlSource.Tool/README.md`, where they state the SDK or IDE floor.
- `CONTRIBUTING.md` is unchanged unless it names 4.8.0; its line about RoslynFloor is generic.

## Left to the builder

- Where the buffer flag is computed in the pipeline.
- How the exceptions' reference check is computed, its record's shape, and the hint name of the exceptions' file.
- The ids of the three new diagnostics (the build check and the two exception errors).
- The exact wording of messages and docs, within the rules of `AGENTS.md`.
- How the diagnostics test learns about an MSBuild-only code, unless no clean way exists.

## Validation

- `./pre-commit-validation.sh --full` passes on the tree as pushed.
- The RoslynFloor project passes on 4.14.0.
- branch-review comes back clean before the PR.
