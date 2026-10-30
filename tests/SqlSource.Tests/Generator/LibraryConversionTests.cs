// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Converters;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The class of a library's conversions, SqlSource.NodaTimeConversions, which a project gets once, in a file of its
// own, when a method of a CodeGen query calls one of its conversions, and never otherwise.  Each run is under mssql on
// SqlClient 7 with NodaTime 3.3.7, unless it says otherwise, with an entry written for each query's current hash.
public sealed class LibraryConversionTests
{
    // The file of the class, as the tests name it, not taken from the product.
    private const string ClassFile = "SqlSource-NodaTimeConversions.g.cs";

    private const string Conversions = "global::SqlSource.NodaTimeConversions.";

    private const string DateTime = "global::System.DateTime";

    private const string Instant = "global::NodaTime.Instant";

    private const string NodaTimeOn = "-- library-types: nodatime=on\n\n";

    // A query that reads a date and finds by one.
    private const string FindDates = "-- name: FindDates\nSELECT d FROM dates WHERE d = @d;\n";

    private static readonly SqlServerType Date = TestSidecars.SqlServer("date", 3, 10);

    private static readonly SqlServerType DateTime2 = TestSidecars.SqlServer("datetime2", 8, 27, 7);

    // SqlClient 7 and NodaTime 3.3.7.
    private static MetadataReference[] WithNodaTime =>
        [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")];

    [Fact]
    public void Run_NodaTimeOnAndACodeGenQuery_EmitsTheClass()
    {
        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime);

        run.ShouldBeClean();
        run.Sources[ClassFile].ShouldBe(NodaTimeConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(" + Conversions + "LocalDateFromDateTime(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromLocalDate(parameters.D);\n");
    }

    // The class's file starts as every generated file does.
    [Fact]
    public void Run_NodaTimeOnAndACodeGenQuery_EmitsAFileThatStartsAsAGeneratedOne()
    {
        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime);

        run.Sources[ClassFile].ShouldStartWith(GeneratedFile.ModelHeader);
        run.Sources[ClassFile].ShouldContain("namespace SqlSource\n{\n    internal static class NodaTimeConversions\n");
    }

    // A type at Models has the library's types in no model and calls nothing.
    [Fact]
    public void Run_NodaTimeOnAndOnlyAModelsQuery_EmitsNoClass()
    {
        const string Models = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Sample;
            }
            """;

        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime, Models);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // NodaTime is on, and the project does not reference it: SQLSRC281, the base map's types, and no class.
    [Fact]
    public void Run_NodaTimeOnWithoutTheAssembly_EmitsNoClass()
    {
        var run = RunDates(NodaTimeOn + FindDates, [SqlClientReferences.SqlClient7]);

        run.Problems().ShouldHaveSingleItem().ShouldStartWith("SQLSRC281 ");
        run.Sources.Keys.ShouldNotContain(ClassFile);
        run.Sources[MethodRuns.MethodsFile].ShouldContain("parameter.Value = parameters.D;\n");
    }

    // NodaTime referenced and nothing that turns it on or names one of its types: the methods are what they were
    // without the reference, byte for byte, and there is no class.
    [Fact]
    public void Run_NodaTimeReferencedAndNothingOn_IsUnchanged()
    {
        var without = RunDates(FindDates, [SqlClientReferences.SqlClient7]);
        var with = RunDates(FindDates, WithNodaTime);

        without.ShouldBeClean();
        with.ShouldBeClean();
        with.Sources[MethodRuns.MethodsFile].ShouldBe(without.Sources[MethodRuns.MethodsFile]);
        with.Sources.Keys.ShouldNotContain(ClassFile);
        without.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // On PostgreSQL the plugin teaches Npgsql NodaTime's types, so its rows go to the driver and call nothing.
    [Fact]
    public void Run_PostgresPluginRow_EmitsNoClass()
    {
        var timestamptz = Catalog("timestamptz") with { Name = "timestamp with time zone" };
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListEvents\nSELECT at FROM events;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "ListEvents", [], [Column("at", timestamptz)])],
            references:
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3"),
            ]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<" + Instant + ">(0)");
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // An override to one of NodaTime's types finds the library's edge whether the switch is on or not, and the class
    // is emitted for it.
    [Theory]
    [InlineData("datetime2", "NodaTime.Instant", "InstantFromDateTime", "DateTimeFromInstant")]
    [InlineData("time", "NodaTime.Duration", "DurationFromTimeSpan", "TimeSpanFromDuration")]
    [InlineData("datetimeoffset", "NodaTime.Instant", "InstantFromDateTimeOffset", "DateTimeOffsetFromInstant")]
    public void Run_OverrideWithTheSwitchOff_GetsTheConversion(
        string databaseType,
        string type,
        string read,
        string write
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: "
                + databaseType
                + "="
                + type
                + "\n-- name: FindAt\nSELECT at FROM events WHERE at = @at;\n"
        );
        var sqlType = TypeOf(databaseType);

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "FindAt",
                    [TestSidecars.Parameter(0, "at", sqlType, false)],
                    [TestSidecars.Column(0, "at", sqlType)]
                ),
            ],
            references: WithNodaTime
        );

        run.ShouldBeClean();
        run.Sources[ClassFile].ShouldBe(NodaTimeConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Conversions + read + "(reader.GetFieldValue<");
        methods.ShouldContain("parameter.Value = " + Conversions + write + "(parameters.At);\n");
    }

    // The project's converter of DateTime to LocalDate replaces the library's read, and not its write, which is the
    // other edge.
    [Fact]
    public void Run_ProjectConverterOfTheEdge_ReplacesTheLibrarys()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public static class Dates
                {
                    [SqlSourceConverter]
                    public static global::NodaTime.LocalDate FromDb(global::System.DateTime value) =>
                        global::NodaTime.LocalDate.FromDateTime(value);
                }
            }
            """;

        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime, Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.Dates.FromDb(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldNotContain("LocalDateFromDateTime");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromLocalDate(parameters.D);\n");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // A referenced assembly's Lib.Clock, which one query maps a bigint to, declares a converter of DateTime to
    // Instant, which joins the set with Clock's own: the other query's read of a datetime2 overridden to Instant calls
    // it, and its write calls the library's.
    [Fact]
    public void Run_MappedTypeConverter_ReplacesTheLibrarys()
    {
        var library = GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                "Lib",
                """
                namespace Lib;

                public readonly record struct Clock(long Ticks)
                {
                    [SqlSource.SqlSourceConverter]
                    public static Clock FromTicks(long value) => new(value);

                    [SqlSource.SqlSourceConverter]
                    public static long ToTicks(Clock clock) => clock.Ticks;

                    [SqlSource.SqlSourceConverter]
                    public static NodaTime.Instant FromDb(System.DateTime value) =>
                        NodaTime.Instant.FromDateTimeUtc(System.DateTime.SpecifyKind(value, System.DateTimeKind.Utc));
                }
                """,
                references: [LibraryReferences.NodaTime("3.3.7")]
            )
            .Reference;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: bigint=Lib.Clock\n-- type-override: datetime2=NodaTime.Instant\n\n"
                + "-- name: GetClock\nSELECT ticks FROM clocks;\n\n"
                + "-- name: FindAt\nSELECT at FROM events WHERE at = @at;\n"
        );

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "GetClock",
                    [],
                    [TestSidecars.Column(0, "ticks", TestSidecars.SqlServer("bigint", 8, 19))]
                ),
                MethodRuns.SqlServerEntry(
                    file,
                    "FindAt",
                    [TestSidecars.Parameter(0, "at", DateTime2, false)],
                    [TestSidecars.Column(0, "at", DateTime2)]
                ),
            ],
            references: [.. WithNodaTime, library]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::Lib.Clock.FromDb(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldNotContain("InstantFromDateTime");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromInstant(parameters.At);\n");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // Every row of NodaTime's, and the three overrides, read and written, nullable and not, compile with no error and
    // no warning in what is generated, against SqlClient 6.1 and 7 and the oldest NodaTime and the newest.
    [Theory]
    [InlineData(false, "3.1.9")]
    [InlineData(false, "3.3.7")]
    [InlineData(true, "3.1.9")]
    [InlineData(true, "3.3.7")]
    public void Run_GeneratedCode_CompilesWithoutWarnings(bool sqlClient7, string nodaTime)
    {
        SqlServerType[] rows =
        [
            Date,
            TestSidecars.SqlServer("time", 5, 16, 7),
            DateTime2,
            TestSidecars.SqlServer("datetime", 8, 23, 3),
            TestSidecars.SqlServer("smalldatetime", 4, 16),
            TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        ];
        SqlServerType[] overridden =
        [
            DateTime2,
            TestSidecars.SqlServer("time", 5, 16, 7),
            TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        ];
        var everyRow = new SqlFile(GeneratorHarness.UsersPath, NodaTimeOn + EverythingQuery("Rows", rows.Length));
        var overrides = new SqlFile(
            "/app/Repo/Overrides.sql",
            "-- type-override: datetime2=NodaTime.Instant\n-- type-override: time=NodaTime.Duration\n"
                + "-- type-override: datetimeoffset=NodaTime.Instant\n\n"
                + EverythingQuery("Overrides", overridden.Length)
        );

        var run = MethodRuns.RunSqlServer(
            everyRow,
            [EverythingEntry(everyRow, "Rows", rows)],
            references:
            [
                sqlClient7 ? SqlClientReferences.SqlClient7 : SqlClientReferences.SqlClient61,
                LibraryReferences.NodaTime(nodaTime),
            ],
            otherFiles: [overrides, Sidecars.Of(overrides, EverythingEntry(overrides, "Overrides", overridden))]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Conversions + "OffsetDateTimeFromDateTimeOffset(");
        methods.ShouldContain(Conversions + "DurationFromTimeSpan(");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // The type of SQL Server of the name, with the facets the tool writes for it.
    private static SqlServerType TypeOf(string name) =>
        name switch
        {
            "datetime2" => DateTime2,
            "time" => TestSidecars.SqlServer("time", 5, 16, 7),
            _ => TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        };

    // A query of the name that reads v0 to v<count - 1> and maybe_v0 to maybe_v<count - 1>, and finds by parameters
    // of the same names.
    private static string EverythingQuery(string name, int count)
    {
        var names = Enumerable
            .Range(0, count)
            .Select(index => "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return "-- name: "
            + name
            + "\nSELECT "
            + string.Join(", ", names.Select(each => each + ", maybe_" + each))
            + " FROM everything WHERE "
            + string.Join(
                " AND ",
                names.Select(each => each + " = @" + each + " AND maybe_" + each + " = @maybe_" + each)
            )
            + ";\n";
    }

    // The entry of EverythingQuery over these types: each name's parameter and column not null, and each maybe_
    // name's nullable.
    private static SidecarEntry EverythingEntry(SqlFile file, string name, SqlServerType[] types)
    {
        var parameters = types
            .SelectMany(
                (type, index) =>
                    new[]
                    {
                        TestSidecars.Parameter(0, "v" + index, type, false),
                        TestSidecars.Parameter(0, "maybe_v" + index, type, true),
                    }
            )
            .ToArray();
        var columns = types
            .SelectMany(
                (type, index) =>
                    new[]
                    {
                        TestSidecars.Column(0, "v" + index, type, nullable: false),
                        TestSidecars.Column(0, "maybe_v" + index, type, nullable: true),
                    }
            )
            .ToArray();
        return MethodRuns.SqlServerEntry(file, name, parameters, columns);
    }

    // FindDates, or the query of the text, whose column and parameter d are a not-null date, at CodeGen under mssql on
    // the source, App.Sample unless another is given.
    private static GeneratorRun RunDates(string text, MetadataReference[] references, string? source = null)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "FindDates",
                    [TestSidecars.Parameter(0, "d", Date, false)],
                    [TestSidecars.Column(0, "d", Date)]
                ),
            ],
            source ?? MethodRuns.Source,
            references
        );
    }
}
