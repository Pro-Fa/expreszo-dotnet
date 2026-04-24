using Expreszo.Analysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Completion provider — static catalogue of built-in functions, named
/// operators, and keywords. Scope-aware completion (lambda/function params,
/// local assignments) lands in Tier 2 once the parser can produce partial
/// ASTs from broken input.
/// </summary>
internal sealed class ExpreszoCompletionHandler : CompletionHandlerBase
{
    private static readonly CompletionList CatalogueCompletions =
        new(BuildItems(), isIncomplete: false);

    public override Task<CompletionList> Handle(
        CompletionParams request,
        CancellationToken cancellationToken
    ) => Task.FromResult(CatalogueCompletions);

    public override Task<CompletionItem> Handle(
        CompletionItem request,
        CancellationToken cancellationToken
    ) => Task.FromResult(request);

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability,
        ClientCapabilities clientCapabilities
    ) =>
        new()
        {
            DocumentSelector = DocumentSelectorFactory.Expreszo,
            ResolveProvider = false,
            TriggerCharacters = new Container<string>(".", "(", ","),
        };

    private static IEnumerable<CompletionItem> BuildItems()
    {
        foreach (BuiltinEntry entry in BuiltinMetadata.Entries)
        {
            // Symbolic operators (`+`, `==`, `??`) are typed as-is, not picked
            // from a completion menu; skipping them keeps the list focused.
            if (entry.Kind == BuiltinKind.Operator && !IsWordy(entry.Name))
            {
                continue;
            }

            yield return new CompletionItem
            {
                Label = entry.Name,
                Kind = ToItemKind(entry.Kind),
                Detail = entry.Signature,
                Documentation = new StringOrMarkupContent(
                    new MarkupContent { Kind = MarkupKind.Markdown, Value = entry.Summary }
                ),
                InsertText = entry.Kind == BuiltinKind.Function ? $"{entry.Name}(" : entry.Name,
            };
        }
    }

    private static bool IsWordy(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_');

    private static CompletionItemKind ToItemKind(BuiltinKind kind) =>
        kind switch
        {
            BuiltinKind.Function => CompletionItemKind.Function,
            BuiltinKind.Operator => CompletionItemKind.Operator,
            BuiltinKind.Keyword => CompletionItemKind.Keyword,
            _ => CompletionItemKind.Text,
        };
}
