using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Owns the document lifecycle: on <c>didOpen</c> / <c>didChange</c>, (re)parse
/// the buffer via <see cref="DocumentCache"/> and publish diagnostics. On
/// <c>didClose</c>, drop the cached entry and clear diagnostics.
/// </summary>
internal sealed class ExpreszoTextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private const string LanguageId = "expreszo";

    private readonly ILanguageServerFacade _server;
    private readonly DocumentCache _cache;

    public ExpreszoTextDocumentSyncHandler(ILanguageServerFacade server, DocumentCache cache)
    {
        _server = server;
        _cache = cache;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) =>
        new(uri, LanguageId);

    public override Task<Unit> Handle(
        DidOpenTextDocumentParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument doc = _cache.Update(
            request.TextDocument.Uri,
            request.TextDocument.Text,
            request.TextDocument.Version ?? 0
        );
        PublishDiagnostics(request.TextDocument.Uri, doc);
        return Unit.Task;
    }

    public override Task<Unit> Handle(
        DidChangeTextDocumentParams request,
        CancellationToken cancellationToken
    )
    {
        // We advertise Full sync, so a single Content-Changes element holds the
        // complete new buffer text.
        string text = request.ContentChanges.LastOrDefault()?.Text ?? string.Empty;
        int version = request.TextDocument.Version ?? 0;

        ExpreszoTextDocument doc = _cache.Update(request.TextDocument.Uri, text, version);
        PublishDiagnostics(request.TextDocument.Uri, doc);
        return Unit.Task;
    }

    public override Task<Unit> Handle(
        DidSaveTextDocumentParams request,
        CancellationToken cancellationToken
    ) => Unit.Task;

    public override Task<Unit> Handle(
        DidCloseTextDocumentParams request,
        CancellationToken cancellationToken
    )
    {
        _cache.Remove(request.TextDocument.Uri);
        _server.TextDocument.PublishDiagnostics(
            new PublishDiagnosticsParams
            {
                Uri = request.TextDocument.Uri,
                Diagnostics = new Container<Diagnostic>(),
            }
        );
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities
    ) =>
        new()
        {
            DocumentSelector = DocumentSelectorFactory.Expreszo,
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = false },
        };

    private void PublishDiagnostics(DocumentUri uri, ExpreszoTextDocument doc)
    {
        IEnumerable<Diagnostic> diagnostics = DiagnosticMapper.Map(doc.Errors, doc.LineIndex);

        _server.TextDocument.PublishDiagnostics(
            new PublishDiagnosticsParams
            {
                Uri = uri,
                Version = doc.Version,
                Diagnostics = new Container<Diagnostic>(diagnostics),
            }
        );
    }
}
