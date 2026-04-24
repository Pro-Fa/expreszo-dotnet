using System.Text;

namespace Expreszo.LanguageServer.Tests;

/// <summary>
/// Runs the full server bootstrap on paired memory pipes. Proves the DI
/// graph resolves, handlers register, and an <c>initialize</c> round-trip
/// completes — a strictly stronger check than unit-testing handlers in
/// isolation.
/// </summary>
public class ServerSmokeTests
{
    [Test]
    [Timeout(15000)]
    public async Task Server_responds_to_initialize_and_shuts_down()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Task serverTask = ExpreszoLanguageServer.RunAsync(
            clientToServer.Reader,
            serverToClient.Writer,
            cts.Token
        );

        // Build the initialize request. Minimal client capabilities are enough
        // to satisfy OmniSharp's request pipeline.
        string initialize = """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"processId":null,"rootUri":null,"capabilities":{},"trace":"off"}}
            """;
        await SendMessage(clientToServer.Writer, initialize);

        string response = await ReadMessage(serverToClient.Reader, cts.Token);
        await Assert.That(response).Contains("\"id\":1");
        await Assert.That(response).Contains("capabilities");

        // Notify initialized.
        await SendMessage(
            clientToServer.Writer,
            """{"jsonrpc":"2.0","method":"initialized","params":{}}"""
        );

        // Shutdown + exit to drain the server.
        await SendMessage(
            clientToServer.Writer,
            """{"jsonrpc":"2.0","id":2,"method":"shutdown"}"""
        );
        _ = await ReadMessage(serverToClient.Reader, cts.Token);

        await SendMessage(clientToServer.Writer, """{"jsonrpc":"2.0","method":"exit"}""");

        await Task.WhenAny(serverTask, Task.Delay(5000, cts.Token));
        await Assert.That(serverTask.IsCompletedSuccessfully).IsTrue();
    }

    private static async Task SendMessage(Stream stream, string payload)
    {
        byte[] body = Encoding.UTF8.GetBytes(payload);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadMessage(Stream stream, CancellationToken ct)
    {
        int contentLength = 0;
        var headerBuffer = new StringBuilder();
        byte[] one = new byte[1];

        while (true)
        {
            int read = await stream.ReadAsync(one.AsMemory(0, 1), ct);
            if (read == 0)
            {
                throw new EndOfStreamException("Server closed the stream before sending a header.");
            }

            headerBuffer.Append((char)one[0]);
            string so_far = headerBuffer.ToString();
            if (so_far.EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                foreach (string line in so_far.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(
                            line["Content-Length:".Length..].Trim(),
                            System.Globalization.CultureInfo.InvariantCulture
                        );
                    }
                }
                break;
            }
        }

        byte[] body = new byte[contentLength];
        int offset = 0;
        while (offset < contentLength)
        {
            int read = await stream.ReadAsync(body.AsMemory(offset, contentLength - offset), ct);
            if (read == 0)
            {
                throw new EndOfStreamException("Server closed the stream mid-body.");
            }
            offset += read;
        }

        return Encoding.UTF8.GetString(body);
    }

    /// <summary>
    /// Minimal duplex stream pair. One end writes, the other reads; backed
    /// by an <see cref="AnonymousPipeServerStream"/>-style blocking queue.
    /// </summary>
    private sealed class Pipe
    {
        private readonly System.IO.Pipelines.Pipe _pipe = new();

        public Stream Reader => _pipe.Reader.AsStream();
        public Stream Writer => _pipe.Writer.AsStream();
    }
}
