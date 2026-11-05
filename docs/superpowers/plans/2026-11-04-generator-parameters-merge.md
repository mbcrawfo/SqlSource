# Generator Parameters Merge Per Option Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each level of generator parameters turns options on, off or back to default over the levels below it, instead of replacing their list whole, with the library types' `name=on|off|default` syntax and three negative words renamed to positive ones.

**Architecture:** A level's generator parameters become a `GeneratorParameterChoices` value, the options it names and their states, which `SettingsLevel.Over` merges per option and `QuerySettings.Resolve` applies over the built-in defaults.  The switch is done in two steps: first the merge and the `=state` syntax with today's words, then the rename of the three negative words, so each commit builds and passes.

**Tech Stack:** C# (netstandard2.0 source generator), xUnit and Shouldly, JSON Schema.

**Spec:** `docs/superpowers/specs/2026-11-04-generator-parameters-merge-design.md`

## Global Constraints

- The words are `keep-comments`, `token-validation`, `sort-input`, `sort-output`, `table-models`, `async-method-suffix`, `array-pool` and `default`.  The built-in defaults are `token-validation`, `table-models` and `array-pool` on, every other one off.
- A word is `name`, `name=on`, `name=off` or `name=default`, with no spaces around `=`.  Names and states are matched ignoring case.
- `default` gives every option its built-in default at its level, wherever it stands in the level, and the level's other words apply over it.
- No aliases and no migration hint for `no-token-validation`, `no-table-models` and `no-array-pool`: they are unknown words.
- `[]` in a settings file names nothing and changes nothing.  `["default"]` is the reset.
- One option given two different states in one level is SQLSRC112 in a marker (the later word is not applied) and SQLSRC020 in a settings file (the first applies).  `name=default` beside `name=on` or `name=off` is two different states.  The same state repeated is fine.
- A file with no `-- name:` line is one level.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit (root `AGENTS.md`).  It needs Docker running.
- The historical plans and specs under `docs/superpowers/` are not edited, except this plan and its spec.
- Packed readmes (`README.md`, `src/SqlSource.Tool/README.md`) use absolute links, no HTML; a `sql` sample's info string names its dialect, `fragment` or `error`.

## Review Focus

1. **The parser decides keep-comments apart from `QuerySettings`.**  A chain that keeps comments under a query that writes `keep-comments=off` must drop them, and a chain that keeps them under a file header of `default` too.  Pinned in Task 2's `SqlFileParserTests`.
2. **One option turned off at the query, another kept from above.**  `keep-comments=off` on a query under a file header of `keep-comments no-token-validation` drops the comment and still skips the token check.  Pinned in Task 2's `GeneratorParameterLevelTests`.
3. **Malformed states.**  `keep-comments=`, `keep-comments=1`, `keep-comments=off=on`, `default=off` are SQLSRC111, and `=off` is SQLSRC109.  Pinned in Task 2's scope tests.
4. **The old words after the rename.**  `no-array-pool` in a marker is SQLSRC109, and in a settings file SQLSRC019 at its element, with the other words applied.  Pinned in Task 3.
5. **Caching.**  A level that names no generator parameter equals `SettingsLevel.None`, so a file without settings allocates no level and incremental caching still holds.  Pinned in Task 1 and Task 2.

---

### Task 1: The value of one level's choices

**Files:**
- Create: `src/SqlSource/Settings/GeneratorParameterState.cs`
- Create: `src/SqlSource/Settings/GeneratorParameterChoices.cs`
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs` (add `All` and `Defaults`)
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs`

**Interfaces:**
- Produces:
  - `internal enum GeneratorParameterState { On, Off, Default }`
  - `internal readonly record struct GeneratorParameterChoices(GeneratorParameters Named, GeneratorParameters On)`, invariant `On` ⊆ `Named`, so equal choices compare equal:
    - `bool IsEmpty` (`Named == None`)
    - `static GeneratorParameterChoices None` (`default`, names nothing)
    - `static GeneratorParameterChoices Reset` (`Named = GeneratorParameterList.All`, `On = GeneratorParameterList.Defaults`)
    - `GeneratorParameterChoices With(GeneratorParameters parameter, GeneratorParameterState state)`: `Default` sets the parameter to its bit of `Defaults`
    - `GeneratorParameterChoices Over(GeneratorParameterChoices lower)`: this level's state for each option it names, `lower`'s for the rest
    - `bool? StateOf(GeneratorParameters parameter)`: null when not named
    - `GeneratorParameters Resolve()`: named states over `Defaults`
  - `GeneratorParameterList.All` (every flag) and `GeneratorParameterList.Defaults`, which is `GeneratorParameters.None` until Task 3 renames the flags.

