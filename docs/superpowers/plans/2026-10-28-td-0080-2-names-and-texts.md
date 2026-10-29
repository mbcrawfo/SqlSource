# TD-0080 items 5 and 16: generated file names and shared SQL texts, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The names of the two generated files that most generator tests read, and the two SQL texts that the tool's tests share, are each written once.

**Architecture:** Constants beside the harness that makes the file or text: `GeneratorHarness.SampleFile` beside `SampleSource`, `ModelRuns.GetUserDtoFile` beside `ModelRuns.Source`, and `SqlTexts.ListInvoices` and `SqlTexts.Plain` beside the other texts.  The tests replace the literals; `TypeOverrideLevelTests` uses `GeneratorHarness.UsersFile` where it wrote the same file out.

**Tech Stack:** .NET 10 SDK, xunit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 5 and 16 of "Problem" and of "Proposed fix".

## Global Constraints

- No test checks anything different afterwards.
- The constants are in `tests/SqlSource.Testing`, which RoslynFloor references, and stay `internal`.
- A test of the names themselves keeps them written out, so that the product's name is pinned by a literal: `Generator/GeneratedFileNameTests.cs` and `Generation/Emitting/HintNameTests.cs`.
- Every commit passes `./pre-commit-validation.sh`; the push passes `--full`.
- Only items 5 and 16 of TD-0080 are struck.

## Review Focus

- A literal inside a larger string (an interpolated path, a message) is not the file name alone and stays.
- A test in a class with `using static` of the harness still names the constant by its type, so a reader sees where it is.

---

### Task 1: Items 5 and 16

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, `tests/SqlSource.Testing/Generator/ModelRuns.cs`, `tests/SqlSource.Testing/SqlTexts.cs`, the tests under `tests/SqlSource.Tests/` that write `"App.Sample.g.cs"` or `"App.GetUserDto-Model.g.cs"`, the tests under `tests/SqlSource.Tool.Tests/Commands/` and `Logging/` that write the two texts, `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`, `tests/README.md`, TD-0080.

**Interfaces:**
- `GeneratorHarness.SampleFile = "App.Sample.g.cs"`, `ModelRuns.GetUserDtoFile = "App.GetUserDto-Model.g.cs"`, `SqlTexts.ListInvoices = "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n"`, `SqlTexts.Plain = "-- name: Plain\n-- output: sql\nSELECT 4;\n"`.

- [ ] **Step 1:** Add the constants and replace each exact literal outside the two name tests.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests`, `--project tests/SqlSource.Tests.RoslynFloor` and `--project tests/SqlSource.Tool.Tests`; expected: all pass with the same counts.
- [ ] **Step 3:** Name `SampleFile` in the `SqlSource.Testing` row of `tests/README.md`; strike items 5 and 16 of TD-0080.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.
