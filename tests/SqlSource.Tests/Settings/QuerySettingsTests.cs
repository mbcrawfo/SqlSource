using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting but the generator parameters, the type override, the column types and the library types resolves by one
// rule: the most specific level that has a value gives it.
public sealed class QuerySettingsTests
{
    private static readonly SettingsLevel Keep = Turn(GeneratorParameters.KeepComments, GeneratorParameterState.On);

    private static readonly SettingsLevel Sort = Turn(GeneratorParameters.SortInput, GeneratorParameterState.On);

    // The place of a settings file, which a file's level holds.
    private static readonly SettingPlace SettingsFile = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    [Fact]
    public void Resolve_NoLevelSaysAnything_GivesTheDefaults()
    {
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None);

        settings.Parameters.ShouldBe(
            GeneratorParameters.TokenValidation
                | GeneratorParameters.TableModels
                | GeneratorParameters.ArrayPool
                | GeneratorParameters.ListPadding
        );
        settings.KeepComments.ShouldBeFalse();
        settings.ValidateTokens.ShouldBeTrue();
        settings.UseArrayPool.ShouldBeTrue();
        settings.PadsLists.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Parameters_MergePerOption()
    {
        QuerySettings.Resolve(SettingsLevel.None, Keep).KeepComments.ShouldBeTrue();
        QuerySettings
            .Resolve(Sort, Keep)
            .Parameters.ShouldBe(
                GeneratorParameterList.Defaults | GeneratorParameters.KeepComments | GeneratorParameters.SortInput
            );
    }

    [Fact]
    public void Resolve_MarkersTurnAnOptionOff_OverTheFile()
    {
        var off = Turn(GeneratorParameters.KeepComments, GeneratorParameterState.Off);
        var file = new SettingsLevel { Parameters = Keep.Parameters.Over(Sort.Parameters) };

        var settings = QuerySettings.Resolve(off, file);

        settings.KeepComments.ShouldBeFalse();
        settings.Parameters.ShouldBe(GeneratorParameterList.Defaults | GeneratorParameters.SortInput);
    }

