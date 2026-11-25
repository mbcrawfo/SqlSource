namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.UserClr;

// The queries of Queries/, of the database "user_clr", read and sent as the .NET build of its CLR type dbo.Grade that
// the folder's type override names: extension methods of the connection in UserClrQueriesExtensions.
[SqlSourceGenerate(Source = "Queries")]
internal static partial class UserClrQueries;
