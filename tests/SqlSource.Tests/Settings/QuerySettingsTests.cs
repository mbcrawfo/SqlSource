using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting resolves by one rule: the most specific level that has a value gives it.
public class QuerySettingsTests
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
}
