using System.Collections.Immutable;
using Expreszo.Analysis;
using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Rename: rewrites every occurrence of the identifier under the cursor in
/// the current document. Rejects renames targeting built-ins — their
/// canonical names live in the library, not the buffer — and rejects new
/// names that aren't valid ExpresZo identifiers.
/// </summary>
internal sealed class ExpreszoRenameHandler : RenameHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoRenameHandler(DocumentCache cache) => _cache = cache;

    public override Task<WorkspaceEdit?> Handle(
        RenameParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc is null)
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        int offset = doc.LineIndex.PositionToOffset(
            request.Position.Line,
            request.Position.Character
        );

        string? name = ExpreszoDefinitionHandler.ResolveNameAtOffset(doc.Root, offset);
        if (name is null || BuiltinMetadata.TryGet(name, out _))
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        string newName = request.NewName?.Trim() ?? string.Empty;
        if (!IsValidIdentifier(newName))
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        SymbolIndex index = SymbolIndex.Build(doc.Root);
        ImmutableArray<TextSpan> spans = index.AllOccurrences(name);
        if (spans.Length == 0)
        {
            return Task.FromResult<WorkspaceEdit?>(null);
        }

        IEnumerable<TextEdit> edits = spans.Select(span => new TextEdit
        {
            Range = ExpreszoDefinitionHandler.ToRange(span, doc.LineIndex),
            NewText = newName,
        });

        var workspaceEdit = new WorkspaceEdit
        {
            Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
            {
                [request.TextDocument.Uri] = edits,
            },
        };

        return Task.FromResult<WorkspaceEdit?>(workspaceEdit);
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(
        RenameCapability capability,
        ClientCapabilities clientCapabilities
    ) =>
        new()
        {
            DocumentSelector = DocumentSelectorFactory.Expreszo,
            PrepareProvider = false,
        };

    /// <summary>
    /// Mirrors the tokenizer's identifier rule: leading letter or underscore,
    /// remaining characters letters / digits / underscore, and not a reserved
    /// keyword or built-in literal.
    /// </summary>
    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return !BuiltinMetadata.IsKeyword(name);
    }
}
