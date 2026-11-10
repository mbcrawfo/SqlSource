# Generator Parameter Values Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a generator parameter take a value from a list of words of its own, beyond `on` and `off`, with no change a user sees but the message of SQLSRC111 and the settings schema.

**Architecture:** Each parameter gets a `GeneratorParameterDefinition` (its name, its words, its default).  A level's state becomes a word index for each parameter, packed four bits a parameter into a `ulong` (`GeneratorParameterValues`), so that `GeneratorParameterChoices` and `QuerySettings` stay small equatable values.  The flags enum `GeneratorParameters` stays as each parameter's identity and as the mask of the parameters a level names.

**Tech Stack:** C# (netstandard2.0 generator), xUnit, Shouldly, JSON schema.

**Spec:** `docs/superpowers/specs/2026-11-10-collection-methods-design.md`, "Part 1: generator parameters with values".  Part 2 gets its own plan once this lands.

## Global Constraints

- Every switch of today keeps its name, its default and its behaviour; every existing test passes with only mechanical changes to how it spells a state.
- A word is `name`, `name=value`, `name=default` or `default` alone.  `name` alone is `name=on`.  Names and values are matched ignoring case.
- A value that is not in the parameter's list is SQLSRC111 in a `-- generator:` line and SQLSRC019 in a settings file.  SQLSRC111's message names the parameter's words; SQLSRC019's is unchanged.
- A contradiction is one parameter given two different values in one place, `name=default` beside any other value of it included: SQLSRC112 in a line, SQLSRC020 in a settings file, as today.
- Merging is unchanged: each place names only what it changes, nearer over farther; `default` names every parameter at its default.
- `GeneratorParameterChoices` and `QuerySettings` stay `readonly record struct`s with value equality: equal choices compare equal however they were built.
- At most 16 parameters and 16 words a parameter (four bits a slot).
- The repository's rules hold: `./pre-commit-validation.sh` before every commit, `--full` before every push, `branch-review:review` clean before the PR.

## Review Focus

- **Equal choices built two ways must compare equal**, or the generator's incremental cache misses: a slot of a parameter a level does not name must always hold 0.  Test in Task 2: `None.With(KeepComments, Off).Over(None)` equals `None.With(KeepComments, Off)`, and `Reset.Over(x)` equals `Reset` for any `x`.
- **Case and white space in values**: `Keep-Comments=OFF` in a line, `" keep-comments=Off "` in a settings file, read as `off`.  Test in Task 2.
- **`name=default` beside the same effective value is still a contradiction**: `keep-comments=default keep-comments=off` in one line is SQLSRC112 though `off` is the default.  The existing scope test covers it; Task 2 keeps it passing.
- **`default=on` and `keep-comments=` are invalid**, the first with no list of values (it names no parameter), the second with the list.  Test in Task 3.
- **A parameter table past the four-bit limit** fails a test, not a user's build at random.  Test in Task 1.

---

### Task 1: Definitions and packed values

**Files:**
- Create: `src/SqlSource/Settings/GeneratorParameterDefinition.cs`
- Create: `src/SqlSource/Settings/GeneratorParameterValues.cs`
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterDefinitionTests.cs`
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterValuesTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class GeneratorParameterDefinition` with `string Name`, `GeneratorParameters Parameter`, `IReadOnlyList<string> Words`, `int Default`, `int Slot` (the bit position of `Parameter`); constructor `GeneratorParameterDefinition(string name, GeneratorParameters parameter, IReadOnlyList<string> words, int @default)` that throws `ArgumentException` for more than 16 words, a `Default` outside `Words`, or a `parameter` that is not one flag; `static GeneratorParameterDefinition Switch(string name, GeneratorParameters parameter, bool onByDefault)` with words `["on", "off"]`; `bool TryReadValue(ReadOnlySpan<char> text, out int value)` ignoring case; `string ValuesHelp` = `".  The values of <name> are <w1>, <w2> … and default"` (words joined by `", "`, then `" and default"`).
  - `internal readonly record struct GeneratorParameterValues(ulong Packed)` with `const int On = 0`, `const int Off = 1`; `int Get(GeneratorParameters parameter)`; `GeneratorParameterValues With(GeneratorParameters parameter, int value)` (one flag); `bool IsOn(GeneratorParameters parameter)` (`Get == On`); `GeneratorParameterValues Keep(GeneratorParameters mask)` (zero every slot outside `mask`); `GeneratorParameterValues Over(GeneratorParameters mask, GeneratorParameterValues lower)` (this's slots in `mask`, `lower`'s elsewhere).

- [ ] **Step 1: Write the failing tests**

