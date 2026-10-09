# Query generation, phase 2.1: the snapshot format - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the generator assembly a model of a sidecar, a hand-written reader and writer for it, and the two comparisons the format design defines, so that the tool and the generator share one implementation of the format.

**Architecture:** A new folder, `src/SqlSource/Snapshot/`, holds records with value equality, a tokenizer of JSON that allocates nothing, a reader in two passes (the grammar, then the format version and the model), a writer that follows the format's layout, and a comparer.  The first pass proves the text is one JSON object nested at most 64 levels, so the second walks it without checking grammar again.  Nothing in the generator's pipeline changes.

**Tech Stack:** C# on `netstandard2.0` (Roslyn 4.8.0 floor) with no JSON library; xunit v3 on Microsoft.Testing.Platform, Shouldly, Bogus 35.6.5 and JsonSchema.Net 9.4.0 in the test project only.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-1-snapshot-format-design.md`](../specs/2026-10-09-query-generation-phase-2-1-snapshot-format-design.md).  Read it first; this plan argues from it.  The contract it implements is the [sidecar format design](../specs/2026-10-07-sidecar-format-design.md): read its sections 1 to 3 as well.

## Global Constraints

- Branch: `claude/query-generation-phase-2-1-snapshot-format`.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`.  Run `./format.sh`, then the validation as its own command, fix what it reports, then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it.
- `src/SqlSource` targets `netstandard2.0` only, may use no Roslyn API newer than 4.8.0, and takes no new package reference.  No JSON library: `System.Text.Json` appears in the test project alone.
- No collection expression may target `ImmutableArray<T>`.  A model is a record that holds its collections in `EquatableArray<T>`.
- `Snapshot/` uses `EquatableArray<T>` and `TextSpan` and nothing of `Parsing/`, `Generation/`, `Settings/` or `Diagnostics/`.
- The reader never throws, whatever text it is given.  The writer never throws, whatever model it is given.
- Nothing in the pipeline changes: `SqlSourceGenerator.cs` and `Generation/` are not edited, and the allocation budgets in `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` and `tests/SqlSource.Tests/Generation/PathResolverAllocationTests.cs` and the tests in `tests/SqlSource.Tests/Generator/CachingTests.cs` are untouched.
- This sub-phase adds no diagnostic descriptor: `SqlDiagnostics.cs`, `AnalyzerReleases.Unshipped.md` and `docs/diagnostics.md` are not edited.
- The build treats every analyzer finding as an error (`AnalysisLevel` `latest-all`, Sonar, Roslynator).  The code below was written to those rules and was not compiled against them.  When a rule objects, change the code to satisfy it, split a method that is too complex, and never suppress a rule or edit `.editorconfig` to silence one.  If a rule cannot be satisfied without changing an interface that a later task consumes, stop and ask the owner.
- No line of a file is longer than 120 characters, apart from the files `.editorconfig` exempts: `tools/editorconfig-checker.sh` rejects one.  `./format.sh` wraps code and never breaks a string, and some strings in the tests below are longer than a line allows as written here: break such a string with `+`, at a comma of the JSON it holds.
- Tests in `tests/SqlSource.Tests/Snapshot/` are not compiled into `tests/SqlSource.Tests.RoslynFloor` and may use any C# the SDK has.
- Files under `docs/superpowers/` are not rewritten, except where a task says so: the epic outline's row, and one clause of the sidecar format design.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md` and `docs/diagnostics.md` do not change.  Prose uses two spaces after a full stop, as the repository does.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Before the pull request: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  When this plan was written there was no `v*` tag and nothing to compare.  If there is one now and `VersionPrefix` in `Directory.Build.props` is not greater, ask the owner for the new version.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class, when a task says so (if the filter option is rejected, run the whole solution instead):

```bash
dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Snapshot.SidecarReaderTests
```

## Where this plan departs from the spec

The spec is the intent; these are the places the plan reads it more exactly or differently.

1. **`PackageVersion` is in the root namespace**, `src/SqlSource/PackageVersion.cs`, and its test in `tests/SqlSource.Tests/PackageVersionTests.cs`.  It is the package's version and not a part of the format, and `Snapshot/` uses it from there as `Settings/` uses `SqlIdentifier`.
2. **The copies of the examples arrive in task 4**, with the reader, whose tests read them.  The spec's step 5 names them; the writer's round trip in task 5 uses the same files.
3. **The first pass checks the grammar and does not take `formatVersion`.**  The second starts by reading to the first `formatVersion` and no further, which in a file the tool wrote is its third key, and then reads the model from the start.  So such a file is tokenized twice.  A file of another version is read on to its end, for a second `formatVersion`.
4. **When a file has several mistakes, which one is reported is not a contract.**  A mistake of grammar anywhere is reported before any other.  An entry's `parameters` and `columns` need its `engine` for the shape of their types, and keys may come in any order: they are read where they stand when `engine` came before them, as the tool writes it, and passed over and read once the entry ends when it did not.  The two ways give one model, and may report different mistakes of a file that has several.
5. **A nested type that a kind names and the file gives as `null` is `MissingKey`**, not `WrongType`: `"kind": "array", "element": null`.  The requirement is known only after the whole type object is read, and `null` under a key that a reader can do without is "nothing".
6. **`\u00XX` is written with upper-case hex digits.**  The format design does not say; task 5 adds the clause to its section 1.
7. **`SidecarValues` has a nested class for each list**, `SidecarValues.Plan.Walked`, since `no-origin` is a value of two lists.  `SidecarKeys` holds the name of every key, for the reader, the writer and the comparer.

## Review Focus

Inputs the spec implies and does not list a test for.  Each has a test in the task named.

1. A sidecar with a merge conflict in it, `<<<<<<< HEAD` at the start of a line: the usual way one becomes unreadable.  A reader expects `InvalidJson` at the marker, never an exception.  Task 4.
2. A sidecar cut short or damaged anywhere: every prefix of an example, and an example with one character deleted or replaced at each of many offsets.  The reader returns one of its three states for each and never throws.  Task 4.
3. A sidecar an editor or a Windows checkout rewrote: `\r\n` line endings, tabs for indentation, or all on one line.  It reads as the same model as the file the tool wrote.  Task 4.
4. Two query names that are one name written two ways, `"Q"` and `"\u0051"`: a duplicate.  Two that differ in case only, `"q"` and `"Q"`: two entries.  Task 4.
5. A name or a value with characters a hand-written writer gets wrong: U+0000, U+007F, U+2028, a lone surrogate, a name that is only a quote.  It is written as the format says and reads back as the same string.  Task 5.

---

### Task 1: The epic outline says In progress

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the row of 2.1 in the table of phases, near line 65)
- Add to the commit: `docs/superpowers/plans/2026-10-09-query-generation-phase-2-1-snapshot-format.md` (this plan)

**Interfaces:**
- Produces: nothing a later task consumes.

- [ ] **Step 1: Change the row's status**

In the table of phases, the row that starts `| 2.1 The snapshot format | Not started |` becomes `| 2.1 The snapshot format | In progress |`.  Change nothing else in the row.

- [ ] **Step 2: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-09-query-generation-phase-2-1-snapshot-format.md
```

```bash
git commit -m "Start phase 2.1 of query generation: the snapshot format

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The model, `SidecarFormat`, `SidecarValues`, `SidecarKeys` and `PackageVersion`

**Files:**
- Create in `src/SqlSource/Snapshot/`: `Sidecar.cs`, `SidecarEntry.cs`, `SidecarResultKind.cs`, `SidecarParameter.cs`, `SidecarColumn.cs`, `SidecarOrigin.cs`, `SidecarTable.cs`, `SidecarType.cs`, `PostgresType.cs`, `PostgresTypeKind.cs`, `SqlServerType.cs`, `SqlServerUserType.cs`, `OtherEngineType.cs`, `SidecarFormat.cs`, `SidecarValues.cs`, `SidecarKeys.cs`
- Create: `src/SqlSource/PackageVersion.cs`
- Test: `tests/SqlSource.Tests/Snapshot/TestSidecars.cs`, `tests/SqlSource.Tests/Snapshot/SidecarModelTests.cs`, `tests/SqlSource.Tests/PackageVersionTests.cs`

**Interfaces:**
- Produces, all `internal`, namespace `SqlSource.Snapshot`:
  - `sealed record Sidecar(int FormatVersion, string ToolVersion, EquatableArray<SidecarEntry> Queries)` with `SidecarEntry? Find(string name)`.
  - `sealed record SidecarEntry(string Name, TextSpan NameSpan, string Hash, string Engine, string? Database, string? ServerVersion, SidecarResultKind ResultKind, SidecarTable? MatchesTable, string? Plan, string? TableMatch, EquatableArray<SidecarParameter> Parameters, EquatableArray<SidecarColumn>? Columns)`.
  - `enum SidecarResultKind { Rows, None }`.
  - `sealed record SidecarParameter(string Name, int Ordinal, SidecarType? Type, bool? Nullable, string? TypeSource)`.
  - `sealed record SidecarColumn(int Ordinal, string Name, SidecarType Type, bool? Nullable, string? NullableSource, SidecarOrigin? Origin, bool? Identity, bool? Computed)`.
  - `sealed record SidecarOrigin(string? Schema, string? Table, string? Column)`; `sealed record SidecarTable(string? Schema, string? Table)`.
  - `abstract record SidecarType(string Name)`; `sealed record PostgresType(string Name, string Kind, string Schema, string InternalName, int? Length, int? Precision, int? Scale, PostgresType? Element, PostgresType? Base, EquatableArray<string>? Labels, PostgresType? Subtype)` with `bool TryGetKind(out PostgresTypeKind kind)`; `sealed record SqlServerType(string Name, int MaxLength, int Precision, int Scale, SqlServerUserType? UserType)`; `sealed record SqlServerUserType(string? Schema, string? Name, string? AssemblyQualifiedName)`; `sealed record OtherEngineType(string Name)`.
  - `enum PostgresTypeKind { Base, Array, Domain, Enum, Range, Multirange, Composite }`.
  - `static class SidecarFormat`: `const int Version = 1`, `const string SchemaId`, `const string Warning`, `static string PathFor(string sqlPath)`.
  - `static class SidecarValues` with nested `Engine`, `ResultKind`, `Plan`, `TableMatch`, `TypeSource`, `NullableSource`, `Kind`, each of `const string` members.
  - `static class SidecarKeys`: a `const string` for each key of the format.
- Produces, namespace `SqlSource`: `static class PackageVersion` with `static string Prefix { get; }`.
- Produces for tests, namespace `SqlSource.Tests.Snapshot`: `static class TestSidecars` with `Of<T>`, `Postgres`, `SqlServer`, `Parameter`, `Column`, `Entry`, `SidecarOf`.

- [ ] **Step 1: Write the test support and the failing tests**

`tests/SqlSource.Tests/Snapshot/TestSidecars.cs`:

```csharp
using System;
using System.Collections.Immutable;
using SqlSource.Snapshot;

namespace SqlSource.Tests.Snapshot;

// Builds the records of a sidecar with the values a test does not care about filled in.
internal static class TestSidecars
{
    public static EquatableArray<T> Of<T>(params T[] items)
        where T : IEquatable<T> => new(ImmutableArray.Create(items));

    public static PostgresType Postgres(
        string name = "integer",
        string kind = SidecarValues.Kind.Base,
        string schema = "pg_catalog",
        string internalName = "int4",
        int? length = null,
        int? precision = null,
        int? scale = null,
        PostgresType? element = null,
        PostgresType? baseType = null,
        string[]? labels = null,
        PostgresType? subtype = null
    ) =>
        new(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels is null ? null : Of(labels),
            subtype
        );

    public static SqlServerType SqlServer(
        string name = "int",
        int maxLength = 4,
        int precision = 10,
        int scale = 0,
        SqlServerUserType? userType = null
    ) => new(name, maxLength, precision, scale, userType);

    public static SidecarParameter Parameter(
        int ordinal,
        string name,
        SidecarType? type,
        bool? nullable = null,
        string? typeSource = SidecarValues.TypeSource.Inferred
    ) => new(name, ordinal, type, nullable, type is null ? null : typeSource);

    public static SidecarColumn Column(
        int ordinal,
        string name,
        SidecarType type,
        bool? nullable = false,
        string? nullableSource = SidecarValues.NullableSource.Catalog,
        SidecarOrigin? origin = null,
        bool? identity = null,
        bool? computed = null
    ) => new(ordinal, name, type, nullable, nullableSource, origin, identity, computed);

    // An entry with rows has one column unless it is given some; an entry without rows has no columns, no table
    // and no provenance.
    public static SidecarEntry Entry(
        string name = "Q",
        string hash = "h",
        string engine = SidecarValues.Engine.Postgres,
        string? database = "app",
        string? serverVersion = "16.4",
        SidecarResultKind resultKind = SidecarResultKind.Rows,
        SidecarTable? matchesTable = null,
        string? plan = SidecarValues.Plan.NotNeeded,
        string? tableMatch = SidecarValues.TableMatch.NoOrigin,
        SidecarParameter[]? parameters = null,
        SidecarColumn[]? columns = null
    )
    {
        var rows = resultKind == SidecarResultKind.Rows;
        SidecarType type = engine == SidecarValues.Engine.SqlServer ? SqlServer() : Postgres();
        return new SidecarEntry(
            name,
            default,
            hash,
            engine,
            database,
            serverVersion,
            resultKind,
            rows ? matchesTable : null,
            rows ? plan : null,
            rows ? tableMatch : null,
            Of(parameters ?? []),
            rows ? Of(columns ?? [Column(0, "id", type)]) : null
        );
    }

    public static Sidecar SidecarOf(params SidecarEntry[] entries) => new(SidecarFormat.Version, "1.2.3", Of(entries));
}
```

`tests/SqlSource.Tests/Snapshot/SidecarModelTests.cs`:

```csharp
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarModelTests
{
    // Phase 5 caches these records in the generator's pipeline, so two that hold the same values must be equal.
    [Fact]
    public void Sidecar_SameValuesInSeparateInstances_AreEqual()
    {
        static Sidecar Create(string label = "active") =>
            TestSidecars.SidecarOf(
                Entry(
                    matchesTable: new SidecarTable("public", "users"),
                    parameters:
                    [
                        Parameter(
                            0,
                            "statuses",
                            Postgres(
                                "public.user_status[]",
                                SidecarValues.Kind.Array,
                                "public",
                                "_user_status",
                                element: Postgres(
                                    "public.user_status",
                                    SidecarValues.Kind.Enum,
                                    "public",
                                    "user_status",
                                    labels: [label, "deleted"]
                                )
                            )
                        ),
                    ],
                    columns:
                    [
                        Column(0, "id", Postgres(), origin: new SidecarOrigin("public", "users", "id"), identity: true),
                    ]
                ),
                Entry(
                    "Other",
                    engine: SidecarValues.Engine.SqlServer,
                    columns: [Column(0, "Id", SqlServer(userType: new SqlServerUserType("dbo", "CustomerId", null)))]
                ),
                Entry("Third", engine: "sqlite", columns: [Column(0, "id", new OtherEngineType("INTEGER"))])
            );

        Create().ShouldBe(Create());
        Create().GetHashCode().ShouldBe(Create().GetHashCode());
        Create().ShouldNotBe(Create("suspended"));
    }

    [Fact]
    public void Find_NameOfAnEntry_ReturnsIt()
    {
        var sidecar = TestSidecars.SidecarOf(Entry("GetUser"), Entry("ListUsers", hash: "other"));

        sidecar.Find("ListUsers").ShouldNotBeNull().Hash.ShouldBe("other");
    }

    [Theory]
    [InlineData("Missing")]
    // Names are compared ordinally.
    [InlineData("getuser")]
    [InlineData("")]
    public void Find_NameOfNoEntry_ReturnsNull(string name) =>
        TestSidecars.SidecarOf(Entry("GetUser")).Find(name).ShouldBeNull();

    [Theory]
    [InlineData("base", nameof(PostgresTypeKind.Base))]
    [InlineData("array", nameof(PostgresTypeKind.Array))]
    [InlineData("domain", nameof(PostgresTypeKind.Domain))]
    [InlineData("enum", nameof(PostgresTypeKind.Enum))]
    [InlineData("range", nameof(PostgresTypeKind.Range))]
    [InlineData("multirange", nameof(PostgresTypeKind.Multirange))]
    [InlineData("composite", nameof(PostgresTypeKind.Composite))]
    public void TryGetKind_KindOfTheFormat_ReturnsIt(string kind, string expected)
    {
        Postgres(kind: kind).TryGetKind(out var found).ShouldBeTrue();

        found.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("pseudo")]
    [InlineData("Base")]
    [InlineData("")]
    public void TryGetKind_AnyOtherValue_ReturnsFalse(string kind) =>
        Postgres(kind: kind).TryGetKind(out _).ShouldBeFalse();

    [Theory]
    [InlineData("Queries/Users.sql", "Queries/Users.sql.json")]
    [InlineData(@"C:\app\Users.sql", @"C:\app\Users.sql.json")]
    [InlineData("Users.SQL", "Users.SQL.json")]
    public void PathFor_SqlPath_AppendsJson(string sqlPath, string expected) =>
        SidecarFormat.PathFor(sqlPath).ShouldBe(expected);

    [Fact]
    public void SidecarFormat_IsVersionOneOfTheSchemaInTheRepository()
    {
        SidecarFormat.Version.ShouldBe(1);
        SidecarFormat.SchemaId.ShouldBe(
            "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json"
        );
        SidecarFormat.Warning.ShouldBe(
            "This file was generated by the 'dotnet sqlsource' tool.  "
                + "Do not edit by hand; use the tool to regenerate it."
        );
    }

    // Snapshot/ may not use Parsing/, so the two engines' names are written twice.  This holds them together.
    [Fact]
    public void Engine_Names_AreTheCanonicalNamesOfTheDialects()
    {
        SidecarValues.Engine.Postgres.ShouldBe(SqlDialectName.Canonical(SqlDialect.PostgreSql));
        SidecarValues.Engine.SqlServer.ShouldBe(SqlDialectName.Canonical(SqlDialect.SqlServer));
    }
}
```

`tests/SqlSource.Tests/PackageVersionTests.cs`:

```csharp
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class PackageVersionTests
{
    [Fact]
    public void Prefix_IsThreeNumbers() => PackageVersion.Prefix.ShouldMatch(@"^[0-9]+\.[0-9]+\.[0-9]+$");

    // The assembly's version is VersionPrefix and the run number, so no suffix can reach the value.
    [Fact]
    public void Prefix_IsTheStartOfTheAssemblysVersion() =>
        typeof(SqlSourceGenerator)
            .Assembly.GetName()
            .Version.ShouldNotBeNull()
            .ToString()
            .ShouldStartWith(PackageVersion.Prefix + ".");
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0246 and CS0103 for `SqlSource.Snapshot`, `Sidecar`, `PackageVersion` and the rest.

- [ ] **Step 3: Write the model**

`src/SqlSource/Snapshot/Sidecar.cs`:

```csharp
using System;

namespace SqlSource.Snapshot;

/// <summary>
/// A sidecar: what <c>sqlsource describe</c> learned about the queries of one <c>.sql</c> file.  The sidecar format
/// design is the contract, and this is its model.
/// </summary>
/// <param name="FormatVersion">The version of the format.</param>
/// <param name="ToolVersion">The version of the tool that wrote the file, as <c>major.minor.patch</c>.</param>
/// <param name="Queries">An entry for each query that needs types, in the file's order.</param>
internal sealed record Sidecar(int FormatVersion, string ToolVersion, EquatableArray<SidecarEntry> Queries)
{
    /// <summary>Returns the entry of the query with this name, compared ordinally, or null.</summary>
    public SidecarEntry? Find(string name)
    {
        // By index: Sonar asks for LINQ in place of a foreach that returns its item, and LINQ would box the array.
        for (var index = 0; index < Queries.Count; index++)
        {
            if (string.Equals(Queries[index].Name, name, StringComparison.Ordinal))
            {
                return Queries[index];
            }
        }

        return null;
    }
}
```

`src/SqlSource/Snapshot/SidecarEntry.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// The description of one query.  A member is nullable wherever a reader may find <c>null</c> or nothing.
/// </summary>
/// <param name="Name">The query's name: the entry's key.</param>
/// <param name="NameSpan">
/// Where the key is in the text that was read, quotes included.  <c>default</c> in an entry the tool builds; the
/// writer does not read it.
/// </param>
/// <param name="Hash">The hash of the query that was described.</param>
/// <param name="Engine">The canonical name of the engine, which picks the shape of every type in the entry.</param>
/// <param name="Database">The logical name of the database the query was described against.</param>
/// <param name="ServerVersion">The server's version.  Informational.</param>
/// <param name="ResultKind">Whether the statement produces rows.</param>
/// <param name="MatchesTable">The table whose column list the result is, or null.</param>
/// <param name="Plan">Whether the nullability plan walk ran.  Provenance, kept as written.</param>
/// <param name="TableMatch">The first failing check of the table match.  Provenance, kept as written.</param>
/// <param name="Parameters">The parameters, in order.</param>
/// <param name="Columns">The columns, in order.  Null when there are no rows.</param>
internal sealed record SidecarEntry(
    string Name,
    TextSpan NameSpan,
    string Hash,
    string Engine,
    string? Database,
    string? ServerVersion,
    SidecarResultKind ResultKind,
    SidecarTable? MatchesTable,
    string? Plan,
    string? TableMatch,
    EquatableArray<SidecarParameter> Parameters,
    EquatableArray<SidecarColumn>? Columns
);
```

`src/SqlSource/Snapshot/SidecarResultKind.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>Whether a statement produces a result set.</summary>
internal enum SidecarResultKind
{
    /// <summary><c>rows</c>: it does.</summary>
    Rows,

    /// <summary><c>none</c>: it does not.</summary>
    None,
}
```

`src/SqlSource/Snapshot/SidecarParameter.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>A parameter of a described query.</summary>
/// <param name="Name">The bare name, without the dialect's prefix.</param>
/// <param name="Ordinal">The zero-based position, which is the index in the entry's list.</param>
/// <param name="Type">The engine's type, or null when nothing gave one.</param>
/// <param name="Nullable">What a <c>-- param:</c> marker says, or null.</param>
/// <param name="TypeSource">Where the type came from.  Provenance, kept as written.</param>
internal sealed record SidecarParameter(string Name, int Ordinal, SidecarType? Type, bool? Nullable, string? TypeSource);
```

`src/SqlSource/Snapshot/SidecarColumn.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>A column of a described query's result.</summary>
/// <param name="Ordinal">The zero-based position, which is the index in the entry's list.</param>
/// <param name="Name">The name as the server returned it, an override suffix included.</param>
/// <param name="Type">The engine's type.</param>
/// <param name="Nullable">What the server and the tool's inference established; null is unknown.</param>
/// <param name="NullableSource">Which layer decided <paramref name="Nullable" />.  Provenance, kept as written.</param>
/// <param name="Origin">The base column, or null for an expression.</param>
/// <param name="Identity">Whether the base column is an identity column, or null when the engine cannot say.</param>
/// <param name="Computed">Whether the base column is computed, or null when the engine cannot say.</param>
internal sealed record SidecarColumn(
    int Ordinal,
    string Name,
    SidecarType Type,
    bool? Nullable,
    string? NullableSource,
    SidecarOrigin? Origin,
    bool? Identity,
    bool? Computed
);
```

`src/SqlSource/Snapshot/SidecarOrigin.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>The base column a result column comes from.</summary>
internal sealed record SidecarOrigin(string? Schema, string? Table, string? Column);
```

`src/SqlSource/Snapshot/SidecarTable.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>The table whose column list a result is exactly.</summary>
internal sealed record SidecarTable(string? Schema, string? Table);
```

`src/SqlSource/Snapshot/SidecarType.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// A type as an engine describes it.  The entry's engine picks the shape: <see cref="PostgresType" />,
/// <see cref="SqlServerType" />, or <see cref="OtherEngineType" /> for an engine this version does not know.
/// </summary>
/// <param name="Name">The type as the engine spells it, facets included.</param>
internal abstract record SidecarType(string Name);
```

`src/SqlSource/Snapshot/PostgresTypeKind.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>The kinds of a PostgreSQL type that the format lists.</summary>
internal enum PostgresTypeKind
{
    Base,
    Array,
    Domain,
    Enum,
    Range,
    Multirange,
    Composite,
}
```

`src/SqlSource/Snapshot/PostgresType.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>A PostgreSQL type.</summary>
/// <param name="Name">The spelled type, facets included.</param>
/// <param name="Kind">
/// The kind as written.  It may be one this version does not know: read it with <see cref="TryGetKind" />.
/// </param>
/// <param name="Schema">The type's schema.</param>
/// <param name="InternalName">The type's name in the catalog.</param>
/// <param name="Length">The length the modifier sets, or null.</param>
/// <param name="Precision">The precision the modifier sets, or null.</param>
/// <param name="Scale">The scale the modifier sets, or null.  It may be negative.</param>
/// <param name="Element">The element type of an array.</param>
/// <param name="Base">The base type of a domain.</param>
/// <param name="Labels">The labels of an enum, in order.</param>
/// <param name="Subtype">The element type of a range or a multirange.</param>
internal sealed record PostgresType(
    string Name,
    string Kind,
    string Schema,
    string InternalName,
    int? Length,
    int? Precision,
    int? Scale,
    PostgresType? Element,
    PostgresType? Base,
    EquatableArray<string>? Labels,
    PostgresType? Subtype
) : SidecarType(Name)
{
    /// <summary>
    /// Reads <see cref="Kind" />.  Returns false for a value that is not one of the format's seven, and never throws.
    /// </summary>
    public bool TryGetKind(out PostgresTypeKind kind)
    {
        var found = Kind switch
        {
            SidecarValues.Kind.Base => PostgresTypeKind.Base,
            SidecarValues.Kind.Array => PostgresTypeKind.Array,
            SidecarValues.Kind.Domain => PostgresTypeKind.Domain,
            SidecarValues.Kind.Enum => PostgresTypeKind.Enum,
            SidecarValues.Kind.Range => PostgresTypeKind.Range,
            SidecarValues.Kind.Multirange => PostgresTypeKind.Multirange,
            SidecarValues.Kind.Composite => PostgresTypeKind.Composite,
            _ => (PostgresTypeKind?)null,
        };
        kind = found ?? default;
        return found is not null;
    }
}
```

`src/SqlSource/Snapshot/SqlServerType.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>A SQL Server type, with the three facets as the server gives them for every type.</summary>
/// <param name="Name">The system type name, verbatim.</param>
/// <param name="MaxLength">The length in bytes, <c>-1</c> for <c>max</c>.</param>
/// <param name="Precision">The precision.</param>
/// <param name="Scale">The scale.</param>
/// <param name="UserType">The alias or CLR type, or null.</param>
internal sealed record SqlServerType(string Name, int MaxLength, int Precision, int Scale, SqlServerUserType? UserType)
    : SidecarType(Name);
```

`src/SqlSource/Snapshot/SqlServerUserType.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>An alias type or a CLR type of SQL Server.</summary>
internal sealed record SqlServerUserType(string? Schema, string? Name, string? AssemblyQualifiedName);
```

`src/SqlSource/Snapshot/OtherEngineType.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// A type under an engine this version does not know.  The format asks every engine for a name, and nothing else of
/// the type is read.
/// </summary>
internal sealed record OtherEngineType(string Name) : SidecarType(Name);
```

`src/SqlSource/Snapshot/SidecarFormat.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>What is fixed for the format this version reads and writes.</summary>
internal static class SidecarFormat
{
    /// <summary>The format version.</summary>
    public const int Version = 1;

    /// <summary>The value of <c>$schema</c>, and the <c>$id</c> of <c>schemas/sidecar-v1.schema.json</c>.</summary>
    public const string SchemaId =
        "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json";

    /// <summary>The value of <c>_WARNING</c>, the first line a reader of a file sees.</summary>
    public const string Warning =
        "This file was generated by the 'dotnet sqlsource' tool.  Do not edit by hand; use the tool to regenerate it.";

    /// <summary>The path of the sidecar of a <c>.sql</c> file: the file's path and <c>.json</c>.</summary>
    public static string PathFor(string sqlPath) => sqlPath + ".json";
}
```

`src/SqlSource/Snapshot/SidecarValues.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// The values the format design lists, which the tool writes.  A reader keeps a provenance value it does not know,
/// so none of these lists is closed.
/// </summary>
internal static class SidecarValues
{
    /// <summary>The engines whose type shape this version knows.  They are the dialects' canonical names.</summary>
    public static class Engine
    {
        public const string Postgres = "postgres";
        public const string SqlServer = "mssql";
    }

