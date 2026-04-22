namespace Expreszo.Errors;

/// <summary>
/// Diagnostic context attached to every <see cref="ExpressionException"/>.
/// All fields are optional — parsers and evaluators populate only what's
/// relevant to the failure.
/// </summary>
public sealed record ErrorContext
{
    public string? Expression { get; init; }
    public ErrorPosition? Position { get; init; }
    public TextSpan? Span { get; init; }
    public string? Token { get; init; }
    public string? VariableName { get; init; }
    public string? FunctionName { get; init; }
    public string? PropertyName { get; init; }
    public string? ExpectedType { get; init; }
    public string? ReceivedType { get; init; }
    public int? ArgumentIndex { get; init; }

    public static ErrorContext Empty { get; } = new();
}

/// <summary>1-based line/column pair suitable for human-facing error messages.</summary>
public readonly record struct ErrorPosition(int Line, int Column)
{
    public override string ToString() => $"{Line}:{Column}";
}

/// <summary>0-based half-open source offsets <c>[Start, End)</c>.</summary>
public readonly record struct TextSpan(int Start, int End)
{
    public int Length => End - Start;
    public override string ToString() => $"[{Start},{End})";
}
