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
    private const string IntType = /*lang=json*/
        "{'name': 'integer', 'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'}";

    private const string TextType = /*lang=json*/
        "{'name': 'text', 'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'text'}";

    private const string ArrayType =
        "{'name': 'text[]', 'kind': 'array', 'schema': 'pg_catalog', 'internalName': '_text', 'element': "
        + TextType
        + "}";

    private const string ParametersJson =
        "[{'name': 'id', 'ordinal': 0, 'type': " + IntType + ", 'nullable': null, 'typeSource': 'inferred'}]";

    private const string OriginJson = /*lang=json*/
        "{'schema': 'public', 'table': 'users', 'column': 'tags'}";

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

    private const string SqlServerType = /*lang=json*/
        "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0}";

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
        var status = Postgres(
            "public.user_status",
            "enum",
            "public",
            "user_status",
            labels: ["active", "suspended", "deleted"]
        );

        var getUser = sidecar.Find("GetUser").ShouldNotBeNull();
        getUser.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        getUser.TableMatch.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        getUser.MatchesTable.ShouldBeNull();
        var columns = getUser.Columns.ShouldNotBeNull();
        columns.Count.ShouldBe(5);
        columns[0]
            .ShouldBe(
                Column(
                    0,
                    "id",
                    Postgres(),
                    origin: new SidecarOrigin("public", "users", "id"),
                    identity: true,
                    computed: false
                )
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

        sidecar
            .Queries.Select(entry => entry.Name)
            .ShouldBe(["GetOrder", "CreateOrder", "ArchiveOrder", "SearchOrders"]);
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
    [InlineData(
        "'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'",
        "'schema': 's', 'internalName': 'i'",
        "{'name': 'integer'",
        "kind"
    )]
    [InlineData(
        "'schema': 'pg_catalog', 'internalName': 'int4'",
        "'internalName': 'int4'",
        "{'name': 'integer'",
        "schema"
    )]
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
    [InlineData( /*lang=json*/
        "{'toolVersion': '1.2.3', 'queries': {}}",
        "formatVersion"
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 1, 'toolVersion': '1.2.3'}",
        "queries"
    )]
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
        entry
            .Columns.ShouldNotBeNull()[0]
            .ShouldBe(new SidecarColumn(0, "id", Postgres(), null, null, null, null, null));
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
    [InlineData(
        "'kind': 'base', 'schema': 'pg_catalog', 'internalName': 'int4'",
        "'kind': null, 'schema': 's', 'internalName': 'i'",
        "null, 'schema': 's'",
        "kind"
    )]
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
    [InlineData(
        "'matchesTable': {'schema': 'public', 'table': 'users'}",
        "'matchesTable': []",
        "[], 'plan'",
        2,
        "matchesTable"
    )]
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
    [InlineData(
        "postgres", /*lang=json*/
        "{'name': 'n', 'kind': 'base', 'schema': 's', 'internalName': 'i', 'length': '100'}",
        "'100'",
        5,
        "length"
    )]
    [InlineData(
        "postgres", /*lang=json*/
        "{'name': 'n', 'kind': 'base', 'schema': 's', 'internalName': 'i', 'scale': 1.5}",
        "1.5",
        3,
        "scale"
    )]
    [InlineData(
        "postgres", /*lang=json*/
        "{'name': 'n', 'kind': 'enum', 'schema': 's', 'internalName': 'i', 'labels': ['a', 1]}",
        "1]",
        1,
        "labels"
    )]
    [InlineData(
        "postgres", /*lang=json*/
        "{'name': 'n', 'kind': 'enum', 'schema': 's', 'internalName': 'i', 'labels': 'a'}",
        "'a'}",
        3,
        "labels"
    )]
    [InlineData(
        "postgres", /*lang=json*/
        "{'name': 'n', 'kind': 'range', 'schema': 's', 'internalName': 'i', 'subtype': []}",
        "[]}",
        2,
        "subtype"
    )]
    [InlineData(
        "mssql", /*lang=json*/
        "{'name': 'int', 'maxLength': 'x', 'precision': 10, 'scale': 0}",
        "'x'",
        3,
        "maxLength"
    )]
    [InlineData(
        "mssql", /*lang=json*/
        "{'name': 'int', 'maxLength': null, 'precision': 10, 'scale': 0}",
        "null, 'precision'",
        4,
        "maxLength"
    )]
    [InlineData(
        "mssql", /*lang=json*/
        "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0, 'userType': 5}",
        "5}",
        1,
        "userType"
    )]
    [InlineData(
        "mssql", /*lang=json*/
        "{'name': 'int', 'maxLength': 4, 'precision': 10, 'scale': 0, 'userType': {'name': 5}}",
        "5}",
        1,
        "name"
    )]
    [InlineData(
        "sqlite", /*lang=json*/
        "{'name': 5}",
        "5}",
        1,
        "name"
    )]
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
    [InlineData(
        "'formatVersion': 1",
        "'formatVersion': 1, 'formatVersion':1",
        "'formatVersion':1",
        15,
        "formatVersion"
    )]
    [InlineData(
        "'toolVersion': '1.2.3'",
        "'toolVersion': '1.2.3', 'toolVersion': '9'",
        "'toolVersion': '9'",
        13,
        "toolVersion"
    )]
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
            /*lang=json*/
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
    [InlineData( /*lang=json*/
        "{\"formatVersion\": 1,}",
        20,
        1
    )]
    [InlineData( /*lang=json*/
        "{\"formatVersion\": 1 /* c */}",
        20,
        1
    )]
    [InlineData( /*lang=json*/
        "{\"formatVersion\": 1, // c\n\"toolVersion\": \"1\"}",
        21,
        1
    )]
    [InlineData("{\"formatVersion\": 1", 19, 0)]
    [InlineData( /*lang=json*/
        "{\"formatVersion\": 01}",
        19,
        1
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 1}",
        1,
        1
    )]
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
    [InlineData( /*lang=json*/
        "{'formatVersion': 2, 'toolVersion': '9.0.0', 'queries': {}}",
        2
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 2, 'queries': 5, 'toolVersion': null}",
        2
    )]
    [InlineData( /*lang=json*/
        "{'queries': {'Q': {'hash': 5}}, 'formatVersion': 3}",
        3
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 0}",
        0
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': -1}",
        -1
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 2147483647}",
        int.MaxValue
    )]
    // What format 1 calls a mistake is not judged in a file of another format.
    [InlineData( /*lang=json*/
        "{'formatVersion': 2, 'toolVersion': 5, 'toolVersion': 6, 'queries': 1, 'queries': 2}",
        2
    )]
    [InlineData( /*lang=json*/
        "{'toolVersion': 5, 'toolVersion': 6, 'formatVersion': 2}",
        2
    )]
    public void Read_AnotherFormatVersion_IsItsVersionAlone(string text, int version) =>
        SidecarReader.Read(Q(text)).ShouldBe(new SidecarReadResult(null, version, null));

    // A second formatVersion is a mistake in any version, wherever the first one stopped the search for it.
    [Theory]
    [InlineData( /*lang=json*/
        "{'formatVersion': 2, 'x': {}, 'formatVersion' :2}"
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 2, 'x': {}, 'formatVersion' :1}"
    )]
    [InlineData( /*lang=json*/
        "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {}, 'formatVersion' :2}"
    )]
    public void Read_SecondFormatVersion_IsDuplicateKey(string text) =>
        ShouldBeMalformed(Q(text), SidecarErrorKind.DuplicateKey, "'formatVersion' :", 15, "formatVersion");

    [Fact]
    public void Read_EngineTheReaderDoesNotKnow_ReadsEachTypeAsItsName()
    {
        var text = FileOf(
            "sqlite", /*lang=json*/
            "{'name': 'INTEGER', 'affinity': 'int', 'kind': 5, 'maxLength': 'x'}"
        );

        var entry = ShouldBeRead(text).Queries[0];

        entry.Engine.ShouldBe("sqlite");
        entry.Columns.ShouldNotBeNull()[0].Type.ShouldBe(new OtherEngineType("INTEGER"));
    }

    [Fact]
    public void Read_EngineTheReaderDoesNotKnow_StillNeedsATypesName() =>
        ShouldBeMalformed(
            FileOf(
                "sqlite", /*lang=json*/
                "{'affinity': 'int'}"
            ),
            SidecarErrorKind.MissingKey,
            "{'affinity'",
            1,
            "name"
        );

    // An unknown kind is the "unsupported type" of phase 5, at the column: the reader keeps it and asks nothing more.
    [Fact]
    public void Read_KindTheReaderDoesNotKnow_KeepsItAndRequiresNoNestedType()
    {
        var text = FileOf(
            "postgres", /*lang=json*/
            "{'name': 'n', 'kind': 'pseudo', 'schema': 's', 'internalName': 'i'}"
        );

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
            .Replace(
                Q("'schema': 'public', 'table': 'users'}"),
                Q(Unknown + "'schema': 'public', 'table': 'users'}"),
                StringComparison.Ordinal
            )
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
    [InlineData( /*lang=json*/
        "[{'name': 'a', 'ordinal': 1, 'nullable': null}]",
        "[]",
        "rows",
        "OrdinalMismatch",
        "1, 'nullable'",
        1,
        "1"
    )]
    [InlineData( /*lang=json*/
        "[{'name': 'a', 'nullable': null}]",
        "[]",
        "rows",
        "MissingKey",
        "{'name': 'a'",
        1,
        "ordinal"
    )]
    [InlineData(
        "[]", /*lang=json*/
        "[{'ordinal': 0, 'name': 'c', 'type': {'kind': 'base'}, 'nullable': null}]",
        "rows",
        "MissingKey",
        "{'kind'",
        1,
        "name"
    )]
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
            .ShouldBe(SidecarOf(Entry(engine: SidecarValues.Engine.SqlServer, resultKind: SidecarResultKind.None)));
    }

    [Fact]
    public void Read_NoQueries_IsAnEmptySidecar() =>
        ShouldBeRead(
            Q( /*lang=json*/
                "{'formatVersion': 1, 'toolVersion': '1.2.3', 'queries': {}}"
            )
        )
            .Queries.Count.ShouldBe(0);

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
