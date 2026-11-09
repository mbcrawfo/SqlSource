# TD-0114 - Some repositories get methods that cannot reach their connection

## Problem

A repository's method reaches the member or primary-constructor parameter that gives its connection from inside its own body ([`CommandWriter.AppendAcquire`](../../src/SqlSource/Generation/Emitting/QueryMethods/CommandWriter.cs)).  Two shapes of repository get a method that does not compile, and no diagnostic says why:

- **A struct repository's streaming method.**  The method of a query of `IAsyncEnumerable` gets its connection inside its local iterator, so that the connection lives until the loop ends.  A local function inside an instance member of a struct cannot use `this` (CS1673) or a primary-constructor parameter (CS9111), so a `partial struct` or `partial record struct` repository with such a query fails to compile in the generated file.  Its other methods compile.
- **A connection parameter named as an argument of the method.**  A primary-constructor parameter named `parameters` or `cancellationToken` is hidden by the method's argument of that name, when the method has it, so the method reads the argument instead and fails to compile.  A token of the parameter's name is refused (`SQLSRC275`), and the method's locals avoid it, but the two fixed arguments cannot be renamed.

## Why it exists

Both were found while the repository method was being written, and each needs a decision outside that change: whether a struct repository is supported at all, and how a fixed argument's clash with the connection's parameter is reported.

## Impact

Low.  A struct repository with a stream, or a connection parameter of one of those two names, is unusual; the build fails with the compiler's error in generated code, not at run time.

## Proposed fix

For the struct: refuse a struct repository with a diagnostic of the type, as a static one is refused (`SQLSRC034`), or have its streaming methods read the source before the iterator and hand its value in, which changes when a factory is called.  For the parameter: refuse it in `ConnectionResolver` with a diagnostic at the parameter that names the argument it clashes with.

## Trigger

The repository output's diagnostics are reviewed, or a user reports CS1673, CS9111 or a type error at a repository's connection in a generated file.
