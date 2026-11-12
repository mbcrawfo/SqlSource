# Test quality review: design

A project, delivered as three pull requests: mutation testing, a skill that reviews the tests, and the first run of
that skill, whose output is a cleanup epic.

## Intent

The tests are about 200,000 lines in 1,000 files, some 17,800 cases, against 87,000 lines of library code.  The suite
is large and has become slower, and each engine the library adds (MySQL, SQLite) will grow it again.  The owner wants
regular pruning: a repeatable way to find the tests that do not earn their place and to plan their cleanup in small
steps, with evidence rather than opinion behind every removal.

## Goals

- Mutation testing with Stryker.NET, runnable by one command for either source project and for a part of one.
- A skill, run only when a person asks for it, that reviews every test on four questions and ends in a cleanup epic:
  - **Value**: does the test assert what its name and comment say?
  - **Brittleness**: does it assert something that breaks when the code changes without the behaviour changing?
  - **Redundancy**: do other tests already make the same check?
  - **Cost**: for a test that needs a database or a process, is what it proves worth its time and complexity?
- The epic is an outline and one spec per phase, each phase small enough for a pull request a person can review in
  one sitting.
- Every phase carries a guard that proves it lost nothing.

## Non-goals

- Reducing the amount of test code is not a goal.  The ratio of test code to library code means removal is allowed
  where it loses nothing; no finding is made to reach a number.
- No cleanup happens in these three pull requests.  The epic's phases do that, each from its own spec.
- No mutation job in CI or the nightly run, and no mutation score threshold.  **My call**; see "Calls to check".
- The database projects take no part in a mutation run.
- The test libraries (`tests/SqlSource.Testing`, `tests/SqlSource.Tool.Testing`, `tests/SqlSource.Repository`,
  `tests/DatabaseTests/TestHarness`) are read as context and are not reviewed.
- Comments and folder structure stay with the `reviewing-maintainability` skill.

## Anti-goals

- A test is never removed on a mutation result alone.  Stryker's runner for this stack is a preview, and mutation
  cannot see a diagnostic's wording, an allocation budget, a wait or an engine's real behaviour.
- The review never reports what the repository does on purpose (the list is under "What the review leaves alone").
- No reviewer edits a file.  The only files a run writes in the repository are the epic, its phase specs, its
  findings appendix and the tech-debt items of the gaps it measured.

## What was tried

A spike on this branch, 2026-11-12, with `dotnet-stryker` 5.1.0 added to `dotnet-tools.json`:

- **The generator works.**  From `src/SqlSource`, with `tests/SqlSource.Tests` as the test project and the `mtp`
  runner, a run scoped to `Parsing/SqlDialectName.cs` and the tests of `SqlSource.Tests.Parsing` took 3 minutes 10
  seconds: about 50 seconds to build and mutate, 1 minute 50 seconds to capture per-test coverage of 2,284 tests, and
  20 seconds to test 51 mutants, of which 48 were killed.
- **The report is a kill matrix.**  `mutation-report.json` gives each mutant its status, `coveredBy` and `killedBy`
  as lists of test ids, and `testFiles` maps an id to the test's file, full name and line.  In the trial no killed
  mutant had fewer than four killers and some had over 200.
- **Theory rows share a name.**  Each row has its own id, and the 2,284 tests have 545 names, so the report says how
  many rows of a theory kill a mutant and not which rows.
- **The whole project is mutated on every run.**  `mutate` filters which mutants are tested, not which are made: the
  generator yields 19,868, and the scoped run still paid for all of them.
- **About 23% of the generator's mutants cannot be tested.**  When one mutant in a method does not compile, Stryker
  removes every mutant of that method ("safe mode"): 4,572 were lost this way, mostly to `out` and pattern variables
  that a mutated condition leaves unassigned.
- **Stryker refuses the repository root.**  It finds `SqlSource.slnx`, takes the run as a solution run and then
  rejects a named test project.  It runs from the folder of the project it mutates.
