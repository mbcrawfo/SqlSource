using System;
using System.Data;
using Dapper;
using Npgsql;

namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The sample of docs/manual/recipes.md (#models-through-dapper), word for word from "public sealed class" on, for a
// parameter that Dapper cannot send: a change to one is a change to the other.  DapperRecipeTests sends it.
public sealed class TypedParameter(string dataTypeName, object? value) : SqlMapper.ICustomQueryParameter
{
    public void AddParameter(IDbCommand command, string name) =>
        command.Parameters.Add(
            new NpgsqlParameter
            {
                ParameterName = name,
                DataTypeName = dataTypeName,
                Value = value ?? DBNull.Value,
            }
        );
}
