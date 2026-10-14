using System;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the driver threw, as the answer of a call holds it.
public class PostgresConnectionTests
{
    [Fact]
    public void ErrorOf_ExceptionOfTheDriver_IsItsMessageAlone() =>
        PostgresConnection
            .ErrorOf(new NpgsqlException("Failed to connect to 127.0.0.1:1"))
            .ShouldBe(new PostgresServerError(null, "Failed to connect to 127.0.0.1:1", null, null, null));

    [Fact]
    public void ErrorOf_ExceptionOfTheServer_HasItsSqlStateMessageDetailAndHint() =>
        PostgresConnection
            .ErrorOf(Server(position: 49))
            .ShouldBe(new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", "a hint", 49));

    // The server sends no position as zero.
    [Fact]
    public void ErrorOf_ExceptionOfTheServerWithPositionZero_HasNoPosition() =>
        PostgresConnection.ErrorOf(Server(position: 0)).Position.ShouldBeNull();

    [Fact]
    public void ErrorOf_ExceptionOfTheServerWithPositionOne_KeepsIt() =>
        PostgresConnection.ErrorOf(Server(position: 1)).Position.ShouldBe(1);

    // Port 1 proves that nothing connected: a connect would fail with the driver's own message.  The keyword is read
    // by the driver's builder, so its case is the builder's.
    [Theory]
    [InlineData("Host=127.0.0.1;Port=1;Multiplexing=true")]
    [InlineData("host=127.0.0.1;port=1;multiplexing=True")]
    public async Task OpenAsync_Multiplexing_ThrowsRefusedBeforeConnecting(string connectionString)
    {
        await using var connection = new PostgresConnection(connectionString, NullRunLog.Instance);

        var thrown = await Should.ThrowAsync<NpgsqlException>(() =>
            connection.OpenAsync(TestContext.Current.CancellationToken)
        );

        thrown.Message.ShouldBe(PostgresConnection.MultiplexingRefused);
    }

    [Fact]
    public async Task OpenAsync_MultiplexingFalse_IsNotRefused()
    {
        await using var connection = new PostgresConnection(
            "Host=127.0.0.1;Port=1;Multiplexing=false",
            NullRunLog.Instance
        );

        var thrown = await Should.ThrowAsync<NpgsqlException>(() =>
            connection.OpenAsync(TestContext.Current.CancellationToken)
        );

        thrown.Message.ShouldNotBe(PostgresConnection.MultiplexingRefused);
    }

    // What the driver throws, when a connection string names a file, is no NpgsqlException: it reads the file when it
    // has a server to talk to, a client certificate once the server agrees to TLS and a password file once the
    // server asks for a password, so a closed port proves nothing and these tests have a server of their own words.
    // The driver's text quotes the path.  The message is the constant, with no inner exception to repeat it.
    [Fact]
    public async Task OpenAsync_ClientCertificateThatIsNotThere_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.PathOf("missing-certificate.pem");

        await ShouldRefuseAsync(
            AgreesToTls,
            $"SslMode=Require;SslCertificate={path}",
            path,
            PostgresConnection.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_ClientCertificateInAFolderThatIsNotThere_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.PathOf("no-such-folder/certificate.pem");

        await ShouldRefuseAsync(
            AgreesToTls,
            $"SslMode=Require;SslCertificate={path}",
            path,
            PostgresConnection.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_ClientKeyThatIsNotThere_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var certificate = folder.WriteFile("certificate.pem", "not a certificate");
        var key = folder.PathOf("missing.key");

        await ShouldRefuseAsync(
            AgreesToTls,
            $"SslMode=Require;SslCertificate={certificate};SslKey={key}",
            key,
            PostgresConnection.UnreadableFile
        );
    }

    // On Unix the system says "denied" for a folder where a file is read, which is also what a file without the
    // right to read says.
    [Fact]
    public async Task OpenAsync_PasswordFileThatIsAFolder_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFolder("a-folder");

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            path,
            PostgresConnection.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_PasswordFileWithAPathThatIsTooLong_ThrowsTheFixedMessage()
    {
        var path = new string('p', 40_000);

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            path,
            PostgresConnection.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_ClientCertificateThatIsNoCertificate_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("certificate.pem", "s3cret-marker is no certificate");

        await ShouldRefuseAsync(
            AgreesToTls,
            $"SslMode=Require;SslCertificate={path}",
            "s3cret-marker",
            PostgresConnection.UnusableFile
        );
    }

    [Fact]
    public async Task OpenAsync_PasswordFileWithALineThatIsNotAnEntry_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("passfile", "s3cret-marker is no entry");

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            "s3cret-marker",
            PostgresConnection.UnusableFile
        );
    }

    // The driver refuses it when it builds the data source, before anything connects: the port 1 proves it.
    [Fact]
    public async Task OpenAsync_TargetSessionAttributesForOneHost_ThrowsTheFixedMessage() =>
        await ShouldRefuseAsync(
            null,
            "Host=127.0.0.1;Port=1;Target Session Attributes=primary",
            "primary",
            PostgresConnection.UnsupportedSettings
        );

    // The runtime runs the GSS loader from the static constructor of NetSecurityNative: the first call into it throws
    // for that type, with the loader's type inside.  The chain is built by hand, since this machine has GSS.
    [Fact]
    public void IsGssUnavailable_ExceptionOfNetSecurityNativeWithTheLoaderInside_IsTrue() =>
        PostgresConnection
            .IsGssUnavailable(
                new TypeInitializationException(
                    "Interop+NetSecurityNative",
                    new TypeInitializationException(
                        "Interop+NetSecurityNative+GssInitializer",
                        new InvalidOperationException()
                    )
                )
            )
            .ShouldBeTrue();

    [Fact]
    public void IsGssUnavailable_ExceptionOfTheLoaderAlone_IsTrue() =>
        PostgresConnection
            .IsGssUnavailable(
                new TypeInitializationException(
                    "Interop+NetSecurityNative+GssInitializer",
                    new InvalidOperationException()
                )
            )
            .ShouldBeTrue();

    [Fact]
    public void IsGssUnavailable_ExceptionOfAnotherType_IsFalse() =>
        PostgresConnection
            .IsGssUnavailable(
                new TypeInitializationException(
                    "SqlSource.Tool.Something",
                    new TypeInitializationException("Interop+Sys", new InvalidOperationException())
                )
            )
            .ShouldBeFalse();

    private const bool AgreesToTls = true;
    private const bool AsksForPassword = false;

    // Opens a connection with the keywords added to a server's own, and checks the NpgsqlException that comes.  The
    // server is none for a refusal that comes before anything connects.
    private static async Task ShouldRefuseAsync(bool? agreesToTls, string keywords, string notQuoted, string message)
    {
        using var server = agreesToTls is { } agrees ? new FakePostgresServer(agrees) : null;
        var connectionString = server is null ? keywords : server.ConnectionString + keywords;
        await using var connection = new PostgresConnection(connectionString, NullRunLog.Instance);

        var thrown = await Should.ThrowAsync<NpgsqlException>(() =>
            connection.OpenAsync(TestContext.Current.CancellationToken)
        );

        thrown.Message.ShouldBe(message);
        thrown.Message.ShouldNotContain(notQuoted);
        thrown.InnerException.ShouldBeNull();
    }

    private static PostgresException Server(int position) =>
        new(
            "relation \"users\" does not exist",
            "ERROR",
            "ERROR",
            "42P01",
            detail: "a detail",
            hint: "a hint",
            position: position
        );
}
