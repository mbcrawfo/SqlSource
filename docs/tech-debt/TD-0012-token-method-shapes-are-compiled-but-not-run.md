# TD-0012 - Some shapes of generated method are compiled in tests and never run

## Problem

Two gaps in the tests of the methods that [`MethodWriter`](../../src/SqlSource/Generation/MethodWriter.cs) writes:

- **Shapes that are compiled and not executed.**  A query that is only a token (`{{sql}}`) and a query with more than seven tokens, whose state is a nested tuple read as `state.Item8`, are checked for their text in `tests/SqlSource.Tests/Generation/MethodWriterTests.cs` and compiled by `Run_QueryThatIsOnlyATokenOrHasEightOfThem_Compiles` in [`GeneratedSourceTests.cs`](../../tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs).  No test calls either method and looks at the string it returns.  The end-to-end tests in `tests/SqlSource.Tests/EndToEnd/` run methods with one to three tokens only.
- **Token names that are compiled as C# 12 only.**  `Run_TokenWithAnAwkwardName_Compiles` hands the compiler C# 12, because the files of `tests/SqlSource.Tests/Generator/` also run on Roslyn 4.8.0, which knows nothing newer.  A consumer on .NET 10 compiles generated code as C# 14, where `field` and `extension` are contextual keywords.  The row for `field` therefore tests nothing that C# 12 does not already allow.

## Why it exists

Both were found by the review of phase 3 and graded minor.  The reviewer executed a token-only query and one with nine tokens, and compiled every contextual keyword as a token name at `LanguageVersion.Latest` and `Preview` on Roslyn 5.9.0: all were correct.  The gap is that nothing in the repository repeats those checks.

## Impact

Low.  A change to `MethodWriter` that breaks the read of a nested tuple, or a future C# version that makes a parameter name mean something else inside the generated method, would pass the tests and reach consumers as wrong SQL or as a compiler error in generated code.

## Proposed fix

- Add a token-only query and a query with eight tokens to `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql`, with a test in `EndToEndTests.cs` that asserts the returned string of each.
- Copy `Run_TokenWithAnAwkwardName_Compiles` into a test class outside `tests/SqlSource.Tests/Generator/`, so that it runs on the current Roslyn only, and parse with `LanguageVersion.Latest`.  Add `extension` to its rows.

## Trigger

A change to how `MethodWriter` passes or reads the state.  Raising the Roslyn version that `tests/SqlSource.Tests` overrides to.
