# Reviewing Test Quality Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A skill, `reviewing-test-quality`, that a person starts, which measures the tests, has agents review and
verify every test with the measurements beside it, and writes a cleanup epic of small phases.

**Architecture:** The skill is Markdown and data in `.claude/skills/reviewing-test-quality/`, with one script that
runs the measurements one after another.  `tools/MutationReport` gains three commands: `slices` cuts the test
folders into the slices the reviewers read, `condense` turns the reports of a run into one evidence file per slice,
and `kills` answers a verifier's question about one test.  Everything a run measures or writes on the way goes to
`artifacts/test-review/<run>/`, which git ignores; only the epic, its phase specs, its appendix and two tech-debt
items reach the repository.

**Tech Stack:** bash, .NET 10, `System.Text.Json`, `System.Xml`, xUnit v3 4.0.1 on Microsoft.Testing.Platform,
Shouldly, `dotnet-stryker` 5.1.0 through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-12-test-quality-review-design.md`, "Part 2: the skill", with "What was
tried", which overrides the earlier text: folder scopes replace whole runs.  This plan is pull request 2 of the
spec's three, and is stacked on pull request 1 (`claude/test-quality-analysis-7ba277`) until that merges.

## Global Constraints

- `disable-model-invocation: true` in the front matter of `SKILL.md`: only a person's `/reviewing-test-quality`
  starts a run.
- Folder scopes only.  No scope mutates a whole project, and no step of the skill runs one.
- One mutation run at a time in a checkout, and nothing else that builds runs there meanwhile: not
  `pre-commit-validation.sh`, not `dotnet run`, not a test run.  The measurements are one script, run in order.
- `tests/DatabaseTests/QueryMethodAllocation` is never run by the skill.
- A broad test never counts as cover, and never as the "other test" that stops a kill being a test's alone.
- A static mutant and a mutant that does not compile are blind areas, marked as such, never counted as surviving.
- A `Timeout` is not a kill of any test: the report names no killer for it.
- A database test, a test of `tests/SqlSource.Repository.Tests` or a test of a test library is deleted or folded
  only against a named test, on the same engine, that a verifier read and confirmed proves the same behaviour.
- No reviewing agent edits a file of the repository.  An agent writes only its own file in the run folder.
- The program adds no package: `tools/MutationReport` references nothing but the framework.
- The root `AGENTS.md` is 14,925 of 16,384 bytes and `tests/AGENTS.md` has 6 bytes left: the root gains one section
  of two lines, and `tests/AGENTS.md` gains nothing.  A bullet of any `AGENTS.md` is at most 80 words.
- Lines are at most 120 characters (`.claude/` is exempt from the checker, and keeps to it anyway), shell is
  indented by 4.  Test classes are `sealed`, assert with Shouldly and name tests `Method_Scenario_Outcome`.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit.  Before a push,
  `./pre-commit-validation.sh --full` passes on the tree as pushed, and `branch-review:review` is clean.

## Calls to check

These are mine, made while planning, and the owner may overrule each:

1. **The scopes pair each source folder with the test folder that mirrors it, and no test folder is in two
   scopes.**  The root files of each project (`SqlSourceGenerator.cs`; `Cli.cs` and the commands) are paired with
   the driver tests (`tests/SqlSource.Tests/Generator`) and the command tests (`tests/SqlSource.Tool.Tests/Commands`).
   So a driver test's kills in `Generation/` are not measured: `Generation/` shows more survivors than it has, and
   a driver or command test is deleted or folded only under the named-cover rule or after a re-measurement of the
   source it is really about.  The alternative, each `Generation/` folder also run against the driver tests, is
   measured for cost in Task 9 before anyone chooses it: `Generation/` has about 7,800 testable mutants, and at
   the 3.9 seconds a mutant of the generator's one whole run that alone is over eight hours.
2. **Slices are cut by the program, not by the lead**, so that a run and its rerun cut the same slices and every
   test file is in exactly one.
3. **A slice never spans two test projects**, so that a class's share of its project's time means something.
4. **Agents write their findings and verdicts to files in the run folder** and return a summary, so that the
   lead of a run with 40 reviewers does not hold 40 reports.  The design says "read-only"; the run folder is under
   `artifacts/`, outside what git tracks.
5. **The lines that only the database projects reach are one file for the run**, not a part of each database
   slice's evidence: coverage is per project, so no line belongs to a slice.
6. **Every agent runs on the lead's model.**  The design leaves the model to the builder.
7. **`measure.sh` and the data files live in the skill's folder**, not in `tools/`: nothing but the skill uses them.

## Review Focus

1. **Two scopes that test one mutant.**  `condense` fails and names the file; it never counts a kill twice.  Pinned
   in Task 1 (`Merge_MutantTestedInTwoReports_Throws`).
2. **A test file in no slice, or in two.**  `slices` gives a partition of the `.cs` files under the folders it is
   given.  Pinned in Task 3 (`Cut_AnyTree_PutsEveryFileInExactlyOneSlice`).
3. **A broad test as a test's only fellow killer.**  The kill is the focused test's alone and the broad test is in
   no cover.  Pinned in Task 2.
4. **A slice whose tests ran in no scope.**  Its evidence says that it was not measured, and does not say that its
   tests kill nothing.  Pinned in Task 5.
5. **A report whose run tested a static mutant, or whose scope failed.**  `condense` refuses the report by name,
   since its kills are not real.  Pinned in Task 5.

---

### Task 1: Read the kill matrix

**Files:**
- Create: `tools/MutationReport/KillMatrix.cs`, `tools/MutationReport/KillMatrixReader.cs`
- Modify: `tests/SqlSource.Repository.Tests/MutationReport/Reports.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/KillMatrixReaderTests.cs`, `KillMatrixTests.cs`

**Interfaces:**
- Produces:

```csharp
// A test of a report.  Id is Stryker's, one for each row of a theory.  File is the path from the repository root
// with '/', and Name the full name, which the rows of a theory share.
internal sealed record ReportTest(string Id, string File, string Name);