    public static class ResultKind
    {
        public const string Rows = "rows";
        public const string None = "none";
    }

    public static class Plan
    {
        public const string NotNeeded = "not-needed";
        public const string Walked = "walked";
        public const string Unavailable = "unavailable";
        public const string Skipped = "skipped";
    }

    public static class TableMatch
    {
        public const string Matched = "matched";
        public const string NoOrigin = "no-origin";
        public const string SeveralTables = "several-tables";
        public const string NamesDiffer = "names-differ";
        public const string ColumnsDiffer = "columns-differ";
        public const string OrderDiffers = "order-differs";
        public const string NullabilityDiffers = "nullability-differs";
    }

    public static class TypeSource
    {
        public const string Inferred = "inferred";
        public const string InferredFromCopies = "inferred-from-copies";
        public const string Declared = "declared";
    }

    public static class NullableSource
    {
        public const string Server = "server";
        public const string Catalog = "catalog";
        public const string OuterJoin = "outer-join";
        public const string View = "view";
        public const string NoOrigin = "no-origin";
        public const string Heuristic = "heuristic";
    }

    /// <summary>The kinds of a PostgreSQL type.</summary>
    public static class Kind
    {
        public const string Base = "base";
        public const string Array = "array";
        public const string Domain = "domain";
        public const string Enum = "enum";
        public const string Range = "range";
        public const string Multirange = "multirange";
        public const string Composite = "composite";
    }
}
```

`src/SqlSource/Snapshot/SidecarKeys.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>The keys of the format.  The reader, the writer and the comparer all name a key through here.</summary>
internal static class SidecarKeys
{
    public const string Warning = "_WARNING";
    public const string SchemaUrl = "$schema";
    public const string FormatVersion = "formatVersion";
    public const string ToolVersion = "toolVersion";
    public const string Queries = "queries";

    public const string Hash = "hash";
    public const string Engine = "engine";
    public const string Database = "database";
    public const string ServerVersion = "serverVersion";
    public const string ResultKind = "resultKind";
    public const string MatchesTable = "matchesTable";
    public const string Plan = "plan";
    public const string TableMatch = "tableMatch";
    public const string Parameters = "parameters";
    public const string Columns = "columns";

    public const string Name = "name";
    public const string Ordinal = "ordinal";
    public const string Type = "type";
    public const string Nullable = "nullable";
    public const string TypeSource = "typeSource";
    public const string NullableSource = "nullableSource";
    public const string Origin = "origin";
    public const string Identity = "identity";
    public const string Computed = "computed";

    public const string Schema = "schema";
    public const string Table = "table";
    public const string Column = "column";

    public const string Kind = "kind";
    public const string InternalName = "internalName";
    public const string Length = "length";
    public const string Precision = "precision";
    public const string Scale = "scale";
    public const string Element = "element";
    public const string Base = "base";
    public const string Labels = "labels";
    public const string Subtype = "subtype";

    public const string MaxLength = "maxLength";
    public const string UserType = "userType";
    public const string AssemblyQualifiedName = "assemblyQualifiedName";
}
```

`src/SqlSource/PackageVersion.cs`:

```csharp
namespace SqlSource;

