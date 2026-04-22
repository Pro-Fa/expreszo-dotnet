using System.Collections.Immutable;
using Expreszo.Errors;

namespace Expreszo.Ast;

/// <summary>
/// Base record for every AST node produced by the parser. Each node carries a
/// <see cref="TextSpan"/> covering its source range so downstream consumers
/// (validators, formatters, future IDE tooling) can point at the right
/// characters. All nodes are immutable; visitors return new nodes instead of
/// mutating in place.
/// </summary>
public abstract record Node
{
    public TextSpan Span { get; init; }

    protected Node(TextSpan span) => Span = span;

    /// <summary>Placeholder span used when a concrete source range is not available.</summary>
    public static TextSpan NoSpan => default;
}

// ---------- literals ----------

public sealed record NumberLit(double Value, TextSpan Span) : Node(Span);

public sealed record StringLit(string Value, TextSpan Span) : Node(Span);

public sealed record BoolLit(bool Value, TextSpan Span) : Node(Span);

public sealed record NullLit(TextSpan Span) : Node(Span);

public sealed record UndefinedLit(TextSpan Span) : Node(Span);

/// <summary>
/// Opaque scalar wrapper for values that don't fit the primitive literal
/// types. Produced by simplification when a subtree collapses to a computed
/// <see cref="Expreszo.Value"/> (e.g. a folded array). Visitors pass it
/// through unchanged.
/// </summary>
public sealed record RawLit(Value Value, TextSpan Span) : Node(Span);

// ---------- collections ----------

public sealed record ArrayLit(ImmutableArray<ArrayEntry> Elements, TextSpan Span) : Node(Span)
{
    public bool Equals(ArrayLit? other) =>
        other is not null && Span == other.Span && Elements.SequenceEqual(other.Elements);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);

        foreach (ArrayEntry e in Elements)
        {
            hc.Add(e);
        }

        return hc.ToHashCode();
    }
}

public sealed record ObjectLit(ImmutableArray<ObjectEntry> Properties, TextSpan Span) : Node(Span)
{
    public bool Equals(ObjectLit? other) =>
        other is not null && Span == other.Span && Properties.SequenceEqual(other.Properties);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);

        foreach (ObjectEntry p in Properties)
        {
            hc.Add(p);
        }

        return hc.ToHashCode();
    }
}

/// <summary>Entry in an <see cref="ArrayLit.Elements"/> list - either a regular element or a spread.</summary>
public abstract record ArrayEntry(TextSpan Span);

public sealed record ArrayElement(Node Node, TextSpan Span) : ArrayEntry(Span);

public sealed record ArraySpread(Node Argument, TextSpan Span) : ArrayEntry(Span);

/// <summary>Entry in an <see cref="ObjectLit.Properties"/> list - either a key/value or a spread.</summary>
public abstract record ObjectEntry(TextSpan Span);

public sealed record ObjectProperty(string Key, Node Value, bool Quoted, TextSpan Span)
    : ObjectEntry(Span);

public sealed record ObjectSpread(Node Argument, TextSpan Span) : ObjectEntry(Span);

// ---------- names & access ----------

/// <summary>Variable reference (dereferences a name at evaluation time).</summary>
public sealed record Ident(string Name, TextSpan Span) : Node(Span);

/// <summary>
/// Name as a string literal - used as the target of assignments, lambda
/// parameters, and function definitions. Not dereferenced during evaluation.
/// </summary>
public sealed record NameRef(string Name, TextSpan Span) : Node(Span);

public sealed record Member(Node Object, string Property, TextSpan Span) : Node(Span);

// ---------- operators ----------

public sealed record Unary(string Op, Node Operand, TextSpan Span) : Node(Span);

public sealed record Binary(string Op, Node Left, Node Right, TextSpan Span) : Node(Span);

public sealed record Ternary(string Op, Node A, Node B, Node C, TextSpan Span) : Node(Span);

// ---------- callables ----------

public sealed record Call(Node Callee, ImmutableArray<Node> Args, TextSpan Span) : Node(Span)
{
    public bool Equals(Call? other) =>
        other is not null
        && Span == other.Span
        && Callee.Equals(other.Callee)
        && Args.SequenceEqual(other.Args);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);
        hc.Add(Callee);

        foreach (Node a in Args)
        {
            hc.Add(a);
        }

        return hc.ToHashCode();
    }
}

public sealed record Lambda(ImmutableArray<string> Params, Node Body, TextSpan Span) : Node(Span)
{
    public bool Equals(Lambda? other) =>
        other is not null
        && Span == other.Span
        && Body.Equals(other.Body)
        && Params.SequenceEqual(other.Params, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);
        hc.Add(Body);

        foreach (string p in Params)
        {
            hc.Add(p);
        }

        return hc.ToHashCode();
    }
}

public sealed record FunctionDef(
    string Name,
    ImmutableArray<string> Params,
    Node Body,
    TextSpan Span
) : Node(Span)
{
    public bool Equals(FunctionDef? other) =>
        other is not null
        && Span == other.Span
        && Name == other.Name
        && Body.Equals(other.Body)
        && Params.SequenceEqual(other.Params, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);
        hc.Add(Name);
        hc.Add(Body);

        foreach (string p in Params)
        {
            hc.Add(p);
        }

        return hc.ToHashCode();
    }
}

// ---------- control flow ----------

public sealed record CaseArm(Node When, Node Then);

public sealed record Case(Node? Subject, ImmutableArray<CaseArm> Arms, Node? Else, TextSpan Span)
    : Node(Span)
{
    public bool Equals(Case? other) =>
        other is not null
        && Span == other.Span
        && Equals(Subject, other.Subject)
        && Equals(Else, other.Else)
        && Arms.SequenceEqual(other.Arms);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);
        hc.Add(Subject);
        hc.Add(Else);

        foreach (CaseArm a in Arms)
        {
            hc.Add(a);
        }

        return hc.ToHashCode();
    }
}

// ---------- composition ----------

public sealed record Sequence(ImmutableArray<Node> Statements, TextSpan Span) : Node(Span)
{
    public bool Equals(Sequence? other) =>
        other is not null && Span == other.Span && Statements.SequenceEqual(other.Statements);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Span);

        foreach (Node s in Statements)
        {
            hc.Add(s);
        }

        return hc.ToHashCode();
    }
}

/// <summary>
/// Parenthesised subexpression. Preserved in the AST so <c>ToString()</c> can
/// round-trip source byte-for-byte; semantically transparent - every other
/// visitor treats it as a pass-through to <see cref="Inner"/>.
/// </summary>
public sealed record Paren(Node Inner, TextSpan Span) : Node(Span);
