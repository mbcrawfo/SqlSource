using System;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What the connection makes of an exception of SqlClient's open for a value it read, and which values it refuses
// before it opens.  The refusal of a missing provider of authentication comes only from a server that asks for a
// federated token, which only Azure does, so the exception is built here as SqlClient builds it: an ArgumentException
// with no parameter's name.
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

    // Only open throws it, and only for a value SqlClient read, so its type is enough.
    [Theory]
    [InlineData("")]
    [InlineData("Authentication=Active Directory Default")]
    public void OwnTextAtOpen_PlatformNotSupportedException_IsTheFixedText(string authentication) =>
        SqlServerConnection
            .OwnTextAtOpen(
                new PlatformNotSupportedException("LocalDB is not supported on this platform."),
                Builder(authentication)
            )
            .ShouldBe("SqlClient does not support this server on this platform");

    [Theory]
    [InlineData("Authentication=Sql Password")]
    [InlineData("Authentication=Sql Password;User ID=a")]
    [InlineData("Authentication=Sql Password;Password=b")]
    [InlineData("Authentication=Sql Password;User ID= ;Password=b")]
    [InlineData("Authentication=Active Directory Password;User ID=a")]
    [InlineData("Authentication=Active Directory Service Principal;Password=b")]
    public void LacksCredentials_PasswordMethodWithoutAUserOrAPassword_IsTrue(string settings) =>
        SqlServerConnection.LacksCredentials(Builder(settings)).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("User ID=a")]
    [InlineData("Password=b")]
    [InlineData("Authentication=Sql Password;User ID=a;Password=b")]
    [InlineData("Authentication=Active Directory Service Principal;User ID=a;Password=b")]
    [InlineData("Authentication=Active Directory Integrated")]
    [InlineData("Authentication=Active Directory Default")]
    public void LacksCredentials_OtherValue_IsFalse(string settings) =>
        SqlServerConnection.LacksCredentials(Builder(settings)).ShouldBeFalse();

    private static SqlConnectionStringBuilder Builder(string settings) => [with("Data Source=x;" + settings)];
}
