using Expreszo.Analysis;
using Expreszo.Ast;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Signature-help provider. Finds the innermost <see cref="Call"/> whose
/// span contains the cursor, pulls parameter metadata for the callee from
/// <see cref="BuiltinMetadata"/>, and computes the active parameter index
/// from the call's argument list.
/// </summary>
internal sealed class ExpreszoSignatureHelpHandler : SignatureHelpHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoSignatureHelpHandler(DocumentCache cache) => _cache = cache;

    public override Task<SignatureHelp?> Handle(
        SignatureHelpParams request,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(request.TextDocument.Uri);
        if (doc is null)
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        int offset = doc.LineIndex.PositionToOffset(
            request.Position.Line,
            request.Position.Character
        );

        LocateResult located = AstLocator.Locate(doc.Root, offset);
        Call? call = FindEnclosingCall(located, offset);
        if (call is null)
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        if (call.Callee is not Ident callee || !BuiltinMetadata.TryGet(callee.Name, out BuiltinEntry? entry))
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        if (entry.Parameters.IsDefaultOrEmpty)
        {
            return Task.FromResult<SignatureHelp?>(null);
        }

        int activeParameter = ResolveActiveParameter(call, offset, entry.Parameters.Length);

        var signature = new SignatureInformation
        {
            Label = entry.Signature,
            Documentation = new StringOrMarkupContent(
                new MarkupContent { Kind = MarkupKind.Markdown, Value = entry.Summary }
            ),
            Parameters = new Container<ParameterInformation>(
                entry.Parameters.Select(p => new ParameterInformation
                {
                    Label = new ParameterInformationLabel(p),
                })
            ),
        };

        return Task.FromResult<SignatureHelp?>(
            new SignatureHelp
            {
                Signatures = new Container<SignatureInformation>(signature),
                ActiveSignature = 0,
                ActiveParameter = activeParameter,
            }
        );
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability,
        ClientCapabilities clientCapabilities
    ) =>
        new()
        {
            DocumentSelector = DocumentSelectorFactory.Expreszo,
            TriggerCharacters = new Container<string>("(", ","),
            RetriggerCharacters = new Container<string>(","),
        };

    /// <summary>
    /// Walks the ancestor chain of <paramref name="located"/> from innermost
    /// outwards and returns the first <see cref="Call"/> whose argument
    /// region contains <paramref name="offset"/>. Using the chain avoids the
    /// edge case where the cursor sits on the callee identifier itself, in
    /// which case the locator's deepest node is the <see cref="Ident"/> —
    /// we want its parent <see cref="Call"/> only if the cursor is inside
    /// the argument list.
    /// </summary>
    private static Call? FindEnclosingCall(LocateResult located, int offset)
    {
        for (int i = located.Chain.Length - 1; i >= 0; i--)
        {
            if (located.Chain[i] is Call c && offset > c.Callee.Span.End && offset <= c.Span.End)
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines which parameter slot the cursor is in. Preferred path:
    /// inspect the call's parsed argument list and pick the argument whose
    /// span contains the offset. Fallback: return the last slot when the
    /// cursor is past every argument (the user typed a trailing comma and
    /// hasn't filled in the next argument yet).
    /// </summary>
    private static int ResolveActiveParameter(Call call, int offset, int paramCount)
    {
        if (paramCount == 0)
        {
            return 0;
        }

        for (int i = 0; i < call.Args.Length; i++)
        {
            Node arg = call.Args[i];
            if (offset >= arg.Span.Start && offset <= arg.Span.End)
            {
                return Math.Min(i, paramCount - 1);
            }
        }

        // Cursor isn't inside any parsed argument — it's either before the
        // first argument or after the last. Pick the next slot after the
        // most-recent argument that ends at or before the cursor.
        int active = 0;
        for (int i = 0; i < call.Args.Length; i++)
        {
            if (call.Args[i].Span.End <= offset)
            {
                active = i + 1;
            }
        }

        return Math.Min(active, paramCount - 1);
    }
}
