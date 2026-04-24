using Expreszo.Ast;
using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Document-symbol provider: enumerates top-level <see cref="FunctionDef"/>
/// definitions and top-level <c>name = expr</c> assignments reachable from a
/// <see cref="Sequence"/>. Nested statements are not recursed into — Tier 2
/// adds local-scope awareness.
/// </summary>
internal sealed class ExpreszoDocumentSymbolHandler : DocumentSymbolHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoDocumentSymbolHandler(DocumentCache cache) => _cache = cache;

    public override Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc?.Root is null)
        {
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);
        }

        List<DocumentSymbol> symbols = [];
        Collect(doc.Root, doc.LineIndex, symbols);

        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(
            new SymbolInformationOrDocumentSymbolContainer(
                symbols.Select(s => new SymbolInformationOrDocumentSymbol(s))
            )
        );
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(
        DocumentSymbolCapability capability,
        ClientCapabilities clientCapabilities
    ) => new() { DocumentSelector = DocumentSelectorFactory.Expreszo };

    private static void Collect(Node root, LineIndex lineIndex, List<DocumentSymbol> symbols)
    {
        foreach (Node stmt in FlattenStatements(root))
        {
            switch (stmt)
            {
                case FunctionDef fd:
                    symbols.Add(CreateSymbol(fd.Name, SymbolKind.Function, fd.Span, fd.Span, lineIndex, FormatFunction(fd)));
                    break;

                case Binary { Op: "=", Left: Ident id } bin:
                    symbols.Add(CreateSymbol(id.Name, SymbolKind.Variable, bin.Span, id.Span, lineIndex));
                    break;

                case Binary { Op: "=", Left: NameRef nr } bin:
                    symbols.Add(CreateSymbol(nr.Name, SymbolKind.Variable, bin.Span, nr.Span, lineIndex));
                    break;
            }
        }
    }

    /// <summary>
    /// The parser wraps each semicolon group in <c>Paren(Sequence(...))</c>
    /// and nests the tail right-recursively. Flatten that structure back
    /// into a flat statement list so symbol collection sees every top-level
    /// assignment / function definition.
    /// </summary>
    private static IEnumerable<Node> FlattenStatements(Node node)
    {
        switch (node)
        {
            case Paren p:
                foreach (Node child in FlattenStatements(p.Inner))
                {
                    yield return child;
                }
                break;
            case Sequence s:
                foreach (Node stmt in s.Statements)
                {
                    foreach (Node nested in FlattenStatements(stmt))
                    {
                        yield return nested;
                    }
                }
                break;
            default:
                yield return node;
                break;
        }
    }

    private static string FormatFunction(FunctionDef fd) =>
        $"{fd.Name}({string.Join(", ", fd.Params)})";

    private static DocumentSymbol CreateSymbol(
        string name,
        SymbolKind kind,
        TextSpan fullSpan,
        TextSpan selectionSpan,
        LineIndex lineIndex,
        string? detail = null
    )
    {
        return new DocumentSymbol
        {
            Name = name,
            Kind = kind,
            Detail = detail ?? string.Empty,
            Range = ToRange(fullSpan, lineIndex),
            SelectionRange = ToRange(selectionSpan, lineIndex),
        };
    }

    private static Range ToRange(TextSpan span, LineIndex lineIndex)
    {
        (int startLine, int startChar) = lineIndex.OffsetToPosition(span.Start);
        (int endLine, int endChar) = lineIndex.OffsetToPosition(span.End);
        return new Range(startLine, startChar, endLine, endChar);
    }
}
