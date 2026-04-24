using System.Collections.Immutable;
using Expreszo.Analysis;
using Expreszo.Ast;
using Expreszo.Errors;

namespace Expreszo.LanguageServer;

/// <summary>
/// Per-URI buffer state: the current text, its line index, a (possibly
/// partial) AST from the error-recovering parser, and every diagnostic
/// collected during the parse.
/// </summary>
internal sealed class ExpreszoTextDocument
{
    public ExpreszoTextDocument(
        string text,
        int version,
        Node root,
        ImmutableArray<ExpressionException> errors
    )
    {
        Text = text ?? string.Empty;
        Version = version;
        LineIndex = new LineIndex(Text);
        Root = root;
        Errors = errors.IsDefault ? [] : errors;
    }

    public string Text { get; }

    public int Version { get; }

    public LineIndex LineIndex { get; }

    /// <summary>
    /// The AST from the error-recovering parser. Always populated —
    /// statements that didn't parse simply don't appear. When the whole
    /// buffer failed, this is a zero-span <see cref="UndefinedLit"/>.
    /// </summary>
    public Node Root { get; }

    /// <summary>Errors collected during the most recent parse; empty when the buffer is clean.</summary>
    public ImmutableArray<ExpressionException> Errors { get; }

    public bool HasErrors => Errors.Length > 0;
}
