# TD-0006 - The conflict between two projects that share internals is hidden, not removed

## Problem

The generator adds `SqlSourceGenerateAttribute` and `SqlQueriesMode` to every project that references it, as internal types in the namespace `SqlSource`; see [`AttributeSource`](../../src/SqlSource/Generation/AttributeSource.cs).  When project A uses SqlSource and declares `InternalsVisibleTo` for project B, and B uses SqlSource too, B sees two copies of each type: its own and A's.  The compiler picks B's own and reports warning CS0436 at every use of `SqlSourceGenerate` and `SqlQueriesMode` in B's code.

The package hides that warning and leaves its cause in place.  [`AttributeConflictSuppressor`](../../src/SqlSource/Diagnostics/AttributeConflictSuppressor.cs) is a `DiagnosticSuppressor` that turns CS0436 off where it names one of the two types, and a `#pragma` in the generated file covers that file.  A compiler or an IDE that does not run suppressors still shows the warning, and a project that sets `TreatWarningsAsErrors` then fails to build.

## Why it exists

The epic decided to generate the attribute instead of shipping it in a runtime assembly, so that the package stays a development dependency with nothing in `lib/`.  The clean fix for the conflict is `AddEmbeddedAttributeDefinition`, which marks the generated types so that they are not visible across assemblies.  It needs Roslyn 4.14, and the support floor is Roslyn 4.8.0.  A suppressor works on 4.8.0.

## Impact

None where the suppressor runs.  It was checked in three ways:

- The driver tests in `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs` run it on Roslyn 4.8.0 and on a current version, with warnings as errors too.
- `dotnet build` of two such projects with `TreatWarningsAsErrors`, on the .NET SDKs 8.0.409 and 10.0.401, fails without the suppressor and succeeds with it.
- The same builds succeed with `RunAnalyzers` set to `false`: the compiler runs a suppressor even then.

It was not checked in a build on the first .NET 8 SDK, 8.0.100, whose compiler is Roslyn 4.8.0, nor in the live analysis of Visual Studio or Rider.  Where it does not run, the workaround is `<NoWarn>$(NoWarn);CS0436</NoWarn>` in the second project, which also hides the warning for genuine conflicts.  `README.md` says that the warning is turned off and does not give the workaround.

The suppressor is also code that exists only for this: it binds the name at each CS0436 in a project, and a type that is added to `AttributeSource` has to be added to it.

## Proposed fix

When the floor reaches Roslyn 4.14, call `AddEmbeddedAttributeDefinition` in the post-initialization step and mark both generated types with `[Microsoft.CodeAnalysis.Embedded]`.  Then delete `AttributeConflictSuppressor`, the `#pragma` in the generated file, the paragraph on `SQLSRC901` in `docs/diagnostics.md` and the section "Projects that share internals" in `README.md`.  Keep the tests of `AttributeConflictTests` that expect no CS0436, and make them pass with no analyzer loaded.

## Trigger

The Roslyn pin in `Directory.Packages.props` is raised to 4.14 or later.  A user reports CS0436 for `SqlSourceGenerate` or `SqlQueriesMode`.
