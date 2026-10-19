using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Types;

public sealed class MappedTypeResolverTests
{
    private const string ProjectSource = """
        public sealed class TopLevel;

        namespace MyApp
        {
            public readonly record struct UserId(int Value);

            public sealed class Box<T>;

            public class Outer
            {
                public sealed class Inner;

                private sealed class Closed;
            }

            public sealed class Holder<T>
            {
                public sealed class Item;
            }

            internal sealed class Secret;

            public static class Helpers;

            [System.Obsolete]
            public sealed class OldThing;

            [System.Obsolete("Use another.", DiagnosticId = "MYAPP001")]
            public sealed class Flagged;

            [System.Obsolete("Gone.", true)]
            public sealed class Gone;

            [System.Obsolete("Odd.", DiagnosticId = "MY-APP")]
            public sealed class OddId;

            [System.Obsolete("Numbered.", DiagnosticId = "1234")]
            public sealed class Numbered;

            [System.Diagnostics.CodeAnalysis.Experimental("TRIAL001")]
            public sealed class Trial;

            [System.Diagnostics.CodeAnalysis.Experimental("TRIAL002")]
            public class TrialOuter
            {
                public sealed class Inner;
            }

            // CS9211 here, and CS9204 at each use.
            [System.Diagnostics.CodeAnalysis.Experimental("")]
            public sealed class Unnamed;
        }

        namespace Common
        {
            // The project's own, internal so that the result tells it from the reference's, which is public.
            internal sealed class Clash;
        }
        """;

    private const string OtherSource = """
        namespace Other
        {
            public sealed class Thing;

            internal sealed class Hidden;
        }

        namespace Common
        {
            public sealed class Clash;

            public sealed class Twice;

            public sealed class Dup;

            internal sealed class Unseen;
        }
        """;

    private const string AnotherSource = """
        namespace Common
        {
            public sealed class Twice;

            internal sealed class Dup;

            internal sealed class Unseen;
        }
        """;

    private const string LabSource = """
        [assembly: System.Diagnostics.CodeAnalysis.Experimental("LAB001")]

        namespace Lab
        {
            public sealed class Tool;
        }
        """;

