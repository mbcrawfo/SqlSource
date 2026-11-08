# TD-0108 renaming reads Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One copy of each reading that the bug-report renaming shares with the describers and the generator: items 1, 3, 4, 12, 13 and 14 of TD-0108.  A refactor: no behaviour change.

**Architecture:** Each reading moves to the owner the spec names (the describer's errors, `Parsing/`, `ColumnMarker`, `PostgresIdentifiers`, `SqlRenamer`, `NameMapping`, `BundleQueryFile`) and every copy calls it.  Item 14 adds a theory that the three places deciding which markers `query.sql` holds agree, and item 13 a test that every name `NameMapping` makes is shaped as a replacement.

**Tech Stack:** C# (.NET 8 tool, `netstandard2.0` generator), xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0108-duplicated-and-uneven-patterns-in-src.md`, items 1, 3, 4, 12, 13, 14 and their proposed fixes.

## Where merged code differs from the spec

- **Item 2 is already struck** by td-0104 (PR #257): nothing to do.
- Line numbers moved after td-0104, td-0102 and td-0103.  Current sites: `SqlServerMessages.Rename` (`Bundles/Renaming/SqlServer/SqlServerMessages.cs:29-51`) now maps a copy through `IRenamingScope.ParameterOf`; `SqlServerErrors.NamedIn` is at `:421-441`; the unquoting sites of item 12 are `SqlRenamer.cs:458`, `:460-461`, `:485`, `TokenCheck.Sql.cs:212`, `:216-217`, `:221`, `:224` and `PostgresPlanRenamer.cs:758`; the hint openers `SqlRenamer.cs:349` and `TokenCheck.Sql.cs:57`; `MarkOf` is `RenamingRun.cs:159-160` and the second `Length > 1` test `SqlRenamer.cs:490`; `IsReplacementShaped` is `PostgresPlanRenamer.cs:293-309`.
- `SqlServerErrors.NamedIn` adds every name it reads to its set, the empty one and `@ROWCOUNT` of `@@ROWCOUNT` too; the renaming leaves those out.  Neither can be the name of a query's parameter or a copy's, so the shared reader leaves them out and `NamedIn` chooses the same parameter.
- `TokenCheck.cs:189` and `:194` (`OwnWord`) unquote a quoted word of an engine's own string the same way; the spec does not list them, and they take the shared `ContentOf` too, since it is the same reading.
- td-0102 made `NameMapping` give a second spelling of one name a variant that differs by case.  PostgreSQL's `Spelled` is its fold, so its mapping never makes one, and `IsShapedAsReplacement` keeps today's lower-case rule.

## Global Constraints

- No behaviour change: every existing test passes unchanged; the leak checks, the fixtures' renaming tests and the proof stay green.
- `src/SqlSource` is `netstandard2.0`, Roslyn 4.14.0; allocate nothing new per character in the parser (`src/SqlSource/AGENTS.md`).
- The tool shares the generator's code and never copies it (`src/SqlSource.Tool/AGENTS.md`).
- `108-renaming-tables` splits `PostgresPlanRenamer` by `partial` after this unit merges: change only the lines these items need there, and do not split it.
- Each fix updates the AGENTS.md lines and `docs/architecture/` pages that name what it moves.  `src/SqlSource.Tool/AGENTS.md` (8,173 bytes) and `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` (8,174) are at their 8 KB limit: add nothing there.
- Tests: Shouldly, `Method_Scenario_Outcome`, every test class `sealed`, tests mirror source folders.
- `./format.sh` before validating; `./pre-commit-validation.sh` before each commit.

## Review Focus

1. A server message holding `@@ROWCOUNT`, a lone `@`, or a `$` with no digits: the describer's choice of parameter and the renamed message stay what they were.  Task 1 tests the shared readers on these.
2. A `-- name:` value with white space before `->`, a shape that does not read, or no name: parser error spans, the renamer's refusal and the check's token stay the same.  Task 2 tests the split.
3. A column named `!` or `?` alone: the generator reads a mark, the renaming keeps the name whole.  Task 3 pins both.
4. A quoted name holding a doubled quote or `]]`, and a dollar-quoted literal with a tag: renamed and checked as before.  Task 4 tests `ContentOf` on each opener.
5. A marker kind added to `SqlMarkerKind` with no case in one of the three places: the theory of Task 6 fails until all three agree.

---

### Task 1: One reader of the parameter a server message names (item 1)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs` (`NamedIn`, `:419-441`)
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` (`NamedIn`, `:626-641`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerMessages.cs` (`Rename`, remarks)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresOwnStrings.cs` (delete `ParameterNumberIn`, call the describer's)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` (the `NamedIn` rule names `NamesIn`), `docs/architecture/bundles.md` (`:261`, `:364`)
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`

**Interfaces:**
- Produces: `internal static IEnumerable<string> SqlServerErrors.NamesIn(string message)` — each `@name` of the message read as a whole token (`QuoteReader.IsNameCharacterOfSqlServer`), without its `@`, in order; an `@` that no name follows and a global variable `@@x` give nothing.
- Produces: `internal static int? PostgresErrors.FirstParameterNumberIn(string message)` — the number of the first `$n` that `PostgresIdentifiers.ParameterNumberAt` reads, or null.

- [ ] **Step 1: Write the failing tests**

```csharp
// SqlServerErrorsTests
[Theory]
[InlineData("Parameter @id and @name_1 are ambiguous.", new[] { "id", "name_1" })]
[InlineData("@@ROWCOUNT and @ alone, then @x.", new[] { "x" })]
[InlineData("No name here.", new string[0])]
public void NamesIn_Message_IsEachWholeNameAfterAnAt(string message, string[] names) =>
    SqlServerErrors.NamesIn(message).ShouldBe(names);

// PostgresErrorsTests
[Theory]
[InlineData("could not determine data type of parameter $2", 2)]
[InlineData("cost $ and $x, then parameter $12 and $3", 12)]
[InlineData("no parameter", null)]
public void FirstParameterNumberIn_Message_IsTheFirstDollarNumber(string message, int? number) =>
    PostgresErrors.FirstParameterNumberIn(message).ShouldBe(number);
```

- [ ] **Step 2: Run them; expect a compile failure (members missing)**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~NamesIn_|FullyQualifiedName~FirstParameterNumberIn_"`

- [ ] **Step 3: Implement both readers and use them**

`NamedIn` (SQL Server) maps each of `NamesIn` through `copied.ParameterOfCopy`; `NamedIn` (PostgreSQL) returns null when `FirstParameterNumberIn` gives null or a number outside `1..query.Parameters.Count`, as today.  `SqlServerMessages.Rename` becomes `string.Join(' ', SqlServerErrors.NamesIn(message).Select(name => "@" + scope.Parameter(scope.ParameterOf(name))))`; its remarks name `SqlServerErrors.NamesIn`.  `PostgresOwnStrings.Message` calls `PostgresErrors.FirstParameterNumberIn`.  Update the AGENTS.md rule and the two paragraphs of bundles.md to say the renaming reads a message through the describer's own reader.

- [ ] **Step 4: Run the tests of both engines' errors and messages**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Errors|FullyQualifiedName~Messages|FullyQualifiedName~OwnStrings|FullyQualifiedName~ErrorRenaming"`
Expected: PASS

- [ ] **Step 5: Format, validate, commit** — `refactor(bundles): a message's parameter is read by the describer's own reader (TD-0108 item 1)`

### Task 2: One split of a `-- name:` marker (item 3)

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs` (add `SplitName`)
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs` (`ReadName`, `:160-190`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.QueryFile.cs` (`NameMarker`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.Sql.cs` (`Marker`, `case SqlMarkerKind.Name`)
- Test: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs`

**Interfaces:**
- Produces: `internal static (TextSpan Name, int Arrow) SqlMarkerReader.SplitName(string text, TextSpan value)` — "Name" or "Name -> shape": the name's span, without the white space (`char.IsWhiteSpace`) before the arrow, and the index of the first `->` in the value, or -1.  The shape is the text from `Arrow + 2` to the value's end.  Allocates nothing.

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("GetUser", "GetUser", -1)]
[InlineData("GetUser -> one", "GetUser", 8)]
[InlineData("GetUser\t->many", "GetUser", 8)]
[InlineData(" -> one", "", 1)]
[InlineData("A->B->C", "A", 1)]
public void SplitName_Value_IsTheNameBeforeTheFirstArrow(string value, string name, int arrow)
{
    var text = "-- name: " + value;
    var span = new TextSpan(9, value.Length);
    var split = SqlMarkerReader.SplitName(text, span);
    text.Substring(split.Name.Start, split.Name.Length).ShouldBe(name);
    split.Arrow.ShouldBe(arrow < 0 ? -1 : 9 + arrow);
}
```

(`" -> one"`: the name span is empty at the value's start, as `ReadName` gives today.)

- [ ] **Step 2: Run it; expect a compile failure**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SplitName_"`

- [ ] **Step 3: Implement `SplitName` and call it from the three places**

`ReadName` keeps its error and its span `TextSpan.FromBounds(arrow, value.End)`.  `NameMarker` takes the marker (not its value string) and keeps the text after the name as written, `text[name.End..value.End]`.  The check's `case Name` reads the name and the shape through it, the shape refused as `shape.Trim()` as today.

- [ ] **Step 4: Run the parser's and the renaming's tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Parsing"` and `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles"`
Expected: PASS, the allocation budget of `SqlFileParserAllocationTests` included.

- [ ] **Step 5: Format, validate, commit** — `refactor: one split of a name marker, shared by the parser and the renaming (TD-0108 item 3)`

### Task 3: One rule of the nullability mark (item 4)

**Files:**
- Modify: `src/SqlSource/Generation/Models/ModelReader.cs` (`IsNullable`, `:409-415`), `src/SqlSource/Generation/Models/PropertyNames.cs` (`PascalCase` `:20`, `NameOfPart` `:105`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/RenamingRun.cs` (`MarkOf`), `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs` (`QuotedName`, `:490`)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/RenamingRunTests.cs`

**Interfaces:**
- Consumes: `ColumnMarker.HasMark(string name)` (exists; the one rule).
- Produces: `RenamingRun.MarkOf(string name)` unchanged in signature, built as `name.Length > 1 && ColumnMarker.HasMark(name)`.

- [ ] **Step 1: Write the test that pins the difference**

```csharp
// A name that is a mark alone has a mark to the generator, and no stem for the renaming to rename.
[Theory]
[InlineData("id?", "id", "?")]
[InlineData("id!", "id", "!")]
[InlineData("id", "id", "")]
[InlineData("?", "?", "")]
[InlineData("!", "!", "")]
public void MarkOf_Name_IsItsStemAndMark(string name, string stem, string mark) =>
    RenamingRun.MarkOf(name).ShouldBe((stem, mark));
```

- [ ] **Step 2: Run it; it passes today (it pins the behaviour kept)**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~MarkOf_"`

- [ ] **Step 3: Use `ColumnMarker.HasMark` at every site**

The three generator sites call `ColumnMarker.HasMark`; `MarkOf` adds `name.Length > 1` with a remark: the renaming renames a stem, and a name that is a mark alone has none, so it is renamed whole as a name.  `SqlRenamer.QuotedName` takes its stem and mark from `RenamingRun.MarkOf`.

- [ ] **Step 4: Run the generator's and the renaming's tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Models|FullyQualifiedName~ColumnMarker"` and `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles"`
Expected: PASS

- [ ] **Step 5: Format, validate, commit** — `refactor: ColumnMarker.HasMark is the one rule of a nullability mark (TD-0108 item 4)`

### Task 4: PostgreSQL's quoting and the lexer's quoted regions read once (item 12)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresIdentifiers.cs` (add `Quote`; its summary names quoting)
- Modify: `src/SqlSource.Tool/Describing/Postgres/Types/PostgresTypeNames.cs` (`Identifier`), `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresOwnStrings.cs` (`Written`), `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs` (delete `Quoted`, `:319`; its calls use `PostgresIdentifiers.Quote`; `:758` uses `SqlRenamer.ContentOf`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs` (add `ContentOf`, `DollarTagOf`, `HintOpenerLength` beside `NumberRunEnd`; use them in `Hint`, `Quoted`, `QuotedName`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.Sql.cs` (`Sql`'s hint, `Quoted`), `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.cs` (`OwnWord`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresIdentifiersTests.cs`, `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlRenamerTests.cs`

**Interfaces:**
- Produces: `public static string PostgresIdentifiers.Quote(string name)` — `"` + name with each `"` doubled + `"`.
- Produces: `internal static string SqlRenamer.ContentOf(string text, int start, int end)` — the content of the lexer's quoted region `[start, end)` by its opener: `'` with `''` read as `'`, `"` with `""` as `"`, `[` with `]]` as `]`, `$tag$` between its tags.  Throws `InvalidOperationException` for any other opener (callers switch on the opener first).
- Produces: `internal static string SqlRenamer.DollarTagOf(string text, int start)` — the tag of the dollar-quoted region at `start`, `$` to `$` inclusive.
- Produces: `internal static int SqlRenamer.HintOpenerLength(string text, int start)` — 3 for `--+`, `/*+` and `/*!`, 4 for `/*M!`.

- [ ] **Step 1: Write the failing tests**

```csharp
// PostgresIdentifiersTests
[Theory]
[InlineData("users", "\"users\"")]
[InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
[InlineData("", "\"\"")]
public void Quote_Name_IsInDoubleQuotesWithEachQuoteDoubled(string name, string quoted) =>
    PostgresIdentifiers.Quote(name).ShouldBe(quoted);

// SqlRenamerTests
[Theory]
[InlineData("x 'it''s' y", 2, 10, "it's")]
[InlineData("\"a\"\"b\"", 0, 6, "a\"b")]
[InlineData("[a]]b]", 0, 6, "a]b")]
[InlineData("$q$it's$q$", 0, 10, "it's")]
[InlineData("$$x$$", 0, 5, "x")]
public void ContentOf_QuotedRegion_IsItsContentUnescaped(string text, int start, int end, string content) =>
    SqlRenamer.ContentOf(text, start, end).ShouldBe(content);

[Theory]
[InlineData("--+ INDEX(t)", 3)]
[InlineData("/*+ x */", 3)]
[InlineData("/*! x */", 3)]
[InlineData("/*M! x */", 4)]
public void HintOpenerLength_Hint_IsItsOpenersLength(string hint, int length) =>
    SqlRenamer.HintOpenerLength(hint, 0).ShouldBe(length);
```

- [ ] **Step 2: Run them; expect a compile failure**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Quote_Name|FullyQualifiedName~ContentOf_|FullyQualifiedName~HintOpenerLength_"`

- [ ] **Step 3: Implement the four helpers and replace every hand-written copy**

`PostgresTypeNames.Identifier` is `IsBareName(name) ? name : PostgresIdentifiers.Quote(name)`; `PostgresOwnStrings.Written` is `quoted ? PostgresIdentifiers.Quote(name) : name`.  In `PostgresPlanRenamer` change only the `Quoted` calls and line 758.  `SqlRenamer.Quoted` writes a dollar-quoted literal back with `DollarTagOf`.  Add `PostgresTypeNames` and `PostgresOwnStrings` nowhere in `PostgresIdentifiers`' remarks: they now use its rule.

- [ ] **Step 4: Run the describer's and the renaming's tests**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Describing.Postgres|FullyQualifiedName~Bundles"`
Expected: PASS

- [ ] **Step 5: Format, validate, commit** — `refactor: PostgresIdentifiers.Quote, and one reading of a quoted region and a hint's opener (TD-0108 item 12)`

### Task 5: One shape of a replacement (item 13)

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/NameMapping.cs` (add `IsShapedAsReplacement` beside `Make`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs` (delete `IsReplacementShaped`; `StemOfChild` calls the mapping's)
- Modify: `docs/architecture/bundles.md:281` (the shape is `NameMapping.IsShapedAsReplacement`'s)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`

**Interfaces:**
- Produces: `internal static bool NameMapping.IsShapedAsReplacement(ReadOnlySpan<char> text)` — a lower-case ASCII letter, `_`, then `FirstLength` (8) or more lower-case hex digits: what `Make` gives at the renaming's first length.  Remarks: a variant that differs by case is not, and PostgreSQL, whose `Spelled` is its fold, never has one; a test's shorter `firstLength` is not.

- [ ] **Step 1: Write the failing tests**

```csharp
// Every class's replacement, a literal's too, is shaped as one: the plan renamer reads a child of one by it.
[Fact]
public void IsShapedAsReplacement_OfEveryClassAndALiteral_IsTrue()
{
    var mapping = new NameMapping(RenamingKey.Random(), Fold, Spelled);   // the helpers this test class already uses
    foreach (var cls in Enum.GetValues<NameClass>().Where(static c => c != NameClass.Literal))
    {
        NameMapping.IsShapedAsReplacement(mapping.Name("x" + cls, quoted: false, cls)).ShouldBeTrue();
    }
    NameMapping.IsShapedAsReplacement(mapping.Literal("v")).ShouldBeTrue();
}

[Theory]
[InlineData("t_1234abcd", true)]
[InlineData("t_1234abcdef", true)]
[InlineData("t_1234abc", false)]
[InlineData("T_1234abcd", false)]
[InlineData("t_1234ABCD", false)]
[InlineData("tt1234abcd", false)]
[InlineData("t_1234abcg", false)]
public void IsShapedAsReplacement_Text_IsALetterUnderscoreAndEightHexDigits(string text, bool shaped) =>
    NameMapping.IsShapedAsReplacement(text).ShouldBe(shaped);
```

- [ ] **Step 2: Run them; expect a compile failure**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~IsShapedAsReplacement_"`

- [ ] **Step 3: Implement it and use it in `StemOfChild`**

- [ ] **Step 4: Run the renaming's tests**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles"`
Expected: PASS

- [ ] **Step 5: Format, validate, commit** — `refactor(bundles): NameMapping.IsShapedAsReplacement is the one shape of a replacement (TD-0108 item 13)`

### Task 6: One writer of a `-- param:` value, one read of a marker, and the markers pinned (item 14)

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/BundleQueryFile.cs` (add `WriteParam`, `ReadMarker`; `ParamOf` uses both)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.QueryFile.cs` (`ParamMarker` uses `WriteParam`; `Read` replaced by `BundleQueryFile.ReadMarker`)
- Modify: `src/SqlSource.Tool/Bundles/AGENTS.md` (the raw-bundle rule names the theory), `docs/architecture/bundles.md:37` (one sentence: the theory holds the three together)
- Test: `tests/SqlSource.Tool.Tests/Bundles/BundleQueryFileTests.cs`

**Interfaces:**
- Produces: `internal static string BundleQueryFile.WriteParam(char prefix, string name, string? type, bool? nullable)` — `<prefix><name>`, then ` <type>` when given, then ` null` or ` not null` by `nullable`.
- Produces: `internal static T? BundleQueryFile.ReadMarker<T>(string text, SqlDialectRules rules, SqlMarker marker, Func<SqlMarkerScope, T> read) where T : class` — what a `SqlMarkerScope` of `SqlScopeKind.WholeFile` reads from the one marker, or null when it reports an error.

- [ ] **Step 1: Write the tests**

```csharp
[Theory]
[InlineData('@', "id", null, null, "@id")]
[InlineData('@', "id", "int", null, "@id int")]
[InlineData('@', "id", "int", true, "@id int null")]
[InlineData('@', "id", null, false, "@id not null")]
public void WriteParam_Declaration_IsWrittenAsTheGeneratorReadsIt(char prefix, string name, string? type, bool? nullable, string written) =>
    BundleQueryFile.WriteParam(prefix, name, type, nullable).ShouldBe(written);
```

And the theory over every `SqlMarkerKind`, `Of_MarkerOfEveryKind_IsHeldExactlyWhereTheRenamerAndTheCheckReadIt`, its data `Enum.GetValues<SqlMarkerKind>()`, with a private table of one valid marker line per kind (a missing kind fails the test by name).  For each kind, under PostgreSQL:
  1. **The raw bundle:** a file `-- name: Get` + the kind's line + `SELECT 1 AS {{t}}` (with `-- token: {{t:x}}` only where the line needs one), planned and written by `BundleQueryFile.Of`; `held` is whether a line of the result is read by `SqlMarkerReader.Read` as that kind.
  2. **The renamer:** `SqlRenamer.RenameQueryFile` of `-- name: Get\n<line>\nSELECT 1` (the setup of `SqlRenamerTests.Renamer`); `renamed` is whether no refusal is `-- <word>:` (`SqlMarkerReader.WordOf(kind)`).
  3. **The check:** the token check of `query.sql` holding the raw line (the setup of `TokenCheckTests.CheckQuery`); `checkedAsHeld` is whether its refusal is not `-- <word>:`.
  Assert `renamed.ShouldBe(held)` and `checkedAsHeld.ShouldBe(held)`, and that `held` is true for `Name`, `Dialect`, `Database`, `Param`, `Token`, `TokenIgnore` and false for every other kind, so the theory says which are held.

- [ ] **Step 2: Run them; `WriteParam_` fails to compile, the theory passes once it compiles (the three agree today)**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~BundleQueryFileTests"`

- [ ] **Step 3: Implement `WriteParam` and `ReadMarker`, and use them in both places**

`ParamMarker` passes the renamed name, `type is null ? null : Nested(type, NameClass.Type)` and the declaration's nullability.  Update the `Bundles/AGENTS.md` rule: the theory in `BundleQueryFileTests` holds `BundleQueryFile`, `SqlRenamer.QueryFile.cs` and `TokenCheck.Sql.cs` together.

- [ ] **Step 4: Run the bundles' tests**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles"`
Expected: PASS

- [ ] **Step 5: Format, validate, commit** — `refactor(bundles): one writer of a param marker, one read of a marker, and a theory that the markers agree (TD-0108 item 14)`

### Task 7: The tech-debt record

**Files:**
- Modify: `docs/tech-debt/TD-0108-duplicated-and-uneven-patterns-in-src.md`, `docs/tech-debt/README.md` (TD-0108's row only)

- [ ] **Step 1: Strike items 1, 3, 4, 12, 13 and 14 in place**, each `~~…~~ Fixed: <what replaced it, by name>`, and their proposed fixes `~~…~~ Done.`; numbers unchanged, nothing else edited.  In Impact, the list of copies becomes "items 8, 9 and 10".
- [ ] **Step 2: Rewrite the README row's summary** to what remains (the engines' rule tables, the entry rebuilt twice, the JSON writers, `PostgresPlanRenamer`'s size): drop "the renaming copies readings of the describer and the generator".
- [ ] **Step 3: Search** `grep -rn "IsReplacementShaped\|ParameterNumberIn\|NamedIn\|NameMarker" docs src --include=*.md` and fix any stale mention.
- [ ] **Step 4: Validate and commit** — `docs(tech-debt): TD-0108 items 1, 3, 4, 12, 13 and 14 fixed`

## After the tasks

`./format.sh`, then `./pre-commit-validation.sh --full`, then the describers' projects on their floor images (Tasks 1 and 4 change both describers): `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test tests/DatabaseTests/SqlServer`.  Then `/branch-review:review --base origin/main`.
