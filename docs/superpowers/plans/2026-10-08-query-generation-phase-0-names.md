# Query generation, phase 0: names - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the attribute, its enum and property, and the two markers of a `.sql` file, before anything is published under the old names.

**Architecture:** Three mechanical renames, each one commit, then one structural change: the dialect leaves the generator-parameter line and becomes a marker of its own, read by one class, `SqlDialectMarker`, and checked for the file by `SqlFileParser`.  No diagnostic id changes.

**Tech Stack:** C# source generator on `netstandard2.0` (Roslyn 4.8.0 floor), xunit v3 on Microsoft.Testing.Platform, Shouldly, CSharpier, MSBuild props and targets.

**Spec:** [`docs/superpowers/specs/2026-10-08-query-generation-phase-0-names-design.md`](../specs/2026-10-08-query-generation-phase-0-names-design.md).  Read it first; this plan argues from it.

## Global Constraints

- Branch: `claude/query-generation-phase-0-names`.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`.  Run it as its own command, fix what it reports, then commit in a separate command.  A hook runs it again before any Bash command that contains the words of the commit command; never work around it.
- Format with `./format.sh` before validating.  A longer name can re-wrap a line.
- No diagnostic id is added, removed or renumbered.  `SqlParseErrorKind` keeps its member order.
- A change to a diagnostic's title or message touches the descriptor in `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, the row in `src/SqlSource/AnalyzerReleases.Unshipped.md`, and the index row and section in `docs/diagnostics.md`.  A test compares the index rows with the titles.
- `src/SqlSource` targets `netstandard2.0` only and may use no Roslyn API newer than 4.8.0.  The attribute file, `AttributeSource.Text`, must compile as C# 7.3.
- The allocation budgets in `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` and `tests/SqlSource.Tests/Generation/PathResolverAllocationTests.cs` are never raised.
- Files under `docs/superpowers/` are not rewritten, except the epic outline where a task says so.  Every `git grep` and `perl` command below excludes that folder.
- `README.md` links are absolute URLs.
- Prose uses two spaces after a full stop, as the repository does.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class, when a task says so (if the filter option is rejected, run the whole solution instead):

```bash
dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Parsing.SqlFileParserTests
```

## Review Focus

Inputs the spec implies and does not list a test for.  Each has a test in the task named.

1. An ordinary comment that happens to read `-- generator: something` is now a marker and an error (`SQLSRC109`); a reader expects a clear error at the word, not silence.  Task 3.
2. A comment `-- dialect: see the wiki` inside a query is now a misplaced dialect marker (`SQLSRC115`), whatever its text.  Task 4.
3. `-- dialect: mysql keep-comments`, the old habit of several words on one line, must be `SQLSRC111` at the value, not silently `mysql`.  Task 4.
4. A file with Windows line endings and trailing blanks after the dialect's value must read the dialect.  Task 4.
5. `# dialect: mysql` in a MySQL file is a comment, not a marker: a marker starts with two dashes.  Task 4.

---

### Task 1: `SqlQueriesAttribute` becomes `SqlSourceGenerateAttribute`

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs`
- Modify: every file `git grep -l 'SqlQueries' -- . ':!docs/superpowers'` lists (sources, tests, `README.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`, `tools/package-install/Queries.cs`)

**Interfaces:**
- Produces: `AttributeSource.HintName == "SqlSourceGenerateAttribute.g.cs"`, `AttributeSource.AttributeMetadataName == "SqlSource.SqlSourceGenerateAttribute"`.  The emitted class is `SqlSource.SqlSourceGenerateAttribute`.  `SqlQueriesMode` and `Mode` are untouched until Task 2.

- [ ] **Step 1: Rename every occurrence that is not the enum**

`SqlQueries` not followed by `Mode` is the attribute in every form: `[SqlQueries]`, `SqlQueriesAttribute`, `SqlQueriesAttribute.g.cs`, and prose.

```bash
git grep -l 'SqlQueries' -- . ':!docs/superpowers' | xargs perl -pi -e 's/SqlQueries(?!Mode)/SqlSourceGenerate/g'
```

- [ ] **Step 2: Check what is left**

```bash
git grep -n 'SqlQueries' -- . ':!docs/superpowers' | grep -v 'SqlQueriesMode'
```

Expected: no output.

- [ ] **Step 3: Read the diff of the prose**

```bash
git diff -- README.md docs src/SqlSource/Generation/AttributeSource.cs src/SqlSource/Diagnostics
```

Check that each sentence still reads correctly.  In `AttributeSource.cs` the three places are the `HintName` constant, `AttributeMetadataName`, and inside `Text` the class declaration and the `<see cref="SqlSourceGenerateAttribute" />` on the enum.

- [ ] **Step 4: Format and build**

```bash
./format.sh
```

```bash
dotnet build SqlSource.slnx
```

Expected: the build succeeds.

- [ ] **Step 5: Run the tests and fix the pinned columns**

```bash
dotnet test --solution SqlSource.slnx
```

Expected: failures only in tests that pin the span of the attribute by column, in `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`.  The name is 7 characters longer, so an end column of a span that covers the attribute grows by 7.  Known cases, to confirm against the failure output and not to apply blind:

| Was | Is |
|----|----|
| `(3,2)-(3,12)`, `(2,2)-(2,12)` | `(3,2)-(3,19)`, `(2,2)-(2,19)` |
| `(4,6)-(4,16)` | `(4,6)-(4,23)` |
| `(5,2)-(5,42)`, `(6,6)-(6,46)` | `(5,2)-(5,49)`, `(6,6)-(6,53)` |
| `(3,2)-(3,56)` | `(3,2)-(3,63)` |
| `(3,2)-(3,{21 + mode.Length})` | `(3,2)-(3,{28 + mode.Length})` |

A span on the type's name, such as `(4,14)-(4,20)`, does not move.  For each failing assertion, confirm from the test's source text that the new span is the attribute's, then update the expected string.  Any failure of another kind is a mistake in step 1: stop and find it.

- [ ] **Step 6: Run the tests again**

```bash
dotnet test --solution SqlSource.slnx
```

Expected: every test passes, in both test projects.

- [ ] **Step 7: Validate**

```bash
./pre-commit-validation.sh
```

Expected: every step in the summary says `passed`.  `package-install` proves the attribute reaches a consumer under its new name.

- [ ] **Step 8: Commit**

```bash
git add -A
```

```bash
git commit -m "Rename the attribute to SqlSourceGenerate

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `SqlQueriesMode` and `Mode` become `SqlLocation`

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs`, `src/SqlSource/Generation/TargetTypeReader.cs`, `src/SqlSource/Generation/MemberPlacement.cs`, `src/SqlSource/Diagnostics/AttributeConflictSuppressor.cs`, `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `README.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`, `tools/package-install/Queries.cs`
- Modify: the tests `git grep -l 'SqlQueriesMode' -- tests` lists

**Interfaces:**
- Consumes: Task 1's names.
- Produces: `AttributeSource.LocationMetadataName == "SqlSource.SqlLocation"`, `AttributeSource.LocationProperty == "SqlLocation"`, `SqlDiagnostics.InvalidSqlLocation` (id `SQLSRC006`).  The emitted enum is `SqlSource.SqlLocation` with `Nested = 0` and `Direct = 1`; the attribute's property is `public SqlLocation SqlLocation { get; set; }`.

- [ ] **Step 1: Rename the enum and the property where they are written**

```bash
git grep -l 'SqlQueriesMode' -- . ':!docs/superpowers' | xargs perl -pi -e 's/SqlQueriesMode/SqlLocation/g; s/\bMode = (\(?SqlLocation)/SqlLocation = $1/g'
```

- [ ] **Step 2: Rename what the pattern cannot see**

In `src/SqlSource/Generation/AttributeSource.cs`:

```csharp
    public const string LocationMetadataName = "SqlSource.SqlLocation";

    public const string PathProperty = "Path";

    public const string LocationProperty = "SqlLocation";
```

and in `Text`, the property and its documentation.  The `cref` names the enum from the global namespace: inside the class, `SqlLocation.Nested` could bind to the property.

