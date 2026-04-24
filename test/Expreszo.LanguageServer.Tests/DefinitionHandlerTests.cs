using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class DefinitionHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Goto_definition_resolves_user_function_call_site()
    {
        var cache = new DocumentCache();
        // `double` is defined on line 1; used on line 2. Positions are 0-based.
        cache.Update(Uri, "double(x) = x * 2; double(21)", version: 1);
        var handler = new ExpreszoDefinitionHandler(cache);

        LocationOrLocationLinks? result = await handler.Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 21), // on the `double` call
            },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
        Location loc = result!.Single().Location!;
        // Definition is at offset 0..6 ("double").
        await Assert.That(loc.Range.Start.Character).IsEqualTo(0);
        await Assert.That(loc.Range.End.Character).IsEqualTo(6);
    }

    [Test]
    public async Task Goto_definition_returns_null_for_builtins()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "sum(xs)", version: 1);
        var handler = new ExpreszoDefinitionHandler(cache);

        LocationOrLocationLinks? result = await handler.Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 1),  // on `sum`
            },
            CancellationToken.None
        );

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Goto_definition_resolves_top_level_assignments()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "x = 10; x + 1", version: 1);
        var handler = new ExpreszoDefinitionHandler(cache);

        LocationOrLocationLinks? result = await handler.Handle(
            new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 8), // on the second `x`
            },
            CancellationToken.None
        );

        await Assert.That(result).IsNotNull();
    }
}
