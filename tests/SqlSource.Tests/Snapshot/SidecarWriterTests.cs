using System.Globalization;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing;
using Xunit;
using static SqlSource.Testing.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

public sealed class SidecarWriterTests
{
    // The sample sidecars of Snapshot/Examples/ are the oracle of the layout.
    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Write_ExampleThatWasRead_IsTheFileByteForByte(string name)
    {
        var text = SidecarExamples.Read(name);

        SidecarWriter.Write(SidecarReader.Read(text).Sidecar.ShouldNotBeNull()).ShouldBe(text);
    }

    [Fact]
    public void Write_EntryWithoutRows_HasTheLayoutOfTheFormat()
    {
        var sidecar = SidecarOf(Entry(resultKind: SidecarResultKind.None));

        SidecarWriter
            .Write(sidecar)
            .ShouldBe(
                string.Join(
                    "\n",
                    "{",
                    "  \"_WARNING\": \"" + SidecarFormat.Warning + "\",",
                    "  \"$schema\": \"" + SidecarFormat.SchemaId + "\",",
                    "  \"formatVersion\": 1,",
                    "  \"toolVersion\": \"1.2.3\",",
                    "  \"queries\": {",
                    "    \"Q\": {",
                    "      \"hash\": \"h\",",
                    "      \"engine\": \"postgres\",",
                    "      \"database\": \"app\",",
                    "      \"serverVersion\": \"16.4\",",
                    "      \"resultKind\": \"none\",",
                    "      \"parameters\": []",
                    "    }",
                    "  }",
                    "}",
                    string.Empty
                )
            );
    }

    [Fact]
    public void Write_AnySidecar_HasLineFeedsATrailingNewlineAndNoByteOrderMark()
    {
        var text = SidecarWriter.Write(SidecarOf(Entry(), Entry("Other")));

        text.ShouldStartWith("{\n");
        text.ShouldEndWith("\n}\n");
        text.ShouldNotContain("\r");
        text.ShouldNotContain("\t");
    }

    [Fact]
    public void Write_NoQueries_IsAnEmptyObjectOnTheLineOfItsKey() =>
        SidecarWriter.Write(SidecarOf()).ShouldEndWith("  \"queries\": {}\n}\n");

    // The format design, section 1.  The character is given by its code, so that a test's name holds none.
    // The comparison is case-sensitive, as Shouldly's is not unless asked: the hex digits are in upper case.
    [Theory]
    [InlineData(0x22, "\\\"")]
    [InlineData(0x5C, "\\\\")]
    [InlineData(0x08, "\\b")]
    [InlineData(0x0C, "\\f")]
    [InlineData(0x0A, "\\n")]
    [InlineData(0x0D, "\\r")]
    [InlineData(0x09, "\\t")]
    [InlineData(0x00, "\\u0000")]
    [InlineData(0x0B, "\\u000B")]
    [InlineData(0x1F, "\\u001F")]
    public void Write_CharacterTheFormatEscapes_IsWrittenAsItsEscape(int code, string escape)
    {
        var value = "a" + (char)code + "b";
        var sidecar = SidecarOf(Entry(value, database: value));

        var text = SidecarWriter.Write(sidecar);

        text.ShouldContain("    \"a" + escape + "b\": {\n", Case.Sensitive);
        text.ShouldContain("\"database\": \"a" + escape + "b\",\n", Case.Sensitive);
    }

    // Every other character is written as it is: a solidus, a space, DEL, a line separator, a letter outside ASCII and
    // a surrogate alone.
    [Theory]
    [InlineData(0x2F)]
    [InlineData(0x20)]
    [InlineData(0x7F)]
    [InlineData(0x2028)]
    [InlineData(0xE9)]
    [InlineData(0xD83D)]
    public void Write_AnyOtherCharacter_IsWrittenAsItIs(int code)
    {
        var value = "a" + (char)code + "b";

        SidecarWriter.Write(SidecarOf(Entry(value))).ShouldContain("    \"" + value + "\": {\n");
    }

