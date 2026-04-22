namespace Expreszo.Parsing;

/// <summary>
/// A single lexical unit produced by the tokenizer. Immutable. The payload
/// fields (<see cref="Text"/>, <see cref="Number"/>, <see cref="Const"/>) are
/// populated per <see cref="Kind"/>:
/// <list type="bullet">
///   <item><see cref="TokenKind.Number"/> → <see cref="Number"/> holds the parsed double.</item>
///   <item><see cref="TokenKind.Const"/> → <see cref="Const"/> holds the literal value (<see cref="Value.Boolean"/> or <see cref="Value.Null"/>).</item>
///   <item>All other kinds → <see cref="Text"/> holds the raw or canonical lexeme.</item>
/// </list>
/// </summary>
public sealed record Token(
    TokenKind Kind,
    string Text,
    int Index,
    int End,
    double Number = 0d,
    Value? Const = null
)
{
    public int Length => End - Index;

    public override string ToString()
    {
        return Kind switch
        {
            TokenKind.Number => $"{Kind}: {Number}",
            TokenKind.Const => $"{Kind}: {Const}",
            _ => $"{Kind}: {Text}",
        };
    }
}
