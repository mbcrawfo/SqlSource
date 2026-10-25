using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Types;

public sealed class MappedTypeResolverTests
{
    // Nullable annotations are on, as in a generated model, so that a constraint can forbid null.
    private const string ProjectSource = """
        #nullable enable

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

            // A pragma takes a keyword as an id, so these are disabled like any other.
            [System.Obsolete("Keyword.", DiagnosticId = "class")]
            public sealed class KeywordId;

            [System.Diagnostics.CodeAnalysis.Experimental("if")]
            public sealed class KeywordTrial;

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

            public interface IShape;

            public sealed class Circle : IShape;

            public sealed class Square;

            public readonly struct Dot : IShape;

            public class Base;

            public sealed class Derived : Base;

            public abstract class Sketch;

            public sealed class NoDefault
            {
                private NoDefault() { }
            }

            public sealed class Form
            {
                public required string Title { get; init; }
            }

            public sealed class SetForm
            {
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public SetForm() => Title = "";

                public required string Title { get; init; }
            }

            public sealed class ValueBox<T>
                where T : struct;

            public sealed class RefBox<T>
                where T : class;

            public sealed class MaybeRefBox<T>
                where T : class?;

            public sealed class RawBox<T>
                where T : unmanaged;

            public sealed class KeyBox<T>
                where T : notnull;

            public sealed class NewBox<T>
                where T : new();

            public sealed class ShapeBox<T>
                where T : IShape;

            public sealed class BaseBox<T>
                where T : Base;

            public sealed class SelfBox<T>
                where T : System.IComparable<T>;

            public sealed class PairBox<T, U>
                where U : T;

            public interface IMakes
            {
                static abstract int Make();
            }

            public interface IMakesByDefault
            {
                static virtual int Make() => 0;
            }

            public interface IMadeHere : IMakes
            {
                static int IMakes.Make() => 1;
            }

            public enum Color
            {
                Red,
            }

            public sealed class EnumBox<T>
                where T : System.Enum;

            public class Nest<T>
            {
                public sealed class Inner<U>
                    where U : T;
            }
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

    // A library with the converter attribute, as the generator gives it to every project, on a type with converters,
    // one without, one whose converter the project cannot call, one whose converter it cannot see, one whose converter
    // is an operator, and one that is no mapped type.
    private const string LibrarySource = """
        namespace Lib
        {
            public readonly record struct Email(string Value)
            {
                [SqlSource.SqlSourceConverter]
                public static Email From(string v) => new(v);

                [SqlSource.SqlSourceConverter]
                public static string To(Email e) => e.Value;
            }

            public sealed class Plain
            {
                public static Plain Make(string v) => new();
            }

            public class Locked
            {
                [SqlSource.SqlSourceConverter]
                protected static Locked From(string v) => new();
            }

            // The compiler does not read the internal members of a reference that shows the project none.
            public sealed class Hidden
            {
                [SqlSource.SqlSourceConverter]
                internal static Hidden From(string v) => new();
            }

            // An operator, which generated code cannot call by name.
            public readonly record struct Celsius(double Value)
            {
                [SqlSource.SqlSourceConverter]
                public static implicit operator Celsius(double v) => new(v);
            }

