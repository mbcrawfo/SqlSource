# Briefs for the test review

The lead writes `brief.md` of the run folder once: the repository root, the run folder in full, the previous appendix's path or that there is none, and then the two sections "Every agent" and "The shared brief", whole.  An agent's prompt is the path of that file, the text of its own section below, pasted, and the paths that section says it is given.  Every agent is general-purpose, on the lead's model.

## Every agent

- No file of the repository is created, changed or deleted.  You write only the files your brief names, in the run folder.  The writer alone writes in the repository, and only the files its brief names.
- You run no build, no test, no mutation run and no git command that changes anything.  The one program you run is the built one your brief names, `dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll`, never `dotnet run`.
- `bin/`, `obj/` and any `.claude/worktrees/` folder are ignored.
- Read a file with the Read tool, never several files through `cat` in one shell command: shell output past about 30 KB is cut to a preview, and the rest is never seen.  A long file read in several calls whose ranges cover it all counts as read in full.
- `slices/`, `evidence/`, `mutation/`, `findings/`, `verdicts/` and `mirrors/` are folders of the run folder.  A path of the repository is from its root.

## The shared brief

**What the review asks of each test**, and nothing else:

- **Value**: does the test assert what its name and its comment say?  A test fails this when the behaviour its name promises could be removed and the test would still pass.
- **Brittleness**: does it assert something that breaks when the code changes and the behaviour does not: the order of things that have none, a private name, the layout of a text where only one part is the rule, a count of calls, a time?
- **Redundancy**: do other focused tests (every test that is not broad, below) already make the same check, on the same behaviour, so that this one could go, or its theory lose rows, and nothing would be lost?
- **Cost**: for a test that needs a database or a process, is what it proves worth its time, or could it be proved without the engine?

Comments, names, folder structure and duplicated helpers are another review's, `.claude/skills/reviewing-maintainability/`, and are not findings here.  A finding is one the owner would act on: report few and true ones, verify each against the code before writing it, and never report one to reach a number.  Removing tests is allowed where it loses nothing and is not the aim.

**Read first** the `AGENTS.md` files that cover your files, from `tests/AGENTS.md` down, and report nothing they call deliberate.  Read the test libraries your tests call (`tests/SqlSource.Testing`, `tests/SqlSource.Tool.Testing`, `tests/SqlSource.Repository`, `tests/DatabaseTests/TestHarness`) as far as you need to understand a test; they are context and are not reviewed.

**The evidence file** of a slice, `evidence/<slice>.md`, is what was measured.  Read it whole, beside the tests.

- `## Mutation scopes` names each mutation run that ran tests of the slice: the tests it ran and the folders of `src/` it mutated.  A mutant is one small change to the source, and a test kills it when the test fails on it.  **A scope sees only its own source and its own tests.**  A test whose subject is outside the scope's source, as a test that drives the whole generator is when the scope mutated only the root files, is unmeasured for that subject.  When the section says `None`, no number below exists and every test of the slice is judged by reading.
- A test line is `` `Class.Method`: rows, time; kills K, alone A, timeouts under it T; rows needed R; cover: ... ``.  The rows of a theory are one line, since a report tells them apart only by number.
  - `kills K`: mutants that a row of it fails on.  `alone A`: of those, the ones no other focused test fails on, listed below the line.  A test with a kill of its own is not redundant, whatever else is true of it.
  - `cover: X, Y`: other focused tests that together fail on everything it fails on, the smallest set found.  It says that X and Y would catch the same changes to the scope's source, **not that they prove the same behaviour**: read X and Y before calling the test redundant.  Covers are found for each test by itself, so two tests may each be the other's cover, and both cannot go.  `no cover` means it has a kill of its own.
  - `timeouts under it T`: mutants it reaches whose tests ran out of time.  A report names no killer for a timeout, so this test may be the one that detects them: a test with timeouts under it is never deleted on its numbers alone, and the finding says so.
  - `rows needed R`, shown when it is less than the rows: that many of its rows fail on everything all its rows fail on.  It says a theory may be able to lose rows, not which ones, and not that the others assert nothing mutation cannot see.
  - `covers N mutants and kills none`: it runs code and checks nothing that a mutant changes.  Either its assertions are weak, which is a **value** finding, or what it checks is what mutation cannot see (below).
  - `reaches no tested mutant`: it runs nothing the scope mutated.  `not measured: in no scope's run`: the scope did not run it; judge it by reading.
  - `(broad)`: a test that checks a whole output at once (the golden corpus, the realistic corpora, the recorded fixtures, the bundle cases, `GeneratorParityTests`).  A broad test is never cover: the numbers already leave it out, and you never name one as the reason another test may go.
