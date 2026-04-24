using Expreszo.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Tests;

public class RenameHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///test.zo");

    [Test]
    public async Task Renames_every_occurrence_of_a_user_name()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "double(x) = x * 2; double(21); double(7)", version: 1);
        var handler = new ExpreszoRenameHandler(cache);

        WorkspaceEdit? edit = await handler.Handle(
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
                NewName = "twice",
            },
            CancellationToken.None
        );

        await Assert.That(edit).IsNotNull();
        await Assert.That(edit!.Changes).IsNotNull();
        IEnumerable<TextEdit> edits = edit.Changes![Uri];
        await Assert.That(edits.Count()).IsEqualTo(3);
        await Assert.That(edits.All(e => e.NewText == "twice")).IsTrue();
    }

    [Test]
    public async Task Refuses_to_rename_builtins()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "sum(xs)", version: 1);
        var handler = new ExpreszoRenameHandler(cache);

        WorkspaceEdit? edit = await handler.Handle(
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 1),
                NewName = "total",
            },
            CancellationToken.None
        );

        await Assert.That(edit).IsNull();
    }

    [Test]
    public async Task Refuses_invalid_identifier_as_new_name()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "x = 1; x + 1", version: 1);
        var handler = new ExpreszoRenameHandler(cache);

        WorkspaceEdit? edit = await handler.Handle(
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier(Uri),
                Position = new Position(0, 0),
                NewName = "1bad",
            },
            CancellationToken.None
        );

        await Assert.That(edit).IsNull();
    }
}