/// <summary>
/// The version of the package, as <c>major.minor.patch</c>: what the tool writes as a sidecar's <c>toolVersion</c>
/// and the generator compares with it.
/// </summary>
internal static class PackageVersion
{
    /// <summary>
    /// The first three parts of the assembly's version.  The build sets that to <c>VersionPrefix</c> and the run
    /// number, so a <c>-dev</c> build and a <c>-pr</c> build of one version give one value.
    /// </summary>
    public static string Prefix { get; } =
        typeof(PackageVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Snapshot.SidecarModelTests`, then the same for `SqlSource.Tests.PackageVersionTests`.
Expected: PASS.  If the build reports RS1035 for `Assembly.GetName`, stop and ask the owner: the spec's review found the call allowed.

- [ ] **Step 5: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/Snapshot src/SqlSource/PackageVersion.cs tests/SqlSource.Tests/Snapshot tests/SqlSource.Tests/PackageVersionTests.cs
```

```bash
git commit -m "Add the model of a sidecar

Records with value equality for a sidecar, its entries and the type
shapes of the two engines, with the constants of the format and the
package's version as the tool writes it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The tokenizer

**Files:**
- Create in `src/SqlSource/Snapshot/`: `SidecarTokenKind.cs`, `SidecarToken.cs`, `SidecarTokenizer.cs`
- Test: `tests/SqlSource.Tests/Snapshot/SidecarTokenizerTests.cs`

**Interfaces:**
- Produces, `internal`, namespace `SqlSource.Snapshot`:
  - `enum SidecarTokenKind { End, Invalid, ObjectStart, ObjectEnd, ArrayStart, ArrayEnd, Colon, Comma, String, Number, True, False, Null }`.
  - `readonly record struct SidecarToken(SidecarTokenKind Kind, TextSpan Span, bool IsPlain = false)`.  `IsPlain` is true for a string with no escape and for a number with no fraction and no exponent.  A string's span includes its quotes.  The span of `End` is empty, at the end of the text.  The span of `Invalid` is the one character that cannot be read, or empty at the end of a text that stops inside a token.
  - `struct SidecarTokenizer(string text, int position = 0)`: `const int MaxDepth = 64`; `int Position { get; }`; `SidecarToken Next()`; `bool TrySkipValue(SidecarToken first, int depth, out TextSpan error)`, where `first` is a token `Next` returned, `depth` is the number of containers open around the value, and on failure `error` is the span of the token that breaks the grammar or opens level 65; `static string GetString(string text, SidecarToken token)`; `static bool StringEquals(string text, SidecarToken token, string value)`; `static bool TryGetInt32(string text, SidecarToken token, out int value)`.
- After `Invalid`, `Next` returns `End`: nothing after text that is not JSON is read.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tests/Snapshot/SidecarTokenizerTests.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public class SidecarTokenizerTests
{
    [Fact]
    public void Next_EachToken_HasItsKindAndItsSpan() =>
        Tokens("{ } [ ] : , \"a\" 12 true false null")
            .ShouldBe(
                [
                    "ObjectStart:{",
                    "ObjectEnd:}",
                    "ArrayStart:[",
                    "ArrayEnd:]",
                    "Colon::",
                    "Comma:,",
                    "String:\"a\"",
                    "Number:12",
                    "True:true",
                    "False:false",
                    "Null:null",
                ]
            );

    [Fact]
    public void Next_WhiteSpaceOfJson_IsSkipped() => Tokens(" \t\r\n1 \t\r\n").ShouldBe(["Number:1"]);

    [Fact]
    public void Next_EndOfText_IsAnEmptySpanAtTheEnd()
    {
        var tokens = new SidecarTokenizer("1 ");
        _ = tokens.Next();

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.End, new TextSpan(2, 0)));
        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.End, new TextSpan(2, 0)));
    }

    [Theory]
    // A byte order mark, a no-break space and a form feed are not white space in JSON.
    [InlineData("\uFEFF{}", 0, 1)]
    [InlineData("\u00A0", 0, 1)]
    [InlineData("\f", 0, 1)]
    // A comment, a single-quoted string, a bare word.
    [InlineData("/* c */", 0, 1)]
    [InlineData("'a'", 0, 1)]
    [InlineData("tru", 0, 1)]
    [InlineData("nul", 0, 1)]
    [InlineData("False", 0, 1)]
    // A string: an escape JSON lacks, bad hex, a control character, and one left open.
    [InlineData("\"a\\qb\"", 3, 1)]
    [InlineData("\"\\u12G4\"", 5, 1)]
    [InlineData("\"a\tb\"", 2, 1)]
    [InlineData("\"a\nb\"", 2, 1)]
    [InlineData("\"abc", 4, 0)]
    [InlineData("\"abc\\", 5, 0)]
    [InlineData("\"\\u12", 5, 0)]
    // A number: no digit, a leading zero, a fraction or an exponent without digits, a sign JSON lacks.
    [InlineData("-", 1, 0)]
    [InlineData("-x", 1, 1)]
    [InlineData("01", 1, 1)]
    [InlineData("1.", 2, 0)]
    [InlineData("1.x", 2, 1)]
    [InlineData("1e", 2, 0)]
    [InlineData("1e+", 3, 0)]
    [InlineData(".5", 0, 1)]
    [InlineData("+1", 0, 1)]
    public void Next_TextThatIsNotJson_IsInvalidAtTheCharacter(string text, int start, int length)
    {
        var tokens = new SidecarTokenizer(text);

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Invalid, new TextSpan(start, length)));
        tokens.Next().Kind.ShouldBe(SidecarTokenKind.End);
    }

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"größe / 日本 😀\"", "größe / 日本 😀")]
    public void GetString_StringWithoutAnEscape_IsPlainAndIsItsText(string json, string expected)
    {
        var token = new SidecarTokenizer(json).Next();

        token.ShouldBe(new SidecarToken(SidecarTokenKind.String, new TextSpan(0, json.Length), true));
        SidecarTokenizer.GetString(json, token).ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"\\\"\"", "\"")]
    [InlineData("\"\\\\\"", "\\")]
    [InlineData("\"\\/\"", "/")]
    [InlineData("\"\\b\"", "\b")]
    [InlineData("\"\\f\"", "\f")]
    [InlineData("\"\\n\"", "\n")]
    [InlineData("\"\\r\"", "\r")]
    [InlineData("\"\\t\"", "\t")]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("\"\\u00e9\\u00E9\"", "éé")]
    [InlineData("\"\\u0000\"", "\0")]
    // A surrogate pair, and one half alone, which JSON allows.
    [InlineData("\"\\uD83D\\uDE00\"", "😀")]
    [InlineData("\"\\uD83D\"", "\uD83D")]
    [InlineData("\"a\\tb\\nc\"", "a\tb\nc")]
    [InlineData("\"\\u0041bc\\\\\"", "Abc\\")]
    public void GetString_StringWithAnEscape_IsNotPlainAndIsDecoded(string json, string expected)
    {
        var token = new SidecarTokenizer(json).Next();

        token.ShouldBe(new SidecarToken(SidecarTokenKind.String, new TextSpan(0, json.Length)));
        SidecarTokenizer.GetString(json, token).ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"name\"", "name", true)]
    [InlineData("\"name\"", "nam", false)]
    [InlineData("\"name\"", "names", false)]
    [InlineData("\"Name\"", "name", false)]
    [InlineData("\"\"", "", true)]
    // A key written with an escape is the same key.
    [InlineData("\"na\\u006de\"", "name", true)]
    [InlineData("\"na\\u006de\"", "nome", false)]
    public void StringEquals_String_ComparesItsDecodedText(string json, string value, bool expected) =>
        SidecarTokenizer.StringEquals(json, new SidecarTokenizer(json).Next(), value).ShouldBe(expected);

    [Theory]
    [InlineData("0", true)]
    [InlineData("-0", true)]
    [InlineData("12", true)]
    [InlineData("-12", true)]
    [InlineData("1.5", false)]
    [InlineData("-0.0", false)]
    [InlineData("1e5", false)]
    [InlineData("1E+5", false)]
    [InlineData("1.5e-3", false)]
    public void Next_Number_IsPlainWhenItHasNoFractionAndNoExponent(string json, bool plain) =>
        new SidecarTokenizer(json)
            .Next()
            .ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(0, json.Length), plain));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("7", 7)]
    [InlineData("-1", -1)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void TryGetInt32_IntegerOfThirtyTwoBits_IsRead(string json, int expected)
    {
        SidecarTokenizer.TryGetInt32(json, new SidecarTokenizer(json).Next(), out var value).ShouldBeTrue();

        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("99999999999999999999999999")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("\"1\"")]
    [InlineData("true")]
    public void TryGetInt32_AnythingElse_IsNotRead(string json) =>
        SidecarTokenizer.TryGetInt32(json, new SidecarTokenizer(json).Next(), out _).ShouldBeFalse();

    [Theory]
    [InlineData("1")]
    [InlineData("\"a\"")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{ }")]
    [InlineData("[1, 2.5, -3e10, \"x\", true, false, null]")]
    [InlineData("{\"a\": 1, \"b\": {\"c\": [[], {}]}, \"d\": \"}]\"}")]
    public void TrySkipValue_Value_EndsAfterIt(string json)
    {
        var text = json + " 7";
        var tokens = new SidecarTokenizer(text);

        tokens.TrySkipValue(tokens.Next(), 0, out _).ShouldBeTrue();

        tokens.Position.ShouldBe(json.Length);
        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(json.Length + 1, 1), true));
    }

    [Theory]
    [InlineData("{\"a\" 1}", 5, 1)]
    [InlineData("{\"a\":}", 5, 1)]
    [InlineData("{\"a\":1,}", 7, 1)]
    [InlineData("{\"a\":1 \"b\":2}", 7, 3)]
    [InlineData("{,}", 1, 1)]
    [InlineData("{1:2}", 1, 1)]
    [InlineData("{\"a\":1", 6, 0)]
    [InlineData("{\"a\"", 4, 0)]
    [InlineData("{", 1, 0)]
    [InlineData("[1,]", 3, 1)]
    [InlineData("[1 2]", 3, 1)]
    [InlineData("[,1]", 1, 1)]
    [InlineData("[1", 2, 0)]
    [InlineData("[1, // c\n2]", 4, 1)]
    [InlineData("[\"a\\q\"]", 4, 1)]
    [InlineData("}", 0, 1)]
    [InlineData(":", 0, 1)]
    [InlineData("", 0, 0)]
    public void TrySkipValue_BrokenGrammar_FailsAtTheToken(string json, int start, int length)
    {
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(start, length));
    }

    [Theory]
    [InlineData('[', ']', "")]
    [InlineData('{', '}', "\"a\":")]
    public void TrySkipValue_SixtyFourLevels_IsRead(char open, char close, string key)
    {
        var json = Nested(open, close, key, SidecarTokenizer.MaxDepth);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out _).ShouldBeTrue();

        tokens.Position.ShouldBe(json.Length);
    }

    [Theory]
    [InlineData('[', ']', "")]
    [InlineData('{', '}', "\"a\":")]
    public void TrySkipValue_SixtyFiveLevels_FailsAtTheBracketThatOpensTheLast(char open, char close, string key)
    {
        var json = Nested(open, close, key, SidecarTokenizer.MaxDepth + 1);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(SidecarTokenizer.MaxDepth * (1 + key.Length), 1));
    }

    // The depth an outer reader has already opened counts.
    [Fact]
    public void TrySkipValue_DepthOfTheCaller_Counts()
    {
        var json = Nested('[', ']', string.Empty, 2);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), SidecarTokenizer.MaxDepth - 1, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(1, 1));
    }

    // The limit is what keeps such a file from overflowing the stack.
    [Fact]
    public void TrySkipValue_HundredThousandOpenBrackets_FailsWithoutOverflow()
    {
        var json = new string('[', 100_000);
        var tokens = new SidecarTokenizer(json);

        tokens.TrySkipValue(tokens.Next(), 0, out var error).ShouldBeFalse();

        error.ShouldBe(new TextSpan(SidecarTokenizer.MaxDepth, 1));
    }

    [Fact]
    public void Tokenizer_StartedAtAnOffset_ReadsFromThere()
    {
        var tokens = new SidecarTokenizer("[1, 22]", 3);

        tokens.Next().ShouldBe(new SidecarToken(SidecarTokenKind.Number, new TextSpan(4, 2), true));
    }

    // `levels` containers, each inside the one before: [[[]]] or {"a":{"a":{}}}.
    private static string Nested(char open, char close, string key, int levels)
    {
        var text = new System.Text.StringBuilder();
        for (var level = 0; level < levels; level++)
        {
            _ = text.Append(open);
            if (level < levels - 1)
            {
                _ = text.Append(key);
            }
        }

        return text.Append(close, levels).ToString();
    }

    private static List<string> Tokens(string text)
    {
        var tokens = new SidecarTokenizer(text);
        var found = new List<string>();
        for (var token = tokens.Next(); token.Kind != SidecarTokenKind.End; token = tokens.Next())
        {
            found.Add(token.Kind + ":" + text.Substring(token.Span.Start, token.Span.Length));
        }

        return found;
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0246 for `SidecarTokenizer`, `SidecarToken` and `SidecarTokenKind`.

- [ ] **Step 3: Write the tokenizer**

`src/SqlSource/Snapshot/SidecarTokenKind.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>What a <see cref="SidecarToken" /> is.</summary>
internal enum SidecarTokenKind
{
    /// <summary>The end of the text.  Its span is empty.</summary>
    End,

    /// <summary>
    /// Text that is not JSON.  Its span is the character that cannot be read, or empty at the end of a text that
    /// stops inside a token.
    /// </summary>
    Invalid,

    ObjectStart,
    ObjectEnd,
    ArrayStart,
    ArrayEnd,
    Colon,
    Comma,

    /// <summary>A string.  Its span includes the quotes.</summary>
    String,

    Number,
    True,
    False,
    Null,
}
```

`src/SqlSource/Snapshot/SidecarToken.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>One token of a sidecar's JSON.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Span">Where it is, as offsets into the text.</param>
/// <param name="IsPlain">
/// For a string, that it has no escape, so its value is the text between its quotes.  For a number, that it has no
/// fraction and no exponent.  False for every other kind.
/// </param>
internal readonly record struct SidecarToken(SidecarTokenKind Kind, TextSpan Span, bool IsPlain = false);
```

`src/SqlSource/Snapshot/SidecarTokenizer.cs`:

```csharp
using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads the tokens of a sidecar's JSON and checks the grammar of a value.  It allocates nothing: a token is a span,
/// and a string is created only by <see cref="GetString" />.
/// </summary>
/// <param name="text">The text.</param>
/// <param name="position">The offset to start at.</param>
internal struct SidecarTokenizer(string text, int position = 0)
{
    /// <summary>
    /// How deep a text may nest.  The readers above are recursive, so the limit is what keeps a file of ten thousand
    /// <c>[</c> from overflowing the stack: an exception in the generator costs every type its generated code.
    /// </summary>
    public const int MaxDepth = 64;

    private int _position = position;

    /// <summary>The offset after the last token read.</summary>
    public readonly int Position => _position;

    /// <summary>The value of a string token.  A string without an escape costs one <c>Substring</c>.</summary>
    public static string GetString(string text, SidecarToken token)
    {
        var start = token.Span.Start + 1;
        var length = token.Span.Length - 2;
        return token.IsPlain ? text.Substring(start, length) : Unescape(text, start, start + length);
    }

    /// <summary>Whether a string token's value is <paramref name="value" />.  A plain string allocates nothing.</summary>
    public static bool StringEquals(string text, SidecarToken token, string value) =>
        token.IsPlain
            ? token.Span.Length - 2 == value.Length
                && string.CompareOrdinal(text, token.Span.Start + 1, value, 0, value.Length) == 0
            : string.Equals(GetString(text, token), value, StringComparison.Ordinal);

    /// <summary>Reads a number token that is a signed 32-bit integer.  False for any other token.</summary>
    public static bool TryGetInt32(string text, SidecarToken token, out int value)
    {
        value = 0;
        if (token.Kind != SidecarTokenKind.Number || !token.IsPlain)
        {
            return false;
        }

        var index = token.Span.Start;
        var negative = text[index] == '-';
        if (negative)
        {
            index++;
        }

        // More digits than a 32-bit integer has.  The total below then fits a long.
        if (token.Span.End - index > 10)
        {
            return false;
        }

        long total = 0;
        for (; index < token.Span.End; index++)
        {
            total = (total * 10) + (text[index] - '0');
        }

        total = negative ? -total : total;
        if (total is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)total;
        return true;
    }

    /// <summary>Reads the next token, after any white space.</summary>
    public SidecarToken Next()
    {
        while (_position < text.Length && (text[_position] is ' ' or '\t' or '\n' or '\r'))
        {
            _position++;
        }

        if (_position >= text.Length)
        {
            return new SidecarToken(SidecarTokenKind.End, new TextSpan(text.Length, 0));
        }

        return text[_position] switch
        {
            '{' => Punctuation(SidecarTokenKind.ObjectStart),
            '}' => Punctuation(SidecarTokenKind.ObjectEnd),
            '[' => Punctuation(SidecarTokenKind.ArrayStart),
            ']' => Punctuation(SidecarTokenKind.ArrayEnd),
            ':' => Punctuation(SidecarTokenKind.Colon),
            ',' => Punctuation(SidecarTokenKind.Comma),
            '"' => ReadString(),
            't' => Literal("true", SidecarTokenKind.True),
            'f' => Literal("false", SidecarTokenKind.False),
            'n' => Literal("null", SidecarTokenKind.Null),
            '-' or (>= '0' and <= '9') => ReadNumber(),
            _ => InvalidAt(_position),
        };
    }

    /// <summary>
    /// Reads to the end of the value that starts with <paramref name="first" />, which <see cref="Next" /> returned,
    /// and checks its grammar on the way.  <paramref name="depth" /> is the number of containers open around the
    /// value.  On failure <paramref name="error" /> is the token that breaks the grammar, or the bracket that would
    /// open a level deeper than <see cref="MaxDepth" />.
    /// </summary>
    public bool TrySkipValue(SidecarToken first, int depth, out TextSpan error)
    {
        error = default;
        switch (first.Kind)
        {
            case SidecarTokenKind.String
            or SidecarTokenKind.Number
            or SidecarTokenKind.True
            or SidecarTokenKind.False
            or SidecarTokenKind.Null:
                return true;
            case SidecarTokenKind.ObjectStart when depth < MaxDepth:
                return TrySkipObject(depth + 1, out error);
            case SidecarTokenKind.ArrayStart when depth < MaxDepth:
                return TrySkipArray(depth + 1, out error);
            default:
                error = first.Span;
                return false;
        }
    }

    private static bool Fail(SidecarToken token, out TextSpan error)
    {
        error = token.Span;
        return false;
    }

    private static bool IsHex(char character) =>
        character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static int HexValue(char character) => character <= '9' ? character - '0' : (character | 0x20) - 'a' + 10;

    // An escape starts at index, with its backslash.  On success end is the offset after the escape; otherwise it
    // is the offset of the character that cannot be read, which may be the end of the text.
    private static bool TryReadEscape(string text, int index, out int end)
    {
        end = index + 1;
        if (end >= text.Length)
        {
            return false;
        }

        var escape = text[end];
        if (escape is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')
        {
            end++;
            return true;
        }

        if (escape != 'u')
        {
            return false;
        }

        for (end++; end < index + 6; end++)
        {
            if (end >= text.Length || !IsHex(text[end]))
            {
                return false;
            }
        }

        return true;
    }

    // The tokenizer has checked every escape between start and end.
    private static string Unescape(string text, int start, int end)
    {
        var value = new StringBuilder(end - start);
        var index = start;
        while (index < end)
        {
            var slash = text.IndexOf('\\', index, end - index);
            if (slash < 0)
            {
                break;
            }

            _ = value.Append(text, index, slash - index);
            var escape = text[slash + 1];
            if (escape == 'u')
            {
                var code =
                    (HexValue(text[slash + 2]) << 12)
                    | (HexValue(text[slash + 3]) << 8)
                    | (HexValue(text[slash + 4]) << 4)
                    | HexValue(text[slash + 5]);
                _ = value.Append((char)code);
                index = slash + 6;
                continue;
            }

            _ = value.Append(
                escape switch
                {
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => escape,
                }
            );
            index = slash + 2;
        }

        return value.Append(text, index, end - index).ToString();
    }

    private bool TrySkipObject(int depth, out TextSpan error)
    {
        error = default;
        var token = Next();
        if (token.Kind == SidecarTokenKind.ObjectEnd)
        {
            return true;
        }

        while (true)
        {
            if (token.Kind != SidecarTokenKind.String)
            {
                return Fail(token, out error);
            }

            token = Next();
            if (token.Kind != SidecarTokenKind.Colon)
            {
                return Fail(token, out error);
            }

            if (!TrySkipValue(Next(), depth, out error))
            {
                return false;
            }

            token = Next();
            if (token.Kind == SidecarTokenKind.ObjectEnd)
            {
                return true;
            }

            if (token.Kind != SidecarTokenKind.Comma)
            {
                return Fail(token, out error);
            }

            token = Next();
        }
    }

    private bool TrySkipArray(int depth, out TextSpan error)
    {
        error = default;
        var token = Next();
        if (token.Kind == SidecarTokenKind.ArrayEnd)
        {
            return true;
        }

        while (true)
        {
            if (!TrySkipValue(token, depth, out error))
            {
                return false;
            }

            token = Next();
            if (token.Kind == SidecarTokenKind.ArrayEnd)
            {
                return true;
            }

            if (token.Kind != SidecarTokenKind.Comma)
            {
                return Fail(token, out error);
            }

            token = Next();
        }
    }

    private SidecarToken Punctuation(SidecarTokenKind kind)
    {
        var token = new SidecarToken(kind, new TextSpan(_position, 1));
        _position++;
        return token;
    }

    private SidecarToken Literal(string word, SidecarTokenKind kind)
    {
        var start = _position;
        if (string.CompareOrdinal(text, start, word, 0, word.Length) != 0)
        {
            return InvalidAt(start);
        }

        _position = start + word.Length;
        return new SidecarToken(kind, new TextSpan(start, word.Length));
    }

    // Ends the read: nothing after text that is not JSON can be trusted.
    private SidecarToken InvalidAt(int index)
    {
        _position = text.Length;
        return new SidecarToken(SidecarTokenKind.Invalid, new TextSpan(index, index < text.Length ? 1 : 0));
    }

    private SidecarToken ReadString()
    {
        var start = _position;
        var plain = true;
        var index = start + 1;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '"')
            {
                _position = index + 1;
                return new SidecarToken(SidecarTokenKind.String, TextSpan.FromBounds(start, _position), plain);
            }

            if (character < ' ')
            {
                return InvalidAt(index);
            }

            if (character != '\\')
            {
                index++;
                continue;
            }

            plain = false;
            if (!TryReadEscape(text, index, out var end))
            {
                return InvalidAt(end);
            }

            index = end;
        }

        return InvalidAt(text.Length);
    }

    private SidecarToken ReadNumber()
    {
        var start = _position;
        var first = text[start] == '-' ? start + 1 : start;
        var index = SkipDigits(first);
        if (index == first)
        {
            return InvalidAt(first);
        }

        // A zero stands alone before a fraction: 01 is not a number.
        if (text[first] == '0' && index > first + 1)
        {
            return InvalidAt(first + 1);
        }

        var plain = true;
        if (IsAt(index, '.'))
        {
            plain = false;
            var end = SkipDigits(index + 1);
            if (end == index + 1)
            {
                return InvalidAt(end);
            }

            index = end;
        }

        if (IsAt(index, 'e') || IsAt(index, 'E'))
        {
            plain = false;
            var digits = IsAt(index + 1, '+') || IsAt(index + 1, '-') ? index + 2 : index + 1;
            var end = SkipDigits(digits);
            if (end == digits)
            {
                return InvalidAt(end);
            }

            index = end;
        }

        _position = index;
        return new SidecarToken(SidecarTokenKind.Number, TextSpan.FromBounds(start, index), plain);
    }

    private readonly bool IsAt(int index, char character) => index < text.Length && text[index] == character;

    private readonly int SkipDigits(int index)
    {
        while (index < text.Length && (text[index] is >= '0' and <= '9'))
        {
            index++;
        }

        return index;
    }
}
```

Two rows of `Next_TextThatIsNotJson_IsInvalidAtTheCharacter` depend on choices made here, and the test is the contract: `-x` is invalid at the `x` (offset 1, length 1), and `tru` and `False` are invalid at their first character.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Snapshot.SidecarTokenizerTests`
Expected: PASS.

- [ ] **Step 5: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/Snapshot tests/SqlSource.Tests/Snapshot
```

```bash
git commit -m "Add the tokenizer of a sidecar's JSON

It reads tokens as spans and allocates nothing, checks the grammar of a
value as it skips it, and stops at 64 levels, which is what keeps a
hostile file from overflowing the compiler's stack.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The reader

**Files:**
- Create in `src/SqlSource/Snapshot/`: `SidecarErrorKind.cs`, `SidecarError.cs`, `SidecarReadResult.cs`, `SidecarCursor.cs`, `SidecarReader.cs`, `SidecarTypeReader.cs`
- Create: `tests/SqlSource.Tests/Snapshot/Examples/Users.sql.json`, `tests/SqlSource.Tests/Snapshot/Examples/Orders.sql.json`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `.editorconfig`, `.gitattributes`
- Test: `tests/SqlSource.Tests/Snapshot/SidecarExamples.cs`, `SidecarReaderTests.cs`, `SidecarReaderRobustnessTests.cs`, `SidecarReaderAllocationTests.cs`

**Interfaces:**
- Consumes: the model and `SidecarKeys`, `SidecarValues`, `SidecarFormat` of task 2; `SidecarTokenizer`, `SidecarToken`, `SidecarTokenKind` of task 3; `TestSidecars` of task 2.
- Produces, `internal`, namespace `SqlSource.Snapshot`:
  - `enum SidecarErrorKind { InvalidJson, MissingKey, WrongType, DuplicateKey, OrdinalMismatch, ColumnsWithoutRows, UnknownResultKind }`.
  - `sealed record SidecarError(SidecarErrorKind Kind, TextSpan Span, string? Argument)`.
  - `sealed record SidecarReadResult(Sidecar? Sidecar, int? FormatVersion, SidecarError? Error)`, in one of three states: read (`Sidecar` set, `FormatVersion` 1), another version (`FormatVersion` alone), malformed (`Error` alone).
  - `static SidecarReadResult SidecarReader.Read(string text)`.
- Produces for tests: `SidecarExamples.Users`, `SidecarExamples.Orders` (the file names) and `SidecarExamples.Read(string name)` (the file's text).

**The span and the argument of each error**, which the tests pin:

| Kind | Span | Argument |
|----|----|----|
| `InvalidJson` | The character that cannot be read, or the token that breaks the grammar; empty at the end of a text that stops early | none |
| `MissingKey` | The opening brace of the object that lacks the key | The key |
| `WrongType` | The whole value, from its first character to its last | The key; for an entry that is not an object, the query's name; for an element of `parameters`, `columns` or `labels`, the array's key |
| `DuplicateKey` | The second key, quotes included | The key, decoded |
| `OrdinalMismatch` | The number | The number as written |
| `ColumnsWithoutRows` | The `columns` key, quotes included | none |
| `UnknownResultKind` | The string, quotes included | The string, decoded |

- [ ] **Step 1: Add the copies of the two examples**

The two files are the first two `json` blocks of the sidecar format design, byte for byte.  Create them with:

```bash
python3 - <<'EOF'
import pathlib, re
doc = pathlib.Path('docs/superpowers/specs/2026-10-07-sidecar-format-design.md').read_text()
blocks = re.findall(r"```json\n(.*?)```", doc, re.S)
assert len(blocks) == 3, len(blocks)
target = pathlib.Path('tests/SqlSource.Tests/Snapshot/Examples')
target.mkdir(parents=True, exist_ok=True)
(target / 'Users.sql.json').write_text(blocks[0])
(target / 'Orders.sql.json').write_text(blocks[1])
EOF
```

Expected: `Users.sql.json` is 11,301 bytes and `Orders.sql.json` 8,903, each starting with `{` and ending with `}` and a line feed.

In `tests/SqlSource.Tests/SqlSource.Tests.csproj`, in the `ItemGroup` that holds the `None` items, add:

```xml
        <!-- Snapshot/SidecarExamples.cs reads the examples of the sidecar format design from the output folder. -->
        <None Update="Snapshot/Examples/*.json" CopyToOutputDirectory="PreserveNewest" />
```

In `.editorconfig`, after the `[*.sql]` section, add:

```ini
# Sidecars in the layout the tool writes, where a line is as long as its value.
[tests/SqlSource.Tests/Snapshot/Examples/*.json]
max_line_length = off
end_of_line = lf
```

In `.gitattributes`, at the end, add:

```gitattributes
# A sidecar is compared byte for byte with what the writer gives, which uses LF
*.sql.json text eol=lf
```

Run: `tools/editorconfig-checker.sh`
Expected: no error.  Without the `.editorconfig` section it reports line 2 of each example as too long.

- [ ] **Step 2: Write the test support and the failing tests**

`tests/SqlSource.Tests/Snapshot/SidecarExamples.cs`:

```csharp
using System;
using System.IO;

namespace SqlSource.Tests.Snapshot;

// The examples of the sidecar format design, sections 4.1 and 4.2, as files beside the tests.  They are copies:
// a pull request that changes an example changes it in the document and here.
internal static class SidecarExamples
{
    public const string Users = "Users.sql.json";

    public const string Orders = "Orders.sql.json";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshot", "Examples", name));
}
```

Add to `TestSidecars` in `tests/SqlSource.Tests/Snapshot/TestSidecars.cs`, with `using System.Linq;` at the top:

```csharp
    // A sidecar that was read, as one that was built: an entry the tool builds has no span.
    public static Sidecar WithoutSpans(Sidecar sidecar) =>
        sidecar with
        {
            Queries = Of(sidecar.Queries.Select(entry => entry with { NameSpan = default }).ToArray()),
        };
```

`tests/SqlSource.Tests/Snapshot/SidecarReaderTests.cs`.  The JSON in this file is written with `'` for `"`, and `Q` turns it into JSON, so that a snippet reads as the file does:

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarReaderTests
{
    private const string IntType = "{'name': 'integer', 'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'}";

    private const string TextType = "{'name': 'text', 'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'text'}";

    private const string ArrayType =
        "{'name': 'text[]', 'kind': 'array', 'schema': 'pg_catalog', 'internalName': '_text', 'element': "
        + TextType
        + "}";

    private const string ParametersJson =
        "[{'name': 'id', 'ordinal': 0, 'type': " + IntType + ", 'nullable': null, 'typeSource': 'inferred'}]";

    private const string OriginJson = "{'schema': 'public', 'table': 'users', 'column': 'tags'}";

    private const string ColumnsJson =
        "[{'ordinal': 0, 'name': 'tags', 'type': "
        + ArrayType
        + ", 'nullable': true, 'nullableSource': 'catalog', 'origin': "
        + OriginJson
        + ", 'identity': false, 'computed': false}]";

    private const string EntryJson =
        "{'hash': 'h', 'engine': 'postgres', 'database': 'app', 'serverVersion': '16.4', 'resultKind': 'rows'"
        + ", 'matchesTable': {'schema': 'public', 'table': 'users'}, 'plan': 'walked', 'tableMatch': 'matched'"
        + ", 'parameters': "
        + ParametersJson
        + ", 'columns': "
        + ColumnsJson
        + "}";

    // One entry with every key of the format, on one line.  Each test changes one thing in it.
    private const string Valid = "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': " + EntryJson + "}}";

    private const string SqlServerType = "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0}";

    // Valid with every object's keys in the opposite order, so that the version and each entry's engine come last.
    // SidecarReaderRobustnessTests damages it too.
    internal static readonly string Reordered = CreateReordered();

    [Fact]
    public void Read_EveryKeyOfTheFormat_ReadsEachIntoTheModel()
    {
        var text = Q(Valid);

        var sidecar = ShouldBeRead(text);

        sidecar.ShouldBe(
            new Sidecar(
                1,
                "1.2.3",
                Of(
                    new SidecarEntry(
                        "Q",
                        new TextSpan(text.IndexOf("\"Q\"", StringComparison.Ordinal), 3),
                        "h",
                        "postgres",
                        "app",
                        "16.4",
                        SidecarResultKind.Rows,
                        new SidecarTable("public", "users"),
                        "walked",
                        "matched",
                        Of(Parameter(0, "id", Postgres())),
                        Of(
                            Column(
                                0,
                                "tags",
                                Postgres(
                                    "text[]",
                                    "array",
                                    internalName: "_text",
                                    element: Postgres("text", internalName: "text")
                                ),
                                nullable: true,
                                origin: new SidecarOrigin("public", "users", "tags"),
                                identity: false,
                                computed: false
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void Read_UsersExample_ReadsItsQueriesInTheFilesOrder()
    {
        var sidecar = ShouldBeRead(SidecarExamples.Read(SidecarExamples.Users));

        sidecar.FormatVersion.ShouldBe(1);
        sidecar.ToolVersion.ShouldBe("0.4.0");
        sidecar.Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "CreateUser", "DeleteUser", "ListUsers"]);
    }

    [Fact]
    public void Read_UsersExample_ReadsAnEntryWithoutRows()
    {
        var text = SidecarExamples.Read(SidecarExamples.Users);

        var entry = ShouldBeRead(text).Find("DeleteUser");

        entry.ShouldBe(
            new SidecarEntry(
                "DeleteUser",
                new TextSpan(text.IndexOf("\"DeleteUser\"", StringComparison.Ordinal), 12),
                "1e2d3c4b5a6f7e8d9c0b1a2f3e4d5c6b7a8f9e0d1c2b3a4f5e6d7c8b9a0f1e2d",
                "postgres",
                "app",
                "16.4",
                SidecarResultKind.None,
                null,
                null,
                null,
                Of(Parameter(0, "id", Postgres())),
                null
            )
        );
    }

    [Fact]
    public void Read_UsersExample_ReadsTheShapesOfPostgresTypes()
    {
        var sidecar = ShouldBeRead(SidecarExamples.Read(SidecarExamples.Users));
        var status = Postgres("public.user_status", "enum", "public", "user_status", labels: ["active", "suspended", "deleted"]);

        var getUser = sidecar.Find("GetUser").ShouldNotBeNull();
        getUser.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        getUser.TableMatch.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        getUser.MatchesTable.ShouldBeNull();
        var columns = getUser.Columns.ShouldNotBeNull();
        columns.Count.ShouldBe(5);
        columns[0]
            .ShouldBe(
                Column(0, "id", Postgres(), origin: new SidecarOrigin("public", "users", "id"), identity: true, computed: false)
            );
        columns[1].Type.ShouldBe(Postgres("character varying(100)", internalName: "varchar", length: 100));
        columns[2]
            .Type.ShouldBe(
                Postgres(
                    "public.email",
                    "domain",
                    "public",
                    "email",
                    baseType: Postgres("character varying(254)", internalName: "varchar", length: 254)
                )
            );
        columns[4].Nullable.ShouldBe(true);

        var listUsers = sidecar.Find("ListUsers").ShouldNotBeNull();
        listUsers
            .Parameters[0]
            .ShouldBe(
                Parameter(
                    0,
                    "statuses",
                    Postgres("public.user_status[]", "array", "public", "_user_status", element: status)
                )
            );
        listUsers.Parameters[1].TypeSource.ShouldBe(SidecarValues.TypeSource.Declared);
        listUsers
            .Columns.ShouldNotBeNull()[5]
            .ShouldBe(
                Column(
                    5,
                    "email_lower!",
                    Postgres("text", internalName: "text"),
                    nullable: null,
                    nullableSource: SidecarValues.NullableSource.NoOrigin
                )
            );
    }

    [Fact]
    public void Read_OrdersExample_ReadsTheShapeOfSqlServerTypes()
    {
        var sidecar = ShouldBeRead(SidecarExamples.Read(SidecarExamples.Orders));
        var customerId = SqlServer(userType: new SqlServerUserType("dbo", "CustomerId", null));

        sidecar.Queries.Select(entry => entry.Name).ShouldBe(["GetOrder", "CreateOrder", "ArchiveOrder", "SearchOrders"]);
        var createOrder = sidecar.Find("CreateOrder").ShouldNotBeNull();
        createOrder.Engine.ShouldBe(SidecarValues.Engine.SqlServer);
        createOrder.Database.ShouldBe("sales");
        createOrder.ServerVersion.ShouldBe("16.0.4135.4");
        createOrder.Plan.ShouldBe(SidecarValues.Plan.Skipped);
        createOrder.Parameters[0].ShouldBe(Parameter(0, "customerId", customerId));
        createOrder.Parameters[2].Type.ShouldBe(SqlServer("nvarchar(max)", -1, 0, 0));
        sidecar.Find("ArchiveOrder").ShouldNotBeNull().Columns.ShouldBeNull();
        var searchOrders = sidecar.Find("SearchOrders").ShouldNotBeNull().Columns.ShouldNotBeNull();
        searchOrders[2].Computed.ShouldBe(true);
        searchOrders[2].Type.ShouldBe(SqlServer("decimal(19,4)", 9, 19, 4));
        searchOrders[3]
            .ShouldBe(
                Column(
                    3,
                    "NotesOrEmpty",
                    SqlServer("nvarchar(max)", -1, 0, 0),
                    nullableSource: SidecarValues.NullableSource.Server
                )
            );
    }

    // What the reader cannot do without, removed.  The span is the opening brace of the object that lacks it.
    [Theory]
    [InlineData("'toolVersion': '1.2.3', ", "", "{'formatVersion'", "toolVersion")]
    [InlineData("'hash': 'h', ", "", "{'engine'", "hash")]
    [InlineData("'engine': 'postgres', ", "", "{'hash'", "engine")]
    [InlineData(", 'resultKind': 'rows'", "", "{'hash'", "resultKind")]
    [InlineData(", 'parameters': " + ParametersJson, "", "{'hash'", "parameters")]
    [InlineData(", 'columns': " + ColumnsJson, "", "{'hash'", "columns")]
    [InlineData("'name': 'id', ", "", "{'ordinal': 0, 'type'", "name")]
    [InlineData("'ordinal': 0, 'type'", "'type'", "{'name': 'id'", "ordinal")]
    [InlineData(", 'nullable': null", "", "{'name': 'id'", "nullable")]
    [InlineData("'ordinal': 0, 'name'", "'name'", "{'name': 'tags'", "ordinal")]
    [InlineData("'name': 'tags', ", "", "{'ordinal': 0, 'type': {'name': 'text[]'", "name")]
    [InlineData(", 'type': " + ArrayType, "", "{'ordinal': 0, 'name': 'tags'", "type")]
    [InlineData(", 'nullable': true", "", "{'ordinal': 0, 'name': 'tags'", "nullable")]
    [InlineData("'name': 'integer', ", "", "{'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'", "name")]
    [InlineData("'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'", "'schema': 's', 'internalName': 'i'", "{'name': 'integer'", "kind")]
    [InlineData("'schema': 'pg_catalog', 'internalName': 'int4'", "'internalName': 'int4'", "{'name': 'integer'", "schema")]
    [InlineData(", 'internalName': 'int4'", "", "{'name': 'integer'", "internalName")]
    // The nested type a kind names.  One given as null is missing.
    [InlineData(", 'element': " + TextType, "", "{'name': 'text[]'", "element")]
    [InlineData("'element': " + TextType, "'element': null", "{'name': 'text[]'", "element")]
    [InlineData("'kind': 'array'", "'kind': 'domain'", "{'name': 'text[]'", "base")]
    [InlineData("'kind': 'array'", "'kind': 'enum'", "{'name': 'text[]'", "labels")]
    [InlineData("'kind': 'array'", "'kind': 'range'", "{'name': 'text[]'", "subtype")]
    [InlineData("'kind': 'array'", "'kind': 'multirange'", "{'name': 'text[]'", "subtype")]
    public void Read_KeyAReaderCannotDoWithoutRemoved_IsMissingKeyAtTheObject(
        string old,
        string replacement,
        string at,
        string key
    ) => ShouldBeMalformed(Change(old, replacement), SidecarErrorKind.MissingKey, at, 1, key);

    [Theory]
    [InlineData("{'toolVersion': '1.2.3', 'queries': {}}", "formatVersion")]
    [InlineData("{'formatVersion': 1, 'toolVersion': '1.2.3'}", "queries")]
    [InlineData("{}", "formatVersion")]
    public void Read_TopLevelKeyRemoved_IsMissingKeyAtTheFirstBrace(string text, string key) =>
        ShouldBeMalformed(Q(text), SidecarErrorKind.MissingKey, "{", 1, key);

    [Theory]
    [InlineData("'maxLength': 4, ", "maxLength")]
    [InlineData("'precision': 10, ", "precision")]
    [InlineData(", 'scale': 0", "scale")]
    [InlineData("'name': 'int', ", "name")]
    public void Read_SqlServerTypeWithoutAFacet_IsMissingKeyAtTheType(string old, string key)
    {
        var type = SqlServerType.Replace(old, string.Empty, StringComparison.Ordinal);

        ShouldBeMalformed(FileOf("mssql", type), SidecarErrorKind.MissingKey, type, 1, key);
    }

    [Fact]
    public void Read_EveryKeyAReaderCanDoWithoutRemoved_ReadsNull()
    {
        var text = Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'engine': 'postgres'"
                + ", 'resultKind': 'rows', 'parameters': [{'name': 'id', 'ordinal': 0, 'nullable': null}]"
                + ", 'columns': [{'ordinal': 0, 'name': 'id', 'type': "
                + IntType
                + ", 'nullable': null}]}}}"
        );

        var entry = ShouldBeRead(text).Queries[0];

        entry.Database.ShouldBeNull();
        entry.ServerVersion.ShouldBeNull();
        entry.MatchesTable.ShouldBeNull();
        entry.Plan.ShouldBeNull();
        entry.TableMatch.ShouldBeNull();
        entry.Parameters[0].ShouldBe(new SidecarParameter("id", 0, null, null, null));
        entry.Columns.ShouldNotBeNull()[0].ShouldBe(new SidecarColumn(0, "id", Postgres(), null, null, null, null, null));
    }

    [Fact]
    public void Read_NullUnderEveryKeyAReaderCanDoWithout_ReadsNull()
    {
        var type =
            "{'name': 'n', 'kind': 'base', 'schema': 's', 'internalName': 'i', 'length': null, 'precision': null"
            + ", 'scale': null, 'element': null, 'base': null, 'labels': null, 'subtype': null}";
        var text = Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'engine': 'postgres'"
                + ", 'database': null, 'serverVersion': null, 'resultKind': 'rows', 'matchesTable': null"
                + ", 'plan': null, 'tableMatch': null"
                + ", 'parameters': [{'name': 'id', 'ordinal': 0, 'type': null, 'nullable': null, 'typeSource': null}]"
                + ", 'columns': [{'ordinal': 0, 'name': 'id', 'type': "
                + type
                + ", 'nullable': null, 'nullableSource': null, 'origin': {'schema': null, 'table': null"
                + ", 'column': null}, 'identity': null, 'computed': null}]}}}"
        );

        var entry = ShouldBeRead(text).Queries[0];

        entry.ShouldBe(
            Entry(
                database: null,
                serverVersion: null,
                plan: null,
                tableMatch: null,
                parameters: [new SidecarParameter("id", 0, null, null, null)],
                columns:
                [
                    new SidecarColumn(
                        0,
                        "id",
                        Postgres("n", "base", "s", "i"),
                        null,
                        null,
                        new SidecarOrigin(null, null, null),
                        null,
                        null
                    ),
                ]
            ) with
            {
                NameSpan = entry.NameSpan,
            }
        );
    }

    // null under a key the reader cannot do without.  The span is the null.
    [Theory]
    [InlineData("'toolVersion': '1.2.3'", "'toolVersion': null", "null, 'queries'", "toolVersion")]
    [InlineData("'queries': {'Q': " + EntryJson + "}", "'queries': null", "null}", "queries")]
    [InlineData("'hash': 'h'", "'hash': null", "null, 'engine'", "hash")]
    [InlineData("'engine': 'postgres'", "'engine': null", "null, 'database'", "engine")]
    [InlineData("'resultKind': 'rows'", "'resultKind': null", "null, 'matchesTable'", "resultKind")]
    [InlineData("'parameters': " + ParametersJson, "'parameters': null", "null, 'columns'", "parameters")]
    [InlineData("'columns': " + ColumnsJson, "'columns': null", "null}}}", "columns")]
    [InlineData("'name': 'id'", "'name': null", "null, 'ordinal'", "name")]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': null, 'type'", "null, 'type'", "ordinal")]
    [InlineData("'name': 'tags'", "'name': null", "null, 'type': {'name': 'text[]'", "name")]
    [InlineData("'type': " + ArrayType, "'type': null", "null, 'nullable': true", "type")]
    [InlineData("'name': 'integer'", "'name': null", "null, 'kind'", "name")]
    [InlineData("'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'", "'kind': null, 'schema': 's', 'internalName': 'i'", "null, 'schema': 's'", "kind")]
    [InlineData("'internalName': 'int4'", "'internalName': null", "null}, 'nullable': null", "internalName")]
    public void Read_NullUnderAKeyAReaderCannotDoWithout_IsWrongTypeAtTheNull(
        string old,
        string replacement,
        string at,
        string key
    ) => ShouldBeMalformed(Change(old, replacement), SidecarErrorKind.WrongType, at, 4, key);

    [Theory]
    [InlineData("rows")]
    [InlineData("none")]
    public void Read_ColumnsAsNull_IsWrongTypeUnderEitherResultKind(string resultKind) =>
        ShouldBeMalformed(
            Q(
                "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'engine': 'postgres'"
                    + ", 'resultKind': '"
                    + resultKind
                    + "', 'parameters': [], 'columns': null}}}"
            ),
            SidecarErrorKind.WrongType,
            "null}}}",
            4,
            "columns"
        );

    // A value of another JSON type.  The span is the whole value.
    [Theory]
    [InlineData("'formatVersion': 1", "'formatVersion': '1'", "'1'", 3, "formatVersion")]
    [InlineData("'formatVersion': 1", "'formatVersion': 1.0", "1.0", 3, "formatVersion")]
    [InlineData("'formatVersion': 1", "'formatVersion': 1e0", "1e0", 3, "formatVersion")]
    [InlineData("'formatVersion': 1", "'formatVersion': [1]", "[1]", 3, "formatVersion")]
    [InlineData("'formatVersion': 1", "'formatVersion': null", "null", 4, "formatVersion")]
    [InlineData("'formatVersion': 1", "'formatVersion': 2147483648", "2147483648", 10, "formatVersion")]
    [InlineData("'toolVersion': '1.2.3'", "'toolVersion': 123", "123", 3, "toolVersion")]
    [InlineData("'queries': {'Q': " + EntryJson + "}", "'queries': []", "[]", 2, "queries")]
    [InlineData("'Q': " + EntryJson, "'Q': 5", "5}}", 1, "Q")]
    [InlineData("'hash': 'h'", "'hash': 5", "5, 'engine'", 1, "hash")]
    [InlineData("'hash': 'h'", "'hash': ['h', {}]", "['h', {}]", 9, "hash")]
    [InlineData("'database': 'app'", "'database': 5", "5, 'serverVersion'", 1, "database")]
    [InlineData("'serverVersion': '16.4'", "'serverVersion': 16.4", "16.4", 4, "serverVersion")]
    [InlineData("'resultKind': 'rows'", "'resultKind': 5", "5, 'matchesTable'", 1, "resultKind")]
    [InlineData("'matchesTable': {'schema': 'public', 'table': 'users'}", "'matchesTable': []", "[], 'plan'", 2, "matchesTable")]
    [InlineData("'table': 'users'}", "'table': 5}", "5}, 'plan'", 1, "table")]
    [InlineData("'plan': 'walked'", "'plan': true", "true, 'tableMatch'", 4, "plan")]
    [InlineData("'tableMatch': 'matched'", "'tableMatch': {}", "{}, 'parameters'", 2, "tableMatch")]
    [InlineData("'parameters': " + ParametersJson, "'parameters': {}", "{}, 'columns'", 2, "parameters")]
    [InlineData("'parameters': " + ParametersJson, "'parameters': [5]", "5], 'columns'", 1, "parameters")]
    [InlineData("'columns': " + ColumnsJson, "'columns': {}", "{}}}}", 2, "columns")]
    [InlineData("'columns': " + ColumnsJson, "'columns': ['c']", "'c']}}}", 3, "columns")]
    [InlineData("'name': 'id'", "'name': 5", "5, 'ordinal'", 1, "name")]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': '0', 'type'", "'0', 'type'", 3, "ordinal")]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': 0.0, 'type'", "0.0, 'type'", 3, "ordinal")]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': 2147483648, 'type'", "2147483648", 10, "ordinal")]
    [InlineData("'type': " + IntType, "'type': 'int4'", "'int4', 'nullable'", 6, "type")]
    [InlineData("'nullable': null, ", "'nullable': 'yes', ", "'yes'", 5, "nullable")]
    [InlineData("'typeSource': 'inferred'", "'typeSource': 5", "5}], 'columns'", 1, "typeSource")]
    [InlineData("'nullableSource': 'catalog'", "'nullableSource': 5", "5, 'origin'", 1, "nullableSource")]
    [InlineData("'origin': " + OriginJson, "'origin': 'x'", "'x', 'identity'", 3, "origin")]
    [InlineData("'column': 'tags'", "'column': 5", "5}, 'identity'", 1, "column")]
    [InlineData("'identity': false", "'identity': 1", "1, 'computed'", 1, "identity")]
    [InlineData("'computed': false", "'computed': 'no'", "'no'", 4, "computed")]
    [InlineData("'internalName': 'int4'", "'internalName': 4", "4}, 'nullable': null", 1, "internalName")]
    [InlineData("'element': " + TextType, "'element': 5", "5}, 'nullable': true", 1, "element")]
    public void Read_ValueOfAnotherJsonType_IsWrongTypeAtTheValue(
        string old,
        string replacement,
        string at,
        int length,
        string key
    ) => ShouldBeMalformed(Change(old, replacement), SidecarErrorKind.WrongType, at, length, key);

    [Theory]
    [InlineData("postgres", "{'name': 'n', 'kind': 'base', 'schema': 's', 'internalName': 'i', 'length': '100'}", "'100'", 5, "length")]
    [InlineData("postgres", "{'name': 'n', 'kind': 'base', 'schema': 's', 'internalName': 'i', 'scale': 1.5}", "1.5", 3, "scale")]
    [InlineData("postgres", "{'name': 'n', 'kind': 'enum', 'schema': 's', 'internalName': 'i', 'labels': ['a', 1]}", "1]", 1, "labels")]
    [InlineData("postgres", "{'name': 'n', 'kind': 'enum', 'schema': 's', 'internalName': 'i', 'labels': 'a'}", "'a'}", 3, "labels")]
    [InlineData("postgres", "{'name': 'n', 'kind': 'range', 'schema': 's', 'internalName': 'i', 'subtype': []}", "[]}", 2, "subtype")]
    [InlineData("mssql", "{'name': 'int', 'maxLength': 'x', 'precision': 10, 'scale': 0}", "'x'", 3, "maxLength")]
    [InlineData("mssql", "{'name': 'int', 'maxLength': null, 'precision': 10, 'scale': 0}", "null, 'precision'", 4, "maxLength")]
    [InlineData("mssql", "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0, 'userType': 5}", "5}", 1, "userType")]
    [InlineData("mssql", "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0, 'userType': {'name': 5}}", "5}", 1, "name")]
    [InlineData("sqlite", "{'name': 5}", "5}", 1, "name")]
    [InlineData("sqlite", "'INTEGER'", "'INTEGER'", 9, "type")]
    public void Read_TypeWithAValueOfAnotherJsonType_IsWrongTypeAtTheValue(
        string engine,
        string type,
        string at,
        int length,
        string key
    ) => ShouldBeMalformed(FileOf(engine, type), SidecarErrorKind.WrongType, at, length, key);

    // A key the reader reads, written twice.  The span is the second key.
    [Theory]
    [InlineData("'formatVersion': 1", "'formatVersion': 1, 'formatVersion':1", "'formatVersion':1", 15, "formatVersion")]
    [InlineData("'toolVersion': '1.2.3'", "'toolVersion': '1.2.3', 'toolVersion': '9'", "'toolVersion': '9'", 13, "toolVersion")]
    [InlineData("'queries': {'Q': " + EntryJson + "}", "'queries': {}, 'queries':{}", "'queries':{}", 9, "queries")]
    [InlineData("'Q': " + EntryJson, "'Q': " + EntryJson + ", 'Q': 5", "'Q': 5", 3, "Q")]
    // One name written two ways is one name.
    [InlineData("'Q': " + EntryJson, "'Q': " + EntryJson + ", '\\u0051': 5", "'\\u0051'", 8, "Q")]
    [InlineData("'hash': 'h'", "'hash': 'h', 'hash': 'i'", "'hash': 'i'", 6, "hash")]
    [InlineData("'hash': 'h'", "'hash': 'h', 'h\\u0061sh': 'i'", "'h\\u0061sh'", 11, "hash")]
    [InlineData("'table': 'users'}", "'table': 'users', 'table': 'x'}", "'table': 'x'", 7, "table")]
    [InlineData("'nullable': null, ", "'nullable': null, 'nullable': false, ", "'nullable': false", 10, "nullable")]
    [InlineData("'name': 'integer'", "'name': 'integer', 'name': 'int'", "'name': 'int',", 6, "name")]
    [InlineData("'column': 'tags'", "'column': 'tags', 'column': 'x'", "'column': 'x'", 8, "column")]
    [InlineData("'computed': false", "'computed': false, 'computed': null", "'computed': null", 10, "computed")]
    public void Read_KeyWrittenTwice_IsDuplicateKeyAtTheSecond(
        string old,
        string replacement,
        string at,
        int length,
        string key
    ) => ShouldBeMalformed(Change(old, replacement), SidecarErrorKind.DuplicateKey, at, length, key);

    [Fact]
    public void Read_UnknownKeyWrittenTwice_IsRead() =>
        ShouldBeRead(Change("'hash': 'h'", "'x': 1, 'hash': 'h', 'x': 2")).Queries.Count.ShouldBe(1);

    // Names are compared ordinally, so these are two queries.
    [Fact]
    public void Read_QueryNamesThatDifferInCase_AreTwoEntries()
    {
        var text = Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {"
                + "'q': {'hash': 'a', 'engine': 'postgres', 'resultKind': 'none', 'parameters': []}, "
                + "'Q': {'hash': 'b', 'engine': 'postgres', 'resultKind': 'none', 'parameters': []}}}"
        );

        var sidecar = ShouldBeRead(text);

        sidecar.Find("q").ShouldNotBeNull().Hash.ShouldBe("a");
        sidecar.Find("Q").ShouldNotBeNull().Hash.ShouldBe("b");
    }

    [Theory]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': 1, 'type'", "1, 'type'", 1, "1")]
    [InlineData("'ordinal': 0, 'type'", "'ordinal': -1, 'type'", "-1, 'type'", 2, "-1")]
    [InlineData("'ordinal': 0, 'name'", "'ordinal': 7, 'name'", "7, 'name'", 1, "7")]
    public void Read_OrdinalThatIsNotTheIndex_IsOrdinalMismatchAtTheNumber(
        string old,
        string replacement,
        string at,
        int length,
        string ordinal
    ) => ShouldBeMalformed(Change(old, replacement), SidecarErrorKind.OrdinalMismatch, at, length, ordinal);

    // A hand edit that copies an element leaves two with one ordinal.
    [Fact]
    public void Read_SecondParameterWithTheOrdinalOfTheFirst_IsOrdinalMismatch()
    {
        const string Two =
            "[{'name': 'a', 'ordinal': 0, 'nullable': null}, {'name': 'b', 'ordinal':0, 'nullable': null}]";

        ShouldBeMalformed(
            Change("'parameters': " + ParametersJson, "'parameters': " + Two),
            SidecarErrorKind.OrdinalMismatch,
            "0, 'nullable': null}]",
            1,
            "0"
        );
    }

    [Theory]
    [InlineData("'resultKind': 'rows'", "'resultKind': 'none'")]
    // Key order is not significant: the kind may come after the columns.
    [InlineData("'resultKind': 'rows', 'matchesTable'", "'columns': [], 'resultKind': 'none', 'matchesTable'")]
    public void Read_ColumnsWithoutRows_IsMalformedAtTheColumnsKey(string old, string replacement)
    {
        var text = Change(old, replacement);
        if (replacement.StartsWith("'columns'", StringComparison.Ordinal))
        {
            text = text.Replace(Q(", 'columns': " + ColumnsJson), string.Empty, StringComparison.Ordinal);
        }

        ShouldBeMalformed(text, SidecarErrorKind.ColumnsWithoutRows, "'columns'", 9, null);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("Rows")]
    [InlineData("")]
    public void Read_ResultKindThatIsNotRowsOrNone_IsUnknownResultKindAtTheValue(string value) =>
        ShouldBeMalformed(
            Change("'resultKind': 'rows'", "'resultKind': '" + value + "'"),
            SidecarErrorKind.UnknownResultKind,
            "'" + value + "', 'matchesTable'",
            value.Length + 2,
            value
        );

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData(" \n\t", 3, 0)]
    [InlineData("[]", 0, 1)]
    [InlineData("5", 0, 1)]
    [InlineData("\"formatVersion\"", 0, 15)]
    [InlineData("null", 0, 4)]
    // A byte order mark is the caller's to remove, as the compiler does for a file it reads.
    [InlineData("﻿{\"formatVersion\": 1}", 0, 1)]
    [InlineData("{\"formatVersion\": 1} x", 21, 1)]
    [InlineData("{\"formatVersion\": 1}{}", 20, 1)]
    [InlineData("{\"formatVersion\": 1,}", 20, 1)]
    [InlineData("{\"formatVersion\": 1 /* c */}", 20, 1)]
    [InlineData("{\"formatVersion\": 1, // c\n\"toolVersion\": \"1\"}", 21, 1)]
    [InlineData("{\"formatVersion\": 1", 19, 0)]
    [InlineData("{\"formatVersion\": 01}", 19, 1)]
    [InlineData("{'formatVersion': 1}", 1, 1)]
    // Not valid JSON, whatever its version.
    [InlineData("{\"formatVersion\": 2, \"queries\": [}", 33, 1)]
    public void Read_TextThatIsNotOneJsonObject_IsInvalidJson(string text, int start, int length)
    {
        var result = SidecarReader.Read(text);

        result.Sidecar.ShouldBeNull();
        result.FormatVersion.ShouldBeNull();
        result.Error.ShouldBe(new SidecarError(SidecarErrorKind.InvalidJson, new TextSpan(start, length), null));
    }

    // The first object is level 1, so an unknown key may hold 63 arrays, each inside the one before.
    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void Read_NestingUnderAnUnknownKey_IsReadToSixtyFourLevels(int arrays, bool read)
    {
        const string Start = "{\"formatVersion\": 2, \"x\": ";
        var text = Start + new string('[', arrays) + new string(']', arrays) + "}";

        var result = SidecarReader.Read(text);

        result.FormatVersion.ShouldBe(read ? 2 : null);
        result.Error.ShouldBe(
            read
                ? null
                : new SidecarError(SidecarErrorKind.InvalidJson, new TextSpan(Start.Length + arrays - 1, 1), null)
        );
    }

    // A reader of format 1 cannot say whether a file of format 2 is well formed: it reads the version and stops.
    [Theory]
    [InlineData("{'formatVersion': 2, 'toolVersion': '9.0.0', 'queries': {}}", 2)]
    [InlineData("{'formatVersion': 2, 'queries': 5, 'toolVersion': null}", 2)]
    [InlineData("{'queries': {'Q': {'hash': 5}}, 'formatVersion': 3}", 3)]
    [InlineData("{'formatVersion': 0}", 0)]
    [InlineData("{'formatVersion': -1}", -1)]
    [InlineData("{'formatVersion': 2147483647}", int.MaxValue)]
    // What format 1 calls a mistake is not judged in a file of another format.
    [InlineData("{'formatVersion': 2, 'toolVersion': 5, 'toolVersion': 6, 'queries': 1, 'queries': 2}", 2)]
    [InlineData("{'toolVersion': 5, 'toolVersion': 6, 'formatVersion': 2}", 2)]
    public void Read_AnotherFormatVersion_IsItsVersionAlone(string text, int version) =>
        SidecarReader.Read(Q(text)).ShouldBe(new SidecarReadResult(null, version, null));

    // A second formatVersion is a mistake in any version, wherever the first one stopped the search for it.
    [Theory]
    [InlineData("{'formatVersion': 2, 'x': {}, 'formatVersion':2}")]
    [InlineData("{'formatVersion': 2, 'x': {}, 'formatVersion':1}")]
    [InlineData("{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {}, 'formatVersion':2}")]
    public void Read_SecondFormatVersion_IsDuplicateKey(string text) =>
        ShouldBeMalformed(Q(text), SidecarErrorKind.DuplicateKey, "'formatVersion':", 15, "formatVersion");

    [Fact]
    public void Read_EngineTheReaderDoesNotKnow_ReadsEachTypeAsItsName()
    {
        var text = FileOf("sqlite", "{'name': 'INTEGER', 'affinity': 'int', 'kind': 5, 'maxLength': 'x'}");

        var entry = ShouldBeRead(text).Queries[0];

        entry.Engine.ShouldBe("sqlite");
        entry.Columns.ShouldNotBeNull()[0].Type.ShouldBe(new OtherEngineType("INTEGER"));
    }

    [Fact]
    public void Read_EngineTheReaderDoesNotKnow_StillNeedsATypesName() =>
        ShouldBeMalformed(FileOf("sqlite", "{'affinity': 'int'}"), SidecarErrorKind.MissingKey, "{'affinity'", 1, "name");

    // An unknown kind is the "unsupported type" of phase 5, at the column: the reader keeps it and asks nothing more.
    [Fact]
    public void Read_KindTheReaderDoesNotKnow_KeepsItAndRequiresNoNestedType()
    {
        var text = FileOf("postgres", "{'name': 'n', 'kind': 'pseudo', 'schema': 's', 'internalName': 'i'}");

        var type = ShouldBeRead(text).Queries[0].Columns.ShouldNotBeNull()[0].Type.ShouldBeOfType<PostgresType>();

        type.Kind.ShouldBe("pseudo");
        type.TryGetKind(out _).ShouldBeFalse();
    }

    [Fact]
    public void Read_ProvenanceValueTheReaderDoesNotKnow_KeepsIt()
    {
        var text = Change("'plan': 'walked'", "'plan': 'guessed'")
            .Replace(Q("'tableMatch': 'matched'"), Q("'tableMatch': 'almost'"), StringComparison.Ordinal)
            .Replace(Q("'typeSource': 'inferred'"), Q("'typeSource': 'asked'"), StringComparison.Ordinal)
            .Replace(Q("'nullableSource': 'catalog'"), Q("'nullableSource': 'oracle'"), StringComparison.Ordinal);

        var entry = ShouldBeRead(text).Queries[0];

        entry.Plan.ShouldBe("guessed");
        entry.TableMatch.ShouldBe("almost");
        entry.Parameters[0].TypeSource.ShouldBe("asked");
        entry.Columns.ShouldNotBeNull()[0].NullableSource.ShouldBe("oracle");
    }

    // The value under an unknown key may be any JSON, a number with a fraction or an exponent included.
    [Fact]
    public void Read_UnknownKeyAtEveryLevel_IsSkippedWithItsValue()
    {
        const string Unknown = "'future': {'a': [1.5, -2e10, {'b': null}], 'name': 'x', 'ordinal': 'x'}, ";
        var text = Q(Valid)
            .Replace(Q("'formatVersion'"), Q(Unknown + "'formatVersion'"), StringComparison.Ordinal)
            .Replace(Q("'hash'"), Q(Unknown + "'hash'"), StringComparison.Ordinal)
            .Replace(Q("'schema': 'public', 'table': 'users'}"), Q(Unknown + "'schema': 'public', 'table': 'users'}"), StringComparison.Ordinal)
            .Replace(Q("'name': 'id'"), Q(Unknown + "'name': 'id'"), StringComparison.Ordinal)
            .Replace(Q("'name': 'integer'"), Q(Unknown + "'name': 'integer'"), StringComparison.Ordinal)
            .Replace(Q("'ordinal': 0, 'name'"), Q(Unknown + "'ordinal': 0, 'name'"), StringComparison.Ordinal)
            .Replace(Q("'name': 'text'"), Q(Unknown + "'name': 'text'"), StringComparison.Ordinal)
            .Replace(Q("'column': 'tags'"), Q(Unknown + "'column': 'tags'"), StringComparison.Ordinal);

        WithoutSpans(ShouldBeRead(text)).ShouldBe(WithoutSpans(ShouldBeRead(Q(Valid))));
    }

    // One array before the engine and one after it: the first is passed over and the second read where it stands.
    [Theory]
    [InlineData(", 'parameters': " + ParametersJson, "'parameters': " + ParametersJson + ", ")]
    [InlineData(", 'columns': " + ColumnsJson, "'columns': " + ColumnsJson + ", ")]
    public void Read_OneArrayBeforeTheEngine_ReadsTheSameModel(string old, string moved)
    {
        var text = Change(old, string.Empty).Replace(Q("{'hash'"), Q("{" + moved + "'hash'"), StringComparison.Ordinal);

        WithoutSpans(ShouldBeRead(text)).ShouldBe(WithoutSpans(ShouldBeRead(Q(Valid))));
    }

    // A mistake in an array that was passed over is found as one in an array read where it stands is.
    [Theory]
    [InlineData("null", "[]", "rows", "WrongType", "null, 'columns'", 4, "parameters")]
    [InlineData("{}", "[]", "rows", "WrongType", "{}, 'columns'", 2, "parameters")]
    [InlineData("[5]", "[]", "rows", "WrongType", "5], 'columns'", 1, "parameters")]
    [InlineData("[]", "null", "rows", "WrongType", "null, 'engine'", 4, "columns")]
    [InlineData("[]", "null", "none", "WrongType", "null, 'engine'", 4, "columns")]
    [InlineData("[]", "['c']", "rows", "WrongType", "'c'], 'engine'", 3, "columns")]
    [InlineData("[]", "[]", "none", "ColumnsWithoutRows", "'columns'", 9, null)]
    [InlineData("[{'name': 'a', 'ordinal': 1, 'nullable': null}]", "[]", "rows", "OrdinalMismatch", "1, 'nullable'", 1, "1")]
    [InlineData("[{'name': 'a', 'nullable': null}]", "[]", "rows", "MissingKey", "{'name': 'a'", 1, "ordinal")]
    [InlineData("[]", "[{'ordinal': 0, 'name': 'c', 'type': {'kind': 'base'}, 'nullable': null}]", "rows", "MissingKey", "{'kind'", 1, "name")]
    public void Read_MistakeInAnArrayBeforeTheEngine_IsFound(
        string parameters,
        string columns,
        string resultKind,
        string kind,
        string at,
        int length,
        string? argument
    )
    {
        var text = Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'resultKind': '"
                + resultKind
                + "', 'parameters': "
                + parameters
                + ", 'columns': "
                + columns
                + ", 'engine': 'postgres'}}}"
        );

        ShouldBeMalformed(text, Enum.Parse<SidecarErrorKind>(kind), at, length, argument);
    }

    // The types of an entry need its engine, and the engine may be the last key.
    [Fact]
    public void Read_KeysInAnotherOrder_ReadsTheSameModel() =>
        WithoutSpans(ShouldBeRead(Reordered)).ShouldBe(WithoutSpans(ShouldBeRead(Q(Valid))));

    private static string CreateReordered()
    {
        var text = Q(
            "{'queries': {'Q': {'columns': "
                + "[{'computed': false, 'identity': false"
                + ", 'origin': {'column': 'tags', 'table': 'users', 'schema': 'public'}"
                + ", 'nullableSource': 'catalog', 'nullable': true, 'type': {'element': {'internalName': 'text'"
                + ", 'schema': 'pg_catalog', 'kind': 'base', 'name': 'text'}, 'internalName': '_text'"
                + ", 'schema': 'pg_catalog', 'kind': 'array', 'name': 'text[]'}, 'name': 'tags', 'ordinal': 0}]"
                + ", 'parameters': [{'typeSource': 'inferred', 'nullable': null, 'type': {'internalName': 'int4'"
                + ", 'schema': 'pg_catalog', 'kind': 'base', 'name': 'integer'}, 'ordinal': 0, 'name': 'id'}]"
                + ", 'tableMatch': 'matched', 'plan': 'walked', 'matchesTable': {'table': 'users', 'schema': 'public'}"
                + ", 'resultKind': 'rows', 'serverVersion': '16.4', 'database': 'app', 'engine': 'postgres'"
                + ", 'hash': 'h'}}, 'toolVersion': '1.2.3', 'formatVersion': 1}"
        );
        return text;
    }

    [Fact]
    public void Read_SqlServerType_ReadsItsFacetsAndItsUserType()
    {
        const string Type =
            "{'name': 'geography', 'maxLength': -1, 'precision': 0, 'scale': 0, 'userType': {'schema': 'sys'"
            + ", 'name': 'geography', 'assemblyQualifiedName': 'Microsoft.SqlServer.Types.SqlGeography'}}";

        var column = ShouldBeRead(FileOf("mssql", Type)).Queries[0].Columns.ShouldNotBeNull()[0];

        column.Type.ShouldBe(
            SqlServer(
                "geography",
                -1,
                0,
                0,
                new SqlServerUserType("sys", "geography", "Microsoft.SqlServer.Types.SqlGeography")
            )
        );
    }

    [Fact]
    public void Read_EntryWithoutRows_HasNoColumns()
    {
        var text = Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'engine': 'mssql'"
                + ", 'database': 'app', 'serverVersion': '16.4', 'resultKind': 'none', 'parameters': []}}}"
        );

        WithoutSpans(ShouldBeRead(text))
            .ShouldBe(
                TestSidecars.SidecarOf(
                    Entry(engine: SidecarValues.Engine.SqlServer, resultKind: SidecarResultKind.None)
                )
            );
    }

