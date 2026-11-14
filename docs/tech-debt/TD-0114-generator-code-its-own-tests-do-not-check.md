# TD-0114 - Generator code its own tests do not check

## Problem

The test review of 2026-11-13 ran a mutation test of every scope of `src/SqlSource`, each part of the source with the tests of its own folder.  2,504 of its mutants belong to no test: a survivor that no test of its scope fails on, or a mutant that no test of its scope runs, less the survivors that a finding of the review asks a test to kill.  The appendix of the review, [docs/superpowers/specs/2026-11-13-test-cleanup-findings.md](../superpowers/specs/2026-11-13-test-cleanup-findings.md#gaps-that-belong-to-no-test), counts them by file.  The files with the most:

| File | Survived | No coverage | Gap |
|----|----|----|----|
| [`Generation/Methods/MethodPlanner.cs`](../../src/SqlSource/Generation/Methods/MethodPlanner.cs) | 4 | 422 | 426 |
| [`Generation/Models/ModelReader.cs`](../../src/SqlSource/Generation/Models/ModelReader.cs) | 0 | 141 | 141 |
| [`Generation/Exceptions/ExceptionProbe.cs`](../../src/SqlSource/Generation/Exceptions/ExceptionProbe.cs) | 0 | 95 | 95 |
| [`Generation/Types/QueryTyper.cs`](../../src/SqlSource/Generation/Types/QueryTyper.cs) | 56 | 35 | 91 |
| [`Generation/Models/ModelCollector.cs`](../../src/SqlSource/Generation/Models/ModelCollector.cs) | 40 | 44 | 84 |
| [`Generation/Types/MappedTypes/MappedTypeResolver.cs`](../../src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs) | 61 | 6 | 67 |
| [`Generation/Models/TypeModels.cs`](../../src/SqlSource/Generation/Models/TypeModels.cs) | 0 | 65 | 65 |
| [`Generation/Converters/ConverterReader.cs`](../../src/SqlSource/Generation/Converters/ConverterReader.cs) | 22 | 42 | 64 |
| [`Generation/Types/Drivers/LibraryProbe.cs`](../../src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs) | 20 | 40 | 60 |
| [`Generation/Types/TypeDiagnostics.cs`](../../src/SqlSource/Generation/Types/TypeDiagnostics.cs) | 6 | 51 | 57 |
| [`Generation/Models/ModelSummary.cs`](../../src/SqlSource/Generation/Models/ModelSummary.cs) | 24 | 33 | 57 |

## Why it exists

A scope sees only its own source and the tests of its own folder, so code whose behaviour is proved through the whole generator, by the tests of `tests/SqlSource.Tests/Generator/` and the golden corpus, shows as unchecked in the scope of its folder.  The review proposes no new test but the unit tests a demote needs, so these gaps are recorded rather than closed.

## Impact

Low today: much of this code is run by the driver tests and the golden corpus, which the scope of its folder does not see, so a gap here is not proof that nothing checks it.  It risks a change that the folder's own tests let through, and a slower and less precise failure, from a whole-generator test, where a unit test would have named the fault.

## Proposed fix

For each file of the table, from the most: run its folder's scope with the driver tests added to its filter, `tools/mutation-test.sh generator --tests '<the scope's filter>|FullyQualifiedName~SqlSource.Tests.Generator.' '<the file>'`, to see which gaps the driver tests already close.  Write a unit test in the folder's tests for the behaviour each remaining survivor or uncovered branch breaks, or record why a driver test is the right guard.  Rewrite the table of this item, or resolve it, as the gaps close.

## Trigger

A change to one of the files of the table, or the next run of the test review, which updates this item.
