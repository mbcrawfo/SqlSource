# TD-0114 - Generator code its own tests do not check

## Problem

The test review of 2026-11-13 ran a mutation test of every scope of `src/SqlSource`, each part of the source with the tests of its own folder.  It found 2,504 mutants that belong to no test: a survivor that no test of its scope fails on, or a mutant that no test of its scope runs, less the survivors that a finding of the review asks a test to kill.  The appendix of the review, [docs/superpowers/specs/2026-11-13-test-cleanup-findings.md](../superpowers/specs/2026-11-13-test-cleanup-findings.md#gaps-that-belong-to-no-test), counts them by file.

The eleven files with the most, 1,207 of those mutants, have been measured with the driver tests and closed.  The other 1,297, in 131 files, have not.  The files with the most:

| File | Survived | No coverage | Gap |
|----|----|----|----|
| [`Generation/Methods/MethodsInput.cs`](../../src/SqlSource/Generation/Methods/MethodsInput.cs) | 1 | 54 | 55 |
| [`Generation/Emitting/QueryMethods/ListCountWriter.cs`](../../src/SqlSource/Generation/Emitting/QueryMethods/ListCountWriter.cs) | 29 | 23 | 52 |
| [`Generation/Sidecars/SidecarChecker.cs`](../../src/SqlSource/Generation/Sidecars/SidecarChecker.cs) | 0 | 51 | 51 |
| [`Generation/TargetTypeReader.cs`](../../src/SqlSource/Generation/TargetTypeReader.cs) | 0 | 51 | 51 |
| [`Generation/Emitting/ModelEmitter.cs`](../../src/SqlSource/Generation/Emitting/ModelEmitter.cs) | 19 | 27 | 46 |
| [`Generation/Emitting/MethodEmitter.cs`](../../src/SqlSource/Generation/Emitting/MethodEmitter.cs) | 27 | 17 | 44 |
| [`Parsing/SqlListUses.Conditions.cs`](../../src/SqlSource/Parsing/SqlListUses.Conditions.cs) | 39 | 0 | 39 |
| [`Generation/Methods/MethodConflicts.cs`](../../src/SqlSource/Generation/Methods/MethodConflicts.cs) | 6 | 29 | 35 |
| [`Parsing/SqlListUses.cs`](../../src/SqlSource/Parsing/SqlListUses.cs) | 28 | 2 | 30 |
| [`Parsing/SqlListUses.Places.cs`](../../src/SqlSource/Parsing/SqlListUses.Places.cs) | 26 | 5 | 29 |
| [`Generation/Types/Drivers/IDriverProbe.cs`](../../src/SqlSource/Generation/Types/Drivers/IDriverProbe.cs) | 4 | 24 | 28 |
| [`Generation/Types/Drivers/NpgsqlProbe.cs`](../../src/SqlSource/Generation/Types/Drivers/NpgsqlProbe.cs) | 7 | 21 | 28 |

## Why it exists

A scope sees only its own source and the tests of its own folder, so code whose behaviour is proved through the whole generator, by the tests of `tests/SqlSource.Tests/Generator/` and the golden corpus, shows as unchecked in the scope of its folder.  The review proposes no new test but the unit tests a demote needs, so these gaps were recorded rather than closed.

## Impact

Low today.  In the eleven files measured, the tests of `SqlSource.Tests.Generation` left 963 mutants alive.  The driver tests killed 750 of them, new tests killed 111, 4 more make an existing test hang, and the other 98 need no test, each for the reason in [the list of them](../superpowers/specs/2026-11-23-td-0114-mutants-that-need-no-test.md).  The work also found wrong keys in the help of `SQLSRC246`, now fixed, so a gap here can hide a real fault.  It risks a change that the folder's own tests let through, and a slower and less precise failure, from a whole-generator test, where a unit test would have named the fault.

## Proposed fix

For each file of the table, from the most:

1. Run it with the tests of `SqlSource.Tests.Generation`, `tools/mutation-test.sh generator --tests 'FullyQualifiedName~SqlSource.Tests.Generation.' '<the file>'`, which takes minutes.
2. Run only the mutants that leave alive, by their spans, with the driver tests added: `tools/mutation-test.sh generator --tests 'FullyQualifiedName~SqlSource.Tests.Generation.|FullyQualifiedName~SqlSource.Tests.Generator.' '<the file>{<start>..<end>}'`, at the ten-minute allowance that [`tools/README.md`](../../tools/README.md#limits) asks of a run with slow tests.  The eleven files' 963 mutants took under seven hours.
3. Write a test for each mutant left that changes what a user or a later step sees.  Give any other the class and reason that the list above uses.
4. Rewrite the table of this item, or resolve it, as the gaps close.

## Trigger

A change to one of the files of the table, or the next run of the test review, which updates this item.
