using System;
using System.Data.SqlTypes;
using System.Globalization;
using Microsoft.SqlServer.Server;

namespace SqlSource.DatabaseTests.SqlServer.ClrType;

/// <summary>
/// A CLR type of a user's own assembly: a grade, a whole number, or its Null.  It is byte ordered, so the server
/// compares two of it with <c>=</c>.  <c>Parse</c> answers its Null for the string <c>NULL</c>, as <c>geometry</c>'s
/// does.
/// </summary>
[Serializable]
[SqlUserDefinedType(Format.Native, IsByteOrdered = true)]
public struct Grade : INullable, IEquatable<Grade>
{
    // The fields are the type's bytes in Format.Native, in this order, so a change to them is a change to the type.
    private bool _isNotNull;
    private int _value;

    /// <summary>The type's Null.</summary>
    public static Grade Null => default;

    /// <inheritdoc />
    public readonly bool IsNull => !_isNotNull;

    /// <summary>A grade of the number.</summary>
    public static Grade Of(int value) => new() { _isNotNull = true, _value = value };

    /// <summary>The grade a string writes, or the Null for a null string or <c>NULL</c>.</summary>
    public static Grade Parse(SqlString text) =>
        text.IsNull || text.Value == "NULL" ? Null : Of(int.Parse(text.Value, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override readonly string ToString() => _isNotNull ? _value.ToString(CultureInfo.InvariantCulture) : "NULL";

    /// <inheritdoc />
    public readonly bool Equals(Grade other) => _isNotNull == other._isNotNull && _value == other._value;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Grade other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => _isNotNull ? _value : -1;

    /// <summary>Whether two grades are equal.</summary>
    public static bool operator ==(Grade left, Grade right) => left.Equals(right);

    /// <summary>Whether two grades differ.</summary>
    public static bool operator !=(Grade left, Grade right) => !left.Equals(right);
}
