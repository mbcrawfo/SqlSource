using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Models;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodConflictsTests
{
    [Fact]
    public void Find_NoClash_DropsNothing()
    {
        var conflicts = Find([
            Signature("App.First", "FirstExtensions", "Get"),
            Signature("App.Second", "SecondExtensions", "List"),
            Signature("App.Third", null, "Get"),
        ]);

        conflicts.ShouldBe(
            new MethodConflicts(
                EquatableArray<DroppedMethod>.Empty,
                EquatableArray<string>.Empty,
                EquatableArray<DiagnosticInfo>.Empty
            )
        );
    }

    [Fact]
    public void Find_TwoTypesOfOneExtensionClassWithOneName_DropsBothWithSqlsrc274()
    {
        var first = Signature("App.First.Queries", "QueriesExtensions", "Get", parameterKey: "global::App.GetInput");
        var second = Signature("App.Second.Queries", "QueriesExtensions", "Get", path: "b.sql");

        var conflicts = Find([first, second]);

        conflicts.Dropped.ShouldBe([
            new DroppedMethod("App.First.Queries", first.NameLocation),
            new DroppedMethod("App.Second.Queries", second.NameLocation),
        ]);
        conflicts.BlockedTypes.ShouldBeEmpty();
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC274 a.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.Second.Queries' has that name too, in the class 'App.QueriesExtensions'.  "
                    + "Rename the query.",
                "SQLSRC274 b.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.First.Queries' has that name too, in the class 'App.QueriesExtensions'.  "
                    + "Rename the query.",
            ]);
    }

    [Fact]
    public void Find_TwoExtensionClassesOfOneNamespaceWithOneSignature_DropsBothWithSqlsrc274()
    {
        var first = Signature("App.First", "FirstExtensions", "Get", parameterKey: ",string");
        var second = Signature("App.Second", "SecondExtensions", "Get", parameterKey: ",string", path: "b.sql");

        var conflicts = Find([first, second]);

        conflicts.Dropped.ShouldBe([
            new DroppedMethod("App.First", first.NameLocation),
            new DroppedMethod("App.Second", second.NameLocation),
        ]);
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC274 a.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.Second', in the class 'App.SecondExtensions' of the same namespace, has "
                    + "that name and the same parameters.  Rename the query.",
                "SQLSRC274 b.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.First', in the class 'App.FirstExtensions' of the same namespace, has "
                    + "that name and the same parameters.  Rename the query.",
            ]);
    }

    // Overloads of two classes are told apart by their parameters, as long as they differ.
    [Fact]
    public void Find_TwoExtensionClassesWithOneNameAndOtherParameters_DropsNothing()
    {
        var conflicts = Find([
            Signature("App.First", "FirstExtensions", "Get", parameterKey: "global::App.GetInput"),
            Signature("App.Second", "SecondExtensions", "Get"),
        ]);

        conflicts.Dropped.ShouldBeEmpty();
        conflicts.Diagnostics.ShouldBeEmpty();
    }

    // Two classes of two namespaces, and a method on its own type, never meet.
    [Fact]
    public void Find_OneSignatureInTwoNamespacesOrOnTheType_DropsNothing()
    {
        var conflicts = Find([
            Signature("App.A.Sample", "SampleExtensions", "Get", ns: "App.A"),
            Signature("App.B.Sample", "SampleExtensions", "Get", ns: "App.B"),
            Signature("App.A.Other", null, "Get", ns: "App.A"),
        ]);

        conflicts.Dropped.ShouldBeEmpty();
        conflicts.Diagnostics.ShouldBeEmpty();
    }

    // TakenReason is internal, so a row gives its number.
    [Theory]
    [InlineData((int)TakenReason.Type, "App.SampleExtensions", "the project has a type of that name")]
    [InlineData((int)TakenReason.Namespace, "App.SampleExtensions", "the project has a namespace of that name")]
    [InlineData((int)TakenReason.NamespacePartIsType, "App", "'App' is a type of the project")]
    public void Find_ExtensionClassTakenByTheProject_BlocksTheTypeWithSqlsrc276(int reason, string part, string because)
    {
        var conflicts = MethodConflicts.Find(
            Sorted(
                Signature("App.Sample", "SampleExtensions", "Get"),
                Signature("App.Sample", "SampleExtensions", "List")
            ),
            new EquatableArray<TakenName>([new TakenName("App.SampleExtensions", (TakenReason)reason, part)]),
            EquatableArray<string>.Empty
        );

        conflicts.BlockedTypes.ShouldBe(["App.Sample"]);
        conflicts.Dropped.ShouldBeEmpty();
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC276 Sample.cs: The query methods of 'App.Sample' cannot go into the extension class "
                    + "'App.SampleExtensions', because "
                    + because
                    + ".  Rename the type or what has the name, or set the attribute's MethodLocation to put the "
                    + "methods on the type.",
            ]);
    }

    [Theory]
    [InlineData("App.SampleExtensions", "SqlSource writes a model of that name")]
    [InlineData("App.SampleExtensions.Row", "SqlSource writes a model in a namespace of that name")]
    public void Find_ExtensionClassNamedAsAModel_BlocksTheTypeWithSqlsrc276(string model, string because)
    {
        var conflicts = MethodConflicts.Find(
            Sorted(Signature("App.Sample", "SampleExtensions", "Get")),
            EquatableArray<TakenName>.Empty,
            new EquatableArray<string>(["App.GetDto", model])
        );

        conflicts.BlockedTypes.ShouldBe(["App.Sample"]);
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC276 Sample.cs: The query methods of 'App.Sample' cannot go into the extension class "
                    + "'App.SampleExtensions', because "
                    + because
                    + ".  Rename the type or what has the name, or set the attribute's MethodLocation to put the "
                    + "methods on the type.",
            ]);
    }

    [Fact]
    public void Find_ExtensionClassOfTwoAccessibilities_BlocksBothWithSqlsrc276()
    {
        var conflicts = Find([
            Signature("App.First.Queries", "QueriesExtensions", "GetA", attributePath: "First.cs"),
            Signature("App.Second.Queries", "QueriesExtensions", "GetB", isPublic: false, attributePath: "Second.cs"),
        ]);

        conflicts.BlockedTypes.ShouldBe(["App.First.Queries", "App.Second.Queries"]);
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC276 First.cs: The query methods of 'App.First.Queries' cannot go into the extension class "
                    + "'App.QueriesExtensions', because 'App.Second.Queries' puts its methods there too, and the "
                    + "class would be public for one and internal for the other.  Rename the type or what has the "
                    + "name, or set the attribute's MethodLocation to put the methods on the type.",
                "SQLSRC276 Second.cs: The query methods of 'App.Second.Queries' cannot go into the extension class "
                    + "'App.QueriesExtensions', because 'App.First.Queries' puts its methods there too, and the "
                    + "class would be public for one and internal for the other.  Rename the type or what has the "
                    + "name, or set the attribute's MethodLocation to put the methods on the type.",
            ]);
    }

    // A blocked type writes no method, so its methods take no name from the others.
    [Fact]
    public void Find_MethodOfABlockedType_ClashesWithNothing()
    {
        var conflicts = MethodConflicts.Find(
            Sorted(
                Signature("App.First", "FirstExtensions", "Get"),
                Signature("App.Second", "SecondExtensions", "Get", path: "b.sql")
            ),
            new EquatableArray<TakenName>([
                new TakenName("App.FirstExtensions", TakenReason.Type, "App.FirstExtensions"),
            ]),
            EquatableArray<string>.Empty
        );

        conflicts.BlockedTypes.ShouldBe(["App.First"]);
        conflicts.Dropped.ShouldBeEmpty();
        conflicts.Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id).ShouldBe(["SQLSRC276"]);
    }

    // Three types claim one file, so their methods share the query's place: the two of one namespace clash, and the
    // third, of another namespace, keeps its method.
    [Fact]
    public void DroppedOf_QueryOfThreeTypesWithAClashBetweenTwo_LeavesTheThirdItsMethod()
    {
        var first = Signature("App.First", "FirstExtensions", "Get");
        var second = Signature("App.Second", "SecondExtensions", "Get");
        var third = Signature("Other.Third", "ThirdExtensions", "Get", ns: "Other");

        var conflicts = Find([first, second, third]);

        conflicts.DroppedOf("App.First").ShouldBe([first.NameLocation]);
        conflicts.DroppedOf("App.Second").ShouldBe([second.NameLocation]);
        conflicts.DroppedOf("Other.Third").ShouldBeEmpty();
    }

    // Each of three methods of one class is dropped, and each names the first of the others.
    [Fact]
    public void Find_ThreeTypesOfOneExtensionClassWithOneName_DropsAllThreeWithSqlsrc274()
    {
        var conflicts = Find([
            Signature("App.A.Queries", "QueriesExtensions", "Get", path: "a.sql"),
            Signature("App.B.Queries", "QueriesExtensions", "Get", path: "b.sql"),
            Signature("App.C.Queries", "QueriesExtensions", "Get", path: "c.sql"),
        ]);

        conflicts
            .Dropped.Select(dropped => dropped.TypeKey)
            .ShouldBe(["App.A.Queries", "App.B.Queries", "App.C.Queries"]);
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC274 a.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.B.Queries' has that name too, in the class 'App.QueriesExtensions'.  "
                    + "Rename the query.",
                "SQLSRC274 b.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.A.Queries' has that name too, in the class 'App.QueriesExtensions'.  "
                    + "Rename the query.",
                "SQLSRC274 c.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'App.A.Queries' has that name too, in the class 'App.QueriesExtensions'.  "
                    + "Rename the query.",
            ]);
    }

    // The name that the project has is the reason, and each type gets one diagnostic, not one for each reason.
    [Fact]
    public void Find_ExtensionClassTakenAndOfTwoAccessibilities_ReportsTheTakenNameOnceForEachType()
    {
        var conflicts = MethodConflicts.Find(
            Sorted(
                Signature("App.First.Queries", "QueriesExtensions", "GetA", attributePath: "First.cs"),
                Signature(
                    "App.Second.Queries",
                    "QueriesExtensions",
                    "GetB",
                    isPublic: false,
                    attributePath: "Second.cs"
                )
            ),
            new EquatableArray<TakenName>([
                new TakenName("App.QueriesExtensions", TakenReason.Type, "App.QueriesExtensions"),
            ]),
            EquatableArray<string>.Empty
        );

        conflicts.BlockedTypes.ShouldBe(["App.First.Queries", "App.Second.Queries"]);
        Texts(conflicts)
            .ShouldBe([
                "SQLSRC276 First.cs: The query methods of 'App.First.Queries' cannot go into the extension class "
                    + "'App.QueriesExtensions', because the project has a type of that name.  Rename the type or what "
                    + "has the name, or set the attribute's MethodLocation to put the methods on the type.",
                "SQLSRC276 Second.cs: The query methods of 'App.Second.Queries' cannot go into the extension class "
                    + "'App.QueriesExtensions', because the project has a type of that name.  Rename the type or what "
                    + "has the name, or set the attribute's MethodLocation to put the methods on the type.",
            ]);
    }

    // A class of the global namespace has its own name as its full name.
    [Fact]
    public void Find_TwoTypesOfTheGlobalNamespaceWithOneName_NamesTheClassWithoutANamespace()
    {
        var conflicts = Find([
            Signature("First.Queries", "QueriesExtensions", "Get", ns: "", path: "a.sql"),
            Signature("Second.Queries", "QueriesExtensions", "Get", ns: "", path: "b.sql"),
        ]);

        Texts(conflicts)
            .ShouldBe([
                "SQLSRC274 a.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'Second.Queries' has that name too, in the class 'QueriesExtensions'.  Rename "
                    + "the query.",
                "SQLSRC274 b.sql: The method of the query 'Get' cannot be named 'Get', because the method of the "
                    + "query 'Get' of 'First.Queries' has that name too, in the class 'QueriesExtensions'.  Rename "
                    + "the query.",
            ]);
    }

    // The value does not depend on the order of the compilation's types.
    [Fact]
    public void Find_SignaturesInAnotherOrder_GiveAnEqualValue()
    {
        MethodSignature[] signatures =
        [
            Signature("App.First", "FirstExtensions", "Get"),
            Signature("App.Second", "SecondExtensions", "Get", path: "b.sql"),
            Signature("App.Third.Queries", "QueriesExtensions", "List", attributePath: "Third.cs"),
            Signature("App.Fourth.Queries", "QueriesExtensions", "List", isPublic: false, attributePath: "Fourth.cs"),
        ];

        var forward = Find(signatures);
        var backward = Find([.. signatures.Reverse()]);

        backward.ShouldBe(forward);
        forward.Dropped.Select(dropped => dropped.TypeKey).ShouldBe(["App.First", "App.Second"]);
        forward.BlockedTypes.ShouldBe(["App.Fourth.Queries", "App.Third.Queries"]);
    }

    private static MethodConflicts Find(MethodSignature[] signatures) =>
        MethodConflicts.Find(
            new EquatableArray<MethodSignature>([.. signatures]),
            EquatableArray<TakenName>.Empty,
            EquatableArray<string>.Empty
        );

    private static EquatableArray<MethodSignature> Sorted(params MethodSignature[] signatures) => new([.. signatures]);

    // The type's key is its full name, which no test gives a type parameter.
    private static MethodSignature Signature(
        string typeKey,
        string? extensionClass,
        string name,
        string ns = "App",
        bool isPublic = true,
        string parameterKey = "",
        string path = "a.sql",
        string attributePath = "Sample.cs"
    ) =>
        new(
            typeKey,
            typeKey,
            ns,
            extensionClass,
            isPublic,
            name,
            name,
            parameterKey,
            new LocationInfo(path, new TextSpan(9, name.Length), default),
            new LocationInfo(attributePath, new TextSpan(1, 17), default)
        );

    private static string[] Texts(MethodConflicts conflicts) =>
        [
            .. conflicts.Diagnostics.Select(diagnostic =>
                diagnostic.Descriptor.Id
                + " "
                + diagnostic.Location.Path
                + ": "
                + diagnostic.ToDiagnostic().GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            ),
        ];
}
