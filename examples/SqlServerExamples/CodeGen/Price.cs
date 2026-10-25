using SqlSource;

namespace SqlServerExamples.CodeGen;

// A price, the type that OrderLines.sql gives its decimal columns with a type override.  The driver reads a
// decimal column as a C# decimal and knows nothing of Price, so the generated method reads the decimal and passes it to
// FromDatabase, and a Price parameter would be sent as the decimal that ToDatabase returns.  See
// docs/manual/settings.md, "Converters".
public readonly record struct Price(decimal Amount)
{
    [SqlSourceConverter]
    public static Price FromDatabase(decimal amount) => new(amount);

    [SqlSourceConverter]
    public static decimal ToDatabase(Price price) => price.Amount;
}
