namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>What the server's answer of <c>operands</c> says of one operand.</summary>
/// <param name="NotNull">True when its row says <c>is_nullable</c> false.</param>
/// <param name="NotClr">
/// True when its row's type is known and no CLR type (<see cref="Types.SqlServerTypes.MayBeClr" />).
/// </param>
/// <remarks>The default, an operand not asked or not answered, proves nothing.</remarks>
internal readonly record struct OperandFacts(bool NotNull, bool NotClr);
