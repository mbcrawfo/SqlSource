# TD-0089 - Theories that branch on their row

## Problem

A theory is clearest when its rows share one arrangement and one set of assertions.  The theories whose row chose between two sets of assertions or two arrangements by an `if` and `else` were split into facts, or into theories whose rows share their arrangement and assertions.  These remain, found on 2026-11-17 by scanning every `[Theory]` of `tests/` for an `if`, `switch` or `?:` whose condition names one of its parameters, or a local taken from one or deconstructed from a value that does, and reading each theory the scan found.  Paths are under `tests/`.

- A branch that adds or swaps a step or an assertion for some rows only:
  - `DatabaseTests/Postgres/Describer/PlanOutputTests.cs`, `Explain_PolymorphicFormsOfTheMajor_PrintAsEach` (`parameterType`) and `Explain_FullJoinCoalesce_IsComputedAtTheJoin` (`mergeJoin`);
  - `DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` and `DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs`, `Int4Array_EachKindOfSequence_RoundTrips` (`ordered`);
  - `DatabaseTests/Postgres/EndToEnd/` and `DatabaseTests/SqlServer/EndToEnd/`, `RepositoryQueryTests.GetNote_EachSource_ReadsTheRow` and `SynchronousRepositoryTests.GetNoteSync_EachSource_ReadsTheRow` (`source`);
  - `DatabaseTests/Postgres/Renaming/` and `DatabaseTests/SqlServer/Renaming/`, `RealisticRenamingTests.RenameAsync_Case_ReplaysWithTheOriginalsProvenanceAndLeaksNothing` and `RealisticRenamingTests.RenameAsync_RealisticCase_ReplaysToItsRenamedAnswerAndLeaksNothing` (`name`);
  - `DatabaseTests/SqlServer/Describer/ColumnTests.cs`, `ColumnsAsync_RowOfTheSpike_IsAsTheSpikeFound` (`sql`);
  - `DatabaseTests/SqlServer/EndToEnd/AllTypesQueryTests.cs`, `ExecuteAsync_LeftOutColumnAlone_FailsToWriteItsValue` (`property`);
  - `DatabaseTests/SqlServer/EndToEnd/RunTimeArgumentTests.cs`, `GetAccountInRange_TokenCancelledBeforeTheCall_ThrowsTaskCanceledException` (`open`);
  - `SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`, `Append_SynchronousEachShape_ReturnsTheResultWithoutAwaiting` (`returnsXml`);
  - `SqlSource.Tests/Generation/Placement/TypePlacementTests.cs`, `Of_Protected_IsAnErrorOnlyWhereCSharpForbidsIt` (`reason`);
  - `SqlSource.Tests/Generator/ConverterCompileTests.cs`, `Run_ConvertedQueries_CompileWithNoWarning` (`driver`);
  - `SqlSource.Tests/Generator/JsonMethodTests.cs`, `Run_NotNullJsonObjectColumn_ThrowsOnAJsonNull` and `Run_JsonElementColumn_HasNoJsonNullCheck` (`isSqlServer`);
  - `SqlSource.Tests/Generator/JsonSerializerMethodTests.cs`, `Run_ClassOnJsonb_ReadsAndWritesThroughTheSerializer` and `Run_OverrideToAClassSharedByModelsAndMethods_UsesTheClassInEach` (`driver`);
  - `SqlSource.Tests/Generator/LibraryConversionTests.cs`, `Run_PostgresJsonNode_ByEdgeOnNpgsql8AndByTheDriverOn10` (`npgsql10`) and `Run_JsonGeneratedCode_CompilesWithoutWarnings` (`driver`);
  - `SqlSource.Tests/Generator/LibraryTypeTests.cs`, `Type_JsonOn_GivesJsonElement` (`place`);
  - `SqlSource.Tests/Generator/MethodTests.cs`, `Run_MethodsQueryNamedLikeTheNestedClass_IsAMethodOfTheExtensionClass` (`other`);
  - `SqlSource.Tests/Generator/ModelNameTests.cs`, `Run_ModelNameWithAContextualKeywordThatCSharpRefusesAsATypeName_IsAnError` (`place`);
  - `SqlSource.Tests/Generator/NestedClassTests.cs`, `Run_ContextualKeywordThatCannotNameTheNestedClass_IsAnError` (`inSettingsFile`);
  - `SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`, `Marker_OfEveryKind_HasAKeywordAndAScope` (`kindName`);
  - `SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs`, `TryReadEntry_AnEntry_ReadsNamesAndValuesAsAChoice` (`valid`);
  - `SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs`, `Schema_TypeOfEachKey_IsTheReaders` (`key`);
  - `SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs`, `Next_BlockCommentNotClosed_IsInvalidAtTheEnd` and `Next_SlashThatStartsNoComment_IsInvalidAtTheSlash` (`text`);
  - `SqlSource.Tool.Tests/Bundles/BundleQueryFileTests.cs`, `Of_MarkerOfEveryKind_IsHeldExactlyWhereTheRenamerAndTheCheckReadIt` (`kindName`);
  - `SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresFixtureRenamingTests.cs`, `RenameAsync_Fixture_ReplaysWithTheOriginalsProvenance` (`name`);
  - `SqlSource.Tool.Tests/Commands/ManifestRunTests.cs`, `Run_OnlineWithABuildPathThatHoldsASemicolonOrAnEqualsSign_PrintsItInFull` (`option`);
  - `SqlSource.Tool.Tests/Commands/OnlineRunTests.cs`, `Run_ErrorTheCompilerReportsToo_IsNotWrittenAndKeepsItsEffect` (`id`);
  - `SqlSource.Tool.Tests/Commands/Watch/WatchFailureTests.cs`, `Watch_WatcherErrorDuringAPass_IsWrittenAfterThePassesOutput` (`first`);
  - `SqlSource.Tool.Tests/Commands/Watch/WatchFolderTests.cs`, `Watch_WatchedFolderRemovedThenBack_IsWatchedAgain` (`removed`) and `Watch_PolledFolderDeletedAndMadeAgain_ReportsNoError` (`aboveListsWhileItIsGone`);
  - `SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs`, `Apply_SidecarThatAProgramHoldsOpenUntilTheStoreWaits_IsChangedWithoutAnError` (`actionName`);
  - `SqlSource.Tool.Tests/Projects/OnlineMultiTargetTests.cs`, `Build_ProjectThatListsNoSidecar_GivesTheCompilersOnlyTheSidecarTheRunCreated` (`created`);
  - `SqlSource.Tool.Tests/Watching/WatchSetTests.cs`, `Classify_NamedEnvFileUnderAnIgnoredFolder_Describes` (`path`) and `Folders_AssetsFolderThatDoesNotExistYet_IsBoundByTheBuildProps` (`watched`).
