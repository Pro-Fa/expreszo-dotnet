using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class CompletionHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Returns_the_builtin_catalogue()
    {
        var handler = new ExpreszoCompletionHandler();

        CompletionList result = await handler.Handle(
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
            },
            CancellationToken.None
        );

        string[] labels = [.. result.Items.Select(i => i.Label)];

        await Assert.That(labels).Contains("sum");
        await Assert.That(labels).Contains("filter");
        await Assert.That(labels).Contains("case");
        // Symbolic operators like '+' are not completion targets.
        await Assert.That(labels).DoesNotContain("+");
    }

    [Test]
    public async Task Function_items_insert_open_paren_for_quick_call()
    {
        var handler = new ExpreszoCompletionHandler();

        CompletionList result = await handler.Handle(
            new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
            },
            CancellationToken.None
        );

        CompletionItem sum = result.Items.Single(i => i.Label == "sum");
        await Assert.That(sum.InsertText).IsEqualTo("sum(");
        await Assert.That(sum.Kind).IsEqualTo(CompletionItemKind.Function);
    }
}
