using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class ReferencesHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Returns_every_use_of_the_name()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "double(x) = x * 2; double(21); double(7)", version: 1);
        var handler = new ExpreszoReferencesHandler(cache);

        LocationContainer? result = await handler.Handle(
            new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 21), // first call site
                Context = new ReferenceContext { IncludeDeclaration = true },
            },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
        // 1 declaration + 2 call-site idents = 3 locations.
        await Assert.That(result!.Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Excludes_declaration_when_requested()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "double(x) = x * 2; double(21); double(7)", version: 1);
        var handler = new ExpreszoReferencesHandler(cache);

        LocationContainer? result = await handler.Handle(
            new ReferenceParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 21),
                Context = new ReferenceContext { IncludeDeclaration = false },
            },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
        // Only the two Ident call sites.
        await Assert.That(result!.Count()).IsEqualTo(2);
    }
}
