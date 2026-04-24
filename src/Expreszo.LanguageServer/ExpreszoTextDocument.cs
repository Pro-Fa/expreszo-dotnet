using Expreszo.Ast;
using Expreszo.Errors;

namespace Expreszo.LanguageServer;

/// <summary>
/// Per-URI buffer state: the current text, its line index, and the most
/// recent parse result (a successful AST or the thrown exception). Features
/// read from <see cref="Root"/> when present and fall back to <see cref="LastError"/>
/// for diagnostics.
/// </summary>
internal sealed class ExpreszoTextDocument
{
    public ExpreszoTextDocument(string text, int version, Node? root, ExpressionException? error)
    {
        Text = text ?? string.Empty;
        Version = version;
        LineIndex = new LineIndex(Text);
        Root = root;
        LastError = error;
    }

    public string Text { get; }

    public int Version { get; }

    public LineIndex LineIndex { get; }

    /// <summary>The AST from the most recent successful parse, if any.</summary>
    public Node? Root { get; }

    /// <summary>The exception from the most recent failed parse, if any.</summary>
    public ExpressionException? LastError { get; }
}