```csharp
// GeneratorParameterDefinitionTests
[Fact] public void TryReadValue_AWordOfTheList_IsItsIndexIgnoringCase()
{
    var mode = new GeneratorParameterDefinition("mode", GeneratorParameters.SortInput, ["on", "off", "fast", "read-committed"], 0);
    mode.TryReadValue("FAST", out var fast).ShouldBeTrue(); fast.ShouldBe(2);
    mode.TryReadValue("Read-Committed", out var rc).ShouldBeTrue(); rc.ShouldBe(3);
}
[Theory, InlineData(""), InlineData("default"), InlineData("slow"), InlineData("on ")]
public void TryReadValue_ATextNotInTheList_IsFalse(string text) => /* mode as above */ .TryReadValue(text, out _).ShouldBeFalse();
[Fact] public void ValuesHelp_NamesEachWordThenDefault() =>
    GeneratorParameterDefinition.Switch("keep-comments", GeneratorParameters.KeepComments, false).ValuesHelp
        .ShouldBe(".  The values of keep-comments are on, off and default");
[Fact] public void Constructor_SeventeenWords_Throws();          // ArgumentException
[Fact] public void Constructor_DefaultOutsideTheWords_Throws();  // ArgumentException
[Fact] public void Constructor_TwoFlags_Throws();                // KeepComments | SortInput

// GeneratorParameterValuesTests
[Fact] public void With_OneSlot_LeavesTheOthers()
{
    var values = default(GeneratorParameterValues).With(GeneratorParameters.SortInput, 5).With(GeneratorParameters.KeepComments, 1);
    values.Get(GeneratorParameters.SortInput).ShouldBe(5);
    values.Get(GeneratorParameters.KeepComments).ShouldBe(1);
    values.Get(GeneratorParameters.ListPadding).ShouldBe(0);
}
[Fact] public void With_AValueAgain_ReplacesIt();                // 5 then 2 reads 2
[Fact] public void Over_TakesTheMaskedSlotsFromThisAndTheRestFromLower();
[Fact] public void Keep_ZeroesEverySlotOutsideTheMask();
[Fact] public void IsOn_IsValueZero();
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameterDefinitionTests|FullyQualifiedName~GeneratorParameterValuesTests"`
Expected: build fails, the two types do not exist.

- [ ] **Step 3: Implement both types**

A slot is four bits at `Slot * 4`; `Slot` is the index of `Parameter`'s one set bit, found by a loop (no `BitOperations` on netstandard2.0).  Doc comments in the style of `GeneratorParameterChoices.cs`.

- [ ] **Step 4: Run the tests to see them pass**

Run: the command of Step 2.  Expected: PASS.

- [ ] **Step 5: Commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource/Settings/GeneratorParameterDefinition.cs src/SqlSource/Settings/GeneratorParameterValues.cs tests/SqlSource.Tests/Settings/GeneratorParameter*Tests.cs
git commit -m "feat: add generator parameter definitions and packed values"
```

### Task 2: Levels, words and query settings hold values

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs` (`Names` becomes `Definitions`; `ReadWord`, `ReadElements`, `TryFind`; remove `TryReadState` and `States`)
- Modify: `src/SqlSource/Settings/GeneratorParameterChoices.cs`
- Modify: `src/SqlSource/Settings/QuerySettings.cs:30,71-80,92`
- Modify: `src/SqlSource/Settings/SettingsLevel.cs:97`
- Modify: `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs`
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs:472`
- Modify: `src/SqlSource/Generation/Placement/TypePlacement.cs:484`, `src/SqlSource/Generation/Methods/MethodPlanner.cs:426,905`, `src/SqlSource/Generation/Models/TypeModels.cs:167,193-194`
- Modify: every test that spells a state or a resolved set (`grep -rln 'GeneratorParameter' tests`), mechanically
- Modify: `docs/architecture/generator.md:402`, `src/SqlSource/Settings/AGENTS.md:17`, `src/SqlSource/Parsing/AGENTS.md:24`
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs`, `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`

