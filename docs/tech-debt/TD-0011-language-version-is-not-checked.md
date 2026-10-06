# TD-0011 - The consumer's language version is not checked

## Problem

The file generated for a type is C# 12.  The method that [`MethodWriter`](../../src/SqlSource/Generation/MethodWriter.cs) writes for a query with tokens uses a `static` lambda (C# 9) and relies on `CallerArgumentExpression` (C# 10) for the parameter name in the exception that validation throws.  The file's `#nullable enable` needs C# 8.

Nothing checks the language version of the project that uses the generator.  A project that targets .NET 8 or later and sets `LangVersion` below 12 gets what the compiler makes of it:

- C# 9: no error, and a validation exception whose `ParamName` is null, because the compiler does not fill in the caller expression;
- C# 8: error CS8400 at the lambda of every query with tokens;
- below C# 8: error CS8370 at `#nullable enable` in every type's file, as before tokens were added.

## Why it exists

C# 12 is the default language version of a project that targets .NET 8, which is the oldest target the generator supports ([SQLSRC003](../diagnostics.md#sqlsrc003)).  The phase 3 design made C# 12 a documented requirement and left the check out: a project has to lower `LangVersion` on purpose to be affected.

## Impact

Low.  An affected project gets errors that point into generated code and do not name the cause, or, on C# 9, an exception that does not name its parameter.  The README states the requirement under "Supported environments".

## Proposed fix

Read the language version in [`TargetTypeReader`](../../src/SqlSource/Generation/TargetTypeReader.cs), from the parse options of the syntax tree that carries the attribute (`CSharpParseOptions.LanguageVersion`).  Below 12, report a new usage error at the attribute and emit nothing for the type, as `SQLSRC003` does for the framework.  The error needs its descriptor, its release-tracking row and its section in `docs/diagnostics.md`.

## Trigger

A user reports a compiler error in generated code that comes from a lowered `LangVersion`.  Generated code starts to use a feature newer than C# 12.
