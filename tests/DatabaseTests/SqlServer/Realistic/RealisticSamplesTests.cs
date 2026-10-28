using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic;

// The oracle's batch of a case: its sample values bound to its parameters.  Their checks against the query's
// parameters are tested with RealisticSampleChecks in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticSamplesTests
{
    private const string Sql = "SELECT 1";

    [Fact]
    public void Batch_TwoParameters_DeclaresEachSampleAndBindsItByName()
    {
        var batch = RealisticSamples.Batch(
            Sql,
            RealisticSample.Of(("a", "1"), ("b", "N'x'")),
            [Described("a", "int"), Described("b", "Email", userSchema: "dbo")]
        );

        batch.Text.ShouldBe(
            "DECLARE @sample_0 int = (\n1\n);\nDECLARE @sample_1 [dbo].[Email] = (\nN'x'\n);\n"
                + "EXEC sys.sp_executesql @statement, @declarations, @a = @sample_0, @b = @sample_1;"
        );
        batch.Declarations.ShouldBe("@a int, @b [dbo].[Email]");
        batch.Statement.ShouldBe(Sql);
    }

    [Fact]
    public void Batch_NoParameters_RunsTheStatementAlone()
    {
        var batch = RealisticSamples.Batch(Sql, RealisticSample.Of(), []);

        batch.Text.ShouldBe("EXEC sys.sp_executesql @statement;");
        batch.Declarations.ShouldBeEmpty();
        batch.Statement.ShouldBe(Sql);
    }

    [Fact]
    public void Batch_SampleKeyDifferingInCase_IsFound()
    {
        var batch = RealisticSamples.Batch(Sql, RealisticSample.Of(("ACCOUNT", "7")), [Described("account", "int")]);

        batch.Text.ShouldContain("DECLARE @sample_0 int = (\n7\n);");
    }

    // A sample may end in a comment, which would otherwise run to the end of the line and take the parenthesis.
    [Fact]
    public void Batch_SampleEndingInAComment_ClosesOnALineOfItsOwn()
    {
        var batch = RealisticSamples.Batch(
            Sql,
            RealisticSample.Of(("a", "7 -- Ana's account")),
            [Described("a", "int")]
        );

        batch.Text.ShouldStartWith("DECLARE @sample_0 int = (\n7 -- Ana's account\n);\n");
    }

    [Fact]
    public void Batch_ParameterWithNoSampleValue_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Batch(
                    Sql,
                    RealisticSample.Of(("a", "1")),
                    [Described("a", "int"), Described("c", "int")]
                )
            )
            .Message.ShouldContain("@c");

    [Fact]
    public void Batch_ParameterWithNoType_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Batch(
                    Sql,
                    RealisticSample.Of(("a", "1"), ("c", "1")),
                    [Described("a", "int"), new DescribedParameter("c", null, null)]
                )
            )
            .Message.ShouldContain("@c");

    [Fact]
    public void Batch_ParameterWithAnotherEnginesType_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticSamples.Batch(
                    Sql,
                    RealisticSample.Of(("c", "1")),
                    [
                        new DescribedParameter(
                            "c",
                            new PostgresType(
                                "integer",
                                "base",
                                "pg_catalog",
                                "int4",
                                null,
                                null,
                                null,
                                null,
                                null,
                                null,
                                null
                            ),
                            null
                        ),
                    ]
                )
            )
            .Message.ShouldContain("@c");

    private static DescribedParameter Described(string name, string type, string? userSchema = null) =>
        new(
            name,
            new SqlServerType(type, 0, 0, 0, userSchema is null ? null : new SqlServerUserType(userSchema, type, null)),
            null
        );
}
