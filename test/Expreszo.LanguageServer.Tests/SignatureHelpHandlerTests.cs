using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class SignatureHelpHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Returns_signature_for_known_function_call()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "clamp(5, 1, 10)", version: 1);
        var handler = new ExpreszoSignatureHelpHandler(cache);

        SignatureHelp? help = await handler.Handle(
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 6),  // just inside the `(`
            },
            CancellationToken.None
        );

        await Assert.That(help).IsNotNull();
        await Assert.That(help!.Signatures.Count()).IsEqualTo(1);
        SignatureInformation sig = help.Signatures.First();
        await Assert.That(sig.Label).Contains("clamp");
        await Assert.That(sig.Parameters!.Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Active_parameter_advances_with_comma_position()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "clamp(5, 1, 10)", version: 1);
        var handler = new ExpreszoSignatureHelpHandler(cache);

        SignatureHelp? help1 = await handler.Handle(
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 7),  // inside first argument
            },
            CancellationToken.None
        );
        SignatureHelp? help2 = await handler.Handle(
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 10), // inside second argument
            },
            CancellationToken.None
        );
        SignatureHelp? help3 = await handler.Handle(
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 13), // inside third argument
            },
            CancellationToken.None
        );

        await Assert.That(help1!.ActiveParameter).IsEqualTo(0);
        await Assert.That(help2!.ActiveParameter).IsEqualTo(1);
        await Assert.That(help3!.ActiveParameter).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_null_outside_any_call()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "1 + 2", version: 1);
        var handler = new ExpreszoSignatureHelpHandler(cache);

        SignatureHelp? help = await handler.Handle(
            new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
            },
            CancellationToken.None
        );

        await Assert.That(help).IsNull();
    }
}
