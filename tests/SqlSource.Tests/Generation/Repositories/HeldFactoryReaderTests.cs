using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generation.Repositories;

// The marked members of the factories that a repository holds, as the generator reads them from the compilation into
// its RepositoryShapes step.
public sealed class HeldFactoryReaderTests
{
    [Fact]
    public void Read_InterfaceMethodMarked_IsAHeldFactory()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsAsync.ShouldBeTrue();
        candidate.TakesCancellationToken.ShouldBeTrue();
        candidate.IsMarked.ShouldBeTrue();
        candidate.Tier.ShouldBe(CandidateTier.Held);
        var holder = candidate.Holder.ShouldNotBeNull();
        holder.Name.ShouldBe("dbf");
        holder.Member.ShouldBe(CandidateMember.Parameter);
        holder.TypeName.ShouldBe("IDatabaseFactory");
        shape.Candidates.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("string ConnectionString { get; }", "ConnectionString", "ConnectionString")]
    [InlineData("NpgsqlDataSource Source { get; }", "Source", "DataSource")]
    [InlineData("DbConnection Connection { get; }", "Connection", "Connection")]
    [InlineData("DbConnection Open();", "Open", "Factory")]
    public void Read_EachKindOfMember_IsRead(string member, string name, string type)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(name);
        candidate.Type.ShouldBe(Enum.Parse<CandidateType>(type));
    }

    [Fact]
    public void Read_MarkOnBaseInterface_IsFound()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IConnectionOpener
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public interface IDatabaseFactory : IConnectionOpener;

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Holder.ShouldNotBeNull().Name.ShouldBe("dbf");
    }

    // An implementation of a marked interface member is marked as it is, as an override of a marked member is, and the
    // two are one candidate.
    [Fact]
    public void Read_ClassImplementingMarkedInterface_IsMarked()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection(Database = "audit")]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    public Task<DbConnection> Open(CancellationToken ct) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
        candidate.Database.ShouldBe("audit");
        candidate.CastTypeText.ShouldBeNull();
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("DatabaseFactory");
    }

    // As with a source, the field the parameter is stored in is the holder, so the generated code never captures the
    // parameter as well.
    [Fact]
    public void Read_ParameterStoredInField_HoldsTheFieldAlone()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf)
                {
                    private readonly IDatabaseFactory _dbf = dbf;
                }
            }
            """
        );

        var holder = shape.HeldCandidates.ShouldHaveSingleItem().Holder.ShouldNotBeNull();
        holder.Name.ShouldBe("_dbf");
        holder.Member.ShouldBe(CandidateMember.Field);
    }

    [Fact]
    public void Read_ParameterPassedToBase_HoldsTheBaseMember()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public abstract class RepositoryBase(IDatabaseFactory factory)
                {
                    protected IDatabaseFactory Factory { get; } = factory;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf) : RepositoryBase(dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.IsInherited.ShouldBeTrue();
        var holder = candidate.Holder.ShouldNotBeNull();
        holder.Name.ShouldBe("Factory");
        holder.Member.ShouldBe(CandidateMember.Property);
    }

    // A field that holds a factory leaves the parameters out, even one of another factory that nothing else reads.
    [Fact]
    public void Read_FieldHolder_HidesAParameterHolder()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public interface IAuditFactory
                {
                    [SqlSourceConnection(Database = "audit")]
                    Task<DbConnection> OpenAudit(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IAuditFactory audit)
                {
                    private readonly IDatabaseFactory _dbf = null!;
                }
            }
            """
        );

        shape.HeldCandidates.Select(static candidate => candidate.DisplayName).ShouldBe(["_dbf.Open"]);
    }

    // A parameter that the type passes to its base type, or reads in an initializer, is not a holder, even when no
    // member that keeps it is one: the base type's is out of the type's reach, and an object holds no factory.
    [Theory]
    [InlineData("UserRepository(IDatabaseFactory dbf) : RepositoryBase(dbf);")]
    [InlineData(
        "UserRepository(IDatabaseFactory dbf) : RepositoryBase(null!) { private readonly object _kept = dbf; }"
    )]
    public void Read_ParameterReadBesides_IsNotAHolder(string declaration)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public abstract class RepositoryBase(IDatabaseFactory factory)
                {
                    private readonly IDatabaseFactory _factory = factory;
                }

                [SqlSourceGenerate]
                public partial class {{declaration}}
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // Only a member whose type has candidates is a holder, so a field of another service leaves the parameters to be
    // holders.
    [Fact]
    public void Read_FieldOfATypeWithoutMarks_DoesNotHideTheParameter()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface ILogger { }

                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf, ILogger logger)
                {
                    private readonly ILogger _logger = logger;
                }
            }
            """
        );

        shape.HeldCandidates.ShouldHaveSingleItem().Holder.ShouldNotBeNull().Name.ShouldBe("dbf");
    }

    [Fact]
    public void Read_TwoHolders_AreTwoCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private readonly IDatabaseFactory _main = null!;

                    private readonly IDatabaseFactory _audit = null!;
                }
            }
            """
        );

        shape.HeldCandidates.Select(static candidate => candidate.DisplayName).ShouldBe(["_main.Open", "_audit.Open"]);
    }

    // A static member of a factory type is a global, so it is never found twice.
    [Fact]
    public void Read_StaticFactoryMember_IsNotHeld()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public sealed class DatabaseFactory
                {
                    [SqlSourceConnection]
                    public static NpgsqlDataSource Source { get; } = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // A marked member is a source of the repository's own, never a holder.
    [Theory]
    [InlineData("NpgsqlDataSource")]
    [InlineData("IDatabaseFactory")]
    public void Read_MarkedHolder_IsNotAHolder(string type)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private readonly {{type}} _source = null!;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("_source");
        shape.HeldCandidates.ShouldBeEmpty();
    }

    // The attribute stays in the metadata of a library that a factory comes from.
    [Fact]
    public void Read_FactoryInReferencedAssembly_IsFound()
    {
        var library = RepositoryShapes.Library(
            """
            namespace Library
            {
                public interface IDatabaseFactory
                {
                    [SqlSource.SqlSourceConnection(Database = "users")]
                    System.Threading.Tasks.Task<System.Data.Common.DbConnection> Open(
                        System.Threading.CancellationToken ct
                    );
                }
            }
            """
        );

        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(Library.IDatabaseFactory dbf);
            }
            """,
            references: [library]
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Database.ShouldBe("users");
        candidate.DeclaredTypeName.ShouldBe("System.Data.Common.DbConnection");
        candidate.Location.ShouldBe(candidate.Holder.ShouldNotBeNull().Location);
    }

    [Fact]
    public void Read_UnreachableMember_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public sealed class DatabaseFactory
                {
                    [SqlSourceConnection]
                    private NpgsqlDataSource Source { get; } = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_UnsupportedMember_IsAnUnsupportedCandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open(int retries);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Type.ShouldBe(CandidateType.Unsupported);
    }

    // Only a factory's members follow the marks of the interfaces they implement: the repository's own keep the rule of
    // its own tier.
    [Fact]
    public void Read_RepositoryImplementingMarkedInterface_GainsNoMark()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IConnections
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                [SqlSourceGenerate]
                public partial class UserRepository : IConnections
                {
                    private readonly string connectionString = "";

                    public DbConnection Open() => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("connectionString");
        candidate.IsMarked.ShouldBeFalse();
    }

    // A method hides only a method of its signature, so a sync factory and an async one of one name are a pair.
    [Fact]
    public void Read_SyncAndAsyncAcrossInterfaces_AreBothCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IConnectionOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public interface IDatabaseFactory : IConnectionOpener
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.Select(static candidate => candidate.IsAsync).ShouldBe([true, false]);
    }

    // A member the repository cannot reach is not the one its call would bind to, so it hides nothing.
    [Fact]
    public void Read_UnreachableDerivedMember_HidesNothing()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Open() => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    private new DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
    }

    [Fact]
    public void Read_BaseHolderBehindAPrivateMember_IsAHolder()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public abstract class LowestBase
                {
                    protected IDatabaseFactory Connections = null!;
                }

                public abstract class MiddleBase : LowestBase
                {
                    private new int Connections => 0;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : MiddleBase;
            }
            """
        );

        shape.HeldCandidates.ShouldHaveSingleItem().Holder.ShouldNotBeNull().Name.ShouldBe("Connections");
    }

    // A read of the name binds to the method, so the base's field is out of reach.
    [Fact]
    public void Read_BaseHolderHiddenByAMethod_IsNotAHolder()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public abstract class RepositoryBase
                {
                    protected IDatabaseFactory Connections = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private int Connections() => 0;
                }
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // C# binds a read to the property it can reach and only then fails on its getter, so the base's is out of reach.
    [Fact]
    public void Read_FactoryPropertyWithAnUnreachableGetter_HidesTheBases()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Connection => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public new DbConnection Connection { private get; set; } = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // The generated call binds to a factory's overload that can take it, so the base's method is out of reach.
    [Fact]
    public void Read_FactoryMethodBesideAnOverloadThatTakesTheCall_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Open() => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public int Open(int retries = 3) => retries;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // An overload that takes the call hides an implementation as a new member does, so the interface still reaches it.
    [Fact]
    public void Read_ImplementationThatAnOverloadTakesTheCallOf_IsReachedThroughTheInterface()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public class FactoryBase : IDatabaseFactory
                {
                    public DbConnection Open() => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public int Open(int retries = 3) => retries;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldHaveSingleItem().CastTypeText.ShouldBe("global::App.IDatabaseFactory");
    }

    // A call of an interface's default method passes over a class member that cannot be invoked.
    [Fact]
    public void Read_DefaultMemberBesideANonInvocableClassMember_IsCalledDirectly()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open() => null!;
                }

                public class FactoryBase : IDatabaseFactory
                {
                    public string Open => "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : FactoryBase, IDatabaseFactory;
            }
            """
        );

        shape.HeldCandidates.ShouldHaveSingleItem().CastTypeText.ShouldBeNull();
    }

    // A type parameter's class constraint hides an interface constraint's member from the call that it takes.
    [Theory]
    [InlineData(
        "[SqlSourceConnection] DbConnection Open();",
        "[SqlSourceConnection] public DbConnection Open() => null!;"
    )]
    [InlineData(
        "[SqlSourceConnection] DbConnection Open();",
        "[SqlSourceConnection] public DbConnection Open(int retries = 3) => null!;"
    )]
    [InlineData(
        "[SqlSourceConnection] DbConnection Open { get; }",
        "[SqlSourceConnection] public DbConnection Open() => null!;"
    )]
    [InlineData(
        "[SqlSourceConnection] DbConnection Open();",
        "[SqlSourceConnection] public System.Func<DbConnection> Open => null!;"
    )]
    public void Read_ClassConstraintsMemberThatTakesTheCall_HidesTheInterfaces(
        string interfaceMember,
        string classMember
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    {{interfaceMember}}
                }

                public class FactoryBase
                {
                    {{classMember}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : FactoryBase, IDatabaseFactory;
            }
            """
        );

        shape.HeldCandidates.ShouldHaveSingleItem().CastTypeText.ShouldBeNull();
    }

    // A class method that cannot take the call, or a value the call cannot invoke, leaves the interface's method.
    [Theory]
    [InlineData("[SqlSourceConnection] public DbConnection Open(int required) => null!;")]
    [InlineData("[SqlSourceConnection] public DbConnection Open => null!;")]
    public void Read_ClassConstraintsMemberThatCannotTakeTheCall_LeavesTheInterfaces(string classMember)
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public class FactoryBase
                {
                    {{classMember}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : FactoryBase, IDatabaseFactory;
            }
            """
        );

        shape.HeldCandidates.Count.ShouldBe(2);
    }

    [Fact]
    public void Read_NullableStructHolder_ReadsItsValuesType()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public struct DatabaseFactory
                {
                    [SqlSourceConnection]
                    public DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory? dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("DatabaseFactory");
    }

    // A holder of a struct, or of a type parameter constrained to one, can never be null; a Nullable<T> of a struct, a
    // class, an interface and a type parameter that may be a class can.
    [Theory]
    [InlineData("UserRepository(DatabaseFactory dbf)", false)]
    [InlineData("UserRepository<T>(T dbf) where T : struct, IDatabaseFactory", false)]
    [InlineData("UserRepository(DatabaseFactory? dbf)", true)]
    [InlineData("UserRepository(IDatabaseFactory dbf)", true)]
    [InlineData("UserRepository<T>(T dbf) where T : IDatabaseFactory", true)]
    public void Read_Holder_SaysWhetherItCanBeNull(string declaration, bool canBeNull)
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public struct DatabaseFactory : IDatabaseFactory
                {
                    public DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class
            """
                + " "
                + declaration
                + ";\n}\n"
        );

        shape.HeldCandidates.ShouldHaveSingleItem().Holder.ShouldNotBeNull().CanBeNull.ShouldBe(canBeNull);
    }

    [Fact]
    public void Read_TypeParameterHolder_ReadsItsConstraint()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : IDatabaseFactory;
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("TFactory");
    }

    // The implementation that a factory inherits from its base class and the interface member it implements are one
    // candidate, marked as the interface member is.
    [Fact]
    public void Read_ImplementationInheritedFromTheFactorysBase_IsFoundOnce()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public class FactoryBase
                {
                    public Task<DbConnection> Open(CancellationToken ct) => null!;
                }

                public sealed class DatabaseFactory : FactoryBase, IDatabaseFactory;

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
    }

    // A call through a holder of the class cannot name an explicit implementation, so the candidate is the interface's
    // member, marked as it is and reached through the interface, located at the implementation.
    [Fact]
    public void Read_ExplicitImplementationOnAClassHolder_IsReachedThroughTheInterface()
    {
        const string Source = """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection(Database = "audit")]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    Task<DbConnection> IDatabaseFactory.Open(CancellationToken ct) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """;

        var shape = RepositoryShapes.Of(Source);

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsAsync.ShouldBeTrue();
        candidate.TakesCancellationToken.ShouldBeTrue();
        candidate.IsMarked.ShouldBeTrue();
        candidate.Database.ShouldBe("audit");
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("DatabaseFactory");
        candidate.Location.LineSpan.Start.Line.ShouldBe(RepositoryShapes.LineOf(Source, "IDatabaseFactory.Open("));
    }

    // Each kind of member that a factory implements explicitly is read as the interface's member is.
    [Theory]
    [InlineData(
        "string ConnectionString { get; }",
        "string IDatabaseFactory.ConnectionString => \"\";",
        "ConnectionString"
    )]
    [InlineData("string Connect();", "string IDatabaseFactory.Connect() => \"\";", "ConnectionString")]
    [InlineData("NpgsqlDataSource Source { get; }", "NpgsqlDataSource IDatabaseFactory.Source => null!;", "DataSource")]
    [InlineData("DbConnection Connection { get; }", "DbConnection IDatabaseFactory.Connection => null!;", "Connection")]
    [InlineData("DbConnection Open();", "DbConnection IDatabaseFactory.Open() => null!;", "Factory")]
    public void Read_ExplicitImplementationOfEachKind_IsReachedThroughTheInterface(
        string member,
        string implementation,
        string type
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    {{implementation}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(Enum.Parse<CandidateType>(type));
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
    }

    // The interface is written in full from global::, as the method's code can name it: closed over the factory's type
    // arguments, with a nullable annotation's ?, a keyword's @ and the types it is nested in.  A message names it by
    // its own name and its type arguments, without a keyword's @.  A member that the class cannot be called by is
    // reached so too: one its base class implements explicitly, one a derived interface implements explicitly, and an
    // interface's default that the class does not implement.
    [Theory]
    [InlineData(
        "public interface IFactory<TConnection> { [SqlSourceConnection] TConnection Open(); }\n"
            + "public sealed class DatabaseFactory : IFactory<NpgsqlConnection>"
            + " { NpgsqlConnection IFactory<NpgsqlConnection>.Open() => null!; }",
        "DatabaseFactory",
        "global::App.IFactory<global::Npgsql.NpgsqlConnection>",
        "IFactory<NpgsqlConnection>"
    )]
    [InlineData(
        "public interface IFactory<TConnection> { [SqlSourceConnection] TConnection Open(); }\n"
            + "public sealed class DatabaseFactory<TConnection> : IFactory<TConnection>"
            + " { TConnection IFactory<TConnection>.Open() => default!; }",
        "DatabaseFactory<NpgsqlConnection>",
        "global::App.IFactory<global::Npgsql.NpgsqlConnection>",
        "IFactory<NpgsqlConnection>"
    )]
    [InlineData(
        "#nullable enable\npublic interface IFactory<TConnection> { [SqlSourceConnection] TConnection Open(); }\n"
            + "public sealed class DatabaseFactory : IFactory<NpgsqlConnection?>"
            + " { NpgsqlConnection? IFactory<NpgsqlConnection?>.Open() => null; }",
        "DatabaseFactory",
        "global::App.IFactory<global::Npgsql.NpgsqlConnection?>",
        "IFactory<NpgsqlConnection?>"
    )]
    [InlineData(
        "public interface @event { [SqlSourceConnection] DbConnection Open(); }\n"
            + "public sealed class DatabaseFactory : @event { DbConnection @event.Open() => null!; }",
        "DatabaseFactory",
        "global::App.@event",
        "event"
    )]
    [InlineData(
        "public static class Outer { public interface IFactory { [SqlSourceConnection] DbConnection Open(); } }\n"
            + "public sealed class DatabaseFactory : Outer.IFactory { DbConnection Outer.IFactory.Open() => null!; }",
        "DatabaseFactory",
        "global::App.Outer.IFactory",
        "IFactory"
    )]
    [InlineData(
        "public interface IDatabaseFactory { [SqlSourceConnection] DbConnection Open(); }\n"
            + "public class FactoryBase : IDatabaseFactory { DbConnection IDatabaseFactory.Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase;",
        "DatabaseFactory",
        "global::App.IDatabaseFactory",
        "IDatabaseFactory"
    )]
    [InlineData(
        "public interface IOpener { [SqlSourceConnection] DbConnection Open(); }\n"
            + "public interface IDatabaseFactory : IOpener { DbConnection IOpener.Open() => null!; }\n"
            + "public sealed class DatabaseFactory : IDatabaseFactory;",
        "DatabaseFactory",
        "global::App.IOpener",
        "IOpener"
    )]
    [InlineData(
        "public interface IDatabaseFactory { [SqlSourceConnection] DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : IDatabaseFactory;",
        "DatabaseFactory",
        "global::App.IDatabaseFactory",
        "IDatabaseFactory"
    )]
    [InlineData(
        "public sealed class @class;\n"
            + "public interface IFactory<T> { [SqlSourceConnection] DbConnection Open(); }\n"
            + "public sealed class DatabaseFactory : IFactory<@class>"
            + " { DbConnection IFactory<@class>.Open() => null!; }",
        "DatabaseFactory",
        "global::App.IFactory<global::App.@class>",
        "IFactory<class>"
    )]
    public void Read_MemberTheClassCannotBeCalledBy_IsCastToTheInterfaceInFull(
        string declarations,
        string holderType,
        string cast,
        string castName
    )
    {
        var shape = RepositoryShapes.Of(
            "namespace App\n{\n"
                + declarations
                + "\n\n[SqlSourceGenerate]\npublic partial class UserRepository("
                + holderType
                + " dbf);\n}\n"
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
        candidate.CastTypeText.ShouldBe(cast);
        candidate.CastTypeName.ShouldBe(castName);
        candidate.DisplayName.ShouldBe("dbf." + castName + ".Open");
    }

    // Every holder of the class reaches an explicit implementation through the interface: a struct, which cannot be
    // null, a Nullable<T> of one, and a type parameter constrained to the class, alone or with the interface, which
    // gives the one candidate.  A holder of the interface, or of a type parameter that only the interface constrains,
    // calls the interface's member directly.
    [Theory]
    [InlineData("UserRepository(DatabaseFactory dbf)", "global::App.IDatabaseFactory", true)]
    [InlineData("UserRepository(StructFactory dbf)", "global::App.IDatabaseFactory", false)]
    [InlineData("UserRepository(StructFactory? dbf)", "global::App.IDatabaseFactory", true)]
    [InlineData("UserRepository<T>(T dbf) where T : DatabaseFactory", "global::App.IDatabaseFactory", true)]
    [InlineData(
        "UserRepository<T>(T dbf) where T : DatabaseFactory, IDatabaseFactory",
        "global::App.IDatabaseFactory",
        true
    )]
    [InlineData("UserRepository<T>(T dbf) where T : struct, IDatabaseFactory", null, false)]
    [InlineData("UserRepository(IDatabaseFactory dbf)", null, true)]
    public void Read_ExplicitImplementation_IsCastFromEachHolderOfItsClass(
        string declaration,
        string? cast,
        bool canBeNull
    )
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public class DatabaseFactory : IDatabaseFactory
                {
                    DbConnection IDatabaseFactory.Open() => null!;
                }

                public struct StructFactory : IDatabaseFactory
                {
                    readonly DbConnection IDatabaseFactory.Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class
            """
                + " "
                + declaration
                + ";\n}\n"
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBe(cast);
        candidate.Holder.ShouldNotBeNull().CanBeNull.ShouldBe(canBeNull);
    }

    // A class that implements again, implicitly, an interface its base class implements explicitly is called directly.
    [Fact]
    public void Read_ImplicitImplementationOverAnExplicitBase_IsCalledDirectly()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public class FactoryBase : IDatabaseFactory
                {
                    DbConnection IDatabaseFactory.Open() => null!;
                }

                public sealed class DatabaseFactory : FactoryBase, IDatabaseFactory
                {
                    public DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBeNull();
    }

    // A public member of the interface member's name that a call through the class would bind is not the member that
    // implements it, so the interface does not mark it, and the explicit implementation is the candidate.
    [Fact]
    public void Read_PublicMemberBesideAnExplicitImplementation_IsNotMarkedByTheInterface()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    public DbConnection Open() => null!;

                    DbConnection IDatabaseFactory.Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
    }

    // A public member that implements one marked interface, beside an explicit implementation of another's member of
    // its signature, is called directly, and the explicit one through its interface: two candidates.
    [Fact]
    public void Read_ImplicitAndExplicitImplementationsOfTwoInterfaces_AreBothCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IMainOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public interface IAuditOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public sealed class DatabaseFactory : IMainOpener, IAuditOpener
                {
                    public DbConnection Open() => null!;

                    DbConnection IAuditOpener.Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape
            .HeldCandidates.Select(static candidate => (candidate.Name, candidate.CastTypeText))
            .ShouldBe([("Open", null), ("Open", "global::App.IAuditOpener")]);
        shape
            .HeldCandidates.Select(static candidate => candidate.DisplayName)
            .ShouldBe(["dbf.Open", "dbf.IAuditOpener.Open"]);
    }

    // A marked member that is no connection source is unsupported, implemented explicitly as implicitly, so that the
    // build reports it at the holder: a method that takes an argument, and an indexer.
    [Theory]
    [InlineData("DbConnection Open(string name);", "DbConnection IDatabaseFactory.Open(string name) => null!;", "Open")]
    [InlineData(
        "DbConnection this[int index] { get; }",
        "DbConnection IDatabaseFactory.this[int index] => null!;",
        "this[]"
    )]
    public void Read_ExplicitImplementationOfAnUnsupportedMember_IsAnUnsupportedCandidate(
        string member,
        string implementation,
        string name
    )
    {
        var shape = RepositoryShapes.Of(
            $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    {{implementation}}
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(name);
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
    }

    // An explicit implementation that bears the mark itself is unsupported, as on a repository, so that the build
    // reports it at the holder, though no call can reach it.
    [Fact]
    public void Read_MarkedExplicitImplementation_IsAnUnsupportedCandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    DbConnection Open();
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection IDatabaseFactory.Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("App.IDatabaseFactory.Open");
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        candidate.CastTypeText.ShouldBeNull();
        candidate.Holder.ShouldNotBeNull().Name.ShouldBe("dbf");
    }

    // Only a marked interface member is a factory's: an explicit implementation of an unmarked data source, which the
    // conventions would take on the repository, is none.
    [Fact]
    public void Read_ExplicitImplementationOfAnUnmarkedMember_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    NpgsqlDataSource Source { get; }
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    NpgsqlDataSource IDatabaseFactory.Source => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // A static interface member is no instance member of the factory, even when the class implements it explicitly.
    [Fact]
    public void Read_ExplicitImplementationOfAStaticMember_IsNotHeld()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    static abstract DbConnection Open();
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    static DbConnection IDatabaseFactory.Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // The method could not name an interface that the repository cannot reach.
    [Fact]
    public void Read_ExplicitImplementationOfAnUnreachableInterface_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public static class Outer
                {
                    private interface IHidden
                    {
                        [SqlSourceConnection]
                        DbConnection Open();
                    }

                    public sealed class DatabaseFactory : IHidden
                    {
                        DbConnection IHidden.Open() => null!;
                    }
                }

                [SqlSourceGenerate]
                public partial class UserRepository(Outer.DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ProtectedFactoryMember_IsNotACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class DatabaseFactory
                {
                    [SqlSourceConnection]
                    protected DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_StaticHolder_IsAHolder()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    private static readonly IDatabaseFactory Factory = null!;
                }
            }
            """
        );

        var holder = shape.HeldCandidates.ShouldHaveSingleItem().Holder.ShouldNotBeNull();
        holder.Name.ShouldBe("Factory");
        holder.IsStatic.ShouldBeTrue();
    }

    [Fact]
    public void Read_OwnAndBaseHolders_AreOwnFirstAndInheritedAfter()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                public abstract class RepositoryBase
                {
                    protected IDatabaseFactory Shared { get; } = null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    private readonly IDatabaseFactory _own = null!;
                }
            }
            """
        );

        shape.HeldCandidates.Select(static candidate => candidate.DisplayName).ShouldBe(["_own.Open", "Shared.Open"]);
        shape.HeldCandidates.Select(static candidate => candidate.IsInherited).ShouldBe([false, true]);
    }

    // A factory's member in source is located at itself; only one in metadata takes its holder's location.
    [Fact]
    public void Read_FactoryMemberInSource_IsLocatedAtItself()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<DbConnection> Open(CancellationToken ct);
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Location.Path.ShouldBe(GeneratorHarness.SourcePath);
        candidate.Location.ShouldNotBe(candidate.Holder.ShouldNotBeNull().Location);
    }

    // A method of a derived type hides a member of its base type of its name that is no method.
    [Fact]
    public void Read_MethodHidingAMarkedProperty_HidesIt()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IConnectionOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open { get; }
                }

                public interface IDatabaseFactory : IConnectionOpener
                {
                    new DbConnection Open();
                }

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // Each generic method declares its own type parameters, which are one by their position.
    [Fact]
    public void Read_GenericMethodOfTheSameSignature_HidesTheBases()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Open<T>(T options) => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public new DbConnection Open<T>(T options) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // Two interfaces that neither derives from the other hide nothing of each other: the call is ambiguous, and both
    // are candidates.
    [Fact]
    public void Read_SiblingInterfacesOfOneSignature_AreBothCandidates()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IMainOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public interface IAuditOpener
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public interface IDatabaseFactory : IMainOpener, IAuditOpener;

                [SqlSourceGenerate]
                public partial class UserRepository(IDatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.Select(static candidate => candidate.Name).ShouldBe(["Open", "Open"]);
    }

    [Fact]
    public void Read_UnmarkedNewMethodOfTheSameSignature_HidesTheBases()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Open() => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public new DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        shape.HeldCandidates.ShouldBeEmpty();
    }

    // A type nested in two constructions of one generic type is two types, so the overload hides nothing.
    [Fact]
    public void Read_OverloadOfAnotherConstructionsNestedType_DoesNotHideTheBases()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public class Outer<T>
                {
                    public class Inner { }
                }

                public class FactoryBase
                {
                    [SqlSourceConnection]
                    public DbConnection Open(Outer<int>.Inner options) => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public DbConnection Open(Outer<string>.Inner options) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.IsMarked.ShouldBeTrue();
    }

    // A call through a type parameter reaches an interface's default member that its class constraint does not
    // implement.
    [Fact]
    public void Read_DefaultInterfaceMemberThroughATypeParameter_IsACandidate()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open() => null!;
                }

                public class FactoryBase : IDatabaseFactory { }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : FactoryBase, IDatabaseFactory;
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBeNull();
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("TFactory");
    }

    // A ref struct cannot be cast to its interface, so a marked member that it implements explicitly cannot give the
    // connection: an unsupported candidate, which the build reports at the holder, of each kind of member.  C# 13 lets
    // a ref struct implement an interface, and a ref struct repository hold one.
    [Theory]
    [InlineData("DbConnection Open();", "DbConnection IDatabaseFactory.Open() => null!;", "Open")]
    [InlineData("NpgsqlDataSource Source { get; }", "NpgsqlDataSource IDatabaseFactory.Source => null!;", "Source")]
    public void Read_ExplicitImplementationOfARefStruct_IsAnUnsupportedCandidateOfTheHolder(
        string member,
        string implementation,
        string name
    )
    {
        var source = $$"""
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    {{member}}
                }

                public ref struct RefFactory : IDatabaseFactory
                {
                    {{implementation}}
                }

                [SqlSourceGenerate]
                public ref partial struct UserRepository
                {
                    private RefFactory dbf;
                }
            }
            """;

        var shape = RepositoryShapes.Of(source, languageVersion: LanguageVersion.CSharp13);

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe(name);
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        var holder = candidate.Holder.ShouldNotBeNull();
        holder.Name.ShouldBe("dbf");
        holder.Location.LineSpan.Start.Line.ShouldBe(RepositoryShapes.LineOf(source, "private RefFactory dbf;"));
    }

    // A ref struct's implicit implementation is called directly, with no cast.
    [Fact]
    public void Read_ImplicitImplementationOfARefStruct_IsCalledDirectly()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open();
                }

                public ref struct RefFactory : IDatabaseFactory
                {
                    public DbConnection Open() => null!;
                }

                [SqlSourceGenerate]
                public ref partial struct UserRepository
                {
                    private RefFactory dbf;
                }
            }
            """,
            languageVersion: LanguageVersion.CSharp13
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.CastTypeText.ShouldBeNull();
    }

    // A call through the class names the member that hides the implementation of the marked interface member, which
    // is then reached through the interface, as an explicit one is: a new method of its signature, a new member of its
    // name that can be invoked, a new property over a property, a new member two classes down, a new member over an
    // override of it, and a holder of a type parameter that the class constrains, alone or with the interface.  An
    // interface's default that a class member of its name hides is reached so too, though the interface constrains
    // the type parameter.  The candidate is the interface's member, located at the implementation.
    [Theory]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase { public new DbConnection Open() => null!; }",
        "UserRepository(DatabaseFactory dbf)"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase "
            + "{ public new System.Func<DbConnection> Open => null!; }",
        "UserRepository(DatabaseFactory dbf)"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public class Middle : FactoryBase;\n"
            + "public sealed class DatabaseFactory : Middle { public new DbConnection Open() => null!; }",
        "UserRepository(DatabaseFactory dbf)"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public virtual DbConnection Open() => null!; }\n"
            + "public class Middle : FactoryBase { public override DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : Middle { public new DbConnection Open() => null!; }",
        "UserRepository(DatabaseFactory dbf)"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public class DatabaseFactory : FactoryBase { public new DbConnection Open() => null!; }",
        "UserRepository<T>(T dbf) where T : DatabaseFactory"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public class DatabaseFactory : FactoryBase { public new DbConnection Open() => null!; }",
        "UserRepository<T>(T dbf) where T : DatabaseFactory, IDatabaseFactory"
    )]
    public void Read_ImplementationThatANewMemberHides_IsReachedThroughTheInterface(string classes, string declaration)
    {
        var source =
            "namespace App\n{\n"
            + "public interface IDatabaseFactory\n{\n    [SqlSourceConnection]\n    DbConnection Open();\n}\n\n"
            + classes
            + "\n\n[SqlSourceGenerate]\npublic partial class "
            + declaration
            + ";\n}\n";

        var shape = RepositoryShapes.Of(source);

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Type.ShouldBe(CandidateType.Factory);
        candidate.IsMarked.ShouldBeTrue();
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
        candidate.CastTypeName.ShouldBe("IDatabaseFactory");
    }

    // A marked property that a new property hides is reached through the interface, located at its implementation.
    [Fact]
    public void Read_PropertyThatANewPropertyHides_IsReachedThroughTheInterface()
    {
        const string Source = """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection(Database = "audit")]
                    NpgsqlDataSource Source { get; }
                }

                public class FactoryBase : IDatabaseFactory
                {
                    public NpgsqlDataSource Source => null!;
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public new NpgsqlDataSource Source => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
            }
            """;

        var shape = RepositoryShapes.Of(Source);

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Source");
        candidate.Type.ShouldBe(CandidateType.DataSource);
        candidate.Database.ShouldBe("audit");
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
        candidate.Location.LineSpan.Start.Line.ShouldBe(
            RepositoryShapes.LineOf(Source, "public NpgsqlDataSource Source => null!;")
        );
    }

    // An interface's default that a class member of its name hides from a type parameter, which the class and the
    // interface constrain, is reached through the interface: the call through the type parameter would name the
    // class's member.
    [Fact]
    public void Read_DefaultMemberThatAClassMemberHidesFromATypeParameter_IsReachedThroughTheInterface()
    {
        var shape = RepositoryShapes.Of(
            """
            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    DbConnection Open() => null!;
                }

                public class FactoryBase : IDatabaseFactory
                {
                    public System.Func<DbConnection> Open => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository<TFactory>(TFactory dbf)
                    where TFactory : FactoryBase, IDatabaseFactory;
            }
            """
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBe("global::App.IDatabaseFactory");
    }

    // A call through the class names an implementation that only an override of it follows, that a member it cannot
    // reach would hide, or that a member it cannot invoke passes over, so it is called directly: one candidate, with no
    // cast.
    [Theory]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public virtual DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase { public override DbConnection Open() => null!; }"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase { private new DbConnection Open() => null!; }"
    )]
    [InlineData(
        "public class FactoryBase : IDatabaseFactory { public DbConnection Open() => null!; }\n"
            + "public sealed class DatabaseFactory : FactoryBase { public new string Open => \"\"; }"
    )]
    public void Read_ImplementationThatNoReachableNewMemberHides_IsCalledDirectly(string classes)
    {
        var shape = RepositoryShapes.Of(
            "namespace App\n{\n"
                + "public interface IDatabaseFactory\n{\n    [SqlSourceConnection]\n    DbConnection Open();\n}\n\n"
                + classes
                + "\n\n[SqlSourceGenerate]\npublic partial class UserRepository(DatabaseFactory dbf);\n}\n"
        );

        var candidate = shape.HeldCandidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.CastTypeText.ShouldBeNull();
        candidate.CastTypeName.ShouldBeNull();
    }

    // A ref struct repository that holds a ref struct factory, whose marked member the factory implements explicitly,
    // gets SQLSRC032 at the holder, and no method that would cast the factory, which the build could not compile.  The
    // tests of tests/SqlSource.Tests/Generator/ hand the compiler C# 12 at most, and a ref struct implements an
    // interface from C# 13.
    [Fact]
    public void Run_RefStructFactoryWithAnExplicitImplementation_ReportsSqlsrc032AtTheHolder()
    {
        var run = RunRefStructFactory("NpgsqlConnection IDatabaseFactory.Open() => new();");

        run.Problems()
            .ShouldBe([
                "SQLSRC032 /app/Repo/Sample.cs(20,28)-(20,31): 'IDatabaseFactory.Open' reached through 'dbf' of "
                    + "'App.Sample' is marked [SqlSourceConnection], and cannot give a connection.  Mark a field, "
                    + "property or primary-constructor parameter of a string, a DbDataSource or a DbConnection, or a "
                    + "method that is not generic, takes nothing or a CancellationToken, and returns a string, a "
                    + "DbConnection, or a Task or ValueTask of a DbConnection.",
            ]);
        run.Sources.Values.ShouldAllBe(text => !text.Contains("(global::App.IDatabaseFactory)"));
    }

    // A ref struct factory that implements the marked member implicitly is called directly.
    [Fact]
    public void Run_RefStructFactoryWithAnImplicitImplementation_CallsTheMember()
    {
        var run = RunRefStructFactory("public NpgsqlConnection Open() => new();");

        run.ShouldBeClean();
        run.Sources["App.Sample-Methods.g.cs"].ShouldContain("var connection = this.dbf.Open() ?? throw ");
    }

    // A ref struct repository, Sample, that holds a ref struct factory of the implementation in a field, at C# 13, with
    // a synchronous method of GetUser, since a ref struct cannot be used in an asynchronous one.
    private static GeneratorRun RunRefStructFactory(string implementation)
    {
        var source = $$"""
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    NpgsqlConnection Open();
                }

                public ref struct RefFactory : IDatabaseFactory
                {
                    {{implementation}}
                }

                [SqlSourceGenerate(Source = ".")]
                public ref partial struct Sample
                {
                    private RefFactory dbf;
                }
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: method-type=sync\n-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        return RepositoryRuns.Run(
            file,
            [Entry(file, "GetUser", [Parameter("id")], [Column("id")])],
            source,
            languageVersion: LanguageVersion.CSharp13
        );
    }
}