- A branch that only supplies an input the row gives, or picks how the row's input is supplied, after which every row runs the same steps and assertions, which may be acceptable as it stands:
  - `SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`, `Run_ArrayPool_ComesFromTheMostSpecificLevelThatNamesIt` (`folderFile`);
  - `SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs`, `RenameAsync_AnAliasOfAColumnOfTheServers_IsRenamed` (`first`);
  - `SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`, `Diagnose_AnotherFileWithAnErrorOfThePlan_WritesTheBundleAndLogsTheError` (`latin1`) and `Diagnose_AnErrorThatMayChangeTheQuery_IsPrintedAndWritesNoBundle` (`at`);
  - `SqlSource.Tool.Tests/Commands/OnlineRunTests.cs`, `Run_AutoWithAVariableTwoDatabasesShare_IsSqlsrc214AndNotOffline` (`inDotEnv`);
  - `SqlSource.Tool.Tests/Commands/SecretTests.cs`, three tests over `debug` and `Diagnose_ConnectionStringWherePathsStand_IsNeverPrintedOrLogged` (`fromVariable`), and `SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs`, `Run_DescriberThatThrows_IsAFaultWithoutTheConnection` (`debug`);
  - `SqlSource.Tool.Tests/Commands/Watch/WatchChangeTests.cs`, `Watch_RestoreIntoAnArtifactsFolder_RunsTheManifestTarget` (`repository`);
  - `SqlSource.Tool.Tests/Commands/Watch/WatchDuringAPassTests.cs`, `Watch_SaveDuringTheFirstPass_GivesOneMorePassThatDescribesIt` (`polls`);
  - `SqlSource.Tool.Tests/Commands/Watch/WatchFailureTests.cs`, `Watch_PollingVariable_PollsEachFolderOnlyWhenItAsksTo` (`value`);
  - `SqlSource.Tool.Tests/Describing/FileOutcomesTests.cs`, `Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunThatMayDeleteAndWhenThereIsOne` (`sidecarExists`);
  - `SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs`, `Run_NoLogOptionAndNoVariable_WritesNoFile` (`variable`);
  - `SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`, `Errors_OfSettingsFiles_AreTheGeneratorsAtTheSamePlaces` (`name`);
  - `SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, `Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry` (`chain`);
  - `SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs`, `Build_AfterAnInputIsTouched_RunsTheToolOnceMore` (`input`);
  - `SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`, `Evaluate_Project_RunsTheDotnetThatStartedTheToolOrTheOneOnThePath` (`hostPath`);
  - `SqlSource.Tool.Tests/Watching/WatchSetTests.cs`, `Folders_AssetsFolderBelowTheProject_AddsNoFolder` (`restored`), `Folders_AssetsFolderThatDoesNotExistAboveTheRepository_IsNotWatched` and `Folders_FolderAboveEachWatchedFolder_IsWatchedOutsideARepository` (`repository`).
- 130 more theories derive an expected value or a piece of the arrangement from the row with a `?:`, a `switch` expression or an `if` that only picks a value, as `nullable is null ? … : …`, and then run the same calls and assertions on every row.  These state one rule over rows that differ only in data, and need no change; the same scan lists them.

## Why it exists

The cleanup of the tests' uneven conventions split the theories it named and the six of the same shape that its review found, and recorded the rest here rather than grow that pull request past its scope.

## Impact

Low: a reader must follow the branch to know what a row checks, and a row that takes the other branch by mistake checks the wrong thing without failing.

## Proposed fix

Judge each listed theory: split one whose rows check different things into facts, or into theories whose rows share their arrangement and assertions over a helper of the class, as the theories split before were; keep one whose branch only supplies an input the row gives, and say so in its comment.

## Trigger

A change to one of the listed theories, or the next maintainability review.
