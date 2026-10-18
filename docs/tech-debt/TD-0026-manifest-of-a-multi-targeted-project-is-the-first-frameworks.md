# TD-0026 - The manifest of a project with several target frameworks is the first one's alone

## Problem

For a project with `TargetFrameworks`, the `sqlsource` tool passes the first of them as `TargetFramework`, to the evaluation that asks whether the project uses SqlSource and to the target that writes the project manifest: [`ProjectEvaluator`](../../src/SqlSource.Tool/Projects/ProjectEvaluator.cs).  So there is one manifest for the project, and it is that framework's.

- A `.sql` file or a `Compile` file that the project lists only under a condition on another framework is not in it.
- An attribute under `#if` for a constant that only another framework defines is not read.
- A project that references SqlSource for another framework alone does not use SqlSource at all, as the tool sees it: in a solution it is left out and nothing is said.

The queries those bring are never described.  The generator compiles for every framework, so the build of the other framework reports each of them whose output is not `Sql` as having no entry, `SQLSRC241`.

`tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` and `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs` pin the first two with the fixture `Multi`, and the third with `SecondOnly`.

## Why it exists

`AdditionalFiles` almost never differ by framework, and a manifest for each framework would multiply the MSBuild runs of every project that has several, for the few that differ.

## Impact

Low.  The gap is loud in the build, which reports each query that was not described: for a file or an attribute of another framework, and for the project that uses SqlSource in a later framework alone, of which the tool itself says nothing.

## Proposed fix

One evaluation and one manifest for each framework, and the plan of a run as the union of what they need: a query needs an entry when any framework's types claim it with an output that needs one.

## Trigger

A user reports a query that the tool does not describe and the build asks for, in a project with several target frameworks.
