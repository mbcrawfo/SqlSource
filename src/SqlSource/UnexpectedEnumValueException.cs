using System;

namespace SqlSource;

/// <summary>
/// What the discard or the <c>default:</c> of a switch over an enum throws, for a value that is none of the enum's
/// members.  Every member is named, so only such a value reaches it.  It is the generator's and the tool's own, and
/// not one of the exceptions written into a user's project.  A switch throws the one that <see cref="Of{T}" /> gives;
/// the standard constructors are there because CA1032 asks for them.
/// </summary>
internal sealed class UnexpectedEnumValueException : InvalidOperationException
{
    public UnexpectedEnumValueException() { }

    public UnexpectedEnumValueException(string message)
        : base(message) { }

    public UnexpectedEnumValueException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The exception for a value of <typeparamref name="T" /> that no switch over it names.</summary>
    /// <param name="value">The value.</param>
    public static UnexpectedEnumValueException Of<T>(T value)
        where T : struct, Enum => new($"The value {value} is no member of {typeof(T).FullName}.");
}