            // A static class, which can be no property's type.
            public static class Conversions
            {
                [SqlSource.SqlSourceConverter]
                public static Email ToEmail(int v) => new(v.ToString());
            }
        }
        """;

    // A converter of the project's own source, which the attribute's step reads and the resolver leaves alone.
    private const string ConvertingSource = """
        namespace MyApp
        {
            public readonly record struct Phone(string Value)
            {
                [SqlSource.SqlSourceConverter]
                public static Phone From(string v) => new(v);
            }
        }
        """;

    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    // A project with the converter attribute and a converter of its own, and the library as a reference.
    private static readonly Lazy<CSharpCompilation> ConvertingProject = new(() =>
        GeneratorHarness.CreateCompilation(
            [
                new SourceFile("/app/SqlSourceGenerateAttribute.g.cs", AttributeSource.Text),
                new SourceFile("/app/MyApp.cs", ConvertingSource),
            ],
            references: [GeneratorHarness.CreateReference(OutputKind.Sql, "Lib", LibrarySource)]
        )
    );

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
    [InlineData("global::MyApp.KeywordId", nameof(MappedTypeStatus.Found), false, true, "class")]
    [InlineData("global::MyApp.KeywordTrial", nameof(MappedTypeStatus.Found), false, true, "if")]
    [InlineData("global::MyApp.Box<global::MyApp.Gone>", nameof(MappedTypeStatus.NotAPropertyType), false, false, "")]
    public void Resolve_Name_GivesItsStatusAndWhatTheTypeIs(
        string name,
        string status,
        bool isValueType,
        bool isPublic,
        string warningIds
    )
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken).Types;

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

    // A generic type is found exactly when the compiler takes it as a property's type, in a file with nullable
    // annotations on as a model's is, without an error or a warning: for each kind of constraint, a type argument
    // that meets it and one that does not.  A constraint that forbids null is a warning to break.
    [Theory]
    [InlineData("global::MyApp.ValueBox<int>", true)]
    [InlineData("global::MyApp.ValueBox<global::MyApp.Dot>", true)]
    [InlineData("global::MyApp.RefBox<string>", true)]
    [InlineData("global::MyApp.RefBox<int[]>", true)]
    [InlineData("global::MyApp.RefBox<global::MyApp.IShape>", true)]
    [InlineData("global::MyApp.MaybeRefBox<string?>", true)]
    [InlineData("global::MyApp.RawBox<int>", true)]
    [InlineData("global::MyApp.RawBox<global::MyApp.Dot>", true)]
    [InlineData("global::MyApp.KeyBox<string>", true)]
    [InlineData("global::MyApp.KeyBox<int>", true)]
    [InlineData("global::System.Collections.Generic.Dictionary<int, string?>", true)]
    [InlineData("global::MyApp.NewBox<global::MyApp.Circle>", true)]
    [InlineData("global::MyApp.NewBox<int>", true)]
    [InlineData("global::MyApp.NewBox<int?>", true)]
    [InlineData("global::MyApp.NewBox<global::MyApp.UserId>", true)]
    [InlineData("global::MyApp.NewBox<global::MyApp.SetForm>", true)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.Circle>", true)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.Dot>", true)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.IShape>", true)]
    [InlineData("global::MyApp.BaseBox<global::MyApp.Derived>", true)]
    [InlineData("global::MyApp.BaseBox<global::MyApp.Base>", true)]
    [InlineData("global::MyApp.SelfBox<int>", true)]
    [InlineData("global::MyApp.SelfBox<string>", true)]
    [InlineData("global::MyApp.PairBox<object, string>", true)]
    [InlineData("global::MyApp.PairBox<object, int>", true)]
    [InlineData("global::MyApp.PairBox<string?, string?>", true)]
    [InlineData("global::MyApp.PairBox<string?, string>", true)]
    [InlineData("global::MyApp.Nest<global::MyApp.Base>.Inner<global::MyApp.Derived>", true)]
    [InlineData("global::MyApp.ValueBox<int>[]", true)]
    [InlineData("global::System.Collections.Generic.List<global::MyApp.ValueBox<int>?>", true)]
    [InlineData("global::System.Collections.Generic.List<global::System.Nullable<string>>", false)]
    [InlineData("global::MyApp.ValueBox<string>", false)]
    [InlineData("global::MyApp.ValueBox<int?>", false)]
    [InlineData("global::MyApp.ValueBox<global::System.Nullable<int>>", false)]
    [InlineData("global::MyApp.RefBox<int>", false)]
    [InlineData("global::MyApp.RefBox<string?>", false)]
    [InlineData("global::MyApp.RawBox<string>", false)]
    [InlineData("global::MyApp.RawBox<global::System.Collections.Generic.KeyValuePair<int, string>>", false)]
    [InlineData("global::MyApp.RawBox<int?>", false)]
    [InlineData("global::MyApp.KeyBox<string?>", false)]
    [InlineData("global::MyApp.KeyBox<int?>", false)]
    [InlineData("global::System.Collections.Generic.Dictionary<int?, string>", false)]
    [InlineData("global::System.Collections.Generic.Dictionary<string?, int>", false)]
    [InlineData("global::MyApp.EnumBox<global::MyApp.Color>", true)]
    [InlineData("global::System.Collections.Generic.List<global::MyApp.IMakesByDefault>", true)]
    [InlineData("global::System.Collections.Generic.List<global::MyApp.IMadeHere>", true)]
    [InlineData("global::System.Collections.Generic.List<global::MyApp.IMakes[]>", true)]
    [InlineData("global::System.Collections.Generic.List<global::System.Numerics.INumber<int>[]>", true)]
    [InlineData("global::System.Collections.Generic.Dictionary<int, string>.KeyCollection", true)]
    [InlineData("global::System.Collections.Immutable.ImmutableArray<int>.Builder", true)]
    [InlineData("global::System.Collections.Immutable.ImmutableArray<int>", true)]
    [InlineData("global::System.Collections.Generic.IEnumerable<string>", true)]
    [InlineData("global::System.Memory<byte>", true)]
    [InlineData("global::System.Collections.Generic.KeyValuePair<string, int?>", true)]
    [InlineData("global::System.Collections.Generic.List<global::System.Nullable<int>>", true)]
    [InlineData("global::MyApp.PairBox<object?, int?>", true)]
    [InlineData("global::MyApp.PairBox<global::System.ValueType?, int?>", true)]
    [InlineData("global::MyApp.EnumBox<global::MyApp.Color?>", false)]
    [InlineData("global::MyApp.PairBox<global::System.Enum, global::MyApp.Color?>", false)]
    [InlineData("global::MyApp.PairBox<object, int?>", false)]
    [InlineData("global::System.Collections.Generic.List<global::System.Numerics.INumber<int>>", false)]
    [InlineData("global::System.Collections.Generic.List<global::MyApp.IMakes>", false)]
    [InlineData("global::MyApp.NewBox<string>", false)]
    [InlineData("global::MyApp.NewBox<global::MyApp.Sketch>", false)]
    [InlineData("global::MyApp.NewBox<global::MyApp.NoDefault>", false)]
    [InlineData("global::MyApp.NewBox<global::MyApp.Form>", false)]
    [InlineData("global::MyApp.NewBox<int[]>", false)]
    [InlineData("global::MyApp.NewBox<global::MyApp.IShape>", false)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.Square>", false)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.Dot?>", false)]
    [InlineData("global::MyApp.ShapeBox<global::MyApp.Circle?>", false)]
    [InlineData("global::MyApp.BaseBox<object>", false)]
    [InlineData("global::MyApp.SelfBox<global::MyApp.UserId>", false)]
    [InlineData("global::MyApp.PairBox<string, object>", false)]
    [InlineData("global::MyApp.PairBox<string, string?>", false)]
    [InlineData("global::MyApp.Nest<global::MyApp.Derived>.Inner<global::MyApp.Base>", false)]
    [InlineData("global::System.Collections.Generic.Dictionary<string, global::MyApp.ValueBox<string>>", false)]
    [InlineData("global::MyApp.ValueBox<string>[]", false)]
    [InlineData("global::MyApp.ValueBox<int?[]>", false)]
    public void Resolve_GenericName_IsFoundWhenItsArgumentsMeetTheConstraints(string name, bool meets)
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken).Types;

        var reports = CompilerReports(name);
        if (meets)
        {
            reports.ShouldBeEmpty();
        }
        else
        {
            reports.ShouldNotBeEmpty();
        }

        types
            .ShouldHaveSingleItem()
            .Status.ShouldBe(meets ? MappedTypeStatus.Found : MappedTypeStatus.NotAPropertyType);
    }

    // Of two failures in one name, the first in the order of the statuses is the whole's: a missing argument over an
    // inaccessible one, an ambiguous argument over a static one, and an inaccessible argument over a static one or a
    // broken constraint.
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
    [InlineData(
        "global::System.Collections.Generic.Dictionary<global::Other.Hidden, global::MyApp.ValueBox<string>>",
        nameof(MappedTypeStatus.NotAccessible)
    )]
    public void Resolve_NameWithTwoFailures_GivesTheFirstInTheOrderOfTheStatuses(string name, string status)
    {
        var types = MappedTypeResolver.Resolve(Project.Value, Names(name), TestContext.Current.CancellationToken).Types;

        types.ShouldHaveSingleItem().Status.ShouldBe(Enum.Parse<MappedTypeStatus>(status));
    }

    [Fact]
    public void Resolve_SeveralNames_GivesOneRecordForEachInTheirOrder()
    {
        var names = Names("global::MyApp.Missing", "global::MyApp.UserId", "int");

        var types = MappedTypeResolver.Resolve(Project.Value, names, TestContext.Current.CancellationToken).Types;

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
    public void Resolve_NoNames_GivesEmpty()
    {
        var resolved = MappedTypeResolver.Resolve(
            Project.Value,
            EquatableArray<string>.Empty,
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldBeEmpty();
        resolved.Converters.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_TextThatIsNoType_IsNotFound()
    {
        var types = MappedTypeResolver
            .Resolve(Project.Value, Names("not a type"), TestContext.Current.CancellationToken)
            .Types;

        types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.NotFound);
    }

    [Fact]
    public void Resolve_TypeOfAReferenceWithConverters_GivesThem()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Email"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        resolved.Converters.ShouldBe(EmailConverters);
        resolved.Converters.ShouldAllBe(converter =>
            converter.Origin == ConverterOrigin.MappedType && converter.Location == null
        );
        resolved.Converters[1].Method.ShouldBe("global::Lib.Email.From");
    }

    [Fact]
    public void Resolve_ArrayOfSuchAType_GivesTheElementsConverters()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Email[]"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        resolved.Converters.ShouldBe(EmailConverters);
    }

    [Fact]
    public void Resolve_TypeNamedTwice_GivesItsConvertersOnce()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Email", "global::Lib.Email[]"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.Count.ShouldBe(2);
        resolved.Converters.ShouldBe(EmailConverters);
    }

    [Fact]
    public void Resolve_ConverterOfAReferenceWithAFault_KeepsTheFault()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Locked"),
            TestContext.Current.CancellationToken
        );

        var converter = resolved.Converters.ShouldHaveSingleItem();
        converter.Method.ShouldBe("global::Lib.Locked.From");
        converter.Fault.ShouldBe("this project cannot call it");
        converter.Origin.ShouldBe(ConverterOrigin.MappedType);
        converter.Location.ShouldBeNull();
    }

    [Fact]
    public void Resolve_OperatorOfAReference_KeepsTheFault()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Celsius"),
            TestContext.Current.CancellationToken
        );

        var converter = resolved.Converters.ShouldHaveSingleItem();
        converter.DisplayName.ShouldBe("Lib.Celsius.implicit operator Lib.Celsius(double)");
        converter.Fault.ShouldBe("it is an operator");
        converter.Origin.ShouldBe(ConverterOrigin.MappedType);
        converter.Location.ShouldBeNull();
    }

    // An internal member of a reference without InternalsVisibleTo is not in the compilation at all, as in a build.
    [Fact]
    public void Resolve_InternalConverterOfAReference_IsNotSeen()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Hidden"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        resolved.Converters.ShouldBeEmpty();
    }

    // The compiler reads the internal members of a reference only when it shows its internals to the project: one that
    // shows them to another assembly alone, as to its tests, gives the project no internal converter, and one that
    // shows them to the project gives it a converter the project can call.
    [Fact]
    public void Resolve_InternalConverterOfAReferenceWithInternalsVisibleToAnother_IsNotSeen()
    {
        var resolved = ResolveHidden("SomeOtherAssembly");

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        resolved.Converters.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_InternalConverterOfAReferenceWithInternalsVisibleToTheProject_IsAConverter()
    {
        var resolved = ResolveHidden("TestAssembly");

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        var converter = resolved.Converters.ShouldHaveSingleItem();
        converter.Method.ShouldBe("global::TestedLib.Hidden.From");
        converter.Fault.ShouldBeNull();
        converter.Origin.ShouldBe(ConverterOrigin.MappedType);
    }

    [Fact]
    public void Resolve_TypeOfTheProject_GivesNoConverters()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::MyApp.Phone"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.Found);
        resolved.Converters.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_TypeWithoutConverters_GivesNone()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Plain", "global::System.Uri", "int"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.Count.ShouldBe(3);
        resolved.Converters.ShouldBeEmpty();
    }

    // A name that fails gives no converters, though its type has some.
    [Fact]
    public void Resolve_NameThatFails_GivesNoConverters()
    {
        var resolved = MappedTypeResolver.Resolve(
            ConvertingProject.Value,
            Names("global::Lib.Conversions"),
            TestContext.Current.CancellationToken
        );

        resolved.Types.ShouldHaveSingleItem().Status.ShouldBe(MappedTypeStatus.NotAPropertyType);
        resolved.Converters.ShouldBeEmpty();
    }

    [Fact]
    public void Find_NameNotResolved_GivesNull()
    {
        var types = MappedTypeResolver
            .Resolve(Project.Value, Names("int"), TestContext.Current.CancellationToken)
            .Types;

        MappedTypeResolver.Find(types, "long").ShouldBeNull();
        MappedTypeResolver.Find(EquatableArray<MappedType>.Empty, "int").ShouldBeNull();
    }

    // The errors and warnings that the compiler gives for a property of the type, as a model declares one.
    private static string[] CompilerReports(string name)
    {
        var tree = CSharpSyntaxTree.ParseText(
            $$"""
            #nullable enable
            namespace Check;

