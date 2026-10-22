using System;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The same code as the sample of docs/manual/recipes.md (#dapper-with-sql-server), in this repository's layout, for a
// parameter that Dapper cannot send, or sends as another type: a change to one is a change to the other.
// DapperRecipeTests sends it.
public sealed class TypedParameter(SqlDbType sqlDbType, object? value) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name) =>
        command.Parameters.Add(
            new SqlParameter
            {
                ParameterName = name,
                SqlDbType = sqlDbType,
                Value = value ?? DBNull.Value,
            }
        );
}
