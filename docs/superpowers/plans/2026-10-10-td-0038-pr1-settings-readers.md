# TD-0038, pull request 1: the readers of settings - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Items 1, 2, 3, 4 and 8 of TD-0038 each get one definition: one reader of a `SettingsLevel`, `SqlSourceDatabase` read through it, the compiler's key prefixes as two constants, the names of `OutputKind` derived from the enum, and one walker of the words of a generator-parameter list.

**Architecture:** `Settings/SettingsLevelReader` holds what the attribute reader and the MSBuild reader share, and each keeps a small struct, an `ISettingSource`, that says how a raw value is found and where an invalid one goes.  Everything else is the removal of a copy in favour of the definition that already exists.  Nothing a user sees changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `netstandard2.0` with Roslyn 4.8.0 for `src/SqlSource`; `net8.0` for `src/SqlSource.Tool`; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md`, the section "Pull request 1".

## Global Constraints

- Nothing a user sees changes: no message, id, exit code, generated file, sidecar, log member or line of `--verbose`.  `README.md`, `docs/diagnostics.md`, `docs/publishing.md` and `CONTRIBUTING.md` are not touched.
- The existing tests pass unchanged, apart from tests of a member that is renamed, moved or removed, which follow it.
- The allocation budgets of `SqlFileParserAllocationTests`, `PathResolverAllocationTests` and `SidecarReaderAllocationTests` do not rise.
- `src/SqlSource` targets `netstandard2.0` and Roslyn 4.8.0: no collection expression may target `ImmutableArray<T>`, and no API newer than those.
- `Settings/` uses nothing of `Parsing/`, `Generation/` or `Diagnostics/`.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, so every analyzer rule applies to the code of this plan as to any other.  Fix what it reports in the code; never suppress.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- An `AGENTS.md` that names a member changes in the commit that renames or removes it.
- Prose in comments and documents follows the repository's style: plain sentences, two spaces after a full stop.
- End each commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Run one test class with:

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.GeneratorParameterListTests"
```

and the tool's with `--project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`.  Run everything with `dotnet test --solution SqlSource.slnx`.

## Review Focus

1. **A project that itself shows `SqlSourceDatabase` to the compiler** (`<CompilerVisibleProperty Include="SqlSourceDatabase" />`), with a value that is no name.  The generator now reads the key: it must give one `InvalidSetting` and set nothing, never throw.  Test in Task 4.
2. **A `-- generator:` line with tabs or runs of spaces between words.**  The span of an error must still be the word's own.  Test in Task 1.
3. **An attribute with several values that are not valid.**  The diagnostics must come in the order they did: `SqlLocation`, `MethodLocation`, then the settings in the attribute reader's order.  Test in Task 4.
4. **`Output = GeneratorOutput.models`, a member in the wrong case, in the tool's attribute reader.**  `Enum.TryParse` can ignore case; the compiler does not.  It must be not set, with no error.  Test in Task 3.
5. **Metadata `SqlSourceDatabase` that is white space, on a file of a project whose property names a database.**  The file must fall back to the property.  Test in Task 5.

## File Structure

| File | Change |
|----|----|
| `src/SqlSource/Settings/GeneratorParameterList.cs` | Gains `Words` and `WordEnumerator`; `Parse` walks with it |
| `src/SqlSource/Parsing/SqlGeneratorParameterScope.cs` | Walks with `Words`; loses `FindWordEnd` and `SkipWhiteSpace` |
| `src/SqlSource/Generation/MSBuild/MSBuildSettings.cs` | Gains the two prefixes, `DatabaseName`, `NameOf`; `Read` goes through the shared reader; `SettingKeys` is indexed by `SettingId` |
| `src/SqlSource/Generation/MSBuild/DialectSetting.cs` | Composes its two keys from the prefixes |
| `src/SqlSource.Tool/Planning/ManifestOptions.cs` | Uses the prefixes of `MSBuildSettings` |
| `src/SqlSource/Settings/SettingValue.cs` | Gains `NameOf<T>` |
| `src/SqlSource.Tool/Planning/AttributeReader.cs` | `TryReadOutput` uses `Enum.TryParse` |
| `src/SqlSource/Settings/SettingId.cs` | New: the settings a source other than a marker can give |
| `src/SqlSource/Settings/ISettingSource.cs` | New: how a raw value is found and where an invalid one goes |
| `src/SqlSource/Settings/SettingsLevelReader.cs` | New: the one reader of a level |
| `src/SqlSource/Generation/TargetTypeReader.cs` | Keeps a source over an `AttributeData`; loses `ReadPlacement`, `ReadSettings`, `ReadParameters`, `ReadText`, `ReadChoice` |
| `src/SqlSource.Tool/Planning/RunPlanner.cs` | Loses `DatabaseSetting`, `ReadDatabase`, `NameOf`; reads the database from the levels |
| `src/SqlSource/AGENTS.md` | The rule for a new setting, and the name that holds the item type |
| `docs/tech-debt/TD-0038-*.md`, `docs/tech-debt/README.md` | Rewritten to what remains |

---

### Task 0: Measure before

Items 1 and 8 touch code that runs for every `.sql` file of every compilation, so the pull request states time and allocation before and after (`src/SqlSource/AGENTS.md`, "Performance is a requirement").  This task changes nothing in the repository.

**Files:**
- Create, outside the repository: `$TMPDIR/sqlsource-measure/SettingsMeasure.cs`

- [ ] **Step 1: Write the measurement, outside the repository**

```bash
mkdir -p "$TMPDIR/sqlsource-measure"
```

`$TMPDIR/sqlsource-measure/SettingsMeasure.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SqlSource.Generation.MSBuild;
using SqlSource.Parsing;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Scratch;

public class SettingsMeasure
{
    private const int Iterations = 200_000;

    [Fact]
    public void Measure()
    {
        var file = new InMemoryAdditionalText("/app/Q/Users.sql", "SELECT 1;");
        var unset = new TestOptionsProvider(null).GetOptions(file);
        var set = new TestOptionsProvider(
            null,
            fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [file.Path] = new Dictionary<string, string>
                {
                    ["SqlSourceOutput"] = "models",
                    ["SqlSourceGeneratorParameters"] = "keep-comments sort-input",
                },
            }
        ).GetOptions(file);
        const string Sql =
            "-- generator: keep-comments sort-input\n-- name: One\n-- generator: no-token-validation\nSELECT 1;\n";

        var lines = new List<string>
        {
            Run("FileSettings.Read, nothing set", () => FileSettings.Read(file.Path, unset)),
            Run("FileSettings.Read, two set", () => FileSettings.Read(file.Path, set)),
            Run("SqlFileParser.Parse, generator markers", () => SqlFileParser.Parse(Sql, "Q.sql")),
        };
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "sqlsource-settings-measure.txt"), lines);
    }

    private static string Run(string name, Func<object?> action)
    {
        for (var iteration = 0; iteration < 20_000; iteration++)
        {
            _ = action();
        }

        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = action();
        }

        watch.Stop();
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name}: {watch.Elapsed.TotalMilliseconds * 1_000_000 / Iterations:F0} ns, {bytes / (double)Iterations:F1} bytes"
        );
    }
}
```

