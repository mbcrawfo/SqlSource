using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class SettingsFileLibraryUseTests
{
    private const string AppPath = "/app/sqlsource.json";

    // NodaTime on, on the second line, from its key to its value.
    private const string NodaTimeOn = /*lang=json,strict*/
        "{\n  \"libraryTypes\": {\"nodaTime\": \"on\"}\n}";

    private static readonly SettingsFileLibraryUse Use = new(
        AppPath,
        SqlLibrary.NodaTime,
        DatabaseEngine.PostgreSql,
        "Npgsql.NodaTime"
    );

    [Fact]
    public void Locate_UseOfAFileOfTheSet_IsReportedAtItsMember()
    {
        var diagnostic = SettingsFileLibraryUse.Locate(Uses(Use), Set(AppPath, NodaTimeOn)).ShouldHaveSingleItem();

        diagnostic.Descriptor.Id.ShouldBe("SQLSRC281");
        diagnostic.Location.Path.ShouldBe(AppPath);
        diagnostic.Location.Span.ShouldBe(
            new TextSpan(NodaTimeOn.IndexOf("\"nodaTime\"", System.StringComparison.Ordinal), 16)
        );
        diagnostic.Location.LineSpan.ShouldBe(new LinePositionSpan(new LinePosition(1, 19), new LinePosition(1, 35)));
        diagnostic.Arguments.ShouldBe(["NodaTime", "PostgreSQL", "Npgsql.NodaTime"]);
    }

    // The uses are planned from the run's settings files, so a use always finds its file and member.  One that does
    // not is left out rather than thrown on, since an exception costs every type its code.
    [Fact]
    public void Locate_FileNotInTheSet_ReportsNothing() =>
        SettingsFileLibraryUse.Locate(Uses(Use), Set("/other/sqlsource.json", NodaTimeOn)).ShouldBeEmpty();

    [Fact]
    public void Locate_PathThatDoesNotNormalize_ReportsNothing() =>
        SettingsFileLibraryUse
            .Locate(Uses(Use with { Path = "/../sqlsource.json" }), Set("/sqlsource.json", NodaTimeOn))
            .ShouldBeEmpty();

    [Fact]
    public void Locate_FileWithoutTheMember_ReportsNothing() =>
        SettingsFileLibraryUse.Locate(Uses(Use), Set(AppPath, "{}")).ShouldBeEmpty();

    // Two types that one place reaches give one use, and the uses are in the order of path, ordinal, library and
    // engine, whatever the order of the types.
    [Fact]
    public void Distinct_UsesOfEveryType_AreEachOnceInOrder()
    {
        var sqlServer = Use with { Engine = DatabaseEngine.SqlServer, MissingAssemblies = "NodaTime" };
        var root = Use with { Path = "/sqlsource.json" };

        var distinct = SettingsFileLibraryUse.Distinct([Uses(sqlServer, Use), Uses(Use, root)]);

        distinct.ShouldBe([Use, sqlServer, root]);
    }

    private static EquatableArray<SettingsFileLibraryUse> Uses(params SettingsFileLibraryUse[] uses) =>
        new(ImmutableArray.Create(uses));

    // The set of one settings file at the path with the text.
    private static SettingsFileSet Set(string path, string text) =>
        SettingsFileSet.Of([
            ListedSettingsFile
                .Read(new InMemoryAdditionalText(path, text), SettingsFileSide.Build, CancellationToken.None)
                .ShouldNotBeNull(),
        ]);
}
