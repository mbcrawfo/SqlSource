using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class MappingFileTests
{
    [Fact]
    public void Write_TwoNames_ListsEachRenamedName()
    {
        var text = MappingFile.Write([
            new MappedName("t_1a2b3c4d", NameClass.Table, "users"),
            new MappedName("l_00000000", NameClass.Literal, "it's <b>"),
        ]);

        text.ShouldBe(
            "{\n"
                + "  \"mappingVersion\": 1,\n"
                + "  \"names\": [\n"
                + "    {\n"
                + "      \"replacement\": \"t_1a2b3c4d\",\n"
                + "      \"class\": \"table\",\n"
                + "      \"original\": \"users\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"replacement\": \"l_00000000\",\n"
                + "      \"class\": \"literal\",\n"
                + "      \"original\": \"it's <b>\"\n"
                + "    }\n"
                + "  ]\n"
                + "}\n"
        );
    }

    [Fact]
    public void Write_NoNames_IsAnEmptyList() =>
        MappingFile
            .Write([])
            .ShouldBe( /*lang=json,strict*/
                "{\n  \"mappingVersion\": 1,\n  \"names\": []\n}\n"
            );
}
