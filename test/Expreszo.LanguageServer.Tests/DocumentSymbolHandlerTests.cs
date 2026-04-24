using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class DocumentSymbolHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Enumerates_top_level_function_definitions()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "double(x) = x * 2; double(21)", version: 1);
        var handler = new ExpreszoDocumentSymbolHandler(cache);

        SymbolInformationOrDocumentSymbolContainer? result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier(Uri) },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
        DocumentSymbol[] symbols =
        [
            .. result!
                .Where(s => s.DocumentSymbol is not null)
                .Select(s => s.DocumentSymbol!),
        ];

        await Assert.That(symbols.Length).IsEqualTo(1);
        await Assert.That(symbols[0].Name).IsEqualTo("double");
        await Assert.That(symbols[0].Kind).IsEqualTo(SymbolKind.Function);
    }

    [Test]
    public async Task Enumerates_top_level_assignments_as_variables()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "a = 1; b = 2; a + b", version: 1);
        var handler = new ExpreszoDocumentSymbolHandler(cache);

        SymbolInformationOrDocumentSymbolContainer? result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier(Uri) },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
        string[] names =
        [
            .. result!
                .Where(s => s.DocumentSymbol is not null)
                .Select(s => s.DocumentSymbol!.Name),
        ];

        await Assert.That(names).Contains("a");
        await Assert.That(names).Contains("b");
    }

    [Test]
    public async Task Returns_empty_container_for_unparseable_document()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "1 +", version: 1);
        var handler = new ExpreszoDocumentSymbolHandler(cache);

        SymbolInformationOrDocumentSymbolContainer? result = await handler.Handle(
            new DocumentSymbolParams { TextDocument = new TextDocumentIdentifier(Uri) },
            CancellationToken.None
        );

        // Error-recovering parse still yields a (minimal) AST, so the handler
        // returns an empty container rather than null. Null stays reserved
        // for the "no document open" case.
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count()).IsEqualTo(0);
    }
}