```csharp
                /// <summary>
                /// Where the generated members go.  The default is
                /// <see cref="global::SqlSource.SqlLocation.Nested" />.
                /// </summary>
                public SqlLocation SqlLocation { get; set; }
```

In `src/SqlSource/Generation/TargetTypeReader.cs` use `AttributeSource.LocationProperty` and `SqlDiagnostics.InvalidSqlLocation`.  In `src/SqlSource/Diagnostics/AttributeConflictSuppressor.cs` use `AttributeSource.LocationMetadataName`, and rename the local `mode` to `location`.

In `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`, the theory that builds the attribute from a parameter:

```csharp
    [Theory]
    [InlineData("(SqlLocation)2", "2")]
    [InlineData("(SqlLocation)(-1)", "-1")]
    public void Run_SqlLocationThatIsNotDefined_IsAnErrorAtTheAttribute(string location, string value)
    {
        var run = GeneratorHarness.Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(SqlLocation = {{location}})]
            public partial class Sample { }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC006 /app/Repo/Sample.cs(3,2)-(3,{35 + location.Length}): '{value}' is not a value of SqlLocation",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }
```

- [ ] **Step 3: Reword `SQLSRC006`**

In `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, the descriptor field becomes `InvalidSqlLocation`, in the declaration and in `All`:

```csharp
    public static readonly DiagnosticDescriptor InvalidSqlLocation = new(
        id: "SQLSRC006",
        title: "SqlLocation is not valid",
        messageFormat: "'{0}' is not a value of SqlLocation",
```

`SQLSRC007`'s message now ends `Rename it, or use SqlLocation.Direct.` from step 1; confirm it.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`:

```
SQLSRC006 | SqlSource | Error | SqlLocation is not valid
```

In `docs/diagnostics.md`, the index row and the section:

```markdown
| [SQLSRC006](#sqlsrc006) | SqlLocation is not valid |
```

```markdown
**SqlLocation is not valid**

`SqlLocation` was given a value that the `SqlLocation` enum does not define.
```

- [ ] **Step 4: Say location where the documents say mode**

```bash
git grep -n -i -w -e mode -e modes -- README.md docs/diagnostics.md docs/tech-debt src/SqlSource tests tools
```

For each hit that means where the members go, write location: the README's `### Modes` heading becomes `### Locations` and its table header `Mode` becomes `SqlLocation`; ``In `Nested` mode`` becomes ``With `SqlLocation.Nested` ``; a test named `Run_NestedModeAnd...` becomes `Run_NestedLocationAnd...`, and `Run_DirectModeAnd...` becomes `Run_DirectLocationAnd...`.  Leave every hit that means something else: MySQL's SQL modes in the README and in `SqlDialectName.cs`, `DocumentationMode` in `GeneratorHarness.cs`, the tool's `--check` mode in scripts.  `MemberPlacement.cs` keeps its name; its comment now reads "The generator's own form of the `SqlLocation` it emits."

- [ ] **Step 5: Check what is left**

```bash
git grep -n 'SqlQueriesMode\|ModeProperty\|ModeMetadataName\|InvalidMode\|Mode = ' -- . ':!docs/superpowers'
```

Expected: no output.

- [ ] **Step 6: Format, build and test**

```bash
./format.sh
```

```bash
dotnet test --solution SqlSource.slnx
```

Expected failures: pinned columns again.  `Mode = SqlQueriesMode.X` became `SqlLocation = SqlLocation.X`, 4 characters longer, so the known case `(3,2)-(3,63)` becomes `(3,2)-(3,67)`; confirm against the output as in Task 1.  The tests that assert `run.GeneratedCodeWarnings.ShouldBeEmpty()` and that `AttributeSource.Text` compiles on an older language version prove the `cref` and the same-named property; if one reports CS1574 or CS0120 in `SqlSourceGenerateAttribute.g.cs`, the `cref` in step 2 is not as written there.

- [ ] **Step 7: Validate**

```bash
./pre-commit-validation.sh
```

Expected: every step `passed`.

- [ ] **Step 8: Commit**

```bash
git add -A
```

```bash
git commit -m "Rename SqlQueriesMode and Mode to SqlLocation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `-- SqlSource:` becomes `-- generator:`, and a directive a generator parameter

For this commit only, the dialect is still the generator parameter `dialect=name`.

**Files:**
- Rename: `src/SqlSource/Parsing/SqlDirectiveScope.cs` to `SqlGeneratorParameterScope.cs`
- Rename: `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs` to `SqlGeneratorParameterScopeTests.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerReader.cs`, `SqlMarkerKind.cs`, `SqlParseErrorKind.cs`, `SqlFileParser.cs`, `SqlPreambleDialect.cs`, and every other file under `src/SqlSource` that says directive
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/AGENTS.md`
- Modify: `README.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`
- Modify: every test and `.sql` file that holds `-- SqlSource:` or says directive; `tools/package-install/Queries/Users.sql`, `tools/package-install/Program.cs`, `tests/SqlSource.Tests/SqlSource.Tests.csproj`

**Interfaces:**
- Produces: `SqlMarkerKind.GeneratorParameters`; `SqlGeneratorParameterScope(int headerEnd)` with the members `SqlDirectiveScope` had; `SqlParseErrorKind.UnknownGeneratorParameter`, `EmptyGeneratorLine`, `InvalidMarkerValue`, `ConflictingSettings`, at the positions of the members they replace; descriptors of the same names in `SqlDiagnostics`.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, replace the two `SqlSource` rows of `Read_MarkerComment_ReturnsItsKindAndValue`:

```csharp
    [InlineData("-- generator: keep-comments  token-ignore=a", "GeneratorParameters:keep-comments  token-ignore=a")]
    [InlineData("-- GENERATOR: x", "GeneratorParameters:x")]
```

add to `Read_AnythingElse_IsNotAMarker`:

```csharp
    [InlineData("-- SqlSource: keep-comments")]
    [InlineData("-- generators: keep-comments")]
```

and in `Read_HashComment_IsNotAMarker` replace the `SqlSource` row with:

```csharp
    [InlineData("#  generator: keep-comments")]
```

In `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs`, add:

```csharp
    // The old marker is an ordinary comment: removed by default, kept when comments are.
    [Fact]
    public void Build_LineOfTheOldMarker_IsAComment()
    {
        const string Text = "-- SqlSource: keep-comments\nSELECT 1";

        Build(Text).ShouldBe("SELECT 1");
        Build(Text, keepComments: true).ShouldBe(Text);
    }
```

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, add (it compiles once step 3 has renamed the kind):

```csharp
    // A comment that has the form of a marker is one, whatever its author meant by it.
    [Fact]
    public void Parse_OrdinaryCommentThatStartsWithGenerator_IsReadAsGeneratorParameters()
    {
        const string Text = "-- generator: pgloader\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(Text, "pgloader"), "pgloader"),
            ]);
    }
```

- [ ] **Step 2: Rename the files**

```bash
git mv src/SqlSource/Parsing/SqlDirectiveScope.cs src/SqlSource/Parsing/SqlGeneratorParameterScope.cs
```

```bash
git mv tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs tests/SqlSource.Tests/Parsing/SqlGeneratorParameterScopeTests.cs
```

- [ ] **Step 3: Rename the marker and the identifiers**

The marker text, in every case it is written in:

```bash
git grep -l -i 'sqlsource:' -- . ':!docs/superpowers' | xargs perl -pi -e 's/SqlSource:/generator:/g; s/SQLSOURCE:/GENERATOR:/g; s/sqlsource:/generator:/g'
```

Then restore the three rows step 1 wrote on purpose: `"-- SqlSource: keep-comments"` in `Read_AnythingElse_IsNotAMarker`, and the text of `Build_LineOfTheOldMarker_IsAComment`.

The identifiers, most specific first:

```bash
git grep -l 'Directive' -- src tests ':!docs/superpowers' | xargs perl -pi -e 's/SqlDirectiveScope/SqlGeneratorParameterScope/g; s/SqlMarkerKind\.Directives/SqlMarkerKind.GeneratorParameters/g; s/UnknownDirective/UnknownGeneratorParameter/g; s/EmptyDirectiveLine/EmptyGeneratorLine/g; s/InvalidDirectiveValue/InvalidMarkerValue/g; s/ConflictingDirectives/ConflictingSettings/g'
```

In `src/SqlSource/Parsing/SqlMarkerKind.cs`:

```csharp
    /// <summary><c>-- generator:</c> carries generator parameters.</summary>
    GeneratorParameters,
```

In `src/SqlSource/Parsing/SqlMarkerReader.cs` the keyword row reads:

```csharp
        ("generator:", SqlMarkerKind.GeneratorParameters),
```

- [ ] **Step 4: Rename the word in the rest of the code**

In test method names, parameters and locals, `Directive` becomes `GeneratorParameter`; in comments it becomes "generator parameter".  Comment lines first, then identifiers:

```bash
git grep -l -i 'directive' -- src tests tools ':!docs/superpowers' | xargs perl -pi -e 'if (m{^\s*(//|<!--|--)}) { s/\bDirectives\b/Generator parameters/g; s/\bDirective\b/Generator parameter/g; s/\bdirectives\b/generator parameters/g; s/\bdirective\b/generator parameter/g } else { s/Directives/GeneratorParameters/g; s/Directive/GeneratorParameter/g; s/\bdirectives\b/parameters/g; s/\bdirective\b/parameter/g }'
```

This is a first pass, not the result.  Read the whole diff and correct by hand:

```bash
git diff -- src tests tools
```

- A string literal that is prose, such as a diagnostic message, was changed by the identifier rule: step 5 gives the text of each.
- A local named `parameter` or `parameters` that now collides with another name in its method gets a clearer one, such as `written`.
- `/// <summary>` text inside a line that also holds code keeps the prose form: "generator parameter", two words.
- A line inside a block comment or a multi-line XML comment does not start with a comment mark, so it got the identifier form: "a parameter" there becomes "a generator parameter".  `tests/SqlSource.Tests/SqlSource.Tests.csproj` and `tests/SqlSource.Tests/EndToEnd/Dialects/ByDirective.sql` each have one.
- The mentions of C#'s own directives are not this word and go back as they were: "no `#nullable` directive" in `src/SqlSource/AGENTS.md` and in the comment of `AttributeSource.cs`.

- [ ] **Step 5: Reword the diagnostics**

In `src/SqlSource/Diagnostics/SqlDiagnostics.cs`:

```csharp
    // SQLSRC108
        messageFormat: "A '-- summary:' or '-- generator:' marker comes before the SQL it describes, and no SQL "
            + "follows this one in its query",

    public static readonly DiagnosticDescriptor UnknownGeneratorParameter = new(
        id: "SQLSRC109",
        title: "Generator parameter is not known",
        messageFormat: "'{0}' is not a generator parameter",

    public static readonly DiagnosticDescriptor EmptyGeneratorLine = new(
        id: "SQLSRC110",
        title: "Generator parameter is missing",
        messageFormat: "The '-- generator:' marker has no parameter",

    public static readonly DiagnosticDescriptor InvalidMarkerValue = new(
        id: "SQLSRC111",
        title: "Marker value is not valid",
        messageFormat: "'{0}' lacks a value it needs, has one it does not take, or has one that is not valid",

    public static readonly DiagnosticDescriptor ConflictingSettings = new(
        id: "SQLSRC112",
        title: "Settings conflict",
        messageFormat: "'{0}' conflicts with a setting given earlier in the same scope",

    public static readonly DiagnosticDescriptor MisplacedDialect = new(
        id: "SQLSRC115",
        title: "Dialect parameter is misplaced",
        messageFormat: "The 'dialect' generator parameter must come before the file's first query and before any SQL",
```

Only the titles and messages change; the other arguments of each descriptor stay.  The `SQLSRC115` text is for this commit; Task 4 gives its last form.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`:

```
SQLSRC109 | SqlSource | Error | Generator parameter is not known
SQLSRC110 | SqlSource | Error | Generator parameter is missing
SQLSRC111 | SqlSource | Error | Marker value is not valid
SQLSRC112 | SqlSource | Error | Settings conflict
SQLSRC115 | SqlSource | Error | Dialect parameter is misplaced
```

In `src/SqlSource/Parsing/SqlParseErrorKind.cs`, the comments of the renamed members say "generator parameter" and `-- generator:`; the order of the members is unchanged.

Update the expected messages in `tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs`:

```csharp
            "SQLSRC109 /app/Repo/Users.sql(1,29)-(1,34): 'shout' is not a generator parameter",
```

```csharp
            "SQLSRC115 /app/Repo/Users.sql(2,15)-(2,28): The 'dialect' generator parameter must come before the "
                + "file's first query and before any SQL",
```

```csharp
            "SQLSRC111 /app/Repo/Users.sql(1,15)-(1,28): 'dialect=pgsql' lacks a value it needs, has one it does "
                + "not take, or has one that is not valid",
            "SQLSRC112 /app/Repo/Users.sql(2,29)-(2,43): 'dialect=oracle' conflicts with a setting given earlier in "
                + "the same scope",
```

- [ ] **Step 6: Rewrite the documents**

`README.md`:

- Under Queries: "Before the first `-- name:` line a file may hold comments, such as a licence header, and `-- generator:` lines that apply to every query in the file."
- Add, as the first paragraph of the section that introduces `.sql` files, the rule: "A line comment that starts its line and has the form `-- word: rest`, where the word is one SqlSource knows, is a marker.  Nothing else in a comment is read.  The markers are `-- name:`, `-- summary:` and `-- generator:`, written in any case, and each has its own form for the rest of the line."
- The `### Directives` section becomes `### Generator parameters`: "A `-- generator:` line holds one or more generator parameters, separated by spaces.  Inside a query it applies to that query.  Before the first `-- name:` line it applies to every query in the file."  The table's first header is `Parameter`.
- Everywhere else, "directive" becomes "generator parameter", and "in the directive" for a dialect becomes "in the `dialect` parameter".

`docs/diagnostics.md`: the index rows for 109, 110, 111, 112 and 115 take the titles above; each of those sections takes its title, says "generator parameter" for "directive", and shows `-- generator:` in its examples.  The sections of 106, 108 and 114 name `-- generator:`.

`src/SqlSource/AGENTS.md`: `SqlGeneratorParameterScope` for `SqlDirectiveScope`; "generator parameter" for "directive" where it means one; "marker keywords and the names of generator parameters are ordinal ignoring case".

`docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`: "The `keep-comments` generator parameter", twice.

`tools/package-install/Program.cs`: "A generator parameter turns it back on for this query."

`tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`: the test is `Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`, its array is `parameters`, and its line is built with `"-- generator: " + parameter`.

- [ ] **Step 7: Check what is left**

```bash
git grep -n -i 'directive' -- . ':!docs/superpowers'
```

Expected: only the three settings of `.editorconfig` (`dotnet_sort_system_directives_first`, `dotnet_separate_import_directive_groups`, `csharp_using_directive_placement`) and the two mentions of the `#nullable` directive.

```bash
git grep -n -i 'sqlsource:' -- . ':!docs/superpowers'
```

Expected: only the rows step 1 wrote on purpose.

- [ ] **Step 8: Format, build and test**

```bash
./format.sh
```

```bash
dotnet test --solution SqlSource.slnx
```

Expected: every test passes, the three new ones included.  `-- generator:` has the length of `-- SqlSource:`, so no pinned column moves.

- [ ] **Step 9: Validate**

```bash
./pre-commit-validation.sh
```

Expected: every step `passed`.

- [ ] **Step 10: Commit**

```bash
git add -A
```

```bash
git commit -m "Rename the SqlSource marker to generator, and its directives to generator parameters

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: the dialect becomes the marker `-- dialect:`

**Files:**
- Rename: `src/SqlSource/Parsing/SqlPreambleDialect.cs` to `SqlDialectMarker.cs`
- Rename: `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs` to `SqlDialectMarkerTests.cs`
- Rename: `tests/SqlSource.Tests/EndToEnd/Dialects/ByDirective.sql` to `ByMarker.sql`
- Create: `tools/package-install/Queries/ByMarker.sql`
- Modify: `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlGeneratorParameterScope.cs`, `SqlFileParser.cs`, `SqlParseErrorKind.cs`, `SqlDialectName.cs`, `SqlLexer.cs`
- Modify: `src/SqlSource/Generation/DialectSetting.cs`, `FileDialect.cs`, `SqlFileReader.cs` (comments only)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/AGENTS.md`
- Modify: `README.md`, `docs/diagnostics.md`, `CONTRIBUTING.md`
- Modify: `tools/package-install/Program.cs`, `tools/package-install/expected-output.txt`
- Test: `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs`, `SqlMarkerReaderTests.cs`, `SqlTextBuilderTests.cs`, `SqlGeneratorParameterScopeTests.cs`, `SqlFileParserTests.cs`, `SqlFileParserAllocationTests.cs`, `SqlDialectNameTests.cs`; `tests/SqlSource.Tests/Generator/DialectTests.cs`, `FileDiagnosticsTests.cs`; `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`; `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`, `DialectQueries.cs`

**Interfaces:**
- Consumes: Task 3's `SqlMarkerKind.GeneratorParameters`, `SqlGeneratorParameterScope`, `SqlParseErrorKind.InvalidMarkerValue`, `ConflictingSettings`, `UnknownGeneratorParameter`.
- Produces:
  - `SqlMarkerKind.Dialect`
  - `static int SqlDialectMarker.Apply(SqlLexer lexer, string text)`
  - `static bool SqlDialectMarker.TryRead(string text, SqlMarker marker, out SqlDialectChoice dialect)`
  - `static string SqlDialectMarker.Describe(string text, SqlMarker marker)`
  - `SqlGeneratorParameterScope()` with no parameter, and without `Dialect`, `TryFindDialect` and `ReportMisplacedDialects`
  - `SqlParseErrorKind.MisplacedDialect` carries no argument

- [ ] **Step 1: Record what a parse allocates today**

In `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, change the assertion's bound for one run, so that the failure message prints the measured value:

```csharp
        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(0);
```

```bash
dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Parsing.SqlFileParserAllocationTests
```

Expected: four failures, each naming a value near 9.7.  Write the four values down for the pull request, then put `BudgetInBytesPerCharacter` back.

- [ ] **Step 2: Rename the files**

```bash
git mv src/SqlSource/Parsing/SqlPreambleDialect.cs src/SqlSource/Parsing/SqlDialectMarker.cs
```

```bash
git mv tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs
```

```bash
git mv tests/SqlSource.Tests/EndToEnd/Dialects/ByDirective.sql tests/SqlSource.Tests/EndToEnd/Dialects/ByMarker.sql
```

```bash
git grep -l 'SqlPreambleDialect\|ByDirective' -- . ':!docs/superpowers' | xargs perl -pi -e 's/SqlPreambleDialect/SqlDialectMarker/g; s/ByDirective/ByMarker/g'
```

- [ ] **Step 3: Write the tests of the marker reader and the text builder**

`tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, added to `Read_MarkerComment_ReturnsItsKindAndValue`:

```csharp
    [InlineData("-- dialect: postgres", "Dialect:postgres")]
    [InlineData("-- DIALECT: MySql", "Dialect:MySql")]
    [InlineData("--dialect:mysql, ansi-quotes  ", "Dialect:mysql, ansi-quotes")]
    [InlineData("-- dialect:", "Dialect:")]
```

to `Read_AnythingElse_IsNotAMarker`:

```csharp
    [InlineData("-- dialect=mysql")]
    [InlineData("-- dialects: mysql")]
    [InlineData("SELECT 1 -- dialect: mysql")]
```

and to `Read_HashComment_IsNotAMarker`:

```csharp
    [InlineData("# dialect: mysql")]
```

`tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs`, added to `Build_MarkerLines_AreRemoved`:

```csharp
    [InlineData("-- dialect: mysql\nSELECT 1", "SELECT 1")]
```

- [ ] **Step 4: Rewrite the tests of the dialect marker**

Replace the body of the class in `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs` (the `using` lines stay):

```csharp
public class SqlDialectMarkerTests
{
    [Theory]
    [InlineData("-- dialect: mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- DIALECT: MySql\n-- name: A\nSELECT 1")]
    [InlineData("--dialect:mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- dialect: mysql  \r\n-- name: A\r\nSELECT 1")]
    [InlineData("\n\n  -- dialect: mysql\nSELECT 1")]
    [InlineData("-- a comment\n-- generator: keep-comments\n-- dialect: mysql\nSELECT 1")]
    [InlineData("/* Copyright\n   (c) Example */\n-- dialect: mysql\n-- name: A\nSELECT 1")]
    public void Apply_MarkerInTheHeader_SwitchesTheLexer(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    [InlineData("-- dialect: mysql,no-backslash-escapes\nSELECT 'C:\\temp\\'")]
    [InlineData("-- dialect: mysql, no-backslash-escapes\nSELECT 'C:\\temp\\'")]
    [InlineData("-- dialect: MySQL ,\tNO_BACKSLASH_ESCAPES\nSELECT 'C:\\temp\\'")]
    public void Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(
            SqlDialectRules.For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes))
        );
        lexer.Rules.ShouldNotBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    // No marker.
    [InlineData("SELECT 1")]
    [InlineData("-- name: A\nSELECT 1")]
    [InlineData("")]
    // After SQL.
    [InlineData("SELECT 1;\n-- dialect: mysql\n")]
    [InlineData("-- a\nSELECT 1; -- dialect: mysql")]
    // After a hint, which is kept in the SQL.
    [InlineData("/*+ h */\n-- dialect: mysql\nSELECT 1")]
    // Inside a named query.
    [InlineData("-- name: A\n-- dialect: mysql\nSELECT 1")]
    // Not a dialect.
    [InlineData("-- dialect: pgsql\nSELECT 1")]
    [InlineData("-- dialect:\nSELECT 1")]
    [InlineData("-- dialect: mysql keep-comments\nSELECT 1")]
    [InlineData("-- dialect: postgres,ansi-quotes\nSELECT 1")]
    // Not a marker.
    [InlineData("-- dialect=mysql\nSELECT 1")]
    [InlineData("-- dialects: mysql\nSELECT 1")]
    [InlineData("/* -- dialect: mysql */\nSELECT 1")]
    // The old form: a generator parameter that is not known.
    [InlineData("-- generator: dialect=mysql\nSELECT 1")]
    public void Apply_NoValidMarkerInTheHeader_LeavesTheLexerAsItWas(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
    }

    [Fact]
    public void Apply_SeveralMarkers_TakesTheFirstThatNamesADialect()
    {
        const string Text = "-- dialect: nope\n-- dialect: oracle\n-- dialect: mysql\n-- name: A\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // The header is read under the dialect the lexer starts with, up to the marker.
    [Fact]
    public void Apply_CommentFormOfTheStartingDialectAboveTheMarker_IsRead()
    {
        const string Text = "# licence\n-- dialect: postgres\nSELECT 1";
        var underMySql = new SqlLexer(Text, SqlDialectRules.MySql);
        var underAnsi = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(underMySql, Text);
        var headerEnd = SqlDialectMarker.Apply(underAnsi, Text);

        underMySql.Rules.ShouldBeSameAs(SqlDialectRules.PostgreSql);

        // To ANSI the first line is SQL, so the header is empty and the marker is not in it.
        underAnsi.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
        headerEnd.ShouldBe(0);
    }

    // The marker applies from the line after it: a comment form of the new dialect is read there.
    [Fact]
    public void Apply_CommentFormOfTheNewDialectBelowTheMarker_IsPartOfTheHeader()
    {
        const string Text = "-- dialect: mysql\n# c\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var headerEnd = SqlDialectMarker.Apply(lexer, Text);

        headerEnd.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-- a\n-- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- dialect: mysql\n\n  -- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- name: A\n-- name: B\nSELECT 1", "-- name: A")]
    public void Apply_FileWithANameMarker_EndsTheHeaderAtTheMarker(string text, string marker)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlDialectMarker.Apply(lexer, text).ShouldBe(text.IndexOf(marker, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1", 0)]
    [InlineData("  \n SELECT 1", 0)]
    [InlineData("", 0)]
    [InlineData("-- a\nSELECT 1", 4)]
    [InlineData("-- a\n/* b */ SELECT 1", 12)]
    [InlineData("-- a\n-- b", 9)]
    [InlineData("-- a\n/*+ h */ SELECT 1", 4)]
    public void Apply_FileWithoutANameMarker_EndsTheHeaderAfterItsLastLeadingComment(string text, int expected)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlDialectMarker.Apply(lexer, text).ShouldBe(expected);
    }

    [Fact]
    public void Apply_ThenReadToEnd_GivesEveryLexemeOfTheFile()
    {
        const string Text = "-- dialect: mysql\n-- name: A\nSELECT 'a\\'b' # c\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlDialectMarker.Apply(lexer, Text);
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        result.Lexemes[0].Span.Start.ShouldBe(0);
        result.Lexemes[^1].Span.End.ShouldBe(Text.Length);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.LineComment).ShouldBe(3);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.Quoted).ShouldBe(1);
    }

    [Theory]
    [InlineData("-- dialect: mysql", "dialect: mysql")]
    [InlineData("-- DIALECT:   MySql, ansi-quotes  ", "dialect: MySql, ansi-quotes")]
    [InlineData("-- dialect:", "dialect:")]
    [InlineData("-- dialect:   ", "dialect:")]
    public void Describe_Marker_GivesTheWordAndTheTrimmedValue(string line, string expected)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]).ShouldNotBeNull();

        SqlDialectMarker.Describe(line, marker).ShouldBe(expected);
    }
}
```

- [ ] **Step 5: Move the dialect out of the tests of the scope**

In `tests/SqlSource.Tests/Parsing/SqlGeneratorParameterScopeTests.cs`:

- Delete every test whose name holds `Dialect`, and the `TryFindDialect_` and `ReportMisplacedDialects_` tests.  What they pinned is now pinned in `SqlDialectMarkerTests` and `SqlFileParserTests`.
- Delete `scope.Dialect.ShouldBeNull();` from the first test, the `Plain` helper, and the `Read(int headerEnd, ...)` overload; the remaining `Read(params string[] lines)` builds `new SqlGeneratorParameterScope()`.
- Add:

```csharp
    // The dialect is a marker of its own.  As a generator parameter the word means nothing.
    [Theory]
    [InlineData("dialect=mysql")]
    [InlineData("dialect")]
    [InlineData("DIALECT=postgres")]
    public void Read_Dialect_IsNotAGeneratorParameter(string parameter)
    {
        var line = "-- generator: " + parameter;

        var (_, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(line, parameter), parameter),
        ]);
    }
```

In `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, remove `"dialect=name"` from the list and build the scope with `new SqlGeneratorParameterScope()`.

- [ ] **Step 6: Translate the tests of the file parser**

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, first the lines that hold the dialect and nothing else:

```bash
perl -pi -e 's/-- generator: dialect=/-- dialect: /g; s/DialectGeneratorParameter/DialectMarker/g' tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs tests/SqlSource.Tests/Generator/DialectTests.cs tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs
```

Then replace these tests whole.  `Parse_DialectMarkerInAFileWithoutANameMarker_GoesAboveItsSql`:

```csharp
    [Fact]
    public void Parse_DialectMarkerInAFileWithoutANameMarker_GoesAboveItsSql()
    {
        const string Text =
            "-- summary: S\n-- dialect: oracle\n-- generator: keep-comments\nSELECT q'[it's]' --+ h\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        Sql(block).ShouldBe("SELECT q'[it's]' --+ h");
        block.Summary.ShouldBe("S");
        block.KeepComments.ShouldBeTrue();
    }
