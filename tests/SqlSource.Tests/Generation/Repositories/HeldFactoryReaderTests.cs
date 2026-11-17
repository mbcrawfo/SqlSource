using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;

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

    // A call through a holder of the class cannot name an explicit implementation.
    [Fact]
    public void Read_ExplicitImplementationOnAClassHolder_IsNotACandidate()
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

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    Task<DbConnection> IDatabaseFactory.Open(CancellationToken ct) => null!;
                }

                [SqlSourceGenerate]
                public partial class UserRepository(DatabaseFactory dbf);
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
        candidate.Holder.ShouldNotBeNull().TypeName.ShouldBe("TFactory");
    }
}
