# TD-0006 - The conflict between two projects that share internals is hidden, not removed

## Problem

The generator adds `SqlSourceGenerateAttribute`, `SqlLocation`, `GeneratorOutput`, `GeneratorModelType`, `GeneratorCollectionType`, `MethodLocation`, `UnexpectedRowCountException`, `TransactionRolledBackException` and `SqlSourceConverterAttribute` to every project that references it, as internal types in the namespace `SqlSource`; see [`AttributeSource`](../../src/SqlSource/Generation/AttributeSource.cs).  It adds the library classes the query methods use, `SqlSource.NodaTimeConversions`, `SqlSource.NetTopologySuiteConversions` and `SqlSource.JsonConversions`, internal static classes, each to a project whose query methods call it: NodaTime's and NetTopologySuite's on SQL Server, System.Text.Json's on SQL Server and on Npgsql 8 and 9; see [`LibraryConverters`](../../src/SqlSource/Generation/Converters/LibraryConverters.cs).  When project A uses SqlSource and declares `InternalsVisibleTo` for project B, and B uses SqlSource too, B sees two copies of each type: its own and A's.  The compiler picks B's own and reports warning CS0436 at every use of one of them in B's code.

The two copies of `UnexpectedRowCountException` are two types, and so are those of `TransactionRolledBackException`, so the conflict also has an effect that no warning shows: a `catch (UnexpectedRowCountException)` in B binds to B's copy, and does not catch the exception that a generated method of A throws, which is A's.  B catches `InvalidOperationException`, from which all four derive, as the manual's [query methods](../manual/generator.md#unexpected-row-count) say.

The package hides the warning and leaves its cause in place.  [`AttributeConflictSuppressor`](../../src/SqlSource/Diagnostics/AttributeConflictSuppressor.cs) is a `DiagnosticSuppressor` that turns CS0436 off where it names one of the types of `AttributeSource.GeneratedTypes` or one of the classes of `LibraryConverters.ClassMetadataNames`, and a `#pragma` in the generated file covers that file.  A compiler or an IDE that does not run suppressors still shows the warning, and a project that sets `TreatWarningsAsErrors` then fails to build.

## Why it exists

The epic decided to generate the attribute instead of shipping it in a runtime assembly, so that the package stays a development dependency with nothing in `lib/`.  The clean fix for the conflict is `AddEmbeddedAttributeDefinition`, which marks the generated types so that they are not visible across assemblies.  It needs Roslyn 4.14, and the support floor is Roslyn 4.8.0.  A suppressor works on 4.8.0.

## Impact

None for the attribute and the enums where the suppressor runs.  For the exceptions, a project that sees another's internals must catch `InvalidOperationException` to catch the other's, which [the known issues](../manual/generator.md#known-issues) of `docs/manual/generator.md` say.  The suppressor was checked in three ways:

- The driver tests in `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` run it on Roslyn 4.8.0 and on a current version, with warnings as errors too.
- `dotnet build` of two such projects with `TreatWarningsAsErrors`, on the .NET SDKs 8.0.409 and 10.0.401, fails without the suppressor and succeeds with it.
- The same builds succeed with `RunAnalyzers` set to `false`: the compiler runs a suppressor even then.

It was not checked in a build on the first .NET 8 SDK, 8.0.100, whose compiler is Roslyn 4.8.0, nor in the live analysis of Visual Studio or Rider.  Where it does not run, the workaround is `<NoWarn>$(NoWarn);CS0436</NoWarn>` in the second project, which also hides the warning for genuine conflicts.  The known issues of [`docs/manual/generator.md`](../manual/generator.md#known-issues) give the workaround.

The suppressor is also code that exists only for this: it binds the name at each CS0436 in a project, and a type that is added to `AttributeSource` has to be added to `GeneratedTypes`, and a library's class of conversions to `ClassMetadataNames`, the two lists it reads.

## Proposed fix

When the floor reaches Roslyn 4.14, call `AddEmbeddedAttributeDefinition` in the post-initialization step and mark every generated type with `[Microsoft.CodeAnalysis.Embedded]`.  A library's class of conversions is not written in that step but with the methods that call it, so mark it `[Microsoft.CodeAnalysis.Embedded]` too, in the text `LibraryConverters.SourceOf` writes: deleting the suppressor with that class unmarked brings CS0436 back for it.  Then delete `AttributeConflictSuppressor` and the `#pragma` in the generated file.  An embedded `UnexpectedRowCountException` or `TransactionRolledBackException` is no longer seen across assemblies, so a project that sees another's internals still cannot catch the other's by its type: the known issue on catching them stays, reworded to that case alone.  In the user documentation, delete the paragraph on `SQLSRC901` in `docs/manual/diagnostics.md` and the known issue on CS0436 in `docs/manual/generator.md`, and rewrite the section "Projects that share internals" there, the recipe "test-project-sees-internals" in `docs/manual/recipes.md` and the FAQ item "My test project sees the internals of my main project" in `README.md` to say only that the two projects do not conflict.  Keep the tests of `AttributeConflictTests` that expect no CS0436, and make them pass with no analyzer loaded.

## Trigger

The Roslyn pin in `Directory.Packages.props` is raised to 4.14 or later.  A user reports CS0436 for `SqlSourceGenerate` or for one of the enums that `AttributeSource.GeneratedTypes` lists, or for one of the library classes that `LibraryConverters.ClassMetadataNames` lists, `SqlSource.NodaTimeConversions`, `SqlSource.NetTopologySuiteConversions` or `SqlSource.JsonConversions`.