    [Fact]
    public void Resolve_MarkersThatReset_HideTheFile() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameterChoices.Reset }, Keep)
            .Parameters.ShouldBe(GeneratorParameterList.Defaults);

    [Fact]
    public void Resolve_TokenValidationOff_TurnsValidationOff() =>
        QuerySettings
            .Resolve(Turn(GeneratorParameters.TokenValidation, GeneratorParameterState.Off), Keep)
            .ValidateTokens.ShouldBeFalse();

    [Fact]
    public void Resolve_ArrayPoolOff_TurnsThePoolOff() =>
        QuerySettings
            .Resolve(Turn(GeneratorParameters.ArrayPool, GeneratorParameterState.Off), Keep)
            .UseArrayPool.ShouldBeFalse();

    [Fact]
    public void Resolve_WithoutListPadding_PadsLists() =>
        QuerySettings.Resolve(new SettingsLevel(), Keep).PadsLists.ShouldBeTrue();

    [Fact]
    public void Resolve_ListPaddingOff_TurnsPaddingOff() =>
        QuerySettings
            .Resolve(Turn(GeneratorParameters.ListPadding, GeneratorParameterState.Off), Keep)
            .PadsLists.ShouldBeFalse();

    [Fact]
    public void Over_EachMember_ComesFromThisLevelWhenItHasOne()
    {
        var output = new SettingsLevel { Output = OutputKind.Sql };

        output.Over(Sort).ShouldBe(new SettingsLevel { Output = OutputKind.Sql, Parameters = Sort.Parameters });
        SettingsLevel.None.Over(Sort).ShouldBeSameAs(Sort);
        Keep.Over(SettingsLevel.None).ShouldBeSameAs(Keep);
    }

    [Fact]
    public void Over_Parameters_MergePerOption() =>
        Keep.Over(Sort).Parameters.ShouldBe(Keep.Parameters.Over(Sort.Parameters));

    [Fact]
    public void KeepsComments_IsTheLevelsResolvedKeepComments()
    {
        Keep.KeepsComments.ShouldBeTrue();
        Sort.KeepsComments.ShouldBeFalse();
        SettingsLevel.None.KeepsComments.ShouldBeFalse();
    }

    [Fact]
    public void None_ComparedWithAnEmptyLevel_IsEqual()
    {
        new SettingsLevel().ShouldBe(SettingsLevel.None);
        new SettingsLevel { Parameters = default }.ShouldBe(SettingsLevel.None);
    }

    [Fact]
    public void Resolve_Output_IsCodeGenUnlessALevelSaysOtherwise()
    {
        var sql = new SettingsLevel { Output = OutputKind.Sql };
        var models = new SettingsLevel { Output = OutputKind.Models };

        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None).Output.ShouldBe(OutputKind.CodeGen);
        QuerySettings.Resolve(SettingsLevel.None, sql).Output.ShouldBe(OutputKind.Sql);
        QuerySettings.Resolve(models, sql).Output.ShouldBe(OutputKind.Models);
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
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None);

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
        var markers = new SettingsLevel
        {
            ModelNamespace = "A",
            InputModelType = ModelKind.Record,
            CollectionType = CollectionKind.IReadOnlyList,
        };

        var settings = QuerySettings.Resolve(markers, file);

        settings.InputModelSuffix.ShouldBe("In");
        settings.OutputModelSuffix.ShouldBe("Row");
        settings.ModelNamespace.ShouldBe("A");
        settings.InputModelType.ShouldBe(ModelKind.Record);
        settings.OutputModelType.ShouldBe(ModelKind.Class);
        settings.CollectionType.ShouldBe(CollectionKind.IReadOnlyList);
    }

    [Fact]
    public void Resolve_PlacementSettings_HaveTheirDefaults()
    {
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None);

        settings.WrittenMemberLocation.ShouldBeNull();
        settings.WrittenVisibility.ShouldBeNull();
        settings.NestedClass.ShouldBe(QuerySettings.DefaultNestedClass);
        settings.NestedClass.ShouldBe("Sql");
    }

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the names.
    [InlineData("Sql", "NestedClass")]
    [InlineData("Models", "NestedClass")]
    [InlineData("CodeGen", "ExtensionClass")]
    public void MemberLocation_NoLevelWritesOne_IsTheDefaultOfTheOutput(string output, string expected)
    {
        var level = new SettingsLevel { Output = Enum.Parse<OutputKind>(output) };

        var settings = QuerySettings.Resolve(level, SettingsLevel.None);

        settings.WrittenMemberLocation.ShouldBeNull();
        settings.MemberLocation.ShouldBe(Enum.Parse<MemberLocation>(expected));
    }

    [Theory]
    [InlineData("Sql")]
    [InlineData("CodeGen")]
    public void MemberLocation_ALevelWritesOne_IsItWhateverTheOutput(string output)
    {
        var file = new SettingsLevel
        {
            Output = Enum.Parse<OutputKind>(output),
            MemberLocation = new Placed<MemberLocation>(MemberLocation.Class, SettingsFile),
        };

        QuerySettings.Resolve(SettingsLevel.None, file).MemberLocation.ShouldBe(MemberLocation.Class);
    }

    // The block's markers are the query's over the header's, and that level is over the file's.
    [Fact]
    public void Resolve_PlacementSettings_TheQueryWinsOverTheHeaderOverTheFile()
    {
        var file = new SettingsLevel
        {
            MemberLocation = new Placed<MemberLocation>(MemberLocation.ExtensionClass, SettingsFile),
            Visibility = new Placed<Visibility>(Visibility.Private, SettingsFile),
            NestedClass = "FromFile",
        };
        var header = new SettingsLevel
        {
            MemberLocation = new Placed<MemberLocation>(MemberLocation.NestedClass, SettingPlace.FileHeader),
            Visibility = new Placed<Visibility>(Visibility.Internal, SettingPlace.FileHeader),
            NestedClass = "FromHeader",
        };
        var query = new SettingsLevel
        {
            MemberLocation = new Placed<MemberLocation>(MemberLocation.Class, SettingPlace.Query),
            NestedClass = "FromQuery",
        };

        var fromQuery = QuerySettings.Resolve(query.Over(header), file);
        var fromHeader = QuerySettings.Resolve(header, file);
        var fromFile = QuerySettings.Resolve(SettingsLevel.None, file);

        fromQuery.WrittenMemberLocation.ShouldBe(new Placed<MemberLocation>(MemberLocation.Class, SettingPlace.Query));
        fromQuery.WrittenVisibility.ShouldBe(new Placed<Visibility>(Visibility.Internal, SettingPlace.FileHeader));
        fromQuery.NestedClass.ShouldBe("FromQuery");
        fromHeader.WrittenMemberLocation.ShouldBe(
            new Placed<MemberLocation>(MemberLocation.NestedClass, SettingPlace.FileHeader)
        );
        fromHeader.NestedClass.ShouldBe("FromHeader");
        fromFile.WrittenMemberLocation.ShouldBe(
            new Placed<MemberLocation>(MemberLocation.ExtensionClass, SettingsFile)
        );
        fromFile.WrittenVisibility.ShouldBe(new Placed<Visibility>(Visibility.Private, SettingsFile));
        fromFile.NestedClass.ShouldBe("FromFile");
    }

    // The type override merges per key, as the column types and the library types do, and the generator parameters
    // per parameter: each level replaces only the keys it names, and a default stays, so that the lookup of a type
    // finds it in its place.
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
            .TypeOverrides[DatabaseEngine.PostgreSql]
            .ShouldNotBeNull()
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
            TypeOverrides = PerEngine
                .Only<EquatableArray<TypeOverride>?>(
                    DatabaseEngine.PostgreSql,
                    TestSidecars.Of(new TypeOverride("int4", "long", SettingsFile))
                )
                .With(
                    DatabaseEngine.SqlServer,
                    TestSidecars.Of(
                        new TypeOverride("int", "short", SettingsFile),
                        new TypeOverride("nvarchar", "string", SettingsFile)
                    )
                ),
        };
        var markers = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only<EquatableArray<TypeOverride>?>(
                DatabaseEngine.SqlServer,
                TestSidecars.Of(new TypeOverride("nvarchar", null, SettingPlace.Query))
            ),
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
            TypeOverrides = PerEngine.Only<EquatableArray<TypeOverride>?>(
                DatabaseEngine.SqlServer,
                TestSidecars.Of(new TypeOverride("int", null, SettingPlace.Query))
            ),
        };
        var preamble = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only<EquatableArray<TypeOverride>?>(
                DatabaseEngine.SqlServer,
                TestSidecars.Of(
                    new TypeOverride("bigint", "decimal", SettingPlace.FileHeader),
                    new TypeOverride("int", "long", SettingPlace.FileHeader)
                )
            ),
        };

        var merged = query.Over(preamble);

        merged
            .TypeOverrides[DatabaseEngine.SqlServer]
            .ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("bigint", "decimal", SettingPlace.FileHeader),
                new TypeOverride("int", null, SettingPlace.Query),
            ]);
        merged.TypeOverrides[DatabaseEngine.PostgreSql].ShouldBeNull();
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
        new()
        {
            TypeOverrides = PerEngine.Only<EquatableArray<TypeOverride>?>(
                DatabaseEngine.PostgreSql,
                TestSidecars.Of(entries)
            ),
        };

    private static SettingsLevel Columns(params ColumnType[] entries) =>
        new() { ColumnTypes = TestSidecars.Of(entries) };

    // A level that gives the parameter that state, and says nothing else.
    private static SettingsLevel Turn(GeneratorParameters parameter, GeneratorParameterState state) =>
        new() { Parameters = GeneratorParameterChoices.None.With(parameter, state) };
}
