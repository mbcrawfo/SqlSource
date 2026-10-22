using System;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlServerExamples;

// A parameter that Dapper cannot send as it is, sent with its SqlDbType: Dapper sends a DateTime as a datetime, which
// holds a time to 1/300 of a second, so a datetime2 parameter goes through this.  See docs/manual/recipes.md, "With SQL
// Server".
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
