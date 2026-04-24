using Expreszo.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expreszo.LanguageServer.Handlers;

/// <summary>
/// Semantic-tokens provider: re-runs the library <see cref="Tokenizer"/> over
/// the buffer and maps tokens to LSP semantic token types. Runs regardless
/// of parse state — highlighting needs to keep working while the user is
/// mid-edit.
/// </summary>
internal sealed class ExpreszoSemanticTokensHandler : SemanticTokensHandlerBase
{
    private readonly DocumentCache _cache;

    public ExpreszoSemanticTokensHandler(DocumentCache cache) => _cache = cache;

    protected override Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken
    )
    {
        ExpreszoTextDocument? doc = _cache.TryGet(identifier.TextDocument.Uri);
        if (doc is null)
        {
            return Task.CompletedTask;
        }

        foreach (Token token in TokenStreamReader.Tokens(doc.Text))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            (int line, int character) = doc.LineIndex.OffsetToPosition(token.Index);
            int length = token.End - token.Index;
            if (length <= 0)
            {
                continue;
            }

            (SemanticTokenType type, SemanticTokenModifier[] modifiers) = Classify(token);
            builder.Push(line, character, length, type, modifiers);
        }

        return Task.CompletedTask;
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken
    ) => Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities
    ) =>
        new()
        {
            DocumentSelector = DocumentSelectorFactory.Expreszo,
            Legend = new SemanticTokensLegend
            {
                TokenTypes = new Container<SemanticTokenType>(
                    SemanticTokenType.Keyword,
                    SemanticTokenType.Operator,
                    SemanticTokenType.Number,
                    SemanticTokenType.String,
                    SemanticTokenType.Variable,
                    SemanticTokenType.Function,
                    SemanticTokenType.Parameter,
                    SemanticTokenType.Comment
                ),
                TokenModifiers = new Container<SemanticTokenModifier>(
                    SemanticTokenModifier.DefaultLibrary
                ),
            },
            Full = new SemanticTokensCapabilityRequestFull { Delta = true },
            Range = true,
        };

    private static (SemanticTokenType, SemanticTokenModifier[]) Classify(Token token) =>
        token.Kind switch
        {
            TokenKind.Keyword => (SemanticTokenType.Keyword, []),
            TokenKind.Op when BuiltinMetadata.IsKeyword(token.Text) => (SemanticTokenType.Keyword, []),
            TokenKind.Op => (SemanticTokenType.Operator, []),
            TokenKind.Number => (SemanticTokenType.Number, []),
            TokenKind.String => (SemanticTokenType.String, []),
            TokenKind.Const => (SemanticTokenType.Keyword, []),
            TokenKind.Name when BuiltinMetadata.IsBuiltinFunction(token.Text) => (
                SemanticTokenType.Function,
                [SemanticTokenModifier.DefaultLibrary]
            ),
            TokenKind.Name => (SemanticTokenType.Variable, []),
            _ => (SemanticTokenType.Variable, []),
        };
}
