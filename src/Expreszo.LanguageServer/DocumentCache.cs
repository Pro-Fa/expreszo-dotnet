using System.Collections.Concurrent;
using Expreszo.Ast;
using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Expreszo.LanguageServer;

/// <summary>
/// Stores one <see cref="ExpreszoTextDocument"/> per open URI and re-parses
/// the buffer on every change. The cache owns the single <see cref="Parser"/>
/// instance — parsers are immutable and safe for concurrent use.
/// </summary>
internal sealed class DocumentCache
{
    private readonly ConcurrentDictionary<DocumentUri, ExpreszoTextDocument> _documents = new();
    private readonly Parser _parser = new();

    /// <summary>
    /// Parses <paramref name="text"/> and stores the resulting state under
    /// <paramref name="uri"/>. Returns the stored document so callers can
    /// publish diagnostics or react to the new state without a second lookup.
    /// </summary>
    public ExpreszoTextDocument Update(DocumentUri uri, string text, int version)
    {
        Node? root = null;
        ExpressionException? error = null;

        try
        {
            Expression expr = _parser.Parse(text ?? string.Empty);
            root = expr.Root;
        }
        catch (ExpressionException ex)
        {
            error = ex;
        }

        var doc = new ExpreszoTextDocument(text ?? string.Empty, version, root, error);
        _documents[uri] = doc;
        return doc;
    }

    /// <summary>Removes the entry for <paramref name="uri"/>.</summary>
    public void Remove(DocumentUri uri) => _documents.TryRemove(uri, out _);

    /// <summary>Looks up the current document for <paramref name="uri"/>; returns null if the URI is not open.</summary>
    public ExpreszoTextDocument? TryGet(DocumentUri uri) =>
        _documents.TryGetValue(uri, out ExpreszoTextDocument? doc) ? doc : null;
}