- **The tool needs a direct reference.**  From `src/SqlSource.Tool` with `tests/SqlSource.Tool.Tests`, analysis ends
  with "No project found": the test project reaches the tool only through `tests/SqlSource.Tool.Testing`, and Stryker
  looks among the test project's own references.  With a `ProjectReference` to `src/SqlSource.Tool` added and
  `"project": "SqlSource.Tool.csproj"` named, a run scoped to `DotEnv/DotEnvParser.cs` and the tests of
  `SqlSource.Tool.Tests.DotEnv` took 59 seconds: 15,207 mutants made, 2,759 of them untestable, 115 tested, 100
  killed, 4 timed out and 11 survived.  Stryker still warns that its "simulated build" of two test projects failed,
  and goes on.
- **A run leaves nothing behind** in the working tree when `--output` points outside it.

## Constraints

- `global.json` sets the test runner to Microsoft.Testing.Platform and the tests are xUnit v3 4.0.1, so Stryker runs
  with `"test-runner": "mtp"`.  The `vstest` runner cannot run these projects.
- Stryker builds and runs in the working tree, so two runs never share a checkout at the same time.
- `artifacts/` is ignored by git.  A full report is large (5.6 MB for 51 tested mutants) and is never committed.
- A cleanup phase changes no file of `src/`, so the mutants of its "before" and "after" runs are the same mutants
  and can be compared by file, position, mutator and replacement.
- SQL Server's images run under emulation on Apple silicon, so a database test's duration is compared with others of
  its project on the same machine and never with a fixed number of seconds.
- The root `AGENTS.md` is 14,925 bytes of the 16 KB that `AgentsFileTests` allows.
- Every rule of the root `AGENTS.md` holds: `./pre-commit-validation.sh --full` before a push, the docs that a change
  makes wrong are updated in the same pull request, and `branch-review:review` runs before a pull request is offered.

## Approaches

**A. Evidence first (chosen).**  Measure mechanically, then have agents read every test with the measurements beside
it.  It costs hours of machine time before the first agent starts, and it gives each removal a measured reason.

**B. Reading first, mutation as the check.**  Agents read every test with no measurements, and Stryker runs only on
the source behind what they flag.  It starts sooner.  Redundancy across files becomes guesswork, and a test that runs
a line without checking it is found only where an agent happened to look.

**C. Mutation data alone.**  Rank tests by the kill matrix and read only the bottom.  Dropped: it cannot see
brittleness, nor a test whose name does not match its assertions.

The owner chose A, and chose a skill that is kept and run again over a single run, on the condition that it is never
invoked automatically.

## Part 1: mutation testing

Pull request 1.

- **The tool.**  `dotnet-stryker` 5.1.0 in `dotnet-tools.json`, with `rollForward` off like the others.  5.1.0 was
  published on 2026-11-10 and carries fixes to the `mtp` runner; the pin is deliberate.
- **Two configuration files**, `src/SqlSource/stryker-config.json` and `src/SqlSource.Tool/stryker-config.json`,
  where Stryker looks for them, so that a bare `dotnet stryker` in either folder works.  **My call.**  Each names its
  one test project (`tests/SqlSource.Tests`, `tests/SqlSource.Tool.Tests`), the `mtp` runner, `perTest` coverage
  analysis, the `json`, `html` and `progress` reporters, and no threshold that fails a run.
- **`tools/mutation-test.sh`**, which a person or an agent calls from anywhere:
  - `tools/mutation-test.sh <generator|tool> [<glob>...]` runs Stryker for that project from its folder, limited to
    the source files the globs name, and writes to `artifacts/mutation/<project>/` unless `--output` says otherwise.
    A `--tests <filter>` option passes Stryker's `test-case-filter`.
  - `tools/mutation-test.sh compare <before.json> <after.json>` lists every mutant that the first report has as
    killed and the second does not, and exits non-zero when there is one.  This is the guard of a cleanup phase.
