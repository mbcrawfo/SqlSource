using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Tests;

// A listener on the loopback that speaks the first words of the PostgreSQL protocol and no more, for a test of what
// the driver does before it has a server to talk to: it reads a certificate when the server agrees to TLS, and a
// password file when the server asks for a password.  It never completes a handshake, and a connection to it ends
// in the exception the test provokes.
internal sealed class FakePostgresServer : IDisposable
{
    private const int SslRequest = 80877103;
    private const int GssEncryptionRequest = 80877104;
    private const int AuthenticationCleartextPassword = 3;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly bool _agreesToTls;
    private readonly Task _accepting;

    public FakePostgresServer(bool agreesToTls)
    {
        _agreesToTls = agreesToTls;
        _listener.Start();
        _accepting = Task.Run(AcceptAsync, _stop.Token);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    // A connection string for the server, to which a test adds the keywords it tries.
    public string ConnectionString => $"Host=127.0.0.1;Port={Port};Username=app;Database=app;Timeout=10;";

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Dispose();
        try
        {
            _accepting.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Stopping is how the loop ends.
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception exception)
                when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var header = new byte[8];
                while (true)
                {
                    await stream.ReadExactlyAsync(header, _stop.Token);
                    var length = BinaryPrimitives.ReadInt32BigEndian(header);
                    var code = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4));
                    if (code == GssEncryptionRequest || (code == SslRequest && !_agreesToTls))
                    {
                        await stream.WriteAsync("N"u8.ToArray(), _stop.Token);
                    }
                    else if (code == SslRequest)
                    {
                        await stream.WriteAsync("S"u8.ToArray(), _stop.Token);
                        await HoldAsync(stream);
                        return;
                    }
                    else
                    {
                        // The startup message: the rest of it is read, and the server asks for a cleartext password.
                        await stream.ReadExactlyAsync(new byte[length - header.Length], _stop.Token);
                        var request = new byte[9];
                        request[0] = (byte)'R';
                        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(1), 8);
                        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(5), AuthenticationCleartextPassword);
                        await stream.WriteAsync(request, _stop.Token);
                        await HoldAsync(stream);
                        return;
                    }
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException)
            {
                // The driver closed the connection, or the test is over.
            }
        }
    }

    // Reads until the driver ends the connection.
    private async Task HoldAsync(NetworkStream stream)
    {
        var sink = new byte[256];
        var read = await stream.ReadAsync(sink, _stop.Token);
        while (read > 0)
        {
            read = await stream.ReadAsync(sink, _stop.Token);
        }
    }
}
