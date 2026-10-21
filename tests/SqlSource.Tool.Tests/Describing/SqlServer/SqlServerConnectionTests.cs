using System;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the connection makes of an exception of SqlClient's open for a value it read.  The refusal of a missing
// provider of authentication comes only from a server that asks for a federated token, which only Azure does, so the
// exception is built here as SqlClient builds it: an ArgumentException with no parameter's name.
public sealed class SqlServerConnectionTests
{
    private const string ProviderMissing = "Cannot find an authentication provider for 'ActiveDirectoryDefault'.";

    [Theory]
    [InlineData("Active Directory Default")]
    [InlineData("Active Directory Managed Identity")]
    [InlineData("Active Directory Service Principal")]
    public void OwnTextAtOpen_ArgumentExceptionForAnEntraMethod_IsTheFixedText(string method) =>
        SqlServerConnection
            .OwnTextAtOpen(new ArgumentException(ProviderMissing), Builder("Authentication=" + method))
            .ShouldBe("SqlClient has no provider for the Authentication of the connection string");

    // Any other ArgumentException of open is a bug, and stays one.
    [Theory]
    [InlineData("")]
    [InlineData("Authentication=Sql Password")]
    public void OwnTextAtOpen_ArgumentExceptionWithoutAnEntraMethod_IsNone(string authentication) =>
        SqlServerConnection
            .OwnTextAtOpen(new ArgumentException(ProviderMissing), Builder(authentication))
            .ShouldBeNull();

    [Fact]
    public void OwnTextAtOpen_OtherExceptionForAnEntraMethod_IsNone() =>
        SqlServerConnection
            .OwnTextAtOpen(new InvalidOperationException("a bug"), Builder("Authentication=Active Directory Default"))
            .ShouldBeNull();

    private static SqlConnectionStringBuilder Builder(string settings) => [with("Data Source=x;" + settings)];
}
