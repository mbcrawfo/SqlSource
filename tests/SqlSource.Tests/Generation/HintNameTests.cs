using System;
using System.Collections.Immutable;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class HintNameTests
{
    [Fact]
    public void Create_Type_JoinsTheNamespaceAndTheTypeNamesWithTheArityOfEachGenericType()
    {
        var type = TestModels.Type(
            @namespace: "App.@event.Data",
            types:
            [
                new TypeDeclaration("class", "Outer", "Outer", "<TKey, TValue>", 2),
                new TypeDeclaration("struct", "@class", "class", "", 0),
            ]
        );

        HintName.Create(type).ShouldBe("App.event.Data.Outer-2.class.g.cs");
    }

    [Fact]
    public void Create_TypeInTheGlobalNamespace_HasNoLeadingDot() =>
        HintName.Create(TestModels.Type(@namespace: "")).ShouldBe("UserRepository.g.cs");

    [Fact]
    public void FindAmbiguous_Names_GivesThoseThatAreEqualIgnoringCaseToAnotherOrToTheAttributes()
    {
        var ambiguous = HintName.FindAmbiguous(
            ImmutableArray.Create(
                "App.Sample.g.cs",
                "App.Other.g.cs",
                "App.sample.g.cs",
                "sqlsourcegenerateattribute.g.cs",
                "Other.Sample.g.cs"
            )
        );

        ambiguous.ShouldBe(
            ["App.Sample.g.cs", "App.sample.g.cs", "sqlsourcegenerateattribute.g.cs"],
            ignoreOrder: true
        );
    }

    [Fact]
    public void FindAmbiguous_NamesThatAllDiffer_GivesNone() =>
        HintName.FindAmbiguous(["App.Sample.g.cs", "App.Sample-1.g.cs", "Other.Sample.g.cs"]).ShouldBeEmpty();

    [Fact]
    public void MakeUnique_NameThatIsNotAmbiguous_IsKept() =>
        HintName
            .MakeUnique("App.Other.g.cs", TestModels.Array("App.Sample.g.cs", "App.sample.g.cs"))
            .ShouldBe("App.Other.g.cs");

    [Fact]
    public void MakeUnique_AmbiguousName_GainsAHashOfItsExactSpelling()
    {
        var ambiguous = TestModels.Array("App.Sample.g.cs", "App.sample.g.cs");

        // The values are fixed: a name must not change from one build to the next.
        HintName.MakeUnique("App.Sample.g.cs", ambiguous).ShouldBe("App.Sample.22B482A5.g.cs");
        HintName.MakeUnique("App.sample.g.cs", ambiguous).ShouldBe("App.sample.A4506F05.g.cs");
        string.Equals(
                HintName.MakeUnique("App.Sample.g.cs", ambiguous),
                HintName.MakeUnique("App.sample.g.cs", ambiguous),
                StringComparison.OrdinalIgnoreCase
            )
            .ShouldBeFalse();
    }
}
