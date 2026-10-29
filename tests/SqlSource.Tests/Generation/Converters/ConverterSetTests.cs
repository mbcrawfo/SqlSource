using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Converters;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Converters;

public sealed class ConverterSetTests
{
    [Fact]
    public void Of_ProjectAndMappedTypeOfOneEdge_KeepsTheProjects()
    {
        var project = TestSidecars.Of(
            ProjectConverter("global::App.Conversions.ToEmail", "string", "global::Lib.Email", 3)
        );
        var mapped = TestSidecars.Of(
            MappedTypeConverter("global::Lib.Email.From", "string", "global::Lib.Email"),
            MappedTypeConverter("global::Lib.Email.To", "global::Lib.Email", "string")
        );

        var set = ConverterSet.Of(project, mapped);

        set.Converters.Select(static converter => converter.Method)
            .ShouldBe(["global::Lib.Email.To", "global::App.Conversions.ToEmail"]);
        set.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Of_Converters_AreSortedByArgumentThenReturn()
    {
        var project = TestSidecars.Of(
            ProjectConverter("global::App.C.A", "string", "global::App.Email", 1),
            ProjectConverter("global::App.C.B", "long", "global::App.UserId", 2),
            ProjectConverter("global::App.C.C", "global::App.Email", "string", 3),
            ProjectConverter("global::App.C.D", "long", "global::App.OrderId", 4)
        );
        var mapped = TestSidecars.Of(MappedTypeConverter("global::Lib.Id.From", "int", "global::Lib.Id"));

        var set = ConverterSet.Of(project, mapped);

        set.Converters.Select(static converter => (converter.ArgumentText, converter.ReturnText))
            .ShouldBe([
                ("global::App.Email", "string"),
                ("int", "global::Lib.Id"),
                ("long", "global::App.OrderId"),
                ("long", "global::App.UserId"),
                ("string", "global::App.Email"),
            ]);
    }

    [Fact]
    public void Touching_Type_GivesTheEdgesFromAndToIt()
    {
        var set = ConverterSet.Of(
            TestSidecars.Of(
                ProjectConverter("global::App.C.FromEmail", "global::App.Email", "string", 1),
                ProjectConverter("global::App.C.ToEmail", "string", "global::App.Email", 2),
                ProjectConverter("global::App.C.ToUserId", "long", "global::App.UserId", 3),
                ProjectConverter("global::App.C.Trim", "global::App.Email", "global::App.Email", 4),
                ProjectConverter("global::App.C.FromUserId", "global::App.UserId", "long", 5)
            ),
            TestSidecars.Of(MappedTypeConverter("global::Lib.Email.Parse", "byte[]", "global::App.Email"))
        );

        set.Touching("global::App.Email")
            .Select(static converter => converter.Method)
            .ShouldBe([
                "global::Lib.Email.Parse",
                "global::App.C.Trim",
                "global::App.C.FromEmail",
                "global::App.C.ToEmail",
            ]);
        set.Touching("long")
            .Select(static converter => converter.Method)
            .ShouldBe(["global::App.C.FromUserId", "global::App.C.ToUserId"]);
        set.Touching("int").ShouldBeEmpty();
    }

    private static Converter ProjectConverter(string method, string argument, string result, int line) =>
        new(
            method,
            method.Replace("global::", string.Empty, StringComparison.Ordinal) + "(" + argument + ")",
            argument,
            false,
            result,
            false,
            EquatableArray<string>.Empty,
            null,
            ConverterOrigin.Project,
            new LocationInfo(
                "/app/Repo/Conversions.cs",
                new TextSpan(line * 10, 4),
                new LinePositionSpan(new LinePosition(line, 4), new LinePosition(line, 8))
            )
        );

    private static Converter MappedTypeConverter(string method, string argument, string result) =>
        new(
            method,
            method.Replace("global::", string.Empty, StringComparison.Ordinal) + "(" + argument + ")",
            argument,
            false,
            result,
            false,
            EquatableArray<string>.Empty,
            null,
            ConverterOrigin.MappedType,
            null
        );
}
