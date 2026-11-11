using SqlSource.Snapshot;

namespace SqlSource.Testing.Generator.Corpus;

// The part of the golden corpus of query methods that holds the scenario converters: parameters and columns read and
// bound through the converters of a value type and of a class, and a column read through an obsolete converter.
// QueryMethodCorpus.cs lists the cases.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // App.Sample, and the types its queries convert: UserId, a value type over int, Email, a class over string, and
    // LegacyId, a value type over int whose converters are obsolete.
    private const string ConvertersSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample;

            public readonly record struct UserId(int Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(int value) => new(value);

                [SqlSourceConverter]
                public static int ToDatabase(UserId id) => id.Value;
            }

            public sealed record Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }

            public readonly record struct LegacyId(int Value)
            {
                [System.Obsolete("Read ids as UserId.")]
                [SqlSourceConverter]
                public static LegacyId FromDatabase(int value) => new(value);

                [System.Obsolete("Write ids as UserId.")]
                [SqlSourceConverter]
                public static int ToDatabase(LegacyId id) => id.Value;
            }
        }
        """;

    // The engine's Int read as App.UserId and its Text as App.Email, in parameters and columns that can be null and
    // that cannot, in a many, a one, a one-optional and a stream query; and ListLegacy, whose query reads its Int as
    // App.LegacyId, so that the methods' file disables the warning of an obsolete member.
    private static GeneratorRun Converters(CorpusEngine engine, string? settings)
    {
        const string Select =
            "SELECT id, manager_id, email, backup_email FROM users\n"
            + "WHERE id = @id OR manager_id = @manager OR email = @email OR backup_email = @backup;\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: "
                + engine.IntOverrideKey
                + "=App.UserId\n-- type-override: "
                + engine.TextOverrideKey
                + "=App.Email\n\n-- name: ListUsers\n"
                + Select
                + "-- name: GetUser -> one\n"
                + Select
                + "-- name: FindUser -> one-optional\n"
                + Select
                + "-- name: StreamUsers\n-- collection-type: IAsyncEnumerable\n"
                + Select
                + "-- name: ListLegacy\n-- type-override: "
                + engine.IntOverrideKey
                + "=App.LegacyId\nSELECT id FROM users;\n"
        );
        SidecarParameter[] parameters =
        [
            CorpusEngine.Parameter("id", engine.Int, nullable: false),
            CorpusEngine.Parameter("manager", engine.Int, nullable: true),
            CorpusEngine.Parameter("email", engine.Text, nullable: false),
            CorpusEngine.Parameter("backup", engine.Text, nullable: true),
        ];
        SidecarColumn[] columns =
        [
            CorpusEngine.Column("id", engine.Int),
            CorpusEngine.Column("manager_id", engine.Int, nullable: true),
            CorpusEngine.Column("email", engine.Text),
            CorpusEngine.Column("backup_email", engine.Text, nullable: true),
        ];
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, ConvertersSource)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(file, "ListUsers", parameters, columns),
                    engine.Entry(file, "GetUser", parameters, columns),
                    engine.Entry(file, "FindUser", parameters, columns),
                    engine.Entry(file, "StreamUsers", parameters, columns),
                    engine.Entry(file, "ListLegacy", [], [CorpusEngine.Column("id", engine.Int)])
                ),
            ],
            settings: settings
        );
    }
}
