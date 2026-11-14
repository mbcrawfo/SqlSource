# Test cleanup phase 42: tests/SqlSource.Tests/Generation/Repositories, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Repositories`
- Findings: 5 (2 fold, 3 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 62
- Guard: mutation (generator-generation-repositories)

## Files

- `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs`
- `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-3-02

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_MarkedMethodWithTwoParameters_IsUnsupported`; `ConnectionCandidateReaderTests.Read_MarkedMemberOfAnotherShape_IsUnsupported` | rows: all
- action: fold
- evidence: measured (`Read_MarkedMethodWithTwoParameters_IsUnsupported`: kills 22, alone 0; cover `Read_MarkedMemberOfAnotherShape_IsUnsupported`).  Read: a marked method with two parameters is one more shape of marked member that `Method` makes unsupported through `isCallable` (ConnectionCandidateReader.cs:357-366), the same rule the theory's rows at ConnectionCandidateReaderTests.cs:176-179 (generic, `ref` token, `int` return, `int` field) prove, and the fact asserts what the theory does, Name, Type `Unsupported` and IsMarked, plus Member `Method`, which line 365 sets for every method and `Read_MarkedFactory_IsAsyncAndTakesTheToken` asserts.  No comment above either test records anything, and a search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither name; blind: no
- cover: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_MarkedMemberOfAnotherShape_IsUnsupported`
- proposal: Delete the fact and add `[InlineData("private NpgsqlConnection Open(string name, CancellationToken cancellationToken) => null!;")]` as a fifth row of `Read_MarkedMemberOfAnotherShape_IsUnsupported`.
- verdict: confirmed
- verifier's reason: The fact's kills (22) are all ones its input explains: 361:26 `||` and 366:13 `(true?kind…)` turn a method with two parameters into a callable Factory, 422:13 inverts IsMarked; the theory's `ref CancellationToken` row (ConnectionCandidateReaderTests.cs:177) reaches the same branch of `isCallable` (ConnectionCandidateReader.cs:357-366: parameters not empty and not one by-value token), and its generic row (176) the `||` mutant, which is why the evidence names the theory as the whole cover.  The one assertion the theory lacks, Member `Method`, is set unconditionally at ConnectionCandidateReader.cs:365 and asserted by `Read_MarkedFactory_IsAsyncAndTakesTheToken` (547), `Read_MarkedAccessor_IsUnsupportedAndNamedForItsProperty` (225) and `Read_MarkedExplicitImplementation_IsUnsupported` (253).  No comment sits above either test (598, 175); a grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither name; only the plan docs/superpowers/plans/2026-11-09-repository-output.md:147 lists the fact as a planned test, which records no limit, regression or decision, and `git log -S` shows it born in #268 with the rest of the class.  The proposal keeps its input as a fifth row, so nothing is lost.

### F-SqlSource.Tests.Generation-3-08

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` | `ConnectionResolverTests.Resolve_MarkedCandidate_HasTheKindOfWhatItGives` | rows: `("ConnectionString", "Property", "ConnectionString")` line 420, `("ConnectionString", "Parameter", "ConnectionString")` line 421, `("ConnectionString", "Method", "ConnectionString")` line 422, `("DataSource", "Parameter", "DataSource")` line 424, `("Connection", "Property", "Borrowed")` line 427, `("Connection", "Parameter", "Borrowed")` line 428
- action: fold
- evidence: measured (10 rows, kills 17, alone 0, rows needed 1; cover `Resolve_KeywordName_IsEscaped`).  Read: the kind is `KindOf(candidate.Type)` (ConnectionResolver.cs:406, 452-461), which reads the candidate's type alone; the named rows vary only the member, which the kind never reads and which `SourceOf` copies as it is (ConnectionResolver.cs:407), as `Resolve_OneConventionCandidate_IsTheSource` and `Resolve_MethodReturningAConnectionString_IsAMethodSource` assert.  The comment above the theory (ConnectionResolverTests.cs:416-417) explains why the rows are strings and records nothing; a search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test nor its class; blind: no
- cover: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` | `ConnectionResolverTests.Resolve_MarkedCandidate_HasTheKindOfWhatItGives` (rows at lines 419, 423, 425, 426, one for each type); `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` | `ConnectionResolverTests.Resolve_OneConventionCandidate_IsTheSource`
- proposal: Drop the six named rows, keeping one row for each candidate type that gives a source.
- verdict: confirmed
- verifier's reason: The kind is `KindOf(candidate.Type)` (ConnectionResolver.cs:406, 452-461), which reads nothing but the type; the member reaches the source only through `Member` (407), `AccessTextOf` and `LeadingNameOf`, none of which the theory asserts (ConnectionResolverTests.cs:440).  What the six named rows also exercise is covered by tests that assert it: a usable non-argument parameter (rows 421, 424, 428; `Resolve_Parameter_IsAccessedByItsName`, `Resolve_KeywordName_IsEscaped`, which is the evidence's whole cover), a marked connection-string method (422; `Resolve_MethodReturningAConnectionString_IsAMethodSource`, `Resolve_MarkedWithoutDatabaseOnOneDatabase_IsItsSource`).  `kills` shows all 17 kills general to resolving one marked source, none of them alone.  The comment above (416-417) explains the string rows and records nothing; a grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test nor its class (only the plan docs/superpowers/plans/2026-11-09-repository-output.md:205, which lists the kinds to cover and records no decision).  The rows share a name, so this is confirmed by reading: the four rows kept, one per type, are the phase's own guard of the mapping.

### F-SqlSource.Tests.Generation-3-01

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` | `ConnectionCandidateReaderTests.Read_MarkedStringMethod_IsAConnectionString` | rows: all
- action: strengthen
- evidence: measured (kills 27, alone 0; survivor src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs:382:53 Boolean mutation -> `true`, covered by this test alone).  Read: `Method` takes `IsAsync` from `ReturnOf` (ConnectionCandidateReader.cs:362,367), and `ConnectionResolver.Take` gives a source that is async no synchronous source (ConnectionResolver.cs:336), so the mutant leaves every synchronous method of a repository with a marked string method without a connection.  The test asserts Member, Type and DeclaredTypeName (ConnectionCandidateReaderTests.cs:120-123) but not that a method returning a string is no task, as its siblings `Read_MarkedSyncFactory_IsAFactoryOfItsConnectionType` and `Read_MarkedTaskFactory_IsAsync` assert of theirs; blind: no
- cover: none
- proposal: Add `candidate.IsAsync.ShouldBeFalse();` to the test, which kills the survivor at 382:53.
- verdict: confirmed
- verifier's reason: `kills` (generator-generation-repositories) lists `SURVIVED ConnectionCandidateReader.cs:382:53 Boolean mutation -> true` under the test, and the evidence names it as the one test reaching it.  The mutant makes `ReturnOf` give `IsAsync = true` for a string return (ConnectionCandidateReader.cs:382), which `Method` passes through as `isAsync && isCallable` (367); the test asserts Member, Type and DeclaredTypeName only (ConnectionCandidateReaderTests.cs:120-123), while its siblings assert IsAsync (145, 170).  `Take` gives such a source no synchronous twin (`source.IsAsync ? null : source`, ConnectionResolver.cs:336), so the effect the finding names is real.  Adding `candidate.IsAsync.ShouldBeFalse()` kills it.
- survivors to kill: `src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs:382:53`

### F-SqlSource.Tests.Generation-3-06

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` | `ConnectionResolverTests.Resolve_NoCandidate_ReportsSqlsrc028AndFailsTheDatabase` | rows: all
- action: strengthen
- evidence: measured (kills 12, alone 2; survivor src/SqlSource/Generation/Repositories/ConnectionResolver.cs:253:13 Equality mutation -> `heldParameters.Count >= 0`, covered by this test and `Resolve_HeldParameters_ReportsSqlsrc028NamingThem`).  Read: the test checks the advice with three `ShouldContain` calls (ConnectionResolverTests.cs:78-80), and the held-parameters advice with an empty list ("The primary-constructor parameters  are not chosen ... named connectionString or of a DbDataSource type, or mark the parameter [SqlSourceConnection].", ConnectionResolver.cs:256-267) contains all three, so a type with no candidate would be told about parameters it does not have and the test would pass; blind: no
- cover: none
- proposal: Assert the whole advice of ConnectionResolver.cs:270-271, as `Resolve_HeldParameters_ReportsSqlsrc028NamingThem` does for its own, or at least that it does not contain "primary-constructor parameter".
- verdict: confirmed
- verifier's reason: `kills` lists `SURVIVED ConnectionResolver.cs:253:13 Equality mutation -> heldParameters.Count >= 0` under this test.  With an empty `HeldParameters` the mutant gives the held-parameters advice (ConnectionResolver.cs:256-267) with an empty list, which contains "connectionString", "DbDataSource" and "[SqlSourceConnection]", the three `ShouldContain` checks at ConnectionResolverTests.cs:78-80, so the test passes on it.  Its two own kills (270:16, 271:15) blank a part of the right advice.  Asserting the whole advice of 270-271, as `Resolve_HeldParameters_ReportsSqlsrc028NamingThem` does at 108, kills the survivor.
- survivors to kill: `src/SqlSource/Generation/Repositories/ConnectionResolver.cs:253:13`

### F-SqlSource.Tests.Generation-3-07

- test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` | `ConnectionResolverTests.Resolve_FieldNamedAsAnArgument_IsTheSource` | rows: all
- action: strengthen
- evidence: measured (kills 17, alone 0; cover `Resolve_OneConventionCandidate_IsTheSource`).  Read: the field is named `parameters` and resolved for `Main` (ConnectionResolverTests.cs:346), whose `TakesParameters` is false (ConnectionResolverTests.cs:18, NeededDatabase.cs default), so `parameters` is no argument's name there (`IsAnArgumentsName`, ConnectionResolver.cs:466-468) and the `Member == Parameter` condition of `IsUsable` (ConnectionResolver.cs:360) is never needed: the test would pass if a field were checked as a parameter is, which is what its name and comment say it proves; blind: no
- cover: none
- proposal: Resolve the field for `Main with { TakesParameters = true }`, as `Resolve_ParameterNamedAsAnArgument_ReportsSqlsrc036` does for the parameter, or name the field `cancellationToken`.
- verdict: confirmed
- verifier's reason: The test resolves a field named `parameters` for `Main` (ConnectionResolverTests.cs:18, 346), and `NeededDatabase.TakesParameters` defaults to false (src/SqlSource/Generation/Repositories/NeededDatabase.cs), so `IsAnArgumentsName` (ConnectionResolver.cs:466-468) is false whatever the member, and the `Member == Parameter` half of 360 is never needed.  `kills` confirms it: the test kills only `360:17 Negate expression` of the whole condition, not the `Logical mutation` to `||` that removes the member check (killed instead by the parameter rows of `Resolve_MarkedCandidate_HasTheKindOfWhatItGives` and others).  Its comment (343) promises that no argument hides a member; resolving for `Main with { TakesParameters = true }`, or naming it `cancellationToken`, would make it prove that.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-repositories`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-42/generator-generation-repositories/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Repositories.' 'Generation/Repositories/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-42/generator-generation-repositories/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Repositories.' 'Generation/Repositories/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json artifacts/test-cleanup/phase-42/generator-generation-repositories/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionCandidateReaderTests.Read_MarkedStringMethod_IsAConnectionString
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionCandidateReaderTests.Read_MarkedMethodWithTwoParameters_IsUnsupported
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionCandidateReaderTests.Read_MarkedMemberOfAnotherShape_IsUnsupported
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionResolverTests.Resolve_NoCandidate_ReportsSqlsrc028AndFailsTheDatabase
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionResolverTests.Resolve_FieldNamedAsAnArgument_IsTheSource
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-42/generator-generation-repositories/before/reports/mutation-report.json ConnectionResolverTests.Resolve_MarkedCandidate_HasTheKindOfWhatItGives
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-3-01: `src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs:382:53`
- F-SqlSource.Tests.Generation-3-06: `src/SqlSource/Generation/Repositories/ConnectionResolver.cs:253:13`
- F-SqlSource.Tests.Generation-3-07: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-repositories`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-42/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-42/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-42/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-42/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-42/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-42/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Generation-3-03 (keep, `ConnectionCandidateReaderTests.Read_FieldNamedConnectionString_IsAConnectionStringCandidate`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-3-04 (keep, `ConnectionCandidateReaderTests.Read_ParameterPassedOnOrStored_IsHeld`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Generation-3-05 (keep, `ConnectionCandidateReaderTests.Read_ParameterNotPassedOnOrStored_IsACandidate`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
