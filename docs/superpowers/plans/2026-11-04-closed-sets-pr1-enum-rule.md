# Closed Sets PR 1: The Enum Rule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every switch over an enum in `src/SqlSource` and `src/SqlSource.Tool` names every member and ends in a discard or `default:` that throws `UnexpectedEnumValueException`, and TD-0096 is resolved.

**Architecture:** Turn CS8524 and IDE0072 back on for `src/SqlSource`, so both projects run the analyzers' defaults.  IDE0072 (switch expressions) and IDE0010 (switch statements) then fail the build for any unnamed member, even when a discard is there.  One shared exception makes a value that is no member fail loudly.  The rule is written once in a new `src/AGENTS.md`.

**Tech Stack:** C# (netstandard2.0 generator on the Roslyn 4.14 floor, net8.0 tool), xUnit, Shouldly, `.editorconfig` analyzer severities.

**Spec:** `docs/superpowers/specs/2026-11-04-closed-sets-design.md`, sections 1 and 7, PR 1 of "How the work is split".  PRs 2 and 3 get plans of their own; PR 2's is written once the file-owned settings work has merged.

## Global Constraints

- No change to generated code, sidecars, diagnostics or golden files: the whole suite passes with no golden file updated.
- A switch expression over an enum names every member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`.
- A switch statement over an enum names every member, and its `default:` label stands alone and throws `UnexpectedEnumValueException.Of(value)`.  A `default:` stacked onto named cases is split: the named cases keep their behaviour.
- `[Flags]` enums tested by bit are not switched over.  A flags value known to hold one member follows the rule, with its combined members named in one throwing arm.  `ConverterTargets` is PR 3's.
- **An enum the code does not own, and whose members can grow at run time, is never switched over by member.**  Roslyn's enums are the case: the host compiler can be newer than the one the generator compiled against, and a throw there would break a user's build.  The design does not state this rule; this plan adds it.  The tool ships its own copies of Npgsql, SqlClient and System.Text.Json, so their enums follow the rule.
- Switches over strings, characters, numbers and types keep their discards.  Type switches over the sidecar's records are PR 3's.
- `src/AGENTS.md`, like every AGENTS.md, keeps each rule within 80 words and the file within 8 KB.
- Every commit passes `./pre-commit-validation.sh`, every push passes `./pre-commit-validation.sh --full`, and branch-review comes back clean before the PR.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A Roslyn enum switched over by member.**  A newer compiler adding a member must not make the generator throw.  Task 2's step 6 checks that no switch over a `Microsoft.CodeAnalysis` enum names its members, and that none gained a throw.
- **A value cast into an enum from outside data**, such as a number from JSON or a driver's code, reaching a switch whose discard used to give a fallback and now throws.  A user would see `SQLSRC200` where they got a quiet default before.  Task 3's step 2 lists every cast to an enum and `Enum.Parse` in the tool and checks that each is validated before any switch reads it.
- **A discard that caught reachable values.**  A tool discard over an enum that IDE0072 accepted only because every member is named is fine.  But a discard over a nullable enum, `SomeEnum?`, catches `null`, which is reachable.  Task 3's step 3 keeps a `null =>` arm wherever one is needed, before the throw.
- **A stacked `default:` that hid a named member's behaviour.**  Splitting `case Facet.Max: default: return true;` must leave `Facet.Max` returning `true`.  The existing `SqlServerParameterBinder` tests and the golden files pin this.  Task 2's step 5 runs them.

---

### Task 1: `UnexpectedEnumValueException`

**Files:**
- Create: `src/SqlSource/UnexpectedEnumValueException.cs`
- Test: `tests/SqlSource.Tests/UnexpectedEnumValueExceptionTests.cs`

**Interfaces:**
- Produces: `internal sealed class UnexpectedEnumValueException : InvalidOperationException` in namespace `SqlSource`, with `public static UnexpectedEnumValueException Of<T>(T value) where T : struct, Enum`.  The tool uses it through `SqlSource`'s existing `InternalsVisibleTo`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class UnexpectedEnumValueExceptionTests
{
    [Fact]
    public void Of_ValueOfNoMember_NamesTheValueAndTheEnum() =>
        UnexpectedEnumValueException
            .Of((DatabaseEngine)7)
            .Message.ShouldBe("The value 7 is no member of SqlSource.Settings.DatabaseEngine.");

    [Fact]
    public void Of_AnyValue_IsAnInvalidOperationException() =>
        UnexpectedEnumValueException.Of(DatabaseEngine.SqlServer).ShouldBeAssignableTo<InvalidOperationException>();
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~UnexpectedEnumValueExceptionTests"`
Expected: build error, `UnexpectedEnumValueException` not found.