- `## Survivors under these tests`: mutants that tests of the slice reach and no test of the scope fails on.  The tests are named only when at most three reach it.  A survivor under a test whose name promises the behaviour the mutant breaks is a **strengthen** finding.  A survivor under tests whose names promise nothing about it is a gap that belongs to no test, and is not a finding.
- `## Timeouts under these tests`: where the timeouts are.
- `## Source these tests reach: uncovered and blind`: lines no test covers, and lines where no run can test a mutant, because the mutant does not compile or is in a static initialiser.  Mark a finding **blind** when the code its test is about is on such lines: its numbers say nothing there, and a low count of kills is no evidence.
- `## Time`: each class's time and its share of its project's.  Times are compared within one project of one run, never with a number of seconds: SQL Server's images run under emulation on Apple silicon.

**What mutation cannot see**, so that a test may be the only guard of it and kill nothing: the wording of a diagnostic or a message, which is static text; an allocation budget; a wait, a timeout or an order of events; what a real database engine does; what a build or a process does; code that is blind.  A test of one of these is judged by reading.

**A finding** goes in `findings/<slice>.md`, in this shape and no other:

```markdown
## F-<slice>-<nn>

- test: `<file>` | `<Class>.<Method>` | rows: all, or the rows named
- question: value | brittleness | redundancy | cost
- action: delete | fold | strengthen | loosen | demote | keep
- evidence: measured (<the numbers of the evidence file>) or read (<what was seen, with file:line>); blind: yes or no
- cover: the tests that prove the same, each `<file>` | `<Class>.<Method>`; or `none`
- proposal: what a cleanup phase would do, in a sentence or two
```

- `test` is one test.  A **fold** of several tests into one theory names them all, apart by `;`.
- A row is named by its arguments as the source writes them, with its line: the evidence counts rows and cannot tell them apart.
- `cover` is required for **delete** and **fold**, and is `none` for **strengthen**, **loosen** and **keep**.  For a **demote** it is the unit test that takes over, or `to be written: <Class>.<Method> in <file>` with what it asserts in the proposal.
- A survivor or a kill is named as the evidence names it: `<file>:<line>:<column> <mutator>`.

The actions:

- **delete**: the test proves nothing that the named cover does not.
- **fold**: several tests become one theory, or a theory loses the rows named.
- **strengthen**: assert what the name promises.  Name the survivor or the missing assertion.
- **loosen**: assert the behaviour and not its incidental form.  Say what would break the test without changing the behaviour.
- **demote**: prove at the unit level what a database or process test proves without needing the engine.  Name the unit test that takes over, or specify it.
- **keep**: recorded only when a measurement made the test look removable (a cover, no kill of its own) and reading showed why it is not.  It stops the next run raising it again.

**Tests that mutation cannot guard**: the database projects (`tests/DatabaseTests/`), `tests/SqlSource.Repository.Tests`, the tests of the test libraries (`tests/SqlSource.Tests/TestHarness/`, and those of `tests/SqlSource.Tool.Tests` that test `tests/SqlSource.Tool.Testing`), every slice whose evidence says `None`, and every test whose subject is outside its scope's source.  There:

- **delete** and **fold** need a named cover: a test that you read and that proves the same behaviour.  For a database test the cover is on the same engine; a test of the other engine is never the cover, since the same behaviour is proved once on each engine on purpose.
- **demote** needs the unit test that takes over, named or specified.

**What the review leaves alone**, because the repository does it on purpose.  None of these is a finding, and none is cover for another test:

- `tests/SqlSource.Tests.RoslynFloor` compiling `tests/SqlSource.Tests/Generator/` a second time, and the two driver-floor projects, `tests/DatabaseTests/Postgres.Npgsql8` and `tests/DatabaseTests/SqlServer.SqlClient61`, repeating tests on the oldest supported driver.
- The same behaviour proved once on each engine, and a test that a version gate skips on some images.
- The allocation budgets that `tests/AGENTS.md` lists, and `tests/DatabaseTests/QueryMethodAllocation`.
- The golden corpus of query methods, the one place where a whole generated text is the assertion; the realistic corpora; the recorded fixtures of the describers; the bundle cases.  A broad test is not a finding for being broad or slow.
- The comparisons of `tests/DatabaseTests/Postgres.SyncAsync` and `tests/DatabaseTests/SqlServer.SyncAsync`, each synchronous method beside its asynchronous twin.
- The one test of each project that starts the built tool as a process, and the builds of `OnlineBuildTests`.
- The tests of `tests/SqlSource.Repository.Tests`, which have no source under mutation: judge them by reading, under the named-cover rule.
- What the `AGENTS.md` files of `tests/` require of every test: its stated output, its waits through `Eventually`, its clock, its names.
- A test that is the record of something: a known limit, a regression, a decision.  A comment in its file says so, or an `AGENTS.md`, a readme or an item of `docs/tech-debt/` or `docs/deferred/` names it.  It is not redundant on its kills, and its rows are not dropped because another test reads the same text.  Before a **delete** or a **fold**, search those documents for the test's name and its class's (`grep -rn` over the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/`), read the comment above the test and its group, and say in the finding's evidence what both gave.

**Gaps that belong to no test** are not findings: a mutant no test covers, or one that survives under tests that promise nothing about it.  The review prunes and repairs tests and proposes no new one, but the unit test a **demote** needs.

**The previous review**, when the lead names one, is `docs/superpowers/specs/<date>-test-cleanup-findings.md`.  Read its entries for your files first.  What it refuted or recorded as **keep** is not raised again unless the test or its evidence has changed since, and then the finding says what changed.

## The reviewer

Given: the shared brief, the slice's name, its file list `slices/<slice>.txt` and its evidence `evidence/<slice>.md`.

Read every file of the list in full, then the evidence, then judge each test method.  Your scope is the listed files and nothing else, even where a listed folder holds other files.  Write:

- `findings/<slice>.md`: the findings, numbered from 01, or the one line `No findings.`;
- `findings/<slice>.read.txt`: the files you read in full, one path to a line, exactly as the list spells them.  A file you read only in part, or not at all, is not in it.

A reviewer sent for the files that another did not read is given those files as its list, with the slice's name and evidence, and writes `findings/<slice>-rest.md`, with ids `F-<slice>-R<nn>`, and `findings/<slice>-rest.read.txt`.

Return, in a few lines: the count of findings by action, and the files not read in full with the reason.

## The verifier

A fresh agent, never one that wrote a finding it checks.  Given: the slice's name, its findings `findings/<slice>.md` (and `findings/<slice>-rest.md` when there is one), its file list `slices/<slice>.txt` and its evidence `evidence/<slice>.md`.  The report of a scope that the evidence names is `mutation/<scope>/reports/mutation-report.json`.

For each **delete**, **fold** and **demote**, try to refute it:

1. Read the test and every test named as its cover.  Do they prove the same behaviour, on the same inputs that matter, on the same engine?
2. Run `dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills <report.json> <Class.Method>` for the test, in the report of each scope that ran it.  For each mutant the test kills, would you expect that test to notice that change, from its inputs?  A kill you cannot explain is a doubt about the tool, not a fact about the test.  Its lines `TIMEOUT` and `SURVIVED` are the mutants the test reaches that ran out of time or that nothing fails on: a timeout under the test that is not also under a named cover is a reason for **needs measurement**.  A test that no scope ran has no kills to read: skip this step, and hold it to the named-cover rule.
3. Look for what mutation cannot see, as the shared brief lists it: is the test the only guard of a wording, a budget, a wait, an engine's behaviour, or of blind code?
4. Make the search of the documents yourself, as the shared brief says under "A test that is the record of something", and read the comment above the test and above its group.  A test, or a row, that a comment or a document names as the record of a limit, a regression or a decision is **refuted** as a delete or a fold, unless the cover is named as that record too.
5. For a database test, a test of `tests/SqlSource.Repository.Tests` or a test of a test library: is the named cover one you read and confirm?

Read each **strengthen**, **loosen** and **keep** finding against its test too: it is **confirmed** when it reads the test rightly and **refuted** when it does not.

Write `verdicts/<slice>.md`, an entry for every finding:

```markdown
## F-<slice>-<nn>

- verdict: confirmed | refuted | needs measurement
- reason: what you read or ran, with file:line
```

- **confirmed**: you tried to refute it and could not.
- **refuted**: with the reason, which the appendix keeps so that the next run does not raise it again.
- **needs measurement**: the kill matrix cannot settle it: the test has timeouts under it, a kill you cannot explain, or a cover whose kills you doubt.  Name the scope to run again and the test in full, `Namespace.Class.Method`, as the last line of `kills` gives it.  A fold that drops rows of a theory cannot be measured by a filter, since the rows share a name: confirm it by reading, and say in the reason that the phase's own guard proves it.

Return the counts by verdict.

## The mirror agent

One for each of three groups of test folders built to resemble each other, when every side of the group is in the run:

- `engines`: the PostgreSQL projects and the SQL Server projects under `tests/DatabaseTests/`, each project beside its twin;
- `generator`: `Generator/` and `Generation/` of `tests/SqlSource.Tests`;
- `describers`: `Describing/` of `tests/SqlSource.Tool.Tests`, `tests/DatabaseTests/Postgres/Describer/` and `tests/DatabaseTests/SqlServer/Describer/`, three sides.

Given: the pair's name, and for each side the names of its slices, whose findings, verdicts and file lists are in `findings/`, `verdicts/` and `slices/`.  Read the confirmed findings of each side, and for each the twin test on the other side.  Report, in `mirrors/<pair>.md` and in the finding shape with ids `F-<pair>-<nn>`:

- a confirmed finding whose twin on the other side has the same fault and no finding;
- two findings of the two sides that contradict each other, with which one the code supports.

A difference between the sides that the engines explain is not a finding.  Across the two engines a test of one is never cover for a test of the other; a mirror finding there is a twin fault, never a redundancy.  The lead sends the mirror findings to a verifier.

## The writer

Given: `findings/`, `verdicts/` and `mirrors/`, whole; `evidence/gaps.md`; `run-notes.md` of the run folder, the lead's record of the run, its scopes, its failures and its coverage gaps; and today's date.  Read `docs/superpowers/AGENTS.md` and `docs/tech-debt/AGENTS.md` first.  The writer is the one agent that writes in the repository, and only the files named here.

**A cover stays.**  Before cutting phases, list every test that a confirmed **delete**, **fold** or **demote** names as cover.  A finding that would delete one of them, or drop the rows that make it a cover, is withdrawn: it goes to the appendix as a **keep**, with the finding that leans on it as the reason.  Where two findings lean on each other, the one whose test asserts more stays a test.

Only confirmed findings go into a phase: a verdict of **confirmed**, or of **needs measurement** with a line `measured: confirmed` below it.  A finding left at **needs measurement**, or `measured: unsettled`, goes to the appendix as unsettled, and one `measured: refuted` as refuted.

**The phases** are cut by these rules:

- one test folder, or part of one, and one kind of action where the findings allow;
- about 1,500 changed lines at most, except a phase that only deletes;
- no two phases touch one file, which comes before the rule of one kind: the files that hold findings of several kinds, and the files a fold ties to them, are a phase of mixed kinds;
- deletions with measured backing first, since they shrink what every later phase reads; then folds; then strengthening and loosening; the database projects last.  A phase of mixed kinds takes the place of the last kind it holds.

The rules give no least size: say in your report when they cut phases that together change fewer than a few hundred lines, and leave them cut.

**The files**, with `<date>` today's:

- `docs/superpowers/specs/<date>-test-cleanup-epic-design.md`, the outline, which is kept current until the epic closes: what the run was (its date, its folders, its scopes with their counts, what it could not see); how a phase is done; and the phases in order, a row each with its number, its scope, its count of findings by action, its kind of guard and its state: `not started`, then `in progress` and `done` as its pull request goes.  The row's scope is the part of the test folder the phase touches.  The outline names the commit the run read, from `run-notes.md`: a line number of a finding is of that commit, and a phase finds each test by its name.
- `docs/superpowers/specs/<date>-test-cleanup-phase-<NN>-<slug>-design.md`, one for each phase:
  - the files it touches;
  - the findings it applies, each with its id, its test, its action, its evidence, its cover and the verifier's reason;
  - **the guard**.  For tests under a mutation scope:
    - the `tools/mutation-test.sh` command of the scope, from `.claude/skills/reviewing-test-quality/scopes.txt`, to run before the change and after it with two `--output` folders.  Phases of one scope done one after another may take one's "after" report as the next one's "before";
    - `tools/mutation-test.sh compare <before> <after>`, which must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test the phase removed or changed killed, or had as a timeout under it, in the "before" report, as `kills` shows for each such test: it is a kill that did not repeat, and the phase's pull request names it;
    - for a **strengthen**, each survivor the finding names is `Killed` in the "after" report;
    - the line coverage of the touched source files, which must not fall: the test project's own run with `--coverage`, as `CONTRIBUTING.md` shows it for the solution, before and after, and the files' figures in the report of `dotnet reportgenerator`.

    For tests that mutation cannot guard, and besides the above for each finding that is blind or whose test reaches no tested mutant: the list of each removed test or row beside its named cover, and the project's run time before and after on one machine;
  - the documents that name a test it removes, renames or takes rows from, found by a search for the test's and the class's name in the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/`, which the phase updates.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are;
  - what it leaves out, and why.
- `docs/superpowers/specs/<date>-test-cleanup-findings.md`, the appendix, the memory between runs: the counts by slice, action and verdict; every finding refuted, with its reason; every **keep**; every finding left unsettled; every coverage gap and failed scope, exactly; the tests that stay because a finding names them as cover, each with the findings that lean on it; and the gaps that belong to no test, counted by source file: the survivors and uncovered mutants of `evidence/gaps.md`, less those a **strengthen** names.
- One item in `docs/tech-debt/` for each source project all of whose scopes were run, for the code its tests do not check: it links the appendix and names the files with the most surviving and uncovered mutants.  When an earlier run's item exists, it is updated and none is added.  A run of only some of a project's scopes writes and changes no item, and the appendix says so.  The format, the id and the row in `docs/tech-debt/README.md` are as `docs/tech-debt/AGENTS.md` says.

Return the phases with their counts, and anything the rules could not place.