```

The two-dialects test that differs only in options:

```csharp
    [Fact]
    public void Parse_TwoDialectMarkersThatDifferOnlyInOptions_IsAnError()
    {
        const string Text = "-- dialect: mysql\n-- dialect: mysql, ansi-quotes\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Text, "mysql, ansi-quotes"),
                    "dialect: mysql, ansi-quotes"
                ),
            ]);
    }
```

The misplaced tests, from `Parse_DialectMarkerInsideAQueryOrAfterSql_IsMisplaced` to `Parse_TwoDialectsInOneHeader_...`:

```csharp
    // The place is checked before the value, so text that was never meant as a dialect is reported the same way.
    [Theory]
    [InlineData("-- name: A\n-- dialect: mysql\nSELECT 1\n")]
    [InlineData("-- dialect: mysql\n-- name: A\n-- dialect: mysql\nSELECT 1\n")]
    [InlineData("SELECT 1\n-- dialect: mysql\nFROM t\n")]
    [InlineData("-- name: A\nSELECT 1\n-- name: B\n-- dialect: nope\nSELECT 2\n")]
    [InlineData("-- name: A\n-- dialect: see the wiki\nSELECT 1\n")]
    [InlineData("-- name: A\n-- dialect:\nSELECT 1\n")]
    public void Parse_DialectMarkerInsideAQueryOrAfterSql_IsMisplaced(string text)
    {
        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ShouldBe(SqlParseErrorKind.MisplacedDialect);
        error.Span.Start.ShouldBe(text.LastIndexOf("-- dialect:", StringComparison.Ordinal));
        error.Span.End.ShouldBe(text.IndexOf('\n', error.Span.Start));
        error.Arguments.Count.ShouldBe(0);
    }

    // A marker after the last SQL of its block is reported as that.  A dialect marker is reported as misplaced too,
    // so that the user learns at once that it belongs at the top of the file.
    [Theory]
    [InlineData("SELECT 1;\n-- dialect: mysql\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- dialect: nope\n")]
    [InlineData("-- name: A\nSELECT 1;\n-- DIALECT:\n-- name: B\nSELECT 2;\n")]
    public void Parse_DialectMarkerAfterTheLastSqlOfItsBlock_IsMisplacedAsWellAsAtTheEndOfItsBlock(string text)
    {
        var errors = Errors(text);
        var marker = text.IndexOf("-- dialect:", StringComparison.OrdinalIgnoreCase);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.MarkerAtEndOfBlock, SqlParseErrorKind.MisplacedDialect]);
        errors[0].Span.Start.ShouldBe(marker);
        errors[1].Span.ShouldBe(errors[0].Span);
    }

    // A misplaced marker does not change how the file is read: the # would be a comment under MySQL.
    [Fact]
    public void Parse_MisplacedDialectMarker_IsNotApplied() =>
        Errors("-- name: A\n-- dialect: mysql\nSELECT 'it''s' # '\n")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.UnterminatedQuote);

    [Fact]
    public void Parse_DialectMarkerAfterSqlInThePreamble_IsReportedWithTheSql()
    {
        const string Text = "SELECT 0;\n-- dialect: mysql\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.SqlBeforeFirstName, SqlParseErrorKind.MisplacedDialect]);
    }

    [Theory]
    [InlineData("-- dialect: pgsql\n-- name: A\nSELECT 1\n", "pgsql")]
    [InlineData("-- dialect: postgres,ansi-quotes\nSELECT 1\n", "postgres,ansi-quotes")]
    [InlineData("-- dialect: mysql,\nSELECT 1\n", "mysql,")]
    // Several words on one line were the old form.  The line is one value, and it is not a dialect.
    [InlineData("-- dialect: mysql keep-comments\nSELECT 1\n", "mysql keep-comments")]
    [InlineData("-- dialect: mysql -- legacy\nSELECT 1\n", "mysql -- legacy")]
    public void Parse_DialectMarkerWithoutAValidValue_IsAnErrorAtTheValue(string text, string value) =>
        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "dialect: " + value),
            ]);

    [Fact]
    public void Parse_DialectMarkerWithoutAValue_IsAnErrorAtTheMarker()
    {
        const string Text = "-- dialect:\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- dialect:"), "dialect:"),
            ]);
    }

    [Fact]
    public void Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot()
    {
        const string Conflict = "-- dialect: mysql\n-- dialect: oracle\n-- name: A\nSELECT 1\n";
        const string Repeat = "-- dialect: mysql\n-- dialect: MYSQL\n-- name: A\nSELECT 1 # c\n";

        Errors(Conflict)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    SpanOf(Conflict, "oracle"),
                    "dialect: oracle"
                ),
            ]);
        Sql(Blocks(Repeat).ShouldHaveSingleItem()).ShouldBe("SELECT 1");
    }

    // An invalid marker does not hide a conflict between the two valid ones around it.
    [Fact]
    public void Parse_InvalidDialectMarkerBetweenTwoThatDiffer_ReportsBoth()
    {
        const string Text = "-- dialect: mysql\n-- dialect: nope\n-- dialect: oracle\nSELECT 1\n";

        Errors(Text)
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.InvalidMarkerValue, SqlParseErrorKind.ConflictingSettings]);
    }

    [Fact]
    public void Parse_DialectMarkerInAFileWithWindowsLineEndings_IsRead()
    {
        const string Text =
            "-- dialect: mysql, no-backslash-escapes  \r\n-- name: A\r\nSELECT 'C:\\temp\\' AS p # c\r\n";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT 'C:\\temp\\' AS p");
    }

    [Fact]
    public void Parse_DialectAsAGeneratorParameter_IsNotKnownAndIsNotApplied()
    {
        const string Text = "-- generator: dialect=mysql\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnknownGeneratorParameter,
                    SpanOf(Text, "dialect=mysql"),
                    "dialect=mysql"
                ),
            ]);
    }
