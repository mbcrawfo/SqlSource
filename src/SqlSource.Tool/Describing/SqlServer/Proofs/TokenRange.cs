namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>A run of tokens of a <see cref="TsqlTokens.Read" /> list.</summary>
/// <param name="Start">The index of its first token.</param>
/// <param name="End">The index after its last token.</param>
internal readonly record struct TokenRange(int Start, int End)
{
    /// <summary>The number of tokens.</summary>
    public int Count => End - Start;

    /// <summary>True when it holds no token.</summary>
    public bool IsEmpty => Count == 0;
}
