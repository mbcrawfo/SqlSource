using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

namespace SqlServerExamples.Sql;

// The output sql: Counts.CountUsers is a constant, and Counts.CountRows a method that fills in its token.  NestedClass
// names the class that holds them, which is Sql by default.
[SqlSourceGenerate(Output = GeneratorOutput.Sql, NestedClass = "Counts")]
public sealed partial class CountQueries(DbConnection connection)
{
    public Task<int> CountUsersAsync() => connection.ExecuteScalarAsync<int>(Counts.CountUsers);

    // The token is written into the SQL as it is, so pass only a table name that your own code chooses.
    public Task<int> CountRowsAsync(string table) => connection.ExecuteScalarAsync<int>(Counts.CountRows(table));
}
