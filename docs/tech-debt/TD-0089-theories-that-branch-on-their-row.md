# TD-0089 - Theories that branch on their row

## Problem

A theory is clearest when its rows share one arrangement and one set of assertions.  TD-0080 item 12 split the theories whose row chose between two sets of assertions or two arrangements by an `if` and `else`.  These remain, found by scanning every `[Theory]` of `tests/` for an `if` whose condition names one of its parameters.  Paths are under `tests/`.

- An `if` that adds a step or an assertion for some rows only:
  - `DatabaseTests/Postgres/Describer/PlanOutputTests.cs`, `Explain_FullJoinCoalesce_IsComputedAtTheJoin` (`mergeJoin`);
  - `DatabaseTests/Postgres/Describer/SessionTests.cs`, `DescribeAsync_TableDroppedDuringTheDescribe_WaitsForIt`;
  - `DatabaseTests/SqlServer/EndToEnd/AllTypesQueryTests.cs`, `ExecuteAsync_LeftOutColumnAlone_FailsToWriteItsValue`;
  - `DatabaseTests/SqlServer/EndToEnd/RunTimeArgumentTests.cs`, `GetAccountInRange_TokenCancelledBeforeTheCall_ThrowsTaskCanceledException` (`open`);
  - `SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`, `Map_EveryRowOfTheTable_GivesItsType`;
  - `SqlSource.Tests/Generator/MethodTests.cs`, `Run_CodeGenQueryNamedLikeTheNestedClass_IsAMethodOfTheExtensionClass`;
  - `SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`, `Read_ColumnsWithoutRows_IsMalformedAtTheColumnsKey`;
  - `SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs`, `Next_BlockCommentNotClosed_IsInvalidAtTheEnd` and `Next_SlashThatStartsNoComment_IsInvalidAtTheSlash`;
  - `SqlSource.Tests/Snapshot/SidecarWriterTests.cs`, `Write_PostgresType_WritesTheNestedTypeItsKindNames`;
  - `SqlSource.Tool.Tests/Describing/FileOutcomesTests.cs`, `Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunThatMayDeleteAndWhenThereIsOne`.
- An `if` that sets an optional input only when the row gives one, which may be acceptable as it stands:
  - `SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`, `Run_NoArrayPool_ComesWholeFromTheMostSpecificLevelThatGivesAList` (`folderFile`);
  - `SqlSource.Tool.Tests/Commands/SecretTests.cs`, three tests over `debug`, and `SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs`, `Run_DescriberThatThrows_IsAFaultWithoutTheConnection`;
  - `SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs`, `Run_NoLogOptionAndNoVariable_WritesNoFile`;
  - `SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, `Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry`;
  - `SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`, `Evaluate_Project_RunsTheDotnetThatStartedTheToolOrTheOneOnThePath`.
- About fifty more theories derive an expected value or a piece of the arrangement from the row with `?:`, as `nullable is null ? … : …`.  Most of these state one rule over rows that differ only in data, and need no change; a scan for `?:` in a theory's body whose condition names a parameter lists them.

## Why it exists

The review of TD-0080's item 12 split the theories it named and the six of the same shape that its review found, and recorded the rest here rather than grow that pull request past its item.

## Impact

Low: a reader must follow the `if` to know what a row checks, and a row that takes the other branch by mistake checks the wrong thing without failing.

## Proposed fix

Judge each listed theory: split one whose rows check different things into facts, or into theories whose rows share their arrangement and assertions over a helper of the class, as TD-0080 item 12 did; keep one whose `if` only adds an input the row gives, and say so in its comment.

## Trigger

A change to one of the listed theories, or the next maintainability review.
