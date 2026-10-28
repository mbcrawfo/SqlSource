using System.Data;
using Dapper;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Types;

// The same code as the sample of docs/manual/recipes.md (#dapper-with-postgresql), in this repository's layout, for a
// type of the project that has converters: a change to one is a change to the other.  It is internal, as Email is.
// EndToEndSources registers it, and DapperRecipeTests reads and sends an Email through it.
internal sealed class EmailHandler : SqlMapper.TypeHandler<Email>
{
    public override Email Parse(object value) => Email.FromDatabase((string)value);

    public override void SetValue(IDbDataParameter parameter, Email value) => parameter.Value = Email.ToDatabase(value);
}
