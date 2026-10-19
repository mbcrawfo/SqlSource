using System.Data;
using Dapper;

namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The Dapper type handler of the record struct Email, which the schema's domain email is read as: Npgsql gives a
// string, and Dapper fills a property of another type only through a handler.
internal sealed class EmailHandler : SqlMapper.TypeHandler<Email>
{
    public override Email Parse(object value) => new((string)value);

    public override void SetValue(IDbDataParameter parameter, Email value) => parameter.Value = value.Value;
}
