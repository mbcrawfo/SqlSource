using System;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Types;

// The typer called directly, for what no sidecar the reader accepts can hold.  The driver tests of the type override
// are in tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs.
public sealed class QueryTyperTests
{
    private const string Guid = "global::System.Guid";

    // The reader refuses an array without its element, which only a sidecar edited by hand has.  Such an array has no
    // key to write, so an entry of its own name does not map it: it gets the help to describe again.
    [Fact]
    public void Type_ArrayWithoutItsElement_IsSqlsrc246EvenWithAnEntryOfItsOwnKey()
    {
        var array = TestSidecars.Postgres(
            name: "user_status[]",
            kind: SidecarValues.Kind.Array,
            schema: "public",
            internalName: "_user_status"
        );

        var types = QueryTyper.Type(
            InputOf(array, new TypeOverride("public._user_status", Guid, SettingPlace.Property))
        );

        types.Queries.ShouldBeEmpty();
        var diagnostic = types.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.TypeHasNoCSharpType);
        diagnostic.Arguments.ShouldBe(["column 'c0'", "user_status[]", "Run 'sqlsource describe'"]);
    }

    // The file Users with the one query GetUser, whose entry has a column of the type, under the property's entry.
    private static FileTypesInput InputOf(PostgresType column, TypeOverride entry)
    {
        var file = SqlFileReader.Read(
            new InMemoryAdditionalText("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n"),
            "app/Repo/Users.sql",
            SqlDialect.PostgreSql,
            null,
            false,
            TestContext.Current.CancellationToken
        );
        var sidecarEntry = TestSidecars.Entry("GetUser", columns: [TestSidecars.Column(0, "c0", column)]);

        return new FileTypesInput(
            new SidecarCheckResult(
                file.NormalizedPath,
                EquatableArray<DiagnosticInfo>.Empty,
                TestSidecars.Of(sidecarEntry)
            ),
            file,
            SettingsLevel.None,
            new SettingsLevel { TypeOverrides = TestSidecars.Of(entry) },
            new NpgsqlProbe(true, true, true, new Version(10, 0)),
            TestSidecars.Of(new MappedType(Guid, MappedTypeStatus.Found, true, true, EquatableArray<string>.Empty))
        );
    }
}