    private const string FriendSource = """
        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestAssembly")]

        namespace Friend
        {
            internal sealed class Shared;
        }
        """;

    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    // The project, with four references: two that share names, public in both, public in one alone, or internal in
    // both, the first of which also has a name of the project; one that shows the project its internals; and one
    // whose assembly is marked [Experimental].
    private static readonly Lazy<CSharpCompilation> Project = new(() =>
        GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/MyApp.cs", ProjectSource)],
            references:
            [
                GeneratorHarness.CreateReference(OutputKind.Sql, "Other", OtherSource),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Another", AnotherSource),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Friend", FriendSource),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Lab", LabSource),
            ]
        )
    );

    [Theory]
    [InlineData("int", nameof(MappedTypeStatus.Found), true, true, "")]
    [InlineData("string", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.UserId", nameof(MappedTypeStatus.Found), true, true, "")]
    [InlineData("global::System.Uri", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::TopLevel", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.Outer.Inner", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.Holder<int>.Item", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData(
        "global::System.Collections.Generic.List<global::MyApp.UserId>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        ""
    )]
    [InlineData("global::MyApp.Box<global::System.Uri>", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData(
        "global::System.Collections.Generic.KeyValuePair<int, string>",
        nameof(MappedTypeStatus.Found),
        true,
        true,
        ""
    )]
    [InlineData("int?[]", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("int[,]", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.UserId[]", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::System.Collections.Generic.List<int?>", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.Box<global::Other.Thing>", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::MyApp.Missing", nameof(MappedTypeStatus.NotFound), false, false, "")]
    [InlineData("global::MyApp.Box<int, int>", nameof(MappedTypeStatus.NotFound), false, false, "")]
    [InlineData("global::MyApp.Box<global::Other.Missing>", nameof(MappedTypeStatus.NotFound), false, false, "")]
    [InlineData("global::Common.Twice", nameof(MappedTypeStatus.Ambiguous), false, false, "")]
    [InlineData("global::Common.Dup", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::Common.Unseen", nameof(MappedTypeStatus.NotAccessible), false, false, "")]
    [InlineData("global::MyApp.Outer.Closed", nameof(MappedTypeStatus.NotAccessible), false, false, "")]
    [InlineData("global::Common.Clash", nameof(MappedTypeStatus.Found), false, false, "")]
    [InlineData("global::Other.Hidden", nameof(MappedTypeStatus.NotAccessible), false, false, "")]
    [InlineData(
        "global::System.Collections.Generic.List<global::Other.Hidden>",
        nameof(MappedTypeStatus.NotAccessible),
        false,
        false,
        ""
    )]
    [InlineData("global::MyApp.Box<global::Other.Hidden>", nameof(MappedTypeStatus.NotAccessible), false, false, "")]
    [InlineData("global::Friend.Shared", nameof(MappedTypeStatus.Found), false, false, "")]
    [InlineData("global::MyApp.Secret", nameof(MappedTypeStatus.Found), false, false, "")]
    [InlineData("global::MyApp.Box<global::MyApp.Secret>", nameof(MappedTypeStatus.Found), false, false, "")]
    [InlineData("global::MyApp.Helpers", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::System.Span<int>", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::System.Void", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::System.Nullable<int>", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData(
        "global::MyApp.Box<global::MyApp.Helpers>",
        nameof(MappedTypeStatus.NotAPropertyType),
        false,
        false,
        ""
    )]
    [InlineData(
        "global::MyApp.Box<global::System.Nullable<int>?>",
        nameof(MappedTypeStatus.NotAPropertyType),
        false,
        false,
        ""
    )]
    [InlineData(
        "global::System.Collections.Generic.List<global::System.Nullable<int>>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        ""
    )]
    [InlineData("global::System.Nullable<int>[]", nameof(MappedTypeStatus.Found), false, true, "")]
    [InlineData("global::System.Nullable<int>?[]", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::MyApp.OldThing", nameof(MappedTypeStatus.Found), false, true, "CS0612,CS0618")]
    [InlineData(
        "global::MyApp.Box<global::MyApp.OldThing>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        "CS0612,CS0618"
    )]
    [InlineData("global::MyApp.Flagged", nameof(MappedTypeStatus.Found), false, true, "MYAPP001")]
    [InlineData("global::MyApp.Trial", nameof(MappedTypeStatus.Found), false, true, "TRIAL001")]
    [InlineData("global::MyApp.TrialOuter.Inner", nameof(MappedTypeStatus.Found), false, true, "TRIAL002")]
    [InlineData("global::MyApp.Unnamed", nameof(MappedTypeStatus.Found), false, true, "CS9204")]
    [InlineData("global::Lab.Tool", nameof(MappedTypeStatus.Found), false, true, "LAB001")]
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::MyApp.Trial, global::MyApp.Box<global::MyApp.OldThing>>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        "CS0612,CS0618,TRIAL001"
    )]
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::MyApp.OldThing, global::MyApp.OldThing>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        "CS0612,CS0618"
    )]
    [InlineData("global::MyApp.Gone", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::MyApp.OddId", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::MyApp.Numbered", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    [InlineData("global::MyApp.Box<global::MyApp.Gone>", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    public void Resolve_Name_GivesItsStatusAndWhatTheTypeIs(
        string name,
        string status,
        bool isValueType,
        bool isPublic,
        string warningIds
    )
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken);

        types
            .ShouldHaveSingleItem()
            .ShouldBe(
                new MappedType(
                    name,
                    Enum.Parse<MappedTypeStatus>(status),
                    isValueType,
                    isPublic,
                    warningIds.Length == 0 ? NoWarnings : Names(warningIds.Split(','))
                )
            );
    }

    // Of two failures in one name, the first in the order of the statuses is the whole's: a missing argument over an
    // inaccessible one, an ambiguous argument over a static one, and an inaccessible argument over a static one.
    [Theory]
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::Other.Hidden, global::MyApp.Missing>",
        nameof(MappedTypeStatus.NotFound)
    )]
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::MyApp.Helpers, global::Common.Twice>",
        nameof(MappedTypeStatus.Ambiguous)
    )]
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::MyApp.Helpers, global::Other.Hidden>",
        nameof(MappedTypeStatus.NotAccessible)
    )]
    public void Resolve_NameWithTwoFailures_GivesTheFirstInTheOrderOfTheStatuses(string name, string status)
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken);

        types.ShouldHaveSingleItem().Status.ShouldBe(Enum.Parse<MappedTypeStatus>(status));
    }

    [Fact]
    public void Resolve_SeveralNames_GivesOneRecordForEachInTheirOrder()
    {
        var names = Names("global::MyApp.Missing", "global::MyApp.UserId", "int");

        var types = MappedTypeResolver.Resolve(Project.Value, names, TestContext.Current.CancellationToken);

        types.ShouldBe([
            new MappedType("global::MyApp.Missing", MappedTypeStatus.NotFound, false, false, NoWarnings),
            new MappedType("global::MyApp.UserId", MappedTypeStatus.Found, true, true, NoWarnings),
            new MappedType("int", MappedTypeStatus.Found, true, true, NoWarnings),
        ]);
        MappedTypeResolver.Find(types, "global::MyApp.UserId").ShouldBe(types[1]);
        MappedTypeResolver.Find(types, "int").ShouldBe(types[2]);
        MappedTypeResolver.Find(types, "global::MyApp.Missing").ShouldBe(types[0]);
    }

    [Fact]
    public void Resolve_NoNames_GivesNoTypes()
    {
        var types = MappedTypeResolver.Resolve(
            Project.Value,
            EquatableArray<string>.Empty,
            TestContext.Current.CancellationToken
        );

        types.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_TextThatIsNoType_IsNotFound()
    {
        var types = MappedTypeResolver.Resolve(
            Project.Value,
            Names("not a type"),
            TestContext.Current.CancellationToken
        );

        types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.NotFound);
    }

    // An open generic is the spec's case of a type that cannot be a property's.  The grammar has no way to write
    // one, so no override names it.
    [Fact]
    public void TryParse_OpenGeneric_IsRefused()
    {
        ClrTypeName.TryParse("System.Collections.Generic.List<>".AsSpan(), out _).ShouldBeFalse();
        ClrTypeName.TryParse("global::MyApp.Box<>".AsSpan(), out _).ShouldBeFalse();
    }

    [Fact]
    public void Find_NameNotResolved_GivesNull()
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names("int"), TestContext.Current.CancellationToken);

        MappedTypeResolver.Find(types, "long").ShouldBeNull();
        MappedTypeResolver.Find(EquatableArray<MappedType>.Empty, "int").ShouldBeNull();
    }

    private static EquatableArray<string> Names(params string[] names) => new(ImmutableArray.Create(names));
}
