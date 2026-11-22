using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// The members of a repository that can give its connection, as the generator reads them from the compilation into its
// RepositoryShapes step.
public sealed class ConnectionCandidateReaderTests
{
    [Fact]
    public void Read_PrimaryConstructorDataSource_IsAParameterCandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("dataSource");
        candidate.Member.ShouldBe(CandidateMember.Parameter);
        candidate.Type.ShouldBe(CandidateType.DataSource);
        candidate.IsMarked.ShouldBeFalse();
        candidate.DeclaredTypeName.ShouldBe("Npgsql.NpgsqlDataSource");
    }

    [Theory]
    [InlineData("connectionString")]
    [InlineData("_connectionString")]
    [InlineData("ConnectionString")]
    public void Read_FieldNamedConnectionString_IsAConnectionStringCandidate(string name)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private readonly string {{name}} = "";
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(name);
        candidate.Member.ShouldBe(CandidateMember.Field);
        candidate.Type.ShouldBe(CandidateType.ConnectionString);
        candidate.DeclaredTypeName.ShouldBe("System.String");
    }

    [Fact]
    public void Read_FieldNotNamedByTheConvention_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private readonly string connstr = "";
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    // A nullable annotation is no part of the type's name.
    [Fact]
    public void Read_NullableDataSourceField_IsADataSourceCandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            #nullable enable
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private readonly NpgsqlDataSource? _dataSource = null;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(CandidateType.DataSource);
        candidate.DeclaredTypeName.ShouldBe("Npgsql.NpgsqlDataSource");
    }

    [Fact]
    public void Read_MarkedStringMethod_IsAConnectionString()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private string GetConnectionString() => "";
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.ConnectionString);
        candidate.IsAsync.ShouldBeFalse();
        candidate.DeclaredTypeName.ShouldBe("System.String");
    }

    [Fact]
    public void Read_MarkedSyncFactory_IsAFactoryOfItsConnectionType()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private static NpgsqlConnection Open() => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsAsync.ShouldBeFalse();
        candidate.TakesCancellationToken.ShouldBeFalse();
        candidate.IsStatic.ShouldBeTrue();
        candidate.DeclaredTypeName.ShouldBe("Npgsql.NpgsqlConnection");
    }

    [Fact]
    public void Read_MarkedTaskFactory_IsAsync()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private Task<DbConnection> OpenAsync() => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsAsync.ShouldBeTrue();
        candidate.TakesCancellationToken.ShouldBeFalse();
        candidate.DeclaredTypeName.ShouldBe("System.Data.Common.DbConnection");
    }

    [Theory]
    [InlineData("private NpgsqlConnection Open<T>() => null!;")]
    [InlineData("private NpgsqlConnection Open(ref CancellationToken cancellationToken) => null!;")]
    [InlineData("private int Open() => 0;")]
    [InlineData("private readonly int Open = 0;")]
    [InlineData("private NpgsqlConnection Open(string name, CancellationToken cancellationToken) => null!;")]
    public void Read_MarkedMemberOfAnotherShape_IsUnsupported(string member)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    {{member}}
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        candidate.IsMarked.ShouldBeTrue();
    }

    // A member the user marked is reported, so that no convention is chosen over it.
    [Fact]
    public void Read_MarkedAccessor_IsUnsupportedAndNamedForItsProperty()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource)
                {
                    private string Settings
                    {
                        [SqlSourceConnection]
                        get => "";
                    }
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Settings");
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.Unsupported);
    }

    [Fact]
    public void Read_MarkedExplicitImplementation_IsUnsupported()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IConnections
                {
                    DbConnection Open();
                }

                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource) : IConnections
                {
                    [SqlSourceConnection]
                    DbConnection IConnections.Open() => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("App.IConnections.Open");
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.Unsupported);
    }

    [Fact]
    public void Read_MarkedOverloads_AreEachACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection(Database = "users")]
                    private NpgsqlConnection Open() => null!;

                    [SqlSourceConnection(Database = "reports")]
                    private NpgsqlConnection Open(CancellationToken cancellationToken) => null!;
                }
            }
            """
        );

        shape
            .Candidates.Select(candidate => (candidate.Name, candidate.Database, candidate.TakesCancellationToken))
            .ShouldBe([("Open", "users", false), ("Open", "reports", true)]);
    }

    // A member of the repository hides the base's member of its name, which the repository's code cannot reach as it.
    [Fact]
    public void Read_BaseMemberHiddenByTheRepository_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected string ConnectionString { get; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private new int ConnectionString => 0;
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    // A call with no argument cannot bind to an overload that needs one, so the base's method is still the one called.
    [Fact]
    public void Read_BaseMethodBesideARequiredOverload_IsACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int Open(int retries) => retries;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsInherited.ShouldBeTrue();
    }

    // A member that the repository cannot reach is not the one its call binds to.
    [Fact]
    public void Read_PrivateMemberOfABaseTypeBetween_HidesNothing()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class LowestBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection Open() => null!;
                }

                public abstract class MiddleBase : LowestBase
                {
                    private new NpgsqlConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : MiddleBase;
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
    }

    // C# binds a read to the property it can reach and only then fails on its getter, so the base's is out of reach.
    [Fact]
    public void Read_PropertyWithAnUnreachableGetter_HidesTheBases()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class LowestBase
                {
                    protected string ConnectionString { get; } = "";
                }

                public abstract class MiddleBase : LowestBase
                {
                    public new string ConnectionString { private get; set; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : MiddleBase;
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    // A destructor cannot be called by its name, but the repository's own still hides the base's.
    [Fact]
    public void Read_MarkedBaseDestructorBesideTheRepositorysOwn_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    ~RepositoryBase() { }
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    ~UserRepository() { }
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MarkedAccessorOfAHiddenProperty_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected string Source { [SqlSourceConnection] get; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private new int Source { set { } }
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    // A property's accessor cannot be named, so it hides no method or value that has its name.
    [Theory]
    [InlineData("protected NpgsqlConnection get_Source() => null!;")]
    [InlineData("protected string get_Source = \"\";")]
    public void Read_BaseMemberNamedForADerivedAccessor_IsACandidate(string member)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int Source => 0;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("get_Source");
    }

    // An indexer hides an indexer of its signature alone.
    [Theory]
    [InlineData("string", 1)]
    [InlineData("int", 0)]
    public void Read_MarkedBaseIndexer_IsHiddenOnlyByItsSignature(string keyType, int candidates)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected string this[int key] => "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private new string this[{{keyType}} key] => "";
                }
            }
            """
        );

        shape
            .Candidates.Select(static candidate => candidate.Type)
            .ShouldBe(Enumerable.Repeat(CandidateType.Unsupported, candidates));
    }

    // The call of a base method with no parameters is Open(), which a more derived overload can take.
    [Theory]
    [InlineData("Open(int retries = 3)", false)]
    [InlineData("Open(params int[] retries)", false)]
    [InlineData("Open(in int retries = 3)", false)]
    [InlineData("Open(int retries)", true)]
    [InlineData("Open(int required, int retries = 3)", true)]
    [InlineData("Open<T>()", true)]
    [InlineData("Open<T>(T value = default!)", true)]
    [InlineData("Open(ref int retries)", true)]
    public void Read_BaseMethodBesideAnOverload_IsACandidateUnlessItTakesTheCall(string overload, bool isCandidate)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int {{overload}} => 0;
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(isCandidate ? 1 : 0);
    }

    // A synchronous factory is called with the method's token, or a typed default, which bind alike: to a parameter
    // that takes a CancellationToken, and never to one that only an untyped default could reach.
    [Theory]
    [InlineData("Open(object value)", false)]
    [InlineData("Open<T>(T value)", false)]
    [InlineData("Open<T>(System.IEquatable<T> value)", false)]
    [InlineData("Open(in CancellationToken token)", false)]
    [InlineData("Open(ref readonly CancellationToken token)", false)]
    [InlineData("Open(CancellationToken? token)", false)]
    [InlineData("Open(int value)", true)]
    [InlineData("Open(params int[] values)", true)]
    [InlineData("Open(string value, int retries = 3)", true)]
    [InlineData("Open(ref CancellationToken token)", true)]
    [InlineData("Open(int value, int retries)", true)]
    [InlineData("Open<T>(System.Collections.Generic.List<T> values)", true)]
    [InlineData("Open<T>(T? value) where T : struct", true)]
    [InlineData("Open<T, U>(T value)", true)]
    public void Read_SyncTokenMethodBesideAnOverload_IsACandidateUnlessItTakesTheCall(string overload, bool isCandidate)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection Open(CancellationToken cancellationToken) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int {{overload}} => 0;
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(isCandidate ? 1 : 0);
    }

    // An asynchronous factory is only ever called with the method's token, which no int parameter can take.
    [Theory]
    [InlineData("Open(object value)", false)]
    [InlineData("Open<T>(T value)", false)]
    [InlineData("Open(params CancellationToken[] tokens)", false)]
    [InlineData("Open<T>(params T[] values)", false)]
    [InlineData("Open(params System.Collections.Generic.IEnumerable<CancellationToken> tokens)", false)]
    [InlineData("Open(params System.ReadOnlySpan<CancellationToken> tokens)", false)]
    [InlineData("Open(int value)", true)]
    [InlineData("Open(params int[] values)", true)]
    [InlineData("Open(params System.Collections.Generic.List<int> values)", true)]
    [InlineData("Open(params System.ReadOnlySpan<int> values)", true)]
    [InlineData("Open(ExplicitFromToken value)", true)]
    [InlineData("Open(params UntypedBag values)", false)]
    public void Read_AsyncTokenMethodBesideAnOverload_IsACandidateUnlessItTakesTheCall(
        string overload,
        bool isCandidate
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public struct ExplicitFromToken
                {
                    public static explicit operator ExplicitFromToken(CancellationToken token) => default;
                }

                public sealed class UntypedBag : System.Collections.IEnumerable
                {
                    public void Add(object value) { }

                    public System.Collections.IEnumerator GetEnumerator() => null!;
                }

                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected Task<NpgsqlConnection> Open(CancellationToken cancellationToken) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int {{overload}} => 0;
                }
            }
            """,
            languageVersion: LanguageVersion.CSharp13
        );

        shape.Candidates.Count.ShouldBe(isCandidate ? 1 : 0);
    }

    // The build writes no call of a destructor or a generic method, so no overload takes one: each is still reported.
    [Theory]
    [InlineData("~RepositoryBase() { }", "private int Finalize(int retries = 3) => retries;")]
    [InlineData("protected NpgsqlConnection Open<T>() => null!;", "private int Open(int retries = 3) => retries;")]
    [InlineData("protected NpgsqlConnection Open(int value) => null!;", "private int Open(object value) => 0;")]
    public void Read_MarkedMethodThatIsNeverCalled_IsNotHiddenByAnOverload(string marked, string overload)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    {{marked}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    {{overload}}
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Type.ShouldBe(CandidateType.Unsupported);
    }

    // Signatures are compared by their parts: an array's rank, a generic type's arguments and the type it is nested in.
    [Theory]
    [InlineData("Open(int[] values)", "private int Open(int[,] values) => 0;", 1)]
    [InlineData("Open<T>(List<T> values)", "private new int Open<T>(List<T> values) => 0;", 0)]
    [InlineData("Open<T>(Dictionary<T, int> values)", "private int Open<T>(Dictionary<T, string> values) => 0;", 1)]
    [InlineData("Open<T>(Outer<T>.Inner value)", "private new int Open<T>(Outer<T>.Inner value) => 0;", 0)]
    [InlineData("Open<T>(Outer<T>.Inner value)", "private int Open<T>(Outer<T[]>.Inner value) => 0;", 1)]
    public void Read_MarkedMethodBesideAnOverloadOfACompositeType_IsHiddenOnlyByItsSignature(
        string marked,
        string overload,
        int candidates
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                using System.Collections.Generic;

                public sealed class Outer<T>
                {
                    public sealed class Inner;
                }

                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection {{marked}} => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    {{overload}}
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(candidates);
    }

    // An override of a property replaces only the accessors that it declares, so a marked one it leaves is still read.
    [Theory]
    [InlineData("{ [SqlSourceConnection] get; set; }", "{ set { } }", true)]
    [InlineData("{ get; [SqlSourceConnection] set; }", "{ get => \"\"; }", true)]
    [InlineData("{ [SqlSourceConnection] get; set; }", "{ get => \"\"; }", false)]
    public void Read_MarkedAccessorBesideAnOverrideOfItsProperty_IsOneUnsupportedCandidate(
        string accessors,
        string overrideAccessors,
        bool isInherited
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected virtual string Source {{accessors}} = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    protected override string Source {{overrideAccessors}}
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        candidate.IsInherited.ShouldBe(isInherited);
    }

    // C# leaves an override out of overload resolution and looks at the virtual it overrides, so an override hides a
    // marked method only when that virtual is declared in a type derived from the marked method's.
    [Theory]
    [InlineData(
        "public abstract class RepositoryBase { [SqlSourceConnection] protected NpgsqlConnection Open() => null!; "
            + "protected virtual int Open(int retries = 3) => 0; }",
        "protected override int Open(int retries = 3) => 0;",
        1
    )]
    [InlineData(
        "public abstract class Lowest { protected virtual int Open(int retries = 3) => 0; }\n"
            + "public abstract class RepositoryBase : Lowest "
            + "{ [SqlSourceConnection] protected NpgsqlConnection Open() => null!; }",
        "protected override int Open(int retries = 3) => 0;",
        1
    )]
    [InlineData(
        "public abstract class RepositoryBase { [SqlSourceConnection] protected NpgsqlConnection Open() => null!; "
            + "protected virtual int Open(params int[] retries) => 0; }",
        "protected override int Open(params int[] retries) => 0;",
        1
    )]
    [InlineData(
        "public abstract class RepositoryBase { [SqlSourceConnection] "
            + "protected Task<NpgsqlConnection> Open(CancellationToken cancellationToken) => null!; "
            + "protected virtual int Open(object value) => 0; }",
        "protected override int Open(object value) => 0;",
        1
    )]
    [InlineData(
        "public abstract class RepositoryBase { [SqlSourceConnection] "
            + "protected NpgsqlConnection Open(CancellationToken cancellationToken) => null!; "
            + "protected virtual int Open(string value) => 0; }",
        "protected override int Open(string value) => 0;",
        1
    )]
    [InlineData(
        "public abstract class Lowest { [SqlSourceConnection] protected NpgsqlConnection Open() => null!; }\n"
            + "public abstract class RepositoryBase : Lowest { protected virtual int Open(int retries = 3) => 0; }",
        "protected override int Open(int retries = 3) => 0;",
        0
    )]
    public void Read_MarkedMethodBesideAnOverride_IsHiddenOnlyByAVirtualBelowIt(
        string baseTypes,
        string repositoryMember,
        int candidates
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                {{baseTypes}}

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    {{repositoryMember}}
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(candidates);
    }

    // A call passes over a member that cannot be invoked, and stops at one that can.
    [Theory]
    [InlineData("private new int Open = 0;", true)]
    [InlineData("private new int Open => 0;", true)]
    [InlineData("private new class Open { }", true)]
    [InlineData("private new System.Func<NpgsqlConnection> Open = null!;", false)]
    [InlineData("private new System.Func<NpgsqlConnection> Open => null!;", false)]
    [InlineData("private new dynamic Open = null!;", false)]
    [InlineData("private new event System.Func<NpgsqlConnection> Open = null!;", false)]
    public void Read_BaseMethodBesideAMemberThatIsNoMethod_IsACandidateUnlessItIsInvocable(
        string member,
        bool isCandidate
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected NpgsqlConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    {{member}}
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(isCandidate ? 1 : 0);
    }

    // The read names no type arguments, so a generic nested type is not what it binds to.
    [Theory]
    [InlineData("private class ConnectionString<T> { }", true)]
    [InlineData("private new class ConnectionString { }", false)]
    public void Read_BaseValueBesideANestedType_IsACandidateUnlessTheTypeIsNotGeneric(string member, bool isCandidate)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected string ConnectionString { get; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    {{member}}
                }
            }
            """
        );

        shape.Candidates.Count.ShouldBe(isCandidate ? 1 : 0);
    }

    [Fact]
    public void Read_OverrideOfAMarkedBaseMethod_IsMarkedWithTheBasesDatabase()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection(Database = "users")]
                    protected abstract ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken);
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    protected override ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
                        default;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("OpenAsync");
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsMarked.ShouldBeTrue();
        candidate.IsInherited.ShouldBeFalse();
        candidate.Database.ShouldBe("users");
    }

    [Fact]
    public void Read_OverrideOfAMarkedBaseProperty_IsMarked()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected abstract NpgsqlDataSource Source { get; }
                }

                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource) : RepositoryBase
                {
                    private readonly string _connectionString = "";

                    protected override NpgsqlDataSource Source => dataSource;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Source");
        candidate.Member.ShouldBe(CandidateMember.Property);
        candidate.IsMarked.ShouldBeTrue();
        candidate.IsInherited.ShouldBeFalse();
    }

    // The attribute stays in the metadata of a library that a repository's base type comes from.
    [Fact]
    public void Read_MarkedProtectedMemberOfAReferencedBase_IsInherited()
    {
        var library = RepositoryShapes.Library(
            """
            namespace Library
            {
                public abstract class RepositoryBase
                {
                    [SqlSource.SqlSourceConnection(Database = "users")]
                    protected Npgsql.NpgsqlDataSource Source { get; } = null!;
                }
            }
            """
        );

        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository : Library.RepositoryBase
                {
                    private readonly string _connectionString = "";
                }
            }
            """,
            references: [library]
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Source");
        candidate.IsMarked.ShouldBeTrue();
        candidate.IsInherited.ShouldBeTrue();
        candidate.Database.ShouldBe("users");
        candidate.DeclaredTypeName.ShouldBe("Npgsql.NpgsqlDataSource");
        candidate.Location.Path.ShouldBe(GeneratorHarness.SourcePath);
    }

    // The common shape of dependency injection: the field the parameter is stored in is the source, so the generated
    // code never captures the parameter as well.
    [Fact]
    public void Read_ParameterStoredInAField_KeepsTheFieldAlone()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource)
                {
                    private readonly NpgsqlDataSource _dataSource = dataSource;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("_dataSource");
        candidate.Member.ShouldBe(CandidateMember.Field);
        shape.HeldParameters.ShouldBeEmpty();
    }

    // A method that captured a parameter that the type also passes to its base type, or that an initializer reads,
    // would make the compiler warn in the user's code (CS9107, CS9124), so the conventions leave it alone, in every
    // part of the type.
    [Theory]
    [InlineData(
        "public partial class UserRepository(string connectionString) : Base(connectionString);\n"
            + "public abstract class Base\n{\n    protected Base(string value) { }\n}"
    )]
    [InlineData(
        "public partial class UserRepository(string connectionString)\n{\n"
            + "    private readonly string _value = connectionString;\n}"
    )]
    [InlineData(
        "public partial class UserRepository(string connectionString)\n{\n"
            + "    private int Length { get; } = connectionString.Length;\n}"
    )]
    [InlineData(
        "public partial class UserRepository(string connectionString)\n{\n"
            + "    private event System.Func<string>? Read = () => connectionString;\n}"
    )]
    [InlineData(
        "public partial class UserRepository(string connectionString);\n"
            + "public partial class UserRepository\n{\n    private readonly string _value = connectionString;\n}"
    )]
    public void Read_ParameterPassedOnOrStored_IsHeld(string declarations)
    {
        var shape = RepositoryShapes.Of("namespace App\n{\n[SqlSourceGenerate]\n" + declarations + "\n}\n");

        shape.Candidates.ShouldBeEmpty();
        shape.HeldParameters.ShouldBe(["connectionString"]);
    }

    // Only the parameter itself counts: one that a method alone reads, or a member of its name that an initializer
    // reads, leaves it a candidate.
    [Theory]
    [InlineData(
        "public partial class UserRepository(string connectionString)\n{\n"
            + "    public int Length() => connectionString.Length;\n}"
    )]
    [InlineData(
        "public partial class UserRepository(string connectionString)\n{\n"
            + "    private readonly int _length = Other.connectionString.Length;\n}\n"
            + "public static class Other\n{\n    public static string connectionString = \"\";\n}"
    )]
    public void Read_ParameterNotPassedOnOrStored_IsACandidate(string declarations)
    {
        var shape = RepositoryShapes.Of("namespace App\n{\n[SqlSourceGenerate]\n" + declarations + "\n}\n");

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("connectionString");
        shape.HeldParameters.ShouldBeEmpty();
    }

    // A marked parameter is the user's choice, so it is taken though the type passes it on.
    [Fact]
    public void Read_MarkedParameterPassedOn_IsACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository([SqlSourceConnection] string connectionString)
                    : Base(connectionString);

                public abstract class Base
                {
                    protected Base(string value) { }
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().IsMarked.ShouldBeTrue();
        shape.HeldParameters.ShouldBeEmpty();
    }

    [Fact]
    public void Read_RecordPositionalString_IsItsPropertyAlone()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial record UserRepository(string ConnectionString);
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("ConnectionString");
        candidate.Member.ShouldBe(CandidateMember.Property);
        candidate.Type.ShouldBe(CandidateType.ConnectionString);
    }

    [Fact]
    public void Read_MarkedFactory_IsAsyncAndTakesTheToken()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private ValueTask<NpgsqlConnection> Open(CancellationToken cancellationToken) => default;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsAsync.ShouldBeTrue();
        candidate.TakesCancellationToken.ShouldBeTrue();
        candidate.IsMarked.ShouldBeTrue();
        candidate.DeclaredTypeName.ShouldBe("Npgsql.NpgsqlConnection");
    }

    [Fact]
    public void Read_MarkedDbConnectionProperty_IsAConnection()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    public static DbConnection Connection { get; set; } = null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Member.ShouldBe(CandidateMember.Property);
        candidate.Type.ShouldBe(CandidateType.Connection);
        candidate.IsStatic.ShouldBeTrue();
        candidate.DeclaredTypeName.ShouldBe("System.Data.Common.DbConnection");
    }

    [Fact]
    public void Read_UnmarkedDbConnection_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlConnection connection)
                {
                    private readonly NpgsqlConnection _connection = connection;
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MarkedMemberWithDatabase_CarriesIt()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection(Database = "reports")]
                    private readonly NpgsqlDataSource _reports = null!;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Database.ShouldBe("reports");
    }

    [Fact]
    public void Read_ProtectedBaseMember_IsInherited()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase(string connectionString)
                {
                    protected string ConnectionString { get; } = connectionString;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(string connectionString) : RepositoryBase(connectionString);
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("ConnectionString");
        candidate.Member.ShouldBe(CandidateMember.Property);
        candidate.IsInherited.ShouldBeTrue();
    }

    [Fact]
    public void Read_PrivateBaseMember_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    private readonly NpgsqlDataSource _dataSource = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase;
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_BasePropertyWithAPrivateGetter_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected string ConnectionString { private get; set; } = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase;
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_AnyMarked_DropsConventionCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource)
                {
                    private readonly string _connectionString = "";

                    [SqlSourceConnection]
                    private NpgsqlConnection Open() => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsAsync.ShouldBeFalse();
        candidate.TakesCancellationToken.ShouldBeFalse();
    }
}
