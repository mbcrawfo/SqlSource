using System;
using System.Data;
using Dapper;

namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The Dapper type handler of DateTimeOffset, which the project's type override makes every timestamptz: Npgsql gives a
// timestamptz as a DateTime in UTC, and Dapper fills a property of another type only through a handler.  A timetz
// comes as a DateTimeOffset already, and passes through.  Npgsql writes a timestamptz only at offset 0, so a value is
// written as its UTC DateTime, though Dapper 2.1.89 sends a DateTimeOffset parameter by its own DbType and does not
// call this.
internal sealed class UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) =>
        value switch
        {
            DateTime dateTime => new DateTimeOffset(dateTime),
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            _ => throw new InvalidCastException($"Cannot read a {value.GetType()} as a DateTimeOffset."),
        };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
        parameter.Value = value.UtcDateTime;
}
