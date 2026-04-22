using System.Globalization;

namespace Expreszo.Errors;

/// <summary>
/// Central catalogue of error message templates. Keeps message strings out of
/// throw sites so future localisation stays mechanical and message tweaks are
/// a single edit.
/// </summary>
internal static class Messages
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    internal static string UndefinedVariable(string name) =>
        string.Format(Culture, "undefined variable: {0}", name);

    internal static string UndefinedFunction(string name) =>
        string.Format(Culture, "undefined function: {0}", name);

    internal static string NotCallable(string name) =>
        string.Format(Culture, "'{0}' is not callable", name);

    internal static string MemberAccessDenied(string property) =>
        string.Format(Culture, "access to member '{0}' is not allowed", property);

    internal static string MemberAccessDisabled() => "member access is disabled on this parser";

    /// <summary>
    /// Numeric indices are user-controlled inputs, safe to echo. The literal
    /// value helps the caller understand why the index was rejected
    /// (e.g. <c>NaN</c>, <c>Infinity</c>, <c>1.5</c>).
    /// </summary>
    internal static string ArrayIndexNotInteger(double index) =>
        string.Format(Culture, "array index must be an integer, got: {0}", index);

    /// <summary>
    /// Non-numeric indices: only echo the type name, never the raw value, so
    /// sensitive scope bindings (e.g. <c>arr[password]</c>) don't leak into
    /// error messages or logs.
    /// </summary>
    internal static string ArrayIndexNotInteger(Value index) =>
        string.Format(
            Culture,
            "array index must be an integer, got a value of type '{0}'",
            index.TypeName()
        );

    internal static string WrongArgCount(string function, int expected, int actual) =>
        string.Format(Culture, "{0}: expected {1} arguments, got {2}", function, expected, actual);

    internal static string WrongArgType(
        string function,
        int index,
        string expected,
        string actual
    ) =>
        string.Format(
            Culture,
            "{0}: argument {1} expected {2}, got {3}",
            function,
            index,
            expected,
            actual
        );

    internal static string AsyncRequired() =>
        "expression produced an async result; call EvaluateAsync instead of Evaluate";

    internal static string UnexpectedToken(string? token) =>
        string.Format(Culture, "unexpected token: {0}", token ?? "<eof>");

    internal static string ExpectedToken(string expected, string? actual) =>
        string.Format(Culture, "expected {0}, got: {1}", expected, actual ?? "<eof>");

    internal static string AssignmentToNonIdentifier() =>
        "left-hand side of assignment must be an identifier";

    internal static string DivisionByZero() => "division by zero";

    internal static string CannotAdd(string leftType, string rightType) =>
        string.Format(Culture, "cannot add values of types {0} and {1}", leftType, rightType);

    internal static string UnsupportedCast(string target) =>
        string.Format(Culture, "unsupported cast target: {0}", target);
}
