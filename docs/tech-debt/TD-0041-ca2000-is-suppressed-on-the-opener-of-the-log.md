# TD-0041 - `CA2000` is suppressed on the opener of the log

## Problem

[`RunLogFile.Open`](../../src/SqlSource.Tool/Logging/RunLogFile.cs) has a `[SuppressMessage]` for `CA2000`, "Dispose objects before losing scope".  It returns the log in an [`Outcome`](../../src/SqlSource.Tool/Outcome.cs), and the analyzer follows a disposable only when it is returned itself, so it reports the log, which `Cli.RunAsync` closes, as never closed.

An attribute covers the whole method.  So the `FileStream` that the method makes is no longer checked either, and an edit that leaks it, or the log, on some path of `Open` is not reported.

## Why it exists

`Outcome` is the one shape of a result that may fail where files of the tool meet, and the log's opener is such a result whose value must be disposed.  The other ways to satisfy the analyzer were weighed and not taken: `dotnet_code_quality.CA2000.dispose_ownership_transfer_at_method_call = true` in `.editorconfig` stops the analyzer from following any disposable that is passed to a method, across the tool; and returning the log itself with the error as an `out` gives the opener a shape of its own.

## Impact

Low.  The method is twenty lines and has one caller, and `RunLogFileTests` holds what it opens and what it refuses.  It is the only suppression of an analyzer's finding in `src/SqlSource.Tool`.  A second `Outcome` of a disposable has appeared in the tests: [`FakeDescriber.OpenAsync`](../../tests/SqlSource.Tool.Tests/FakeDescriber.cs) returns an opened session in one and has the same attribute, since `IQueryDescriber.OpenAsync` gives that shape to every describer.  The first describer in `src/`, sub-phase 3.2's, will add a third.

## Proposed fix

Remove the attribute when the analyzer follows a disposable into the object it is returned in, which a later version may.  Failing that, when a second such `Outcome` appears, decide once for both: a factory on `Outcome` that the analyzer is told hands over what it is given, or a type of its own for a result that must be disposed.

## Trigger

The first describer in `src/SqlSource.Tool`, whose `OpenAsync` returns an `Outcome` of a session: decide then for all three.  Or an update of the analyzers after which the build passes without the attribute.
