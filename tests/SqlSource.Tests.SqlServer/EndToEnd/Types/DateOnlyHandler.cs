using System;
using System.Data;
using Dapper;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The Dapper type handler of DateOnly, which the project's type override makes every date: SqlClient gives a date as a
// DateTime, and Dapper fills a record's constructor with another type only through a handler.  A parameter is sent
// as a date.
internal sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }
}