```

Read the rest of the file's dialect tests after the `perl` pass.  A test whose text still joins the dialect and another word on one line gets two lines, as in the first replacement above.

In `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, the parameter `hasDirective` (by now `hasGeneratorParameter`) becomes `hasDialect`, and the file's start becomes:

```csharp
        var file = new StringBuilder("-- Copyright (c) Example\n")
            .Append(hasDialect ? "-- dialect: postgres\n" : string.Empty)
            .Append("-- generator: token-ignore=raw\n\n");
```

In `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`, the two spans read `"dialect: MariaDB".AsSpan(9)` and `"dialect: MariaDB,ansi-quotes".AsSpan(9)`.

- [ ] **Step 7: Translate the generator tests**

`tests/SqlSource.Tests/Generator/DialectTests.cs`: in the three theories, the parameter `directive` (by now `parameter`) becomes `marker`, and the SQL is built with:

```csharp
        var sql = (marker is null ? string.Empty : "-- dialect: " + marker + "\n") + PathQuery;
```

(`+ Query` in the first theory).  In this file, in `FileDiagnosticsTests.cs` and in `EndToEndTests.cs`, a test name that says `GeneratorParameter` for the dialect says `Marker`: `Run_File_IsReadByItsMarkerThenItsMetadataThenTheProperty`, `Run_OptionOfADialect_IsReadFromTheMarkerTheMetadataAndTheProperty`, `Run_MarkerWithAnOptionThatIsNotValid_IsAnErrorAtItsValue`, `Run_InvalidMetadataOfAFileWithAMarker_IsStillReportedAndTheMarkerIsUsed`.  Add to `Run_OptionOfADialect_IsReadFromTheMarkerTheMetadataAndTheProperty`:

