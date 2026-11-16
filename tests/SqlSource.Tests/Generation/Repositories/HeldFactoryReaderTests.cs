using System;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Repositories;

// The marked members of the factories that a repository holds, as the generator reads them from the compilation into
// its RepositoryShapes step.
public sealed class HeldFactoryReaderTests
{
    [Fact]
    public void ReadHeld_InterfaceMethodMarked_IsAHeldFactory()
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
    public void ReadHeld_EachKindOfMember_IsRead(string member, string name, string type)
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
    public void ReadHeld_MarkOnBaseInterface_IsFound()
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
    public void ReadHeld_ClassImplementingMarkedInterface_IsMarked()
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
    public void ReadHeld_ParameterStoredInField_HoldsTheFieldAlone()
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
    public void ReadHeld_ParameterPassedToBase_HoldsTheBaseMember()
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

    // Only a member whose type has candidates is a holder, so a field of another service leaves the parameters to be
    // holders.
    [Fact]
    public void ReadHeld_FieldOfATypeWithoutMarks_DoesNotHideTheParameter()
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
    public void ReadHeld_TwoHolders_AreTwoCandidates()
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
    public void ReadHeld_StaticFactoryMember_IsNotHeld()
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
    public void ReadHeld_MarkedHolder_IsNotAHolder(string type)
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
    public void ReadHeld_FactoryInReferencedAssembly_IsFound()
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
    public void ReadHeld_UnreachableMember_IsNotACandidate()
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
    public void ReadHeld_UnsupportedMember_IsAnUnsupportedCandidate()
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
    public void ReadHeld_RepositoryImplementingMarkedInterface_GainsNoMark()
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
}
