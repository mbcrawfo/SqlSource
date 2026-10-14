using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The kind of statement a text is, read from its first word and from an INTO: a word to refuse is on a list, so a
// typo such as SELEC is never refused here and reaches the server.
public class StatementShapeTests
{
    [Theory]
    [InlineData("ABORT")]
    [InlineData("ALTER")]
    [InlineData("ANALYSE")]
    [InlineData("ANALYZE")]
    [InlineData("BEGIN")]
    [InlineData("CALL")]
    [InlineData("CHECKPOINT")]
    [InlineData("CLOSE")]
    [InlineData("CLUSTER")]
    [InlineData("COMMENT")]
    [InlineData("COMMIT")]
    [InlineData("COPY")]
    [InlineData("CREATE")]
    [InlineData("DEALLOCATE")]
    [InlineData("DECLARE")]
    [InlineData("DISCARD")]
    [InlineData("DO")]
    [InlineData("DROP")]
    [InlineData("END")]
    [InlineData("EXECUTE")]
    [InlineData("EXPLAIN")]
    [InlineData("FETCH")]
    [InlineData("GRANT")]
    [InlineData("IMPORT")]
    [InlineData("LISTEN")]
    [InlineData("LOAD")]
    [InlineData("LOCK")]
    [InlineData("MOVE")]
    [InlineData("NOTIFY")]
    [InlineData("PREPARE")]
    [InlineData("REASSIGN")]
    [InlineData("REFRESH")]
    [InlineData("REINDEX")]
    [InlineData("RELEASE")]
    [InlineData("RESET")]
    [InlineData("REVOKE")]
    [InlineData("ROLLBACK")]
    [InlineData("SAVEPOINT")]
    [InlineData("SECURITY")]
    [InlineData("SET")]
    [InlineData("SHOW")]
    [InlineData("START")]
    [InlineData("TRUNCATE")]
    [InlineData("UNLISTEN")]
    [InlineData("VACUUM")]
    public void RefusedKind_RefusedFirstWord_IsThatWord(string word) =>
        StatementShape.RefusedKind($"{word} x").ShouldBe(word);

    [Theory]
    [InlineData("call p(@a)", "CALL")]
    [InlineData("Call p(@a)", "CALL")]
    // A comment, a block comment or a hint before the word does not hide it, and the case of the word does not matter.
    [InlineData("-- archive\ncall archive(@id)", "CALL")]
    [InlineData("/* c */ CREATE TEMP TABLE t (a int)", "CREATE")]
    [InlineData("/*+ hint */ TRUNCATE t", "TRUNCATE")]
    [InlineData("  \n\t DROP TABLE t", "DROP")]
    public void RefusedKind_RefusedWordAfterCommentsAndWhiteSpace_IsThatWord(string sql, string kind) =>
        StatementShape.RefusedKind(sql).ShouldBe(kind);

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("(SELECT 1)")]
    [InlineData("((SELECT 1) UNION (SELECT 2))")]
    [InlineData("TABLE users")]
    [InlineData("VALUES (1)")]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("UPDATE t SET a = 1")]
    [InlineData("DELETE FROM t")]
    [InlineData("MERGE INTO t USING s ON true WHEN MATCHED THEN DELETE")]
    [InlineData("WITH d AS (DELETE FROM t RETURNING *) INSERT INTO u SELECT * FROM d")]
    // A typo is the server's to reject, as SQLSRC225.
    [InlineData("SELEC 1")]
    [InlineData("SELECT 'INTO' FROM t")]
    [InlineData("SELECT 1 -- INTO t")]
    [InlineData("SELECT (SELECT 1 INTO t)")]
    // A word of the list inside the statement is no first word.
    [InlineData("SELECT 1 FROM t WHERE a = 'CALL' OR b = \"drop\"")]
    // Nothing to read, or no word first.
    [InlineData("")]
    [InlineData("-- only a comment")]
    [InlineData("'CALL'")]
    [InlineData("* CALL")]
    public void RefusedKind_StatementPrepareTakes_IsNull(string sql) => StatementShape.RefusedKind(sql).ShouldBeNull();

    [Theory]
    [InlineData("SELECT * INTO t FROM users")]
    [InlineData("select a into t from users")]
    [InlineData("WITH x AS (SELECT 1) SELECT * INTO t FROM x")]
    [InlineData("SELECT a /* c */ INTO -- c\n t FROM users")]
    [InlineData("SELECT @a::int AS a INTO TEMP t")]
    public void RefusedKind_SelectInto_IsSelectInto(string sql) =>
        StatementShape.RefusedKind(sql).ShouldBe("SELECT INTO");

    // INSERT and MERGE are followed by INTO as a rule of their grammar, with comments allowed between.
    [Theory]
    [InlineData("INSERT /* c */ INTO t VALUES (1)")]
    [InlineData("insert\n-- c\ninto t VALUES (1)")]
    [InlineData("MERGE /* c */ INTO t USING s ON true WHEN MATCHED THEN DELETE")]
    public void RefusedKind_InsertOrMergeWithCommentBeforeInto_IsNull(string sql) =>
        StatementShape.RefusedKind(sql).ShouldBeNull();

    [Fact]
    public void RefusedKind_TextThatDoesNotLex_Throws() =>
        Should.Throw<System.InvalidOperationException>(() => StatementShape.RefusedKind("SELECT 'open"));
}