```csharp
    // In the marker the value is the rest of the line, read as the property is.
    [InlineData("mysql, no-backslash-escapes", null, null)]
    [InlineData("MySQL ,  NO_BACKSLASH_ESCAPES", null, null)]
```

The invalid-option test expects the value's span, `postgres,ansi-quotes` at columns 13 to 33:

```csharp
        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC111 /app/Repo/Q.sql(1,13)-(1,33): ");
```

The class comment starts "Which dialect a file is read by: the `-- dialect:` marker at the top of the file decides, then ...".

`tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs`:

```csharp
    [Fact]
    public void Run_DialectMarkerInsideAQuery_IsAnErrorAtTheMarker()
    {
        var run = GeneratorHarness.Run(
            Source,
            new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\n-- dialect: mysql\nSELECT 1;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC115 /app/Repo/Users.sql(2,1)-(2,18): The '-- dialect:' marker must come before the file's first "
                + "query and before any SQL",
        ]);
        run.Sources["App.Sample.g.cs"].ShouldBe(EmptySqlClass);
    }

    [Fact]
    public void Run_DialectMarkerThatIsNotValidOrConflicts_IsAnErrorAtItsValue()
    {
        var run = GeneratorHarness.Run(
            Source,
            new SqlFile(
                "/app/Repo/Users.sql",
                "-- dialect: pgsql\n-- dialect: mysql\n-- dialect: oracle\n-- name: GetUser\nSELECT 1;\n"
            )
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC111 /app/Repo/Users.sql(1,13)-(1,18): 'dialect: pgsql' lacks a value it needs, has one it does "
                + "not take, or has one that is not valid",
            "SQLSRC112 /app/Repo/Users.sql(3,13)-(3,19): 'dialect: oracle' conflicts with a setting given earlier in "
                + "the same scope",
        ]);
    }
```

