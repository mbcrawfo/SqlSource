using System.Data;
using Dapper;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The Dapper type handler of the record struct Email, which the schema's alias type dbo.Email is read as: SqlClient
// gives a string, and Dapper fills a property of another type only through a handler.
internal sealed class EmailHandler : SqlMapper.TypeHandler<Email>
{
    public override Email Parse(object value) => new((string)value);

    public override void SetValue(IDbDataParameter parameter, Email value) => parameter.Value = value.Value;
}