// Project is the mutated project's folder from the repository root, "src/SqlSource".  Reason is the report's
// statusReason or "".  CoveredBy and KilledBy index KillMatrix.Tests.
internal sealed record MatrixMutant(
    string Project, MutantKey Key, MutantStatus Status, bool Static, string Reason, int[] CoveredBy, int[] KilledBy);

internal sealed record KillMatrix(IReadOnlyList<ReportTest> Tests, IReadOnlyList<MatrixMutant> Mutants)
{
    // One matrix of several scopes' reports: tests are one by Id, and a mutant by Project and Key.  Of a mutant in
    // several, the one that was tested is kept.  Throws InvalidDataException naming the file of a mutant that two
    // reports tested.
    public static KillMatrix Merge(IReadOnlyList<KillMatrix> matrices);
}

internal static class MutantKinds
{
    public static bool IsTested(MatrixMutant mutant);  // Killed, Survived, Timeout or NoCoverage
    public static bool IsBlind(MatrixMutant mutant);   // CompileError, or Static and not tested
}

internal static class KillMatrixReader
{
    // Tests holds only the tests the run ran, those the report's testFiles list.  The repository root is the
    // folder two above projectRoot.  Throws InvalidDataException as ReportReader does, and for a test id that
    // testFiles does not hold.
    public static ValueTask<KillMatrix> ReadAsync(Stream json, CancellationToken cancellation);
}
```

- `Reports.cs` gains, without changing what its callers get today: `ReportMutant` takes optional `CoveredBy` and
  `KilledBy` (`string[]?`, the old fixed lists when null) and `Reason`; and `Reports.Json`/`Write` overloads take
  `ReportTestFile(string Path, params (string Id, string Name)[] Tests)` values for `testFiles`.

- [ ] **Step 1: Write the failing tests**

`KillMatrixReaderTests`, on a report of `/repo/src/SqlSource` with tests in `/repo/tests/SqlSource.Tests/ATests.cs`:
`ReadAsync_Report_GivesEachMutantItsTestsByIndex` (ids `t1`, `t2`; a mutant covered by both and killed by `t2` has
`CoveredBy` `[0, 1]`, `KilledBy` `[1]`, `Project` `"src/SqlSource"`, and `Tests[1]` is
`("t2", "tests/SqlSource.Tests/ATests.cs", "N.A.Two")`); `ReadAsync_TestFileWithNoTests_AddsNone`;
`ReadAsync_MutantWithAStatusReason_KeepsIt`; `ReadAsync_IdThatNoTestFileHolds_Throws`;
`ReadAsync_TextThatIsNoReport_Throws` (the three texts of `ReportReaderTests`).

`KillMatrixTests`: `Merge_TwoReports_UnitesTestsByIdAndRenumbers`; `Merge_MutantIgnoredInOneAndTestedInAnother_`
`KeepsTheTested`; `Merge_MutantTestedInTwoReports_Throws` (message contains the mutant's file);
`IsBlind_OfEachKind_IsTrueOnlyForUncompiledAndUntestedStatic` (a theory over status and `Static`).

- [ ] **Step 2: Run them and see them fail to compile**

Run: `dotnet test --project tests/SqlSource.Repository.Tests --filter-namespace "*MutationReport"`

- [ ] **Step 3: Implement**

The reader deserialises with source generation as `ReportReader` does, declaring no `source`.  A converter on the
two id lists maps each id to a number as it is read, so that no id string is held for each mutant: a report of a
wide scope is hundreds of megabytes of ids.  `testFiles` comes after `files` in a report, so ids are numbered when
first seen and named afterwards.

- [ ] **Step 4: Run the tests and see them pass, then read a real report**

Run the reader on a report of Task 9's pilot or of pull request 1's runs (65 MB, 2,284 tests, 3,728 mutants, 1,967
killed) from a scratch test or `dotnet run`, and check those counts.  Expected: under 5 seconds and 1 GB.

- [ ] **Step 5: Commit**

`./pre-commit-validation.sh`, then `git commit -m "feat: read the kill matrix of a mutation report"`.

---

### Task 2: What each test method kills

**Files:**
- Create: `tools/MutationReport/MethodEvidence.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/KillEvidenceTests.cs`

**Interfaces:**
- Consumes: `KillMatrix`, `MutantKinds` (Task 1).
- Produces:

```csharp
// What a merged matrix says of one test method, all its rows together.
internal sealed record MethodEvidence(
    string File,
    string Name,
    int Rows,
    bool Broad,
    int Covered,                      // tested mutants that a row covers
    int Kills,                        // mutants that a row kills
    IReadOnlyList<MatrixMutant> Alone,     // of those, the ones no other focused method kills, by file and place
    IReadOnlyList<string>? Cover,     // other focused methods that together kill all it kills; null when Alone
                                      // is not empty or Kills is 0
    int RowsNeeded,                   // rows of it that together kill all its rows kill; 0 when Kills is 0
    int Timeouts,                     // Timeout mutants that a row covers
    IReadOnlyList<MatrixMutant> Survivors); // Survived mutants that a row covers, by file and place

