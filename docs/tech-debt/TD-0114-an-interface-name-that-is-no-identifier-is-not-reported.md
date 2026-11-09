# TD-0114 - An interface name that is no identifier is not reported

## Problem

`[SqlSourceGenerate(InterfaceName = "...")]` takes any string that is not empty.  [`TargetTypeReader`](../../src/SqlSource/Generation/TargetTypeReader.cs) reads it as it is, and [`MethodEmitter`](../../src/SqlSource/Generation/Emitting/MethodEmitter.cs) writes it into the repository's interface and the base list of the type's generated part, with an `@` before a keyword.  A value that is not a C# identifier, such as `"I-Users"`, `"IUsers<T>"` or `"App.IUsers"`, is written as it is, and the compiler reports its syntax errors in the generated file of the type's methods, which the user cannot read or fix there.

## Why it exists

The interface was delivered with `SQLSRC035`, the name that something else already has, as its one diagnostic.  A name that is not valid needs a diagnostic of its own, an id, a message, a section of the manual and its tests, which the interface's change did not decide.

## Impact

Only a type that sets `InterfaceName` to something that is not an identifier, a mistake that the compiler's errors in the generated file point at, though not at the attribute.  No type that leaves `InterfaceName` alone is affected: `I` and the type's name is always an identifier.

## Proposed fix

Check the value with `SqlIdentifier.IsValid` where `RepositoryShapeReader` reads the interface's name, and give the shape a reason the name cannot be used beside `InterfaceTakenBy`.  `MethodPlanner` would report a new error at the type's attribute, "Repository interface name is not valid", and write no interface, as it does for `SQLSRC035`.  The new id needs its descriptor in `SqlDiagnostics.All`, its row in `AnalyzerReleases.Unshipped.md` and its section of `docs/manual/diagnostics.md`.

## Trigger

A user report of a syntax error in a file of methods, or the next change to the interface's settings.
