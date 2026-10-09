using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

// The schema is strict and the writer is written by hand.  These tests are what keeps the two in step.
public class SidecarSchemaTests
{
    private static readonly string SchemaText = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "schemas", "sidecar-v1.schema.json")
    );

    // One instance: a schema is registered under its $id when it is first used.
    private static readonly JsonSchema Schema = JsonSchema.FromText(SchemaText);

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Schema_ExampleOfTheFormatDesign_IsValid(string name) =>
        Errors(SidecarExamples.Read(name)).ShouldBeEmpty();

    [Fact]
    public void Schema_EverySidecarTheToolCanBuild_IsValidAsTheWriterWritesIt()
    {
        foreach (var sidecar in SidecarRoundTripTests.Samples(lenient: false))
        {
            var text = SidecarWriter.Write(sidecar);

            Errors(text).ShouldBeEmpty(text);
        }
    }

    [Fact]
    public void Schema_Id_IsTheUrlTheWriterWrites()
    {
        using var schema = JsonDocument.Parse(SchemaText);

        schema.RootElement.GetProperty("$id").GetString().ShouldBe(SidecarFormat.SchemaId);
    }

    // The schema says what this tool writes, and the reader rules say what any version tolerates.
    [Fact]
    public void Schema_FileWithAKeyTheToolDoesNotWrite_IsNotValidThoughTheReaderReadsIt()
    {
        var text = SidecarExamples
            .Read(SidecarExamples.Users)
            .Replace("\"formatVersion\": 1,", "\"formatVersion\": 1,\n  \"future\": true,", StringComparison.Ordinal);

        Errors(text).ShouldNotBeEmpty();
        SidecarReader.Read(text).Sidecar.ShouldNotBeNull().Queries.Count.ShouldBe(4);
    }

    // The validator must hold the schema's conditions, or the tests above prove nothing.
    [Theory]
    [InlineData("\"kind\": \"base\"", "\"kind\": \"pseudo\"")]
    [InlineData("\"kind\": \"base\"", "\"kind\": \"array\"")]
    [InlineData("\"resultKind\": \"none\"", "\"resultKind\": \"none\",\n      \"columns\": []")]
    [InlineData("\"plan\": \"not-needed\",", "")]
    [InlineData("\"typeSource\": \"declared\"", "\"typeSource\": \"guessed\"")]
    [InlineData("\"engine\": \"postgres\"", "\"engine\": \"sqlite\"")]
    [InlineData("\"toolVersion\": \"0.4.0\"", "\"toolVersion\": \"0.4.0-dev\"")]
    [InlineData("\"formatVersion\": 1", "\"formatVersion\": 2")]
    public void Schema_FileThatBreaksACondition_IsNotValid(string old, string replacement)
    {
        var text = SidecarExamples.Read(SidecarExamples.Users);
        text.ShouldContain(old);

        Errors(text.Replace(old, replacement, StringComparison.Ordinal)).ShouldNotBeEmpty();
    }

    // The values the tool writes are the values the schema allows, each list in both directions.
    [Theory]
    [InlineData(typeof(SidecarValues.Engine), "query", "engine")]
    [InlineData(typeof(SidecarValues.ResultKind), "query", "resultKind")]
    [InlineData(typeof(SidecarValues.Plan), "query", "plan")]
    [InlineData(typeof(SidecarValues.TableMatch), "query", "tableMatch")]
    [InlineData(typeof(SidecarValues.TypeSource), "parameter", "typeSource")]
    [InlineData(typeof(SidecarValues.NullableSource), "column", "nullableSource")]
    [InlineData(typeof(SidecarValues.Kind), "postgresType", "kind")]
    public void Schema_ListOfValues_IsTheConstantsOfSidecarValues(Type constants, string definition, string property)
    {
        using var schema = JsonDocument.Parse(SchemaText);
        var allowed = schema
            .RootElement.GetProperty("$defs")
            .GetProperty(definition)
            .GetProperty("properties")
            .GetProperty(property)
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString());

        var written = constants
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string?)field.GetRawConstantValue());

        written.ShouldBe(allowed, ignoreOrder: true);
    }

    private static string[] Errors(string json)
    {
        using var document = JsonDocument.Parse(json);
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
