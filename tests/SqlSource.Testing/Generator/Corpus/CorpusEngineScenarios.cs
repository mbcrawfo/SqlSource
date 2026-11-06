using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;

namespace SqlSource.Testing.Generator.Corpus;

// The part of the golden corpus of query methods that holds the scenarios only one engine has, which CorpusEngine.cs
// lists among each engine's own: PostgreSQL's binding, a parameter of each kind of type Npgsql binds, and by-element,
// arrays read and bound through their element's converter; and SQL Server's binding, a parameter of each type
// SqlClient binds, vector, SqlClient's vector read and bound, and spatial, NetTopologySuite's geometries read and bound
// through the shared instances.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // App.Email, a class over string with its converters, beside MethodRuns.KindsSource in PostgreSQL's binding.
    private const string PostgresEmailSource = """
        using SqlSource;

        namespace App
        {
            public sealed record Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }
        }
        """;

    // App.Values.Email, a value type over string with its converters, beside ConverterCases' types of App, whose Email
    // is a class, in by-element.
    private const string ValueEmailSource = """
        using SqlSource;

        namespace App.Values
        {
            public readonly record struct Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }
        }
        """;

    // App.Sample, and the types of SQL Server's binding: Email, a class over string, UserId, a value type over int,
    // and Price, a value type over decimal, each with its converters; App.Types.Money, a stub of a CLR type of a user;
    // and Microsoft.SqlServer.Types.SqlHierarchyId, a stub of the package's.
    private const string SqlServerBindingSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            public sealed record Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }

            public readonly record struct UserId(int Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(int value) => new(value);

                [SqlSourceConverter]
                public static int ToDatabase(UserId id) => id.Value;
            }

            public readonly record struct Price(decimal Value)
            {
                [SqlSourceConverter]
                public static Price FromDatabase(decimal value) => new(value);

                [SqlSourceConverter]
                public static decimal ToDatabase(Price price) => price.Value;
            }
        }

        namespace App.Types
        {
            public sealed class Money;
        }

        namespace Microsoft.SqlServer.Types
        {
            public struct SqlHierarchyId;
        }
        """;

    // App.Sample, and the types of vector: Embedding, a class over SqlClient's vector, and UserId, a value type over
    // int, each with its converters.
    private const string VectorSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            public sealed record Embedding(float[] Values)
            {
                [SqlSourceConverter]
                public static Embedding FromDatabase(Microsoft.Data.SqlTypes.SqlVector<float> value) =>
                    new(value.Memory.ToArray());

                [SqlSourceConverter]
                public static Microsoft.Data.SqlTypes.SqlVector<float> ToDatabase(Embedding embedding) =>
                    new(embedding.Values);
            }

            public readonly record struct UserId(int Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(int value) => new(value);

                [SqlSourceConverter]
                public static int ToDatabase(UserId id) => id.Value;
            }
        }
        """;

    // The settings of spatial, over the project's settings file: NetTopologySuite's types on.
    private const string NetTopologySuiteOn = /*lang=json,strict*/
        """{"libraryTypes": {"netTopologySuite": "on"}}""";

    // The domain public.email over text.
    private static readonly PostgresType PostgresEmail = TestSidecars.Postgres(
        name: "email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "email",
        baseType: ModelRuns.Text
    );

    // The domain public.work_email over public.email.
    private static readonly PostgresType WorkEmail = TestSidecars.Postgres(
        name: "work_email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "work_email",
        baseType: PostgresEmail
    );

    // A base type of the schema my.schema, whose name holds a dot.
    private static readonly PostgresType DottedSchema = TestSidecars.Postgres(
        name: "\"my.schema\".t",
        schema: "my.schema",
        internalName: "t"
    );

    // The domain public.outer_t over the domain public.inner_t over the base type of my.schema, whose innermost base
    // has no canonical name.
    private static readonly PostgresType DomainOverADottedSchema = TestSidecars.Postgres(
        name: "outer_t",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "outer_t",
        baseType: TestSidecars.Postgres(
            name: "inner_t",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "inner_t",
            baseType: DottedSchema
        )
    );

    // public.thing, a type of a kind this version does not know.
    private static readonly PostgresType UnknownKind = TestSidecars.Postgres(
        name: "thing",
        kind: "shell",
        schema: "public",
        internalName: "thing"
    );

    // vector(3) of SQL Server.
    private static readonly SqlServerType Vector3 = TestSidecars.SqlServer("vector(3)", 20, 0);

    // PostgreSQL's binding: for each kind of type Npgsql binds, a -> none query of a parameter of it that cannot be
    // null and one that can.  Each row of MethodRuns.ParameterKinds; int4 without a bindName, which sets no
    // DataTypeName; a domain over a domain, bound by its innermost base, and an array of it, bound by its bindName; a
    // type whose schema holds a dot, an array of it, one whose name holds one, a domain over a domain over the first,
    // and a type of a kind this version does not know, each bound by its bindName, which is not its canonical name; an
    // array of that type, bound by its canonical name; and App.Email, a class, converted from public.email.  The types
    // the map refuses are given a C# type by an override.
    public static GeneratorRun PostgresBinding(CorpusEngine engine)
    {
        (string Query, string Markers, SidecarType Type)[] types =
        [
            .. MethodRuns.ParameterKinds.Select(row =>
                (char.ToUpperInvariant(row.Kind[0]) + row.Kind[1..], "", (SidecarType)row.Type)
            ),
            ("NoBindName", "", ModelRuns.Int4),
            ("DomainOverADomain", "", WorkEmail with { BindName = "text" }),
            ("ArrayOfADomainOverADomain", "", ModelRuns.ArrayOf(WorkEmail, "_work_email") with { BindName = "text[]" }),
            (
                "DotInTheSchema",
                "-- type-override: \"my.schema\".t=string\n",
                DottedSchema with
                {
                    BindName = "\"my.schema\".t",
                }
            ),
            (
                "ArrayOfADotInTheSchema",
                "-- type-override: \"my.schema\".t=string\n",
                ModelRuns.ArrayOf(DottedSchema, "_t") with
                {
                    BindName = "\"my.schema\".t[]",
                }
            ),
            (
                "DotInTheName",
                "-- type-override: public.\"a.b\"=string\n",
                TestSidecars.Postgres(
                    name: "\"a.b\"",
                    schema: "public",
                    internalName: "a.b",
                    bindName: "public.\"a.b\""
                )
            ),
            (
                "DomainOverADomainOverADotInTheSchema",
                "-- type-override: \"my.schema\".t=string\n",
                DomainOverADottedSchema with
                {
                    BindName = "\"my.schema\".t",
                }
            ),
            ("UnknownKind", "-- type-override: public.thing=string\n", UnknownKind with { BindName = "thing" }),
            (
                "ArrayOfAnUnknownKind",
                "-- type-override: public.thing=string\n",
                ModelRuns.ArrayOf(UnknownKind, "_thing") with
                {
                    BindName = "thing[]",
                }
            ),
            (
                "ConvertedClass",
                "-- type-override: public.email=App.Email\n",
                PostgresEmail with
                {
                    BindName = "public.email",
                }
            ),
        ];
        return BindingQueries(
            engine,
            [
                new SourceFile(GeneratorHarness.SourcePath, MethodRuns.KindsSource),
                new SourceFile("/app/Repo/Email.cs", PostgresEmailSource),
            ],
            MethodRuns.KindsOverride + "\n",
            types
        );
    }

    // Arrays read and bound element by element through their element's converter, on ConverterCases' types of App,
    // whose Email is a class, and App.Values.Email, a value type, in a many, a one, a one-optional and a stream query,
    // and a many query read into a class.  The columns are public.email[] read as App.Email[], whose null element
    // stays null!, and as App.Values.Email[], whose null element throws; one that can be null; and int8[] read as
    // App.UserId[], whose null element throws, one that can be null, and one read as App.UserId?[], whose null element
    // stays null.  The parameters are the same, each element converted unless it is null, and a not-null element of a
    // value type converted directly.
    public static GeneratorRun ByElement(CorpusEngine engine)
    {
        var emails = ModelRuns.ArrayOf(PostgresEmail, "_email") with { BindName = "public.email[]" };
        var ids = ModelRuns.ArrayOf(ModelRuns.Catalog("int8") with { Name = "bigint" }, "_int8") with
        {
            BindName = "bigint[]",
        };
        const string Markers =
            "-- column: optional_ids -> App.UserId?[]\n-- column: value_emails -> App.Values.Email[]\n"
            + "-- param: @optional_ids -> App.UserId?[]\n-- param: @value_emails -> App.Values.Email[]\n";
        const string Select =
            "SELECT emails, maybe_emails, value_emails, ids, maybe_ids, optional_ids FROM teams\n"
            + "WHERE emails = @emails AND maybe_emails = @maybe_emails AND value_emails = @value_emails\n"
            + "AND ids = @ids AND maybe_ids = @maybe_ids AND optional_ids = @optional_ids;\n";
        SidecarParameter[] parameters =
        [
            CorpusEngine.Parameter("emails", emails, nullable: false),
            CorpusEngine.Parameter("maybe_emails", emails, nullable: true),
            CorpusEngine.Parameter("value_emails", emails, nullable: false),
            CorpusEngine.Parameter("ids", ids, nullable: false),
            CorpusEngine.Parameter("maybe_ids", ids, nullable: true),
            CorpusEngine.Parameter("optional_ids", ids, nullable: false),
        ];
        SidecarColumn[] columns =
        [
            CorpusEngine.Column("emails", emails),
            CorpusEngine.Column("maybe_emails", emails, nullable: true),
            CorpusEngine.Column("value_emails", emails),
            CorpusEngine.Column("ids", ids),
            CorpusEngine.Column("maybe_ids", ids, nullable: true),
            CorpusEngine.Column("optional_ids", ids),
        ];
        return EachShape(
            engine,
            [
                new SourceFile(GeneratorHarness.SourcePath, ConverterCases.AppSourceOf(ConverterCases.Class)),
                new SourceFile("/app/Repo/ValueEmail.cs", ValueEmailSource),
            ],
            "-- type-override: public.email=App.Email\n-- type-override: int8=App.UserId\n\n",
            "Team",
            Markers,
            Select,
            parameters,
            columns
        );
    }

    // SQL Server's binding: for each type SqlClient binds, a -> none query of a parameter of it that cannot be null and
    // one that can.  A fixed and a variable length of bytes and of characters, bounded and max, which an over-long
    // value rebinds at max; a decimal, and a numeric of scale 0, whose value is rounded; a datetime2(3), bound at scale
    // 7; json; a vector, whose null is the null vector of its dimension, and one that names its base type; int;
    // sql_variant; an alias type, bound as its base; a decimal of a precision no SqlParameter holds, a vector with no
    // dimension and a type this version does not know, overridden by its sys. key, each bound by value; hierarchyid, a
    // CLR type of sys, with and without its user type, and dbo.Money, a CLR type of a user, each overridden to a stub;
    // and a varchar, an int and a numeric of scale 0 converted through App.Email, App.UserId and App.Price, whose
    // converted value is checked and rounded.
    public static GeneratorRun SqlServerBinding(CorpusEngine engine)
    {
        const string HierarchyId = "-- type-override: hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId\n";
        (string Query, string Markers, SidecarType Type)[] types =
        [
            ("Char", "", TestSidecars.SqlServer("char(10)", 10, 0)),
            ("VarChar", "", TestSidecars.SqlServer("varchar(50)", 50, 0)),
            ("NVarCharMax", "", TestSidecars.SqlServer("nvarchar(max)", -1, 0)),
            ("NChar", "", TestSidecars.SqlServer("nchar(5)", 10, 0)),
            ("Binary", "", TestSidecars.SqlServer("binary(4)", 4, 0)),
            ("VarBinaryMax", "", TestSidecars.SqlServer("varbinary(max)", -1, 0)),
            ("Decimal", "", TestSidecars.SqlServer("decimal(10,2)", 9, 10, 2)),
            ("Numeric", "", TestSidecars.SqlServer("numeric(5,0)", 5, 5, 0)),
            ("DateTime2", "", TestSidecars.SqlServer("datetime2(3)", 7, 23, 3)),
            ("Json", "", TestSidecars.SqlServer("json", -1, 0)),
            ("Vector", "", Vector3),
            ("VectorWithItsBaseType", "", TestSidecars.SqlServer("vector(3, float32)", 20, 0)),
            ("Int", "", TestSidecars.SqlServer()),
            ("SqlVariant", "", TestSidecars.SqlServer("sql_variant", 8016, 0)),
            (
                "Alias",
                "",
                TestSidecars.SqlServer("nvarchar(320)", 640, 0, 0, new SqlServerUserType("dbo", "Email", null))
            ),
            ("WideDecimal", "", TestSidecars.SqlServer("decimal(300,4)", 17, 300, 4)),
            ("VectorWithNoDimension", "", TestSidecars.SqlServer("vector", 20, 0)),
            ("UnknownType", "-- type-override: sys.futuretype=string\n", TestSidecars.SqlServer("futuretype", 8, 0)),
            (
                "HierarchyId",
                HierarchyId,
                TestSidecars.SqlServer(
                    "hierarchyid",
                    892,
                    0,
                    0,
                    new SqlServerUserType(
                        "sys",
                        "hierarchyid",
                        "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types, Version=11.0.0.0, "
                            + "Culture=neutral, PublicKeyToken=89845dcd8080cc91"
                    )
                )
            ),
            ("HierarchyIdWithNoUserType", HierarchyId, TestSidecars.SqlServer("hierarchyid", 892, 0)),
            (
                "Money",
                "-- type-override: dbo.Money=App.Types.Money\n",
                TestSidecars.SqlServer(
                    "Money",
                    -1,
                    0,
                    0,
                    new SqlServerUserType("dbo", "Money", "App.Types.Money, App.Types")
                )
            ),
            ("ConvertedVarChar", "-- type-override: varchar=App.Email\n", TestSidecars.SqlServer("varchar(50)", 50, 0)),
            ("ConvertedInt", "-- type-override: int=App.UserId\n", TestSidecars.SqlServer()),
            (
                "ConvertedNumeric",
                "-- type-override: numeric=App.Price\n",
                TestSidecars.SqlServer("numeric(5,0)", 5, 5, 0)
            ),
        ];
        return BindingQueries(engine, [new SourceFile(GeneratorHarness.SourcePath, SqlServerBindingSource)], "", types);
    }

    // SqlClient's vector, which reads a NULL as its Null: a vector(3) column that cannot be null, read into a local
    // and refused when it IsNull, and one that can, with a parameter of each, in a many, a one, a one-optional and a
    // stream query, and a stream read into a class; the same read and bound through App.Embedding, in a one and a
    // stream query; and a one and a stream query whose token is named value, beside a parameter that can be null
    // converted through App.UserId and a vector column at index 1, so that the converted value is value1 and the
    // vector's local value11.
    public static GeneratorRun Vector(CorpusEngine engine)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: ListDocuments
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: GetDocument -> one
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: FindDocument -> one-optional
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: StreamDocuments
            -- collection-type: IAsyncEnumerable
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: StreamDocumentClasses
            -- collection-type: IAsyncEnumerable
            -- output-model-type: class
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: GetEmbedding -> one
            -- type-override: vector=App.Embedding
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: StreamEmbeddings
            -- collection-type: IAsyncEnumerable
            -- type-override: vector=App.Embedding
            SELECT embedding, draft FROM documents WHERE embedding = @embedding OR draft = @draft;
            -- name: GetByValue -> one
            -- type-override: int=App.UserId
            -- token: {{value:documents}}
            SELECT id, embedding FROM {{value}} WHERE id = @id;
            -- name: StreamByValue
            -- collection-type: IAsyncEnumerable
            -- type-override: int=App.UserId
            -- token: {{value:documents}}
            SELECT id, embedding FROM {{value}} WHERE id = @id;

            """
        );
        SidecarParameter[] parameters =
        [
            CorpusEngine.Parameter("embedding", Vector3, nullable: false),
            CorpusEngine.Parameter("draft", Vector3, nullable: true),
        ];
        SidecarColumn[] columns =
        [
            CorpusEngine.Column("embedding", Vector3),
            CorpusEngine.Column("draft", Vector3, nullable: true),
        ];
        SidecarParameter[] id = [CorpusEngine.Parameter("id", engine.Int, nullable: true)];
        SidecarColumn[] idAndEmbedding =
        [
            CorpusEngine.Column("id", engine.Int),
            CorpusEngine.Column("embedding", Vector3),
        ];
        string[] documents =
        [
            "ListDocuments",
            "GetDocument",
            "FindDocument",
            "StreamDocuments",
            "StreamDocumentClasses",
            "GetEmbedding",
            "StreamEmbeddings",
        ];
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, VectorSource)],
            [
                file,
                Sidecars.Of(
                    file,
                    [
                        .. documents.Select(name => engine.Entry(file, name, parameters, columns)),
                        engine.Entry(file, "GetByValue", id, idAndEmbedding),
                        engine.Entry(file, "StreamByValue", id, idAndEmbedding),
                    ]
                ),
            ]
        );
    }

    // NetTopologySuite's geometries on SQL Server, as SpatialConversionTests reads them: place, a geography that can be
    // null, read as Geometry, and shape, a geometry that cannot, read as a Point by the override, each a column and a
    // parameter, read through the column's stream and bound as SqlBytes, in a many, a one and a stream query.
    public static GeneratorRun Spatial(CorpusEngine engine)
    {
        var geography = SqlServerClr("geography");
        var geometry = SqlServerClr("geometry");
        return LibraryQueries(
            engine,
            MethodRuns.Source,
            "-- type-override: geometry=NetTopologySuite.Geometries.Point\n\n",
            "Place",
            "places",
            "",
            [
                CorpusEngine.Parameter("place", geography, nullable: true),
                CorpusEngine.Parameter("shape", geometry, nullable: false),
            ],
            [CorpusEngine.Column("place", geography, nullable: true), CorpusEngine.Column("shape", geometry)],
            [
                SqlClientReferences.SqlClient7,
                LibraryReferences.NetTopologySuite("2.6.0"),
                LibraryReferences.SqlServerBytes("2.1.0"),
            ],
            NetTopologySuiteOn
        );
    }

    // For each type, a -> none query Bind<Query>, with its markers after its name, that finds by arg, a parameter of
    // the type that cannot be null, and maybe_arg, one that can.  The header starts the file.
    private static GeneratorRun BindingQueries(
        CorpusEngine engine,
        SourceFile[] sources,
        string header,
        (string Query, string Markers, SidecarType Type)[] types
    )
    {
        var text = header;
        foreach (var (query, markers, _) in types)
        {
            text +=
                "-- name: Bind"
                + query
                + " -> none\n"
                + markers
                + "UPDATE things SET seen = 1 WHERE arg = @arg OR maybe_arg = @maybe_arg;\n";
        }

        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        var entries = types
            .Select(type =>
                engine.Entry(
                    file,
                    "Bind" + type.Query,
                    [
                        CorpusEngine.Parameter("arg", type.Type, nullable: false),
                        CorpusEngine.Parameter("maybe_arg", type.Type, nullable: true),
                    ],
                    null
                )
            )
            .ToArray();
        return engine.Run(sources, [file, Sidecars.Of(file, entries)]);
    }

    // List<Noun>s, a many query, Get<Noun>, a one, Find<Noun>, a one-optional, Stream<Noun>s, a stream, and
    // List<Noun>Classes, a many query read into a class, each with the markers after its name and the SQL.  The
    // header starts the file.
    private static GeneratorRun EachShape(
        CorpusEngine engine,
        SourceFile[] sources,
        string header,
        string noun,
        string markers,
        string sql,
        SidecarParameter[] parameters,
        SidecarColumn[] columns
    )
    {
        (string Name, string Shape)[] queries =
        [
            ("List" + noun + "s", ""),
            ("Get" + noun, " -> one"),
            ("Find" + noun, " -> one-optional"),
            ("Stream" + noun + "s", "\n-- collection-type: IAsyncEnumerable"),
            ("List" + noun + "Classes", "\n-- output-model-type: class"),
        ];
        var text = header;
        foreach (var (name, shape) in queries)
        {
            text += "-- name: " + name + shape + "\n" + markers + sql;
        }

        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        var entries = queries.Select(query => engine.Entry(file, query.Name, parameters, columns)).ToArray();
        return engine.Run(sources, [file, Sidecars.Of(file, entries)]);
    }

    // A CLR type of sys, as the tool describes one, as SpatialConversionTests.SqlServerClr gives it.
    private static SqlServerType SqlServerClr(string name) =>
        TestSidecars.SqlServer(
            name,
            maxLength: -1,
            precision: 0,
            userType: new SqlServerUserType("sys", name, "Microsoft.SqlServer.Types, Version=11.0.0.0")
        );
}