internal static class KillEvidence
{
    // One entry for each test method of the matrix, by file and name.  isBroad is asked a method's full name.
    public static IReadOnlyList<MethodEvidence> Of(KillMatrix matrix, Func<string, bool> isBroad);
}
```

`Cover` and `RowsNeeded` are greedy set covers: take the candidate that kills the most of what is left, the
ordinally smaller name or id on a tie, until nothing is left.  It is "the smallest set found", not the smallest.

- [ ] **Step 1: Write the failing tests**, each on a matrix built in the test:
  - `Of_TheoryRows_AreOneMethodWithTheirKillsUnited` (rows `r1`, `r2` of one name: `Rows` 2, `Kills` the union);
  - `Of_KillThatAnotherFocusedMethodMakes_IsNotAlone`, `Of_KillThatNoOtherMethodMakes_IsAlone`;
  - `Of_KillThatOnlyABroadTestShares_IsAlone` and `Of_BroadTest_IsInNoCover` (Review Focus 3);
  - `Of_MethodWhoseKillsOthersMake_NamesTheSmallestCoverFound` (A kills m1, m2, m3; B kills m1, m2; C kills m3; D
    kills m1: the cover of A is `[B, C]`);
  - `Of_MethodWithAKillOfItsOwn_HasNoCover`, `Of_MethodThatKillsNothing_HasNoCoverAndCountsWhatItCovers`;
  - `Of_TheoryWhoseRowsRepeatKills_CountsTheRowsNeeded` (four rows, two of which kill all: `RowsNeeded` 2);
  - `Of_TimeoutUnderAMethod_IsCountedAndIsNoKill`;
  - `Of_SurvivorUnderAMethod_IsListed`;
  - `Of_Methods_AreOrderedByFileThenName`.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement `KillEvidence.Of`**
- [ ] **Step 4: Run them and see them pass**
- [ ] **Step 5: Commit**: `feat: condense a kill matrix to what each test method kills`

---

### Task 3: Cut the slices

**Files:**
- Create: `tools/MutationReport/TestSlices.cs`
- Modify: `tools/MutationReport/Cli.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/TestSlicesTests.cs`, and `CliTests.cs`

**Interfaces:**
- Produces:

```csharp
// A file of a test folder: its path from the repository root with '/', its lines, and the partial class it
// declares at the start of a line, if any.
internal sealed record SliceFile(string Path, int Lines, string? PartialClass);

internal sealed record Slice(string Name, int Lines, IReadOnlyList<string> Files);

internal static class TestSlices
{
    public static IReadOnlyList<Slice> Cut(string folder, IReadOnlyList<SliceFile> files, int limit);
}
```

- The command: `slices <repository root> <output folder> [--limit <lines>] [--exclude-from <file>] <folder> ...`.
  It reads every `.cs` file under each folder, but those under `bin/`, `obj/` or a path the exclusion file lists
  (one path from the root to a line, `#` starting a comment), cuts each folder by itself, and writes
  `<output folder>/<name>.txt`, the slice's files one to a line, and `<output folder>/index.txt`, a line
  `<name> <lines> <files>` for each.  `--limit` defaults to 5000.  Exit 2 for a folder that does not exist.

The algorithm, since the signature does not fix it:

1. The files of one folder that declare the same partial class are one unit, at the place of the first; every
   other file is a unit.
2. A folder whose files, at any depth, total at most the limit is one block.  A larger folder is its own files,
   as a first child, and then its sub-folders in ordinal order, each cut by this same rule.  Own files, or a
   folder with no sub-folders, over the limit are packed in path order into parts of at most the limit; a unit
   that is over the limit alone is a part.
3. Neighbouring blocks and parts of one parent are joined, in order, while the sum stays at or under the limit.
4. A slice is named for the deepest folder that holds all its files, without the leading `tests/` and with `.` for
   `/`, then `-` and its number among the slices of that name, from 1: `SqlSource.Tests.Parsing-1`.