    [Fact]
    public void Read_NoQueries_IsAnEmptySidecar() =>
        ShouldBeRead(Q("{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {}}")).Queries.Count.ShouldBe(0);

    // A file with one column of one type, under an engine.
    private static string FileOf(string engine, string type) =>
        Q(
            "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {'Q': {'hash': 'h', 'engine': '"
                + engine
                + "', 'resultKind': 'rows', 'parameters': [], 'columns': [{'ordinal': 0, 'name': 'c', 'type': "
                + type
                + ", 'nullable': null}]}}}"
        );

    private static string Q(string json) => json.Replace('\'', '"');

    // Valid with one snippet replaced.  The snippet must be there exactly once, so that a test cannot go on passing
    // after Valid changes under it.
    private static string Change(string old, string replacement)
    {
        var first = Valid.IndexOf(old, StringComparison.Ordinal);
        first.ShouldBeGreaterThanOrEqualTo(0, old);
        Valid.IndexOf(old, first + 1, StringComparison.Ordinal).ShouldBe(-1, old);
        return Q(Valid.Replace(old, replacement, StringComparison.Ordinal));
    }

    private static Sidecar ShouldBeRead(string text)
    {
        var result = SidecarReader.Read(text);

        result.Error.ShouldBeNull();
        result.FormatVersion.ShouldBe(SidecarFormat.Version);
        return result.Sidecar.ShouldNotBeNull();
    }

    // The span starts where `at` is first found in the text.
    private static void ShouldBeMalformed(string text, SidecarErrorKind kind, string at, int length, string? argument)
    {
        var start = text.IndexOf(Q(at), StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, at);

        var result = SidecarReader.Read(text);

        result.Sidecar.ShouldBeNull();
        result.FormatVersion.ShouldBeNull();
        result.Error.ShouldBe(new SidecarError(kind, new TextSpan(start, length), argument));
    }
}
```

Some rows of the theories hold a string longer than a line allows.  Break each with `+` at a comma of its JSON, as the constants at the top are; the Global Constraints say why.

`tests/SqlSource.Tests/Snapshot/SidecarReaderRobustnessTests.cs`:

```csharp
using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

