using SqlSource;

namespace PostgresExamples.CodeGen;

// The id of an order, which the project's column types give order_lines.order_id, and markers of OrderLines.sql a
// parameter and a computed column, each by its short name.  The driver reads an integer column as a C# int and knows
// nothing of OrderId, so the generated method reads the int and passes it to FromDatabase, and sends an OrderId
// parameter as the int that ToDatabase returns.  See docs/manual/settings.md, "Converters".
public readonly record struct OrderId(int Value)
{
    [SqlSourceConverter]
    public static OrderId FromDatabase(int value) => new(value);

    [SqlSourceConverter]
    public static int ToDatabase(OrderId orderId) => orderId.Value;
}
