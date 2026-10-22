using System;
using System.Data;
using Dapper;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The same code as a sample of docs/manual/recipes.md (#dapper-with-sql-server), in this repository's layout: a change
// to one is a change to the other.  The Dapper type handler of DateOnly, which the project's type override makes every
// date: SqlClient gives a date as a DateTime, and Dapper fills a record's constructor with another type only through
// a handler.  A parameter is sent as a date.
public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }
}
