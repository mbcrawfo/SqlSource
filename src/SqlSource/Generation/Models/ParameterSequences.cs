namespace SqlSource.Generation.Models;

/// <summary>The questions asked of a <see cref="ParameterSequence" />, each answered in one place.</summary>
internal static class ParameterSequences
{
    /// <summary>
    /// Whether the sequence is a PostgreSQL array parameter's, <see cref="ParameterSequence.Array" />: an array that a
    /// <c>Methods</c> method sends from any sequence, and that every other output declares as the array.
    /// </summary>
    public static bool IsArray(ParameterSequence sequence) =>
        sequence switch
        {
            ParameterSequence.Array => true,
            ParameterSequence.None or ParameterSequence.List => false,
            _ => throw UnexpectedEnumValueException.Of(sequence),
        };
}
