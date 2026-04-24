using Expreszo.LanguageServer.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Expreszo.LanguageServer;

/// <summary>
/// Boots an OmniSharp language server pre-wired with ExpresZo handlers.
/// Host applications call <see cref="RunAsync"/> to serve over a given
/// pair of streams (stdio in the default host, pipes in tests).
/// </summary>
public static class ExpreszoLanguageServer
{
    /// <summary>
    /// Runs the server until shutdown, reading LSP messages from
    /// <paramref name="input"/> and writing them to <paramref name="output"/>.
    /// </summary>
    /// <param name="input">Inbound LSP message stream (e.g. stdin).</param>
    /// <param name="output">Outbound LSP message stream (e.g. stdout).</param>
    /// <param name="cancellationToken">Cancellation token for the server run.</param>
    public static async Task RunAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer
            .From(options =>
            {
                options
                    .WithInput(input)
                    .WithOutput(output)
                    .ConfigureLogging(logging =>
                        logging
                            .AddLanguageProtocolLogging()
                            .SetMinimumLevel(LogLevel.Information)
                    )
                    .WithServices(services =>
                    {
                        services.AddSingleton<DocumentCache>();
                    })
                    .WithHandler<ExpreszoTextDocumentSyncHandler>()
                    .WithHandler<ExpreszoHoverHandler>()
                    .WithHandler<ExpreszoCompletionHandler>()
                    .WithHandler<ExpreszoSignatureHelpHandler>()
                    .WithHandler<ExpreszoDocumentSymbolHandler>()
                    .WithHandler<ExpreszoDefinitionHandler>()
                    .WithHandler<ExpreszoReferencesHandler>()
                    .WithHandler<ExpreszoRenameHandler>()
                    .WithHandler<ExpreszoSemanticTokensHandler>();
            }, cancellationToken)
            .ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