// A sidecar is a committed file that people merge, reformat and cut short.  Nothing that happens to one may make
// the reader throw: an exception in the generator costs every type its generated code.
public partial class SidecarReaderRobustnessTests
{
    // The usual way a sidecar becomes unreadable.
    [Fact]
    public void Read_FileWithAMergeConflict_IsInvalidJsonAtTheMarker()
    {
        const string Line = "  \"toolVersion\": \"0.4.0\",\n";
        var text = SidecarExamples
            .Read(SidecarExamples.Users)
            .Replace(
                Line,
                "<<<<<<< HEAD\n" + Line + "=======\n  \"toolVersion\": \"0.5.0\",\n>>>>>>> theirs\n",
                StringComparison.Ordinal
            );

        var result = SidecarReader.Read(text);

        result.Error.ShouldBe(
            new SidecarError(
                SidecarErrorKind.InvalidJson,
                new TextSpan(text.IndexOf("<<<<<<<", StringComparison.Ordinal), 1),
                null
            )
        );
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileCutShortAnywhere_IsInvalidJson(string name)
    {
        var text = SidecarExamples.Read(name).TrimEnd();

        for (var length = 0; length < text.Length; length++)
        {
            var result = SidecarReader.Read(text.Substring(0, length));

            result.Error.ShouldNotBeNull().Kind.ShouldBe(SidecarErrorKind.InvalidJson, $"at length {length}");
        }
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileDamagedAnywhere_ReturnsOneOfItsThreeStates(string name)
    {
        const string Replacements = "\"{}[]:,\\0x \n-.eu";
        var text = SidecarExamples.Read(name);

        for (var offset = 0; offset < text.Length; offset += 13)
        {
            ShouldBeOneState(SidecarReader.Read(text.Remove(offset, 1)));
            foreach (var replacement in Replacements)
            {
                var damaged = text.Substring(0, offset) + replacement + text.Substring(offset + 1);

                ShouldBeOneState(SidecarReader.Read(damaged));
            }
        }
    }

    // The examples are in the tool's order, where an entry's arrays are read where they stand.  This file is in the
    // opposite order, where they are passed over and read later, a path no file of the tool takes.
    [Fact]
    public void Read_ReorderedFileCutShortOrDamagedAnywhere_ReturnsOneOfItsThreeStates()
    {
        const string Replacements = "\"{}[]:,\\0x \n-.eu";
        var text = SidecarReaderTests.Reordered;
        SidecarReader.Read(text).Sidecar.ShouldNotBeNull();

        for (var offset = 0; offset < text.Length; offset++)
        {
            SidecarReader
                .Read(text.Substring(0, offset))
                .Error.ShouldNotBeNull()
                .Kind.ShouldBe(SidecarErrorKind.InvalidJson, $"at length {offset}");
            ShouldBeOneState(SidecarReader.Read(text.Remove(offset, 1)));
            foreach (var replacement in Replacements)
            {
                var damaged = text.Substring(0, offset) + replacement + text.Substring(offset + 1);

                ShouldBeOneState(SidecarReader.Read(damaged));
            }
        }
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileAnEditorRewrote_ReadsTheSameModel(string name)
    {
        var text = SidecarExamples.Read(name);
        var expected = WithoutSpans(SidecarReader.Read(text).Sidecar.ShouldNotBeNull());

        Reread(text.Replace("\n", "\r\n", StringComparison.Ordinal)).ShouldBe(expected);
        Reread(Indentation().Replace(text, match => new string('\t', match.Length / 2))).ShouldBe(expected);
        Reread(LineBreaks().Replace(text, string.Empty)).ShouldBe(expected);
        Reread("\n\n" + text + "\n\n").ShouldBe(expected);
    }

    private static Sidecar Reread(string text) => WithoutSpans(SidecarReader.Read(text).Sidecar.ShouldNotBeNull());

    private static void ShouldBeOneState(SidecarReadResult result)
    {
        if (result.Error is not null)
        {
            result.Sidecar.ShouldBeNull();
            result.FormatVersion.ShouldBeNull();
            return;
        }

        result.FormatVersion.ShouldNotBeNull();
        (result.Sidecar is not null).ShouldBe(result.FormatVersion == SidecarFormat.Version);
    }

    [GeneratedRegex("^ +", RegexOptions.Multiline)]
    private static partial Regex Indentation();

    [GeneratedRegex("\n *")]
    private static partial Regex LineBreaks();
}
```

`tests/SqlSource.Tests/Snapshot/SidecarReaderAllocationTests.cs`.  The budget is measured in step 5; write `1000` until then:

```csharp
using System;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public class SidecarReaderAllocationTests
{
    // From phase 5 the generator reads a sidecar again each time the file changes, so what a read allocates is
    // tracked here.  The budget leaves room for differences between runtimes, not for a regression: lower it when
    // the reader improves, and do not raise it to make a change pass.
    private const double BudgetInBytesPerCharacter = 1000;

    [Fact]
    public void Read_UsersExample_AllocatesWithinItsBudget()
    {
        const int Iterations = 20;
        var text = SidecarExamples.Read(SidecarExamples.Users);
        SidecarReader.Read(text).Sidecar.ShouldNotBeNull().Queries.Count.ShouldBe(4);

        // The first reads pay for one-off work: JIT compilation and static initialisers.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SidecarReader.Read(text);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SidecarReader.Read(text);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(BudgetInBytesPerCharacter);
    }
}
```

- [ ] **Step 3: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0246 and CS0103 for `SidecarReader`, `SidecarError`, `SidecarErrorKind` and `SidecarReadResult`.

- [ ] **Step 4: Write the reader**

`src/SqlSource/Snapshot/SidecarErrorKind.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// The ways a sidecar is malformed.  Each member says what <see cref="SidecarError.Span" /> and
/// <see cref="SidecarError.Argument" /> hold for it.
/// </summary>
internal enum SidecarErrorKind
{
    /// <summary>
    /// The text is not one JSON object, or is nested deeper than <see cref="SidecarTokenizer.MaxDepth" /> levels.
    /// Span: the character that cannot be read or the token that breaks the grammar, empty at the end of a text that
    /// stops early.  No argument.
    /// </summary>
    InvalidJson,

    /// <summary>
    /// A key that a reader cannot do without is absent.  Span: the opening brace of the object.  Argument: the key.
    /// </summary>
    MissingKey,

    /// <summary>
    /// A key the reader reads holds a value of another JSON type, a number that is not a 32-bit integer included.
    /// Span: the whole value.  Argument: the key; the query's name for an entry; the array's key for an element.
    /// </summary>
    WrongType,

    /// <summary>
    /// A key the reader reads, or the name of a query, is written twice in one object.  Span: the second key, quotes
    /// included.  Argument: the key.
    /// </summary>
    DuplicateKey,

    /// <summary>
    /// An <c>ordinal</c> is not the index of its element.  Span: the number.  Argument: the number as written.
    /// </summary>
    OrdinalMismatch,

    /// <summary>
    /// <c>columns</c> is present and <c>resultKind</c> is <c>none</c>.  Span: the <c>columns</c> key, quotes
    /// included.  No argument.
    /// </summary>
    ColumnsWithoutRows,

    /// <summary>
    /// <c>resultKind</c> is not <c>rows</c> or <c>none</c>.  Span: the string, quotes included.  Argument: the
    /// value.
    /// </summary>
    UnknownResultKind,
}
```

`src/SqlSource/Snapshot/SidecarError.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>Why a sidecar is malformed.  There is one for a file: the first that the reader finds.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the text that was read.</param>
/// <param name="Argument">The text a message quotes, when the kind has one.</param>
internal sealed record SidecarError(SidecarErrorKind Kind, TextSpan Span, string? Argument);
```

`src/SqlSource/Snapshot/SidecarReadResult.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// What reading a sidecar gave, in one of three states: read, with the sidecar and the format version of this
/// reader; of another format version, with that version alone; or malformed, with the error alone.
/// </summary>
/// <param name="Sidecar">The sidecar, when it was read.</param>
/// <param name="FormatVersion">The file's format version, unless the file is malformed.</param>
/// <param name="Error">Why the file is malformed.</param>
internal sealed record SidecarReadResult(Sidecar? Sidecar, int? FormatVersion, SidecarError? Error)
{
    public static SidecarReadResult Of(Sidecar sidecar) => new(sidecar, sidecar.FormatVersion, null);

    public static SidecarReadResult OfAnotherVersion(int formatVersion) => new(null, formatVersion, null);

    public static SidecarReadResult Malformed(SidecarError error) => new(null, null, error);
}
```

`src/SqlSource/Snapshot/SidecarCursor.cs`:

```csharp
using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Walks a text that <see cref="SidecarReader" /> has found to be one JSON object, and keeps the first error.  It
/// checks no grammar: after a key comes a colon, and after a value a comma or the end of its container.
/// </summary>
/// <remarks>
/// Once an error is recorded <see cref="Next" /> returns the end of the text, so every loop above ends by itself and
/// no caller needs to check after each step.
/// </remarks>
internal sealed class SidecarCursor(string text)
{
    private SidecarTokenizer _tokens = new(text);

    public string Text { get; } = text;

    /// <summary>The first error, or null.</summary>
    public SidecarError? Error { get; private set; }

    /// <summary>The span of the key that <see cref="NextKey" /> returned last, quotes included.</summary>
    public TextSpan KeySpan { get; private set; }

    /// <summary>The offset after the last token.  Setting it moves the cursor: a value may be read out of order.</summary>
    public int Position
    {
        get => _tokens.Position;
        set => _tokens = new SidecarTokenizer(Text, value);
    }

    public SidecarToken Next() =>
        Error is null ? _tokens.Next() : new SidecarToken(SidecarTokenKind.End, new TextSpan(Text.Length, 0));

    /// <summary>Records an error, unless there is one already.</summary>
    public void Fail(SidecarErrorKind kind, TextSpan span, string? argument = null) =>
        Error ??= new SidecarError(kind, span, argument);

    public string GetString(SidecarToken token) => SidecarTokenizer.GetString(Text, token);

    /// <summary>Moves past the value that starts with <paramref name="first" /> and returns its span.</summary>
    public TextSpan Skip(SidecarToken first)
    {
        if (Error is not null)
        {
            return default;
        }

        // The grammar and the depth were checked before any cursor was made.
        _ = _tokens.TrySkipValue(first, 0, out _);
        return TextSpan.FromBounds(first.Span.Start, _tokens.Position);
    }

    /// <summary>
    /// Reads the next key of the object being read, and its colon.  False at the object's end.
    /// </summary>
    public bool NextMember(out SidecarToken key)
    {
        key = Next();
        if (key.Kind == SidecarTokenKind.Comma)
        {
            key = Next();
        }

        if (key.Kind != SidecarTokenKind.String)
        {
            return false;
        }

        _ = Next();
        return true;
    }

    /// <summary>
    /// Reads to the next key of the object that is one of <paramref name="keys" />, skipping every other key with
    /// its value, and returns it; null at the object's end.  A key found twice is an error.
    /// <paramref name="seen" /> has a bit for each key found so far, by its index in <paramref name="keys" />.
    /// </summary>
    public string? NextKey(string[] keys, ref int seen)
    {
        while (NextMember(out var key))
        {
            var index = IndexOf(keys, key);
            if (index < 0)
            {
                _ = Skip(Next());
                continue;
            }

            if ((seen & (1 << index)) != 0)
            {
                Fail(SidecarErrorKind.DuplicateKey, key.Span, keys[index]);
                return null;
            }

            seen |= 1 << index;
            KeySpan = key.Span;
            return keys[index];
        }

        return null;
    }

    /// <summary>Reads the first token of the next element of the array being read.  False at the array's end.</summary>
    public bool NextElement(out SidecarToken first)
    {
        first = Next();
        if (first.Kind == SidecarTokenKind.Comma)
        {
            first = Next();
        }

        return first.Kind is not (SidecarTokenKind.ArrayEnd or SidecarTokenKind.End);
    }

    /// <summary>Whether <see cref="NextKey" /> found <paramref name="key" />, by the bits it keeps.</summary>
    public static bool Has(string[] keys, int seen, string key) => (seen & (1 << Array.IndexOf(keys, key))) != 0;

    /// <summary>Records <see cref="SidecarErrorKind.MissingKey" /> unless <paramref name="key" /> was found.</summary>
    public void Require(string[] keys, int seen, TextSpan brace, string key)
    {
        if (!Has(keys, seen, key))
        {
            Fail(SidecarErrorKind.MissingKey, brace, key);
        }
    }

    /// <summary>Records <see cref="SidecarErrorKind.WrongType" /> at the value that starts with the token.</summary>
    public void WrongType(SidecarToken first, string key) => Fail(SidecarErrorKind.WrongType, Skip(first), key);

    /// <summary>Reads a string, or <c>null</c> where <paramref name="orNull" /> allows it.</summary>
    public string? ReadString(string key, bool orNull)
    {
        var token = Next();
        if (token.Kind == SidecarTokenKind.String)
        {
            return GetString(token);
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return null;
    }

    /// <summary>Reads a 32-bit integer, or <c>null</c> where <paramref name="orNull" /> allows it.</summary>
    public int? ReadInt32(string key, bool orNull)
    {
        var token = Next();
        if (SidecarTokenizer.TryGetInt32(Text, token, out var value))
        {
            return value;
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return null;
    }

    /// <summary>Reads <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    public bool? ReadBoolean(string key)
    {
        var token = Next();
        switch (token.Kind)
        {
            case SidecarTokenKind.True:
                return true;
            case SidecarTokenKind.False:
                return false;
            case SidecarTokenKind.Null:
                return null;
            default:
                WrongType(token, key);
                return null;
        }
    }

    /// <summary>
    /// Reads the opening brace of an object, whose members are then read with <see cref="NextKey" />.  False for
    /// <c>null</c> where <paramref name="orNull" /> allows it, and for any other value, which is an error.
    /// </summary>
    public bool BeginObject(string key, bool orNull, out TextSpan brace) =>
        Begin(SidecarTokenKind.ObjectStart, key, orNull, out brace);

    /// <summary>Reads the opening bracket of an array, as <see cref="BeginObject" /> does a brace.</summary>
    public bool BeginArray(string key, bool orNull) => Begin(SidecarTokenKind.ArrayStart, key, orNull, out _);

    /// <summary>
    /// Moves past an array without reading it and returns the offset to read it from later, with
    /// <see cref="BeginArray" /> first; -1, and an error, for any other value.
    /// </summary>
    public int DeferArray(string key)
    {
        var position = Position;
        var first = Next();
        if (first.Kind != SidecarTokenKind.ArrayStart)
        {
            WrongType(first, key);
            return -1;
        }

        _ = Skip(first);
        return position;
    }

    private bool Begin(SidecarTokenKind kind, string key, bool orNull, out TextSpan brace)
    {
        var token = Next();
        brace = token.Span;
        if (token.Kind == kind)
        {
            return true;
        }

        if (!(orNull && token.Kind == SidecarTokenKind.Null))
        {
            WrongType(token, key);
        }

        return false;
    }

    private int IndexOf(string[] keys, SidecarToken key)
    {
        for (var index = 0; index < keys.Length; index++)
        {
            if (SidecarTokenizer.StringEquals(Text, key, keys[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
```

`src/SqlSource/Snapshot/SidecarReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads a sidecar, by the reader rules of the sidecar format design.  It never throws, whatever the text: a file a
/// user can commit must not cost every type its generated code.
/// </summary>
/// <remarks>
/// Two passes.  The first checks that the text is one JSON object nested at most
/// <see cref="SidecarTokenizer.MaxDepth" /> levels.  The second reads <c>formatVersion</c> before anything else: a
/// reader of this format cannot say whether a file of another is well formed, so such a file is read as its version
/// alone.  Then it reads the model.  An unknown key is skipped with its value at every level, a key the format marks
/// "always" that is absent is read as null, and the first error ends the read.  A file the tool wrote is tokenized
/// twice and no more: keep it so.
/// </remarks>
internal static class SidecarReader
{
    private static readonly string[] VersionKeys = [SidecarKeys.FormatVersion];

    private static readonly string[] TopKeys =
    [
        SidecarKeys.FormatVersion,
        SidecarKeys.ToolVersion,
        SidecarKeys.Queries,
    ];

    private static readonly string[] EntryKeys =
    [
        SidecarKeys.Hash,
        SidecarKeys.Engine,
        SidecarKeys.Database,
        SidecarKeys.ServerVersion,
        SidecarKeys.ResultKind,
        SidecarKeys.MatchesTable,
        SidecarKeys.Plan,
        SidecarKeys.TableMatch,
        SidecarKeys.Parameters,
        SidecarKeys.Columns,
    ];

    private static readonly string[] TableKeys = [SidecarKeys.Schema, SidecarKeys.Table];

    private static readonly string[] ParameterKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.Ordinal,
        SidecarKeys.Type,
        SidecarKeys.Nullable,
        SidecarKeys.TypeSource,
    ];

    private static readonly string[] ColumnKeys =
    [
        SidecarKeys.Ordinal,
        SidecarKeys.Name,
        SidecarKeys.Type,
        SidecarKeys.Nullable,
        SidecarKeys.NullableSource,
        SidecarKeys.Origin,
        SidecarKeys.Identity,
        SidecarKeys.Computed,
    ];

    private static readonly string[] OriginKeys = [SidecarKeys.Schema, SidecarKeys.Table, SidecarKeys.Column];

    /// <summary>
    /// Reads <paramref name="text" />, the text of a <c>.sql.json</c> file without a byte order mark.
    /// </summary>
    public static SidecarReadResult Read(string text)
    {
        if (CheckGrammar(text) is { } invalid)
        {
            return SidecarReadResult.Malformed(invalid);
        }

        var cursor = new SidecarCursor(text);
        var version = ReadVersion(cursor);
        if (cursor.Error is { } versionError)
        {
            return SidecarReadResult.Malformed(versionError);
        }

        if (version != SidecarFormat.Version)
        {
            return SidecarReadResult.OfAnotherVersion(version);
        }

        cursor.Position = 0;
        return ReadSidecar(cursor);
    }

    private static SidecarError? CheckGrammar(string text)
    {
        var tokens = new SidecarTokenizer(text);
        var first = tokens.Next();
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            return Invalid(first.Span);
        }

        if (!tokens.TrySkipValue(first, 0, out var problem))
        {
            return Invalid(problem);
        }

        var after = tokens.Next();
        return after.Kind == SidecarTokenKind.End ? null : Invalid(after.Span);
    }

    private static SidecarError Invalid(TextSpan span) => new(SidecarErrorKind.InvalidJson, span, null);

    // Reads to the first formatVersion.  When it is this reader's, the read stops there: the tool writes the key
    // third, so nothing of the queries has been passed over, and ReadSidecar starts again from the first brace and
    // finds a second formatVersion itself.  When it is another's, the read goes on to the end of the object, since
    // a second formatVersion is a mistake in any version and nothing else of such a file is judged.
    private static int ReadVersion(SidecarCursor cursor)
    {
        var brace = cursor.Next().Span;
        var seen = 0;
        var version = 0;
        while (cursor.NextKey(VersionKeys, ref seen) is not null)
        {
            version = cursor.ReadInt32(SidecarKeys.FormatVersion, orNull: false) ?? 0;
            if (version == SidecarFormat.Version)
            {
                return version;
            }
        }

        cursor.Require(VersionKeys, seen, brace, SidecarKeys.FormatVersion);
        return version;
    }

    private static SidecarReadResult ReadSidecar(SidecarCursor cursor)
    {
        var brace = cursor.Next().Span;
        var seen = 0;
        string? toolVersion = null;
        var queries = EquatableArray<SidecarEntry>.Empty;
        for (var key = cursor.NextKey(TopKeys, ref seen); key is not null; key = cursor.NextKey(TopKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.FormatVersion:
                    // ReadVersion read it.  It is a key here so that a second one is a duplicate.
                    _ = cursor.Skip(cursor.Next());
                    break;
                case SidecarKeys.ToolVersion:
                    toolVersion = cursor.ReadString(key, orNull: false);
                    break;
                default:
                    queries = ReadQueries(cursor);
                    break;
            }
        }

        cursor.Require(TopKeys, seen, brace, SidecarKeys.ToolVersion);
        cursor.Require(TopKeys, seen, brace, SidecarKeys.Queries);
        return cursor.Error is { } error
            ? SidecarReadResult.Malformed(error)
            : SidecarReadResult.Of(new Sidecar(SidecarFormat.Version, toolVersion ?? string.Empty, queries));
    }

    private static EquatableArray<SidecarEntry> ReadQueries(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.Queries, orNull: false, out _))
        {
            return EquatableArray<SidecarEntry>.Empty;
        }

        var entries = ImmutableArray.CreateBuilder<SidecarEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (cursor.NextMember(out var key))
        {
            var name = cursor.GetString(key);
            if (!names.Add(name))
            {
                cursor.Fail(SidecarErrorKind.DuplicateKey, key.Span, name);
            }
            else if (ReadEntry(cursor, name, key.Span) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return new EquatableArray<SidecarEntry>(entries.ToImmutable());
    }

    // The types of an entry's parameters and columns have the shape its engine picks.  The tool writes the engine
    // before them, and then they are read where they stand.  Key order is not significant to a reader, though: an
    // array that comes before the engine is passed over, and read once the object has ended.
    private static SidecarEntry? ReadEntry(SidecarCursor cursor, string name, TextSpan nameSpan)
    {
        if (!cursor.BeginObject(name, orNull: false, out var brace))
        {
            return null;
        }

        var seen = 0;
        string? hash = null;
        string? engine = null;
        string? database = null;
        string? serverVersion = null;
        SidecarResultKind? resultKind = null;
        SidecarTable? matchesTable = null;
        string? plan = null;
        string? tableMatch = null;
        var parameters = EquatableArray<SidecarParameter>.Empty;
        EquatableArray<SidecarColumn>? columns = null;
        var parametersAt = -1;
        var columnsAt = -1;
        var columnsKey = default(TextSpan);
        for (var key = cursor.NextKey(EntryKeys, ref seen); key is not null; key = cursor.NextKey(EntryKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Hash:
                    hash = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Engine:
                    engine = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Database:
                    database = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ServerVersion:
                    serverVersion = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ResultKind:
                    resultKind = ReadResultKind(cursor);
                    break;
                case SidecarKeys.MatchesTable:
                    matchesTable = ReadTable(cursor);
                    break;
                case SidecarKeys.Plan:
                    plan = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.TableMatch:
                    tableMatch = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Parameters:
                    parametersAt = ReadParametersOrDefer(cursor, engine, ref parameters);
                    break;
                default:
                    columnsKey = cursor.KeySpan;
                    columnsAt = ReadColumnsOrDefer(cursor, engine, ref columns);
                    break;
            }
        }

        RequireEntryKeys(cursor, seen, brace, resultKind);
        if (resultKind == SidecarResultKind.None && SidecarCursor.Has(EntryKeys, seen, SidecarKeys.Columns))
        {
            cursor.Fail(SidecarErrorKind.ColumnsWithoutRows, columnsKey);
        }

        if (cursor.Error is not null || hash is null || engine is null || resultKind is not { } kind)
        {
            return null;
        }

        ReadDeferred(cursor, engine, parametersAt, columnsAt, ref parameters, ref columns);
        if (cursor.Error is not null)
        {
            return null;
        }

        return new SidecarEntry(
            name,
            nameSpan,
            hash,
            engine,
            database,
            serverVersion,
            kind,
            matchesTable,
            plan,
            tableMatch,
            parameters,
            columns
        );
    }

    // Reads the parameters where they stand when the entry's engine is known.  Otherwise passes over them and
    // returns the offset to read them from; -1 when there is nothing left to read.
    private static int ReadParametersOrDefer(
        SidecarCursor cursor,
        string? engine,
        ref EquatableArray<SidecarParameter> parameters
    )
    {
        if (engine is null)
        {
            return cursor.DeferArray(SidecarKeys.Parameters);
        }

        parameters = ReadParameters(cursor, engine);
        return -1;
    }

    private static int ReadColumnsOrDefer(
        SidecarCursor cursor,
        string? engine,
        ref EquatableArray<SidecarColumn>? columns
    )
    {
        if (engine is null)
        {
            return cursor.DeferArray(SidecarKeys.Columns);
        }

        columns = ReadColumns(cursor, engine);
        return -1;
    }

    // Reads the arrays that were passed over, and comes back to the end of the entry.
    private static void ReadDeferred(
        SidecarCursor cursor,
        string engine,
        int parametersAt,
        int columnsAt,
        ref EquatableArray<SidecarParameter> parameters,
        ref EquatableArray<SidecarColumn>? columns
    )
    {
        var end = cursor.Position;
        if (parametersAt >= 0)
        {
            cursor.Position = parametersAt;
            parameters = ReadParameters(cursor, engine);
        }

        if (columnsAt >= 0)
        {
            cursor.Position = columnsAt;
            columns = ReadColumns(cursor, engine);
        }

        cursor.Position = end;
    }

    private static void RequireEntryKeys(SidecarCursor cursor, int seen, TextSpan brace, SidecarResultKind? resultKind)
    {
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Hash);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Engine);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.ResultKind);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Parameters);
        if (resultKind == SidecarResultKind.Rows)
        {
            cursor.Require(EntryKeys, seen, brace, SidecarKeys.Columns);
        }
    }

    private static SidecarResultKind? ReadResultKind(SidecarCursor cursor)
    {
        var token = cursor.Next();
        if (token.Kind != SidecarTokenKind.String)
        {
            cursor.WrongType(token, SidecarKeys.ResultKind);
            return null;
        }

        if (SidecarTokenizer.StringEquals(cursor.Text, token, SidecarValues.ResultKind.Rows))
        {
            return SidecarResultKind.Rows;
        }

        if (SidecarTokenizer.StringEquals(cursor.Text, token, SidecarValues.ResultKind.None))
        {
            return SidecarResultKind.None;
        }

        cursor.Fail(SidecarErrorKind.UnknownResultKind, token.Span, cursor.GetString(token));
        return null;
    }

    private static SidecarTable? ReadTable(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.MatchesTable, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? table = null;
        for (var key = cursor.NextKey(TableKeys, ref seen); key is not null; key = cursor.NextKey(TableKeys, ref seen))
        {
            if (key == SidecarKeys.Schema)
            {
                schema = cursor.ReadString(key, orNull: true);
            }
            else
            {
                table = cursor.ReadString(key, orNull: true);
            }
        }

        return new SidecarTable(schema, table);
    }

    private static EquatableArray<SidecarParameter> ReadParameters(SidecarCursor cursor, string engine)
    {
        if (!cursor.BeginArray(SidecarKeys.Parameters, orNull: false))
        {
            return EquatableArray<SidecarParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SidecarParameter>();
        while (cursor.NextElement(out var first))
        {
            if (ReadParameter(cursor, first, engine, parameters.Count) is { } parameter)
            {
                parameters.Add(parameter);
            }
        }

        return new EquatableArray<SidecarParameter>(parameters.ToImmutable());
    }

    private static SidecarParameter? ReadParameter(SidecarCursor cursor, SidecarToken first, string engine, int index)
    {
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            cursor.WrongType(first, SidecarKeys.Parameters);
            return null;
        }

        var seen = 0;
        string? name = null;
        SidecarType? type = null;
        bool? nullable = null;
        string? typeSource = null;
        for (
            var key = cursor.NextKey(ParameterKeys, ref seen);
            key is not null;
            key = cursor.NextKey(ParameterKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Ordinal:
                    ReadOrdinal(cursor, index);
                    break;
                case SidecarKeys.Type:
                    type = SidecarTypeReader.Read(cursor, engine, orNull: true);
                    break;
                case SidecarKeys.Nullable:
                    nullable = cursor.ReadBoolean(key);
                    break;
                default:
                    typeSource = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Name);
        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Ordinal);
        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Nullable);
        return cursor.Error is null && name is not null
            ? new SidecarParameter(name, index, type, nullable, typeSource)
            : null;
    }

    private static EquatableArray<SidecarColumn> ReadColumns(SidecarCursor cursor, string engine)
    {
        if (!cursor.BeginArray(SidecarKeys.Columns, orNull: false))
        {
            return EquatableArray<SidecarColumn>.Empty;
        }

        var columns = ImmutableArray.CreateBuilder<SidecarColumn>();
        while (cursor.NextElement(out var first))
        {
            if (ReadColumn(cursor, first, engine, columns.Count) is { } column)
            {
                columns.Add(column);
            }
        }

        return new EquatableArray<SidecarColumn>(columns.ToImmutable());
    }

    private static SidecarColumn? ReadColumn(SidecarCursor cursor, SidecarToken first, string engine, int index)
    {
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            cursor.WrongType(first, SidecarKeys.Columns);
            return null;
        }

        var seen = 0;
        string? name = null;
        SidecarType? type = null;
        bool? nullable = null;
        string? nullableSource = null;
        SidecarOrigin? origin = null;
        bool? identity = null;
        bool? computed = null;
        for (var key = cursor.NextKey(ColumnKeys, ref seen); key is not null; key = cursor.NextKey(ColumnKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Ordinal:
                    ReadOrdinal(cursor, index);
                    break;
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Type:
                    type = SidecarTypeReader.Read(cursor, engine, orNull: false);
                    break;
                case SidecarKeys.Nullable:
                    nullable = cursor.ReadBoolean(key);
                    break;
                case SidecarKeys.NullableSource:
                    nullableSource = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Origin:
                    origin = ReadOrigin(cursor);
                    break;
                case SidecarKeys.Identity:
                    identity = cursor.ReadBoolean(key);
                    break;
                default:
                    computed = cursor.ReadBoolean(key);
                    break;
            }
        }

        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Ordinal);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Name);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Type);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Nullable);
        return cursor.Error is null && name is not null && type is not null
            ? new SidecarColumn(index, name, type, nullable, nullableSource, origin, identity, computed)
            : null;
    }

    // An ordinal is kept in the file so that a reordered list and a hand edit show.  It must be its index, so the
    // model takes the index.
    private static void ReadOrdinal(SidecarCursor cursor, int index)
    {
        var token = cursor.Next();
        if (!SidecarTokenizer.TryGetInt32(cursor.Text, token, out var ordinal))
        {
            cursor.WrongType(token, SidecarKeys.Ordinal);
        }
        else if (ordinal != index)
        {
            cursor.Fail(
                SidecarErrorKind.OrdinalMismatch,
                token.Span,
                cursor.Text.Substring(token.Span.Start, token.Span.Length)
            );
        }
    }

    private static SidecarOrigin? ReadOrigin(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.Origin, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? table = null;
        string? column = null;
        for (var key = cursor.NextKey(OriginKeys, ref seen); key is not null; key = cursor.NextKey(OriginKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Table:
                    table = cursor.ReadString(key, orNull: true);
                    break;
                default:
                    column = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        return new SidecarOrigin(schema, table, column);
    }
}
```

`src/SqlSource/Snapshot/SidecarTypeReader.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads a type object.  The entry's engine picks the shape, and each shape says which keys it cannot do without.
/// </summary>
internal static class SidecarTypeReader
{
    private static readonly string[] PostgresKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.Kind,
        SidecarKeys.Schema,
        SidecarKeys.InternalName,
        SidecarKeys.Length,
        SidecarKeys.Precision,
        SidecarKeys.Scale,
        SidecarKeys.Element,
        SidecarKeys.Base,
        SidecarKeys.Labels,
        SidecarKeys.Subtype,
    ];

    private static readonly string[] SqlServerKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.MaxLength,
        SidecarKeys.Precision,
        SidecarKeys.Scale,
        SidecarKeys.UserType,
    ];

    private static readonly string[] UserTypeKeys =
    [
        SidecarKeys.Schema,
        SidecarKeys.Name,
        SidecarKeys.AssemblyQualifiedName,
    ];

    private static readonly string[] OtherKeys = [SidecarKeys.Name];

    /// <summary>
    /// Reads the value of a <c>type</c> key under <paramref name="engine" />.  Null for <c>null</c> where
    /// <paramref name="orNull" /> allows it, and after an error.
    /// </summary>
    public static SidecarType? Read(SidecarCursor cursor, string engine, bool orNull)
    {
        if (!cursor.BeginObject(SidecarKeys.Type, orNull, out var brace))
        {
            return null;
        }

        return engine switch
        {
            SidecarValues.Engine.Postgres => ReadPostgres(cursor, brace),
            SidecarValues.Engine.SqlServer => ReadSqlServer(cursor, brace),
            _ => ReadOther(cursor, brace),
        };
    }

    // A type inside a type is as deep as the text nests, and the text nests 64 levels at most.
    private static PostgresType? ReadPostgres(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        string? kind = null;
        string? schema = null;
        string? internalName = null;
        int? length = null;
        int? precision = null;
        int? scale = null;
        PostgresType? element = null;
        PostgresType? baseType = null;
        EquatableArray<string>? labels = null;
        PostgresType? subtype = null;
        for (
            var key = cursor.NextKey(PostgresKeys, ref seen);
            key is not null;
            key = cursor.NextKey(PostgresKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Kind:
                    kind = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.InternalName:
                    internalName = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Length:
                    length = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Precision:
                    precision = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Scale:
                    scale = cursor.ReadInt32(key, orNull: true);
                    break;
                case SidecarKeys.Element:
                    element = ReadNested(cursor, key);
                    break;
                case SidecarKeys.Base:
                    baseType = ReadNested(cursor, key);
                    break;
                case SidecarKeys.Labels:
                    labels = ReadLabels(cursor);
                    break;
                default:
                    subtype = ReadNested(cursor, key);
                    break;
            }
        }

        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Name);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Kind);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.Schema);
        cursor.Require(PostgresKeys, seen, brace, SidecarKeys.InternalName);

        if (MissingNestedKey(kind, element, baseType, labels, subtype) is { } missing)
        {
            cursor.Fail(SidecarErrorKind.MissingKey, brace, missing);
        }

        if (cursor.Error is not null || name is null || kind is null || schema is null || internalName is null)
        {
            return null;
        }

        return new PostgresType(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels,
            subtype
        );
    }

    // The key of the nested type that a kind names and the object lacks.  What a kind names is known only once the
    // whole object is read, so one given as null is missing.  A kind the reader does not know names nothing.
    private static string? MissingNestedKey(
        string? kind,
        PostgresType? element,
        PostgresType? baseType,
        EquatableArray<string>? labels,
        PostgresType? subtype
    ) =>
        kind switch
        {
            SidecarValues.Kind.Array when element is null => SidecarKeys.Element,
            SidecarValues.Kind.Domain when baseType is null => SidecarKeys.Base,
            SidecarValues.Kind.Enum when labels is null => SidecarKeys.Labels,
            SidecarValues.Kind.Range or SidecarValues.Kind.Multirange when subtype is null => SidecarKeys.Subtype,
            _ => null,
        };

    private static PostgresType? ReadNested(SidecarCursor cursor, string key) =>
        cursor.BeginObject(key, orNull: true, out var brace) ? ReadPostgres(cursor, brace) : null;

    private static EquatableArray<string>? ReadLabels(SidecarCursor cursor)
    {
        if (!cursor.BeginArray(SidecarKeys.Labels, orNull: true))
        {
            return null;
        }

        var labels = ImmutableArray.CreateBuilder<string>();
        while (cursor.NextElement(out var first))
        {
            if (first.Kind == SidecarTokenKind.String)
            {
                labels.Add(cursor.GetString(first));
            }
            else
            {
                cursor.WrongType(first, SidecarKeys.Labels);
            }
        }

        return new EquatableArray<string>(labels.ToImmutable());
    }

    private static SqlServerType? ReadSqlServer(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        int? maxLength = null;
        int? precision = null;
        int? scale = null;
        SqlServerUserType? userType = null;
        for (
            var key = cursor.NextKey(SqlServerKeys, ref seen);
            key is not null;
            key = cursor.NextKey(SqlServerKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.MaxLength:
                    maxLength = cursor.ReadInt32(key, orNull: false);
                    break;
                case SidecarKeys.Precision:
                    precision = cursor.ReadInt32(key, orNull: false);
                    break;
                case SidecarKeys.Scale:
                    scale = cursor.ReadInt32(key, orNull: false);
                    break;
                default:
                    userType = ReadUserType(cursor);
                    break;
            }
        }

        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Name);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.MaxLength);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Precision);
        cursor.Require(SqlServerKeys, seen, brace, SidecarKeys.Scale);
        return cursor.Error is null && name is not null
            ? new SqlServerType(
                name,
                maxLength.GetValueOrDefault(),
                precision.GetValueOrDefault(),
                scale.GetValueOrDefault(),
                userType
            )
            : null;
    }

    private static SqlServerUserType? ReadUserType(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.UserType, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? name = null;
        string? assemblyQualifiedName = null;
        for (
            var key = cursor.NextKey(UserTypeKeys, ref seen);
            key is not null;
            key = cursor.NextKey(UserTypeKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: true);
                    break;
                default:
                    assemblyQualifiedName = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        return new SqlServerUserType(schema, name, assemblyQualifiedName);
    }

    private static OtherEngineType? ReadOther(SidecarCursor cursor, TextSpan brace)
    {
        var seen = 0;
        string? name = null;
        while (cursor.NextKey(OtherKeys, ref seen) is not null)
        {
            name = cursor.ReadString(SidecarKeys.Name, orNull: false);
        }

        cursor.Require(OtherKeys, seen, brace, SidecarKeys.Name);
        return cursor.Error is null && name is not null ? new OtherEngineType(name) : null;
    }
}
```

- [ ] **Step 5: Run the tests, then measure and pin the budget**

Run each of `SidecarReaderTests`, `SidecarReaderRobustnessTests` and `SidecarReaderAllocationTests` with `--filter-class`.
Expected: PASS.  A row of a theory that fails on its span or its argument is a wrong row or a wrong reader: the table at the top of this task decides which.

Then measure.  In `Read_UsersExample_AllocatesWithinItsBudget`, add this line before the last one, run the test, read the figure from the failure, and remove the line:

```csharp
        (allocated / (double)(Iterations * text.Length)).ShouldBe(0);
