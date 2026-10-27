using Shouldly;
using SqlSource.Generation;
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
    // The places of a farther and of a nearer settings file.
    private static readonly SettingPlace Farther = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    private static readonly SettingPlace Nearer = SettingPlace.OfSettingsFile("/app/Repo/sqlsource.json", 2);

    [Fact]
    public void Collect_NoOverride_GivesNoNames()
    {
        var names = MappedTypeNames.Collect(EquatableArray<FileLevel>.Empty, TestModels.Array(Read(SqlTexts.GetUser)));

        names.ShouldBeEmpty();
    }

    // Two files' levels, a file's header and a query each name a type.  The header's reaches both queries, so it is
    // met twice and given once.  An entry of default names none.
    [Fact]
    public void Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder()
    {
        var files = TestModels.Array(
            new FileLevel(
                "app/Other.sql",
                Level(new TypeOverride("int4", "long", Farther), new TypeOverride("uuid", null, Farther))
            ),
            new FileLevel(
                GeneratorHarness.UsersNormalizedPath,
                Level(new TypeOverride("text", "global::MyApp.Name", Nearer))
            )
        );
        var parsed = TestModels.Array(
            Read(
                "-- type-override: int8 = MyApp.Header\n\n"
                    + "-- name: GetUser\n-- type-override: numeric = System.Decimal\nSELECT 1;\n\n"
                    + "-- name: ListUsers\nSELECT 2;\n"
            )
        );

        var names = MappedTypeNames.Collect(files, parsed);

        names.ShouldBe(["global::MyApp.Header", "global::MyApp.Name", "global::System.Decimal", "long"]);
    }

    // The column types name types at each file's level, beside the type override's.
    [Fact]
    public void Collect_ColumnTypes_GiveTheirTypes()
    {
        var files = TestModels.Array(
            new FileLevel(
                "app/Other.sql",
                new SettingsLevel
                {
                    TypeOverrides = TestModels.Array(new TypeOverride("int4", "long", Farther)),
                    ColumnTypes = TestModels.Array(
                        new ColumnType("posts.id", null, Farther),
                        new ColumnType("users.id", "UserId", Farther)
                    ),
                }
            ),
            new FileLevel(
                GeneratorHarness.UsersNormalizedPath,
                new SettingsLevel
                {
                    ColumnTypes = TestModels.Array(new ColumnType("users.id", "global::MyApp.UserId", Nearer)),
                }
            )
        );

        var names = MappedTypeNames.Collect(files, TestModels.Array(Read(SqlTexts.GetUser)));

        names.ShouldBe(["UserId", "global::MyApp.UserId", "long"]);
    }

    // A settings file's level is the level of every file in its folder, and its types are named once.  A file whose
    // chain has a nearer settings file has that chain's level, which adds its own.
    [Fact]
    public void Collect_FilesOfOneChain_GiveItsTypesOnce()
    {
        var chain = new SettingsLevel { TypeOverrides = TestModels.Array(new TypeOverride("int4", "long", Farther)) };
        var files = TestModels.Array(
            new FileLevel("app/Repo/A.sql", chain),
            new FileLevel("app/Repo/B.sql", Level(new TypeOverride("text", "global::MyApp.Name", Nearer)).Over(chain)),
            new FileLevel("app/Repo/C.sql", chain)
        );

        var names = MappedTypeNames.Collect(files, TestModels.Array(Read(SqlTexts.GetUser)));

        names.ShouldBe(["global::MyApp.Name", "long"]);
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

        var names = MappedTypeNames.Collect(EquatableArray<FileLevel>.Empty, parsed);

        names.ShouldBe(["PostCount", "global::MyApp.PostId"]);
    }

    private static SettingsLevel Level(params TypeOverride[] entries) =>
        new() { TypeOverrides = TestModels.Array(entries) };

    private static ParsedSqlFile Read(string text) =>
        SqlFileReader.Read(
            new InMemoryAdditionalText(GeneratorHarness.UsersPath, text),
            SqlPath.Normalize(GeneratorHarness.UsersPath),
            SqlDialect.Ansi,
            false,
            TestContext.Current.CancellationToken
        );
}
