using SqlSource;

namespace PostgresExamples.CodeGen;

// An email address, the type that Users.sql gives the domain email with a type override.  The driver reads a domain
// over text as a C# string and knows nothing of EmailAddress, so the generated methods read the string and pass it to
// FromDatabase, and send an EmailAddress parameter as the string that ToDatabase returns.  See docs/manual/settings.md,
// "Converters".
public readonly record struct EmailAddress(string Value)
{
    [SqlSourceConverter]
    public static EmailAddress FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(EmailAddress email) => email.Value;
}