```

Set `BudgetInBytesPerCharacter` to the figure times 1.2, rounded up to one decimal place, and add the figure to the comment above it in the words of `SqlFileParserAllocationTests`: `A read of the first example allocated N bytes for each character when the budget was set, the same in Debug and in Release.`  Check that sentence by running the test in Release too: `dotnet test --project tests/SqlSource.Tests --configuration Release --filter-class SqlSource.Tests.Snapshot.SidecarReaderAllocationTests`.  If the two differ, say so in the comment and set the budget from the larger.  Keep the figure for the pull request's description.

- [ ] **Step 6: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/Snapshot tests/SqlSource.Tests .editorconfig .gitattributes
```

```bash
git commit -m "Read a sidecar

Two passes: the grammar and the format version, then the model.  An
unknown key is skipped, a file of another format version is read as its
version alone, and no text makes the reader throw.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The writer and the round trip

**Files:**
- Create in `src/SqlSource/Snapshot/`: `SidecarJsonBuilder.cs`, `SidecarWriter.cs`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj` (the reference to Bogus), `tests/SqlSource.Tests/packages.lock.json`
- Modify: `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (one clause of section 1)
- Test: `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs`, `SidecarFaker.cs`, `SidecarRoundTripTests.cs`

**Interfaces:**
- Consumes: the model, `SidecarKeys`, `SidecarValues`, `SidecarFormat` of task 2; `SidecarReader.Read` of task 4; `TestSidecars`, `SidecarExamples` of tasks 2 and 4.
- Produces, `internal`, namespace `SqlSource.Snapshot`:
  - `static string SidecarWriter.Write(Sidecar sidecar)`: the text of the file, with `\n` line endings and a trailing newline.
  - `sealed class SidecarJsonBuilder` with `BeginObject(string? key)`, `EndObject()`, `BeginArray(string? key)`, `EndArray()`, `WriteString(string? key, string? value)`, `WriteNumber(string? key, int? value)`, `WriteBoolean(string? key, bool? value)`, `WriteNull(string? key)`, `string ToText()` and `static string Quote(string value)`, which task 6 uses.  A null key is an element of an array or the first object.  A null value is written as `null`.
- Produces for tests: `sealed class SidecarFaker(int seed, bool lenient)` with `Sidecar Next()`, and `SidecarRoundTripTests.Samples(bool lenient)`, which task 7 uses.

**What the writer writes for each key**, where the condition decides and not the value:

| Key | Written |
|----|----|
| `_WARNING`, `$schema`, `formatVersion`, `toolVersion`, `queries` | Always, in this order |
| `hash`, `engine`, `database`, `serverVersion`, `resultKind`, `parameters` | Always; `null` for a `database` or a `serverVersion` that has no value |
| `matchesTable`, `plan`, `tableMatch`, `columns` | When `ResultKind` is `Rows`, with `null` for one that has no value; never when it is `None`, whatever the model holds |
| A parameter's `name`, `ordinal`, `type`, `nullable` | Always |
| `typeSource` | When `Type` is not null, with `null` when it has no value; never when `Type` is null |
| A column's eight keys; the keys of `origin` and of `matchesTable` | Always |
| PostgreSQL `name`, `kind`, `schema`, `internalName` | Always |
| `length`, `precision`, `scale` | When it has a value |
| `element`; `base`; `labels`; `subtype` | When `Kind` is `array`; `domain`; `enum`; `range` or `multirange`.  `null` when it has no value |
| SQL Server `name`, `maxLength`, `precision`, `scale` | Always |
| `userType` | When it has a value, with `schema` and `name` always and `assemblyQualifiedName` when it has a value |
| The `name` of an `OtherEngineType` | Always, and nothing else |

- [ ] **Step 1: Reference Bogus from the test project**

In `tests/SqlSource.Tests/SqlSource.Tests.csproj`, in the `ItemGroup` of package references, add as the first item, keeping the list in alphabetical order:

```xml
        <PackageReference Include="Bogus" />
```

`Directory.Packages.props` already pins it to 35.6.5.  Update the lock file:

```bash
dotnet restore SqlSource.slnx
```

Expected: `tests/SqlSource.Tests/packages.lock.json` gains `Bogus`.  If the restore fails with NU1004, run it once with `--force-evaluate`.

- [ ] **Step 2: Write the failing tests**

`tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs`:

```csharp
using System;
using System.Globalization;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarWriterTests
{
    // The examples of the format design are the oracle of the layout.
    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Write_ExampleThatWasRead_IsTheFileByteForByte(string name)
    {
        var text = SidecarExamples.Read(name);

        SidecarWriter.Write(SidecarReader.Read(text).Sidecar.ShouldNotBeNull()).ShouldBe(text);
    }

    [Fact]
    public void Write_EntryWithoutRows_HasTheLayoutOfTheFormat()
    {
        var sidecar = TestSidecars.SidecarOf(Entry(resultKind: SidecarResultKind.None));

        SidecarWriter
            .Write(sidecar)
            .ShouldBe(
                string.Join(
                    "\n",
                    "{",
                    "  \"_WARNING\": \"" + SidecarFormat.Warning + "\",",
                    "  \"$schema\": \"" + SidecarFormat.SchemaId + "\",",
                    "  \"formatVersion\": 1,",
                    "  \"toolVersion\": \"1.2.3\",",
                    "  \"queries\": {",
                    "    \"Q\": {",
                    "      \"hash\": \"h\",",
                    "      \"engine\": \"postgres\",",
                    "      \"database\": \"app\",",
                    "      \"serverVersion\": \"16.4\",",
                    "      \"resultKind\": \"none\",",
                    "      \"parameters\": []",
                    "    }",
                    "  }",
                    "}",
                    string.Empty
                )
            );
    }

    [Fact]
    public void Write_AnySidecar_HasLineFeedsATrailingNewlineAndNoByteOrderMark()
    {
        var text = SidecarWriter.Write(TestSidecars.SidecarOf(Entry(), Entry("Other")));

        text.ShouldStartWith("{\n");
        text.ShouldEndWith("\n}\n");
        text.ShouldNotContain("\r");
        text.ShouldNotContain("\t");
    }

    [Fact]
    public void Write_NoQueries_IsAnEmptyObjectOnTheLineOfItsKey() =>
        SidecarWriter.Write(TestSidecars.SidecarOf()).ShouldEndWith("  \"queries\": {}\n}\n");

    // The format design, section 1.  The character is given by its code, so that a test's name holds none.
    [Theory]
    [InlineData(0x22, "\\\"")]
    [InlineData(0x5C, "\\\\")]
    [InlineData(0x08, "\\b")]
    [InlineData(0x0C, "\\f")]
    [InlineData(0x0A, "\\n")]
    [InlineData(0x0D, "\\r")]
    [InlineData(0x09, "\\t")]
    [InlineData(0x00, "\\u0000")]
    [InlineData(0x0B, "\\u000B")]
    [InlineData(0x1F, "\\u001F")]
    public void Write_CharacterTheFormatEscapes_IsWrittenAsItsEscape(int code, string escape)
    {
        var value = "a" + (char)code + "b";
        var sidecar = TestSidecars.SidecarOf(Entry(value, database: value));

        var text = SidecarWriter.Write(sidecar);

        text.ShouldContain("    \"a" + escape + "b\": {\n");
        text.ShouldContain("\"database\": \"a" + escape + "b\",\n");
    }

    // Every other character is written as it is: a solidus, DEL, a line separator, a letter outside ASCII, a pair of
    // surrogates and one alone.
    [Theory]
    [InlineData(0x2F)]
    [InlineData(0x20)]
    [InlineData(0x7F)]
    [InlineData(0x2028)]
    [InlineData(0xE9)]
    [InlineData(0xD83D)]
    public void Write_AnyOtherCharacter_IsWrittenAsItIs(int code)
    {
        var value = "a" + (char)code + "b";

        SidecarWriter.Write(TestSidecars.SidecarOf(Entry(value))).ShouldContain("    \"" + value + "\": {\n");
    }

    // What a hand-written writer gets wrong comes back as it went in.
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x1F)]
    [InlineData(0x22)]
    [InlineData(0x5C)]
    [InlineData(0x7F)]
    [InlineData(0x2028)]
    [InlineData(0xD83D)]
    [InlineData(0xDE00)]
    public void Write_AwkwardCharacterInANameAndInAValue_ReadsBackAsItWas(int code)
    {
        var value = ((char)code).ToString();
        var labels = Postgres("e", SidecarValues.Kind.Enum, labels: [value, value + value, "😀" + value]);
        var sidecar = TestSidecars.SidecarOf(
            Entry(
                value,
                database: value,
                parameters: [Parameter(0, value, labels)],
                columns: [Column(0, value, Postgres(value), origin: new SidecarOrigin(value, value, value))]
            )
        );

        var read = SidecarReader.Read(SidecarWriter.Write(sidecar));

        WithoutSpans(read.Sidecar.ShouldNotBeNull()).ShouldBe(sidecar);
    }

    [Fact]
    public void Write_EntryWithRows_WritesTheFourKeysOfAResultInTheFormatsOrder()
    {
        var sidecar = TestSidecars.SidecarOf(
            Entry(
                matchesTable: new SidecarTable(null, "users"),
                plan: SidecarValues.Plan.Walked,
                tableMatch: SidecarValues.TableMatch.Matched
            )
        );

        SidecarWriter
            .Write(sidecar)
            .ShouldContain(
                string.Join(
                    "\n",
                    "      \"resultKind\": \"rows\",",
                    "      \"matchesTable\": {",
                    "        \"schema\": null,",
                    "        \"table\": \"users\"",
                    "      },",
                    "      \"plan\": \"walked\",",
                    "      \"tableMatch\": \"matched\",",
                    "      \"parameters\": [],",
                    "      \"columns\": [",
                    "        {",
                    "          \"ordinal\": 0,",
                    "          \"name\": \"id\",",
                    "          \"type\": {",
                    "            \"name\": \"integer\",",
                    "            \"kind\": \"base\",",
                    "            \"schema\": \"pg_catalog\",",
                    "            \"internalName\": \"int4\"",
                    "          },",
                    "          \"nullable\": false,",
                    "          \"nullableSource\": \"catalog\",",
                    "          \"origin\": null,",
                    "          \"identity\": null,",
                    "          \"computed\": null",
                    "        }",
                    "      ]",
                    "    }"
                )
            );
    }

    // The condition decides, and not the value.
    [Fact]
    public void Write_EntryWithoutRowsThatHoldsAResult_LeavesTheFourKeysOut()
    {
        var entry = Entry(resultKind: SidecarResultKind.None) with
        {
            MatchesTable = new SidecarTable("public", "users"),
            Plan = SidecarValues.Plan.Walked,
            TableMatch = SidecarValues.TableMatch.Matched,
            Columns = Of(Column(0, "id", Postgres())),
        };

        var text = SidecarWriter.Write(TestSidecars.SidecarOf(entry));

        text.ShouldNotContain("\"matchesTable\"");
        text.ShouldNotContain("\"plan\"");
        text.ShouldNotContain("\"tableMatch\"");
        text.ShouldNotContain("\"columns\"");
    }

    [Fact]
    public void Write_EntryWithRowsThatHoldsNoResult_WritesEachOfTheFourAsNull()
    {
        var entry = Entry() with { MatchesTable = null, Plan = null, TableMatch = null, Columns = null };

        var text = SidecarWriter.Write(TestSidecars.SidecarOf(entry));

        text.ShouldContain(
            "      \"matchesTable\": null,\n      \"plan\": null,\n      \"tableMatch\": null,\n      \"parameters\": [],\n"
        );
        text.ShouldContain("      \"columns\": null\n    }");
    }

    [Fact]
    public void Write_AlwaysKeyWithoutAValue_IsNull()
    {
        var text = SidecarWriter.Write(TestSidecars.SidecarOf(Entry(database: null, serverVersion: null)));

        text.ShouldContain("      \"database\": null,\n      \"serverVersion\": null,\n");
    }

    [Fact]
    public void Write_Parameter_WritesTypeSourceWhenItHasAType()
    {
        var sidecar = TestSidecars.SidecarOf(
            Entry(
                parameters:
                [
                    Parameter(0, "a", Postgres(), nullable: true, typeSource: SidecarValues.TypeSource.Declared),
                    new SidecarParameter("b", 1, Postgres(), false, null),
                    new SidecarParameter("c", 2, null, null, SidecarValues.TypeSource.Declared),
                ]
            )
        );

        var text = SidecarWriter.Write(sidecar);

        text.ShouldContain("          \"nullable\": true,\n          \"typeSource\": \"declared\"\n        },");
        text.ShouldContain("          \"nullable\": false,\n          \"typeSource\": null\n        },");
        text.ShouldContain(
            string.Join(
                "\n",
                "        {",
                "          \"name\": \"c\",",
                "          \"ordinal\": 2,",
                "          \"type\": null,",
                "          \"nullable\": null",
                "        }",
                "      ],"
            )
        );
    }

    [Fact]
    public void Write_PostgresType_WritesAFacetOnlyWhenItHasAValue()
    {
        var text = WriteColumnOf(Postgres("numeric(18,-2)", internalName: "numeric", precision: 18, scale: -2));

        text.ShouldContain("            \"internalName\": \"numeric\",\n            \"precision\": 18,\n            \"scale\": -2\n");
        text.ShouldNotContain("\"length\"");
    }

    [Theory]
    [InlineData("array", "element")]
    [InlineData("domain", "base")]
    [InlineData("range", "subtype")]
    [InlineData("multirange", "subtype")]
    public void Write_PostgresType_WritesTheNestedTypeItsKindNames(string kind, string key)
    {
        var inner = Postgres("text", internalName: "text");
        var all = Postgres("n", kind, element: inner, baseType: inner, labels: ["a"], subtype: inner);
        var none = Postgres("n", kind);

        var text = WriteColumnOf(all);

        text.ShouldContain("            \"" + key + "\": {\n              \"name\": \"text\",");
        foreach (var other in new[] { "element", "base", "labels", "subtype" })
        {
            if (other != key)
            {
                text.ShouldNotContain("\"" + other + "\":");
            }
        }

        WriteColumnOf(none).ShouldContain("            \"" + key + "\": null\n");
    }

    [Fact]
    public void Write_PostgresEnum_WritesItsLabelsOneOnALine()
    {
        WriteColumnOf(Postgres("e", "enum", labels: ["active", "deleted"]))
            .ShouldContain(
                "            \"labels\": [\n              \"active\",\n              \"deleted\"\n            ]\n"
            );
        WriteColumnOf(Postgres("e", "enum", labels: [])).ShouldContain("            \"labels\": []\n");
        WriteColumnOf(Postgres("e", "enum")).ShouldContain("            \"labels\": null\n");
        WriteColumnOf(Postgres("b", "base", labels: ["a"])).ShouldNotContain("\"labels\"");
    }

    [Fact]
    public void Write_SqlServerType_WritesItsFacetsAlwaysAndItsUserTypeWhenItHasOne()
    {
        var plain = WriteColumnOf(SqlServer("nvarchar(max)", -1, 0, 0));
        var alias = WriteColumnOf(SqlServer(userType: new SqlServerUserType("dbo", "CustomerId", null)));
        var clr = WriteColumnOf(SqlServer(userType: new SqlServerUserType(null, "geography", "Types.SqlGeography")));

        plain.ShouldContain(
            string.Join(
                "\n",
                "          \"type\": {",
                "            \"name\": \"nvarchar(max)\",",
                "            \"maxLength\": -1,",
                "            \"precision\": 0,",
                "            \"scale\": 0",
                "          },"
            )
        );
        alias.ShouldContain(
            string.Join(
                "\n",
                "            \"scale\": 0,",
                "            \"userType\": {",
                "              \"schema\": \"dbo\",",
                "              \"name\": \"CustomerId\"",
                "            }",
                "          },"
            )
        );
        clr.ShouldContain(
            "              \"schema\": null,\n              \"name\": \"geography\",\n"
                + "              \"assemblyQualifiedName\": \"Types.SqlGeography\"\n"
        );
    }

    [Fact]
    public void Write_TypeOfAnotherEngine_IsItsNameAlone() =>
        WriteColumnOf(new OtherEngineType("INTEGER"), "sqlite")
            .ShouldContain("          \"type\": {\n            \"name\": \"INTEGER\"\n          },\n");

    // In Swedish the minus sign of a number is U+2212.  A sidecar is the same file on every machine.
    [Fact]
    public void Write_UnderAnotherCulture_WritesNumbersTheSameWay()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

            WriteColumnOf(SqlServer("nvarchar(max)", -1, 0, 0)).ShouldContain("\"maxLength\": -1,");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static string WriteColumnOf(SidecarType type, string engine = SidecarValues.Engine.Postgres) =>
        SidecarWriter.Write(
            TestSidecars.SidecarOf(
                Entry(
                    engine: type is SqlServerType ? SidecarValues.Engine.SqlServer : engine,
                    columns: [Column(0, "c", type)]
                )
            )
        );
}
```

`tests/SqlSource.Tests/Snapshot/SidecarFaker.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Bogus;
using SqlSource.Snapshot;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

// Builds sidecars from a seed, so that a failure can be run again.  Strict, they are what the tool builds: one of
// the two engines, every "always" key with a value, and every value inside the limits of the schema.  Lenient, they
// are what a reader may find: another engine, a kind and a provenance value it does not know, and nulls where the
// reader allows them.  Neither holds what the writer's conditions leave out.
internal sealed class SidecarFaker(int seed, bool lenient)
{
    public const string UnknownEngine = "sqlite";

    public const string UnknownKind = "pseudo";

    private const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";

    private static readonly string[] Awkward =
    [
        "a\"b",
        "back\\slash",
        "tab\there",
        "line\nbreak",
        "\u0001",
        "größe",
        "日本語",
        "😀",
        "a/b",
        "{{x}}",
        " ",
        "deleted_at?",
    ];

    private static readonly string[] Kinds =
    [
        SidecarValues.Kind.Base,
        SidecarValues.Kind.Array,
        SidecarValues.Kind.Domain,
        SidecarValues.Kind.Enum,
        SidecarValues.Kind.Range,
        SidecarValues.Kind.Multirange,
        SidecarValues.Kind.Composite,
    ];

    private static readonly string[] Plans =
    [
        SidecarValues.Plan.NotNeeded,
        SidecarValues.Plan.Walked,
        SidecarValues.Plan.Unavailable,
        SidecarValues.Plan.Skipped,
    ];

    private static readonly string[] TableMatches =
    [
        SidecarValues.TableMatch.Matched,
        SidecarValues.TableMatch.NoOrigin,
        SidecarValues.TableMatch.SeveralTables,
        SidecarValues.TableMatch.NamesDiffer,
        SidecarValues.TableMatch.ColumnsDiffer,
        SidecarValues.TableMatch.OrderDiffers,
        SidecarValues.TableMatch.NullabilityDiffers,
    ];

    private static readonly string[] TypeSources =
    [
        SidecarValues.TypeSource.Inferred,
        SidecarValues.TypeSource.InferredFromCopies,
        SidecarValues.TypeSource.Declared,
    ];

    private static readonly string[] NullableSources =
    [
        SidecarValues.NullableSource.Server,
        SidecarValues.NullableSource.Catalog,
        SidecarValues.NullableSource.OuterJoin,
        SidecarValues.NullableSource.View,
        SidecarValues.NullableSource.NoOrigin,
        SidecarValues.NullableSource.Heuristic,
    ];

    private readonly Randomizer _random = new(seed);

    // Counts the PostgreSQL types built, so that each kind comes in turn and a few sidecars hold them all.
    private int _types;

    public Sidecar Next()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<SidecarEntry>();
        var count = _random.Int(1, 4);
        while (entries.Count < count)
        {
            var name = Text();
            if (names.Add(name))
            {
                entries.Add(Entry(name));
            }
        }

        var version = string.Create(
            CultureInfo.InvariantCulture,
            $"{_random.Int(0, 20)}.{_random.Int(0, 20)}.{_random.Int(0, 20)}"
        );
        return new Sidecar(SidecarFormat.Version, version, Of(entries.ToArray()));
    }

    private SidecarEntry Entry(string name)
    {
        var engine = lenient
            ? Pick(SidecarValues.Engine.Postgres, SidecarValues.Engine.SqlServer, UnknownEngine)
            : Pick(SidecarValues.Engine.Postgres, SidecarValues.Engine.SqlServer);
        var rows = _random.Bool();
        var hash = _random.String2(64, "0123456789abcdef");
        var database = OrNull(Word());
        var serverVersion = OrNull("16.4");
        var table = rows && _random.Bool() ? new SidecarTable(_random.Bool() ? Word() : null, OrNull(Word())) : null;
        var plan = rows ? Provenance(Plans) : null;
        var tableMatch = rows ? Provenance(TableMatches) : null;
        var parameters = Many(0, 3, index => Parameter(index, engine));
        var columns = rows ? Many(lenient ? 0 : 1, 4, index => Column(index, engine)) : null;
        return new SidecarEntry(
            name,
            default,
            hash,
            engine,
            database,
            serverVersion,
            rows ? SidecarResultKind.Rows : SidecarResultKind.None,
            table,
            plan,
            tableMatch,
            Of(parameters),
            columns is null ? null : Of(columns)
        );
    }

    private SidecarParameter Parameter(int index, string engine)
    {
        var name = Text();
        var type = _random.Bool(0.9f) ? Type(engine) : null;
        var nullable = Pick<bool?>(true, false, null);
        return new SidecarParameter(name, index, type, nullable, type is null ? null : Provenance(TypeSources));
    }

    private SidecarColumn Column(int index, string engine)
    {
        var name = Text(orEmpty: true);
        var type = Type(engine);
        var nullable = Pick<bool?>(true, false, null);
        var nullableSource = Provenance(NullableSources);
        var origin = _random.Bool()
            ? new SidecarOrigin(_random.Bool() ? Word() : null, OrNull(Word()), OrNull(Word()))
            : null;
        var identity = Pick<bool?>(true, false, null);
        var computed = Pick<bool?>(true, false, null);
        return new SidecarColumn(index, name, type, nullable, nullableSource, origin, identity, computed);
    }

    private SidecarType Type(string engine) =>
        engine switch
        {
            SidecarValues.Engine.Postgres => PostgresOf(0),
            SidecarValues.Engine.SqlServer => SqlServerOf(),
            _ => new OtherEngineType(Word()),
        };

    private PostgresType PostgresOf(int depth)
    {
        // A type two levels inside another is a leaf.
        var kind = depth >= 2 ? SidecarValues.Kind.Base : Kinds[_types++ % Kinds.Length];
        if (lenient && _random.Bool(0.1f))
        {
            kind = UnknownKind;
        }

        var name = Text();
        var schema = Word();
        var internalName = Word();
        var length = Facet(0, 10_485_760);
        var precision = Facet(0, 1000);
        var scale = Facet(-1000, 1000);
        var element = kind == SidecarValues.Kind.Array ? PostgresOf(depth + 1) : null;
        var baseType = kind == SidecarValues.Kind.Domain ? PostgresOf(depth + 1) : null;
        EquatableArray<string>? labels =
            kind == SidecarValues.Kind.Enum ? Of(Many(0, 3, _ => Text(orEmpty: true))) : null;
        var subtype = kind is SidecarValues.Kind.Range or SidecarValues.Kind.Multirange ? PostgresOf(depth + 1) : null;
        return new PostgresType(
            name,
            kind,
            schema,
            internalName,
            length,
            precision,
            scale,
            element,
            baseType,
            labels,
            subtype
        );
    }

    private SqlServerType SqlServerOf()
    {
        var name = Word();
        var maxLength = _random.Int(-1, 8000);
        var precision = _random.Int(0, 38);
        var scale = _random.Int(0, 38);
        var userType = _random.Bool(0.3f)
            ? new SqlServerUserType(OrNull(Word()), OrNull(Word()), _random.Bool() ? Word() : null)
            : null;
        return new SqlServerType(name, maxLength, precision, scale, userType);
    }

    private int? Facet(int minimum, int maximum) => _random.Bool(0.3f) ? _random.Int(minimum, maximum) : null;

    private T Pick<T>(params T[] items) => _random.ArrayElement(items);

    // The value, or null where a reader may find null.
    private string? OrNull(string value) => lenient && _random.Bool(0.3f) ? null : value;

    private string? Provenance(string[] values) =>
        lenient && _random.Bool(0.3f) ? Pick<string?>(null, "future-value") : Pick(values);

    private T[] Many<T>(int minimum, int maximum, Func<int, T> create)
    {
        var items = new T[_random.Int(minimum, maximum)];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = create(index);
        }

        return items;
    }

    private string Word() => _random.String2(_random.Int(1, 10), Letters);

    // A name: mostly a word, sometimes one with a character that must be escaped or that is not ASCII.
    private string Text(bool orEmpty = false)
    {
        if (orEmpty && _random.Bool(0.05f))
        {
            return string.Empty;
        }

        return _random.Bool(0.3f) ? Pick(Awkward) : Word();
    }
}
```

`tests/SqlSource.Tests/Snapshot/SidecarRoundTripTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarRoundTripTests
{
    private const int Seed = 20261009;

    private const int Count = 200;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Write_AnySidecar_ReadsBackEqualAndWritesTheSameTextAgain(bool lenient)
    {
        foreach (var sidecar in Samples(lenient))
        {
            var text = SidecarWriter.Write(sidecar);

            var result = SidecarReader.Read(text);

            result.Error.ShouldBeNull(text);
            var read = result.Sidecar.ShouldNotBeNull();
            WithoutSpans(read).ShouldBe(sidecar, text);
            SidecarWriter.Write(read).ShouldBe(text);
        }
    }

    // The samples are worth what they cover: each of the two engines and each kind.
    [Fact]
    public void Samples_AsTheToolBuildsThem_HoldBothEnginesAndEveryKind()
    {
        var samples = Samples(lenient: false);
        var types = samples.SelectMany(Types).ToList();

        samples
            .SelectMany(sidecar => sidecar.Queries)
            .Select(entry => entry.Engine)
            .Distinct()
            .Order()
            .ShouldBe([SidecarValues.Engine.SqlServer, SidecarValues.Engine.Postgres]);
        types.OfType<SqlServerType>().ShouldNotBeEmpty();
        types
            .OfType<PostgresType>()
            .Select(type => type.Kind)
            .Distinct()
            .Order()
            .ShouldBe(["array", "base", "composite", "domain", "enum", "multirange", "range"]);
        types.OfType<OtherEngineType>().ShouldBeEmpty();
    }

    [Fact]
    public void Samples_AsAReaderMayFindThem_HoldWhatTheToolNeverWrites()
    {
        var samples = Samples(lenient: true);
        var entries = samples.SelectMany(sidecar => sidecar.Queries).ToList();
        var types = samples.SelectMany(Types).ToList();

        types.OfType<OtherEngineType>().ShouldNotBeEmpty();
        types.OfType<PostgresType>().ShouldContain(type => type.Kind == SidecarFaker.UnknownKind);
        entries.ShouldContain(entry => entry.Database == null);
        entries.ShouldContain(entry => entry.Plan == "future-value");
        entries.ShouldContain(entry => entry.Columns != null && entry.Columns.Value.Count == 0);
    }

    internal static List<Sidecar> Samples(bool lenient)
    {
        var faker = new SidecarFaker(Seed, lenient);
        return [.. Enumerable.Range(0, Count).Select(_ => faker.Next())];
    }

    // Every type of a sidecar, the ones inside another included.
    private static IEnumerable<SidecarType> Types(Sidecar sidecar)
    {
        foreach (var entry in sidecar.Queries)
        {
            var types = entry
                .Parameters.Select(parameter => parameter.Type)
                .Concat((entry.Columns ?? Of<SidecarColumn>()).Select(column => (SidecarType?)column.Type));
            foreach (var type in types.SelectMany(Flatten))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<SidecarType> Flatten(SidecarType? type)
    {
        if (type is null)
        {
            yield break;
        }

        yield return type;
        if (type is PostgresType postgres)
        {
            foreach (var inner in new[] { postgres.Element, postgres.Base, postgres.Subtype }.SelectMany(Flatten))
            {
                yield return inner;
            }
        }
    }
}
```

`"mssql"` sorts before `"postgres"`, which is the order the first assertion gives the two engines in.

- [ ] **Step 3: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0103 for `SidecarWriter`.

- [ ] **Step 4: Write the writer**

`src/SqlSource/Snapshot/SidecarJsonBuilder.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Builds JSON in the layout of the sidecar format design, section 1: two-space indent, line feeds, one key or
/// array element on a line, an empty array or object on the line of its key, and a trailing newline.
/// </summary>
/// <remarks>
/// A null key is an element of an array, or the first object.  A null value is written as <c>null</c>.  A string is
/// written with <c>\"</c> and <c>\\</c>, with <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c> and <c>\t</c> for those
/// characters, and with <c>\u00XX</c> in upper case for every other character below U+0020; every other character
/// is written as it is.
/// </remarks>
internal sealed class SidecarJsonBuilder
{
    private const string Hex = "0123456789ABCDEF";

    private const string Null = "null";

    private readonly StringBuilder _text = new();

    private int _depth;

    // Whether the container being written has an item already, so that the next one needs a comma before it.
    private bool _hasItem;

    /// <summary>A string as the writer writes one, quotes included.</summary>
    public static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2);
        AppendQuoted(text, value);
        return text.ToString();
    }

    public void BeginObject(string? key) => Begin(key, '{');

    public void EndObject() => End('}');

    public void BeginArray(string? key) => Begin(key, '[');

    public void EndArray() => End(']');

    public void WriteString(string? key, string? value)
    {
        Item(key);
        if (value is null)
        {
            _ = _text.Append(Null);
        }
        else
        {
            AppendQuoted(_text, value);
        }
    }

    public void WriteNumber(string? key, int? value)
    {
        Item(key);
        _ = _text.Append(value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Null);
    }

    public void WriteBoolean(string? key, bool? value)
    {
        Item(key);
        _ = _text.Append(
            value switch
            {
                true => "true",
                false => "false",
                null => Null,
            }
        );
    }

    public void WriteNull(string? key)
    {
        Item(key);
        _ = _text.Append(Null);
    }

    /// <summary>The text, with its trailing newline.  Call it once, after the first object is ended.</summary>
    public string ToText() => _text.Append('\n').ToString();

    private static void AppendQuoted(StringBuilder text, string value)
    {
        _ = text.Append('"');
        var run = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character >= ' ' && character != '"' && character != '\\')
            {
                continue;
            }

            _ = text.Append(value, run, index - run);
            AppendEscape(text, character);
            run = index + 1;
        }

        _ = text.Append(value, run, value.Length - run).Append('"');
    }

    private static void AppendEscape(StringBuilder text, char character) =>
        _ = character switch
        {
            '"' => text.Append("\\\""),
            '\\' => text.Append("\\\\"),
            '\b' => text.Append("\\b"),
            '\f' => text.Append("\\f"),
            '\n' => text.Append("\\n"),
            '\r' => text.Append("\\r"),
            '\t' => text.Append("\\t"),
            _ => text.Append("\\u00").Append(Hex[character >> 4]).Append(Hex[character & 0xF]),
        };

    private void Begin(string? key, char open)
    {
        Item(key);
        _ = _text.Append(open);
        _depth++;
        _hasItem = false;
    }

    private void End(char close)
    {
        _depth--;
        if (_hasItem)
        {
            NewLine();
        }

        _ = _text.Append(close);
        _hasItem = true;
    }

    // Starts an item of the container being written: a comma after the item before it, a line of its own, its key.
    private void Item(string? key)
    {
        if (_hasItem)
        {
            _ = _text.Append(',');
        }

        if (_depth > 0)
        {
            NewLine();
        }

        _hasItem = true;
        if (key is not null)
        {
            AppendQuoted(_text, key);
            _ = _text.Append(": ");
        }
    }

    private void NewLine() => _ = _text.Append('\n').Append(' ', _depth * 2);
}
```

`src/SqlSource/Snapshot/SidecarWriter.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>
/// Writes a sidecar as the text of its file, in the layout and the key order of the sidecar format design.  What it
/// gives is a contract with every committed file: a change here is a change to the format design, to the schema and
/// to the examples.
/// </summary>
/// <remarks>
/// A key the format marks "always" is written with <c>null</c> when it has no value.  A key with a condition is
/// written when the condition holds, whatever the model holds: an entry without rows has no <c>columns</c>, and a
/// parameter without a type no <c>typeSource</c>.  A facet is written when it has a value.  The writer checks
/// nothing and never throws: a model the schema would refuse is the caller's mistake.
/// </remarks>
internal static class SidecarWriter
{
    public static string Write(Sidecar sidecar)
    {
        var json = new SidecarJsonBuilder();
        json.BeginObject(null);
        json.WriteString(SidecarKeys.Warning, SidecarFormat.Warning);
        json.WriteString(SidecarKeys.SchemaUrl, SidecarFormat.SchemaId);
        json.WriteNumber(SidecarKeys.FormatVersion, sidecar.FormatVersion);
        json.WriteString(SidecarKeys.ToolVersion, sidecar.ToolVersion);
        json.BeginObject(SidecarKeys.Queries);
        foreach (var entry in sidecar.Queries)
        {
            WriteEntry(json, entry);
        }

        json.EndObject();
        json.EndObject();
        return json.ToText();
    }