- [ ] **Step 1: Write the failing tests** in `GeneratorParameterChoicesTests`:
  - `Default_IsEmpty_AndResolvesToTheDefaults`: `default(GeneratorParameterChoices).IsEmpty` is true, and `Resolve()` is `GeneratorParameterList.Defaults`.
  - `With_On_NamesItAndTurnsItOn`, `With_Off_NamesItAndTurnsItOff`, `With_Default_NamesItWithItsDefault`: `StateOf(KeepComments)` is `true`, `false` and `(Defaults & KeepComments) != 0`.
  - `Over_OptionNamedAbove_TakesTheUpperState`: `None.With(KeepComments, Off).Over(None.With(KeepComments, On)).StateOf(KeepComments)` is `false`.
  - `Over_OptionNotNamedAbove_KeepsTheLowerState`: `None.With(SortInput, On).Over(None.With(KeepComments, On)).Resolve()` has both `KeepComments` and `SortInput`.
  - `Over_ResetAbove_HidesEveryLowerState`: `Reset.Over(None.With(KeepComments, On)).Resolve()` is `Defaults`.
  - `With_TheSameStateTwice_IsEqual`: two choices built the same way are equal, and `With(KeepComments, Off)` equals `new(KeepComments, None)`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~GeneratorParameterChoicesTests`
Expected: a build error, since `GeneratorParameterChoices` does not exist.

- [ ] **Step 3: Implement the two types and the two constants.**

- [ ] **Step 4: Run them and see them pass**, with the same command.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh` and see it pass, then:

```bash
git add src/SqlSource/Settings tests/SqlSource.Tests/Settings/GeneratorParameterChoicesTests.cs
git commit -m "feat: a level's generator parameter choices, merged per option"
```

### Task 2: Merge per option, with the `=state` syntax

