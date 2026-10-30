using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting but the type override and the column types resolves by one rule: the most specific level that has a
// value gives it.
public sealed class QuerySettingsTests
{
    private static readonly SettingsLevel Keep = new() { Parameters = GeneratorParameters.KeepComments };

    private static readonly SettingsLevel Sort = new() { Parameters = GeneratorParameters.SortInput };

    private static readonly SettingsLevel Empty = new() { Parameters = GeneratorParameters.None };

    // The place of a settings file, which a file's level holds.
    private static readonly SettingPlace SettingsFile = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    [Fact]
    public void Resolve_NoLevelSaysAnything_GivesTheDefaults()
    {
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None);

        settings.Parameters.ShouldBe(GeneratorParameters.None);
        settings.KeepComments.ShouldBeFalse();
        settings.ValidateTokens.ShouldBeTrue();
        settings.UseArrayPool.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Parameters_ComeWholeFromTheMostSpecificLevelThatGivesAList()
    {
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, Keep).KeepComments.ShouldBeTrue();
        QuerySettings.Resolve(SettingsLevel.None, Sort, Keep).Parameters.ShouldBe(GeneratorParameters.SortInput);
        QuerySettings.Resolve(Keep, Sort, Sort).Parameters.ShouldBe(GeneratorParameters.KeepComments);
        QuerySettings.Resolve(Empty, Keep, Keep).Parameters.ShouldBe(GeneratorParameters.None);
    }

    [Fact]
    public void Resolve_NoTokenValidation_TurnsValidationOff() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }, Keep, Keep)
            .ValidateTokens.ShouldBeFalse();

    [Fact]
    public void Resolve_NoArrayPool_TurnsThePoolOff() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameters.NoArrayPool }, Keep, Keep)
            .UseArrayPool.ShouldBeFalse();

    [Fact]
    public void Over_EachMember_ComesFromThisLevelWhenItHasOne()
    {
        Keep.Over(Sort).ShouldBe(Keep);
        SettingsLevel.None.Over(Sort).ShouldBeSameAs(Sort);
        Keep.Over(SettingsLevel.None).ShouldBeSameAs(Keep);
    }

    [Fact]
    public void None_ComparedWithAnEmptyLevel_IsEqual() => new SettingsLevel().ShouldBe(SettingsLevel.None);

    [Fact]
    public void Resolve_Output_IsCodeGenUnlessALevelSaysOtherwise()
    {
        var sql = new SettingsLevel { Output = OutputKind.Sql };
        var models = new SettingsLevel { Output = OutputKind.Models };

        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None)
            .Output.ShouldBe(OutputKind.CodeGen);
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, sql).Output.ShouldBe(OutputKind.Sql);
        QuerySettings.Resolve(SettingsLevel.None, models, sql).Output.ShouldBe(OutputKind.Models);
        QuerySettings.Resolve(models, sql, sql).Output.ShouldBe(OutputKind.Models);
    }

    [Fact]
    public void Over_LevelsThatSetDifferentMembers_TakesMembersOneByOne()
    {
        var query = new SettingsLevel { Output = OutputKind.Sql };
        var preamble = new SettingsLevel { Output = OutputKind.Models, Database = "billing" };

        query.Over(preamble).ShouldBe(new SettingsLevel { Output = OutputKind.Sql, Database = "billing" });
    }

    [Fact]
    public void Resolve_ModelSettings_HaveTheirDefaults()
    {
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None);

        settings.InputModelSuffix.ShouldBe("Params");
        settings.OutputModelSuffix.ShouldBe("Dto");
        settings.ModelNamespace.ShouldBeNull();
        settings.InputModelType.ShouldBe(ModelKind.SealedRecord);
        settings.OutputModelType.ShouldBe(ModelKind.SealedRecord);
        settings.CollectionType.ShouldBe(CollectionKind.Array);
    }

    [Fact]
    public void Resolve_EachModelSetting_ComesFromTheMostSpecificLevelThatHasIt()
    {
        var file = new SettingsLevel
        {
            InputModelSuffix = "In",
            OutputModelSuffix = "Row",
            ModelNamespace = "M",
            InputModelType = ModelKind.Class,
            OutputModelType = ModelKind.Class,
            CollectionType = CollectionKind.List,
        };
        var attribute = new SettingsLevel { ModelNamespace = "A", InputModelType = ModelKind.Record };
        var markers = new SettingsLevel { CollectionType = CollectionKind.IReadOnlyList };

        var settings = QuerySettings.Resolve(markers, attribute, file);

        settings.InputModelSuffix.ShouldBe("In");
        settings.OutputModelSuffix.ShouldBe("Row");
        settings.ModelNamespace.ShouldBe("A");
        settings.InputModelType.ShouldBe(ModelKind.Record);
        settings.OutputModelType.ShouldBe(ModelKind.Class);
        settings.CollectionType.ShouldBe(CollectionKind.IReadOnlyList);
    }

    // The type override is one exception, the column types the other: each level replaces only the keys it names,
    // and a default stays, so that the lookup of a type finds it in its place.
    [Fact]
    public void ResolveTypeOverrides_Levels_MergePerKeyAcrossTheLevels()
    {
        var file = Overrides(
            new TypeOverride("int4", "int", SettingsFile),
            new TypeOverride("text", null, SettingsFile),
            new TypeOverride("timestamptz", "global::System.DateTimeOffset", SettingsFile)
        );
        var markers = Overrides(new TypeOverride("timestamptz", "global::System.DateTime", SettingPlace.Query));

        QuerySettings
            .ResolveTypeOverrides(markers, file, DatabaseEngine.PostgreSql)
            .ShouldBe([
                new TypeOverride("int4", "int", SettingsFile),
                new TypeOverride("text", null, SettingsFile),
                new TypeOverride("timestamptz", "global::System.DateTime", SettingPlace.Query),
            ]);
    }

    [Fact]
    public void ResolveTypeOverrides_NoLevelSetsOne_IsEmpty() =>
        QuerySettings
            .ResolveTypeOverrides(SettingsLevel.None, SettingsLevel.None, DatabaseEngine.PostgreSql)
            .ShouldBeEmpty();

    [Theory]
    [InlineData("a", "b", "a")]
    [InlineData(null, "b", "b")]
    [InlineData(null, null, "postgres")]
    public void ResolveDatabase_EachLevel_WinsOverTheLevelsBelowIt(string? markers, string? file, string expected) =>
        QuerySettings
            .ResolveDatabase(
                new SettingsLevel { Database = markers },
                new SettingsLevel { Database = file },
                "postgres"
            )
            .ShouldBe(expected);

    [Theory]
    [InlineData("Statements", "Queries", "Statements")]
    [InlineData("Statements", null, "Statements")]
    [InlineData(null, "Queries", "Queries")]
    [InlineData(null, null, "Sql")]
    public void ResolveNestedClass_TheAttribute_WinsOverTheChain(string? attribute, string? chain, string expected) =>
        QuerySettings
            .ResolveNestedClass(
                new SettingsLevel { NestedClass = attribute },
                new SettingsLevel { NestedClass = chain }
            )
            .ShouldBe(expected);

    [Fact]
    public void Over_NestedClass_ComesFromThisLevelWhenItHasOne()
    {
        new SettingsLevel { NestedClass = "A" }
            .Over(new SettingsLevel { NestedClass = "B" })
            .NestedClass.ShouldBe("A");
        Keep.Over(new SettingsLevel { NestedClass = "B" }).NestedClass.ShouldBe("B");
    }

    [Fact]
    public void Over_TypeOverrides_MergePerKey()
    {
        var query = Overrides(new TypeOverride("int4", null, SettingPlace.Query));
        var preamble = Overrides(
            new TypeOverride("int4", "long", SettingPlace.FileHeader),
            new TypeOverride("int8", "decimal", SettingPlace.FileHeader)
        );

        query
            .Over(preamble)
            .TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int4", null, SettingPlace.Query),
                new TypeOverride("int8", "decimal", SettingPlace.FileHeader),
            ]);
    }

    // Each engine merges its own entries, by the same rule, and never the other's.
    [Fact]
    public void ResolveTypeOverrides_SqlServer_MergesSqlServersEntriesAcrossTheLevels()
    {
        var file = new SettingsLevel
        {
            TypeOverrides = TestSidecars.Of(new TypeOverride("int4", "long", SettingsFile)),
            SqlServerTypeOverrides = TestSidecars.Of(
                new TypeOverride("int", "short", SettingsFile),
                new TypeOverride("nvarchar", "string", SettingsFile)
            ),
        };
        var markers = new SettingsLevel
        {
            SqlServerTypeOverrides = TestSidecars.Of(new TypeOverride("nvarchar", null, SettingPlace.Query)),
        };

        QuerySettings
            .ResolveTypeOverrides(markers, file, DatabaseEngine.SqlServer)
            .ShouldBe([
                new TypeOverride("int", "short", SettingsFile),
                new TypeOverride("nvarchar", null, SettingPlace.Query),
            ]);
        QuerySettings
            .ResolveTypeOverrides(markers, file, DatabaseEngine.PostgreSql)
            .ShouldBe([new TypeOverride("int4", "long", SettingsFile)]);
    }

    [Fact]
    public void Over_SqlServerTypeOverrides_MergePerKey()
    {
        var query = new SettingsLevel
        {
            SqlServerTypeOverrides = TestSidecars.Of(new TypeOverride("int", null, SettingPlace.Query)),
        };
        var preamble = new SettingsLevel
        {
            SqlServerTypeOverrides = TestSidecars.Of(
                new TypeOverride("bigint", "decimal", SettingPlace.FileHeader),
                new TypeOverride("int", "long", SettingPlace.FileHeader)
            ),
        };

        var merged = query.Over(preamble);

        merged
            .SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("bigint", "decimal", SettingPlace.FileHeader),
                new TypeOverride("int", null, SettingPlace.Query),
            ]);
        merged.TypeOverrides.ShouldBeNull();
    }

    // The column types merge per key as the type override does, one list for both engines.
    [Fact]
    public void ResolveColumnTypes_Levels_MergePerKeyAcrossTheLevels()
    {
        var file = Columns(
            new ColumnType("posts.id", null, SettingsFile),
            new ColumnType("users.id", "UserId", SettingsFile),
            new ColumnType("auth.users.id", "AuthUserId", SettingsFile)
        );
        var markers = Columns(new ColumnType("users.id", "global::App.UserId", SettingPlace.FileHeader));

        QuerySettings
            .ResolveColumnTypes(markers, file)
            .ShouldBe([
                new ColumnType("posts.id", null, SettingsFile),
                new ColumnType("users.id", "global::App.UserId", SettingPlace.FileHeader),
                new ColumnType("auth.users.id", "AuthUserId", SettingsFile),
            ]);
    }

    [Fact]
    public void ResolveColumnTypes_NoLevelSetsOne_IsEmpty() =>
        QuerySettings.ResolveColumnTypes(SettingsLevel.None, SettingsLevel.None).ShouldBeEmpty();

    [Fact]
    public void Over_ColumnTypes_MergePerKey()
    {
        var query = Columns(new ColumnType("users.id", null, SettingPlace.FileHeader));
        var preamble = Columns(
            new ColumnType("posts.id", "PostId", SettingPlace.FileHeader),
            new ColumnType("users.id", "UserId", SettingPlace.FileHeader)
        );

        query
            .Over(preamble)
            .ColumnTypes.ShouldNotBeNull()
            .ShouldBe([
                new ColumnType("posts.id", "PostId", SettingPlace.FileHeader),
                new ColumnType("users.id", null, SettingPlace.FileHeader),
            ]);
    }

    // The levels merge per library: the nearer one's entry for a library it names, whatever its state, and the
    // farther one's for any it does not.  An entry of default stays, so that it hides the farther level's.
    [Theory]
    [InlineData("On", "Off", "On", "Query")]
    [InlineData("Off", "On", "Off", "Query")]
    [InlineData("Default", "On", "Default", "Query")]
    [InlineData("Default", "Off", "Default", "Query")]
    [InlineData("On", "Default", "On", "Query")]
    public void Over_LibraryTypes_TakeTheNearerLevelsEntryOfALibrary(
        string near,
        string far,
        string state,
        string placeName
    )
    {
        var nearer = Libraries(Enum.Parse<LibraryState>(near), SettingPlace.Query);
        var farther = Libraries(Enum.Parse<LibraryState>(far), SettingPlace.FileHeader);

        var entry = LibrarySettings.Find(nearer.Over(farther).LibraryTypes.ShouldNotBeNull(), SqlLibrary.NodaTime);

        entry.ShouldNotBeNull().State.ShouldBe(Enum.Parse<LibraryState>(state));
        entry.Place.Kind.ShouldBe(Enum.Parse<SettingPlaceKind>(placeName));
    }

    [Fact]
    public void Over_LibraryTypesOfOnlyOneLevel_AreThatLevelsItself()
    {
        var entries = Libraries(LibraryState.On, SettingPlace.FileHeader);

        SettingsLevel.None.Over(entries).LibraryTypes.ShouldBe(entries.LibraryTypes);
        entries.Over(SettingsLevel.None).LibraryTypes.ShouldBe(entries.LibraryTypes);
    }

    // The markers' entry for a library wins over the file's level's, a default included, which hides the file's.
    [Fact]
    public void ResolveLibraries_MarkersOverFile()
    {
        var markers = Libraries(LibraryState.Default, SettingPlace.Query);
        var file = Libraries(LibraryState.On, SettingsFile);

        var entry = QuerySettings.ResolveLibraries(markers, file).ShouldHaveSingleItem();

        entry.State.ShouldBe(LibraryState.Default);
        entry.Place.ShouldBe(SettingPlace.Query);
        QuerySettings.ResolveLibraries(SettingsLevel.None, file).ShouldBe(file.LibraryTypes!.Value);
        QuerySettings.ResolveLibraries(SettingsLevel.None, SettingsLevel.None).ShouldBeEmpty();
    }

    private static SettingsLevel Libraries(LibraryState state, SettingPlace place) =>
        new()
        {
            LibraryTypes = new EquatableArray<LibrarySetting>([new LibrarySetting(SqlLibrary.NodaTime, state, place)]),
        };

    private static SettingsLevel Overrides(params TypeOverride[] entries) =>
        new() { TypeOverrides = TestSidecars.Of(entries) };

    private static SettingsLevel Columns(params ColumnType[] entries) =>
        new() { ColumnTypes = TestSidecars.Of(entries) };
}