- [ ] **Step 2: Run it on the untouched branch, in Release, and keep the numbers**

```bash
mkdir -p tests/SqlSource.Tests/Scratch && cp "$TMPDIR/sqlsource-measure/SettingsMeasure.cs" tests/SqlSource.Tests/Scratch/
```

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj -c Release --filter-class "SqlSource.Tests.Scratch.SettingsMeasure"
```

```bash
cp "$TMPDIR/sqlsource-settings-measure.txt" "$TMPDIR/sqlsource-measure/before.txt" && cat "$TMPDIR/sqlsource-measure/before.txt"
```

Expected: three lines, each a name, nanoseconds and bytes.  If the build rejects the scratch file for an analyzer rule, fix the scratch file; it is never committed.  Run it three times and keep the run with the lowest times.

- [ ] **Step 3: Remove the scratch file from the repository**

```bash
rm -r tests/SqlSource.Tests/Scratch && git status --short
```

Expected: no output.  Nothing is committed in this task.

---

### Task 1: One walker of words (item 8)

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs`
- Modify: `src/SqlSource/Parsing/SqlGeneratorParameterScope.cs`
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlGeneratorParameterScopeTests.cs`

**Interfaces:**
- Produces: `GeneratorParameterList.Words(ReadOnlySpan<char> text)` returning `GeneratorParameterList.WordEnumerator`, a `ref struct` with `bool MoveNext()`, `ReadOnlySpan<char> Current`, `int Start`, `int Length` and `GetEnumerator()`.

- [ ] **Step 1: Write the failing tests**

Add to `GeneratorParameterListTests`:

```csharp
    [Theory]
    [InlineData("", "")]
    [InlineData(" \t ", "")]
    [InlineData("one", "0:one")]
    [InlineData("  one\ttwo   three ", "2:one 6:two 12:three")]
    [InlineData("a=b  c", "0:a=b 5:c")]
    public void Words_AnyText_GivesEachWordWithWhereItStarts(string text, string expected)
    {
        var found = new List<string>();
        var words = GeneratorParameterList.Words(text.AsSpan());
        while (words.MoveNext())
        {
            words.Length.ShouldBe(words.Current.Length);
            found.Add($"{words.Start}:{words.Current.ToString()}");
        }

        string.Join(" ", found).ShouldBe(expected);
    }

    [Fact]
    public void Words_InAForeach_GivesTheWords()
    {
        var found = new List<string>();
        foreach (var word in GeneratorParameterList.Words(" keep-comments  default ".AsSpan()))
        {
            found.Add(word.ToString());
        }

        found.ShouldBe(["keep-comments", "default"]);
    }
```

Add `using System;` to the file if it is not there.

Add to `SqlGeneratorParameterScopeTests`, for Review Focus 2:

```csharp
    [Fact]
    public void Read_WordsSeparatedByTabsAndRunsOfSpaces_AreEachReportedAtTheirOwnPlace()
    {
        const string Line = "-- generator: keep-comments \t nope   foo=bar";

        var (scope, errors) = Read(Line);

        scope.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(Line, "nope"), "nope"),
            SqlParseError.Create(SqlParseErrorKind.UnknownGeneratorParameter, SpanOf(Line, "foo=bar"), "foo=bar"),
        ]);
    }
