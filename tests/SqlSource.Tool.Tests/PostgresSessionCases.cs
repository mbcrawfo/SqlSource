using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;

namespace SqlSource.Tool.Tests;

// What several classes of the session's tests share: two queries of users.id and a window's count, one without a word
// of the gate and one with a LEFT JOIN, and the answers of a description.
internal static class PostgresSessionCases
{
    public const string UngatedUsersId = "SELECT id, count(*) OVER () AS n FROM users";

    public const string GatedUsersId =
        "SELECT u.id, count(*) OVER () AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id";

    public static (bool? Nullable, string? Source)[] AnswersOf(QueryDescription description) =>
        NullabilityAnswers.Of(description.Columns.ShouldNotBeNull());
}