    // The characters a hand-written writer most often gets wrong read back as they went in.
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x1F)]
    [InlineData(0x22)]
    [InlineData(0x5C)]
    [InlineData(0x7F)]
    [InlineData(0x2028)]
    [InlineData(0xD83D)]
    [InlineData(0xDE00)]
    public void Write_AwkwardCharacterInANameAndInAValue_ReadsBackAsItWas(int code)
    {
        var value = ((char)code).ToString();
        var labels = Postgres("e", SidecarValues.Kind.Enum, labels: [value, value + value, "😀" + value]);
        var sidecar = SidecarOf(
            Entry(
                value,
                database: value,
                parameters: [Parameter(0, value, labels)],
                columns: [Column(0, value, Postgres(value), origin: new SidecarOrigin(value, value, value))]
            )
        );

        var read = SidecarReader.Read(SidecarWriter.Write(sidecar));

        WithoutSpans(read.Sidecar.ShouldNotBeNull()).ShouldBe(sidecar);
    }

    [Fact]
    public void Write_EntryWithRows_WritesTheFourKeysOfAResultInTheFormatsOrder()
    {
        var sidecar = SidecarOf(
            Entry(
                matchesTable: new SidecarTable(null, "users"),
                plan: SidecarValues.Plan.Walked,
                tableMatch: SidecarValues.TableMatch.Matched
            )
        );

        SidecarWriter
            .Write(sidecar)
            .ShouldContain(
                string.Join(
                    "\n",
                    "      \"resultKind\": \"rows\",",
                    "      \"matchesTable\": {",
                    "        \"schema\": null,",
                    "        \"table\": \"users\"",
                    "      },",
                    "      \"plan\": \"walked\",",
                    "      \"tableMatch\": \"matched\",",
                    "      \"parameters\": [],",
                    "      \"columns\": [",
                    "        {",
                    "          \"ordinal\": 0,",
                    "          \"name\": \"id\",",
                    "          \"type\": {",
                    "            \"name\": \"integer\",",
                    "            \"kind\": \"base\",",
                    "            \"schema\": \"pg_catalog\",",
                    "            \"internalName\": \"int4\"",
                    "          },",
                    "          \"nullable\": false,",
                    "          \"nullableSource\": \"catalog\",",
                    "          \"origin\": null,",
                    "          \"identity\": null,",
                    "          \"computed\": null",
                    "        }",
                    "      ]",
                    "    }"
                )
            );
    }

    // The condition decides, and not the value.
    [Fact]
    public void Write_EntryWithoutRowsThatHoldsAResult_LeavesTheFourKeysOut()
    {
        var entry = Entry(resultKind: SidecarResultKind.None) with
        {
            MatchesTable = new SidecarTable("public", "users"),
            Plan = SidecarValues.Plan.Walked,
            TableMatch = SidecarValues.TableMatch.Matched,
            Columns = Of(Column(0, "id", Postgres())),
        };

        var text = SidecarWriter.Write(SidecarOf(entry));

        text.ShouldNotContain("\"matchesTable\"");
        text.ShouldNotContain("\"plan\"");
        text.ShouldNotContain("\"tableMatch\"");
        text.ShouldNotContain("\"columns\"");
    }

    [Fact]
    public void Write_EntryWithRowsThatHoldsNoResult_WritesEachOfTheFourAsNull()
    {
        var entry = Entry() with { MatchesTable = null, Plan = null, TableMatch = null, Columns = null };

        var text = SidecarWriter.Write(SidecarOf(entry));

        text.ShouldContain(
            "      \"matchesTable\": null,\n      \"plan\": null,\n"
                + "      \"tableMatch\": null,\n      \"parameters\": [],\n"
        );
        text.ShouldContain("      \"columns\": null\n    }");
    }

    [Fact]
    public void Write_AlwaysKeyWithoutAValue_IsNull()
    {
        var text = SidecarWriter.Write(SidecarOf(Entry(database: null, serverVersion: null)));

        text.ShouldContain("      \"database\": null,\n      \"serverVersion\": null,\n");
    }

    [Fact]
    public void Write_Parameter_WritesTypeSourceWhenItHasAType()
    {
        var sidecar = SidecarOf(
            Entry(
                parameters:
                [
                    Parameter(0, "a", Postgres(), nullable: true, typeSource: SidecarValues.TypeSource.Declared),
                    new SidecarParameter("b", 1, Postgres(), false, null),
                    new SidecarParameter("c", 2, null, null, SidecarValues.TypeSource.Declared),
                ]
            )
        );

        var text = SidecarWriter.Write(sidecar);

        text.ShouldContain("          \"nullable\": true,\n          \"typeSource\": \"declared\"\n        },");
        text.ShouldContain("          \"nullable\": false,\n          \"typeSource\": null\n        },");
        text.ShouldContain(
            string.Join(
                "\n",
                "        {",
                "          \"name\": \"c\",",
                "          \"ordinal\": 2,",
                "          \"type\": null,",
                "          \"nullable\": null",
                "        }",
                "      ],"
            )
        );
    }

    [Fact]
    public void Write_ParameterTypeWithABindName_WritesItAfterTheName()
    {
        var status = Postgres("public.status", "enum", "public", "status", labels: ["a"], bindName: "public.status");
        var sidecar = SidecarOf(
            Entry(resultKind: SidecarResultKind.None, parameters: [Parameter(0, "status", status)])
        );

        SidecarWriter
            .Write(sidecar)
            .ShouldContain(
                string.Join(
                    "\n",
                    "          \"type\": {",
                    "            \"name\": \"public.status\",",
                    "            \"bindName\": \"public.status\",",
                    "            \"kind\": \"enum\","
                )
            );
    }

    [Fact]
    public void Write_SqlServerAndOtherParameterTypesWithABindName_WriteItAfterTheName()
    {
        var sqlServer = SidecarOf(
            Entry(
                engine: SidecarValues.Engine.SqlServer,
                resultKind: SidecarResultKind.None,
                parameters: [Parameter(0, "ids", SqlServer("int", bindName: "dbo.Ids"))]
            )
        );
        var other = SidecarOf(
            Entry(
                engine: "sqlite",
                resultKind: SidecarResultKind.None,
                parameters: [Parameter(0, "id", new OtherEngineType("INTEGER") { BindName = "INT" })]
            )
        );

        SidecarWriter
            .Write(sqlServer)
            .ShouldContain(
                "            \"name\": \"int\",\n            \"bindName\": \"dbo.Ids\",\n            \"maxLength\""
            );
        SidecarWriter
            .Write(other)
            .ShouldContain("            \"name\": \"INTEGER\",\n            \"bindName\": \"INT\"\n          },");
    }

    [Fact]
    public void Write_TypeWithoutABindName_WritesNoKey()
    {
        var sidecar = SidecarOf(Entry(parameters: [Parameter(0, "id", Postgres()), Parameter(1, "untyped", null)]));

        SidecarWriter.Write(sidecar).ShouldNotContain("bindName");
        WriteColumnOf(Postgres()).ShouldNotContain("bindName");
        WriteColumnOf(SqlServer()).ShouldNotContain("bindName");
        WriteColumnOf(new OtherEngineType("INTEGER"), "sqlite").ShouldNotContain("bindName");
    }

    [Fact]
    public void Write_PostgresType_WritesAFacetOnlyWhenItHasAValue()
    {
        var text = WriteColumnOf(Postgres("numeric(18,-2)", internalName: "numeric", precision: 18, scale: -2));

        text.ShouldContain(
            "            \"internalName\": \"numeric\",\n            \"precision\": 18,\n            \"scale\": -2\n"
        );
        text.ShouldNotContain("\"length\"");
    }

    [Theory]
    [InlineData("array", "element")]
    [InlineData("domain", "base")]
    [InlineData("range", "subtype")]
    [InlineData("multirange", "subtype")]
    public void Write_PostgresType_WritesTheNestedTypeItsKindNames(string kind, string key)
    {
        var inner = Postgres("text", internalName: "text");
        var all = Postgres("n", kind, element: inner, baseType: inner, labels: ["a"], subtype: inner);
        var none = Postgres("n", kind);

        var text = WriteColumnOf(all);

        text.ShouldContain("            \"" + key + "\": {\n              \"name\": \"text\",");
        foreach (var other in new[] { "element", "base", "labels", "subtype" })
        {
            if (other != key)
            {
                text.ShouldNotContain("\"" + other + "\":");
            }
        }

        WriteColumnOf(none).ShouldContain("            \"" + key + "\": null\n");
    }

    [Fact]
    public void Write_PostgresEnum_WritesItsLabelsOneOnALine()
    {
        WriteColumnOf(Postgres("e", "enum", labels: ["active", "deleted"]))
            .ShouldContain(
                "            \"labels\": [\n              \"active\",\n              \"deleted\"\n            ]\n"
            );
        WriteColumnOf(Postgres("e", "enum", labels: [])).ShouldContain("            \"labels\": []\n");
        WriteColumnOf(Postgres("e", "enum")).ShouldContain("            \"labels\": null\n");
        WriteColumnOf(Postgres("b", "base", labels: ["a"])).ShouldNotContain("\"labels\"");
    }

    [Fact]
    public void Write_SqlServerType_WritesItsFacetsAlwaysAndItsUserTypeWhenItHasOne()
    {
        var plain = WriteColumnOf(SqlServer("nvarchar(max)", -1, 0, 0));
        var alias = WriteColumnOf(SqlServer(userType: new SqlServerUserType("dbo", "CustomerId", null)));
        var clr = WriteColumnOf(SqlServer(userType: new SqlServerUserType(null, "geography", "Types.SqlGeography")));

        plain.ShouldContain(
            string.Join(
                "\n",
                "          \"type\": {",
                "            \"name\": \"nvarchar(max)\",",
                "            \"maxLength\": -1,",
                "            \"precision\": 0,",
                "            \"scale\": 0",
                "          },"
            )
        );
        alias.ShouldContain(
            string.Join(
                "\n",
                "            \"scale\": 0,",
                "            \"userType\": {",
                "              \"schema\": \"dbo\",",
                "              \"name\": \"CustomerId\"",
                "            }",
                "          },"
            )
        );
        clr.ShouldContain(
            "              \"schema\": null,\n              \"name\": \"geography\",\n"
                + "              \"assemblyQualifiedName\": \"Types.SqlGeography\"\n"
        );
    }

    [Fact]
    public void Write_TypeOfAnotherEngine_IsItsNameAlone() =>
        WriteColumnOf(new OtherEngineType("INTEGER"), "sqlite")
            .ShouldContain("          \"type\": {\n            \"name\": \"INTEGER\"\n          },\n");

    // In Swedish the minus sign of a number is U+2212.  A sidecar is the same file on every machine.
    [Fact]
    public void Write_UnderAnotherCulture_WritesNumbersTheSameWay()
    {
        using var swedish = new CultureScope(new CultureInfo("sv-SE"));

        WriteColumnOf(SqlServer("nvarchar(max)", -1, 0, 0)).ShouldContain("\"maxLength\": -1,");
    }

    private static string WriteColumnOf(SidecarType type, string engine = SidecarValues.Engine.Postgres) =>
        SidecarWriter.Write(
            SidecarOf(
                Entry(
                    engine: type is SqlServerType ? SidecarValues.Engine.SqlServer : engine,
                    columns: [Column(0, "c", type)]
                )
            )
        );
}