- **A direct reference from `tests/SqlSource.Tool.Tests` to `src/SqlSource.Tool`**, which Stryker needs: no rule of
  `tests/AGENTS.md` or check of `TestLayoutTests` forbids it, the tool already grants that project
  `InternalsVisibleTo`, and `tests/README.md` gains the edge in its graph.  **My call.**  If a whole run of the tool
  cannot be made to work, stop and ask the owner before going on, since a third of the suite would then have no
  kill matrix.
- **One full run of each project** on the finished configuration, with its wall time and its counts of killed,
  surviving, uncovered and untestable mutants recorded in `tools/README.md`.
- **A ceiling on a full run.**  One project's full run fits in a night, about ten hours.  Tune `concurrency` and
  drop mutators that only make noise to get there.  If it still does not fit, stop and ask the owner: lowering the
  mutation level, sampling and reusing an older report each weaken the evidence behind a removal, and that trade is
  the owner's.  **My call.**
- **Documents.**  A "Mutation testing" section in `tools/README.md` with the commands, what the report holds, the
  limits (the preview runner, the untestable methods, theory rows) and the measured times; a line in
  `tools/AGENTS.md` that the script is not a check and stays out of `pre-commit-validation.sh` and `build.yml`;
  Stryker among the local tools of `CONTRIBUTING.md` with the command; `StrykerOutput/` in `.gitignore` for a bare
  run.
- **Stryker's limits are a limitation note, not tech debt.**  They are an outside tool's, and they go in
  `tools/README.md`.

A cross-project kill is not measured: a mutant of the generator that only a test of the tool or of a database project
kills is reported as surviving.  The review treats a survivor as a statement about the mirrored test project alone.

## Part 2: the skill

Pull request 2.  `.claude/skills/reviewing-test-quality/`, with `disable-model-invocation: true` in the front matter
of `SKILL.md`, so that only a person's `/reviewing-test-quality` starts it.  A run takes an optional list of test
folders; with none it reviews everything.

### Stages

1. **Measure.**  No agents.  Started first and run in the background, one after another in one checkout:
   - `tools/mutation-test.sh generator` and `tools/mutation-test.sh tool`, whole;
   - one run of every test project with coverage and with each test's duration, the database projects on their
     default images.  Each project writes its own Cobertura file, as `CONTRIBUTING.md` shows, which is what tells
     the lines only the database projects reach.  `tests/DatabaseTests/QueryMethodAllocation` is not run: it is a
     test project only under `RunQueryMethodAllocation=true` and skips under coverage, so it is reviewed by reading.
2. **Condense.**  A program of the skill reads the reports and writes one evidence file per slice, small enough for
   an agent to read whole.  A slice's evidence holds:
   - for each test method: its rows, its duration, the mutants it kills, how many of those nothing else kills, and
     the smallest set of other tests found that kills everything it kills;
   - tests that cover mutants and kill none;
   - for the source files the slice mirrors: surviving mutants with the tests that cover them, mutants no test
     covers, and the methods Stryker could not test;
   - for a database slice: each class's share of its project's time, and the source lines that only the database
     projects reach.
3. **Review.**  One read-only agent per slice.  Slices are cut by measured size: at most about 5,000 lines of test
   code, never splitting a file, and keeping the files of one `partial` class together.  Each agent reads every file
   of its slice in full, with the evidence file and a brief shared by all slices, and reports findings in a fixed
   shape.  It lists the files it read in full; a file missing from that list goes to one more agent, and what that
   agent does not read either is a coverage gap, reported exactly.  This is the mechanism of step 3 of
   `.claude/skills/reviewing-maintainability/SKILL.md` and of its `sweeps.md`, copied with its traps.
4. **Verify.**  A fresh agent per slice, which did not find the findings, tries to refute each one that proposes to
   remove, merge or move a test.  It reads the test and the tests said to cover for it, checks the kill matrix, and
   looks for what mutation cannot see.  Its verdict is confirmed, refuted or needs measurement.
