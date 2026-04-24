using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Expreszo.LanguageServer;

/// <summary>
/// Stores one <see cref="ExpreszoTextDocument"/> per open URI and re-parses
/// the buffer on every change via <see cref="Parser.TryParse(string)"/>. The
/// cache owns a single <see cref="Parser"/> instance — parsers are immutable
/// and safe for concurrent use.
/// </summary>
internal sealed class DocumentCache
{
    private readonly ConcurrentDictionary<DocumentUri, ExpreszoTextDocument> _documents = new();
    private readonly Parser _parser = new();

    /// <summary>
    /// Parses <paramref name="text"/> tolerantly and stores the resulting
    /// state under <paramref name="uri"/>. Always returns a populated
    /// document — syntax errors in one statement don't eliminate the whole
    /// AST.
    /// </summary>
    public ExpreszoTextDocument Update(DocumentUri uri, string text, int version)
    {
        ParseResult result = _parser.TryParse(text ?? string.Empty);

        var doc = new ExpreszoTextDocument(
            text ?? string.Empty,
            version,
            result.Expression.Root,
            result.Errors
        );
        _documents[uri] = doc;
        return doc;
    }

    /// <summary>Removes the entry for <paramref name="uri"/>.</summary>
    public void Remove(DocumentUri uri) => _documents.TryRemove(uri, out _);

    /// <summary>Looks up the current document for <paramref name="uri"/>; returns null if the URI is not open.</summary>
    public ExpreszoTextDocument? TryGet(DocumentUri uri) =>
        _documents.TryGetValue(uri, out ExpreszoTextDocument? doc) ? doc : null;
}
