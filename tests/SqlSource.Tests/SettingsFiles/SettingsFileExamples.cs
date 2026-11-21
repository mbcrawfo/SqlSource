namespace SqlSource.Tests.SettingsFiles;

internal static class SettingsFileExamples
{
    /// <summary>
    /// A settings file with a schema, <c>root</c>, a dialect, generator parameters, type overrides and column types,
    /// and with comments and trailing commas.
    /// </summary>
    public const string Design = /*lang=json*/
        """
        {
            "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
            "root": true,
            "dialect": "postgres",
            "generatorParameters": ["keep-comments", "async-method-suffix"],
            // Dapper reads these through UtcDateTimeOffsetHandler.
            "typeOverrides": {
                "timestamptz": "System.DateTimeOffset",
                "public.\"Email\"": "MyApp.Email",
            },
            // Every query that reads one of these columns gets the C# type, by its short name.
            "columnTypes": {
                "users.id": "UserId",
                "posts.created_by_user_id": "UserId",
            },
        }
        """;
}
