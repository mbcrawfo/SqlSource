using System;
using System.Data;
using Dapper;

namespace SqlServerExamples;

// Reads a date as a DateOnly, the type the project's type override gives it: SqlClient gives a date as a DateTime, and
// Dapper fills a property of another type only through a handler.  A DateOnly parameter is sent as a date.  See
// docs/manual/recipes.md, "With SQL Server".
public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }
}
