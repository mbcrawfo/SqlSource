# TD-0114 Generator Mutation Gaps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the mutation gaps of the eleven files in TD-0114's table that guard real behaviour, and record why the rest need no test.

**Architecture:** No source changes.  Each surviving mutant is classified, a mutant that changes what a user or the next step sees gets a test, and a final Stryker run over the same mutants measures the result.

**Tech Stack:** C#, xUnit on the Microsoft Testing Platform, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** [`docs/tech-debt/TD-0114-generator-code-its-own-tests-do-not-check.md`](../../tech-debt/TD-0114-generator-code-its-own-tests-do-not-check.md)

## What was measured

On the merge of `main` at `52498126`, the eleven files were run with every test of `SqlSource.Tests.Generation`, which left 963 mutants alive (432 survived, 531 not covered).  Those 963 were run again with the driver tests of `SqlSource.Tests.Generator` added, at the ten-minute allowance of `tools/README.md`: 750 were killed, and 213 remain (173 survived, 33 not covered, 7 timed out).

| File | Gap under `Generation` | Alive with the driver tests | Task |
|----|----|----|----|
| `Generation/Methods/MethodPlanner.cs` | 287 | 63 | 1 |
| `Generation/Types/MappedTypes/MappedTypeResolver.cs` | 67 | 50 | 2 |
| `Generation/Models/ModelCollector.cs` | 84 | 16 | 3 |
| `Generation/Models/ModelReader.cs` | 90 | 14 | 3 |
| `Generation/Models/TypeModels.cs` | 54 | 11 | 3 |
| `Generation/Models/ModelSummary.cs` | 55 | 2 | 3 |
| `Generation/Types/QueryTyper.cs` | 82 | 23 | 4 |
| `Generation/Types/TypeDiagnostics.cs` | 57 | 9 | 4 |
| `Generation/Converters/ConverterReader.cs` | 40 | 9 | 4 |
| `Generation/Types/Drivers/LibraryProbe.cs` | 54 | 8 | 4 |
| `Generation/Exceptions/ExceptionProbe.cs` | 93 | 8 | 4 |

## Global Constraints

- No change under `src/`.  A mutant that shows a bug is reported to the plan's owner, with the input that shows it, and is not fixed or recorded by the implementer.
- Each mutant gets exactly one class:
  - **test**: it changes generated code, a diagnostic, or a value the next step reads, for an input a user can write.  It gets a test.
  - **equivalent**: no input can tell it from the original.
  - **unreachable**: it sits in a guard that no input reaches, which the types or an earlier step already rule out.
  - **cost**: it changes only how much work is done, such as a cancellation check, a cache or an early exit, and never the result.
- A class other than **test** names the reason in one line.  A mutant is never called equivalent because no test happens to fail on it.
- No test exists only to raise a score.  Prefer a new row of an existing theory, then a new test in the class that owns the behaviour.
- A test of a type's internal API goes in the mirror folder, `tests/SqlSource.Tests/Generation/<Folder>/<Type>Tests.cs`.  A behaviour reached only through the whole generator goes in the class of its feature in `tests/SqlSource.Tests/Generator/`, which must compile on Roslyn 4.14.0 and C# 12.
- Tests follow `tests/AGENTS.md`: Shouldly, `Method_Scenario_Outcome`, `sealed` classes, and an explicit `OutputKind` for every run of the generator.
- Each kill is proved by making the mutation by hand, seeing the new test fail, and restoring the file with `git checkout -- <file>`.  Nothing under `src/` is committed.
- Run `./format.sh`, then `./pre-commit-validation.sh`, before each commit.

## Review Focus

- A type with queries on several engines and a library converter gets each target's converters, never those of a target cached first.  Task 1.
- A mapped type whose argument is written with a C# keyword, an array or a nullable element resolves to that exact type.  Task 2.
- A diagnostic names a type in the global namespace, and one nested in a generic type, with no stray dot.  Task 1.
- A model name or namespace with a dot at its start, end or in several places is split at the right dot.  Task 3.
- A compilation that references two versions of NodaTime is read as the newest.  Task 4.

