# Test cleanup phase 29: tool bundles folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the five theories of `tests/SqlSource.Tool.Tests/Bundles` that run every kind of control character through `BundleText.Of` into facts on one kind, ESC, leaving the kinds to `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape`, with the guard showing nothing lost.

**Architecture:** Five theories become facts, no source change.  The four theories of `BundleErrorsTests` each prove that one `BundleErrors` method passes its argument through `BundleText.Of` and keeps the tab; ESC alone tells `BundleText.Of` from no escaping and from `OneLine.Of`.  `BundleTextTests.Of_ControlCharacterBesideATab_KeepsTheTab` proves the tab is kept, which `IsEscaped` decides for each character alone; ESC between two tabs still runs the prefix copy and the loop.  `BundleErrorsTests.Controls` and its comment go, since no test uses it after; `BundleTextTests.Controls` stays for the cover.  The guard is the `tool-bundles` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/Bundles/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-29-tool-bundles-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the five the spec names; their names stay, since each still says what it proves.
- Each fact keeps the theory's call, text and assertions, with `"\u001b"` for the control and `"\\u001B"` for its escape.
- `BundleTextTests.Of_ControlCharacter_IsWrittenAsItsEscape` and its `Controls` stay as they are: they are the cover of both findings.
- The epic's phase table: change only row 29.
- No theory touched here branches on its row, so TD-0089 does not change.

## Review Focus

None: each theory keeps its first row, ESC, as a fact.  Checked against the spec on `b34a615a`: the four `BundleErrors` methods still pass their argument through `BundleText.Of`, `IsEscaped` is still `character != '\t' && char.IsControl(character)`, the cover still holds every dropped row, and no document outside `docs/superpowers/` names either class.

---

### Task 0: The guard's "before" half

- [x] The `tool-bundles` mutation run into `artifacts/test-cleanup/phase-29/tool-bundles/before` (no earlier phase of this scope).
- [x] `kills` on the before report for the five tests.
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-29/coverage-before/SqlSource.Tool.Tests`, with the line figures of each file of `src/SqlSource.Tool/Bundles/*.cs`.

### Task 1: Fold the tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/BundleErrorsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Bundles/BundleTextTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 29's state)

- [x] **Step 1:** In `BundleErrorsTests`, remove `Controls` and its comment, and make the four theories facts:

```csharp
    [Fact]
    public void CannotBeRead_ReasonWithAControlCharacter_EscapesItAndKeepsTheTab()
    {
        var error = BundleErrors.CannotBeRead(BundleInfos.Path, "the kind is 'x\u001b[2J\ty'");

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(BundleInfos.Path);
        error.Arguments.ToArray().ShouldBe(["the kind is 'x\\u001B[2J\ty'"]);
    }

    [Fact]
    public void CallMissing_MethodWithAControlCharacter_EscapesItAndKeepsTheTab() =>
        BundleErrors
            .CallMissing(BundleInfos.Path, "col\u001b\tumns", BundleErrors.MadeBy("1.4.0"))
            .Arguments.ToArray()
            .ShouldBe(["col\\u001B\tumns", "sqlsource 1.4.0", Cli.Version]);

    [Fact]
    public void CallMissing_ToolVersionWithAControlCharacter_EscapesItAndKeepsTheTab() =>
        BundleErrors
            .CallMissing(BundleInfos.Path, "columns", BundleErrors.MadeBy("1.4\u001b[3A\t0"))
            .Arguments.ToArray()
            .ShouldBe(["columns", "sqlsource 1.4\\u001B[3A\t0", Cli.Version]);

    [Fact]
    public void NoDescriber_EngineWithAControlCharacter_EscapesItAndKeepsTheTab()
    {
        var error = BundleErrors.NoDescriber(BundleInfos.Path, "x\u001b]0;pwned\tsql");

        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Arguments.ToArray().ShouldBe(["x\\u001B]0;pwned\tsql"]);
    }
```

- [x] **Step 2:** In `BundleTextTests`, make the tab theory a fact:

```csharp
    [Fact]
    public void Of_ControlCharacterBesideATab_KeepsTheTab() =>
        BundleText.Of("a\t\u001b\tb").ShouldBe("a\t\\u001B\tb");
```

- [x] **Step 3:** Set row 29 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the `Bundles` tests of `tests/SqlSource.Tool.Tests`: every test passes.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded tests did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-29/coverage-after/SqlSource.Tool.Tests`: no file of `src/SqlSource.Tool/Bundles/*.cs` falls.
- [x] **Step 3:** The pull request lists the dropped rows beside their cover, the compare result, the coverage figures and the run times.
