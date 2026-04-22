using System.Text.Json;
using Expreszo.Builtins;
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
/// reuse across many calls — the parser and the expressions it produces are
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
            allowMemberAccess: options.AllowMemberAccess);
    }

    /// <summary>Parses an expression into a reusable <see cref="Expression"/>.</summary>
    public Expression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var ast = PrattParser.Parse(_config, expression);
        return new Expression(ast, _ops, _config);
    }

    /// <summary>Parses and evaluates in one step.</summary>
    public Value Evaluate(string expression, JsonDocument? values = null, VariableResolver? resolver = null) =>
        Parse(expression).Evaluate(values, resolver);

    /// <summary>Parses and evaluates in one step, asynchronously.</summary>
    public ValueTask<Value> EvaluateAsync(
        string expression,
        JsonDocument? values = null,
        VariableResolver? resolver = null,
        CancellationToken cancellationToken = default) =>
        Parse(expression).EvaluateAsync(values, resolver, cancellationToken);
}
