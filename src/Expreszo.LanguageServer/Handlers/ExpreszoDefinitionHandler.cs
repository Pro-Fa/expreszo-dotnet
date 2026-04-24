using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Goto-definition: resolves the identifier at the cursor to its declaring
/// span. Covers <see cref="FunctionDef"/> targets and top-level
/// <c>name = …</c> assignments; built-in function names route to no
/// definition (the library is the source of truth, not the buffer).
/// </summary>
internal sealed class ExpreszoDefinitionHandler : DefinitionHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoDefinitionHandler(DocumentCache cache) => _cache = cache;

    public override Task<LocationOrLocationLinks?> Handle(
        DefinitionParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc is null)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        int offset = doc.LineIndex.PositionToOffset(
            request.Position.Line,
            request.Position.Character
        );

        string? name = ResolveNameAtOffset(doc.Root, offset);
        if (name is null)
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        SymbolIndex index = SymbolIndex.Build(doc.Root);
        if (!index.Definitions.TryGetValue(name, out ImmutableArray<TextSpan> defs))
        {
            return Task.FromResult<LocationOrLocationLinks?>(null);
        }

        IEnumerable<LocationOrLocationLink> locations = defs.Select(span => new LocationOrLocationLink(
            new Location { Uri = request.TextDocument.Uri, Range = ToRange(span, doc.LineIndex) }
        ));

        return Task.FromResult<LocationOrLocationLinks?>(
            new LocationOrLocationLinks(locations)
        );
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(
        DefinitionCapability capability,
        ClientCapabilities clientCapabilities
    ) => new() { DocumentSelector = DocumentSelectorFactory.Expreszo };

    /// <summary>
    /// Picks the identifier name under the cursor when one is meaningful
    /// for goto. Hovering the callee of a Call, a bare Ident, or a NameRef
    /// (e.g. the LHS of an assignment) all return the name; everything
    /// else returns null.
    /// </summary>
    internal static string? ResolveNameAtOffset(Node root, int offset)
    {
        LocateResult located = AstLocator.Locate(root, offset);
        if (!located.Found)
        {
            return null;
        }

        return located.Deepest switch
        {
            Ident i => i.Name,
            NameRef n => n.Name,
            FunctionDef fd when offset >= fd.Span.Start && offset <= fd.Span.Start + fd.Name.Length =>
                fd.Name,
            _ => null,
        };
    }

    internal static Range ToRange(TextSpan span, LineIndex lineIndex)
    {
        (int startLine, int startChar) = lineIndex.OffsetToPosition(span.Start);
        (int endLine, int endChar) = lineIndex.OffsetToPosition(span.End);
        return new Range(startLine, startChar, endLine, endChar);
    }
}