- [ ] **Step 1: Write the failing tests**
  - `Cut_FolderUnderTheLimit_IsOneSlice`; `Cut_SiblingFoldersThatFitTogether_AreJoined`;
  - `Cut_FolderOverTheLimit_IsSplitByItsSubFoldersWithItsOwnFilesFirst`;
  - `Cut_FolderOfFilesOverTheLimit_IsPackedInPathOrder`; `Cut_FileOverTheLimit_IsASliceOfItsOwn`;
  - `Cut_FilesOfOnePartialClass_StayTogetherAcrossAPartBoundary`;
  - `Cut_Slices_AreNamedForTheirFolderAndNumbered`;
  - `Cut_AnyTree_PutsEveryFileInExactlyOneSlice` (Review Focus 2: a fixed tree of some forty files of mixed
    sizes at three depths, limits 100, 500 and 5000; the slices' files together equal the input, with no repeat);
  - in `CliTests`: `RunAsync_Slices_WritesAFileForEachSliceAndTheIndex` on a temporary folder, which also shows
    that an excluded folder, `bin/` and an indented `partial class` in a string are left alone;
    `RunAsync_SlicesOfAFolderThatDoesNotExist_ExitsTwo`; and the usage theory's row
    `("condense", "one.json", "two.json")` becomes `("slices", "root")`.  The usage text gains each command's
    line in the task that adds the command; Task 6 shows all five.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run them and see them pass, then cut the real tree**

Run `slices` on the ten test project folders with the exclusions of Task 7 (a scratch list will do here).
Expected: about 40 slices, none over 5,000 lines but a single file's, and `tests/SqlSource.Tests/Parsing` in two or
three.

- [ ] **Step 5: Commit**: `feat: cut the test folders into review slices`

---

### Task 4: Read the test times and the coverage

**Files:**
- Create: `tools/MutationReport/TestTimeReader.cs`, `tools/MutationReport/CoverageReader.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/TestTimeReaderTests.cs`, `CoverageReaderTests.cs`

**Interfaces:**
- Produces:

```csharp
// A test of an xUnit XML report (--report-xunit-xml): one row of a theory each.  File is from the repository
// root, or "" when the report gives none.  Result is "Pass", "Fail", "Skip" or "NotRun".
internal sealed record TestTime(string File, string Type, string Method, string Result, double Seconds);

internal static class TestTimeReader
{
    public static IReadOnlyList<TestTime> Read(Stream xml, string repositoryRoot);  // InvalidDataException
}

internal static class CoverageReader
{
    // The lines with a hit, by file from the repository root, of the files under src/.
    public static IReadOnlyDictionary<string, IReadOnlySet<int>> Read(Stream cobertura, string repositoryRoot);

    // The lines of database that are in none of others, as ranges, by file in ordinal order.
    public static IReadOnlyList<(string File, IReadOnlyList<(int From, int To)> Lines)> Only(
        IReadOnlyList<IReadOnlyDictionary<string, IReadOnlySet<int>>> database,
        IReadOnlyList<IReadOnlyDictionary<string, IReadOnlySet<int>>> others);
}
```

Both read with `XmlReader`, forward only.  The shape of a test element, from a run of 2026-11-12:
`<test name="N.C.M(text: &quot;&quot;)" result="Pass" time="0.025" type="N.C" method="M" source-file="/abs/C.cs"
source-line="58" />`.

- [ ] **Step 1: Make a real report of each kind to read the shape from**

```bash
dotnet test --project tests/SqlSource.Repository.Tests --results-directory "$SCRATCH/results" \
    --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml \
    --report-xunit-xml --report-xunit-xml-filename results.xunit.xml
```

Read how the Cobertura file names a file (absolute, or relative to a `<source>`) and write the reader's tests to
that shape.

- [ ] **Step 2: Write the failing tests**: `Read_Tests_GivesEachRowItsTypeMethodFileAndTime`,
  `Read_SkippedTest_KeepsItsResult`, `Read_TestWithNoSourceFile_HasNoFile`, `Read_TextThatIsNoReport_Throws`;
  `Read_Coverage_GivesTheHitLinesOfSourceFilesOnly` (a line with `hits="0"` and a file under `tests/` are left
  out), `Read_FileInTwoClasses_UnitesItsLines`, `Only_LinesTheOthersAlsoHit_AreLeftOut`,
  `Only_Lines_AreRangesInOrder`.
- [ ] **Step 3: Run them and see them fail**
- [ ] **Step 4: Implement**
- [ ] **Step 5: Run them and see them pass, and read Step 1's two files with the readers**
- [ ] **Step 6: Commit**: `feat: read test times and line coverage for the test review`

---

### Task 5: The `condense` command

**Files:**
- Create: `tools/MutationReport/Condenser.cs`, `tools/MutationReport/EvidenceWriter.cs`
- Modify: `tools/MutationReport/Cli.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/EvidenceWriterTests.cs`, `CondenserTests.cs`

**Interfaces:**
- Consumes: Tasks 1 to 4, and `StaticMutantReader`.
- The run folder, which `measure.sh` of Task 7 fills and this command reads:
  - `slices/<name>.txt` and `slices/index.txt` (Task 3);
  - `mutation/<scope>/reports/mutation-report.json`, and `mutation/<scope>/failed` when the scope's run failed;
  - `tests/<project folder without tests/>/results.xunit.xml` and `coverage.cobertura.xml`;
  - `evidence/`, which this command writes: `<slice>.md`, `gaps.md` and `database-only-lines.md`.
- The command: `condense <repository root> <run folder> [--broad-from <file>]`.  A line of the broad file is a
  prefix of a test's full name, `#` starting a comment.  Exit 2 with the scope's name when a report cannot be
  read, tested a static mutant (`StaticMutants.Tested` over 0) or has a `failed` file beside it (Review Focus 5),
  or when two reports tested one mutant.  It prints one line for each file it writes.
- Produces: `Condenser.RunAsync(string repositoryRoot, string runFolder, IReadOnlyList<string> broad,
  TextWriter output, TextWriter error, CancellationToken cancellation) : Task<int>`, and
  `EvidenceWriter.Write(TextWriter writer, SliceEvidence evidence)` over a record that holds what the file below
  shows.

An evidence file, whose exact text `EvidenceWriterTests` pins:

```markdown
# Evidence: SqlSource.Tests.Parsing-1

Files: 19, lines: 4,812.
- tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs (412)

## Mutation scopes

- generator-parsing: ran 2,284 tests of tests/SqlSource.Tests/Parsing; tested 2,260 mutants of
  src/SqlSource/Parsing.  A kill by a test outside the scope is not seen.

## Tests

### tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs

- `SqlFileParserTests.Parse_EmptyFile_GivesNoQueries`: 1 row, 0.004 s; kills 12, alone 0, timeouts under it 0;
  cover: `SqlLexerTests.Read_Empty_GivesNothing`, `SqlFileParserTests.Parse_OnlyComments_GivesNoQueries`
- `SqlFileParserTests.Parse_Marker_IsRead`: 14 rows, 0.120 s; kills 80, alone 3, timeouts under it 2;
  rows needed 5; no cover
  - alone: src/SqlSource/Parsing/SqlLexer.cs:120:17 Equality mutation
- `SqlFileParserTests.Parse_Case_MatchesItsGoldenFile` (broad): 40 rows, 2.1 s; kills 300
- `SqlFileParserTests.Parse_Text_DoesNotThrow`: 1 row, 0.002 s; covers 31 mutants and kills none
- `SqlFileParserTests.Parse_Skipped_OnThisSystem`: 1 row, skipped; not measured: in no scope's run

## Survivors under these tests

- src/SqlSource/Parsing/SqlLexer.cs:88:13 Conditional (true) mutation -> `(true?a:b)`: covered by 14 methods
  here, among them `SqlLexerTests.Read_Quote_Ends`, `SqlLexerTests.Read_Dollar_Ends`

## Timeouts under these tests

- src/SqlSource/Parsing/SqlLexer.cs: 2 (lines 140, 152).  A timeout is read before it is believed.

## Source these tests reach: uncovered and blind

- src/SqlSource/Parsing/SqlLexer.cs: no test covers lines 40, 52-53; blind, does not compile: 60-75; blind,
  static: 5-12

## Time

- `SqlFileParserTests`: 2.3 s, 4.1% of tests/SqlSource.Tests (56.0 s)
```

Its rules: a slice none of whose tests is in a report has, for "Mutation scopes", the one line "None: no mutation
scope ran these tests, so they are judged by reading and the named-cover rule." and its methods carry times alone
(Review Focus 4).  `alone` lists at most 10 mutants and then "and N more"; a cover at most 8 methods; a survivor
at most 3 methods; a replacement at most 60 characters, on one line.  "rows needed" is shown only when it is less
than the rows.  "Time" lists every class of the slice, slowest first.  A database slice ends with
"Lines only the database projects reach: see database-only-lines.md."  Numbers are invariant, with `,` for
thousands.

`gaps.md` has, for each project, a table of every source file in a folder where the run tested a mutant: tested,
killed, survived, no coverage, timeout, blind (does not compile), blind (static); most survivors and uncovered
first.  `database-only-lines.md` lists, by source file, the ranges that only the projects under
`tests/DatabaseTests/` hit.

- [ ] **Step 1: Write the failing tests**
  - `EvidenceWriterTests.Write_SliceWithEveryKindOfLine_IsTheDocumentedText` (the text above, from a
    `SliceEvidence` built in the test), `Write_SliceThatNoScopeRan_SaysItIsNotMeasured`,
    `Write_LongLists_AreCutWithTheirCount`, `Write_DatabaseSlice_NamesTheLinesFile`.
  - `CondenserTests`, each on a run folder the test writes: `RunAsync_Run_WritesEvidenceForEachSliceAndTheGaps`;
    `RunAsync_TestsOfASliceInTwoScopes_AreMeasuredInBoth`; `RunAsync_SliceInNoScope_IsNotMeasured`;
    `RunAsync_ScopeThatFailed_ExitsTwoNamingIt`; `RunAsync_ReportThatTestedAStaticMutant_ExitsTwoNamingIt`;
    `RunAsync_ScopesThatTestOneMutant_ExitsTwoNamingTheFile`; `RunAsync_RunWithNoMutationFolder_WritesTimesAlone`;
    `RunAsync_DatabaseProjects_WriteTheLinesOnlyTheyReach`.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run them and see them pass**
- [ ] **Step 5: Commit**: `feat: write a slice's evidence from a run's reports`

---

### Task 6: The `kills` command, and the documents of the program

**Files:**
- Modify: `tools/MutationReport/Cli.cs`, `tools/MutationReport/MutationReport.csproj` (its comment)
- Modify: `tools/README.md`, `tests/README.md`, `CONTRIBUTING.md`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/CliTests.cs`

**Interfaces:**
- The command: `kills <report.json> <test name>`, the name a method's full name.  For each mutant a row of it
  kills, in file and place order, one line `<file>:<line>:<column> <mutator> -> <replacement>`, then
  `  also killed by <n> methods: <up to 10 names>` or `  killed by it alone`; then the Timeout mutants its rows
  cover, as `TIMEOUT <file>:<line>:<column> <mutator>`; then a line of counts.  Exit 1 when the report holds no
  test of that name, 2 when it cannot be read.
- The usage, all five lines, which the usage theory of `CliTests` pins:

```text
Usage: compare <before.json> <after.json>
       static <report.json>
       slices <repository root> <output folder> [--limit <lines>] [--exclude-from <file>] <folder> ...
       condense <repository root> <run folder> [--broad-from <file>]
       kills <report.json> <test name>
```

- [ ] **Step 1: Write the failing tests**: `RunAsync_KillsOfAMethod_ListsEachKillWithItsOtherKillers`,
  `RunAsync_KillsOfAMethodWithTimeouts_ListsThem`, `RunAsync_KillsOfANameTheReportLacks_ExitsOne`, and the usage
  theory with the five lines.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run them and see them pass**
- [ ] **Step 5: Update the documents**
  - `tools/README.md`, "Mutation testing": a section "The test review's commands" for `slices`, `condense` and
    `kills`, with the run folder's layout and what an evidence file holds, and that an agent runs the built
    program, `dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll`, never `dotnet run`,
    since two builds of one project at once fail.
  - `tests/README.md`, the row of `tests/SqlSource.Repository.Tests`, and `CONTRIBUTING.md`, the sentence on
    `tools/MutationReport`: it also prepares the evidence of the test review.
  - The comment of `MutationReport.csproj`.
- [ ] **Step 6: Commit**: `feat: answer what one test kills, and document the review's commands`

---

### Task 7: The skill's data and its measuring script

**Files:**
- Create: `.claude/skills/reviewing-test-quality/scopes.txt`, `projects.txt`, `excluded.txt`, `broad-tests.txt`
- Create: `.claude/skills/reviewing-test-quality/measure.sh`
- Test: `tests/SqlSource.Repository.Tests/TestReview/SkillDataTests.cs`

**Interfaces:**
- `scopes.txt`: blocks apart by a blank line, `#` starting a comment, each of five lines:

```text
scope generator-parsing
project generator
mutate **/Parsing/**/*.cs
tests FullyQualifiedName~SqlSource.Tests.Parsing.
folders tests/SqlSource.Tests/Parsing
```

  `mutate` and `folders` take several values apart by a space.  The scopes ("Calls to check" 1), each source
  folder with the test folder of the same path: for the generator, `Diagnostics`, `Parsing`, `Settings`,
  `SettingsFiles`, `Snapshot`, each sub-folder of `Generation` and `Generation`'s own files, and the root files
  with `tests/SqlSource.Tests/Generator` and the root test files; for the tool, `Bundles`, `DotEnv`, `Logging`,
  `Planning`, `Processes`, `Projects`, `Reporting`, `Watching`, each sub-folder of `Describing` and its own
  files, and the root files with `tests/SqlSource.Tool.Tests/Commands`.  A test folder with no source folder of
  its path (`EndToEnd`, `Package`, `TestHarness`, `Fixtures`, `Realistic`) is in no scope.  Where the real folders
  differ from this list, the list follows the folders, and the pull request says so.
- `projects.txt`: the ten test projects a run measures and reviews, one folder to a line, the seven of
  `tests/DatabaseTests/` but `QueryMethodAllocation` among them.  `QueryMethodAllocation` is sliced and reviewed by
  reading and is not run, so it is a line `tests/DatabaseTests/QueryMethodAllocation not-run`.
- `excluded.txt`: what no slice holds: the four libraries, `tests/SqlSource.Tool.StandIn`,
  `tests/SqlSource.Tests/Snapshot/Examples`, `tests/SqlSource.Tool.Tests/Fixtures/Projects`.
- `broad-tests.txt`: `SqlSource.Tests.Generator.QueryMethodCorpusTests.`,
  `SqlSource.Tool.Tests.Planning.GeneratorParityTests.`, `SqlSource.Tool.Tests.Fixtures.BundleCasesTests.`, and
  the classes that run every case of a realistic corpus or every recorded fixture, found by reading the classes of
  `tests/SqlSource.Tool.Tests/Realistic/`, `tests/SqlSource.Tool.Tests/Fixtures/` and `tests/README.md`; each line
  has a comment that says what whole output the class checks.
- `measure.sh <run folder> mutation [<scope> ...]` runs the named scopes, or all, one after another through
  `tools/mutation-test.sh <project> --output <run folder>/mutation/<scope> --tests <tests> <mutate ...>`; it goes
  on after a scope that fails, writes `failed` in that scope's folder, and exits 1 at the end if any failed.
  `measure.sh <run folder> tests [<project folder> ...]` runs the named projects, or all that `projects.txt` does
  not mark `not-run`, one after another, with the options of Task 4, Step 1, into
  `<run folder>/tests/<project folder without tests/>`.  Both append a line to `<run folder>/measure.log` when a
  scope or project starts and ends, with its seconds and exit code.

- [ ] **Step 1: Write the failing tests** of `SkillDataTests`, which read the files through `RepositoryRoot.Path`:
  - `Scopes_EveryBlock_NamesAProjectAndFoldersThatExist`;
  - `Scopes_EverySourceFolder_IsMutatedByExactlyOneScope` (each folder of `src/SqlSource` and
    `src/SqlSource.Tool` that holds a `.cs` file, to the depth the scopes split it, but `Polyfills`, `build`,
    `buildMultiTargeting`, `bin` and `obj`);
  - `Scopes_NoTestFolder_IsInTwoScopes`;
  - `Projects_AndExclusions_TogetherHoldEveryProjectUnderTests` (every `.csproj` under `tests/` but those under
    the two folders of test data);
  - `Exclusions_EveryLine_IsAFolderThatExists`;
  - `BroadTests_EveryLine_IsAClassThatATestFileDeclares`.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Write the four data files and see the tests pass**
- [ ] **Step 4: Write `measure.sh`**, and check it with `tools/shellcheck.sh` and `tools/shfmt.sh` if the
  validation script does not already reach a `.sh` file under `.claude/`; if it does not, say so in the pull
  request rather than change the script.
- [ ] **Step 5: Try it on the smallest of each**

```bash
RUN=artifacts/test-review/trial
dotnet run --project tools/MutationReport -- slices . "$RUN/slices" \
    --exclude-from .claude/skills/reviewing-test-quality/excluded.txt tests/SqlSource.Tool.Tests/DotEnv
.claude/skills/reviewing-test-quality/measure.sh "$RUN" mutation tool-dotenv
.claude/skills/reviewing-test-quality/measure.sh "$RUN" tests tests/SqlSource.Repository.Tests
dotnet run --project tools/MutationReport -- condense . "$RUN" \
    --broad-from .claude/skills/reviewing-test-quality/broad-tests.txt
```

Expected: about a minute for the scope, an evidence file for the `DotEnv` slice with kills near the spike's 100
of 115, and `measure.log` with four lines.  Read the evidence file whole against the report in a browser
(`mutation-report.html`): three tests' kills and one cover, by hand.

- [ ] **Step 6: Commit**: `feat: add the test review's scopes, lists and measuring script`

---

### Task 8: The skill and its briefs

**Files:**
- Create: `.claude/skills/reviewing-test-quality/SKILL.md`, `.claude/skills/reviewing-test-quality/briefs.md`
- Modify: `.claude/skills/reviewing-maintainability/SKILL.md` (one sentence), `AGENTS.md` (a section of two
  lines), `tools/AGENTS.md` (one bullet)
- Modify: `tests/SqlSource.Repository.Tests/Documentation/` only if a test there lists the skills

**Interfaces:**
- Consumes: the commands of Tasks 3, 5 and 6, and the data and script of Task 7.
- The run folder gains, written by agents: `findings/<slice>.md`, `findings/<slice>.read.txt` (the files read in
  full, one to a line), `verdicts/<slice>.md`, `mirrors/<pair>.md`, and `remeasure/<scope>/` for stage 5.

`SKILL.md`: the front matter (`name`, a `description` that says a person starts it, and
`disable-model-invocation: true`), then, in the order a lead follows:

1. **Overview**: the four questions, evidence first, what a run writes, and one sentence that comments, folder
   structure and patterns are `reviewing-maintainability`'s.
2. **Before starting**: the argument, a list of test folders, or everything; the run folder
   `artifacts/test-review/<date>`; the previous appendix, the newest
   `docs/superpowers/specs/*-test-cleanup-findings.md`, if any; which scopes and projects the folders need (a
   scope is needed when one of its `folders` holds, or is held by, a folder of the run); the cost, from the times
   that pull request 1 and Task 9 measured, and the stop: a run whose measurements will not fit in a night is put
   to the owner before it starts.  That these instructions are the opt-in the `Workflow` tool asks for, and that
   without it agents go out in batches of about ten.
3. **Stage 1, measure**: cut the slices first, then `measure.sh` in the background, mutation then tests; nothing
   else builds in the checkout until it ends; a scope that failed is run once more, and a scope that fails twice
   is a gap of the run, reported exactly.
4. **Stage 2, condense**: the command, and reading one evidence file whole before any agent is sent.
5. **Stages 3 and 4**: one reviewer for each slice, with the shared brief written once to the run folder; the
   check of `<slice>.read.txt` against `slices/<slice>.txt` with `comm`, one more reviewer for the missing files,
   and what that one misses as a coverage gap; then one fresh verifier for each slice.
6. **Stage 5**: for each scope with findings that need measurement, one run with every candidate filtered out
   (`--tests '<the scope's filter>&FullyQualifiedName!~<Class>.<Method>'`, one clause for each), compared with the
   scope's report by `tools/mutation-test.sh compare`.  Nothing `LOST` confirms them all; a candidate that the
   scope's report names as a killer of a lost mutant is refuted; `NOT COMPARABLE` is read by the lead.  Rows of a
   theory cannot be filtered, so a fold that drops rows is confirmed by its phase's own guard and says so.
7. **Stage 6**: the three mirror pairs and what each agent is given.
8. **Stage 7**: the writer, the files it writes, and the lead's check of them: no two phases touch one file,
   every finding of the verdicts is in a phase, the appendix or neither by its verdict, and every guard is a
   command that runs.
9. **Finishing**: the two tech-debt items by `docs/tech-debt/AGENTS.md`; `./pre-commit-validation.sh --full`;
   `branch-review:review`; the version check; the pull request holds the epic, the specs, the appendix and the
   two items and no other change, and says each coverage gap exactly.
10. **Common mistakes**: the traps of `reviewing-maintainability` that apply (one `cat` of several files, `zsh`
    globs and word splitting, the finder applying its own finding), and: believing a kill that the test's inputs
    do not explain; taking a `Timeout` for a kill; taking a survivor for a fault of a test whose name promises
    nothing about it; taking a test of the other engine, or a broad test, as cover; running anything that builds
    during a mutation run; `dotnet run` from two agents at once.

`briefs.md`, each brief starting as those of `sweeps.md` do (the root, nothing modified, `bin/`, `obj/` and
`.claude/worktrees/` ignored, the `AGENTS.md` files of the area read first, files read with the Read tool):

- **The shared brief**: the four questions with what counts as each; how to read an evidence file, line by line,
  and its limits (a scope's kills only, theory rows by count, blind areas, timeouts); the finding shape below; the
  six actions with the spec's meaning of each, and that `keep` is recorded only when a measurement made a test
  look removable; the rule for tests that mutation cannot guard, with which slices it binds; the gaps that belong
  to no test, which are not findings; what the review leaves alone, the spec's six entries completed from the
  `AGENTS.md` files and readmes of `tests/` (the allocation budgets by file, the flaky-test rules, the one process
  test of each project, the output a generator test states); the previous appendix, and that what it settled is
  not raised again unless the test or its evidence changed.
- **The reviewer**: its slice's file list and evidence; read every file in full; write `findings/<slice>.md` and
  `findings/<slice>.read.txt`; return the counts by action and the files not read in full.
- **The verifier**: fresh, given the findings, the slice's files and the evidence; for each `delete`, `fold` and
  `demote` it reads the test and every test named as cover, runs `kills` on the test, and looks for what mutation
  cannot see (a diagnostic's wording, an allocation budget, a wait, an engine's behaviour, a static or
  uncompiled method); a kill it cannot explain from the test's inputs is a doubt about the tool, and the verdict
  is `needs measurement`; it spot-reads the `strengthen` and `loosen` findings for a wrong reading of the test
  and refutes those; it writes `verdicts/<slice>.md`.
- **The mirror agent**: both sides' confirmed findings and files; it reports where one side has a finding and the
  twin test on the other side has none, and where the two sides' findings disagree, as new findings for the
  lead to send to a verifier.
- **The writer**: the verdicts, the mirror reports, `gaps.md` and the previous appendix; the three kinds of file
  and what each holds, from "The epic a run writes"; the rules that cut phases; the guard of each kind of phase;
  the two tech-debt items.

A finding, in `findings/<slice>.md`:

```markdown
## F-<slice>-<nn>

- test: `<file>` | `<Class>.<Method>` | rows: all, or the rows named
- question: value | brittleness | redundancy | cost
- action: delete | fold | strengthen | loosen | demote | keep
- evidence: measured (<the numbers of the evidence file>) or read (<what was seen>); blind: yes or no
- cover: the tests that prove the same, each `<file>` | `<Class>.<Method>`; required for delete, fold and demote
- proposal: what the phase would do, in a sentence or two
```

A verdict, in `verdicts/<slice>.md`: `## F-<slice>-<nn>`, then `- verdict: confirmed | refuted | needs measurement`
and `- reason:`.

- [ ] **Step 1: Read what the briefs must name**: `tests/AGENTS.md`, every `AGENTS.md` and readme under `tests/`,
  `docs/tech-debt/AGENTS.md`, and the epic outline `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
  for the shape of an outline here.
- [ ] **Step 2: Write `briefs.md`**, then `SKILL.md`.
- [ ] **Step 3: The other documents**
  - `.claude/skills/reviewing-maintainability/SKILL.md`, in its overview: one sentence that whether a test earns
    its place is the review of `.claude/skills/reviewing-test-quality/`.
  - Root `AGENTS.md`, after "Reviewing maintainability":

    ```markdown
    ## Reviewing test quality

    A review of whether each test earns its place follows the skill in `.claude/skills/reviewing-test-quality/`,
    which only a person starts and which ends in a cleanup epic, never in a change to a test.
    ```

  - `tools/AGENTS.md`: one bullet, that the text `condense` writes is what
    `.claude/skills/reviewing-test-quality/briefs.md` teaches a reviewer to read, so a change to one changes the
    other in the same commit.
- [ ] **Step 4: Check the limits**: `wc -c AGENTS.md` under 16,384, and
  `dotnet test --project tests/SqlSource.Repository.Tests` passes.
- [ ] **Step 5: A dry run by a fresh agent**, given only the two files: it lists the commands it would run and the
  agents it would send for `tests/SqlSource.Tests/Parsing`, and every point where the skill left it unsure.  Fix
  each point.
- [ ] **Step 6: Commit**: `feat: add the reviewing-test-quality skill`

---

### Task 9: The pilot

**Files:**
- Modify: whatever of Tasks 1 to 8 the pilot shows to be wrong
- Modify: `tools/README.md` (the table of measured runs), `SKILL.md` (the costs)

The whole pipeline on `tests/SqlSource.Tests/Parsing`, as the skill says and by following the skill, not this
plan.  Nothing else builds in this checkout while a scope runs.

- [ ] **Step 1: Stages 1 and 2.**  Run folder `artifacts/test-review/pilot`.  Expected: two or three slices; the
  scope `generator-parsing` in about 38 minutes with about 1,968 killed, 209 survived and 83 timed out; the test
  run of `tests/SqlSource.Tests`; evidence files under about 100 KB each.  Read both evidence files whole.
- [ ] **Step 2: Check the evidence by hand.**  For three methods with a cover and two with kills of their own,
  compare the evidence with `kills` and with the HTML report.  For one method the evidence calls covered, apply
  one of its killed mutants by hand, run the cover's tests and see one fail, then restore the file.
- [ ] **Step 3: Cost the driver tests** ("Calls to check" 1).  Run the scope of the generator's root files with
  the driver tests, and one `Generation/` folder (`Paths`, 177 testable mutants) against its own tests and again
  with the driver tests added.  Record the three times and counts in `tools/README.md`, and how many of the
  folder's survivors the driver tests kill.  Put the numbers and the choice to the owner; do not choose.
- [ ] **Step 4: Stages 3 and 4.**  A reviewer and then a verifier for each slice.  Check the lists of files read.
  Read every finding and verdict: is each one true, and would the owner act on it?
- [ ] **Step 5: Stage 5.**  Take the findings that need measurement; if there is none, take one confirmed delete
  and measure it anyway, so that the filter with `&` and `!~` and the comparison are shown to work on this runner.
- [ ] **Step 6: Stage 7.**  The writer, to the real paths.  Read the outline, each phase spec and the appendix,
  and run one phase's guard commands as written.  Stage 6 has no pair inside `Parsing`: say in the pull request
  that the mirror stage ran on no real pair.
- [ ] **Step 7: Discard the epic** (`git status` shows the files the writer made; remove them and the two
  tech-debt items), keeping the run folder.
- [ ] **Step 8: Correct the skill, the briefs and the program** from what each step showed, with a test for each
  fault of the program.  Record in `SKILL.md` the times a run costs.
- [ ] **Step 9: Commit**: `feat: correct the test review from its pilot`, with the pilot's numbers in the body.

---

### Task 10: Review and pull request

- [ ] **Step 1:** `branch-review:review`, fix every finding, the minor ones too, and run it again until clean.
- [ ] **Step 2:** `git fetch origin`, and merge `origin/main` if it has moved (if pull request 280 has merged, this
  is where its merge commit arrives).  Conflicts are diff3: look for `<<<<<<<`, `|||||||`, `=======`, `>>>>>>>`.
- [ ] **Step 3:** `./pre-commit-validation.sh --full` on the tree as it will be pushed.
- [ ] **Step 4:** `git fetch --tags origin`; compare `VersionPrefix` of `Directory.Build.props` with
  `git tag --list 'v*' --sort=-v:refname | head -n 1`.  With no tag there is nothing to compare.
- [ ] **Step 5:** Offer the pull request to the owner, with the pilot's result: the slices, the scope's counts and
  time, the findings by action and by verdict, what the pilot corrected, the cost of the driver scopes, and that
  the mirror stage ran on no real pair.  If pull request 280 is still open, say that this one is stacked on it.
  Open it only when the owner agrees, then turn Auto-fix on.