    private static void WriteEntry(SidecarJsonBuilder json, SidecarEntry entry)
    {
        var rows = entry.ResultKind == SidecarResultKind.Rows;
        json.BeginObject(entry.Name);
        json.WriteString(SidecarKeys.Hash, entry.Hash);
        json.WriteString(SidecarKeys.Engine, entry.Engine);
        json.WriteString(SidecarKeys.Database, entry.Database);
        json.WriteString(SidecarKeys.ServerVersion, entry.ServerVersion);
        json.WriteString(SidecarKeys.ResultKind, rows ? SidecarValues.ResultKind.Rows : SidecarValues.ResultKind.None);
        if (rows)
        {
            WriteTable(json, entry.MatchesTable);
            json.WriteString(SidecarKeys.Plan, entry.Plan);
            json.WriteString(SidecarKeys.TableMatch, entry.TableMatch);
        }

        json.BeginArray(SidecarKeys.Parameters);
        foreach (var parameter in entry.Parameters)
        {
            WriteParameter(json, parameter);
        }

        json.EndArray();
        if (rows)
        {
            WriteColumns(json, entry.Columns);
        }

        json.EndObject();
    }

    private static void WriteTable(SidecarJsonBuilder json, SidecarTable? table)
    {
        if (table is null)
        {
            json.WriteNull(SidecarKeys.MatchesTable);
            return;
        }

        json.BeginObject(SidecarKeys.MatchesTable);
        json.WriteString(SidecarKeys.Schema, table.Schema);
        json.WriteString(SidecarKeys.Table, table.Table);
        json.EndObject();
    }

    private static void WriteParameter(SidecarJsonBuilder json, SidecarParameter parameter)
    {
        json.BeginObject(null);
        json.WriteString(SidecarKeys.Name, parameter.Name);
        json.WriteNumber(SidecarKeys.Ordinal, parameter.Ordinal);
        WriteType(json, SidecarKeys.Type, parameter.Type);
        json.WriteBoolean(SidecarKeys.Nullable, parameter.Nullable);
        if (parameter.Type is not null)
        {
            json.WriteString(SidecarKeys.TypeSource, parameter.TypeSource);
        }

        json.EndObject();
    }

    private static void WriteColumns(SidecarJsonBuilder json, EquatableArray<SidecarColumn>? columns)
    {
        if (columns is not { } list)
        {
            json.WriteNull(SidecarKeys.Columns);
            return;
        }

        json.BeginArray(SidecarKeys.Columns);
        foreach (var column in list)
        {
            WriteColumn(json, column);
        }

        json.EndArray();
    }

    private static void WriteColumn(SidecarJsonBuilder json, SidecarColumn column)
    {
        json.BeginObject(null);
        json.WriteNumber(SidecarKeys.Ordinal, column.Ordinal);
        json.WriteString(SidecarKeys.Name, column.Name);
        WriteType(json, SidecarKeys.Type, column.Type);
        json.WriteBoolean(SidecarKeys.Nullable, column.Nullable);
        json.WriteString(SidecarKeys.NullableSource, column.NullableSource);
        if (column.Origin is { } origin)
        {
            json.BeginObject(SidecarKeys.Origin);
            json.WriteString(SidecarKeys.Schema, origin.Schema);
            json.WriteString(SidecarKeys.Table, origin.Table);
            json.WriteString(SidecarKeys.Column, origin.Column);
            json.EndObject();
        }
        else
        {
            json.WriteNull(SidecarKeys.Origin);
        }

        json.WriteBoolean(SidecarKeys.Identity, column.Identity);
        json.WriteBoolean(SidecarKeys.Computed, column.Computed);
        json.EndObject();
    }

    private static void WriteType(SidecarJsonBuilder json, string key, SidecarType? type)
    {
        switch (type)
        {
            case null:
                json.WriteNull(key);
                break;
            case PostgresType postgres:
                WritePostgres(json, key, postgres);
                break;
            case SqlServerType sqlServer:
                WriteSqlServer(json, key, sqlServer);
                break;
            default:
                json.BeginObject(key);
                json.WriteString(SidecarKeys.Name, type.Name);
                json.EndObject();
                break;
        }
    }

    private static void WritePostgres(SidecarJsonBuilder json, string key, PostgresType type)
    {
        json.BeginObject(key);
        json.WriteString(SidecarKeys.Name, type.Name);
        json.WriteString(SidecarKeys.Kind, type.Kind);
        json.WriteString(SidecarKeys.Schema, type.Schema);
        json.WriteString(SidecarKeys.InternalName, type.InternalName);
        WriteFacet(json, SidecarKeys.Length, type.Length);
        WriteFacet(json, SidecarKeys.Precision, type.Precision);
        WriteFacet(json, SidecarKeys.Scale, type.Scale);
        switch (type.Kind)
        {
            case SidecarValues.Kind.Array:
                WriteType(json, SidecarKeys.Element, type.Element);
                break;
            case SidecarValues.Kind.Domain:
                WriteType(json, SidecarKeys.Base, type.Base);
                break;
            case SidecarValues.Kind.Enum:
                WriteLabels(json, type.Labels);
                break;
            case SidecarValues.Kind.Range
            or SidecarValues.Kind.Multirange:
                WriteType(json, SidecarKeys.Subtype, type.Subtype);
                break;
            default:
                break;
        }

        json.EndObject();
    }

    private static void WriteFacet(SidecarJsonBuilder json, string key, int? value)
    {
        if (value is not null)
        {
            json.WriteNumber(key, value);
        }
    }

    private static void WriteLabels(SidecarJsonBuilder json, EquatableArray<string>? labels)
    {
        if (labels is not { } list)
        {
            json.WriteNull(SidecarKeys.Labels);
            return;
        }

        json.BeginArray(SidecarKeys.Labels);
        foreach (var label in list)
        {
            json.WriteString(null, label);
        }

        json.EndArray();
    }