5. **Measure again, where asked.**  A finding that needs measurement gets a scoped run with the candidate tests
   filtered out, compared with the same scope with them in.  Nothing newly surviving confirms it.
6. **Compare the mirrors.**  One agent for each pair that is built to resemble another, given both sides' confirmed
   findings and files: the PostgreSQL and SQL Server projects; `Generator/` and `Generation/` of
   `tests/SqlSource.Tests`; the tool's `Describing/` tests and each database project's `Describer/`.
7. **Write the epic.**  One agent groups the confirmed findings into phases and writes the outline, a spec per phase
   and the findings appendix.

A full run is about 40 reviewers, 40 verifiers, three mirror agents and one writer.  The skill's instructions are
the opt-in the `Workflow` tool asks for; without that tool it dispatches agents in batches.

### A finding

- the test: file, class, method, and its rows when it is a theory;
- the question it fails: value, brittleness, redundancy or cost;
- the evidence, marked **measured** (with the numbers) or **read** (with what was seen), and marked **blind** when
  the code under test is in a method Stryker could not mutate;
- the action:
  - **delete**: the test proves nothing that others do not;
  - **fold**: several tests become one theory, or a theory loses rows;
  - **strengthen**: assert what the name promises, which is what a surviving mutant under a covering test shows;
  - **loosen**: assert the behaviour and not its incidental form;
  - **demote**: prove at the unit level what a database or process test proves without needing the engine;
  - **keep**: recorded only when a measurement made the test look removable and reading showed why it is not.

### Broad tests are not cover

The owner decided: a test that checks a whole output at once never counts as the cover that makes a focused test
redundant.  One theory, `QueryMethodCorpusTests.Run_Case_MatchesItsGoldenFile`, kills most mutants of the emitter
through its golden files, and a golden-file difference says that something changed, not which rule broke.  The
condensing program leaves the broad tests out of every cover set and out of the count of what "nothing else kills",
and a focused test is redundant only when other focused tests kill everything it kills.  The broad tests are the
golden corpus, the realistic corpora, `GeneratorParityTests` and the bundle cases; the builder completes the list
from `tests/README.md`, and it is data of the skill that a run can be corrected in.

### Tests that mutation cannot guard

The database projects take no part in a mutation run, and removing one of their tests rarely lowers the line
coverage of `src/`, since what they exercise is generated code and the engine.  The owner decided what the review
may propose there:

- **Delete** or **fold** only with a named cover: a test, on the same engine, that a verifier has read and confirmed
  proves the same behaviour.  A test of the other engine is never the cover.
- **Demote** only with the unit test that takes over named or specified, which the mutation guard then covers.
- The phase spec lists each removed test beside its cover, and its guard is that list and the project's run time
  before and after on one machine.

The same rule holds for the other tests with no source under mutation: `tests/SqlSource.Repository.Tests` and the
tests of the test libraries, such as `tests/SqlSource.Tests/TestHarness/`.  **My call.**  It also holds for the
tool's tests if the tool cannot be mutated.

### Gaps that belong to no test

A mutant that no test covers, or that survives under tests whose names promise nothing about it, is a gap in the
tests and not a fault of one.  No action above adds a test, and the epic does not: it prunes and repairs.  The
appendix counts these gaps by source file, and a run writes one item in `docs/tech-debt` for each source project,
which links the appendix and names the worst files, as the root `AGENTS.md` requires of a problem found and not
fixed.  A later run updates those items and does not add more.  **My call.**

### What the review leaves alone

The shared brief lists what the repository does on purpose, so no agent reports it.  The builder writes the list from
the `AGENTS.md` files and readmes of `tests/`; it holds at least:

- `tests/SqlSource.Tests.RoslynFloor` compiling `Generator/` a second time, and the two driver-floor projects,
  `Postgres.Npgsql8` and `SqlServer.SqlClient61`;
- the same behaviour proved once on each engine;
- the allocation budgets, and `tests/DatabaseTests/QueryMethodAllocation`;
- the golden corpus of query methods, the one place where a whole generated text is the assertion;
- the realistic corpora and the recorded fixtures;
- the tests of `tests/SqlSource.Repository.Tests`, which have no source under mutation and are judged by reading.

