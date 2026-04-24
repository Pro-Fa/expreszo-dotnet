using System.Collections.Immutable;
using Expreszo.Analysis;
using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Find-references: returns every Ident / declaration span in the buffer
/// that matches the name under the cursor. Declaration inclusion is
/// controlled by the LSP <c>includeDeclaration</c> flag.
/// </summary>
internal sealed class ExpreszoReferencesHandler : ReferencesHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoReferencesHandler(DocumentCache cache) => _cache = cache;

    public override Task<LocationContainer?> Handle(
        ReferenceParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc is null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        int offset = doc.LineIndex.PositionToOffset(
            request.Position.Line,
            request.Position.Character
        );

        string? name = ExpreszoDefinitionHandler.ResolveNameAtOffset(doc.Root, offset);
        if (name is null)
        {
            return Task.FromResult<LocationContainer?>(null);
        }

        SymbolIndex index = SymbolIndex.Build(doc.Root);
        IEnumerable<TextSpan> spans = request.Context?.IncludeDeclaration == true
            ? index.AllOccurrences(name)
            : index.References.TryGetValue(name, out ImmutableArray<TextSpan> refs)
                ? refs
                : [];

        List<Location> locations = [.. spans.Select(span => new Location
        {
            Uri = request.TextDocument.Uri,
            Range = ExpreszoDefinitionHandler.ToRange(span, doc.LineIndex),
        })];

        return Task.FromResult<LocationContainer?>(LocationContainer.From(locations));
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(
        ReferenceCapability capability,
        ClientCapabilities clientCapabilities
    ) => new() { DocumentSelector = DocumentSelectorFactory.Expreszo };
}
