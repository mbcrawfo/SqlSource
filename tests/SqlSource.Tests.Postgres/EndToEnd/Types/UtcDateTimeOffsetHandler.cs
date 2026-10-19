using System;
using System.Data;
using Dapper;

namespace SqlSource.Tests.Postgres.EndToEnd.Types;

// The Dapper type handler of DateTimeOffset, which the project's type override makes every timestamptz: Npgsql gives a
// timestamptz as a DateTime in UTC, and Dapper fills a property of another type only through a handler.  A timetz
// comes as a DateTimeOffset already, and passes through.
internal sealed class UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) =>
        value switch
        {
            DateTime dateTime => new DateTimeOffset(dateTime),
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            _ => throw new InvalidCastException($"Cannot read a {value.GetType()} as a DateTimeOffset."),
        };

    // Not called: Dapper sends a DateTimeOffset parameter by its own mapping, so a value must have offset 0.
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value;
}
