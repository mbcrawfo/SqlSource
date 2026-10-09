using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation;

// The generator reads an enum argument of the attribute as a number and casts it to its own form of the enum.  The
// two must agree member by member, or a value a user writes would mean another.
public partial class AttributeSourceTests
{
    // The generator's own forms are internal, so a public theory names them and the test looks them up.
    [Theory]
    [InlineData("SqlLocation", nameof(MemberPlacement))]
    [InlineData("GeneratorOutput", nameof(OutputKind))]
    public void EmittedEnum_HasTheMembersAndNumbersOfTheGeneratorsOwnForm(string emitted, string ownName)
    {
        var own = new[] { typeof(MemberPlacement), typeof(OutputKind) }.Single(type => type.Name == ownName);
        var body = Regex
            .Match(AttributeSource.Text, @"internal enum " + emitted + @"\s*\{(?<body>[^}]*)\}")
            .Groups["body"]
            .Value;

        var members = Member()
            .Matches(body)
            .Select(match => match.Groups["name"].Value + "=" + match.Groups["value"].Value);

        members.ShouldBe(
            Enum.GetValues(own)
                .Cast<object>()
                .Select(value => value + "=" + Convert.ToInt32(value, CultureInfo.InvariantCulture))
        );
    }

    [Fact]
    public void GeneratedTypes_AreEveryTypeTheFileDeclares()
    {
        var declared = Declaration()
            .Matches(AttributeSource.Text)
            .Select(match => "SqlSource." + match.Groups["name"].Value);

        AttributeSource.GeneratedTypes.ShouldBe(declared, ignoreOrder: true);
    }

    [GeneratedRegex(@"internal (?:enum|sealed class) (?<name>\w+)")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^\s*(?<name>\w+) = (?<value>\d+),", RegexOptions.Multiline)]
    private static partial Regex Member();
}
