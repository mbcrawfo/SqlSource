using System;

namespace SqlSource.Tool.Tests;

// The .sql texts that many tests put in a project.  A test whose text differs for its own sake keeps it.
internal static class SqlTexts
{
    // One query, of the project's own database.
    public const string GetUser = "-- name: GetUser\nSELECT 1;\n";

    // Two queries of the project's own database.
    public const string TwoUsers = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    // A query of the project's own database and one of the database "billing".
    public const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    // Users, and a query that needs no entry.
    public const string UsersAndPlain = Users + "\n-- name: Plain\n-- output: sql\nSELECT 3;\n";

    // The text with the SQL of its second query changed, so that the query has another hash.
    public static string WithSecondQueryEdited(string text) =>
        text.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal);
}
