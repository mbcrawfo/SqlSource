# TD-0006 - The generated attribute conflicts between two projects that share internals

## Problem

The generator adds `SqlQueriesAttribute` and `SqlQueriesMode` to every project that references it, as internal types in the namespace `SqlSource`; see [`AttributeSource`](../../src/SqlSource/Generation/AttributeSource.cs).  When project A uses SqlSource and declares `InternalsVisibleTo` for project B, and B uses SqlSource too, B sees two copies of each type: its own and A's.  The compiler picks B's own and reports warning CS0436 at every use of the attribute.  A project that sets `TreatWarningsAsErrors` then fails to build.

## Why it exists

The epic decided to generate the attribute instead of shipping it in a runtime assembly, so that the package stays a development dependency with nothing in `lib/`.  The clean fix for the conflict is `AddEmbeddedAttributeDefinition`, which marks the generated types so that they are not visible across assemblies.  It needs Roslyn 4.14, and the support floor is Roslyn 4.8.0.

## Impact

A test project that has `InternalsVisibleTo` access to the project it tests is the usual way to meet this: if both use SqlSource, the test project gets CS0436 for each `[SqlQueries]`.  The workaround is `<NoWarn>$(NoWarn);CS0436</NoWarn>` in the second project, which also hides the warning for genuine conflicts.  Nothing in `README.md` mentions it.

## Proposed fix

1. Until the floor moves, describe the warning and the workaround in `README.md`.
2. When the floor reaches Roslyn 4.14, call `AddEmbeddedAttributeDefinition` in the post-initialization step and mark both generated types with `[Microsoft.CodeAnalysis.Embedded]`.  Add a driver test with two compilations, the second referencing the first with `InternalsVisibleTo`, that asserts no CS0436.

## Trigger

The Roslyn pin in `Directory.Packages.props` is raised to 4.14 or later.  A user reports CS0436.