    private static void WriteSqlServer(SidecarJsonBuilder json, string key, SqlServerType type)
    {
        json.BeginObject(key);
        json.WriteString(SidecarKeys.Name, type.Name);
        json.WriteNumber(SidecarKeys.MaxLength, type.MaxLength);
        json.WriteNumber(SidecarKeys.Precision, type.Precision);
        json.WriteNumber(SidecarKeys.Scale, type.Scale);
        if (type.UserType is { } userType)
        {
            json.BeginObject(SidecarKeys.UserType);
            json.WriteString(SidecarKeys.Schema, userType.Schema);
            json.WriteString(SidecarKeys.Name, userType.Name);
            if (userType.AssemblyQualifiedName is not null)
            {
                json.WriteString(SidecarKeys.AssemblyQualifiedName, userType.AssemblyQualifiedName);
            }

            json.EndObject();
        }

        json.EndObject();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run `SidecarWriterTests` and `SidecarRoundTripTests` with `--filter-class`.
Expected: PASS.

If `Write_ExampleThatWasRead_IsTheFileByteForByte` fails, read the difference before changing anything.  The spec says which side gives way: where an example disagrees with the rules of the format design's section 1 or with the order of its tables, the rules win, and the example is corrected in the format design and in its copy under `tests/SqlSource.Tests/Snapshot/Examples/`, in this commit.  Where the writer disagrees with the rules, the writer is wrong.

If a `Samples_` test fails, the seed does not cover what it should: raise `Count`, and change nothing else.

- [ ] **Step 6: Say in the format design how `\u00XX` is written**

In `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, section 1, the first paragraph, change `and with `\u00XX` for every other character below U+0020;` to:

```markdown
and with `\u00XX`, its hex digits in upper case, for every other character below U+0020;
```

- [ ] **Step 7: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/Snapshot tests/SqlSource.Tests docs/superpowers/specs/2026-10-07-sidecar-format-design.md
```

```bash
git commit -m "Write a sidecar

The writer gives the text of a file in the layout and the key order of
the format design, and the two examples come back byte for byte.
Sidecars that Bogus builds from a fixed seed are written, read and
compared.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The two comparisons

**Files:**
- Create in `src/SqlSource/Snapshot/`: `SidecarDifference.cs`, `SidecarComparer.cs`
- Modify: `src/SqlSource/Snapshot/Sidecar.cs` (`IsWrittenBy`), `src/SqlSource/Snapshot/SidecarEntry.cs` (`IsCurrentFor`)
- Test: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`

**Interfaces:**
- Consumes: the model and `SidecarKeys`, `SidecarValues`, `SidecarFormat` of task 2; `SidecarJsonBuilder.Quote` of task 5.
- Produces, `internal`, namespace `SqlSource.Snapshot`:
  - `bool Sidecar.IsWrittenBy(string toolVersion)`: the format version is `SidecarFormat.Version` and the tool version equals the argument, ordinally.
  - `bool SidecarEntry.IsCurrentFor(string hash, string database)`: the hash equals ordinally and the database equals ignoring case.
  - `sealed record SidecarDifference(string Path, string Committed, string Described)`.
  - `static SidecarDifference? SidecarComparer.FindDifference(SidecarEntry committed, SidecarEntry described)`: null when the two agree on what `--check` compares, and otherwise the first difference.

**The order, the paths and the values**, which the tests pin:

- Order: `hash`, `engine`, `database`, `resultKind`, `matchesTable`, `parameters`, `columns`.  Inside `matchesTable`: `schema`, `table`.  An array by index, and inside an element `name`, `ordinal`, `type`, `nullable`.  Inside a type, the keys in the order of the format design's table for its engine, a nested type descended into.
- Not compared: `Name`, `NameSpan`, `serverVersion`, `plan`, `tableMatch`, `typeSource`, `nullableSource`, `origin`, `identity`, `computed`.
- `database` is compared ignoring case.  Every other string is compared ordinally.
- A path names a leaf: `hash`, `matchesTable.table`, `parameters[0].type.name`, `columns[2].type.element.internalName`, `columns[0].type.userType.schema`, `parameters[1].type.labels[1]`.
- Two arrays of two lengths differ at `parameters.length`, `columns.length` or `...labels.length`, with the two counts, before any element is compared.
- A value is its JSON: `"int4"`, `true`, `3`, `null`.  A member that is null on one side and an object on the other differs at the member, with `null` and `an object`; for `labels`, `null` and `an array`.  `columns` absent on one side differs at `columns`, with `nothing` and `an array`.
- Two types of different shapes under one engine, which no reader gives, differ or agree on `type.name` alone.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`:

```csharp
using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public class SidecarComparerTests
{
    private static readonly PostgresType Text = Postgres("text", internalName: "text");

    private static readonly PostgresType Status = Postgres(
        "public.user_status",
        "enum",
        "public",
        "user_status",
        labels: ["active", "deleted"]
    );

    // An array of a domain over text: a type three levels deep.
    private static readonly PostgresType Tags = Postgres(
        "public.tag[]",
        "array",
        "public",
        "_tag",
        element: Postgres("public.tag", "domain", "public", "tag", baseType: Text)
    );

    private static readonly SqlServerType CustomerId = SqlServer(
        userType: new SqlServerUserType("dbo", "CustomerId", "Types.CustomerId")
    );

    [Fact]
    public void FindDifference_SameValuesInSeparateInstances_IsNull()
    {
        SidecarComparer.FindDifference(Sample(), Sample()).ShouldBeNull();
        SidecarComparer.FindDifference(SqlServerSample(), SqlServerSample()).ShouldBeNull();
    }

    [Fact]
    public void FindDifference_MemberOfTheEntry_IsFoundWithItsTwoValues()
    {
        ShouldDiffer(entry => entry with { Hash = "other" }, "hash", "\"h\"", "\"other\"");
        ShouldDiffer(entry => entry with { Engine = "mssql" }, "engine", "\"postgres\"", "\"mssql\"");
        ShouldDiffer(entry => entry with { Database = "other" }, "database", "\"app\"", "\"other\"");
        ShouldDiffer(entry => entry with { Database = null }, "database", "\"app\"", "null");
        ShouldDiffer(
            entry => entry with { ResultKind = SidecarResultKind.None },
            "resultKind",
            "\"rows\"",
            "\"none\""
        );
    }

    // Two names that differ only in case read one connection variable, so they are one database.
    [Fact]
    public void FindDifference_DatabaseNamesThatDifferInCase_IsNull() =>
        SidecarComparer.FindDifference(Sample(), Sample() with { Database = "APP" }).ShouldBeNull();

    [Fact]
    public void FindDifference_MatchesTable_IsComparedAsAnObjectAndThenByItsKeys()
    {
        ShouldDiffer(entry => entry with { MatchesTable = null }, "matchesTable", "an object", "null");
        ShouldDiffer(
            entry => entry with { MatchesTable = new SidecarTable(null, "users") },
            "matchesTable.schema",
            "\"public\"",
            "null"
        );
        ShouldDiffer(
            entry => entry with { MatchesTable = new SidecarTable("public", "Users") },
            "matchesTable.table",
            "\"users\"",
            "\"Users\""
        );
    }

    [Fact]
    public void FindDifference_Parameters_AreComparedByLengthAndThenByElement()
    {
        ShouldDiffer(entry => entry with { Parameters = Of(entry.Parameters[0]) }, "parameters.length", "2", "1");
        ShouldDiffer(
            entry => WithParameter(entry, 1, parameter => parameter with { Name = "Status" }),
            "parameters[1].name",
            "\"status\"",
            "\"Status\""
        );
        ShouldDiffer(
            entry => WithParameter(entry, 0, parameter => parameter with { Ordinal = 5 }),
            "parameters[0].ordinal",
            "0",
            "5"
        );
        ShouldDiffer(
            entry => WithParameter(entry, 0, parameter => parameter with { Type = null }),
            "parameters[0].type",
            "an object",
            "null"
        );
        ShouldDiffer(
            entry => WithParameter(entry, 1, parameter => parameter with { Nullable = true }),
            "parameters[1].nullable",
            "null",
            "true"
        );
    }

    [Fact]
    public void FindDifference_Columns_AreComparedByPresenceByLengthAndThenByElement()
    {
        ShouldDiffer(entry => entry with { Columns = null }, "columns", "an array", "nothing");
        ShouldDiffer(
            entry => entry with { Columns = Of(entry.Columns!.Value[0]) },
            "columns.length",
            "3",
            "1"
        );
        ShouldDiffer(
            entry => WithColumn(entry, 0, column => column with { Name = "ID" }),
            "columns[0].name",
            "\"id\"",
            "\"ID\""
        );
        ShouldDiffer(
            entry => WithColumn(entry, 2, column => column with { Ordinal = 3 }),
            "columns[2].ordinal",
            "2",
            "3"
        );
        ShouldDiffer(
            entry => WithColumn(entry, 1, column => column with { Nullable = false }),
            "columns[1].nullable",
            "true",
            "false"
        );
        ShouldDiffer(
            entry => WithColumn(entry, 1, column => column with { Nullable = null }),
            "columns[1].nullable",
            "true",
            "null"
        );
    }

    [Fact]
    public void FindDifference_PostgresType_IsComparedKeyByKey()
    {
        ShouldDifferInType(type => type with { Name = "int" }, "name", "\"integer\"", "\"int\"");
        ShouldDifferInType(type => type with { Kind = "domain" }, "kind", "\"base\"", "\"domain\"");
        ShouldDifferInType(type => type with { Schema = "public" }, "schema", "\"pg_catalog\"", "\"public\"");
        ShouldDifferInType(type => type with { InternalName = "int8" }, "internalName", "\"int4\"", "\"int8\"");
        ShouldDifferInType(type => type with { Length = 100 }, "length", "null", "100");
        ShouldDifferInType(type => type with { Precision = 18 }, "precision", "null", "18");
        ShouldDifferInType(type => type with { Scale = -2 }, "scale", "null", "-2");
        ShouldDifferInType(type => type with { Element = Text }, "element", "null", "an object");
        ShouldDifferInType(type => type with { Base = Text }, "base", "null", "an object");
        ShouldDifferInType(type => type with { Labels = Of("a") }, "labels", "null", "an array");
        ShouldDifferInType(type => type with { Subtype = Text }, "subtype", "null", "an object");
    }

    [Fact]
    public void FindDifference_NestedType_IsDescendedInto()
    {
        var other = Tags with
        {
            Element = Tags.Element! with { Base = Text with { InternalName = "varchar" } },
        };

        ShouldDiffer(
            entry => WithColumn(entry, 2, column => column with { Type = other }),
            "columns[2].type.element.base.internalName",
            "\"text\"",
            "\"varchar\""
        );
    }

    [Fact]
    public void FindDifference_Labels_AreComparedByLengthAndThenByLabel()
    {
        ShouldDiffer(
            entry => WithParameter(entry, 1, parameter => parameter with { Type = Status with { Labels = Of("active") } }),
            "parameters[1].type.labels.length",
            "2",
            "1"
        );
        ShouldDiffer(
            entry =>
                WithParameter(entry, 1, parameter => parameter with { Type = Status with { Labels = Of("active", "gone") } }),
            "parameters[1].type.labels[1]",
            "\"deleted\"",
            "\"gone\""
        );
        ShouldDiffer(
            entry => WithParameter(entry, 1, parameter => parameter with { Type = Status with { Labels = null } }),
            "parameters[1].type.labels",
            "an array",
            "null"
        );
    }

    [Fact]
    public void FindDifference_SqlServerType_IsComparedKeyByKey()
    {
        ShouldDifferInSqlServerType(type => type with { Name = "bigint" }, "name", "\"int\"", "\"bigint\"");
        ShouldDifferInSqlServerType(type => type with { MaxLength = -1 }, "maxLength", "4", "-1");
        ShouldDifferInSqlServerType(type => type with { Precision = 19 }, "precision", "10", "19");
        ShouldDifferInSqlServerType(type => type with { Scale = 2 }, "scale", "0", "2");
        ShouldDifferInSqlServerType(type => type with { UserType = null }, "userType", "an object", "null");
        ShouldDifferInSqlServerType(
            type => type with { UserType = type.UserType! with { Schema = "sales" } },
            "userType.schema",
            "\"dbo\"",
            "\"sales\""
        );
        ShouldDifferInSqlServerType(
            type => type with { UserType = type.UserType! with { Name = "OrderId" } },
            "userType.name",
            "\"CustomerId\"",
            "\"OrderId\""
        );
        ShouldDifferInSqlServerType(
            type => type with { UserType = type.UserType! with { AssemblyQualifiedName = null } },
            "userType.assemblyQualifiedName",
            "\"Types.CustomerId\"",
            "null"
        );
    }

    [Fact]
    public void FindDifference_TypeOfAnotherEngine_IsComparedByItsName()
    {
        var committed = Entry(engine: "sqlite", columns: [Column(0, "id", new OtherEngineType("INTEGER"))]);
        var described = Entry(engine: "sqlite", columns: [Column(0, "id", new OtherEngineType("TEXT"))]);

        SidecarComparer
            .FindDifference(committed, described)
            .ShouldBe(new SidecarDifference("columns[0].type.name", "\"INTEGER\"", "\"TEXT\""));
    }

    // A value is shown as the writer writes it.
    [Fact]
    public void FindDifference_ValueWithACharacterTheFormatEscapes_IsShownEscaped() =>
        ShouldDiffer(entry => entry with { Hash = "a\"b\n" }, "hash", "\"h\"", "\"a\\\"b\\n\"");

    // The versions and the server differ between a developer's machine and CI by design, and provenance and origin
    // are informational.
    [Fact]
    public void FindDifference_MemberThatCheckDoesNotCompare_IsNull()
    {
        ShouldNotDiffer(entry => entry with { Name = "Other" });
        ShouldNotDiffer(entry => entry with { NameSpan = new TextSpan(5, 3) });
        ShouldNotDiffer(entry => entry with { ServerVersion = "17.0" });
        ShouldNotDiffer(entry => entry with { Plan = SidecarValues.Plan.Unavailable });
        ShouldNotDiffer(entry => entry with { TableMatch = SidecarValues.TableMatch.Matched });
        ShouldNotDiffer(entry =>
            WithParameter(entry, 0, parameter => parameter with { TypeSource = SidecarValues.TypeSource.Declared })
        );
        ShouldNotDiffer(entry =>
            WithColumn(entry, 0, column => column with { NullableSource = SidecarValues.NullableSource.View })
        );
        ShouldNotDiffer(entry => WithColumn(entry, 0, column => column with { Origin = null }));
        ShouldNotDiffer(entry => WithColumn(entry, 0, column => column with { Identity = false }));
        ShouldNotDiffer(entry => WithColumn(entry, 0, column => column with { Computed = true }));
    }

    [Fact]
    public void FindDifference_TwoDifferences_IsTheFirstInTheOrder()
    {
        ShouldDiffer(entry => entry with { Hash = "x", Engine = "mssql" }, "hash", "\"h\"", "\"x\"");
        ShouldDiffer(entry => entry with { Engine = "mssql", Database = "x" }, "engine", "\"postgres\"", "\"mssql\"");
        ShouldDiffer(
            entry => entry with { Database = "x", ResultKind = SidecarResultKind.None },
            "database",
            "\"app\"",
            "\"x\""
        );
        ShouldDiffer(
            entry => entry with { ResultKind = SidecarResultKind.None, MatchesTable = null },
            "resultKind",
            "\"rows\"",
            "\"none\""
        );
        ShouldDiffer(
            entry => entry with { MatchesTable = null, Parameters = Of(entry.Parameters[0]) },
            "matchesTable",
            "an object",
            "null"
        );
        ShouldDiffer(
            entry =>
                WithColumn(
                    WithParameter(entry, 1, parameter => parameter with { Nullable = true }),
                    0,
                    column => column with { Name = "x" }
                ),
            "parameters[1].nullable",
            "null",
            "true"
        );
        ShouldDiffer(
            entry =>
                WithColumn(entry, 0, column => column with { Name = "x", Ordinal = 9, Type = Text, Nullable = null }),
            "columns[0].name",
            "\"id\"",
            "\"x\""
        );
        ShouldDiffer(
            entry => WithColumn(entry, 0, column => column with { Ordinal = 9, Type = Text, Nullable = null }),
            "columns[0].ordinal",
            "0",
            "9"
        );
        ShouldDiffer(
            entry => WithColumn(entry, 0, column => column with { Type = Text, Nullable = null }),
            "columns[0].type.name",
            "\"integer\"",
            "\"text\""
        );
        ShouldDiffer(
            entry => WithColumn(WithColumn(entry, 0, column => column with { Nullable = null }), 1, _ => entry.Columns!.Value[0]),
            "columns[0].nullable",
            "false",
            "null"
        );
    }

    [Theory]
    [InlineData("h", "app", true)]
    // Database names are compared ignoring case, and a hash is not.
    [InlineData("h", "APP", true)]
    [InlineData("H", "app", false)]
    [InlineData("other", "app", false)]
    [InlineData("h", "other", false)]
    [InlineData("h", "", false)]
    public void IsCurrentFor_HashAndDatabase_HoldsWhenBothAreTheEntrys(string hash, string database, bool expected) =>
        Entry(hash: "h", database: "app").IsCurrentFor(hash, database).ShouldBe(expected);

    [Fact]
    public void IsCurrentFor_EntryWithoutADatabase_DoesNotHold() =>
        Entry(hash: "h", database: null).IsCurrentFor("h", "app").ShouldBeFalse();

    [Theory]
    [InlineData(1, "1.2.3", "1.2.3", true)]
    [InlineData(1, "1.2.3", "1.2.4", false)]
    [InlineData(1, "1.2.3", "1.2.3.0", false)]
    [InlineData(1, "1.2.3", "1.2.3-dev", false)]
    [InlineData(2, "1.2.3", "1.2.3", false)]
    [InlineData(0, "1.2.3", "1.2.3", false)]
    public void IsWrittenBy_ToolVersion_HoldsForTheFormatOfThisReaderAndThatTool(
        int formatVersion,
        string written,
        string toolVersion,
        bool expected
    ) => new Sidecar(formatVersion, written, Of<SidecarEntry>()).IsWrittenBy(toolVersion).ShouldBe(expected);

    private static SidecarEntry Sample() =>
        Entry(
            matchesTable: new SidecarTable("public", "users"),
            parameters: [Parameter(0, "id", Postgres()), Parameter(1, "status", Status)],
            columns:
            [
                Column(0, "id", Postgres(), origin: new SidecarOrigin("public", "users", "id"), identity: true),
                Column(1, "name", Text, nullable: true),
                Column(2, "tags", Tags, nullable: null),
            ]
        );

    private static SidecarEntry SqlServerSample() =>
        Entry(engine: SidecarValues.Engine.SqlServer, columns: [Column(0, "CustomerId", CustomerId)]);

    private static SidecarEntry WithParameter(
        SidecarEntry entry,
        int index,
        Func<SidecarParameter, SidecarParameter> change
    )
    {
        var parameters = new SidecarParameter[entry.Parameters.Count];
        for (var position = 0; position < parameters.Length; position++)
        {
            parameters[position] = position == index ? change(entry.Parameters[position]) : entry.Parameters[position];
        }

        return entry with { Parameters = Of(parameters) };
    }

    private static SidecarEntry WithColumn(SidecarEntry entry, int index, Func<SidecarColumn, SidecarColumn> change)
    {
        var current = entry.Columns.ShouldNotBeNull();
        var columns = new SidecarColumn[current.Count];
        for (var position = 0; position < columns.Length; position++)
        {
            columns[position] = position == index ? change(current[position]) : current[position];
        }

        return entry with { Columns = Of(columns) };
    }

    private static void ShouldDiffer(
        Func<SidecarEntry, SidecarEntry> change,
        string path,
        string committed,
        string described
    ) =>
        SidecarComparer
            .FindDifference(Sample(), change(Sample()))
            .ShouldBe(new SidecarDifference(path, committed, described));

    private static void ShouldNotDiffer(Func<SidecarEntry, SidecarEntry> change) =>
        SidecarComparer.FindDifference(Sample(), change(Sample())).ShouldBeNull();

    // The type of the first column, which is integer, changed.
    private static void ShouldDifferInType(
        Func<PostgresType, PostgresType> change,
        string key,
        string committed,
        string described
    ) =>
        ShouldDiffer(
            entry => WithColumn(entry, 0, column => column with { Type = change((PostgresType)column.Type) }),
            "columns[0].type." + key,
            committed,
            described
        );

    private static void ShouldDifferInSqlServerType(
        Func<SqlServerType, SqlServerType> change,
        string key,
        string committed,
        string described
    ) =>
        SidecarComparer
            .FindDifference(
                SqlServerSample(),
                WithColumn(SqlServerSample(), 0, column => column with { Type = change((SqlServerType)column.Type) })
            )
            .ShouldBe(new SidecarDifference("columns[0].type." + key, committed, described));
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL with CS0103 for `SidecarComparer`, CS0246 for `SidecarDifference`, and CS1061 for `IsCurrentFor` and `IsWrittenBy`.

- [ ] **Step 3: Write the comparisons**

Add to `Sidecar` in `src/SqlSource/Snapshot/Sidecar.cs`, after `Find`:

```csharp
    /// <summary>
    /// Whether the file has the format of this version and was written by the tool of
    /// <paramref name="toolVersion" />.  With <see cref="SidecarEntry.IsCurrentFor" /> it is the test of a current
    /// entry: an entry from an older tool is never kept.
    /// </summary>
    public bool IsWrittenBy(string toolVersion) =>
        FormatVersion == SidecarFormat.Version && string.Equals(ToolVersion, toolVersion, StringComparison.Ordinal);
```

Replace the `);` that ends `SidecarEntry` in `src/SqlSource/Snapshot/SidecarEntry.cs` with a body, and add `using System;` at the top:

```csharp
)
{
    /// <summary>
    /// Whether the entry describes the query with this hash against this database.  The name of a database is
    /// compared ignoring case: two names that differ only in case read one connection variable.
    /// </summary>
    public bool IsCurrentFor(string hash, string database) =>
        string.Equals(Hash, hash, StringComparison.Ordinal)
        && string.Equals(Database, database, StringComparison.OrdinalIgnoreCase);
}
```

`src/SqlSource/Snapshot/SidecarDifference.cs`:

```csharp
namespace SqlSource.Snapshot;

/// <summary>The first difference between a committed entry and what the database says today.</summary>
/// <param name="Path">The leaf that differs: <c>hash</c>, <c>columns[2].type.element.internalName</c>.</param>
/// <param name="Committed">The committed value as text: its JSON, or <c>an object</c>.</param>
/// <param name="Described">The described value, the same way.</param>
internal sealed record SidecarDifference(string Path, string Committed, string Described);
```

`src/SqlSource/Snapshot/SidecarComparer.cs`:

```csharp
using System;
using System.Globalization;

namespace SqlSource.Snapshot;

/// <summary>
/// What <c>describe --check</c> compares, by the sidecar format design, section 3: <c>hash</c>, <c>engine</c>,
/// <c>database</c>, <c>resultKind</c>, <c>matchesTable</c>, and each parameter's and column's <c>name</c>,
/// <c>ordinal</c>, <c>type</c> and <c>nullable</c>, in that order.  The server's version, provenance, origin,
/// identity and computed are not compared: they are informational, or differ between machines by design.
/// </summary>
/// <remarks>
/// Each comparison returns a difference whose path starts at what it compares, and its caller puts its own path in
/// front.  So two entries that agree build no path.
/// </remarks>
internal static class SidecarComparer
{
    private const string Null = "null";

    private const string Nothing = "nothing";

    private const string AnObject = "an object";

    private const string AnArray = "an array";

    /// <summary>
    /// Returns the first difference between two entries, or null when they agree on everything that is compared.
    /// </summary>
    public static SidecarDifference? FindDifference(SidecarEntry committed, SidecarEntry described) =>
        Text(SidecarKeys.Hash, committed.Hash, described.Hash)
        ?? Text(SidecarKeys.Engine, committed.Engine, described.Engine)
        ?? Text(SidecarKeys.Database, committed.Database, described.Database, StringComparison.OrdinalIgnoreCase)
        ?? Text(SidecarKeys.ResultKind, ResultKind(committed.ResultKind), ResultKind(described.ResultKind))
        ?? Table(committed.MatchesTable, described.MatchesTable)
        ?? Parameters(committed.Parameters, described.Parameters)
        ?? Columns(committed.Columns, described.Columns);

    private static string ResultKind(SidecarResultKind kind) =>
        kind == SidecarResultKind.Rows ? SidecarValues.ResultKind.Rows : SidecarValues.ResultKind.None;

    private static SidecarDifference? Table(SidecarTable? committed, SidecarTable? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.MatchesTable, committed is not null, described is not null, AnObject, Null);
        }

        return Under(
            SidecarKeys.MatchesTable,
            Text(SidecarKeys.Schema, committed.Schema, described.Schema)
                ?? Text(SidecarKeys.Table, committed.Table, described.Table)
        );
    }

    private static SidecarDifference? Parameters(
        EquatableArray<SidecarParameter> committed,
        EquatableArray<SidecarParameter> described
    )
    {
        if (committed.Count != described.Count)
        {
            return Length(SidecarKeys.Parameters, committed.Count, described.Count);
        }

        for (var index = 0; index < committed.Count; index++)
        {
            var left = committed[index];
            var right = described[index];
            var difference =
                Text(SidecarKeys.Name, left.Name, right.Name)
                ?? Number(SidecarKeys.Ordinal, left.Ordinal, right.Ordinal)
                ?? Type(left.Type, right.Type)
                ?? Flag(SidecarKeys.Nullable, left.Nullable, right.Nullable);
            if (difference is not null)
            {
                return Under(Element(SidecarKeys.Parameters, index), difference);
            }
        }

        return null;
    }

    private static SidecarDifference? Columns(
        EquatableArray<SidecarColumn>? committed,
        EquatableArray<SidecarColumn>? described
    )
    {
        if (committed is not { } left || described is not { } right)
        {
            return Presence(SidecarKeys.Columns, committed.HasValue, described.HasValue, AnArray, Nothing);
        }

        if (left.Count != right.Count)
        {
            return Length(SidecarKeys.Columns, left.Count, right.Count);
        }

        for (var index = 0; index < left.Count; index++)
        {
            var difference =
                Text(SidecarKeys.Name, left[index].Name, right[index].Name)
                ?? Number(SidecarKeys.Ordinal, left[index].Ordinal, right[index].Ordinal)
                ?? Type(left[index].Type, right[index].Type)
                ?? Flag(SidecarKeys.Nullable, left[index].Nullable, right[index].Nullable);
            if (difference is not null)
            {
                return Under(Element(SidecarKeys.Columns, index), difference);
            }
        }

        return null;
    }

    private static SidecarDifference? Type(SidecarType? committed, SidecarType? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.Type, committed is not null, described is not null, AnObject, Null);
        }

        // Two entries of one engine hold types of one shape.  Two shapes can only be told apart by their names.
        var difference = (committed, described) switch
        {
            (PostgresType left, PostgresType right) => Postgres(left, right),
            (SqlServerType left, SqlServerType right) => SqlServer(left, right),
            _ => Text(SidecarKeys.Name, committed.Name, described.Name),
        };
        return Under(SidecarKeys.Type, difference);
    }

    private static SidecarDifference? Postgres(PostgresType committed, PostgresType described) =>
        Text(SidecarKeys.Name, committed.Name, described.Name)
        ?? Text(SidecarKeys.Kind, committed.Kind, described.Kind)
        ?? Text(SidecarKeys.Schema, committed.Schema, described.Schema)
        ?? Text(SidecarKeys.InternalName, committed.InternalName, described.InternalName)
        ?? Number(SidecarKeys.Length, committed.Length, described.Length)
        ?? Number(SidecarKeys.Precision, committed.Precision, described.Precision)
        ?? Number(SidecarKeys.Scale, committed.Scale, described.Scale)
        ?? Nested(SidecarKeys.Element, committed.Element, described.Element)
        ?? Nested(SidecarKeys.Base, committed.Base, described.Base)
        ?? Labels(committed.Labels, described.Labels)
        ?? Nested(SidecarKeys.Subtype, committed.Subtype, described.Subtype);

    private static SidecarDifference? Nested(string key, PostgresType? committed, PostgresType? described) =>
        committed is null || described is null
            ? Presence(key, committed is not null, described is not null, AnObject, Null)
            : Under(key, Postgres(committed, described));

    private static SidecarDifference? Labels(EquatableArray<string>? committed, EquatableArray<string>? described)
    {
        if (committed is not { } left || described is not { } right)
        {
            return Presence(SidecarKeys.Labels, committed.HasValue, described.HasValue, AnArray, Null);
        }

        if (left.Count != right.Count)
        {
            return Length(SidecarKeys.Labels, left.Count, right.Count);
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                var path = SidecarKeys.Labels + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                return new SidecarDifference(path, Json(left[index]), Json(right[index]));
            }
        }

        return null;
    }

    private static SidecarDifference? SqlServer(SqlServerType committed, SqlServerType described) =>
        Text(SidecarKeys.Name, committed.Name, described.Name)
        ?? Number(SidecarKeys.MaxLength, committed.MaxLength, described.MaxLength)
        ?? Number(SidecarKeys.Precision, committed.Precision, described.Precision)
        ?? Number(SidecarKeys.Scale, committed.Scale, described.Scale)
        ?? UserType(committed.UserType, described.UserType);

    private static SidecarDifference? UserType(SqlServerUserType? committed, SqlServerUserType? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.UserType, committed is not null, described is not null, AnObject, Null);
        }

        return Under(
            SidecarKeys.UserType,
            Text(SidecarKeys.Schema, committed.Schema, described.Schema)
                ?? Text(SidecarKeys.Name, committed.Name, described.Name)
                ?? Text(
                    SidecarKeys.AssemblyQualifiedName,
                    committed.AssemblyQualifiedName,
                    described.AssemblyQualifiedName
                )
        );
    }

    private static SidecarDifference? Text(
        string path,
        string? committed,
        string? described,
        StringComparison comparison = StringComparison.Ordinal
    ) => string.Equals(committed, described, comparison) ? null : new SidecarDifference(path, Json(committed), Json(described));

    private static SidecarDifference? Number(string path, int? committed, int? described) =>
        committed == described ? null : new SidecarDifference(path, Json(committed), Json(described));

    private static SidecarDifference? Flag(string path, bool? committed, bool? described) =>
        committed == described ? null : new SidecarDifference(path, Json(committed), Json(described));

    // A member that one side has and the other lacks.  Null when both have it or both lack it.
    private static SidecarDifference? Presence(string path, bool committed, bool described, string present, string absent) =>
        committed == described
            ? null
            : new SidecarDifference(path, committed ? present : absent, described ? present : absent);

    private static SidecarDifference Length(string array, int committed, int described) =>
        new(array + ".length", Json(committed), Json(described));

    private static string Element(string array, int index) =>
        array + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    // Puts a path in front of a difference found under it.  The path is built only when there is one.
    private static SidecarDifference? Under(string path, SidecarDifference? difference) =>
        difference is null ? null : difference with { Path = path + "." + difference.Path };

    private static string Json(string? value) => value is null ? Null : SidecarJsonBuilder.Quote(value);

    private static string Json(int? value) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Null;

    private static string Json(bool? value) =>
        value switch
        {
            true => "true",
            false => "false",
            null => Null,
        };
}
```

`Element(SidecarKeys.Parameters, index)` is called only inside the `if` that found a difference, so two entries that agree allocate nothing.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Snapshot.SidecarComparerTests`
Expected: PASS.

- [ ] **Step 5: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/Snapshot tests/SqlSource.Tests/Snapshot
```

```bash
git commit -m "Add the two comparisons of the sidecar format

The test of a current entry, by hash, database and the two versions,
and what describe --check compares, as the first difference with its
path and its two values.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The schema and its validation

**Files:**
- Create: `schemas/sidecar-v1.schema.json`
- Modify: `Directory.Packages.props`, `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `tests/SqlSource.Tests/packages.lock.json`, `.editorconfig`, `.gitattributes`
- Test: `tests/SqlSource.Tests/Snapshot/SidecarSchemaTests.cs`

**Interfaces:**
- Consumes: `SidecarWriter.Write` of task 5; `SidecarReader.Read` of task 4; `SidecarRoundTripTests.Samples(bool lenient)` of task 5; `SidecarFormat`, `SidecarValues` of task 2; `SidecarExamples` of task 4.
- Produces: nothing a later task consumes.

- [ ] **Step 1: Add the schema file**

The file is the third `json` block of the sidecar format design, its section 5, byte for byte.  Create it with:

```bash
python3 - <<'EOF'
import pathlib, re
doc = pathlib.Path('docs/superpowers/specs/2026-10-07-sidecar-format-design.md').read_text()
blocks = re.findall(r"```json\n(.*?)```", doc, re.S)
assert len(blocks) == 3, len(blocks)
target = pathlib.Path('schemas')
target.mkdir(exist_ok=True)
(target / 'sidecar-v1.schema.json').write_text(blocks[2])
EOF
```

Expected: `schemas/sidecar-v1.schema.json` is 7,628 bytes and its fourth line is the `$id`.

In `.editorconfig`, after the section task 4 added, add:

```ini
# The schema of a sidecar, as the sidecar format design gives it.
[schemas/*.json]
max_line_length = off
end_of_line = lf
```

In `.gitattributes`, after the line task 4 added, add:

```gitattributes
schemas/*.json text eol=lf
```

- [ ] **Step 2: Add the validator to the test project**

In `Directory.Packages.props`, after the `GitHubActionsTestLogger` line, add:

```xml
        <!--
            A validator of JSON Schema, for tests/SqlSource.Tests alone: nothing that ships references it.  Its
            source is MIT and its binaries come under the Open Source Maintenance Fee.  The owner chose it with
            that known; see the spec of phase 2.1 of query generation.
        -->
        <PackageVersion Include="JsonSchema.Net" Version="9.4.0" />
```

In `tests/SqlSource.Tests/SqlSource.Tests.csproj`, add the reference after `GitHubActionsTestLogger`:

```xml
        <PackageReference Include="JsonSchema.Net" />
```

and, in the `ItemGroup` of the `None` items:

```xml
        <!-- Snapshot/SidecarSchemaTests.cs reads the schema from the output folder. -->
        <None
            Include="../../schemas/sidecar-v1.schema.json"
            Link="schemas/sidecar-v1.schema.json"
            CopyToOutputDirectory="PreserveNewest"
        />
```

Update the lock file:

```bash
dotnet restore SqlSource.slnx
```

Expected: `tests/SqlSource.Tests/packages.lock.json` gains `JsonSchema.Net` and what it brings.  No other lock file changes: if `src/SqlSource/packages.lock.json` changes, stop, since the generator must not gain a dependency.

- [ ] **Step 3: Write the tests**

`tests/SqlSource.Tests/Snapshot/SidecarSchemaTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

// The schema is strict and the writer is written by hand.  These tests are what keeps the two in step.
public class SidecarSchemaTests
{
    private static readonly string SchemaText = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "schemas", "sidecar-v1.schema.json")
    );

    // One instance: a schema is registered under its $id when it is first used.
    private static readonly JsonSchema Schema = JsonSchema.FromText(SchemaText);

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Schema_ExampleOfTheFormatDesign_IsValid(string name) =>
        Errors(SidecarExamples.Read(name)).ShouldBeEmpty();

    [Fact]
    public void Schema_EverySidecarTheToolCanBuild_IsValidAsTheWriterWritesIt()
    {
        foreach (var sidecar in SidecarRoundTripTests.Samples(lenient: false))
        {
            var text = SidecarWriter.Write(sidecar);

            Errors(text).ShouldBeEmpty(text);
        }
    }

    [Fact]
    public void Schema_Id_IsTheUrlTheWriterWrites()
    {
        using var schema = JsonDocument.Parse(SchemaText);

        schema.RootElement.GetProperty("$id").GetString().ShouldBe(SidecarFormat.SchemaId);
    }

    // The schema says what this tool writes, and the reader rules say what any version tolerates.
    [Fact]
    public void Schema_FileWithAKeyTheToolDoesNotWrite_IsNotValidThoughTheReaderReadsIt()
    {
        var text = SidecarExamples
            .Read(SidecarExamples.Users)
            .Replace("\"formatVersion\": 1,", "\"formatVersion\": 1,\n  \"future\": true,", StringComparison.Ordinal);

        Errors(text).ShouldNotBeEmpty();
        SidecarReader.Read(text).Sidecar.ShouldNotBeNull().Queries.Count.ShouldBe(4);
    }

    // The validator must hold the schema's conditions, or the tests above prove nothing.
    [Theory]
    [InlineData("\"kind\": \"base\"", "\"kind\": \"pseudo\"")]
    [InlineData("\"kind\": \"base\"", "\"kind\": \"array\"")]
    [InlineData("\"resultKind\": \"none\"", "\"resultKind\": \"none\",\n      \"columns\": []")]
    [InlineData("\"plan\": \"not-needed\",", "")]
    [InlineData("\"typeSource\": \"declared\"", "\"typeSource\": \"guessed\"")]
    [InlineData("\"engine\": \"postgres\"", "\"engine\": \"sqlite\"")]
    [InlineData("\"toolVersion\": \"0.4.0\"", "\"toolVersion\": \"0.4.0-dev\"")]
    [InlineData("\"formatVersion\": 1", "\"formatVersion\": 2")]
    public void Schema_FileThatBreaksACondition_IsNotValid(string old, string replacement)
    {
        var text = SidecarExamples.Read(SidecarExamples.Users);
        text.ShouldContain(old);

        Errors(text.Replace(old, replacement, StringComparison.Ordinal)).ShouldNotBeEmpty();
    }

    // The values the tool writes are the values the schema allows, each list in both directions.
    [Theory]
    [InlineData(typeof(SidecarValues.Engine), "query", "engine")]
    [InlineData(typeof(SidecarValues.ResultKind), "query", "resultKind")]
    [InlineData(typeof(SidecarValues.Plan), "query", "plan")]
    [InlineData(typeof(SidecarValues.TableMatch), "query", "tableMatch")]
    [InlineData(typeof(SidecarValues.TypeSource), "parameter", "typeSource")]
    [InlineData(typeof(SidecarValues.NullableSource), "column", "nullableSource")]
    [InlineData(typeof(SidecarValues.Kind), "postgresType", "kind")]
    public void Schema_ListOfValues_IsTheConstantsOfSidecarValues(Type constants, string definition, string property)
    {
        using var schema = JsonDocument.Parse(SchemaText);
        var allowed = schema
            .RootElement.GetProperty("$defs")
            .GetProperty(definition)
            .GetProperty("properties")
            .GetProperty(property)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString());

        var written = constants
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string?)field.GetRawConstantValue());

        written.ShouldBe(allowed, ignoreOrder: true);
    }

    private static string[] Errors(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        string[] errors =
        [
            .. (result.Details ?? [])
                .Where(detail => detail.Errors is not null)
                .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation}: {error.Value}")),
        ];
        return errors.Length > 0 ? errors : ["The file is not valid, and the validator gave no detail."];
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Snapshot.SidecarSchemaTests`
Expected: PASS.  JsonSchema.Net 9.4.0 was tried against this schema and both examples before the plan was written: both are valid, and a file with an extra key or a broken type is not.

If `Schema_EverySidecarTheToolCanBuild_IsValidAsTheWriterWritesIt` fails, the message holds the file and the place.  Decide which of three is wrong before changing anything: the faker built what the tool never would, and `SidecarFaker` is fixed; the writer wrote what the format design does not say, and the writer is fixed; or the schema refuses what the format design allows, which is a change to the format design and to `schemas/sidecar-v1.schema.json` in this commit, and one to ask the owner about first.

- [ ] **Step 5: Format, validate, commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: every step `passed`, the `package` step included: the package's contents do not change.

```bash
git add schemas Directory.Packages.props tests/SqlSource.Tests .editorconfig .gitattributes
```

```bash
git commit -m "Ship the schema of a sidecar and hold the writer to it

schemas/sidecar-v1.schema.json is the schema of the format design.  The
tests validate the examples and every sidecar the tool can build
against it, with JsonSchema.Net in the test project alone.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The guidance and the outline

**Files:**
- Modify: `src/SqlSource/AGENTS.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the row of 2.1)

**Interfaces:**
- Produces: nothing.

- [ ] **Step 1: Add the section on `Snapshot/` to `src/SqlSource/AGENTS.md`**

In the list at the top of the file, extend the performance item: after ``and `tests/SqlSource.Tests/Generation/PathResolverAllocationTests.cs` one for resolving a type's `Path`.`` add:

```markdown
`tests/SqlSource.Tests/Snapshot/SidecarReaderAllocationTests.cs` holds one for reading a sidecar.
```

Before the maintenance footer, after the section on `Parsing/`, add:

```markdown
## `Snapshot/`

The model of a sidecar, `<file>.sql.json`, with its reader, its writer and the two comparisons.  The command-line tool and the generator share it.  No step of the pipeline reads a sidecar yet.

- **The format is a contract** with every committed sidecar and with `schemas/sidecar-v1.schema.json`.  The sidecar format design, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, is that contract, and says which changes are additive and which need a new format version.  A change to what `SidecarWriter` gives is a change to the format design, to the schema, and to the examples in the document and in `tests/SqlSource.Tests/Snapshot/Examples/`, in one pull request.
- **The reader never throws**, whatever text it is given: a file a user can commit must not cost every type its generated code.  `SidecarReader.Read` checks the grammar of the whole text first, with `SidecarTokenizer.MaxDepth` as the limit of nesting, and only then reads the model with `SidecarCursor`, which checks no grammar.  Do not hand a cursor a text that the first pass has not passed.  A file the tool wrote is tokenized twice and no more: `formatVersion` is found before the queries, and an entry's `parameters` and `columns` are read where they stand because `engine` came before them.  An array that comes before its `engine` is passed over and read once the entry ends; no file of the tool takes that path, so `Read_MistakeInAnArrayBeforeTheEngine_IsFound` and the reordered file of the robustness tests are what hold it.  `tests/SqlSource.Tests/Snapshot/SidecarReaderRobustnessTests.cs` cuts and damages the examples everywhere.
- **The reader is lenient and the schema is strict.**  The reader skips a key it does not know, reads an absent "always" key as null, keeps a provenance value and a PostgreSQL kind it does not know, and reads a type under an engine it does not know as its name.  The schema says what this version of the tool writes.  A new key is read by adding it to the model, to the list of keys of its object and to the `switch` beside the list.
- **Every name of a key is in `SidecarKeys`, and every value the tool writes in `SidecarValues`.**  A test compares each list of `SidecarValues` with the schema's.
- **The writer goes by the condition, not by the value.**  An entry without rows has no `columns`, whatever the model holds; the table in the remarks of `SidecarWriter` is the rule.  It checks nothing and never throws.
- **`Snapshot/` uses `EquatableArray<T>` and `TextSpan` and nothing of `Parsing/`, `Generation/`, `Settings/` or `Diagnostics/`.**  The names of the two engines are therefore written twice, in `SidecarValues.Engine` and in `Parsing/SqlDialectName.cs`, and a test holds them together.
- **`SidecarComparer` compares what `describe --check` compares, and no more.**  Adding a key to the format does not add it to the comparison: the format design's section 3 lists what is compared.
- **`PackageVersion.Prefix`**, in the root namespace, is the tool's version as a sidecar records it: the first three parts of the assembly's version, so that no suffix reaches a committed file.
```

- [ ] **Step 2: Set the outline's row to Done**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, the row that starts `| 2.1 The snapshot format | In progress |` becomes `| 2.1 The snapshot format | Done |`.  Read the outline's section "Phase 2 - the tool and the snapshot", item 1, and the row's last column: if what was delivered differs from what they say, say so there in a sentence.  The departures at the top of this plan are of detail and need no sentence, except this one, which a later sub-phase reads: add to item 1 of that section, after its last sentence:

```markdown
`PackageVersion` is in the root namespace, not in `Snapshot/`.
```

- [ ] **Step 3: Check the documents that should not have changed**

Run: `git diff --stat main -- README.md CONTRIBUTING.md docs/publishing.md docs/diagnostics.md src/SqlSource/SqlSourceGenerator.cs src/SqlSource/Generation src/SqlSource/packages.lock.json src/SqlSource/AnalyzerReleases.Unshipped.md`
Expected: no output.  A user can do nothing with a sidecar before phase 5, and the generator gained no step, no diagnostic and no dependency.

Run: `git diff --stat main -- tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs tests/SqlSource.Tests/Generation/PathResolverAllocationTests.cs tests/SqlSource.Tests/Generator`
Expected: no output.

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh`.  Expected: every step `passed`.

```bash
git add src/SqlSource/AGENTS.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Document the snapshot format and close phase 2.1

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Prepare the pull request**

Run the version check of the Global Constraints.  The pull request's description states, as `src/SqlSource/AGENTS.md` asks of a hot path: what a read of the first example allocates for each character, from task 4, in Debug and in Release; that nothing in the pipeline changed, so no existing budget moved; the validator chosen, JsonSchema.Net 9.4.0, and that it is a dependency of the test project alone; and the departures from the spec listed at the top of this plan.
