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
        ShouldDiffer(entry => entry with { ResultKind = SidecarResultKind.None }, "resultKind", "\"rows\"", "\"none\"");
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
        ShouldDiffer(entry => entry with { Columns = Of(entry.Columns!.Value[0]) }, "columns.length", "3", "1");
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
        var other = Tags with { Element = Tags.Element! with { Base = Text with { InternalName = "varchar" } } };

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
            entry =>
                WithParameter(entry, 1, parameter => parameter with { Type = Status with { Labels = Of("active") } }),
            "parameters[1].type.labels.length",
            "2",
            "1"
        );
        ShouldDiffer(
            entry =>
                WithParameter(
                    entry,
                    1,
                    parameter => parameter with { Type = Status with { Labels = Of("active", "gone") } }
                ),
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
            entry =>
                WithColumn(
                    WithColumn(entry, 0, column => column with { Nullable = null }),
                    1,
                    _ => entry.Columns!.Value[0]
                ),
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

        return entry with
        {
            Parameters = Of(parameters),
        };
    }

    private static SidecarEntry WithColumn(SidecarEntry entry, int index, Func<SidecarColumn, SidecarColumn> change)
    {
        var current = entry.Columns.ShouldNotBeNull();
        var columns = new SidecarColumn[current.Count];
        for (var position = 0; position < columns.Length; position++)
        {
            columns[position] = position == index ? change(current[position]) : current[position];
        }

        return entry with
        {
            Columns = Of(columns),
        };
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
