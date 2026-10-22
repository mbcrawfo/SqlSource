using System.Collections.Immutable;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting but the type override resolves by one rule: the most specific level that has a value gives it.
public sealed class QuerySettingsTests
{
    private static readonly SettingsLevel Keep = new() { Parameters = GeneratorParameters.KeepComments };

    private static readonly SettingsLevel Sort = new() { Parameters = GeneratorParameters.SortInput };

    private static readonly SettingsLevel Empty = new() { Parameters = GeneratorParameters.None };

    [Fact]
    public void Resolve_NoLevelSaysAnything_GivesTheDefaults()
    {
        var settings = QuerySettings.Resolve(
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None
        );

        settings.Parameters.ShouldBe(GeneratorParameters.None);
        settings.KeepComments.ShouldBeFalse();
        settings.ValidateTokens.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Parameters_ComeWholeFromTheMostSpecificLevelThatGivesAList()
    {
        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, Keep)
            .KeepComments.ShouldBeTrue();
        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, Sort, Keep)
            .Parameters.ShouldBe(GeneratorParameters.SortInput);
        QuerySettings
            .Resolve(SettingsLevel.None, Keep, Sort, Sort)
            .Parameters.ShouldBe(GeneratorParameters.KeepComments);
        QuerySettings.Resolve(Empty, Keep, Keep, Keep).Parameters.ShouldBe(GeneratorParameters.None);
    }

    [Fact]
    public void Resolve_NoTokenValidation_TurnsValidationOff() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }, Keep, Keep, Keep)
            .ValidateTokens.ShouldBeFalse();

    [Fact]
    public void Over_EachMember_ComesFromThisLevelWhenItHasOne()
    {
        Keep.Over(Sort).ShouldBe(Keep);
        SettingsLevel.None.Over(Sort).ShouldBeSameAs(Sort);
        Keep.Over(SettingsLevel.None).ShouldBeSameAs(Keep);
    }

    [Fact]
    public void None_IsEqualToALevelThatSetsNothing() => new SettingsLevel().ShouldBe(SettingsLevel.None);

    [Fact]
    public void Resolve_Output_IsCodeGenUnlessALevelSaysOtherwise()
    {
        var sql = new SettingsLevel { Output = OutputKind.Sql };
        var models = new SettingsLevel { Output = OutputKind.Models };

        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, SettingsLevel.None)
            .Output.ShouldBe(OutputKind.CodeGen);
        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, sql)
            .Output.ShouldBe(OutputKind.Sql);
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, models, sql).Output.ShouldBe(OutputKind.Models);
        QuerySettings.Resolve(SettingsLevel.None, sql, models, models).Output.ShouldBe(OutputKind.Sql);
        QuerySettings.Resolve(models, sql, sql, sql).Output.ShouldBe(OutputKind.Models);
    }

    [Fact]
    public void Over_MembersAreTakenOneByOne()
    {
        var query = new SettingsLevel { Output = OutputKind.Sql };
        var preamble = new SettingsLevel { Output = OutputKind.Models, Database = "billing" };

        query.Over(preamble).ShouldBe(new SettingsLevel { Output = OutputKind.Sql, Database = "billing" });
    }

    [Fact]
    public void Resolve_ModelSettings_HaveTheirDefaults()
    {
        var settings = QuerySettings.Resolve(
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None
        );

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
        var property = new SettingsLevel
        {
            InputModelSuffix = "In",
            OutputModelSuffix = "Out",
            ModelNamespace = "P",
            InputModelType = ModelKind.Class,
            OutputModelType = ModelKind.Class,
            CollectionType = CollectionKind.List,
        };
        var metadata = new SettingsLevel { OutputModelSuffix = "Row", ModelNamespace = "M" };
        var attribute = new SettingsLevel { ModelNamespace = "A", InputModelType = ModelKind.Record };
        var markers = new SettingsLevel { CollectionType = CollectionKind.IReadOnlyList };

        var settings = QuerySettings.Resolve(markers, attribute, metadata, property);

        settings.InputModelSuffix.ShouldBe("In");
        settings.OutputModelSuffix.ShouldBe("Row");
        settings.ModelNamespace.ShouldBe("A");
        settings.InputModelType.ShouldBe(ModelKind.Record);
        settings.OutputModelType.ShouldBe(ModelKind.Class);
        settings.CollectionType.ShouldBe(CollectionKind.IReadOnlyList);
    }

    // The type override is the exception: each level replaces only the keys it names, and a default stays, so that
    // the lookup of a type finds it in its place.
    [Fact]
    public void ResolveTypeOverrides_Levels_MergePerKeyAcrossTheLevels()
    {
        var property = Overrides(
            new TypeOverride("int4", "long", SettingPlace.Property),
            new TypeOverride("text", "string", SettingPlace.Property),
            new TypeOverride("timestamptz", "global::System.DateTimeOffset", SettingPlace.Property)
        );
        var metadata = Overrides(
            new TypeOverride("int4", "int", SettingPlace.Metadata),
            new TypeOverride("text", null, SettingPlace.Metadata)
        );
        var markers = Overrides(new TypeOverride("timestamptz", "global::System.DateTime", SettingPlace.Query));

        QuerySettings
            .ResolveTypeOverrides(markers, metadata, property, DatabaseEngine.PostgreSql)
            .ShouldBe([
                new TypeOverride("int4", "int", SettingPlace.Metadata),
                new TypeOverride("text", null, SettingPlace.Metadata),
                new TypeOverride("timestamptz", "global::System.DateTime", SettingPlace.Query),
            ]);
    }

    [Fact]
    public void ResolveTypeOverrides_NoLevelSetsOne_IsEmpty() =>
        QuerySettings
            .ResolveTypeOverrides(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, DatabaseEngine.PostgreSql)
            .ShouldBeEmpty();

    [Theory]
    [InlineData("a", "b", "c", "a")]
    [InlineData(null, "b", "c", "b")]
    [InlineData(null, null, "c", "c")]
    [InlineData(null, null, null, "postgres")]
    public void ResolveDatabase_EachLevel_WinsOverTheLevelsBelowIt(
        string? markers,
        string? metadata,
        string? property,
        string expected
    ) =>
        QuerySettings
            .ResolveDatabase(
                new SettingsLevel { Database = markers },
                new SettingsLevel { Database = metadata },
                new SettingsLevel { Database = property },
                "postgres"
            )
            .ShouldBe(expected);

    [Theory]
    [InlineData("Statements", "Queries", "Statements")]
    [InlineData(null, "Queries", "Queries")]
    [InlineData(null, null, "Sql")]
    public void ResolveNestedClass_TheAttribute_WinsOverTheProperty(
        string? attribute,
        string? property,
        string expected
    ) =>
        QuerySettings
            .ResolveNestedClass(
                new SettingsLevel { NestedClass = attribute },
                new SettingsLevel { NestedClass = property }
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
        var property = new SettingsLevel
        {
            TypeOverrides = Array(new TypeOverride("int4", "long", SettingPlace.Property)),
            SqlServerTypeOverrides = Array(
                new TypeOverride("int", "long", SettingPlace.Property),
                new TypeOverride("nvarchar", "string", SettingPlace.Property)
            ),
        };
        var metadata = new SettingsLevel
        {
            SqlServerTypeOverrides = Array(new TypeOverride("int", "short", SettingPlace.Metadata)),
        };
        var markers = new SettingsLevel
        {
            SqlServerTypeOverrides = Array(new TypeOverride("nvarchar", null, SettingPlace.Query)),
        };

        QuerySettings
            .ResolveTypeOverrides(markers, metadata, property, DatabaseEngine.SqlServer)
            .ShouldBe([
                new TypeOverride("int", "short", SettingPlace.Metadata),
                new TypeOverride("nvarchar", null, SettingPlace.Query),
            ]);
        QuerySettings
            .ResolveTypeOverrides(markers, metadata, property, DatabaseEngine.PostgreSql)
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
    }

    [Fact]
    public void Over_SqlServerTypeOverrides_MergePerKey()
    {
        var query = new SettingsLevel
        {
            SqlServerTypeOverrides = Array(new TypeOverride("int", null, SettingPlace.Query)),
        };
        var preamble = new SettingsLevel
        {
            SqlServerTypeOverrides = Array(
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

    private static EquatableArray<TypeOverride> Array(params TypeOverride[] entries) =>
        new(ImmutableArray.Create(entries));

    private static SettingsLevel Overrides(params TypeOverride[] entries) =>
        new() { TypeOverrides = new EquatableArray<TypeOverride>(ImmutableArray.Create(entries)) };
}
