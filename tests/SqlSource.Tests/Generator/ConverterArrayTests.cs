// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ConverterCases;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The arrays of a converted type: an array whose element an override chose is converted element by element, an array
// override with its own converter is converted whole, and a NULL element is kept or named.  Each run is at CodeGen
// under postgres on Npgsql 10.  The cases are shared with ConverterMethodTests through ConverterCases.
public sealed class ConverterArrayTests
{
    // An array whose element an override chose is read as the base map's array and converted element by element.  A
    // NULL element of a value type cannot be converted, and the method's throw names the column, which Npgsql's does
    // not.
    [Fact]
    public void Run_ArrayOfAnElementsOverride_ConvertsEachElement()
    {
        var run = RunEmailsColumn(Struct, nullable: false);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "(global::System.Array.ConvertAll<string, global::App.Email>(reader.GetFieldValue<string[]>(0), "
                    + "static element => element is { } item ? global::App.Email.FromDatabase(item) : "
                    + "throw new global::System.InvalidCastException(\"Column 'emails' holds a null element.\")))"
            );
    }

    // A NULL element of a reference type stays null, and the converter is not called for it.  The column can be null
    // too, which is checked before the array is read.
    [Fact]
    public void Run_ArrayOfAReferenceElement_KeepsANullElement()
    {
        var run = RunEmailsColumn(Class, nullable: true);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "(reader.IsDBNull(0) ? default(global::App.Email[]?) : "
                    + "global::System.Array.ConvertAll<string, global::App.Email>(reader.GetFieldValue<string[]>(0), "
                    + "static element => element is { } item ? global::App.Email.FromDatabase(item) : null!))"
            );
    }

    // Npgsql refuses an array of a value type that holds a NULL, so the array is read with nullable elements and the
    // method's own throw names the column.
    [Fact]
    public void Run_ArrayOfAValueTypeDatabaseElement_ReadsItNullable()
    {
        var userId = TestSidecars.Postgres(
            name: "user_id",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "user_id",
            baseType: Catalog("int8")
        );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.user_id=App.UserId\n-- name: GetIds\nSELECT ids FROM teams;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetIds", [], [Column("ids", ArrayOf(userId, "_user_id"))])],
            AppSourceOf(Struct)
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "(global::System.Array.ConvertAll<long?, global::App.UserId>(reader.GetFieldValue<long?[]>(0), "
                    + "static element => element is { } item ? global::App.Conversions.ToUserId(item) : "
                    + "throw new global::System.InvalidCastException(\"Column 'ids' holds a null element.\")))"
            );
    }

    // Each element is converted before the array is bound; a NULL element of a reference type is sent as a NULL
    // element.
    [Theory]
    [InlineData(
        Struct,
        "global::System.Array.ConvertAll<global::App.Email, string>(value, "
            + "static element => global::App.Email.ToDatabase(element))"
    )]
    [InlineData(
        Class,
        "global::System.Array.ConvertAll<global::App.Email?, string?>(value, "
            + "static element => element is { } item ? global::App.Email.ToDatabase(item) : null)"
    )]
    public void Run_ArrayParameter_ConvertsEachElement(string kind, string converted)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: DeleteUsers\n"
                + "DELETE FROM users WHERE email = ANY(@emails);\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "DeleteUsers", [Parameter("emails", Emails, nullable: false)], null)],
            AppSourceOf(kind)
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain("parameter.TypedValue = parameters.Emails is { } value ? " + converted + " : null;\n");
    }

    // An override of the array itself takes its own converter, between it and the base map's array, and the
    // element's converters are not used.
    [Fact]
    public void Run_ArrayOverrideWithItsOwnConverter_ConvertsTheWhole()
    {
        var source = AppSourceOf(Struct)
            .Replace(
                "    public static class Conversions",
                """
                    public sealed record Emails(string[] Values)
                    {
                        [SqlSourceConverter]
                        public static Emails FromDatabase(string[] values) => new(values);

                        [SqlSourceConverter]
                        public static string[] ToDatabase(Emails emails) => emails.Values;
                    }

                    public static class Conversions
                """,
                StringComparison.Ordinal
            );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- type-override: public.email[]=App.Emails\n"
                + "-- name: FindEmails\nSELECT emails FROM teams WHERE emails = @emails;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "FindEmails", [Parameter("emails", Emails, nullable: false)], [Column("emails", Emails)])],
            source
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.Emails.FromDatabase(reader.GetFieldValue<string[]>(0)))");
        methods.ShouldContain(
            "parameter.TypedValue = parameters.Emails is { } value ? global::App.Emails.ToDatabase(value) : null;\n"
        );
        methods.ShouldNotContain("ConvertAll");
        methods.ShouldNotContain("global::App.Email.");
    }

    // An override of the array to one of a nullable element, UserId?[], converts by the edges of UserId, and a NULL
    // element stays null, the converter not called for it, either way.  System.Nullable<UserId>[] is the same type.
    [Theory]
    [InlineData("App.UserId?[]")]
    [InlineData("System.Nullable<App.UserId>[]")]
    public void Run_ArrayOverrideOfANullableValueElement_KeepsANullElement(string written)
    {
        var userIds = ArrayOf(
            TestSidecars.Postgres(
                name: "user_id",
                kind: SidecarValues.Kind.Domain,
                schema: "public",
                internalName: "user_id",
                baseType: Catalog("int8")
            ),
            "_user_id"
        );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.user_id[]="
                + written
                + "\n-- name: FindIds\n"
                + "SELECT ids FROM teams WHERE ids = @ids;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "FindIds", [Parameter("ids", userIds, nullable: false)], [Column("ids", userIds)])],
            AppSourceOf(Struct)
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "(global::System.Array.ConvertAll<long?, global::App.UserId?>(reader.GetFieldValue<long?[]>(0), "
                + "static element => element is { } item ? global::App.Conversions.ToUserId(item) : null))"
        );
        methods.ShouldContain(
            "parameter.TypedValue = parameters.Ids is { } value ? global::System.Array.ConvertAll<"
                + "global::App.UserId?, long?>(value, static element => element is { } item ? "
                + "global::App.Conversions.FromUserId(item) : null) : null;\n"
        );
    }

    // A domain over an array is read as its base, the array, so an override of the domain to an array of a type of
    // the project converts element by element, and a NULL element of the value type is the method's own throw.
    [Fact]
    public void Run_DomainOverAnArrayOverriddenToAnArray_ConvertsEachElement()
    {
        var ids = TestSidecars.Postgres(
            name: "ids",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "ids",
            baseType: ArrayOf(Catalog("int8"), "_int8")
        );
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.ids=App.UserId[]\n-- name: FindIds\nSELECT ids FROM teams WHERE ids = @ids;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "FindIds", [Parameter("ids", ids, nullable: false)], [Column("ids", ids)])],
            AppSourceOf(Struct)
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "(global::System.Array.ConvertAll<long?, global::App.UserId>(reader.GetFieldValue<long?[]>(0), "
                + "static element => element is { } item ? global::App.Conversions.ToUserId(item) : "
                + "throw new global::System.InvalidCastException(\"Column 'ids' holds a null element.\")))"
        );
        methods.ShouldContain(
            "value ? global::System.Array.ConvertAll<global::App.UserId, long>(value, "
                + "static element => global::App.Conversions.FromUserId(element))"
        );
    }

    // The array email[] of the domain email.
    private static readonly PostgresType Emails = ArrayOf(Email, "_email");

    private static GeneratorRun RunEmailsColumn(string kind, bool nullable)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: public.email=App.Email\n-- name: GetEmails\nSELECT emails FROM teams;\n"
        );
        return MethodRuns.Run(
            file,
            [Entry(file, "GetEmails", [], [Column("emails", Emails, nullable)])],
            AppSourceOf(kind)
        );
    }
}
