using System.Collections.Concurrent;
using System.Collections.Immutable;
using Expreszo.Analysis;
using Expreszo.Errors;
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
        string source = text ?? string.Empty;
        ParseResult parse = _parser.TryParse(source);
        TypeInference inference = TypeInference.Run(parse.Expression.Root);
        ImmutableArray<ExpressionException> semantic = TypeValidator.Validate(
            parse.Expression.Root,
            inference,
            source
        );

        ImmutableArray<ExpressionException> errors = Concat(parse.Errors, semantic);

        var doc = new ExpreszoTextDocument(source, version, parse.Expression.Root, errors);
        _documents[uri] = doc;
        return doc;
    }

    private static ImmutableArray<ExpressionException> Concat(
        ImmutableArray<ExpressionException> a,
        ImmutableArray<ExpressionException> b
    )
    {
        if (a.IsDefaultOrEmpty)
        {
            return b.IsDefault ? [] : b;
        }
        if (b.IsDefaultOrEmpty)
        {
            return a;
        }

        var builder = ImmutableArray.CreateBuilder<ExpressionException>(a.Length + b.Length);
        builder.AddRange(a);
        builder.AddRange(b);
        return builder.ToImmutable();
    }

    /// <summary>Removes the entry for <paramref name="uri"/>.</summary>
    public void Remove(DocumentUri uri) => _documents.TryRemove(uri, out _);

    /// <summary>Looks up the current document for <paramref name="uri"/>; returns null if the URI is not open.</summary>
    public ExpreszoTextDocument? TryGet(DocumentUri uri) =>
        _documents.TryGetValue(uri, out ExpreszoTextDocument? doc) ? doc : null;
}
