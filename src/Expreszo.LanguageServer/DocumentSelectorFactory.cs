using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer;

/// <summary>
/// Centralised <see cref="TextDocumentSelector"/> so every handler attaches
/// to the same set of document languages and file patterns.
/// </summary>
internal static class DocumentSelectorFactory
{
    /// <summary>
    /// Matches documents whose language id is <c>expreszo</c> or whose URI
    /// ends in <c>.zo</c>. Clients that omit a language id still get proper
    /// handler dispatch via the pattern match.
    /// </summary>
    public static TextDocumentSelector Expreszo { get; } =
        new(
            new TextDocumentFilter { Language = "expreszo" },
            new TextDocumentFilter { Pattern = "**/*.zo" }
        );
}
