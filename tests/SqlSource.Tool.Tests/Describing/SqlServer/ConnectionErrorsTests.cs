using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The describer's own errors, and how every error is printed as a server line.  SqlException has no public
// constructor, so what AtOpen and Of make of one is tested against the container.
public sealed class ConnectionErrorsTests
{
    [Fact]
    public void Own_Text_IsOneRowOfNumberClassAndStateZero() =>
        ConnectionErrors
            .Own(ConnectionErrors.UnreadableConnectionString)
            .Errors.ShouldBe([new SqlServerErrorRow(0, 0, 0, "SqlClient cannot read the connection string")]);

    [Fact]
    public void ServerLines_ServerErrors_AreEachMsgLevelAndStateInOrder() =>
        SqlServerErrors
            .ServerLines(
                new SqlServerServerError([
                    new SqlServerErrorRow(4060, 11, 1, "the server cannot open the database for the login"),
                    new SqlServerErrorRow(18456, 14, 1, "the server refused the login"),
                ])
            )
            .ShouldBe([
                "Msg 4060, Level 11, State 1: the server cannot open the database for the login",
                "Msg 18456, Level 14, State 1: the server refused the login",
            ]);

    // An error of the describer's own has no number, and its line is the text alone.
    [Fact]
    public void ServerLines_OwnError_IsTheTextAlone() =>
        SqlServerErrors
            .ServerLines(ConnectionErrors.Own(ConnectionErrors.LostConnection))
            .ShouldBe(["the connection to the server was lost"]);
}
