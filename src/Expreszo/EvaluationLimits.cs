namespace Expreszo;

/// <summary>
/// Resource budgets the evaluator and built-in functions enforce against
/// user-supplied expressions. Limits are deliberately generous for normal use
/// and tight enough to make obvious DoS shapes fail fast with a thrown
/// <see cref="Errors.ExpressionException"/> instead of exhausting host memory
/// or CPU.
/// </summary>
public static class EvaluationLimits
{
    /// <summary>Maximum length of a string produced by a single built-in (e.g. <c>repeat</c>, <c>padLeft</c>).</summary>
    public const int MaxStringLength = 1_000_000;

    /// <summary>Maximum length of an array produced by a single built-in (e.g. <c>range</c>, <c>sort</c>).</summary>
    public const int MaxArrayLength = 1_000_000;

    /// <summary>Upper bound for <c>fac</c> / postfix <c>!</c>. <c>fac(171)</c> already exceeds <c>double.MaxValue</c>.</summary>
    public const int MaxFactorialInput = 170;

    /// <summary>Maximum nested <c>Call</c>/<c>Lambda</c> depth during evaluation - guards against StackOverflow.</summary>
    public const int MaxCallDepth = 256;
}
