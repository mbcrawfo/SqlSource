using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Types;

public sealed class MappedTypeNamesTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Fact]
    public void Collect_NoOverride_GivesNoNames()
    {
        var names = MappedTypeNames.Collect(
            new ProjectSettings(SettingsLevel.None, EquatableArray<InvalidSetting>.Empty),
            EquatableArray<FileSettings>.Empty,
            TestModels.Array(Read("-- name: GetUser\nSELECT 1;\n"))
        );

        names.ShouldBeEmpty();
    }

    // The property, a file's metadata, a file's header and a query each name a type.  The header's reaches both
    // queries, so it is met twice and given once.  An entry of default names none.
    [Fact]
    public void Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder()
    {
        var project = new ProjectSettings(
            Level(
                new TypeOverride("int4", "long", SettingPlace.Property),
                new TypeOverride("uuid", null, SettingPlace.Property)
            ),
            EquatableArray<InvalidSetting>.Empty
        );
        var files = TestModels.Array(
            new FileSettings(
                "app/Repo/Users.sql",
                Level(new TypeOverride("text", "global::MyApp.Name", SettingPlace.Metadata)),
                EquatableArray<InvalidSetting>.Empty
            )
        );
        var parsed = TestModels.Array(
            Read(
                "-- type-override: int8 = MyApp.Header\n\n"
                    + "-- name: GetUser\n-- type-override: numeric = System.Decimal\nSELECT 1;\n\n"
                    + "-- name: ListUsers\nSELECT 2;\n"
            )
        );

        var names = MappedTypeNames.Collect(project, files, parsed);

        names.ShouldBe(["global::MyApp.Header", "global::MyApp.Name", "global::System.Decimal", "long"]);
    }

    private static SettingsLevel Level(params TypeOverride[] entries) =>
        new() { TypeOverrides = TestModels.Array(entries) };

    private static ParsedSqlFile Read(string text) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(Path, text),
            SqlPath.Normalize(Path)!,
            SqlDialect.Ansi,
            null,
            false,
            TestContext.Current.CancellationToken
        );
}
