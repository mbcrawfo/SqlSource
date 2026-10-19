using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace PostgresExamples.Sql;

// The output sql: Sql.CountUsers is a constant, and Sql.CountRows a method that fills in its token.
[SqlSourceGenerate(Output = GeneratorOutput.Sql)]
public sealed partial class CountQueries(DbConnection connection)
{
    public Task<long> CountUsersAsync() => connection.ExecuteScalarAsync<long>(Sql.CountUsers);

    // The token is written into the SQL as it is, so pass only a table name that your own code chooses.
    public Task<long> CountRowsAsync(string table) => connection.ExecuteScalarAsync<long>(Sql.CountRows(table));
}
