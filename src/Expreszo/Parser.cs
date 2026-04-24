using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Expreszo.Ast;
using Expreszo.Builtins;
using Expreszo.Errors;
using Expreszo.Parsing;

namespace Expreszo;

/// <summary>Configuration options for <see cref="Parser"/>.</summary>
public sealed record ParserOptions
{
    /// <summary>Whether member access (<c>obj.prop</c>) is permitted. Defaults to <c>true</c>.</summary>
    public bool AllowMemberAccess { get; init; } = true;
}

/// <summary>
/// Entry point for parsing and evaluating expressions. Construct once,
/// reuse across many calls - the parser and the expressions it produces are
/// immutable and safe for concurrent use.
/// </summary>
public sealed class Parser
{
    private readonly ParserConfig _config;
    private readonly OperatorTable _ops;

    /// <summary>Creates a parser with the default built-in preset.</summary>
    public Parser(ParserOptions? options = null)
    {
        options ??= new ParserOptions();

        var builder = new OperatorTableBuilder();
        CorePreset.RegisterInto(builder);
        MathPreset.RegisterInto(builder);
        StringPreset.RegisterInto(builder);
        ArrayPreset.RegisterInto(builder);
        ObjectPreset.RegisterInto(builder);
        UtilityPreset.RegisterInto(builder);
        TypeCheckPreset.RegisterInto(builder);
        _ops = builder.Build();

        _config = new ParserConfig(
            ParserConfig.Default.Keywords,
            ParserConfig.Default.UnaryOps,
            ParserConfig.Default.BinaryOps,
            ParserConfig.Default.TernaryOps,
            ParserConfig.Default.NumericConstants,
            ParserConfig.Default.BuiltinLiterals,
            ParserConfig.Default.IsOperatorEnabled,
            allowMemberAccess: options.AllowMemberAccess
        );
    }

    /// <summary>Parses an expression into a reusable <see cref="Expression"/>.</summary>
    public Expression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Node ast = PrattParser.Parse(_config, expression);
        return new Expression(ast, _ops, _config);
    }

    /// <summary>Parses and evaluates in one step.</summary>
    public Value Evaluate(
        string expression,
        JsonDocument? values = null,
        VariableResolver? resolver = null
    ) => Parse(expression).Evaluate(values, resolver);

    /// <summary>Parses and evaluates in one step, asynchronously.</summary>
    public ValueTask<Value> EvaluateAsync(
        string expression,
        JsonDocument? values = null,
        VariableResolver? resolver = null,
        CancellationToken cancellationToken = default
    ) => Parse(expression).EvaluateAsync(values, resolver, cancellationToken);

    /// <summary>
    /// Parses <paramref name="expression"/> tolerantly: splits on top-level
    /// semicolons and parses each segment independently. A syntax error in
    /// one statement therefore does not prevent neighbouring statements from
    /// producing AST and symbols. The returned <see cref="ParseResult"/>
    /// always contains a walkable <see cref="Expression"/> covering the
    /// statements that did parse — when every statement fails, that
    /// expression evaluates to <c>undefined</c>.
    /// </summary>
    /// <remarks>
    /// Statement-level recovery is deliberately coarse: an incomplete
    /// expression inside a single statement (for example a trailing <c>+</c>
    /// in <c>a + </c>) still produces one error for that whole statement.
    /// Fine-grained (token-level) recovery is a non-goal of this pass; it
    /// would require reshaping the strict Pratt parser. Existing callers of
    /// <see cref="Parse(string)"/> are unaffected.
    /// </remarks>
    public ParseResult TryParse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        ImmutableArray<StatementSegment> segments = StatementSplitter.Split(_config, expression);

        if (segments.Length == 0)
        {
            Node emptyRoot = new UndefinedLit(new TextSpan(0, 0));
            return new ParseResult(new Expression(emptyRoot, _ops, _config), []);
        }

        var errors = ImmutableArray.CreateBuilder<ExpressionException>();
        var parsedNodes = ImmutableArray.CreateBuilder<Node>();

        foreach (StatementSegment seg in segments)
        {
            string padded = BuildPaddedSource(expression, seg.Start, seg.End);

            Node node;
            try
            {
                node = PrattParser.Parse(_config, padded);
            }
            catch (ExpressionException ex)
            {
                errors.Add(ex);
                continue;
            }

            if (node is UndefinedLit u && u.Span.Length == 0)
            {
                // Empty segment (pure whitespace between semicolons) — skip.
                continue;
            }

            parsedNodes.Add(node);
        }

        Node combined = parsedNodes.Count switch
        {
            0 => new UndefinedLit(new TextSpan(0, 0)),
            1 => parsedNodes[0],
            _ => new Sequence(
                parsedNodes.ToImmutable(),
                new TextSpan(parsedNodes[0].Span.Start, parsedNodes[^1].Span.End)
            ),
        };

        return new ParseResult(new Expression(combined, _ops, _config), errors.ToImmutable());
    }

    /// <summary>
    /// Builds a source string the tokenizer can consume such that any AST
    /// spans emitted by parsing it carry the absolute offsets they should
    /// have in <paramref name="source"/>. Everything before <paramref name="start"/>
    /// is replaced with spaces (keeping newlines so line/column positions in
    /// thrown errors remain correct); everything from <paramref name="start"/>
    /// to <paramref name="end"/> is copied verbatim; everything after
    /// <paramref name="end"/> is dropped.
    /// </summary>
    private static string BuildPaddedSource(string source, int start, int end)
    {
        var sb = new StringBuilder(end);

        for (int i = 0; i < start; i++)
        {
            char c = source[i];
            sb.Append(c is '\n' or '\r' ? c : ' ');
        }

        if (end > start)
        {
            sb.Append(source, start, end - start);
        }

        return sb.ToString();
    }
}
