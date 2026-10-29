using System;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Testing.Generator;

// What the converter tests of tests/SqlSource.Tests/Generator/ share, ConverterMethodTests, ConverterObsoleteTests and
// ConverterArrayTests: the types of App that the converted reads and writes use, the domain email over text and the
// texts of its converters, and a run of a column or a parameter of email.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class ConverterCases
{
    public const string FromDatabase =
        "[SqlSourceConverter] public static Email FromDatabase(string value) => new(value);";

    public const string ToDatabase =
        "[SqlSourceConverter] public static string ToDatabase(Email email) => email.Value;";

    // The types of App that the converted reads and writes use: UserId, a value type over long, whose converters are
    // on Conversions, and Email, a value type or a reference type as KIND says, whose converters are its own.
    private const string AppSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value);

            public static class Conversions
            {
                [SqlSourceConverter]
                public static UserId ToUserId(long value) => new(value);

                [SqlSourceConverter]
                public static long FromUserId(UserId id) => id.Value;
            }

            public KIND Email(string Value)
            {
                [SqlSourceConverter]
                public static Email FromDatabase(string value) => new(value);

                [SqlSourceConverter]
                public static string ToDatabase(Email email) => email.Value;
            }
        }
        """;

    public const string Struct = "readonly record struct";

    public const string Class = "sealed record";

    public const string NullableEmailRead =
        "reader.IsDBNull(0) ? default(global::App.Email?) : "
        + "global::App.Email.FromDatabase(reader.GetFieldValue<string>(0))";

    public const string EmailWrite =
        "parameter.TypedValue = parameters.Email is { } value ? global::App.Email.ToDatabase(value) : null;\n";

    // The domain email over text.
    public static readonly PostgresType Email = TestSidecars.Postgres(
        name: "email",
        kind: SidecarValues.Kind.Domain,
        schema: "public",
        internalName: "email",
        baseType: Text
    );

    public static GeneratorRun RunEmailColumn(string kind, string? source = null)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: GetEmail\nSELECT email FROM users;\n"
        );
        return MethodRuns.Run(
            file,
            [Entry(file, "GetEmail", [], [Column("email", Email, nullable: true)])],
            source ?? AppSourceOf(kind)
        );
    }

    public static GeneratorRun RunEmailParameter(string kind, bool nullable, string? source = null)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: DeleteUser\nDELETE FROM users WHERE email = @email;\n"
        );
        return MethodRuns.Run(
            file,
            [Entry(file, "DeleteUser", [Parameter("email", Email, nullable)], null)],
            source ?? AppSourceOf(kind)
        );
    }

    public static string AppSourceOf(string kind) => AppSource.Replace("KIND", kind, StringComparison.Ordinal);
}