`tests/SqlSource.Tests/EndToEnd/Dialects/ByMarker.sql`:

```sql
/* The marker is below this comment, which the project's dialect reads. */
-- dialect: mssql
SELECT [it's] FROM #orders; -- a comment
```

`tests/SqlSource.Tests/EndToEnd/DialectQueries.cs`'s comment ends "and from a marker in the file."  `EndToEndTests.cs`'s test is `ProjectWithADialect_FileWithAMarker_IsReadByTheDialectItNames` and reads `DialectQueries.ByMarker`.

- [ ] **Step 8: Run the tests to see them fail**

```bash
dotnet build SqlSource.slnx
```

Expected: the build fails.  The errors name what the next steps add: `SqlMarkerKind.Dialect`, `SqlDialectMarker.Describe`, and a constructor of `SqlGeneratorParameterScope` without arguments.  That is the red state; a failure that names anything else is a mistake in steps 3 to 7.

- [ ] **Step 9: Add the marker kind**

`src/SqlSource/Parsing/SqlMarkerKind.cs`, after `GeneratorParameters`:

```csharp
    /// <summary><c>-- dialect:</c> names the dialect of the file.</summary>
    Dialect,
```

`src/SqlSource/Parsing/SqlMarkerReader.cs`:

```csharp
    private static readonly (string Keyword, SqlMarkerKind Kind)[] Keywords =
    [
        ("name:", SqlMarkerKind.Name),
        ("summary:", SqlMarkerKind.Summary),
        ("generator:", SqlMarkerKind.GeneratorParameters),
        ("dialect:", SqlMarkerKind.Dialect),
    ];
```

- [ ] **Step 10: Write `SqlDialectMarker`**

`src/SqlSource/Parsing/SqlDialectMarker.cs`, whole:

```csharp
using System;

namespace SqlSource.Parsing;

/// <summary>
/// The <c>-- dialect:</c> marker: what a valid one is, and putting a file's into effect before the text after it is
/// lexed.
/// </summary>
/// <remarks>
/// A file's dialect is set in its header: the comments that come before its first SQL and before its first
/// <c>-- name:</c> marker.  The header is read under the dialect the lexer starts with, and the marker applies from
/// the line after it.  Reporting a marker that is wrong or in the wrong place is left to
/// <see cref="SqlFileParser" />.
/// </remarks>
internal static class SqlDialectMarker
{
    private const string Word = "dialect:";

    /// <summary>
    /// Reads the comments at the start of <paramref name="text" /> from <paramref name="lexer" /> and switches the
    /// lexer to the dialect that the first valid <c>-- dialect:</c> marker among them names.  Returns the offset
    /// where the header ends: a <c>-- dialect:</c> marker that starts at or after it is misplaced.
    /// </summary>
    public static int Apply(SqlLexer lexer, string text)
    {
        var switched = false;
        while (lexer.TryReadLeadingComment(out var comment))
        {
            if (SqlMarkerReader.Read(text, comment) is not { } marker)
            {
                continue;
            }

            if (marker.Kind == SqlMarkerKind.Name)
            {
                return marker.Span.Start;
            }

            if (!switched && marker.Kind == SqlMarkerKind.Dialect && TryRead(text, marker, out var dialect))
            {
                lexer.Rules = SqlDialectRules.For(dialect);
                switched = true;
            }
        }

        return lexer.Position;
    }

    /// <summary>
    /// Reads the value of a <c>-- dialect:</c> marker: the rest of its line, as <see cref="SqlDialectName" /> reads
    /// the MSBuild property.  False for a value that is empty or is not a dialect with its options.  Nothing is
    /// allocated.
    /// </summary>
    public static bool TryRead(string text, SqlMarker marker, out SqlDialectChoice dialect) =>
        SqlDialectName.TryParse(text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length), out dialect);

    /// <summary>
    /// The marker as a message names it: <c>dialect: mysql</c>, or <c>dialect:</c> for one without a value.
    /// </summary>
    public static string Describe(string text, SqlMarker marker) =>
        marker.ValueSpan.IsEmpty
            ? Word
            : Word + " " + text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
}
```