---

### Task 1: `MethodPlanner`

**Files:**
- Test: `tests/SqlSource.Tests/Generation/Methods/` and the method classes of `tests/SqlSource.Tests/Generator/`

**Interfaces:**
- Consumes: `MethodRuns`, `GeneratorHarness`
- Produces: nothing later tasks use

- [ ] **Step 1: Classify the task's 63 mutants** by the Global Constraints, reading each in `MethodPlanner.cs`.
- [ ] **Step 2: Write the tests of the mutants classed test**, including `ServedConverters` across targets and `InterfaceDisplayName` in the global namespace and nested in a generic type.
- [ ] **Step 3: Prove each kill** by hand.  Expected: the new test fails under the mutation and passes on the restored file.
- [ ] **Step 4: Validate and commit** with `./format.sh`, `./pre-commit-validation.sh` and `git commit -m "test: pin what MethodPlanner's driver tests left unchecked"`.

### Task 2: `MappedTypeResolver`

**Files:**
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/`

**Interfaces:**
- Consumes: the existing helpers of that folder's tests
- Produces: nothing later tasks use

- [ ] **Step 1: Classify the task's 50 mutants.**
- [ ] **Step 2: Write the tests of the mutants classed test**, including a theory over every keyword of `KeywordType`, and arrays and nullable elements in `AsWritten`.
- [ ] **Step 3: Prove each kill** by hand.
- [ ] **Step 4: Validate and commit** with the message `test: pin what MappedTypeResolver's driver tests left unchecked`.

### Task 3: The models

**Files:**
- Test: `tests/SqlSource.Tests/Generation/Models/` and the model classes of `tests/SqlSource.Tests/Generator/`

**Interfaces:**
- Consumes: `ModelRuns`, `TestModels`
- Produces: nothing later tasks use

- [ ] **Step 1: Classify the 43 mutants of `ModelCollector`, `ModelReader`, `TypeModels` and `ModelSummary`.**  Read the five timeouts of `ModelCollector` as what they are: a mutant that hangs on a real input is a kill, and one that hangs only by the allowance is not.
- [ ] **Step 2: Write the tests of the mutants classed test**, including names and namespaces with a dot at the start, the end and in several places.
- [ ] **Step 3: Prove each kill** by hand.
- [ ] **Step 4: Validate and commit** with the message `test: pin what the model steps' driver tests left unchecked`.

### Task 4: Types, converters and exceptions

**Files:**
- Test: `tests/SqlSource.Tests/Generation/Types/`, `Generation/Converters/`, `Generation/Exceptions/` and the matching classes of `tests/SqlSource.Tests/Generator/`

**Interfaces:**
- Consumes: `LibraryReferences`, `ConverterCases`, `GeneratorHarness`
- Produces: nothing later tasks use

- [ ] **Step 1: Classify the 57 mutants of `QueryTyper`, `TypeDiagnostics`, `ConverterReader`, `LibraryProbe` and `ExceptionProbe`.**
- [ ] **Step 2: Write the tests of the mutants classed test**, including two referenced versions of NodaTime.
- [ ] **Step 3: Prove each kill** by hand.
- [ ] **Step 4: Validate and commit** with the message `test: pin what the type and converter steps' driver tests left unchecked`.

### Task 5: Measure and rewrite the item

**Files:**
- Modify: `docs/tech-debt/TD-0114-generator-code-its-own-tests-do-not-check.md`, `docs/tech-debt/README.md`

- [ ] **Step 1: Run the 213 mutants again** with the `Generation` and driver tests.  Expected: every mutant classed test is `Killed`.
- [ ] **Step 2: Rewrite the item**: take the eleven files out of its table, give the next files of the review's appendix, and say by class what remains in the eleven and why it needs no test.  Update its row.
- [ ] **Step 3: Validate and commit** with the message `docs: narrow TD-0114 to the files not yet measured with the driver tests`.
