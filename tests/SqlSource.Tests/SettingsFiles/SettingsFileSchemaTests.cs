using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.SettingsFiles;
using Xunit;

namespace SqlSource.Tests.SettingsFiles;

// The schema is written by hand for editors, and accepts what the reader accepts and no more.  These tests are what
// keeps its keys, types and choices in step with the reader's.
public sealed class SettingsFileSchemaTests
{
    private static readonly SettingPlace Place = SettingPlace.Metadata;

    private static readonly string SchemaText = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "schemas", "settings-v1.schema.json")
    );

    // One instance: a schema is registered under its $id when it is first used.
    private static readonly JsonSchema Schema = JsonSchema.FromText(SchemaText);

    private static readonly JsonElement Properties = ReadProperties();

    // A settings file may hold comments and trailing commas, as the reader allows.
    private static readonly JsonDocumentOptions FileOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static TheoryData<string> Keys => [.. SettingsFileKeys.All];

    // Each key whose value is the name of a member of an enum, read by SettingValue.TryReadChoice, with the enum.
    public static TheoryData<string, Type> ChoiceKeys =>
        new()
        {
            { SettingsFileKeys.Output, typeof(OutputKind) },
            { SettingsFileKeys.InputModelType, typeof(ModelKind) },
            { SettingsFileKeys.OutputModelType, typeof(ModelKind) },
            { SettingsFileKeys.CollectionType, typeof(CollectionKind) },
        };

    public static TheoryData<string> MapKeys => [SettingsFileKeys.TypeOverrides, SettingsFileKeys.ColumnTypes];

    [Fact]
    public void Schema_Properties_AreTheKeysOfTheReader() =>
        Properties
            .EnumerateObject()
            .Select(property => property.Name)
            .ShouldBe(SettingsFileKeys.All, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(Keys))]
    public void Schema_TypeOfEachKey_IsTheReaders(string key)
    {
        var property = Properties.GetProperty(key);

        switch (SettingsFileKeys.TypeOf(key))
        {
            case SettingsFileValueType.String:
                property.GetProperty("type").GetString().ShouldBe("string");
                break;
            case SettingsFileValueType.Boolean:
                property.GetProperty("type").GetString().ShouldBe("boolean");
                break;
            case SettingsFileValueType.ArrayOfStrings:
                property.GetProperty("type").GetString().ShouldBe("array");
                property.GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
                break;
            case SettingsFileValueType.ObjectOfStrings:
                property.GetProperty("type").GetString().ShouldBe("object");
                property.GetProperty("additionalProperties").GetProperty("type").GetString().ShouldBe("string");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "A type of value that the test does not know.");
        }
    }

    // Each anyOf is a list for completion and a pattern, which Choices and Pattern read by their places.
    [Fact]
    public void Schema_AnyOf_IsOnTheChoicesTheDialectAndTheParametersAlone()
    {
        var withAnyOf = Properties
            .EnumerateObject()
            .SelectMany(property =>
                new[] { (Path: property.Name, Schema: property.Value) }.Concat(
                    property.Value.TryGetProperty("items", out var items)
                        ? [(Path: property.Name + "/items", Schema: items)]
                        : []
                )
            )
            .Where(place => place.Schema.TryGetProperty("anyOf", out _))
            .ToArray();

        withAnyOf
            .Select(place => place.Path)
            .ShouldBe(
                [
                    .. ChoiceKeys.Select(row => row.Data.Item1),
                    SettingsFileKeys.Dialect,
                    SettingsFileKeys.GeneratorParameters + "/items",
                ],
                ignoreOrder: true
            );
        withAnyOf.ShouldAllBe(place => place.Schema.GetProperty("anyOf").GetArrayLength() == 2);
    }

    [Theory]
    [MemberData(nameof(ChoiceKeys))]
    public void Schema_EnumOfAChoice_NamesEveryMemberOnce(string key, Type choice)
    {
        var read = Choices(Properties.GetProperty(key)).Select(name => ReadChoice(choice, name)).ToArray();

        read.ShouldAllBe(member => member != null);
        read.ShouldBeUnique();
        read.ShouldBe(Enum.GetValues(choice).Cast<object>(), ignoreOrder: true);
    }

    [Fact]
    public void Schema_EnumOfGeneratorParameters_NamesEveryParameterAndDefault()
    {
        var names = Choices(Properties.GetProperty(SettingsFileKeys.GeneratorParameters).GetProperty("items"));

        // One name for each parameter: an alias added to the list fails here, and is a choice for the schema to make.
        var parameters = GeneratorParameterList.Names.Select(name => name.Parameter).ToArray();
        parameters.ShouldBeUnique();
        parameters.ShouldBe(
            Enum.GetValues<GeneratorParameters>().Where(parameter => parameter != GeneratorParameters.None),
            ignoreOrder: true
        );
        names.ShouldBe([.. GeneratorParameterList.Names.Select(name => name.Name), GeneratorParameterList.Default]);
    }

    [Fact]
    public void Schema_EnumOfDialect_NamesEveryDialect()
    {
        var names = Choices(Properties.GetProperty(SettingsFileKeys.Dialect));

        var dialects = names
            .Select(name => SqlDialectName.TryParse(name, out var choice) ? choice : (SqlDialectChoice?)null)
            .ToArray();
        dialects.ShouldAllBe(choice => choice != null && choice.Value.Options == SqlDialectOptions.None);
        dialects.Select(choice => choice!.Value.Dialect).ShouldBeUnique();
        dialects.Select(choice => choice!.Value.Dialect).ShouldBe(Enum.GetValues<SqlDialect>(), ignoreOrder: true);
        $"{string.Join(", ", names[..^1])} and {names[^1]}".ShouldBe(SqlDialectName.Accepted);
    }

    [Theory]
    [MemberData(nameof(ChoiceKeys))]
    public void Schema_PatternOfAChoice_IsGeneratedFromItsEnum(string key, Type choice)
    {
        // TryReadChoice reads the names of Enum.GetNames, ignoring hyphens, spaces and tabs anywhere.
        var names = Enum.GetNames(choice)
            .Select(name => string.Concat(name.Select(character => CaseFree(character) + @"[- \t]*")));
        var expected = @"^\s*(?:[- \t]*(?:" + string.Join("|", names) + @"))?\s*$";

        Pattern(Properties.GetProperty(key)).ShouldBe(expected);
    }

    [Fact]
    public void Schema_PatternOfGeneratorParameters_IsGeneratedFromTheParameters()
    {
        var names = GeneratorParameterList
            .Names.Select(name => name.Name)
            .Append(GeneratorParameterList.Default)
            .Select(CaseFree);
        var expected = @"^\s*(?:" + string.Join("|", names) + @")?\s*$";

        Pattern(Properties.GetProperty(SettingsFileKeys.GeneratorParameters).GetProperty("items")).ShouldBe(expected);
    }

    // default stands alone: a list that holds it and a parameter is not valid, whatever else it holds.
    [Fact]
    public void Schema_DefaultBesideAParameter_IsGeneratedFromTheParameters()
    {
        var both = Properties
            .GetProperty(SettingsFileKeys.GeneratorParameters)
            .GetProperty("not")
            .GetProperty("allOf")
            .EnumerateArray()
            .Select(branch => branch.GetProperty("contains"))
            .ToArray();
        var parameters = GeneratorParameterList.Names.Select(name => CaseFree(name.Name));

        both.Length.ShouldBe(2);
        both.ShouldAllBe(contains => contains.GetProperty("type").GetString() == "string");
        both[0]
            .GetProperty("pattern")
            .GetString()
            .ShouldBe(@"^\s*" + CaseFree(GeneratorParameterList.Default) + @"\s*$");
        both[1].GetProperty("pattern").GetString().ShouldBe(@"^\s*(?:" + string.Join("|", parameters) + @")\s*$");
    }

    // A member whose key or type is empty or white space is not valid: the reader trims both.
    [Theory]
    [MemberData(nameof(MapKeys))]
    public void Schema_KeyAndTypeOfAMap_NeedACharacterThatIsNotWhiteSpace(string key)
    {
        var property = Properties.GetProperty(key);

        property.GetProperty("propertyNames").GetProperty("pattern").GetString().ShouldBe(@"\S");
        property.GetProperty("additionalProperties").GetProperty("pattern").GetString().ShouldBe(@"\S");
    }

    [Fact]
    public void Schema_PatternOfDialect_IsGeneratedFromTheNamesAndOptions()
    {
        // One group of options serves every dialect that takes any, so each takes all of them.
        var everyOption = SqlDialectName.Options.Aggregate(
            SqlDialectOptions.None,
            (options, option) => options | option.Option
        );
        SqlDialectName
            .Names.Select(name => SqlDialectName.OptionsOf(name.Dialect))
            .ShouldAllBe(options => options == SqlDialectOptions.None || options == everyOption);

        var withoutOptions = SqlDialectName
            .Names.Where(name => SqlDialectName.OptionsOf(name.Dialect) == SqlDialectOptions.None)
            .Select(name => CaseFree(name.Name));
        var withOptions = SqlDialectName
            .Names.Where(name => SqlDialectName.OptionsOf(name.Dialect) != SqlDialectOptions.None)
            .Select(name => CaseFree(name.Name));
        var options = SqlDialectName.Options.Select(option => CaseFree(option.Name));
        var expected =
            @"^\s*(?:(?:"
            + string.Join("|", withoutOptions)
            + ")|(?:"
            + string.Join("|", withOptions)
            + @")(?:\s*,\s*(?:"
            + string.Join("|", options)
            + @"))*)?\s*$";

        Pattern(Properties.GetProperty(SettingsFileKeys.Dialect)).ShouldBe(expected);
    }

    [Fact]
    public void Schema_Id_IsTheUrlOfTheSchemasFolder()
    {
        using var schema = JsonDocument.Parse(SchemaText);

        schema
            .RootElement.GetProperty("$id")
            .GetString()
            .ShouldBe("https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json");
    }

    // JsonSchema.Net ignores these two keywords; VS Code opens a .json file as strict JSON, and with them it accepts
    // the comments and trailing commas the reader allows.
    [Fact]
    public void Schema_Root_AllowsCommentsAndTrailingCommas()
    {
        using var schema = JsonDocument.Parse(SchemaText);

        schema.RootElement.GetProperty("allowComments").GetBoolean().ShouldBeTrue();
        schema.RootElement.GetProperty("allowTrailingCommas").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Schema_DesignsExample_IsValid() => Errors(SettingsFileReaderTests.DesignsExample).ShouldBeEmpty();

    // The reader trims a value, ignores case and, in a choice, hyphens, spaces and tabs anywhere; a blank value sets
    // nothing and is no error.  The schema's patterns must accept each such spelling.
    [Theory]
    [InlineData(SettingsFileKeys.Output, "\" Code-Gen \"")]
    [InlineData(SettingsFileKeys.InputModelType, "\"SEALED record\"")]
    [InlineData(SettingsFileKeys.CollectionType, "\"i-read-only-list\"")]
    [InlineData(SettingsFileKeys.Output, "\"\"")]
    [InlineData(SettingsFileKeys.Dialect, "\" MySQL , ANSI_QUOTES \"")]
    [InlineData(SettingsFileKeys.Dialect, "\"tsql\"")]
    [InlineData(SettingsFileKeys.Dialect, "\"  \"")]
    [InlineData(SettingsFileKeys.GeneratorParameters, "[\"  Keep-Comments \", \"\"]")]
    [InlineData(SettingsFileKeys.Output, "\"\\tcode-gen\\n\"")]
    [InlineData(SettingsFileKeys.Dialect, "\"mysql\\t,\\tansi-quotes\"")]
    [InlineData(SettingsFileKeys.Dialect, "\"mysql , ansi-quotes\"")]
    [InlineData(SettingsFileKeys.GeneratorParameters, "[\"default\", \"\"]")]
    [InlineData(SettingsFileKeys.GeneratorParameters, "[\"default\", \"default\"]")]
    public void Schema_SpellingTheReaderAccepts_IsValid(string key, string value)
    {
        var text = $$"""{"{{key}}": {{value}}}""";

        Errors(text).ShouldBeEmpty();
        SettingsFileReader.Read(text, Place).Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"bogus": 1}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"Dialect": "ansi"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"root": "yes"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"output": "bogus"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"output": "-"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"dialect": "ansi,ansi-quotes"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"dialect": "mysql,"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"generatorParameters": ["nope"]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"generatorParameters": "keep-comments"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"typeOverrides": {"int4": 5}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"columnTypes": []}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"output": null}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"dialect": "mysql,,ansi-quotes"}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"generatorParameters": ["default", "keep-comments"]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"generatorParameters": ["Sort-Input", "", " DEFAULT "]}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"typeOverrides": {"int4": ""}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"typeOverrides": {" ": "int"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"columnTypes": {"users.id": " \t"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"columnTypes": {"": "UserId"}}"""
    )]
    public void Schema_FileTheReaderRejects_IsNotValid(string text)
    {
        Errors(text).ShouldNotBeEmpty();
        SettingsFileReader.Read(text, Place).Errors.ShouldNotBeEmpty();
    }

    private static JsonElement ReadProperties()
    {
        using var schema = JsonDocument.Parse(SchemaText);
        return schema.RootElement.GetProperty("properties").Clone();
    }

    // The spellings of a choice for completion, in the first branch of its anyOf.
    private static string[] Choices(JsonElement property) =>
        [
            .. property
                .GetProperty("anyOf")[0]
                .GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString().ShouldNotBeNull()),
        ];

    // The pattern of a choice, in the second branch of its anyOf.
    private static string? Pattern(JsonElement property) =>
        property.GetProperty("anyOf")[1].GetProperty("pattern").GetString();

    // A letter as a class of both its cases, and any other character as itself, escaped.
    private static string CaseFree(char character) =>
        char.IsLetter(character)
            ? $"[{char.ToLowerInvariant(character)}{char.ToUpperInvariant(character)}]"
            : Regex.Escape(character.ToString());

    private static string CaseFree(string name) => string.Concat(name.Select(CaseFree));

    private static object? ReadChoice(Type choice, string value) =>
        choice switch
        {
            _ when choice == typeof(OutputKind) => ReadChoice<OutputKind>(value),
            _ when choice == typeof(ModelKind) => ReadChoice<ModelKind>(value),
            _ when choice == typeof(CollectionKind) => ReadChoice<CollectionKind>(value),
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "A choice that the test does not know."),
        };

    private static object? ReadChoice<T>(string value)
        where T : struct, Enum => SettingValue.TryReadChoice<T>(value, out var member) ? member : null;

    private static string[] Errors(string json)
    {
        using var document = JsonDocument.Parse(json, FileOptions);
        var result = Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        string[] errors =
        [
            .. (result.Details ?? [])
                .Where(detail => detail.Errors is not null)
                .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation}: {error.Value}")),
        ];
        return errors.Length > 0 ? errors : ["The file is not valid, and the validator gave no detail."];
    }
}