            /// <summary>A model.</summary>
            public sealed class Model
            {
                /// <summary>A column.</summary>
                public {{name}} Value { get; init; } = default!;
            }
            """,
            GeneratorHarness.ParseOptions,
            "/app/Check.cs",
            cancellationToken: TestContext.Current.CancellationToken
        );
        var compilation = Project.Value.AddSyntaxTrees(tree);

        return
        [
            .. compilation
                .GetSemanticModel(tree)
                .GetDiagnostics(cancellationToken: TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                .Select(static diagnostic => diagnostic.ToString()),
        ];
    }

    // The two converters of Lib.Email, in the order of ConverterSet: by argument, then by return type.
    private static readonly Converter[] EmailConverters =
    [
        new(
            "global::Lib.Email.To",
            "Lib.Email.To(Lib.Email)",
            "global::Lib.Email",
            true,
            "string",
            false,
            NoWarnings,
            null,
            ConverterOrigin.MappedType,
            null
        ),
        new(
            "global::Lib.Email.From",
            "Lib.Email.From(string)",
            "string",
            false,
            "global::Lib.Email",
            true,
            NoWarnings,
            null,
            ConverterOrigin.MappedType,
            null
        ),
    ];

    // A library whose type Hidden has an internal converter, and which shows its internals to the assembly named, with
    // a project of the default assembly name, TestAssembly, that references it.
    private static ResolvedTypes ResolveHidden(string internalsVisibleTo)
    {
        var library = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "TestedLib",
            $$"""
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("{{internalsVisibleTo}}")]

            namespace TestedLib
            {
                public sealed class Hidden
                {
                    [SqlSource.SqlSourceConverter]
                    internal static Hidden From(string v) => new();
                }
            }
            """
        );
        var project = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/SqlSourceGenerateAttribute.g.cs", AttributeSource.Text)],
            references: [library]
        );
        return MappedTypeResolver.Resolve(
            project,
            Names("global::TestedLib.Hidden"),
            TestContext.Current.CancellationToken
        );
    }

    private static EquatableArray<string> Names(params string[] names) => new(ImmutableArray.Create(names));
}