- [ ] **Step 11: Take the dialect out of the scope**

In `src/SqlSource/Parsing/SqlGeneratorParameterScope.cs`:

- The class has no parameter: `internal sealed class SqlGeneratorParameterScope`, and the `<param name="headerEnd">` documentation goes.  The summary reads "The generator parameters given by the `-- generator:` markers of one scope: a block, or a file's preamble."
- Delete the `DialectName` constant, the `Dialect` property, `TryFindDialect`, `ReportMisplacedDialects`, `ApplyDialect`, and the `else if (Is(name, DialectName))` branch of `Apply`.

`Apply` is then:

```csharp
    private void Apply(string parameter, TextSpan span, List<SqlParseError> errors)
    {
        var separator = parameter.IndexOf('=');
        var name = separator < 0 ? parameter : parameter.Substring(0, separator);
        var value = separator < 0 ? null : parameter.Substring(separator + 1);
        SqlParseErrorKind? problem;
        if (Is(name, TokenIgnoreName))
        {
            problem = ApplyTokenIgnore(value);
        }
        else if (Is(name, KeepCommentsName) || Is(name, TokenValidationName) || Is(name, NoTokenValidationName))
        {
            problem = value is null ? ApplyFlag(name) : SqlParseErrorKind.InvalidMarkerValue;
        }
        else
        {
            problem = SqlParseErrorKind.UnknownGeneratorParameter;
        }

        if (problem is { } kind)
        {
            errors.Add(SqlParseError.Create(kind, span, parameter));
        }
    }
```

Remove a `using` that the deletions leave unused.

- [ ] **Step 12: Check the dialect marker in the file parser**

In `src/SqlSource/Parsing/SqlFileParser.cs`:

`Parse` calls `SqlDialectMarker.Apply(lexer, text)`, and its documentation ends "A `-- dialect:` marker in the file's header replaces it whole for the text after the marker."

In `Parser`, a field and a method:

```csharp
        // The dialect that a marker of the file has named so far.  It is already in effect by the time the markers
        // are read here; it is kept to find a second marker that names another.
        private SqlDialectChoice? _dialect;
```

```csharp
        // The place is checked first: a marker in the wrong place is reported as that, whatever it names.
        private void ReadDialect(SqlMarker marker)
        {
            if (marker.Span.Start >= headerEnd)
            {
                AddError(SqlParseErrorKind.MisplacedDialect, marker.Span);
                return;
            }

            var place = marker.ValueSpan.IsEmpty ? marker.Span : marker.ValueSpan;
            if (!SqlDialectMarker.TryRead(text, marker, out var dialect))
            {
                AddError(SqlParseErrorKind.InvalidMarkerValue, place, SqlDialectMarker.Describe(text, marker));
            }
            else if (_dialect is { } existing && existing != dialect)
            {
                AddError(SqlParseErrorKind.ConflictingSettings, place, SqlDialectMarker.Describe(text, marker));
            }
            else
            {
                _dialect = dialect;
            }
        }
```

Every `new SqlGeneratorParameterScope(headerEnd)` becomes `new SqlGeneratorParameterScope()`.

In `ReadPreamble`, after the branch for `GeneratorParameters`:

```csharp
                else if (marker is { Kind: SqlMarkerKind.Dialect })
                {
                    ReadDialect(marker.Value);
                }
```

In `ReadBlock`, the loop body becomes:

```csharp
                var marker = SqlMarkerReader.Read(text, lexemes[index]);
                if (marker is not null && lastContent >= 0 && index > lastContent)
                {
                    // A marker comes before the SQL it describes.  One after the block's last SQL would be taken by a
                    // reader to belong to the next block, so it is rejected and not applied.  A dialect marker there
                    // is past the header by definition, and is reported as misplaced too: it belongs at the top of
                    // the file, not above the next SQL.
                    AddError(SqlParseErrorKind.MarkerAtEndOfBlock, marker.Value.Span);
                    if (marker.Value.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker.Value);
                    }
                }
                else if (marker is { Kind: SqlMarkerKind.GeneratorParameters })
                {
                    scope.Read(text, marker.Value, _errors);
                }
                else if (marker is { Kind: SqlMarkerKind.Dialect })
                {
                    ReadDialect(marker.Value);
                }
                else if (marker is { Kind: SqlMarkerKind.Summary, ValueSpan.IsEmpty: false })
                {
                    summary.Add(text.Substring(marker.Value.ValueSpan.Start, marker.Value.ValueSpan.Length));
                }
```

The errors are sorted by where they start with a stable sort, so the two errors of one marker stay in the order they were added.

In `src/SqlSource/Parsing/SqlParseErrorKind.cs`:

```csharp
    /// <summary>
    /// A <c>-- summary:</c>, <c>-- generator:</c> or <c>-- dialect:</c> marker has no SQL after it in its block.  No
    /// argument.
    /// </summary>
    MarkerAtEndOfBlock,
```

```csharp
    /// <summary>
    /// A generator parameter lacks a value it needs, has one it does not take, or has one that is not valid; or a
    /// <c>-- dialect:</c> marker does not name a dialect.  Argument: the parameter as written, or the marker as
    /// <see cref="SqlDialectMarker.Describe" /> gives it.
    /// </summary>
    InvalidMarkerValue,

    /// <summary>
    /// Two settings contradict each other: both validation parameters in one scope, or two dialects in one file.
    /// Argument: the second one, as for <see cref="InvalidMarkerValue" />.
    /// </summary>
    ConflictingSettings,
```

```csharp
    /// <summary>A <c>-- dialect:</c> marker is inside a named query or after SQL.  No argument.</summary>
    MisplacedDialect,
```

- [ ] **Step 13: Reword the diagnostics**

`src/SqlSource/Diagnostics/SqlDiagnostics.cs`:

```csharp
    // SQLSRC108
        messageFormat: "A '-- summary:', '-- generator:' or '-- dialect:' marker comes before the SQL it describes, "
            + "and no SQL follows this one in its query",

    public static readonly DiagnosticDescriptor MisplacedDialect = new(
        id: "SQLSRC115",
        title: "Dialect marker is misplaced",
        messageFormat: "The '-- dialect:' marker must come before the file's first query and before any SQL",
```

`src/SqlSource/AnalyzerReleases.Unshipped.md`:

```
SQLSRC115 | SqlSource | Error | Dialect marker is misplaced
```

Update any test that pins the text of `SQLSRC108`:

```bash
git grep -n "'-- summary:' or '-- generator:'" -- tests
```

- [ ] **Step 14: Run the parser and generator tests**

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

Expected: every test passes except `DiagnosticsDocumentTests`, which fails on the index row of `SQLSRC115` until step 16.  If `Parse_DialectMarkerInAFileWithWindowsLineEndings_IsRead` fails, the marker's value span holds the `\r`: check `SqlMarkerReader.Trim`, which must end the value before trailing whitespace.

- [ ] **Step 15: Update the comments that name the old form**

```bash
git grep -n 'dialect=' -- src ':!docs/superpowers'
```

Each hit is a comment.  Write `-- dialect:` marker for `dialect=` directive or parameter: in `SqlDialectName.cs` ("in the `-- dialect:` marker, in the `SqlSourceDialect` MSBuild property and in the metadata of the same name"), `SqlLexer.cs`, `DialectSetting.cs`, `FileDialect.cs` ("unless a marker in it names another") and `SqlFileReader.cs`.

Expected afterwards: no output from the command.

`src/SqlSource/AGENTS.md`, the bullets under `Parsing/`:

```markdown
- **A dialect's names, and the names of its options, are in `SqlDialectName` only.**  The marker, the MSBuild property and the metadata all read through it, and what they get is a `SqlDialectChoice`: a `SqlDialect` and its `SqlDialectOptions`.  The text of `SQLSRC011` and of `docs/diagnostics.md` repeats the list of names; `SqlDialectName.Accepted` and a test keep the message in step.
- **A file's `-- dialect:` marker is found before the text after it is lexed.**  `SqlDialectMarker.Apply` reads the leading comments from the lexer one at a time and switches its rules at the marker; `SqlFileParser` then reads the rest.  The errors of a marker are reported later, by `SqlFileParser`, from the header's end that `Apply` returns.  The two must agree on what a valid marker is, so both go through `SqlDialectMarker.TryRead`.
- **The dialect is not a generator parameter.**  `SqlGeneratorParameterScope` holds what a `-- generator:` line gives one scope.  A setting with a value of its own, and an MSBuild property beside it, is a marker.
```

- [ ] **Step 16: Rewrite the documents**

`README.md`:

- The marker rule from Task 3 lists four markers: `-- name:`, `-- summary:`, `-- generator:` and `-- dialect:`.
- Under "What reaches the generated SQL": "The `-- name:`, `-- summary:`, `-- generator:` and `-- dialect:` lines are removed."
- The Generator parameters table loses its `dialect=name` row.
- Under Dialects:

````markdown
For one file, with a marker in the file:

```sql
-- dialect: mysql

-- name: FindByNote
SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
```

The marker wins over the metadata, and the metadata over the property.  A file has one dialect:

- The marker goes before the file's first `-- name:` line and before its first SQL.  Anywhere else it is the error [SQLSRC115](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc115).
- It takes effect on the line after it.  Comments above it, such as a licence header, are read by the dialect that the metadata or the property gives.
- A name that is not a dialect is an error: [SQLSRC011](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc011) in the property or the metadata, [SQLSRC111](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc111) in the marker.
````

- Under "Options of a dialect", the example is `-- dialect: mysql, no-backslash-escapes`, the third bullet reads "A file with `-- dialect: mysql` in a project that sets `mysql,ansi-quotes` is read as plain `mysql`.", and the bullet "In the directive, write no space after the comma." is deleted.

`docs/diagnostics.md`:

````markdown
## SQLSRC109

**Generator parameter is not known**

A `-- generator:` marker holds a word that is not a generator parameter.  The generator parameters are `keep-comments`, `token-validation`, `no-token-validation` and `token-ignore=name`.

```sql
-- generator: keep-comment
```

Correct the parameter.  A dialect is not a generator parameter: it has a marker of its own, `-- dialect: name`.
````

````markdown
## SQLSRC111

**Marker value is not valid**

A generator parameter lacks a value it needs, has one it does not take, or has one that is not valid; or a `-- dialect:` marker does not name a dialect.

- `token-ignore` needs a value that is a C# identifier, as in `token-ignore=table`.
- No other generator parameter takes a value.
- `-- dialect:` needs the name of a dialect, as in `-- dialect: postgres`, with any options after it, as in `-- dialect: mysql, ansi-quotes`.  The names and the options are those of [SQLSRC011](#sqlsrc011).  The value is the rest of the line, so nothing else may follow it.

```sql
-- generator: token-ignore
-- generator: keep-comments=true
-- dialect: pgsql
-- dialect: mysql keep-comments
```

Add the missing value, correct the one that is wrong, or remove the one that does not belong.
````

```markdown
## SQLSRC112

**Settings conflict**

Two settings contradict each other.  The error is at the second.

- `token-validation` and `no-token-validation` both appear in one scope.  A scope is the lines before the first `-- name:` marker, or one query.
- Two `-- dialect:` markers name different dialects, or one dialect with different options.  A file has one dialect.

Remove one of the two.  A validation parameter in a query overrides the one before the first `-- name:` marker, and that is not a conflict.  The same dialect given twice with the same options is not a conflict either.
```

````markdown
## SQLSRC115

**Dialect marker is misplaced**

A `-- dialect:` marker sets the dialect of a whole file, and it changes how the text after it is read.  So it must come before the file's first `-- name:` marker and before the file's first SQL.  This one is inside a named query, or after SQL.

```sql
-- name: GetUser
-- dialect: mysql
SELECT 1;
```

A marker after the last SQL of its query is reported twice: as this error, and as [SQLSRC108](#sqlsrc108).

Move the marker to the top of the file.  Comments may come before it, such as a licence header.  In a file with no `-- name:` marker, which is one query, put it above the query's SQL.

```sql
-- dialect: mysql

-- name: GetUser
SELECT 1;
```
````

The index row of 115 is `| [SQLSRC115](#sqlsrc115) | Dialect marker is misplaced |`.  The section of 108 names `-- summary:`, `-- generator:` and `-- dialect:`; the section of 106 says "only comments, `-- generator:` lines and a `-- dialect:` marker may come before the first one".  The section of `SQLSRC011` and any other text that says "in the directive" or "`dialect` parameter" says "in the `-- dialect:` marker".

- [ ] **Step 17: Prove the marker in a packed consumer**

Create `tools/package-install/Queries/ByMarker.sql`:

```sql
/* The marker is below this comment, which the project's dialect reads. */
-- dialect: mssql
SELECT [it's] FROM #staged; -- a comment
```

Under the project's `postgres` the quote in `[it's]` would never close, so the file builds only if the marker is read.

Append to `tools/package-install/Program.cs`:

```csharp

// ByMarker.sql names its own dialect, mssql, with a marker.
Console.WriteLine($"dialect of the file's marker: {Queries.ByMarker}");
```

Append to `tools/package-install/expected-output.txt`:

```
dialect of the file's marker: SELECT [it's] FROM #staged;
```

In `CONTRIBUTING.md`, the sentence about the project reads: "The project uses a constant, a method with tokens, `SqlSourceTokenValidation`, `SqlSourceDialect` and the two markers `-- generator:` and `-- dialect:`, so it fails when the generator or either MSBuild file does not reach a consumer."

- [ ] **Step 18: Measure the parse again**

Repeat step 1's measurement.  Each of the four values must be at or below the one step 1 recorded, within a few hundredths, and under the budget.  Write the four down beside the first four, then put `BudgetInBytesPerCharacter` back.  If a value rose, find the allocation before going on: `TryRead` and `Apply` allocate nothing, and `Describe` runs only for an error.

- [ ] **Step 19: Check what is left**

```bash
git grep -n -i 'dialect=\|SqlPreambleDialect\|ByDirective\|TryFindDialect\|ReportMisplacedDialects' -- . ':!docs/superpowers'
```

Expected: only test data that is the old form on purpose (`-- dialect=mysql`, `-- generator: dialect=mysql`, the rows of `Read_Dialect_IsNotAGeneratorParameter`).

- [ ] **Step 20: Format, test and validate**

```bash
./format.sh
```

```bash
dotnet test --solution SqlSource.slnx
```

Expected: every test passes.

```bash
./pre-commit-validation.sh
```

Expected: every step `passed`; `package-install` prints the new line.

- [ ] **Step 21: Commit**

```bash
git add -A
```

```bash
git commit -m "Make the dialect a marker of its own

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: close the phase

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: Check the spec's success criteria**

```bash
git grep -n 'SqlQueries\|SqlQueriesMode' -- . ':!docs/superpowers'
```

Expected: no output.

```bash
git grep -n -i 'sqlsource:\|dialect=\|directive' -- . ':!docs/superpowers'
```

Expected: only the hits Tasks 3 and 4 list as deliberate.

```bash
git log --oneline main..HEAD
```

Expected: the spec commit, the plan commit, and one commit for each of Tasks 1 to 4.

- [ ] **Step 2: Mark the phase done in the epic outline**

In the phase table, the row of phase 0 says `Done` where it says `In progress`.

- [ ] **Step 3: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, so `VersionPrefix` in `Directory.Build.props` stays as it is.  If a tag is printed and `VersionPrefix` is not greater, stop and ask the owner for the new version.

- [ ] **Step 4: Validate and commit**

```bash
./pre-commit-validation.sh
```

```bash
git add -A
```

```bash
git commit -m "Mark phase 0 of query generation done

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Hand over**

The pull request's description states the eight allocation values from Task 4, before and after, and that no time measurement was needed: the header pass does the same work on a shorter marker.  Opening the pull request is the owner's call.