```

- [ ] **Step 2: Run them to see the first two fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.GeneratorParameterListTests"`
Expected: the build fails, `'GeneratorParameterList' does not contain a definition for 'Words'`.  (The scope's test passes already: it pins what must not change.)

- [ ] **Step 3: Add the walker and use it in `Parse`**

In `GeneratorParameterList.cs`, add to the `<remarks>` of the class, after its last sentence:

```csharp
/// <para>
/// A <c>-- generator:</c> marker walks the same words, with <see cref="Words" />, and holds them to a rule of its
/// own, in <c>SqlGeneratorParameterScope</c>.  The two rules differ on purpose.  A marker has a position for each
/// word and adds up over several lines, so it reports the one word that conflicts, and a word with <c>=</c> as a
/// value that is not valid.  A property, the metadata of an item and an argument of the attribute have no position
/// inside the value, so <c>default</c> beside another word makes the whole value the thing to quote.
/// </para>
```

and wrap the remarks' existing text in `<para>` and `</para>` so that the two paragraphs are well formed.

Add the walker, after `IsDefault`:

```csharp
    /// <summary>
    /// The words of <paramref name="text" />: each run of characters that are not white space, in order.  Nothing
    /// is allocated.
    /// </summary>
    public static WordEnumerator Words(ReadOnlySpan<char> text) => new(text);
```

and at the end of the class:

```csharp
    /// <summary>Walks the words of a text.  <see cref="Start" /> is an offset into that text.</summary>
    internal ref struct WordEnumerator
    {
        private readonly ReadOnlySpan<char> _text;

        private int _next;

        public WordEnumerator(ReadOnlySpan<char> text)
        {
            _text = text;
            _next = 0;
            Start = 0;
            Length = 0;
        }

        /// <summary>Where the current word starts.</summary>
        public int Start { get; private set; }

        /// <summary>How long the current word is.</summary>
        public int Length { get; private set; }

        public readonly ReadOnlySpan<char> Current => _text.Slice(Start, Length);

        public readonly WordEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var start = _next;
            while (start < _text.Length && char.IsWhiteSpace(_text[start]))
            {
                start++;
            }

            if (start == _text.Length)
            {
                return false;
            }

            var end = start;
            while (end < _text.Length && !char.IsWhiteSpace(_text[end]))
            {
                end++;
            }

            Start = start;
            Length = end - start;
            _next = end;
            return true;
        }
    }
```

Replace the body of `Parse` with:

```csharp
    public static GeneratorParameters? Parse(string? value, List<string> invalid)
    {
        var whole = value.AsSpan().Trim();
        if (whole.IsEmpty)
        {
            return null;
        }

        GeneratorParameters? parameters = null;
        var hasDefault = false;
        var hasOther = false;
        foreach (var word in Words(whole))
        {
            if (IsDefault(word))
            {
                hasDefault = true;
                continue;
            }

            hasOther = true;
            if (TryFind(word, out var parameter))
            {
                parameters = (parameters ?? GeneratorParameters.None) | parameter;
            }
            else
            {
                invalid.Add(word.ToString());
            }
        }

        if (!hasDefault)
        {
            return parameters;
        }

        if (!hasOther)
        {
            return GeneratorParameters.None;
        }

        invalid.Clear();
        invalid.Add(whole.ToString());
        return null;
    }
```

- [ ] **Step 4: Use it in the scope**

In `SqlGeneratorParameterScope.cs`, replace everything in `Read` after the check for an empty value, and delete `FindWordEnd` and `SkipWhiteSpace`:

```csharp
        // A marker's value has no white space around it, so the first word starts where the value does.
        var words = GeneratorParameterList.Words(text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length));
        while (words.MoveNext())
        {
            if (Apply(words.Current) is { } problem)
            {
                errors.Add(
                    SqlParseError.Create(
                        problem,
                        new TextSpan(marker.ValueSpan.Start + words.Start, words.Length),
                        words.Current.ToString()
                    )
                );
            }
        }
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.GeneratorParameterListTests" --filter-class "SqlSource.Tests.Parsing.SqlGeneratorParameterScopeTests" --filter-class "SqlSource.Tests.Parsing.SqlFileParserTests" --filter-class "SqlSource.Tests.Parsing.SqlFileParserAllocationTests"`
Expected: all pass.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

Expected: every line of the summary says `passed`.

```bash
git add -A src tests && git commit -m "Walk the words of a generator-parameter list in one place

GeneratorParameterList.Words serves the list of a property, of metadata and of
the attribute, and the -- generator: marker.  Each keeps its rule for default,
and the remarks say that the two differ on purpose.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The compiler's key prefixes once (item 3)

**Files:**
- Modify: `src/SqlSource/Generation/MSBuild/MSBuildSettings.cs:34-41`
- Modify: `src/SqlSource/Generation/MSBuild/DialectSetting.cs:17-29`
- Modify: `src/SqlSource.Tool/Planning/ManifestOptions.cs`
- Modify: `src/SqlSource/AGENTS.md` (the bullet "The dialect is an input of the parse")
- Test: `tests/SqlSource.Tests/Generation/MSBuild/MSBuildSettingsTests.cs`

**Interfaces:**
- Produces: `MSBuildSettings.PropertyPrefix` (`"build_property."`), `MSBuildSettings.MetadataPrefix` (`"build_metadata.SqlSourceSettingsFile."`), `DialectSetting.Name` (`"SqlSourceDialect"`).

- [ ] **Step 1: Write the failing test**

Add to `MSBuildSettingsTests`:

```csharp
    // The compiler's contract, spelled out: a change to a constant must fail here, not pass by agreeing with itself.
    [Fact]
    public void Prefixes_AreWhereTheCompilerPutsAPropertyAndTheMetadataOfTheSettingsItem()
    {
        MSBuildSettings.PropertyPrefix.ShouldBe("build_property.");
        MSBuildSettings.MetadataPrefix.ShouldBe("build_metadata.SqlSourceSettingsFile.");
        DialectSetting.PropertyName.ShouldBe("build_property.SqlSourceDialect");
        DialectSetting.MetadataName.ShouldBe("build_metadata.SqlSourceSettingsFile.SqlSourceDialect");
    }
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Generation.MSBuild.MSBuildSettingsTests"`
Expected: the build fails, `'MSBuildSettings' does not contain a definition for 'PropertyPrefix'`.

- [ ] **Step 3: Add the constants and compose from them**

In `MSBuildSettings.cs`, add above `GeneratorParametersName`:

```csharp
    /// <summary>Where the compiler puts the properties of the project.</summary>
    public const string PropertyPrefix = "build_property.";

    /// <summary>
    /// Where the compiler puts the metadata of an item of <c>SqlSourceSettingsFile</c>, the item type that
    /// <c>build/SqlSource.targets</c> fills with the <c>AdditionalFiles</c> items that have any.
    /// </summary>
    public const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";
```

and replace the two properties with:

```csharp
    /// <summary>The keys of the project's properties.</summary>
    public static SettingKeys Property { get; } = new(PropertyPrefix);

    /// <summary>The keys of the metadata of a file's item.</summary>
    public static SettingKeys Metadata { get; } = new(MetadataPrefix);
```

In `DialectSetting.cs`, replace the two constants, keeping each one's documentation comment above it:

```csharp
    /// <summary>The name MSBuild knows the setting by.</summary>
    public const string Name = "SqlSourceDialect";

    public const string PropertyName = MSBuildSettings.PropertyPrefix + Name;

    public const string MetadataName = MSBuildSettings.MetadataPrefix + Name;
```

In `ManifestOptions.cs`, delete `PropertyPrefix` and `MetadataPrefix`, add `using SqlSource.Generation.MSBuild;`, and write the two factories as:

```csharp
    public static ManifestOptions ForProject(ProjectManifest manifest) =>
        new(manifest.Properties, MSBuildSettings.PropertyPrefix);

    public static ManifestOptions ForFile(ManifestFile file) => new(file.Metadata, MSBuildSettings.MetadataPrefix);
```

`tests/SqlSource.Tests/Generator/TestOptionsProvider.cs` keeps its own two literals: its comment says why.

- [ ] **Step 4: Correct `src/SqlSource/AGENTS.md`**

In the bullet that starts "The dialect is an input of the parse", replace the sentence

> The keys in `DialectSetting.MetadataName` and `MSBuildSettings.Metadata` hold the name of that item type.

with

> `MSBuildSettings.MetadataPrefix` holds the name of that item type, and `MSBuildSettings.PropertyPrefix` where a property is; every key is composed from one of the two, in the generator and in the tool's `ManifestOptions`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Spell the compiler's two key prefixes once, in MSBuildSettings

DialectSetting and the tool's ManifestOptions compose their keys from them.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The names of `OutputKind` once (item 4)

**Files:**
- Modify: `src/SqlSource/Settings/SettingValue.cs`
- Modify: `src/SqlSource.Tool/Planning/AttributeReader.cs:166-188`
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (the two calls of `NameOf`, and `NameOf` itself)
- Test: `tests/SqlSource.Tests/Settings/SettingValueTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`

**Interfaces:**
- Produces: `SettingValue.NameOf<T>(T choice) where T : struct, Enum`, returning the member's name in lower case.

- [ ] **Step 1: Write the failing tests**

Add to `SettingValueTests`:

```csharp
    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the member's name.
    [InlineData("Sql", "sql")]
    [InlineData("Models", "models")]
    [InlineData("CodeGen", "codegen")]
    public void NameOf_Member_IsItsNameInLowerCaseWhichTryReadChoiceReads(string member, string expected)
    {
        var output = Enum.Parse<OutputKind>(member);

        SettingValue.NameOf(output).ShouldBe(expected);
        SettingValue.TryReadChoice<OutputKind>(expected.AsSpan(), out var read).ShouldBeTrue();
        read.ShouldBe(output);
    }

    [Fact]
    public void NameOf_NumberThatIsNoMember_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SettingValue.NameOf((OutputKind)99));
```

In `AttributeReaderTests.Read_OutputAsAMember_IsThatMember`, add two rows after the row for `GeneratorOutput.Other`, for Review Focus 4:

```csharp
    // The compiler compares the case, so the reader does.
    [InlineData("GeneratorOutput.models", null)]
    [InlineData("GeneratorOutput.CODEGEN", null)]
```

- [ ] **Step 2: Run them to see the first fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.SettingValueTests"`
Expected: the build fails, `'SettingValue' does not contain a definition for 'NameOf'`.

- [ ] **Step 3: Add `NameOf`**

In `SettingValue.cs`, after `TryReadChoice`:

```csharp
    /// <summary>
    /// The name of a member of <typeparamref name="T" /> in lower case, as a message names a choice.
    /// <see cref="TryReadChoice{T}" /> reads it back.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="choice" /> is no member.</exception>
    public static string NameOf<T>(T choice)
        where T : struct, Enum
    {
        var index = Array.IndexOf(Choices<T>.Values, choice);
        return index < 0 ? throw new ArgumentOutOfRangeException(nameof(choice)) : Choices<T>.LowerNames[index];
    }
```

and in `Choices<T>`, after `Names`:

```csharp
        public static readonly string[] LowerNames = Array.ConvertAll(Names, Lower);

        // Character by character: the build runs every analyzer rule, and CA1308 rejects string.ToLowerInvariant.
        private static string Lower(string name)
        {
            var characters = name.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = char.ToLowerInvariant(characters[index]);
            }

            return new string(characters);
        }
```

- [ ] **Step 4: Use the enum in the tool**

In `AttributeReader.TryReadOutput`, replace the `switch` expression with:

```csharp
        // An identifier is never a number or a list, the two other things Enum.TryParse reads.
        output = Enum.TryParse<OutputKind>(access.Name.Identifier.ValueText, ignoreCase: false, out var member)
            ? member
            : null;
        return true;
```

In `RunPlanner.cs`, delete the method `NameOf` with its comment, and write its two calls as `SettingValue.NameOf(output)`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass, `Plan_TwoClaimsWithTwoOutputs_NameTheGreaterInAMessage` among them: it holds the text `models` and `codegen` of `SQLSRC209` and `SQLSRC210`.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Take the names of an output from the enum, in the tool as in the generator

AttributeReader reads a member with Enum.TryParse, and a message names one
with SettingValue.NameOf.  A new member of OutputKind needs no line in the tool.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: One reader of a level (item 1, and the reader's half of item 2)

**Files:**
- Create: `src/SqlSource/Settings/SettingId.cs`
- Create: `src/SqlSource/Settings/ISettingSource.cs`
- Create: `src/SqlSource/Settings/SettingsLevelReader.cs`
- Modify: `src/SqlSource/Generation/MSBuild/MSBuildSettings.cs`
- Modify: `src/SqlSource/Generation/MSBuild/InvalidSetting.cs` (documentation)
- Modify: `src/SqlSource/Settings/SettingsLevel.cs` (documentation of `Database`)
- Modify: `src/SqlSource/Generation/TargetTypeReader.cs`
- Modify: `src/SqlSource/AGENTS.md` (the bullet "`SettingsLevel` is one source's say")
- Create: `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`
- Test: `tests/SqlSource.Tests/Generation/MSBuild/MSBuildSettingsTests.cs`
- Test: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `MSBuildSettings.PropertyPrefix`, `MSBuildSettings.MetadataPrefix` (Task 2); `GeneratorParameterList.Parse` (Task 1).
- Produces:
  - `enum SettingId { Parameters, Output, Database, InputModelSuffix, OutputModelSuffix, ModelNamespace, InputModelType, OutputModelType, CollectionType }`
  - `interface ISettingSource { string? Text(SettingId setting); T? Choice<T>(SettingId setting) where T : struct, Enum; void Invalid(SettingId setting, string value); }`
  - `SettingsLevelReader.Read<TSource>(ref TSource source) where TSource : struct, ISettingSource`, returning `SettingsLevel`
  - `MSBuildSettings.DatabaseName` (`"SqlSourceDatabase"`), `MSBuildSettings.NameOf(SettingId)`; `MSBuildSettings.Read` keeps its signature and now sets `SettingsLevel.Database`.

- [ ] **Step 1: Write the failing tests of the reader**

`tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public class SettingsLevelReaderTests
{
    [Fact]
    public void Read_SourceThatHasNothing_IsTheSharedEmptyLevel()
    {
        var source = new FakeSource([]);

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBeEmpty();
    }

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the setting's name.
    [InlineData("Database", " main ", "main")]
    [InlineData("InputModelSuffix", " Args ", "Args")]
    [InlineData("OutputModelSuffix", "Row", "Row")]
    [InlineData("ModelNamespace", " App.Models ", "App.Models")]
    public void Read_TextThatIsValid_IsSetWithoutTheWhiteSpaceAroundIt(string setting, string written, string expected)
    {
        var id = Enum.Parse<SettingId>(setting);
        var source = new FakeSource(new Dictionary<SettingId, string> { [id] = written });

        var level = SettingsLevelReader.Read(ref source);

        TextOf(level, id).ShouldBe(expected);
        source.Invalids.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Database", " not a name ", "not a name")]
    [InlineData("InputModelSuffix", "a-b", "a-b")]
    [InlineData("OutputModelSuffix", " a b ", "a b")]
    [InlineData("ModelNamespace", "App..Models", "App..Models")]
    public void Read_TextThatIsNotValid_IsRecordedTrimmedAndNotSet(string setting, string written, string recorded)
    {
        var id = Enum.Parse<SettingId>(setting);
        var source = new FakeSource(new Dictionary<SettingId, string> { [id] = written });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBe([(id, recorded)]);
    }

    [Theory]
    [InlineData("Database")]
    [InlineData("InputModelSuffix")]
    [InlineData("OutputModelSuffix")]
    [InlineData("ModelNamespace")]
    [InlineData("Parameters")]
    public void Read_TextThatIsWhiteSpace_IsNotSetAndNotRecorded(string setting)
    {
        var source = new FakeSource(new Dictionary<SettingId, string> { [Enum.Parse<SettingId>(setting)] = " \t " });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBeEmpty();
    }

    [Fact]
    public void Read_Parameters_AreParsedAndEachWordThatIsNoneIsRecorded()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.Parameters] = " keep-comments nope sort-input other " }
        );

        SettingsLevelReader
            .Read(ref source)
            .Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.SortInput);
        source.Invalids.ShouldBe([(SettingId.Parameters, "nope"), (SettingId.Parameters, "other")]);
    }

    [Fact]
    public void Read_Choices_AreWhatTheSourceDecodes()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string>
            {
                [SettingId.Output] = "Models",
                [SettingId.InputModelType] = "Record",
                [SettingId.OutputModelType] = "SealedClass",
                [SettingId.CollectionType] = "ImmutableArray",
            }
        );

        SettingsLevelReader
            .Read(ref source)
            .ShouldBe(
                new SettingsLevel
                {
                    Output = OutputKind.Models,
                    InputModelType = ModelKind.Record,
                    OutputModelType = ModelKind.SealedClass,
                    CollectionType = CollectionKind.ImmutableArray,
                }
            );
    }

    [Fact]
    public void Read_EverySetting_IsAskedForInTheOrderOfTheEnum()
    {
        var source = new FakeSource([]);

        _ = SettingsLevelReader.Read(ref source);

        source.Asked.ShouldBe(Enum.GetValues<SettingId>());
    }

    private static string? TextOf(SettingsLevel level, SettingId setting) =>
        setting switch
        {
            SettingId.Database => level.Database,
            SettingId.InputModelSuffix => level.InputModelSuffix,
            SettingId.OutputModelSuffix => level.OutputModelSuffix,
            SettingId.ModelNamespace => level.ModelNamespace,
            _ => throw new ArgumentOutOfRangeException(nameof(setting)),
        };

    // A source over a dictionary.  A choice is written as the member's name.
    private struct FakeSource(Dictionary<SettingId, string> texts) : ISettingSource
    {
        public List<(SettingId Setting, string Value)> Invalids { get; } = [];

        public List<SettingId> Asked { get; } = [];

        public readonly string? Text(SettingId setting)
        {
            Asked.Add(setting);
            return texts.TryGetValue(setting, out var text) ? text : null;
        }

        public readonly T? Choice<T>(SettingId setting)
            where T : struct, Enum
        {
            Asked.Add(setting);
            return texts.TryGetValue(setting, out var text) && Enum.TryParse<T>(text, out var choice) ? choice : null;
        }

        public readonly void Invalid(SettingId setting, string value) => Invalids.Add((setting, value));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.SettingsLevelReaderTests"`
Expected: the build fails, `The type or namespace name 'SettingId' could not be found`.

- [ ] **Step 3: Write the three files of `Settings/`**

`src/SqlSource/Settings/SettingId.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// The settings of a <see cref="SettingsLevel" /> that a source other than a marker can give, in the order
/// <see cref="SettingsLevelReader" /> reads them.  A source maps each to its own name for it.
/// </summary>
internal enum SettingId
{
    Parameters,
    Output,
    Database,
    InputModelSuffix,
    OutputModelSuffix,
    ModelNamespace,
    InputModelType,
    OutputModelType,
    CollectionType,
}
```

`src/SqlSource/Settings/ISettingSource.cs`:

```csharp
using System;

namespace SqlSource.Settings;

/// <summary>
/// One place that settings are written in, other than a marker: the attribute of a type, the properties of a
/// project, the metadata of a file's item.  It says how a raw value is found and where one that is not valid goes;
/// <see cref="SettingsLevelReader" /> says everything else.
/// </summary>
/// <remarks>
/// An implementation is a struct, so that the reader, which is generic over it, boxes nothing.
/// </remarks>
internal interface ISettingSource
{
    /// <summary>The text as written, or null when the source does not have the setting or does not set it.</summary>
    string? Text(SettingId setting);

    /// <summary>
    /// The choice, or null when it is not set.  A value that is no member is recorded by the source, which alone
    /// knows how it was written: a number in an attribute, a word in a property.
    /// </summary>
    T? Choice<T>(SettingId setting)
        where T : struct, Enum;

    /// <summary>Records a value that is not valid.  The setting is then not set.</summary>
    void Invalid(SettingId setting, string value);
}
```

`src/SqlSource/Settings/SettingsLevelReader.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// Reads a <see cref="SettingsLevel" /> from a source that is not a marker.  This is the one place that says which
/// members such a level has, what makes each valid, and that a text which is empty or white space is not set.
/// </summary>
internal static class SettingsLevelReader
{
    private delegate bool Validator(ReadOnlySpan<char> value);

    /// <summary>
    /// Reads one level.  A value that is not valid is given to the source and is not set, and every setting is
    /// still read, so that all the problems of a source are found together.  The shared empty level is returned
    /// when nothing is set.
    /// </summary>
    public static SettingsLevel Read<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        // In the order of SettingId: a source reports what is not valid in the order it is asked.
        var level = new SettingsLevel
        {
            Parameters = ReadParameters(ref source),
            Output = source.Choice<OutputKind>(SettingId.Output),
            Database = ReadText(ref source, SettingId.Database, SettingValue.IsDatabaseName),
            InputModelSuffix = ReadText(ref source, SettingId.InputModelSuffix, SettingValue.IsSuffix),
            OutputModelSuffix = ReadText(ref source, SettingId.OutputModelSuffix, SettingValue.IsSuffix),
            ModelNamespace = ReadText(ref source, SettingId.ModelNamespace, SettingValue.IsNamespace),
            InputModelType = source.Choice<ModelKind>(SettingId.InputModelType),
            OutputModelType = source.Choice<ModelKind>(SettingId.OutputModelType),
            CollectionType = source.Choice<CollectionKind>(SettingId.CollectionType),
        };

        // A source that sets nothing shares the empty level.
        return level.Equals(SettingsLevel.None) ? SettingsLevel.None : level;
    }

    // A word that is no generator parameter is recorded, and the other words apply.
    private static GeneratorParameters? ReadParameters<TSource>(ref TSource source)
        where TSource : struct, ISettingSource
    {
        if (source.Text(SettingId.Parameters) is not { } list || string.IsNullOrWhiteSpace(list))
        {
            return null;
        }

        var words = new List<string>();
        var parameters = GeneratorParameterList.Parse(list, words);
        foreach (var word in words)
        {
            source.Invalid(SettingId.Parameters, word);
        }

        return parameters;
    }

    private static string? ReadText<TSource>(ref TSource source, SettingId setting, Validator isValid)
        where TSource : struct, ISettingSource
    {
        if (source.Text(setting) is not { } written || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (isValid(value.AsSpan()))
        {
            return value;
        }

        source.Invalid(setting, value);
        return null;
    }
}
```

- [ ] **Step 4: Run the reader's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.SettingsLevelReaderTests"`
Expected: all pass.

- [ ] **Step 5: Write the failing tests of the MSBuild source**

Add to `MSBuildSettingsTests`:

```csharp
    // The names line up with SettingId: the keys are found by its number.
    [Fact]
    public void NameOf_EverySetting_IsItsMSBuildName()
    {
        foreach (var setting in Enum.GetValues<SettingId>())
        {
            MSBuildSettings
                .NameOf(setting)
                .ShouldBe(
                    setting == SettingId.Parameters ? "SqlSourceGeneratorParameters" : "SqlSource" + setting
                );
        }
    }

    // The package does not show the compiler SqlSourceDatabase, so the generator never has it.  A project that
    // shows it itself gets the tool's rule for the value.
    [Theory]
    [InlineData(" main ", "main", null)]
    [InlineData("not a name", null, "not a name")]
    [InlineData("   ", null, null)]
    public void ReadProject_Database_IsReadAsAName(string written, string? expected, string? invalid)
    {
        var settings = ProjectSettings.Read(Provider("SqlSourceDatabase", written).GlobalOptions);

        settings.Level.Database.ShouldBe(expected);
        settings.Invalid.ShouldBe(invalid is null ? [] : [new InvalidSetting("SqlSourceDatabase", invalid)]);
    }

    [Fact]
    public void ReadFile_Database_IsReadAsAName()
    {
        var settings = ReadFileMetadata(new Dictionary<string, string> { ["SqlSourceDatabase"] = "files" });

        settings.ShouldNotBeNull().Level.ShouldBe(new SettingsLevel { Database = "files" });
        settings.Invalid.ShouldBeEmpty();
    }
```

Add `using System;` if it is not there.  `Provider(name, value)` and `ReadFileMetadata(dictionary)` are the helpers the file already has; read them before use and match their signatures.

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Generation.MSBuild.MSBuildSettingsTests"`
Expected: the build fails, `'MSBuildSettings' does not contain a definition for 'NameOf'`.

- [ ] **Step 7: Make `MSBuildSettings` a source**

In `MSBuildSettings.cs`:

Add the name, after `OutputName`:

```csharp
    /// <summary>
    /// The setting that the tool reads and the generator does not: <c>build/SqlSource.props</c> does not show it to
    /// the compiler.
    /// </summary>
    public const string DatabaseName = "SqlSourceDatabase";
```

Add, below the last `...Name` constant and **above** `Property` and `Metadata`, since static members are initialised in the order they are written and the two read it:

```csharp
    // The names, in the order of SettingId.
    private static readonly string[] Names =
    [
        GeneratorParametersName,
        OutputName,
        DatabaseName,
        InputModelSuffixName,
        OutputModelSuffixName,
        ModelNamespaceName,
        InputModelTypeName,
        OutputModelTypeName,
        CollectionTypeName,
    ];
```

Replace `Read`, `ReadParameters`, the delegate `Validator`, `ReadText`, `ReadChoice` and the class `SettingKeys` with:

```csharp
    /// <summary>The name MSBuild knows a setting by, without a prefix.</summary>
    public static string NameOf(SettingId setting) => Names[(int)setting];

    /// <summary>
    /// Reads one level.  A value that is not valid is added to <paramref name="invalid" /> and is not set.  The
    /// shared empty level is returned when nothing is set.
    /// </summary>
    public static SettingsLevel Read(AnalyzerConfigOptions options, SettingKeys keys, ref List<InvalidSetting>? invalid)
    {
        var source = new Source(options, keys, invalid);
        var level = SettingsLevelReader.Read(ref source);
        invalid = source.InvalidSettings;
        return level;
    }

    // The options the compiler gives for the project or for one file, under the keys of one prefix.
    private struct Source(AnalyzerConfigOptions options, SettingKeys keys, List<InvalidSetting>? invalid)
        : ISettingSource
    {
        public List<InvalidSetting>? InvalidSettings { get; private set; } = invalid;

        public readonly string? Text(SettingId setting) =>
            options.TryGetValue(keys[setting], out var value) ? value : null;

        public T? Choice<T>(SettingId setting)
            where T : struct, Enum
        {
            if (Text(setting) is not { } written || string.IsNullOrWhiteSpace(written))
            {
                return null;
            }

            if (SettingValue.TryReadChoice<T>(written.AsSpan().Trim(), out var choice))
            {
                return choice;
            }

            Invalid(setting, written.Trim());
            return null;
        }

        public void Invalid(SettingId setting, string value) =>
            (InvalidSettings ??= []).Add(new InvalidSetting(NameOf(setting), value));
    }

    /// <summary>The keys of the settings under one prefix, built once.</summary>
    internal sealed class SettingKeys(string prefix)
    {
        private readonly string[] _keys = Array.ConvertAll(Names, name => prefix + name);

        public string this[SettingId setting] => _keys[(int)setting];
    }
```

In `InvalidSetting.cs`, the documentation of `Name` becomes:

```csharp
/// <param name="Name">
/// The name MSBuild knows the setting by, as <c>SqlSourceGeneratorParameters</c>: one of the names of
/// <see cref="MSBuildSettings" />.
/// </param>
```

In `SettingsLevel.cs`, the documentation of `Database` becomes:

```csharp
    /// <summary>
    /// The name of the database a query belongs to.  The generator carries it from a marker and never uses it; the
    /// tool does.  The compiler is not shown the property or the metadata, so only the tool has those two levels.
    /// </summary>
```

- [ ] **Step 8: Run the MSBuild tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Generation.MSBuild.MSBuildSettingsTests" --filter-class "SqlSource.Tests.Generation.MSBuild.DialectSettingTests"`
Expected: all pass.

- [ ] **Step 9: Write the test of the attribute's order, for Review Focus 3**

Add to `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`.  The folder is compiled for Roslyn 4.8.0 too, so the source handed to the compiler stays within C# 12:

```csharp
    // Each is reported where the attribute reader reads it: the location, the method location, then the settings in
    // the order of SettingId.
    [Fact]
    public void Run_AttributeWithSeveralValuesThatAreNotValid_ReportsThemInTheOrderTheyAreRead()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(
                CollectionType = (GeneratorCollectionType)91,
                ModelNamespace = "App..Models",
                Output = (GeneratorOutput)92,
                Parameters = "nope",
                MethodLocation = (MethodLocation)93,
                SqlLocation = (SqlLocation)94)]
            public partial class Sample;
            """,
            Users
        );

        run.Diagnostics.Select(static diagnostic => diagnostic.Substring(diagnostic.IndexOf("): ", StringComparison.Ordinal) + 3))
            .ShouldBe([
                "'94' is not a valid value of SqlLocation",
                "'93' is not a valid value of MethodLocation",
                "'nope' is not a valid value of Parameters",
                "'92' is not a valid value of Output",
                "'App..Models' is not a valid value of ModelNamespace",
                "'91' is not a valid value of CollectionType",
            ]);
    }
```

Add `using System;` and `using System.Linq;` if they are not there.

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Generator.TypeDiagnosticsTests"`
Expected: PASS.  It pins the order before the attribute reader changes.  If it fails on the text of a line or on how the harness formats a diagnostic, correct the expectation to what the current code gives, and change nothing else: the test must pass before Step 10 and after it.

- [ ] **Step 10: Make `TargetTypeReader` a source**

In `TargetTypeReader.Read`, make the source before the declarations are read, and read through it:

```csharp
        var attributeLocation = LocationInfo.From(reference.GetSyntax(cancellationToken).GetLocation());
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var source = new AttributeSettings(attribute, attributeLocation, diagnostics);
        var types = ReadDeclarations(declaration, diagnostics);
        var placement = source.Choice<MemberPlacement>(AttributeSource.LocationProperty) ?? MemberPlacement.Nested;
        if (placement == MemberPlacement.Nested && IsNestedClassNameTaken(symbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(SqlDiagnostics.SqlMemberExists, attributeLocation, symbol.Name));
        }

        return new TargetType(
            symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(NamespaceFormat),
            new EquatableArray<TypeDeclaration>(types),
            placement,
            source.Choice<MethodPlacement>(AttributeSource.MethodLocationProperty) ?? MethodPlacement.ExtensionClass,
            SettingsLevelReader.Read(ref source),
            ReadPath(attribute),
            reference.SyntaxTree.FilePath,
            attributeLocation,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
```

Delete `ReadPlacement`, `ReadSettings`, `ReadParameters`, `ReadText` and `ReadChoice`, with their comments.  Keep `ReadPath` and `GetNamedArgument`.  Add, above `GetNamedArgument`:

```csharp
    // The attribute as a source of settings.  A word that is no generator parameter is reported, and a type with a
    // diagnostic gets no members, so nothing of this level applies then; every property is still read, so that all
    // the problems of the attribute are reported together.
    private readonly struct AttributeSettings(
        AttributeData attribute,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    ) : ISettingSource
    {
        // A string that is empty or white space is not set, as Path is: the reader of the level says so.
        public string? Text(SettingId setting) =>
            PropertyOf(setting) is { } property && GetNamedArgument(attribute, property) is { Value: string written }
                ? written
                : null;

        public T? Choice<T>(SettingId setting)
            where T : struct, Enum => PropertyOf(setting) is { } property ? Choice<T>(property) : null;

        // An enum argument arrives as its number.  A number that is no member of the generator's own form of the
        // enum was written with a cast, and is reported.  An argument that is not a constant has no value here; the
        // compiler reports it.
        public T? Choice<T>(string property)
            where T : struct, Enum
        {
            if (GetNamedArgument(attribute, property) is not { Value: int value })
            {
                return null;
            }

            if (Enum.IsDefined(typeof(T), value))
            {
                return (T)Enum.ToObject(typeof(T), value);
            }

            Report(property, value.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        public void Invalid(SettingId setting, string value)
        {
            if (PropertyOf(setting) is { } property)
            {
                Report(property, value);
            }
        }

        private void Report(string property, string value) =>
            diagnostics.Add(
                DiagnosticInfo.Create(SqlDiagnostics.InvalidAttributeValue, attributeLocation, value, property)
            );

        // The attribute has no property for the database.
        private static string? PropertyOf(SettingId setting) =>
            setting switch
            {
                SettingId.Parameters => AttributeSource.ParametersProperty,
                SettingId.Output => AttributeSource.OutputProperty,
                SettingId.InputModelSuffix => AttributeSource.InputModelSuffixProperty,
                SettingId.OutputModelSuffix => AttributeSource.OutputModelSuffixProperty,
                SettingId.ModelNamespace => AttributeSource.ModelNamespaceProperty,
                SettingId.InputModelType => AttributeSource.InputModelTypeProperty,
                SettingId.OutputModelType => AttributeSource.OutputModelTypeProperty,
                SettingId.CollectionType => AttributeSource.CollectionTypeProperty,
                _ => null,
            };
    }
```

`SettingsLevelReader.Read(ref source)` needs `source` to be a variable, not `readonly`-declared with `in`; a `readonly struct` passed by `ref` is fine.  Remove a `using` the file no longer needs if the build says so.

- [ ] **Step 11: Correct `src/SqlSource/AGENTS.md`**

In the section `Settings/`, replace the sentence

> A new setting is a nullable member of the level, a line in `Over`, a member of `QuerySettings` with its default, and a line in each reader that has it.

with

> A new setting is a nullable member of the level, a line in `Over`, a member of `QuerySettings` with its default, and a branch in `SqlMarkerScope.Read` when a marker gives it.  When another source gives it too, it is a member of `SettingId`, a line in `SettingsLevelReader.Read`, which is the one reader of the attribute, the properties and the metadata, a name in `MSBuildSettings`, in the order of `SettingId`, and a property in the `AttributeSettings` of `TargetTypeReader`.  A source is an `ISettingSource`: it says how a raw value is found and where one that is not valid goes, and nothing else.

In the pipeline section's bullet "The settings have two stages", the phrase "both through `Generation/MSBuild/MSBuildSettings.cs`, `TargetTypeReader` the attribute" becomes "both through `Generation/MSBuild/MSBuildSettings.cs`, `TargetTypeReader` the attribute, all three with `Settings/SettingsLevelReader.cs`".

- [ ] **Step 12: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass, with the test of Step 9, `GeneratorParityTests`, `CachingTests` and the three allocation tests among them.

- [ ] **Step 13: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Read a settings level in one place, from the attribute and from MSBuild

SettingsLevelReader holds the members, their validators and the rule for a
text.  TargetTypeReader and MSBuildSettings each keep a source: how a raw value
is found and where an invalid one goes.  The reader knows SqlSourceDatabase,
which the compiler is not shown, so the generator reads nothing new.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `SqlSourceDatabase` travels with the other settings (item 2)

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs:209-243`

**Interfaces:**
- Consumes: `MSBuildSettings.DatabaseName`, and `SettingsLevel.Database` as `ProjectSettings.Read` and `FileMetadata.Read` now set it (Task 4).

- [ ] **Step 1: Add the row of Review Focus 5**

In `RunPlannerTests.Plan_DatabaseFromEachLevel_IsTakenFromTheMostSpecificAndElseIsTheDialectsName`, add after the row `("main", "files", null, null, "files")`:

```csharp
    // Metadata that is white space is not set.
    [InlineData("main", "  ", null, null, "main")]
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Planning.RunPlannerTests"`
Expected: PASS.  It pins what must not change.  If the namespace of the class differs, take it from the top of the file.

- [ ] **Step 2: Read the database from the levels**

In `RunPlanner.cs`:

Delete the constant `DatabaseSetting` with its comment, and the method `ReadDatabase`.

In `ReadProject`, delete the line `var database = ReadDatabase(manifest.Properties, invalidSettings);`, and in the `new ClaimedFile(...)` delete the argument `ReadDatabase(file.Metadata, invalidSettings) ?? database,`.

`IsRead` becomes:

```csharp
    // The generator's reader gives every setting that is not valid.  The tool reports the ones it reads: the build
    // reports the rest.  The database is the tool's alone: the compiler is not shown it.
    private static bool IsRead(InvalidSetting setting) =>
        setting.Name is MSBuildSettings.OutputName or MSBuildSettings.DatabaseName;
```

In the record `ClaimedFile`, delete the member `string? Database,`.

In `PlanFile`, the database of a planned query becomes:

```csharp
                    query.Markers.Database
                        ?? owner.Metadata.Settings?.Level.Database
                        ?? owner.Property.Database
                        ?? SqlDialectName.Canonical(parsed.Dialect),
```

- [ ] **Step 3: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: all pass.  These hold the change: `Plan_DatabaseFromEachLevel_IsTakenFromTheMostSpecificAndElseIsTheDialectsName`, `Plan_OutputAndDatabaseThatAreNotValid_AreSqlsrc014AtTheProjectAndAreNotSet`, `Plan_MetadataThatIsNotValid_IsSqlsrc014ForAClaimedFileAlone` and `Plan_SettingTheToolDoesNotRead_IsNotReportedThoughItIsNotValid`.

- [ ] **Step 4: Check that no file names what is gone**

```bash
grep -rnE 'DatabaseSetting|ReadDatabase|RunPlanner\.NameOf' src tests docs/tech-debt --include='*.cs' --include='*.md'
```

Expected: no line outside `docs/superpowers/` and `docs/tech-debt/TD-0038-*.md`, which Task 6 rewrites.

- [ ] **Step 5: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Read SqlSourceDatabase through the reader of the other settings

RunPlanner takes a file's database from its metadata level and the project's
from its property level, and drops its own reader of the name.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Measure after, rewrite TD-0038, open the pull request

**Files:**
- Modify: `docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0038)
- Add: `docs/superpowers/plans/2026-10-10-td-0038-pr1-settings-readers.md` (this file)

- [ ] **Step 1: Measure after**

```bash
mkdir -p tests/SqlSource.Tests/Scratch && cp "$TMPDIR/sqlsource-measure/SettingsMeasure.cs" tests/SqlSource.Tests/Scratch/
```

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj -c Release --filter-class "SqlSource.Tests.Scratch.SettingsMeasure"
```

```bash
cp "$TMPDIR/sqlsource-settings-measure.txt" "$TMPDIR/sqlsource-measure/after.txt" && rm -r tests/SqlSource.Tests/Scratch && paste -d'\n' "$TMPDIR/sqlsource-measure/before.txt" "$TMPDIR/sqlsource-measure/after.txt"
```

Expected: the bytes of each line are equal to before or lower.  `FileSettings.Read` does one more lookup, for the database, so its time may rise by a few nanoseconds; say so in the pull request.  If the bytes rose, stop: find what allocates, most likely a source that was boxed or a delegate that is made at each call, and fix it before going on.  Run three times, as before.

- [ ] **Step 2: Rewrite TD-0038 to what remains**

In the item's file, keeping the numbers of the items that stay:

- Under **Problem**, delete the items 1, 2, 3, 4 and 8, and add after the first paragraph: `Items 1 to 4 and 8 of the review are resolved; the numbers of the others are kept, since the design names them.`
- Under **Why it exists**, the last sentence becomes: `Items 5 and 6 came from each step defining the record it needed; 7 from readers that were written to allocate nothing and copied the one way to do that.`
- Under **Impact**, the second and third sentences become: `Nothing here is wrong today, and the tests hold each pair together where they could drift.  Each item makes a change touch more places than a reader would expect, and 6 is the kind that a later phase breaks by adding one case in one of the copies.`
- Under **Proposed fix**, delete the entries 1, 2, 3, 4 and 8, and the first line becomes: `As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull requests 2 to 4:`
- Under **Trigger**: `Before phase 3 adds the first describer, which adds cases to 5 and 6.`

In `docs/tech-debt/README.md`, the description of the row becomes:

> Duplicated and uneven patterns in `src/`: twin result records and five shapes of a result that may fail, `ToolDiagnostic.ForFile` taking the path twice, duplicated text helpers in `Parsing/`, methods of a hundred lines, boolean cascades, and dead or forwarding members

The status stays `Open`.

- [ ] **Step 3: Validate and commit the documents**

```bash
./pre-commit-validation.sh
```

```bash
git add -A docs && git commit -m "Rewrite TD-0038 to what remains after the readers of settings, and add the plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as on 2026-10-10: the repository has no release tag, so there is nothing to compare.  If a tag is printed, compare it with `VersionPrefix` in `Directory.Build.props`, and ask the user for the new version if `VersionPrefix` is not greater.

- [ ] **Step 5: Ask the user, then push and open the pull request**

Pushing and opening a pull request are seen outside this machine: ask the user first.  Then:

```bash
git push -u origin claude/td-0038-settings-readers
```

Open the pull request against `main` with `gh pr create`.  Its description says: what TD-0038 is and that this is the first of four pull requests of the design; the five items resolved, one line each; that nothing a user sees changes; the table of time and allocation before and after, from Step 1; and that TD-0038 is rewritten, not deleted.  End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Then follow the repository's rules for a pull request: reply in its thread to every CodeRabbit finding, with what changed or with why not.