Every level now holds `GeneratorParameterChoices`.  The words are still today's words; `no-token-validation=off` works for one commit, until Task 3.

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs`: `ReadWord`, `ReadElements`, the remarks
- Modify: `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs`
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` (`BuildLevel` tests `Generator.Parameters.IsEmpty`)
- Modify: `src/SqlSource/Settings/SettingsLevel.cs` (`Parameters`, `Over`, `KeepsComments`)
- Modify: `src/SqlSource/Settings/QuerySettings.cs` (`Resolve`)
- Modify: `src/SqlSource/Settings/SettingsLevelReader.cs` (`ReadParameters` reports repeats)
- Modify: `src/SqlSource/SettingsFiles/SettingsFileSource.cs` (`RepeatedKey` for an array's element)
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs` (`ResolveSettings`)
- Modify: `src/SqlSource/Generation/FileParseInput.cs` (the doc comment of `CommentsWanted`, which is now exact)
- Modify: `schemas/settings-v1.schema.json`
- Modify: every test, test helper (`tests/SqlSource.Testing/Generator/LevelSettings.cs`, `GeneratorHarness.cs`) and `.sql` or `sqlsource.json` fixture that builds or asserts a list
- Test: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`, `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`, `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`, `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs`, `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`

**Interfaces:**
- Consumes: Task 1's `GeneratorParameterChoices`, `GeneratorParameterState`, `All` and `Defaults`.
- Produces:
  - `GeneratorParameterList.ReadWord(ReadOnlySpan<char> word, out GeneratorParameters parameter, out GeneratorParameterState state)` returns `GeneratorWordKind { Parameter, Default, UnknownName, InvalidState }`, an enum in the same file, with the outs set for `Parameter`.  The marker scope and `ReadElements` both read words through it.
  - `GeneratorParameterList.ReadElements(IReadOnlyList<string> elements, List<int> invalid, List<(int Index, int First)> repeated)` returns `GeneratorParameterChoices`.
  - `SqlGeneratorParameterScope.Parameters` and `SettingsLevel.Parameters` are `GeneratorParameterChoices`, not nullable; empty means the level says nothing.
  - `QuerySettings.Resolve` gives `markers.Parameters.Over(file.Parameters).Resolve()`.

- [ ] **Step 1: Rewrite the scope tests** in `SqlGeneratorParameterScopeTests`.  The flags go in as numbers, as the file does today:
  - `Parameters_OfANewScope_AreEmpty`.
  - `Read_Words_NameTheirStates`, a theory of line, named and on:
    - `keep-comments` gives (1, 1).
    - `KEEP-COMMENTS=Off` gives (1, 0).
    - `keep-comments=ON` gives (1, 1).
    - `keep-comments=default` gives (1, 0).
    - `no-array-pool=off` gives (64, 0).
  - `Read_Default_IsTheReset`: `default` and `DEFAULT` equal `GeneratorParameterChoices.Reset`.
  - `Read_DefaultBesideOtherWords_ResetsThenAppliesThemInAnyOrder`, with no errors, each equal to `Reset.With(KeepComments, On)`:
    - `default keep-comments`;
    - `keep-comments default`;
    - the two lines `keep-comments` and `default`.
  - `Read_OneOptionInTwoStates_ConflictsAtTheLater`, which has one `ConflictingSettings` error whose argument is the later word, and keeps the earlier state:
    - `keep-comments keep-comments=off`;
    - `keep-comments=default keep-comments=off`;
    - the two lines `keep-comments=off` and `keep-comments`.
  - `Read_OneStateTwice_IsNotAConflict`: `keep-comments keep-comments=on KEEP-COMMENTS`.
  - `Read_ParameterWithAValueThatIsNoState_IsInvalid` (replaces `Read_ParameterWithAValue_IsInvalid`): `InvalidMarkerValue` for each of `keep-comments=1`, `keep-comments=`, `sort-input=true`, `keep-comments=off=on`, `default=`, `default=off`.
  - Delete `Read_DefaultBesideAnotherParameter_ConflictsAtTheSecond`, keep `Read_UnknownGeneratorParameter_IsAnError` with its rows (add `=off`), and update the remaining tests to the choices.

- [ ] **Step 2: Rewrite the element tests** in `GeneratorParameterListTests`:
  - `ReadElements_NoElement_NamesNothing`: `[]` and `["  "]` give an empty value.
  - `ReadElements_Default_IsTheReset`: `["default"]` and `["default", "DEFAULT"]` give `Reset`.
  - `ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem`: `["keep-comments", "default"]` gives `Reset.With(KeepComments, On)`, with no invalid index.
  - `ReadElements_UnknownWordOrState_IsItsIndexAndTheOthersApply`: `["keep-comments", "bogus", "sort-input=maybe"]` gives `KeepComments` on, with invalid `[1, 2]`.
  - `ReadElements_OneOptionInTwoStates_IsARepeatAndTheFirstApplies`: `["keep-comments", "keep-comments=off"]` gives `KeepComments` on and repeated `[(1, 0)]`.  `["keep-comments", "KEEP-COMMENTS=on"]` gives no repeat.

- [ ] **Step 3: Rewrite the level, reader and diagnostic tests:**
  - `QuerySettingsTests`:
    - `Resolve_Parameters_ComeWholeFromTheMostSpecificLevelThatGivesAList` becomes `Resolve_Parameters_MergePerOption`: the markers `sort-input` over the file `keep-comments` give both.
    - Add `Resolve_MarkersTurnAnOptionOff_OverTheFile`.
    - `None_ComparedWithAnEmptyLevel_IsEqual` also holds for `new SettingsLevel { Parameters = default }`.
  - `SettingsLevelReaderTests`: `Read_ParametersAsNoElement_AreTheEmptyList` becomes `Read_ParametersAsNoElement_NameNothing`, and the level read equals `SettingsLevel.None`.  Add `Read_ParametersInTwoStates_ReportTheRepeat`: the fake source records `(Parameters, 1, 0)` as a repeated key.
  - `SettingsFileDiagnosticsTests`: a file whose `generatorParameters` is `["keep-comments", "keep-comments=off"]` gives SQLSRC020 at the second element's string, quotes included.  The message is `'keep-comments=off' is the same key as 'keep-comments' in generatorParameters, and the first applies`.

- [ ] **Step 4: Pin the parser and the levels:**
  - `SqlFileParserTests`, `Parse_QueryTurnsKeepCommentsOff_UnderAChainThatKeepsThem_DropsTheComment`: parse with `commentsWanted: true` and a query that writes `-- generator: keep-comments=off`; its SQL has no comment.  Add the same with the file header `-- generator: default`.  Add `Parse_FileWithoutANameLine_ItsGeneratorLinesAreOneLevel`: a file with no `-- name:` line whose markers are `-- generator: keep-comments`, a blank line, then `-- generator: keep-comments=off`, before its SQL, gives SQLSRC112 at `keep-comments=off`, and its SQL keeps its comment.
  - `GeneratorParameterLevelTests`: replace the header comment with the merge rule, and the two theories' rows, with `On = "keep-comments no-token-validation"`, `Off = "keep-comments=off no-token-validation=off"`, `Other = "sort-input"` and `Default = "default"`.  The columns are the project's file, the folder's file, the file header and the query:
    - **The comment is kept:**
      - `(On, null, null, null)`
      - `(null, On, null, null)`
      - `(null, null, On, null)`
      - `(Default, Other, On, null)`
      - `(null, null, Default, On)`
      - `(On, Other, null, null)`
      - `(On, On, On, Other)`
      - `(Off, null, null, On)`
    - **The comment is dropped and the token checked:**
      - `(null, null, null, null)`
      - `(On, Default, null, null)`
      - `(On, On, Default, null)`
      - `(On, On, On, Default)`
      - `(On, null, null, Off)`
    - Add `Run_QueryTurnsOneParameterOff_KeepsTheOtherFromAbove`: the file header `On` and the query `keep-comments=off`.  The comment is dropped and the token is not checked.
  - Rename every test method whose name says a list replaces.

- [ ] **Step 5: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests`
Expected: a build error, or failures in the rewritten tests.

- [ ] **Step 6: Implement**, by the Interfaces above:
  - **The scope** keeps what each option was written as (on, off or default), to find conflicts, and a reset flag, and builds its choices as `Reset` (when reset) with each written state over it.
  - **The parser** keeps a query's comments when `markers.Parameters.StateOf(KeepComments) ?? commentsWanted` is true.
  - **`SettingsLevel.KeepsComments`** is `(Parameters.Resolve() & KeepComments) != 0`.
  - **`RepeatedKey`** on an array reports at `value.Spans[index]`, with `Elements[index]` and `Elements[first]` trimmed.
  - **The schema:**
    - each item matches a name with an optional `=on`, `=off` or `=default`, or `default` alone, in the case-insensitive character classes the file uses, with white space around it allowed;
    - drop the `not` rule against `default` beside another word;
    - keep `SettingsFileSchemaTests` passing.
  - **Update every remaining test** that read `Parameters` as a nullable list.

- [ ] **Step 7: Keep what every fixture query gets today.**
  - **Where the merge would change a query's settings, add `default` to its list.** That's a query or file header that writes a list under a level that also gives one. Do this in:
    - the `.sql` files under `tests/DatabaseTests/*/EndToEnd/Queries/`, `tests/SqlSource.Tests/EndToEnd/` and `tools/package-install/Queries/`;
    - the `.sql` texts inside C# tests and `tests/SqlSource.Testing/Generator/QueryMethodCorpus.cs`.
  - **Do not add it to a test that exists to show how levels combine.** That test asserts merging instead.
  - **Check a query's sidecar after changing it.** A sidecar holds the hash of the query's SQL, so a query whose SQL changes because its comments now stay needs `tools/describe-end-to-end.sh`. Adding `default` should prevent that.
  - **Check the fixture for every file you changed:**

Run: `git diff --stat -- '*.sql.json'`
Expected: no sidecar changed.

- [ ] **Step 8: Run the tests and see them pass**

Run: `dotnet test tests/SqlSource.Tests && dotnet test tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 9: Validate and commit**

Run `./pre-commit-validation.sh` and see it pass, then commit with `git commit -m "feat: generator parameters merge per option, with on, off and default"`.

### Task 3: Rename the three negative parameters

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameters.cs`: `NoTokenValidation`, `NoTableModels` and `NoArrayPool` become `TokenValidation`, `TableModels` and `ArrayPool`, with the same values (2, 16, 64)
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs`: `Names` and `Defaults = TokenValidation | TableModels | ArrayPool`
- Modify: `src/SqlSource/Settings/QuerySettings.cs` (`ValidateTokens`, `UseArrayPool`), `src/SqlSource/Generation/Models/TypeModels.cs`: each check flips
- Modify: `schemas/settings-v1.schema.json`: the names
- Modify: every file outside `docs/superpowers/` that writes an old word: tests, test helpers, `.sql` fixtures, the `sqlsource.json` files of `tests/SqlSource.Tests/EndToEnd/` and `tools/package-install/`, `tools/package-install/Program.cs` and `expected-output.txt`, `tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs`, the database tests' C# and READMEs, `tools/README.md`, `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`, every `sql` sample and mention in `docs/manual/` and `docs/architecture/generator.md`, `src/SqlSource/Parsing/AGENTS.md`, and the doc comments of `src/`
- Test: `SqlGeneratorParameterScopeTests`, `GeneratorParameterListTests`, `QuerySettingsTests`, `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (its list of parameters), `SettingsFileSchemaTests`

**Interfaces:**
- Consumes: Task 2's grammar.
- Produces: the final enum members and words of the Global Constraints.

- [ ] **Step 1: Write the failing tests:**
  - **Scope tests:**
    - `token-validation=off` gives (2, 0), and `array-pool=OFF table-models=off` gives (80, 0).
    - Move `token-validation` out of `Read_UnknownGeneratorParameter_IsAnError`, and add `no-token-validation`, `no-table-models`, `no-array-pool` and `NO-ARRAY-POOL` to it.
  - **Element tests:** `ReadElements_OldNegativeWord_IsItsIndexAndTheOthersApply`, where `["no-array-pool", "keep-comments"]` gives `KeepComments` on and invalid `[0]`.
  - **`QuerySettingsTests`:**
    - `Resolve_NoLevelSaysAnything_GivesTheDefaults` asserts `Parameters == TokenValidation | TableModels | ArrayPool`, `ValidateTokens` and `UseArrayPool` true.
    - `Resolve_NoTokenValidation_TurnsValidationOff` becomes `Resolve_TokenValidationOff_TurnsValidationOff`, and the same for the array pool.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameter|FullyQualifiedName~QuerySettings"`
Expected: FAIL or a build error.

- [ ] **Step 3: Rename and flip, then replace the old words** in every file of the list above:
  - `no-token-validation` becomes `token-validation=off`, and the same for the other two.
  - Each C# identifier or test name that says `NoTokenValidation` and so on is renamed to say it is off.
  - Check that nothing is left:

Run: `git grep -n -i -E "no-(token-validation|table-models|array-pool)|No(TokenValidation|TableModels|ArrayPool)" -- . ':!docs/superpowers'`
Expected: the unknown-word test rows alone.

- [ ] **Step 4: Run every test and see it pass**

Run: `dotnet test`
Expected: PASS, the documentation tests included (the samples parse and the SQLSRC109 section names the new words).

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh` and see it pass, then commit with `git commit -m "feat: token-validation, table-models and array-pool replace their no- words"`.

### Task 4: The user documentation

**Files:**
- Modify: `docs/manual/settings.md`, `docs/manual/diagnostics.md`, `docs/manual/concepts.md`, `docs/manual/sql-files.md`, `docs/manual/parameters-and-tokens.md`, `docs/manual/generator.md`, `docs/manual/recipes.md`, `docs/manual/README.md`, `README.md`

- [ ] **Step 1: Rewrite `settings.md`:**
  - **The rules list:** drop the exception "A list of generator parameters replaces the one below it whole".
  - **The per-key bullet:** add generator parameters to it, merged per option.
  - **The Generator parameters row:** the new words, and how a word is written.
  - **The generator parameters section:**
    - a table of each word with its built-in default and effect;
    - the four ways to write a word, and what `default` and `name=default` do;
    - "each level turns options on or off over the levels below", with an example of a header and a query that each add one;
    - the order of the levels;
    - a file with no `-- name:` line as one level;
    - the settings file's array, `[]` changing nothing and `["default"]` resetting;
    - each error by place, as the spec's table gives them.
  - **The settings files example and "A file's own marker still wins":** a `-- generator:` line adds to the folders' list, and `default` drops it.
  - **The "A value that is wrong" table:** remove "unless one is `default` beside another", and add the repeated generator parameter.
  - **`sql` samples:** the samples that showed replacement now show merging, with their info strings kept valid.

- [ ] **Step 2: Update the other manual pages:**
  - **`diagnostics.md`:**
    - SQLSRC019: a word or a state that is not valid, no longer `default` beside another.
    - SQLSRC020: a generator parameter given two states in one settings file.
    - SQLSRC109: the words, and that the three old words are no longer parameters.
    - SQLSRC111: a state that is not `on`, `off` or `default`.
    - SQLSRC112: one option in two states.
  - **`concepts.md`:** the generator parameter entry says a level turns options on or off.
  - **`sql-files.md`:** line 190 and anything else that states the old rule.
  - **`parameters-and-tokens.md`, `generator.md`, `recipes.md` and the manual's `README.md`:** any sentence that says a list replaces, or that tells a reader to repeat the file's parameters.

- [ ] **Step 3: Rewrite the README's FAQ answer on `async-method-suffix`** (line 195).
  - Drop the sentence about a list replacing the one further out.
  - Say instead that a file or query's own `-- generator:` line adds to it.
  - Keep its absolute link.

- [ ] **Step 4: Check that no page still states the old rule**

Run: `git grep -n -i -E "replaces? (the one|whole)|never adds|replace; it never" -- docs/manual README.md`
Expected: no line about generator parameters.

- [ ] **Step 5: Run the documentation tests**

Run: `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~Documentation`
Expected: PASS.

- [ ] **Step 6: Validate and commit**

Run `./pre-commit-validation.sh`, then commit with `git commit -m "docs: generator parameters merge per option"`.

### Task 5: The developer documentation

**Files:**
- Modify: `docs/architecture/generator.md`, `src/SqlSource/Settings/AGENTS.md`, `src/SqlSource/Parsing/AGENTS.md`, and the doc comments of `GeneratorParameters`, `GeneratorParameterList`, `SqlGeneratorParameterScope`, `SettingsLevel`, `QuerySettings` and `FileParseInput` that Tasks 2 and 3 did not already fix

- [ ] **Step 1: Update `docs/architecture/generator.md`:**
  - **The markers section (line 498):** the scope builds a `GeneratorParameterChoices`, and a query's markers are its scope's over its preamble's.
  - **Line 399:** the per-key merging is no longer "the exception to the rule that a level replaces the one below it whole" for generator parameters.
  - **The settings section:** how a level's choices merge and resolve over the defaults.
  - **The parser's keep-comments decision.**

- [ ] **Step 2: Update the AGENTS.md files.**
  - `Settings/AGENTS.md`'s rule "a new generator parameter gets its name in each of these places" also names the built-in default in `GeneratorParameterList.Defaults`.
  - Remove any statement of the replace rule.
  - Keep each file under its size limit.

- [ ] **Step 3: Check that nothing still says a list replaces**

Run: `git grep -n -i -E "uses it whole|replaces the chain|list.*whole" -- src docs/architecture`
Expected: nothing about generator parameters.

- [ ] **Step 4: Validate and commit**

Run `./pre-commit-validation.sh`, then commit with `git commit -m "docs: the architecture and agent rules of merged generator parameters"`.

### Task 6: The examples

**Files:**
- Modify: one `.sql` file under `examples/PostgresExamples/CodeGen/` and its counterpart under `examples/SqlServerExamples/CodeGen/`, the code that calls them, and each project's `README.md`

- [ ] **Step 1: Show a query that adds to its file header's list,** in each example project:
  - **The example:** the file header writes one parameter and one of its queries writes another, so the README can say the query gets both.
  - **What to pick:** a pair a user would reach for, whose effect the calling code shows, such as `async-method-suffix` in the header and `sort-output` on one query.
  - **What to avoid:** a word that changes a query's SQL. If one is unavoidable, rerun `tools/describe-end-to-end.sh` and read its diff.
  - **Keep it small,** as `examples/AGENTS.md` requires.

- [ ] **Step 2: Build the examples**

Run: `dotnet build examples/PostgresExamples && dotnet build examples/SqlServerExamples`
Expected: no error and no new warning.

- [ ] **Step 3: Validate and commit**

Run `./pre-commit-validation.sh`, then commit with `git commit -m "examples: a query that adds a generator parameter to its file's"`.

### Task 7: Finish the branch

- [ ] **Step 1: Run the full validation**

Run: `./pre-commit-validation.sh --full`
Expected: PASS.

- [ ] **Step 2: Check the version.** Run `git fetch --tags origin` and `git tag --list 'v*' --sort=-v:refname | head -n 1`. If there is a tag and `VersionPrefix` in `Directory.Build.props` is not greater than it, ask the user for the new version.

- [ ] **Step 3: Run the `branch-review:review` skill.** Address its findings and run it again until it comes back clean, as the root `AGENTS.md` requires.
