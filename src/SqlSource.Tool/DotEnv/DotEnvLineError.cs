namespace SqlSource.Tool.DotEnv;

/// <summary>
/// A line of a <c>.env</c> file that holds one of the tool's keys and could not be read.
/// </summary>
/// <param name="Line">The line, counted from zero.</param>
/// <param name="Column">The column of the key's first character, counted from zero.</param>
/// <param name="Key">
/// What the error may name: the key, or for a key that is not a valid name only its leading run of letters, digits
/// and <c>_</c>.  Never any part of the value.
/// </param>
/// <param name="Reason">Why the line could not be read: one of a fixed set of texts that name no value.</param>
internal sealed record DotEnvLineError(int Line, int Column, string Key, string Reason);
