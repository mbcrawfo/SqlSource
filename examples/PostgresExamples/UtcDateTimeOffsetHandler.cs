using System;
using System.Data;
using Dapper;

namespace PostgresExamples;

// Reads a timestamptz as a DateTimeOffset: Npgsql gives it as a DateTime in UTC, and Dapper fills a property of
// another type only through a handler.  Infinity comes as DateTime.MaxValue or MinValue of no kind, so the kind is set
// to UTC, at whose offset both are in range.
public sealed class UtcDateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) =>
        value switch
        {
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            _ => throw new InvalidCastException($"Cannot read a {value.GetType()} as a DateTimeOffset."),
        };

    // Dapper sends a DateTimeOffset parameter by its own mapping, which takes offset 0 alone: pass
    // value.ToUniversalTime().
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value;
}
