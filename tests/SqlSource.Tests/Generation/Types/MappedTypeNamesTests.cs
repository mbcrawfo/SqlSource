using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Types;

public sealed class MappedTypeNamesTests
{
    [Fact]
    public void Collect_NoOverride_GivesNoNames()
    {
        var names = MappedTypeNames.Collect(
            new ProjectSettings(SettingsLevel.None, EquatableArray<InvalidSetting>.Empty),
            EquatableArray<FileSettings>.Empty,
            TestModels.Array(Read(SqlTexts.GetUser))
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
                GeneratorHarness.UsersNormalizedPath,
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

    // The column types name types at the property and the metadata, beside the type override's.
    [Fact]
    public void Collect_ColumnTypes_GiveTheirTypes()
    {
        var project = new ProjectSettings(
            new SettingsLevel
            {
                TypeOverrides = TestModels.Array(new TypeOverride("int4", "long", SettingPlace.Property)),
                ColumnTypes = TestModels.Array(
                    new ColumnType("posts.id", null, SettingPlace.Property),
                    new ColumnType("users.id", "UserId", SettingPlace.Property)
                ),
            },
            EquatableArray<InvalidSetting>.Empty
        );
        var files = TestModels.Array(
            new FileSettings(
                GeneratorHarness.UsersNormalizedPath,
                new SettingsLevel
                {
                    ColumnTypes = TestModels.Array(
                        new ColumnType("users.id", "global::MyApp.UserId", SettingPlace.Metadata)
                    ),
                },
                EquatableArray<InvalidSetting>.Empty
            )
        );

        var names = MappedTypeNames.Collect(project, files, TestModels.Array(Read(SqlTexts.GetUser)));

        names.ShouldBe(["UserId", "global::MyApp.UserId", "long"]);
    }

    // A file header's -- column: is a column type of its queries' markers, and a query's names its result's column.
    [Fact]
    public void Collect_ColumnMarkers_GiveTheirTypes()
    {
        var parsed = TestModels.Array(
            Read(
                "-- column: posts.id -> MyApp.PostId\n-- column: users.id -> default\n\n"
                    + "-- name: GetUser\n-- column: total -> PostCount\n-- column: note null\n"
                    + "-- column: id -> default\nSELECT 1;\n"
            )
        );

        var names = MappedTypeNames.Collect(
            new ProjectSettings(SettingsLevel.None, EquatableArray<InvalidSetting>.Empty),
            EquatableArray<FileSettings>.Empty,
            parsed
        );

        names.ShouldBe(["PostCount", "global::MyApp.PostId"]);
    }

    private static SettingsLevel Level(params TypeOverride[] entries) =>
        new() { TypeOverrides = TestModels.Array(entries) };

    private static ParsedSqlFile Read(string text) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(GeneratorHarness.UsersPath, text),
            SqlPath.Normalize(GeneratorHarness.UsersPath)!,
            SqlDialect.Ansi,
            null,
            false,
            TestContext.Current.CancellationToken
        );
}
