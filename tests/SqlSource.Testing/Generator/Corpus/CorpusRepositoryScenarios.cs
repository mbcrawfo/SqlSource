using SqlSource.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator.Corpus;

// The part of the golden corpus of query methods that holds the scenarios of the repositories: for each kind of
// connection source, a connection string, a data source, a factory and a borrowed connection, the methods of a query
// of one row or none, one of many rows and one that streams them.  QueryMethodCorpus.cs lists the cases.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static partial class QueryMethodCorpus
{
    // A connection string from a primary-constructor parameter, which the methods reach by its name.
    private const string ConnectionStringSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(string connectionString);
        }
        """;

    // A data source that the constructor stores in a field, which the methods reach through this.
    private const string DataSourceSource = """
        using System.Data.Common;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(DbDataSource dataSource)
            {
                private readonly DbDataSource _dataSource = dataSource;
            }
        }
        """;

    // A factory that opens a connection asynchronously and takes the token.
    private const string FactorySource = """
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(DbDataSource dataSource)
            {
                [SqlSourceConnection]
                private ValueTask<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
                    dataSource.OpenConnectionAsync(cancellationToken);
            }
        }
        """;

    // A connection of the caller's, held in a property, which the methods borrow.
    private const string BorrowedSource = """
        using System.Data.Common;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(DbConnection connection)
            {
                [SqlSourceConnection]
                public DbConnection Connection { get; } = connection;
            }
        }
        """;

    private static GeneratorRun RepositoryOfConnectionString(CorpusEngine engine) =>
        Repository(engine, ConnectionStringSource);

    private static GeneratorRun RepositoryOfDataSource(CorpusEngine engine) => Repository(engine, DataSourceSource);

    private static GeneratorRun RepositoryOfFactory(CorpusEngine engine) => Repository(engine, FactorySource);

    private static GeneratorRun RepositoryOfBorrowed(CorpusEngine engine) => Repository(engine, BorrowedSource);

    // The repository's queries, at Repository: one row or none, many rows and a stream of them.
    private static GeneratorRun Repository(CorpusEngine engine, string source)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            """
            -- name: FindUser -> one-optional
            SELECT id, name FROM users WHERE id = @id;
            -- name: ListUsers
            SELECT id, name FROM users WHERE id > @id;
            -- name: StreamUsers
            -- collection-type: IAsyncEnumerable
            SELECT id, name FROM users;

            """
        );
        SidecarParameter[] id = [IdParameter(engine)];
        var columns = IdAndName(engine);
        return engine.Run(
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [
                file,
                Sidecars.Of(
                    file,
                    engine.Entry(file, "FindUser", id, columns),
                    engine.Entry(file, "ListUsers", id, columns),
                    engine.Entry(file, "StreamUsers", [], columns)
                ),
            ],
            output: OutputKind.Repository
        );
    }
}
