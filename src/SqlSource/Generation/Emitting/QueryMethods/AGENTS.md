# AGENTS.md - src/SqlSource/Generation/Emitting/QueryMethods

See [query methods](../../../../../docs/architecture/generator.md#query-methods) and [extending them](../../../../../docs/architecture/generator.md#extending-query-methods).

## A query method's arguments

- **A query method's argument list comes only from `MethodArguments` of `Generation/Methods/MethodArguments.cs`**: its declaration, its iterator's, the call, the `<param>` documentation and the checks.  A phase file never spells an argument's name, type or default; the body names one through the constants of `MethodArguments`, and the connection and the transaction through `context.Locals.Connection` and `Transaction`.  See [query methods](../../../../../docs/architecture/generator.md#query-methods).

## Engines in a query method

- **A phase file asks a flag of `ExecutionCapabilities`, never the engine, which `CommandWriter.cs` names only for `ParameterBinder.For`, and `ListWriter.cs` only for the limit of parameters, `LimitOf`.**  The flags come from the one switch in `ExecutionCapabilityRules.cs`, run in the output step and never cached, and the statements and `<exception>` documentation they turn on are in `ExecutionCapabilityWriter.cs`.  See [query methods](../../../../../docs/architecture/generator.md#query-methods).

## Locals

- **A local, pattern variable or lambda's parameter of a query's method takes its name from `LocalNames` in `LocalNames.cs`, held by `QueryMethodLocals` of `QueryMethodLocals.cs`.**  A name a token could hide gets a row in `Run_TokenWithAnAwkwardName_Compiles` and `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` of `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`, and in their `_CompilesOnTheNewestCSharp` siblings in `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs`.

- **The parameters and locals of a static local function, as `ListWriter.AppendHelpers` writes them, keep fixed names and need no row.**  C# lets them hide any argument or local of the method, so no token can hide them.

## A new kind of method

- **A new kind of method follows [adding a kind of method](../../../../../docs/architecture/generator.md#adding-a-kind-of-method).**
- **A new axis is a value on the plan, `QueryMethod` or `TypeMethods`, that a phase reads from `QueryMethodContext`, never a `bool` threaded through helpers.**
- **A call of the driver, its token, `await`, a disposal scope and the type a method returns are spelled only by `CallSpelling.cs` and its two implementations, `AsyncCallSpelling.cs` and `SyncCallSpelling.cs`**, through `context.Spelling`; the iterator's modifiers are `AsyncCallSpelling.IteratorModifiersOf`'s, since a synchronous method never streams.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
