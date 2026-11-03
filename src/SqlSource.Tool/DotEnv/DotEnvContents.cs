using System.Collections.Immutable;

namespace SqlSource.Tool.DotEnv;

/// <summary>
/// What the tool took from the text of a <c>.env</c> file.
/// </summary>
/// <param name="Values">
/// The value of each of the tool's keys, by key (ordinal), the last of a key winning.  A value is a connection and
/// is never printed.
/// </param>
/// <param name="Errors">The lines that hold one of the tool's keys and could not be read, in order.</param>
internal sealed record DotEnvContents(
    ImmutableDictionary<string, string> Values,
    ImmutableArray<DotEnvLineError> Errors
)
{
    /// <summary>The contents without their values, which are secrets.</summary>
    public override string ToString() => $"DotEnvContents {{ Values = {Values.Count}, Errors = {Errors.Length} }}";
}
