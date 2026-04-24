using Expreszo.Ast;
using Expreszo.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Resolves hover content for identifiers, operators, and keywords by
/// looking up <see cref="BuiltinMetadata"/>. Falls back to a tokenizer pass
/// when the AST is unavailable (e.g. the document has a parse error) so
/// hover still works on broken input for the happy cases.
/// </summary>
internal sealed class ExpreszoHoverHandler : HoverHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoHoverHandler(DocumentCache cache) => _cache = cache;

    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc is null)
        {
            return Task.FromResult<Hover?>(null);
        }

        int offset = doc.LineIndex.PositionToOffset(
            request.Position.Line,
            request.Position.Character
        );

        Hover? hover = ResolveFromAst(doc, offset) ?? ResolveFromTokens(doc, offset);
        return Task.FromResult(hover);
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability,
        ClientCapabilities clientCapabilities
    ) => new() { DocumentSelector = DocumentSelectorFactory.Expreszo };

    private static Hover? ResolveFromAst(ExpreszoTextDocument doc, int offset)
    {
        if (doc.Root is null)
        {
            return null;
        }

        LocateResult located = AstLocator.Locate(doc.Root, offset);
        if (!located.Found)
        {
            return null;
        }

        Node node = located.Deepest!;

        // Prefer the containing Call's callee when the cursor sits on an Ident
        // that is being invoked — gives hover for `sum` in `sum(xs)` even
        // though the cursor is on the Ident child of the Call.
        string? name = node switch
        {
            Call { Callee: Ident c } => c.Name,
            Ident i => i.Name,
            NameRef n => n.Name,
            Unary u => u.Op,
            Binary b => b.Op,
            Ternary t => t.Op,
            _ => null,
        };

        if (name is null)
        {
            return null;
        }

        return BuildHover(name, node.Span, doc.LineIndex);
    }

    private static Hover? ResolveFromTokens(ExpreszoTextDocument doc, int offset)
    {
        foreach (Token token in TokenStreamReader.Tokens(doc.Text))
        {
            if (offset < token.Index || offset > token.End)
            {
                continue;
            }

            string name = token.Text;
            if (!BuiltinMetadata.TryGet(name, out _))
            {
                return null;
            }

            var span = new Expreszo.Errors.TextSpan(token.Index, token.End);
            return BuildHover(name, span, doc.LineIndex);
        }

        return null;
    }

    private static Hover? BuildHover(string name, Expreszo.Errors.TextSpan span, LineIndex lineIndex)
    {
        if (!BuiltinMetadata.TryGet(name, out BuiltinEntry? entry))
        {
            return null;
        }

        (int startLine, int startChar) = lineIndex.OffsetToPosition(span.Start);
        (int endLine, int endChar) = lineIndex.OffsetToPosition(span.End);

        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkupContent { Kind = MarkupKind.Markdown, Value = entry.ToMarkdown() }
            ),
            Range = new Range(startLine, startChar, endLine, endChar),
        };
    }
}
