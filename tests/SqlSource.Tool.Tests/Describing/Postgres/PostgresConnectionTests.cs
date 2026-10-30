using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Npgsql;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the connection refuses before it connects or while it reads what a connection string names, and the fixed
// text each gives.  What an answer makes of what the driver threw is in ConnectionErrorsTests.
public sealed class PostgresConnectionTests
{
    // Port 1 proves that nothing connected: a connect would fail with the driver's own message.  The keyword is read
    // by the driver's builder, so its case is the builder's.
    [Theory]
    [InlineData("Host=127.0.0.1;Port=1;Multiplexing=true")]
    [InlineData("host=127.0.0.1;port=1;multiplexing=True")]
    public async Task OpenAsync_Multiplexing_ThrowsRefusedBeforeConnecting(string connectionString)
    {
        await using var connection = new PostgresConnection(connectionString, NullRunLog.Instance);

        var thrown = await Should.ThrowAsync<PostgresConnection.UnusableException>(() =>
            connection.OpenAsync(TestContext.Current.CancellationToken)
        );

        thrown.Message.ShouldBe(ConnectionErrors.MultiplexingRefused);
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

        thrown.Message.ShouldNotBe(ConnectionErrors.MultiplexingRefused);
    }

    // What the driver throws, when a connection string names a file, is no NpgsqlException: it reads the file when it
    // has a server to talk to, a client certificate once the server agrees to TLS and a password file once the
    // server asks for a password, so a closed port proves nothing and these tests talk to FakePostgresServer.
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
            ConnectionErrors.UnreadableFile
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
            ConnectionErrors.UnreadableFile
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
            ConnectionErrors.UnreadableFile
        );
    }

    // On Unix the system says "denied" for a folder where a file is read, which is also what a file without the
    // right to read says.
    [Fact]
    public async Task OpenAsync_PasswordFileThatIsAFolder_ThrowsTheFixedMessage()
    {
        PasswordFiles.SkipUnlessRead();
        using var folder = new TempFolder();
        var path = folder.CreateFolder("a-folder");

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            path,
            ConnectionErrors.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_PasswordFileWithAPathThatIsTooLong_ThrowsTheFixedMessage()
    {
        PasswordFiles.SkipUnlessRead();
        var path = new string('p', 40_000);

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            path,
            ConnectionErrors.UnreadableFile
        );
    }

    [Fact]
    public async Task OpenAsync_ClientCertificateThatIsNoCertificate_ThrowsTheFixedMessage()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("certificate.pem", Secrets.Marker + " is no certificate");

        await ShouldRefuseAsync(
            AgreesToTls,
            $"SslMode=Require;SslCertificate={path}",
            Secrets.Marker,
            ConnectionErrors.UnusableFile
        );
    }

    [Fact]
    public async Task OpenAsync_PasswordFileWithALineThatIsNotAnEntry_ThrowsTheFixedMessage()
    {
        PasswordFiles.SkipUnlessRead();
        using var folder = new TempFolder();
        var path = folder.WriteFile("passfile", Secrets.Marker + " is no entry");

        await ShouldRefuseAsync(
            AsksForPassword,
            $"SslMode=Disable;Passfile={path}",
            Secrets.Marker,
            ConnectionErrors.UnusableFile
        );
    }

    // The driver refuses it when it builds the data source, before anything connects: the port 1 proves it.
    [Fact]
    public async Task OpenAsync_TargetSessionAttributesForOneHost_ThrowsTheFixedMessage() =>
        await ShouldRefuseAsync(
            null,
            "Host=127.0.0.1;Port=1;Target Session Attributes=primary",
            "primary",
            ConnectionErrors.UnsupportedSettings
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

    // Every error the describer throws of its own is an UnusableException, which AnswerAsync passes as it is: an
    // NpgsqlException or a PostgresException made here would be answered as the driver's or the server's, by a text
    // that is not the one meant.  The tool's source is compiled so that a creation is found by its type, however it is
    // written.
    [Fact]
    public void DescribingPostgres_EveryFile_ConstructsNoNpgsqlException()
    {
        var tool = RepositoryRoot.Path("src", "SqlSource.Tool");
        var postgres = Path.Combine(tool, "Describing", "Postgres") + Path.DirectorySeparatorChar;
        var files = Directory
            .EnumerateFiles(tool, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !IsBuildOutput(file))
            .ToList();

        var created = CreatedTypes(files.Select(static file => (file, File.ReadAllText(file))))
            .Where(creation => creation.File.StartsWith(postgres, StringComparison.Ordinal))
            .ToList();

        // The compilation binds the describer's types: its own exception is found where it is thrown.
        created.ShouldContain(creation =>
            creation.Type == "SqlSource.Tool.Describing.Postgres.PostgresConnection.UnusableException"
        );
        created
            .Where(static creation => DriverExceptions.Contains(creation.Type))
            .Select(static creation => $"{Path.GetFileName(creation.File)}: {creation.Type}")
            .ShouldBeEmpty();
    }

    // Each way of writing a creation that the check above must find, and the base call of a subclass, which is none.
    [Theory]
    [InlineData("throw new NpgsqlException(text);", "Npgsql.NpgsqlException")]
    [InlineData("NpgsqlException e = new(text); throw e;", "Npgsql.NpgsqlException")]
    [InlineData("throw new Npgsql.NpgsqlException(text);", "Npgsql.NpgsqlException")]
    [InlineData("throw new PostgresException(text, \"ERROR\", \"ERROR\", \"XX000\");", "Npgsql.PostgresException")]
    public void CreatedTypes_EachFormOfACreation_IsFoundByItsType(string statement, string type) =>
        CreatedTypes([("C.cs", $"using Npgsql; static class C {{ static void M(string text) {{ {statement} }} }}")])
            .Select(static creation => creation.Type)
            .ShouldBe([type]);

    [Fact]
    public void CreatedTypes_BaseCallOfASubclass_IsNone() =>
        CreatedTypes([("C.cs", "using Npgsql; sealed class E(string text) : NpgsqlException(text);")]).ShouldBeEmpty();

    private static readonly string[] DriverExceptions = ["Npgsql.NpgsqlException", "Npgsql.PostgresException"];

    // The type of every object that the sources create, with the file it is created in.  The references are the
    // assemblies of the test run, but the tool's own, which the sources stand for.
    private static IEnumerable<(string File, string Type)> CreatedTypes(IEnumerable<(string Path, string Text)> sources)
    {
        var trees = sources
            .Select(static source => CSharpSyntaxTree.ParseText(source.Text, path: source.Path))
            .ToList();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(static path => Path.GetFileName(path) != "SqlSource.Tool.dll")
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Scan",
            trees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(node).Type is { TypeKind: not TypeKind.Error } type)
                {
                    yield return (tree.FilePath, type.ToDisplayString());
                }
            }
        }
    }

    private static bool IsBuildOutput(string file) =>
        file.Split(Path.DirectorySeparatorChar).Any(static part => part is "bin" or "obj");

    private const bool AgreesToTls = true;
    private const bool AsksForPassword = false;

    // Opens a connection with the keywords added to a server's own, and checks the describer's own exception that
    // comes.  The server is none for a refusal that comes before anything connects.
    private static async Task ShouldRefuseAsync(bool? agreesToTls, string keywords, string notQuoted, string message)
    {
        using var server = agreesToTls is { } agrees ? new FakePostgresServer(agrees) : null;
        var connectionString = server is null ? keywords : server.ConnectionString + keywords;
        await using var connection = new PostgresConnection(connectionString, NullRunLog.Instance);

        var thrown = await Should.ThrowAsync<PostgresConnection.UnusableException>(() =>
            connection.OpenAsync(TestContext.Current.CancellationToken)
        );

        thrown.Message.ShouldBe(message);
        thrown.Message.ShouldNotContain(notQuoted);
        thrown.InnerException.ShouldBeNull();
    }
}