### The epic a run writes

- `docs/superpowers/specs/YYYY-MM-DD-test-cleanup-epic-design.md`, the outline, kept current until the epic closes as
  `docs/superpowers/AGENTS.md` requires: the phases in order, each with its scope, its count of findings by action
  and its state.
- `docs/superpowers/specs/YYYY-MM-DD-test-cleanup-phase-NN-<slug>-design.md`, one per phase, holding:
  - the files it touches and the findings it applies, each with its evidence and action;
  - the guard: the `tools/mutation-test.sh` scope to run before and after, the `compare` that must list nothing, and
    the line coverage of the touched source folders, which must not fall; or, for tests that mutation cannot guard,
    the list of named covers and the run times;
  - the documents that name a test it removes or renames, such as `tests/AGENTS.md` and the items of
    `docs/tech-debt`, which the phase updates;
  - what it leaves out.
- `docs/superpowers/specs/YYYY-MM-DD-test-cleanup-findings.md`, the appendix: the counts, every finding refuted and
  why, every **keep**, and every coverage gap.  A later run reads the previous appendix first and does not raise
  again what it settled, unless the test or its evidence has changed.

Phases are cut by these rules:

- one test folder, or part of one, and one kind of action where the findings allow;
- about 1,500 changed lines at most, except a phase that only deletes;
- no two phases touch one file;
- deletions with measured backing come first, since they shrink what every later phase reads; then folds; then
  strengthening and loosening; the database projects last.

### Boundary with `reviewing-maintainability`

That skill keeps its sweeps of comments, structure and patterns, and its rule that it does not change a test.  Each
skill gains one sentence naming the other.  The root `AGENTS.md` gains a section of two lines for the new skill, as
it has for the old one.

### Proving the skill

Before the pull request is offered, the pipeline is run end to end on `tests/SqlSource.Tests/Parsing`, which is
8,650 lines and so two slices, up to a small epic that is read and then discarded.  The skill is corrected from what
that pilot shows.

## Part 3: the first run

Pull request 3, opened from a run of the merged skill over everything.  It holds the epic outline, the phase specs,
the appendix and the two tech-debt items of the gaps, and no other change.  It needs no implementation plan: the
skill is the plan.

## Left to the builder

- The language and place of the condensing program.  It must cope with a report of hundreds of megabytes and add no
  required tool that `CONTRIBUTING.md` does not list; a .NET program run with `dotnet run` is the obvious choice.
- How each test's duration is taken (xUnit v3's own `-result-trx`, or a report extension of the platform).
- Whether `killedBy` holds every killing test or only the first to fail in some cases, and `--disable-bail` if so.
- Stryker's `concurrency`, `mutation-level` and any `ignore-mutations` that the full runs show to be noise.
- Whether any change of setting lowers the share of untestable mutants, within an hour's trying.
- The model and effort of each kind of agent, and the wording of the briefs.
- The exact shape of the evidence and finding files.

## Calls to check

These are mine, not the owner's:

1. Configuration files beside the two source projects, not under `tools/`.
2. No CI or nightly mutation job.  If the owner wants one later it is new work, not deferred work of this design.
3. `tests/SqlSource.Tests` alone tests the generator's mutants, and `tests/SqlSource.Tool.Tests` alone the tool's.
4. Three pull requests, in order, with the pilot inside the second.
5. The findings appendix as the memory between runs.
6. The phase limits: 1,500 changed lines, one folder, one kind of action.
7. A direct reference from `tests/SqlSource.Tool.Tests` to `src/SqlSource.Tool`, if Stryker needs it.
8. A ceiling of one night on a project's full run, and a stop to ask the owner beyond it.
9. Gaps that belong to no test go to the appendix and to one tech-debt item per source project, not into the epic.
10. The named-cover rule of the database tests also binds the repository's tests and the tests of the test libraries.
