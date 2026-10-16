namespace SqlSource.Tool.Testing;

// Type modifiers as PostgreSQL stores them in atttypmod and typtypmod.  A length's is the length plus 4, the size of
// the header a value of the type carries; a numeric's is (precision << 16 | scale) plus 4.
internal static class Typmods
{
    // varchar(254).
    public const int Varchar254 = 258;

    // varchar(10).
    public const int Varchar10 = 14;

    // numeric(6,2).
    public const int Numeric6Scale2 = 393222;
}