**Interfaces:**
- Consumes: Task 1's two types.
- Produces:
  - `GeneratorParameterList.Definitions : GeneratorParameterDefinition[]`, in `GeneratorParameters` flag order (index `i` is the flag `1 << i`), each a `Switch` with today's default.  `GeneratorParameterList.All` and `GeneratorParameterList.Defaults` stay as flags: `Defaults` is "the on/off parameters on by default", and its doc says so.  `static GeneratorParameterDefinition Find(GeneratorParameters parameter)`.
  - `GeneratorParameterList.ReadWord(ReadOnlySpan<char> word, out GeneratorParameterDefinition? definition, out int? value) : GeneratorWordKind` — `value` null for `name=default`; `definition` set for `Parameter` and for an `InvalidState` whose name is a parameter, null otherwise.
  - `GeneratorParameterValues.Defaults` (each definition's `Default`) and `static GeneratorParameterValues Of(GeneratorParameters on)` (each switch `On` if in `on`, else `Off`; a parameter with more words its `Default`).
  - `readonly record struct GeneratorParameterChoices(GeneratorParameters Named, GeneratorParameterValues Values)` with `None`, `Reset` (`All`, `Defaults`), `IsEmpty`, `With(GeneratorParameters parameters, GeneratorParameterState state)` kept for a mask (`On` → `On`, `Off` → `Off`, `Default` → each one's default), `With(GeneratorParameters parameter, int value)`, `Over`, `int? ValueOf(GeneratorParameters parameter)`, `bool? StateOf(GeneratorParameters parameter)` (`ValueOf == On`, or null), `GeneratorParameterValues Resolve()`.  `Values` holds 0 in every slot outside `Named`.
  - `QuerySettings.Parameters : GeneratorParameterValues`; `KeepComments`, `ValidateTokens`, `UseArrayPool`, `PadsLists` read `Parameters.IsOn(...)`.

- [ ] **Step 1: Write the new failing tests**

```csharp
// GeneratorParameterChoicesTests
[Fact] public void With_ThenOverNone_EqualsItself() =>
    GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
        .Over(GeneratorParameterChoices.None)
        .ShouldBe(GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off));
[Fact] public void Reset_OverAnything_IsReset() =>
    GeneratorParameterChoices.Reset.Over(GeneratorParameterChoices.None.With(GeneratorParameters.SortInput, GeneratorParameterState.On))
        .ShouldBe(GeneratorParameterChoices.Reset);
[Fact] public void With_AValue_IsReadBackByValueOf() =>
    GeneratorParameterChoices.None.With(GeneratorParameters.SortInput, 1).ValueOf(GeneratorParameters.SortInput).ShouldBe(1);
[Fact] public void Resolve_NamesNothing_IsTheDefaults() =>
    GeneratorParameterChoices.None.Resolve().ShouldBe(GeneratorParameterValues.Of(GeneratorParameterList.Defaults));

// GeneratorParameterListTests
[Fact] public void Definitions_AreInFlagOrderAndWithinTheSlots()
{
    GeneratorParameterList.Definitions.Length.ShouldBeLessThanOrEqualTo(16);
    for (var i = 0; i < GeneratorParameterList.Definitions.Length; i++)
        GeneratorParameterList.Definitions[i].Parameter.ShouldBe((GeneratorParameters)(1 << i));
}
[Theory, InlineData("Keep-Comments=OFF"), InlineData(" keep-comments=Off ")]
public void ReadElements_AValueInAnyCase_IsRead(string element) =>
    /* ReadElements([element], …) */ .ShouldBe(GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off));
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameter"`
Expected: build fails on the new members.

- [ ] **Step 3: Change the types and their callers**

`ReadWord` reads a bare name as the value `on` through `TryReadValue("on")`, so a parameter whose list has no `on` gives `InvalidState`; the value after `=` through `TryReadValue`, and `default` before either.  `ReadElements` records each word written as `(GeneratorParameters Parameter, int? Value, int Index)` and reports a repeat when the values differ (null against a number differs).  `SqlGeneratorParameterScope` replaces `_on`, `_off`, `_default` with `GeneratorParameterChoices _written` and `GeneratorParameters _defaulted`; a word contradicts when its parameter is in `_written.Named` with another value, or in `_defaulted` and given a value, or given `default` and in `_written.Named`; `Parameters` is `_written.With(_defaulted, Default).Over(_reset ? Reset : None)`.  `SqlFileParser.cs:472` keeps `StateOf`.  Callers that tested a flag (`(settings.Parameters & X) != 0`) read `settings.Parameters.IsOn(X)`.  Tests that compared to flags compare to `GeneratorParameterValues.Of(<the same flags>)`.

- [ ] **Step 4: Update the three documents that name the old shape**

`docs/architecture/generator.md:402`: `Named` and `Values`, a word index in four bits a parameter, `Resolve` giving `GeneratorParameterValues`, `ValueOf` and `StateOf`.  `src/SqlSource/Settings/AGENTS.md:17`: `GeneratorParameterList.Names` → `GeneratorParameterList.Definitions`, and its words and default go in its definition.  `src/SqlSource/Parsing/AGENTS.md:24`: a setting with a value of its own is a generator parameter when it should be settable for any query, file or folder alongside the others, and a marker otherwise; the dialect stays a marker.

- [ ] **Step 5: Run every test**

Run: `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tests.RoslynFloor`
Expected: PASS, with no generated snapshot changed.

- [ ] **Step 6: Commit**

```bash
./pre-commit-validation.sh
git add -A src tests docs
git commit -m "refactor: hold generator parameters as values"
```

### Task 3: SQLSRC111 names the parameter's values

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs:50-80`
- Modify: `docs/manual/diagnostics.md:1103-1110` (SQLSRC111's generator bullet)
- Test: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs:150-169`

**Interfaces:**
- Consumes: `ReadWord`'s `definition` and `GeneratorParameterDefinition.ValuesHelp` (Tasks 1–2).

- [ ] **Step 1: Change the failing test's expectation**

`Read_ParameterWithAValueThatIsNoState_IsInvalid` splits in two:

```csharp
[Theory, InlineData("keep-comments=1"), InlineData("keep-comments="), InlineData("sort-input=true"), InlineData("keep-comments=off=on")]
public void Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues(string word)
{
    var line = "-- generator: " + word;
    var (scope, errors) = Read(line);
    var name = word[..word.IndexOf('=')];
    errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, TextSpans.Of(line, word, last: true), word,
        ".  The values of " + name + " are on, off and default")]);
    scope.Parameters.IsEmpty.ShouldBeTrue();
}
[Theory, InlineData("default="), InlineData("default=off")]
public void Read_DefaultWithAValue_IsInvalidWithNoValues(string word)
{
    var line = "-- generator: " + word;
    var (scope, errors) = Read(line);
    errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, TextSpans.Of(line, word, last: true), word, "")]);
    scope.Parameters.IsEmpty.ShouldBeTrue();
}
```

- [ ] **Step 2: Run them to see the first fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlGeneratorParameterScopeTests"`
Expected: FAIL, the help argument is empty.

- [ ] **Step 3: Pass `definition?.ValuesHelp ?? ""` as the error's second argument**

- [ ] **Step 4: Update SQLSRC111's bullet**

"A generator parameter's value is one of its own, `on` or `off` for every parameter today, or `default`, and the message lists them: `keep-comments=true` is this error, and so is `default` followed by `=`, as in `default=on`."  Keep the `sql error` sample as it is.

- [ ] **Step 5: Run the tests to see them pass**

Run: the command of Step 2, then `dotnet test tests/SqlSource.Repository.Tests` (the diagnostics page's and the docs' tests).
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs docs/manual/diagnostics.md
git commit -m "feat: name a generator parameter's values in SQLSRC111"
```

### Task 4: The settings schema gives each parameter its values

**Files:**
- Modify: `schemas/settings-v1.schema.json:47-62`
- Modify: `docs/architecture/generator.md:444` (the sentence on the generator parameters' pattern)
- Test: `tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs:133-189`

**Interfaces:**
- Consumes: `GeneratorParameterList.Definitions` (Task 2).

- [ ] **Step 1: Change the tests to the per-parameter pattern**

`Schema_EnumOfGeneratorParameters_NamesEveryParameterAndDefault` reads `Definitions` where it read `Names`.  `Schema_PatternOfGeneratorParameters_IsGeneratedFromTheParameters` expects:

```csharp
var alternatives = GeneratorParameterList.Definitions.Select(definition =>
    CaseFree(definition.Name) + "(?:=(?:"
    + string.Join("|", definition.Words.Append(GeneratorParameterList.Default).Select(CaseFree)) + "))?");
var expected = @"^\s*(?:(?:" + string.Join("|", alternatives) + ")|" + CaseFree(GeneratorParameterList.Default) + @")?\s*$";
```

- [ ] **Step 2: Run them to see the pattern test fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SettingsFileSchemaTests"`
Expected: FAIL on the pattern.

- [ ] **Step 3: Write the pattern into the schema**, and its `description`: "…a parameter's name, which turns it on, or its name with `=` and one of its values (`on` or `off`) or `=default`, matched ignoring case…".

- [ ] **Step 4: Update `docs/architecture/generator.md:444`** to say the generator parameters' pattern gives each parameter its own values, from `GeneratorParameterList.Definitions`.

- [ ] **Step 5: Run the tests to see them pass**

Run: the command of Step 2.  Expected: PASS.

- [ ] **Step 6: Commit**

```bash
./pre-commit-validation.sh
git add schemas/settings-v1.schema.json tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs docs/architecture/generator.md
git commit -m "feat: give each generator parameter its values in the settings schema"
```

### Task 5: Review and validate the branch

- [ ] **Step 1: Run `./pre-commit-validation.sh --full`** on the branch as it will be pushed.  Expected: every check passed.
- [ ] **Step 2: Run `branch-review:review`**, fix its findings, and run it again until it is clean.  Minor findings are fixed, not deferred.
- [ ] **Step 3: Check the version** before a PR: `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`, and ask the owner for a version if it is not greater.
