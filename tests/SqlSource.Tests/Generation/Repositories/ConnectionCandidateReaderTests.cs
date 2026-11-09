using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Repositories;

// The members of a repository that can give its connection, and the name of its interface, as the generator reads them
// from the compilation into its RepositoryShapes step.
public sealed class ConnectionCandidateReaderTests
{
    private const string Usings = """
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using Npgsql;
        using SqlSource;

        """;

    [Fact]
    public void Read_PrimaryConstructorDataSource_IsAParameterCandidate()
    {
        var shape = ShapeOf(
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
    [InlineData("connectionString", true)]
    [InlineData("_connectionString", true)]
    [InlineData("ConnectionString", true)]
    [InlineData("connstr", false)]
    public void Read_FieldNamedConnectionString_IsAConnectionStringCandidate(string name, bool isCandidate)
    {
        var shape = ShapeOf(
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

        if (isCandidate)
        {
            var candidate = shape.Candidates.ShouldHaveSingleItem();
            candidate.Name.ShouldBe(name);
            candidate.Member.ShouldBe(CandidateMember.Field);
            candidate.Type.ShouldBe(CandidateType.ConnectionString);
            candidate.DeclaredTypeName.ShouldBe("System.String");
        }
        else
        {
            shape.Candidates.ShouldBeEmpty();
        }
    }

    // The common shape of dependency injection: the field the parameter is stored in is the source, so the generated
    // code never captures the parameter as well.
    [Fact]
    public void Read_ParameterStoredInAField_KeepsTheFieldAlone()
    {
        var shape = ShapeOf(
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
    }

    [Fact]
    public void Read_RecordPositionalString_IsItsPropertyAlone()
    {
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
    public void Read_MarkedMethodWithTwoParameters_IsUnsupported()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    private NpgsqlConnection Open(string name, CancellationToken cancellationToken) => null!;
                }
            }
            """
        );

        var candidate = shape.Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Open");
        candidate.Member.ShouldBe(CandidateMember.Method);
        candidate.Type.ShouldBe(CandidateType.Unsupported);
        candidate.IsMarked.ShouldBeTrue();
    }

    [Fact]
    public void Read_MarkedMemberWithDatabase_CarriesIt()
    {
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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
        var shape = ShapeOf(
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

    [Fact]
    public void Read_InterfaceNameTakenByAClass_CarriesTheTakenBy()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public class IUserRepository;
            }
            """
        );

        shape.InterfaceName.ShouldBe("IUserRepository");
        shape.InterfaceTakenBy.ShouldBe("the class App.IUserRepository");
    }

    // The user adds members to the generated interface by declaring another part of it.
    [Fact]
    public void Read_InterfaceNameIsAPartialInterface_IsNotTaken()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public partial interface IUserRepository
                {
                    void Extra();
                }
            }
            """
        );

        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Fact]
    public void Read_NoInterfaceSettings_IsOnAndNamedForTheType()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """
        );

        shape.TypeKey.ShouldBe("App.UserRepository");
        shape.InterfaceOn.ShouldBeTrue();
        shape.InterfaceName.ShouldBe("IUserRepository");
        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Fact]
    public void Read_InterfaceNameWritten_ReplacesTheDefault()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate(InterfaceName = "IUsers")]
                public partial class UserRepository(NpgsqlDataSource dataSource);

                public class IUserRepository;
            }
            """
        );

        shape.InterfaceName.ShouldBe("IUsers");
        shape.InterfaceTakenBy.ShouldBeNull();
    }

    [Theory]
    [InlineData("", null, true)]
    [InlineData("", "false", false)]
    [InlineData("GenerateInterface = false", null, false)]
    [InlineData("GenerateInterface = true", "false", true)]
    public void Read_GenerateInterface_OverridesTheProjectSwitchWhenWritten(
        string argument,
        string? property,
        bool interfaceOn
    )
    {
        var shape = ShapeOf(
            $$"""
            namespace App
            {
                [SqlSourceGenerate({{argument}})]
                public partial class UserRepository(NpgsqlDataSource dataSource);
            }
            """,
            new Dictionary<string, string?> { [RepositoryInterfaces.PropertyName] = property }
        );

        shape.InterfaceOn.ShouldBe(interfaceOn);
    }

    [Fact]
    public void Read_StaticType_HasNoCandidates()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                [SqlSourceGenerate]
                public static partial class UserQueries
                {
                    private static readonly string ConnectionString = "";
                }
            }
            """
        );

        shape.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Read_NestedGenericType_IsFoundByItsMetadataName()
    {
        var shape = ShapeOf(
            """
            namespace App
            {
                public partial class Outer
                {
                    [SqlSourceGenerate]
                    public partial class Repository<T>(NpgsqlDataSource dataSource);

                    public struct IRepository<T>;
                }
            }
            """
        );

        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("dataSource");
        shape.InterfaceTakenBy.ShouldBe("the struct App.Outer.IRepository<T>");
    }

    private static RepositoryShape ShapeOf(string source, IReadOnlyDictionary<string, string?>? properties = null)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, Usings + source)],
            [],
            references: [NpgsqlReferences.Npgsql10],
            properties: properties
        );

        run.CompilationErrors.ShouldBeEmpty();
        return StepOutputs
            .Single<EquatableArray<RepositoryShape>>(run, TrackingNames.RepositoryShapes)
            .ShouldHaveSingleItem();
    }
}
