using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class HoverHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Hover_on_builtin_function_returns_markdown_signature()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "sum(xs)", version: 1);
        var handler = new ExpreszoHoverHandler(cache);

        Hover? hover = await handler.Handle(
            new HoverParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 1),
            },
            CancellationToken.None
        );

        await Assert.That(hover).IsNotNull();
        string? value = hover!.Contents.MarkupContent?.Value;
        await Assert.That(value).IsNotNull();
        await Assert.That(value!).Contains("sum(array)");
    }

    [Test]
    public async Task Hover_on_operator_returns_operator_metadata()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "1 ?? 2", version: 1);
        var handler = new ExpreszoHoverHandler(cache);

        Hover? hover = await handler.Handle(
            new HoverParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 2),
            },
            CancellationToken.None
        );

        await Assert.That(hover).IsNotNull();
        string value = hover!.Contents.MarkupContent!.Value;
        await Assert.That(value).Contains("Null-coalesce");
    }

    [Test]
    public async Task Hover_on_unknown_identifier_returns_null()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "somethingRandom + 1", version: 1);
        var handler = new ExpreszoHoverHandler(cache);

        Hover? hover = await handler.Handle(
            new HoverParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 2),
            },
            CancellationToken.None
        );

        await Assert.That(hover).IsNull();
    }

    [Test]
    public async Task Hover_on_unopened_document_returns_null()
    {
        var handler = new ExpreszoHoverHandler(new DocumentCache());

        Hover? hover = await handler.Handle(
            new HoverParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
            },
            CancellationToken.None
        );

        await Assert.That(hover).IsNull();
    }
}
