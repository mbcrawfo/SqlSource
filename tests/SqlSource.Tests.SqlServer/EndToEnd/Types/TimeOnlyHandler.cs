using System;
using System.Data;
using Dapper;

namespace SqlSource.Tests.SqlServer.EndToEnd.Types;

// The same code as a sample of docs/manual/recipes.md (#dapper-with-sql-server), in this repository's layout: a change
// to one is a change to the other.  The Dapper type handler of TimeOnly, which the type override of Queries/Notes.sql
// makes time: SqlClient gives a time as a TimeSpan, and Dapper fills a record's constructor with another type only
// through a handler.  A parameter is sent as a time.
public sealed class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);

    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value;
    }
}
