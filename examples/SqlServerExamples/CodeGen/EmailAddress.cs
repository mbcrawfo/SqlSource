using SqlSource;

namespace SqlServerExamples.CodeGen;

// An email address, the type that Users.sql gives the alias type dbo.Email with a type override.  The driver reads an
// alias type over nvarchar as a C# string and knows nothing of EmailAddress, so the generated methods read the string
// and pass it to FromDatabase, and send an EmailAddress parameter as the string that ToDatabase returns.  See
// docs/manual/settings.md, "Converters".
public readonly record struct EmailAddress(string Value)
{
    [SqlSourceConverter]
    public static EmailAddress FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(EmailAddress email) => email.Value;
}