- [ ] **Step 3: Implement the exception**

- The message is `$"The value {value} is no member of {typeof(T).FullName}."`.
- `{value}` is the enum's own `ToString`, which gives the number for a value that is no member.
- The constructor is private and takes the message.
- Write a doc comment: it is thrown by a switch's discard or `default:` over an enum, for a value that is none of its members, and it is not one of the exceptions written into a user's project.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~UnexpectedEnumValueExceptionTests"`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource/UnexpectedEnumValueException.cs tests/SqlSource.Tests/UnexpectedEnumValueExceptionTests.cs
git commit -m "feat: an exception for a value that is no member of its enum"
```

### Task 2: The rule in the generator

**Files:**
- Modify: `.editorconfig:253-260` (the `[src/SqlSource/**.cs]` section)
- Modify: every file under `src/SqlSource` that the build then reports.  Known switch statements: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:136`, and `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs:144` and `:209`.
- Test: the existing suite, `tests/SqlSource.Tests`, unchanged.

**Interfaces:**
- Consumes: `UnexpectedEnumValueException.Of<T>(T)` (Task 1).

- [ ] **Step 1: Turn the analyzers back on**

- In `.editorconfig`, delete `dotnet_diagnostic.CS8524.severity = none` and `dotnet_diagnostic.IDE0072.severity = none` from the `[src/SqlSource/**.cs]` section.
- Replace their comment with one saying: both projects use the defaults; IDE0072 and IDE0010 fail the build for an unnamed member even when there is a discard; every switch over an enum ends in a throwing discard or `default:` (`src/AGENTS.md`).
- If the section is then empty, delete its header too.

- [ ] **Step 2: Build to list the switches**

Run: `dotnet build src/SqlSource 2>&1 | grep -E "CS8524|IDE0072" | sort -u`
Expected: CS8524 together with IDE0072 at each switch expression over an enum.  That is about 60 sites, and none is over a Roslyn enum.

- [ ] **Step 3: Give each reported switch expression its throwing discard**

Add `_ => throw UnexpectedEnumValueException.Of(<the switch's governing expression>),` as the last arm.  When the governing expression has side effects or is long, capture it in a local first.

- [ ] **Step 4: Split the stacked `default:` labels**

- `SqlMarkerScope.cs:136`: the cases `Name`, `Summary` and `Dialect` keep their `InvalidOperationException` and its comment.  `default:` stands alone and throws `UnexpectedEnumValueException.Of(marker.Kind)`.
- `SqlServerParameterBinder.cs:144`: the cases `None`, `Scale7` and `Max` keep `return true;`.  `default:` stands alone and throws `UnexpectedEnumValueException.Of(kind)`.
- `SqlServerParameterBinder.cs:209`: the cases `None` and `Vector` keep `break;`.  `default:` stands alone and throws `UnexpectedEnumValueException.Of(kind)`.

- [ ] **Step 5: Build and run the generator's tests**

Run: `dotnet build src/SqlSource && dotnet test tests/SqlSource.Tests`
Expected: the build has no errors, every test passes, and `git status` shows no golden file changed.

- [ ] **Step 6: Check the Roslyn-enum rule**

Run: `grep -rnE --include='*.cs' '(TypeKind|SpecialType|SyntaxKind|Accessibility|SymbolKind|NullableAnnotation|RefKind|MethodKind|TypeParameterKind|OperationKind)\.[A-Z][A-Za-z]+ *(=>|:)' src/SqlSource`
Expected: no arm of a switch over one of those enums.  Any hit means a switch over a Roslyn enum by member.  Rewrite it as tests of the members it cares about, with an explicit fallback for the rest, and with a comment that a newer compiler can add members.

- [ ] **Step 7: Commit**

```bash
./pre-commit-validation.sh
git add .editorconfig src/SqlSource
git commit -m "refactor: every switch over an enum in the generator ends in a throw"
```

### Task 3: The rule in the tool

**Files:**
- Modify: every switch expression over an enum in `src/SqlSource.Tool` whose discard gives a value.
- Modify the switch statements:
  - `src/SqlSource.Tool/Projects/RunProjects.cs:88`;
  - `src/SqlSource.Tool/Describing/SidecarStore.cs:117`;
  - `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs:167`;
  - `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.Expressions.cs:435`.
- Test: the existing suites, `tests/SqlSource.Tool.Tests` and `tests/DatabaseTests/*`, unchanged.

**Interfaces:**
- Consumes: `UnexpectedEnumValueException.Of<T>(T)` (Task 1).

- [ ] **Step 1: Find the switch expressions over enums**

- Create a scratch branch from this one.
- On it, delete every `_ =>` arm in `src/SqlSource.Tool`, including its expression up to the closing `,`.
- Run `dotnet build src/SqlSource.Tool 2>&1 | grep CS8524 | sort -u`, and save the list to a scratch file outside the repository.
- CS8524 marks a switch over an enum.  CS8509 marks a switch over a string or a type, which keeps its discard.
- Delete the scratch branch, and work from the list on the real branch.

- [ ] **Step 2: Check casts into enums**

Run: `grep -rnE --include='*.cs' '\((ProjectState|RunUnitKind|QueryState|FileAction|OnlineMode|RunMode|DescribeStep|PlannedFileState|QueryProblems|WatchEffect|FileChangeKind|InstanceKind|NullExtension|TsqlTokenKind|SelectGrouping)\)|Enum\.(Parse|TryParse)' src/SqlSource.Tool`
Expected: each hit either converts a value the code made itself or checks `Enum.IsDefined` before any switch reads it.  If a hit converts outside data without a check, add one.  The check reports the bad value as the code around it already reports bad input, and is pinned by a test in the matching `tests/SqlSource.Tool.Tests` file.

- [ ] **Step 3: Replace each listed discard with the throw**

- Each listed switch's discard becomes `_ => throw UnexpectedEnumValueException.Of(<governing expression>),`.
- When the governing type is a nullable enum, add a `null =>` arm before the throw, holding what the old discard gave for `null`.

- [ ] **Step 4: Convert the four switch statements**

- `RunProjects.cs:88` over `ProjectState`: the named cases stay.  `default: break;` becomes `default: throw UnexpectedEnumValueException.Of(evaluation.State);`.
- `SidecarStore.cs:117` over `FileAction`: `default:` throws `UnexpectedEnumValueException.Of(outcome.Action)` in place of its `InvalidOperationException`.
- `CaptureJson.cs:167` over `JsonValueKind`: `default: break;` becomes the throw of `element.ValueKind`.
- `NullabilityWalk.Expressions.cs:435` over `InstanceKind`: `default:` throws `UnexpectedEnumValueException.Of(instance.Kind)` in place of its `InvalidOperationException`.

- [ ] **Step 5: Build and run the tool's tests**

Run: `dotnet build src/SqlSource.Tool && dotnet test tests/SqlSource.Tool.Tests`
Expected: the build has no errors and every test passes.

- [ ] **Step 6: Run the describers' tests on the default and floor images**

The Postgres describer changed, and the SQL Server describer did too if step 3 touched `Describing/SqlServer/`.

```bash
dotnet test tests/DatabaseTests/Postgres
```

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/DatabaseTests/Postgres
```

```bash
dotnet test tests/DatabaseTests/SqlServer
```

```bash
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test tests/DatabaseTests/SqlServer
```

Expected: every run passes.

- [ ] **Step 7: Commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource.Tool
git commit -m "refactor: every switch over an enum in the tool ends in a throw"
```

### Task 4: The documents, and TD-0096

**Files:**
- Create: `src/AGENTS.md`
- Modify: `src/SqlSource/AGENTS.md:16`
- Modify: `docs/architecture/generator.md:681`, `:683`, `:689`
- Delete: `docs/tech-debt/TD-0096-no-check-keeps-a-catch-all-out-of-an-enum-switch.md`
- Modify: `docs/tech-debt/README.md:43` (its row)
- Test: `tests/SqlSource.Tests/Documentation/` (the link and AGENTS.md tests), unchanged.

- [ ] **Step 1: Write `src/AGENTS.md`**

- A title, `# AGENTS.md - src`.
- One bullet: **A switch over an enum names every member and ends in a discard or `default:` that throws `UnexpectedEnumValueException.Of(value)`**.  IDE0072 and IDE0010 fail the build for an unnamed member even when there is a discard, so a new member of any enum, such as a `DatabaseEngine`, fails the build at each switch.
- Also say in that bullet: a `default:` is never stacked onto named cases; a `[Flags]` enum is tested by bit; an enum of the host compiler is never switched over by member.
- Link `DatabaseEngine` decisions to [adding an engine](../docs/architecture/generator.md#adding-a-database-engine).
- The bullet keeps within 80 words.  The file names no file or folder, so it needs no maintenance footer.

- [ ] **Step 2: Remove the old rule**

Delete the `_ =>` bullet at `src/SqlSource/AGENTS.md:16`.

- [ ] **Step 3: Update `docs/architecture/generator.md`**

- `:681`: describe today's rule.
  - Every decision on a `DatabaseEngine`, and every switch over any enum, names each member and ends in a throwing discard.
  - IDE0072 for switch expressions and IDE0010 for switch statements fail the build at a member a switch does not name.
  - A value that is no member throws `UnexpectedEnumValueException`.
  - Drop the sentence about CS8524 and IDE0072 being off, and the one saying nothing stops a discard.
- `:683`: change "is CS8509 until it names the role" to "is IDE0072 until it names the role".
- `:689`: change "each switch that CS8509 lists" to "each switch that IDE0072 and IDE0010 list".

- [ ] **Step 4: Resolve TD-0096**

- Delete its file and its row in `docs/tech-debt/README.md`.
- Run `grep -rn "TD-0096" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj`.
- Expected: hits only in `docs/superpowers/` (dated records, which keep them).  The `.editorconfig` and `src/SqlSource/AGENTS.md` mentions are already gone after Task 2 and step 2.

- [ ] **Step 5: Run the documentation tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation"`
Expected: every test passes.  Both AGENTS.md files keep within their limits, and every link resolves.

- [ ] **Step 6: Commit**

```bash
./pre-commit-validation.sh
git add src/AGENTS.md src/SqlSource/AGENTS.md docs/architecture/generator.md docs/tech-debt
git commit -m "docs: one rule for switches over an enum, resolving TD-0096"
```

### Task 5: Verify, review and open the PR

- [ ] **Step 1: Run the full validation**

Run: `./pre-commit-validation.sh --full`
Expected: every check passes and none is skipped.

- [ ] **Step 2: Run branch-review**

Run the `branch-review:review` skill.  Address its findings, minor ones included, and run it again until it comes back clean.

- [ ] **Step 3: Check the version, then open the PR**

- Run `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`.
- With no `v*` tag there is nothing to compare.  Otherwise, if `VersionPrefix` in `Directory.Build.props` is not greater than the tag, ask the owner for the new version.
- Bring the branch up to date with `main`.  Run `--full` and branch-review again if that changed anything.
- Push, open the PR, and turn on Auto-fix.
