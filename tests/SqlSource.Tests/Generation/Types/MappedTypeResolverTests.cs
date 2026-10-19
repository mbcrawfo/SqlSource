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

    private const string FriendSource = """
        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestAssembly")]

        namespace Friend
        {
            internal sealed class Shared;
        }
        """;

    // The project, with three references: two that share names, public in both, public in one alone, or internal in
    // both, the first of which also has a name of the project; and one that shows the project its internals.
    private static readonly Lazy<CSharpCompilation> Project = new(() =>
        GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/MyApp.cs", ProjectSource)],
            references:
            [
                GeneratorHarness.CreateReference(OutputKind.Sql, "Other", OtherSource),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Another", AnotherSource),
                GeneratorHarness.CreateReference(OutputKind.Sql, "Friend", FriendSource),
            ]
        )
    );

    [Theory]
    [InlineData("int", nameof(MappedTypeStatus.Found), true, true, false)]
    [InlineData("string", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.UserId", nameof(MappedTypeStatus.Found), true, true, false)]
    [InlineData("global::System.Uri", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::TopLevel", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.Outer.Inner", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.Holder<int>.Item", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData(
        "global::System.Collections.Generic.List<global::MyApp.UserId>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        false
    )]
    [InlineData("global::MyApp.Box<global::System.Uri>", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData(
        "global::System.Collections.Generic.KeyValuePair<int, string>",
        nameof(MappedTypeStatus.Found),
        true,
        true,
        false
    )]
    [InlineData("int?[]", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("int[,]", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.UserId[]", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::System.Collections.Generic.List<int?>", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.Box<global::Other.Thing>", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::MyApp.Missing", nameof(MappedTypeStatus.NotFound), false, false, false)]
    [InlineData("global::MyApp.Box<int, int>", nameof(MappedTypeStatus.NotFound), false, false, false)]
    [InlineData("global::MyApp.Box<global::Other.Missing>", nameof(MappedTypeStatus.NotFound), false, false, false)]
    [InlineData("global::Common.Twice", nameof(MappedTypeStatus.Ambiguous), false, false, false)]
    [InlineData("global::Common.Dup", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::Common.Unseen", nameof(MappedTypeStatus.NotAccessible), false, false, false)]
    [InlineData("global::MyApp.Outer.Closed", nameof(MappedTypeStatus.NotAccessible), false, false, false)]
    [InlineData("global::Common.Clash", nameof(MappedTypeStatus.Found), false, false, false)]
    [InlineData("global::Other.Hidden", nameof(MappedTypeStatus.NotAccessible), false, false, false)]
    [InlineData(
        "global::System.Collections.Generic.List<global::Other.Hidden>",
        nameof(MappedTypeStatus.NotAccessible),
        false,
        false,
        false
    )]
    [InlineData("global::MyApp.Box<global::Other.Hidden>", nameof(MappedTypeStatus.NotAccessible), false, false, false)]
    [InlineData("global::Friend.Shared", nameof(MappedTypeStatus.Found), false, false, false)]
    [InlineData("global::MyApp.Secret", nameof(MappedTypeStatus.Found), false, false, false)]
    [InlineData("global::MyApp.Box<global::MyApp.Secret>", nameof(MappedTypeStatus.Found), false, false, false)]
    [InlineData("global::MyApp.Helpers", nameof(MappedTypeStatus.NotAPropertyType), false, false, false)]
    [InlineData("global::System.Span<int>", nameof(MappedTypeStatus.NotAPropertyType), false, false, false)]
    [InlineData("global::System.Void", nameof(MappedTypeStatus.NotAPropertyType), false, false, false)]
    [InlineData("global::System.Nullable<int>", nameof(MappedTypeStatus.NotAPropertyType), false, false, false)]
    [InlineData(
        "global::MyApp.Box<global::MyApp.Helpers>",
        nameof(MappedTypeStatus.NotAPropertyType),
        false,
        false,
        false
    )]
    [InlineData(
        "global::MyApp.Box<global::System.Nullable<int>?>",
        nameof(MappedTypeStatus.NotAPropertyType),
        false,
        false,
        false
    )]
    [InlineData(
        "global::System.Collections.Generic.List<global::System.Nullable<int>>",
        nameof(MappedTypeStatus.Found),
        false,
        true,
        false
    )]
    [InlineData("global::System.Nullable<int>[]", nameof(MappedTypeStatus.Found), false, true, false)]
    [InlineData("global::System.Nullable<int>?[]", nameof(MappedTypeStatus.NotAPropertyType), false, false, false)]
    [InlineData("global::MyApp.OldThing", nameof(MappedTypeStatus.Found), false, true, true)]
    [InlineData("global::MyApp.Box<global::MyApp.OldThing>", nameof(MappedTypeStatus.Found), false, true, true)]
    public void Resolve_Name_GivesItsStatusAndWhatTheTypeIs(
        string name,
        string status,
        bool isValueType,
        bool isPublic,
        bool isObsolete
    )
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken);

        types
            .ShouldHaveSingleItem()
            .ShouldBe(new MappedType(name, Enum.Parse<MappedTypeStatus>(status), isValueType, isPublic, isObsolete));
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
            new MappedType("global::MyApp.Missing", MappedTypeStatus.NotFound, false, false, false),
            new MappedType("global::MyApp.UserId", MappedTypeStatus.Found, true, true, false),
            new MappedType("int", MappedTypeStatus.Found, true, true, false),
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
