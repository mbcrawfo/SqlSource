# Test cleanup phase 59: tests/SqlSource.Tool.Tests/DotEnv, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/DotEnv`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (tool-dotenv)

## Files

- `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests-2-01

- test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs` | `DotEnvParserTests.Parse_Text_HoldsTheValuesOfTheToolsKeys` | rows: `"unquoted value ends at a # that follows white space", "SQLSOURCE_CONNECTION_A=Host=x # the dev box"` (line 27) and `"a byte order mark, a carriage return and no final break, the last of a key wins", "\uFEFFSQLSOURCE_CONNECTION_A=1\r\nSQLSOURCE_CONNECTION_A=2"` (line 83)
- action: strengthen
- evidence: measured (survivors under this test alone: src/SqlSource.Tool/DotEnv/DotEnvParser.cs:46:9 Block removal mutation; src/SqlSource.Tool/DotEnv/DotEnvParser.cs:225:79 Arithmetic mutation `index + 1`; src/SqlSource.Tool/DotEnv/DotEnvParser.cs:228:17 Statement mutation `;`).  Read: the byte order mark row puts the mark on a line whose key the next line sets again, so a mark left in place makes line 1 a key that is no name and not the tool's (DotEnvParser.cs:98 passes it over) and the expected `2` still comes from line 2; the "ends at a #" row has white space on both sides of its only `#`, so checking the character after the `#` (225:79) or taking the last ` #` instead of the first (228:17, no `break`) gives the same `Host=x`.  The "keeps a # with no white space before it" row (`a#b`, line 23) has no white space after either.  blind: no
- cover: none
- proposal: Give the byte order mark a row of its own whose marked line is the only one of its key (`"\uFEFFSQLSOURCE_CONNECTION_A=1"` → `A`, `1`), and add rows `Host=x # one # two` → `Host=x` and `Password=a# b` → `Password=a# b`.  The other rows stay: each is a rule `docs/architecture/tool.md` says this class holds a case of.
- verdict: confirmed
- verifier's reason: `kills` (tool-dotenv) for `DotEnvParserTests.Parse_Text_HoldsTheValuesOfTheToolsKeys` lists SURVIVED for DotEnvParser.cs:46:9 Block removal, 225:79 Arithmetic `index + 1` and 228:17 Statement `;`, and the evidence's survivor list names no other method of the slice for 46:9.  Read: the row at DotEnvParserTests.cs:85 holds a real U+FEFF (bytes `ef bb bf`); with the strip removed, line 0's key starts with the mark, is no name (U+FEFF is not a letter or digit, DotEnvParser.cs:278) and does not start with `SQLSOURCE_CONNECTION`, so DotEnvParser.cs:98 passes it over silently and line 1 still gives `2`.  The row at :29 has white space on both sides of its one `#`, so checking `line[index + 1]` (225:79) or not breaking at the first comment (228:17) gives the same `Host=x`; the row at :24 (`a#b`) has white space on neither side.  The proposed rows each fail on one mutant: a lone marked line expects `1`, `Host=x # one # two` ends at the first ` #`, and `Password=a# b` keeps its `#` only while the character before it is read.  docs/architecture/tool.md:158 names the class as holding a case of each rule, which the proposal keeps.
- survivors to kill: `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:225:79`, `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:228:17`, `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:46:9`
- condition: The proposal holds the byte order mark as a literal character: write it as `\uFEFF` in the test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-dotenv`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-59/tool-dotenv/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.DotEnv.' 'DotEnv/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-59/tool-dotenv/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.DotEnv.' 'DotEnv/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-59/tool-dotenv/before/reports/mutation-report.json artifacts/test-cleanup/phase-59/tool-dotenv/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-59/tool-dotenv/before/reports/mutation-report.json DotEnvParserTests.Parse_Text_HoldsTheValuesOfTheToolsKeys
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests-2-01: `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:225:79`, `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:228:17`, `src/SqlSource.Tool/DotEnv/DotEnvParser.cs:46:9`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-dotenv`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-59/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-59/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-59/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-59/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-59/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-59/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests-2-07 (keep, `DotEnvParserTests.Parse_ErrorAmongGoodLines_LeavesTheGoodLines`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests-2-08 (keep, `DotEnvParserTests.Parse_VariableOfADatabase_IsAKeyOfTheFile`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
