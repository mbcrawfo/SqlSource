using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace PostgresExamples.Sql;

// The output sql: Counts.CountUsers is a constant, and Counts.CountRows a method that fills in its token.  NestedClass
// names the class that holds them, which is Sql by default.
[SqlSourceGenerate(Output = GeneratorOutput.Sql, NestedClass = "Counts")]
public sealed partial class CountQueries(DbConnection connection)
{
    public Task<long> CountUsersAsync() => connection.ExecuteScalarAsync<long>(Counts.CountUsers);

    // The token is written into the SQL as it is, so pass only a table name that your own code chooses.
    public Task<long> CountRowsAsync(string table) => connection.ExecuteScalarAsync<long>(Counts.CountRows(table));
}
