using System.Linq;
using Microsoft.CodeAnalysis;
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
    public void Read_MarkedMethodWithTwoParameters_IsUnsupported()
    {
        var shape = RepositoryShapes.Of(
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
