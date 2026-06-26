using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes;

/// <summary>
/// The single normalisation point every date/time function routes through.
/// Accepts the value shapes a user can pass through an expression — an existing
/// <see cref="Value.DateTime"/>, an ISO 8601 <see cref="Value.String"/>, or an
/// epoch-millisecond <see cref="Value.Number"/> — and yields a
/// <see cref="Value.DateTime"/>. Mirrors the TypeScript <c>toDateTime</c> /
/// <c>toDateTimeOrUndefined</c> helpers.
/// </summary>
public static class Normalize
{
    /// <summary>
    /// Converts <paramref name="v"/> to a <see cref="Value.DateTime"/>, throwing
    /// for shapes that are not a recognised date.
    /// </summary>
    public static Value.DateTime ToDateTime(Value v, DateTimeOptions options) =>
        v switch
        {
            Value.DateTime dt => dt,
            Value.String s => Dt.ParseIso(s.V, options.DefaultZone),
            Value.Number n => Dt.FromMillis(n.V, options.DefaultZone),
            _ => throw new EvaluationException($"Cannot convert {v.TypeName()} to DateTime"),
        };

    /// <summary>
    /// Same as <see cref="ToDateTime"/> but returns <c>null</c> when the input is
    /// <see cref="Value.Null"/> or <see cref="Value.Undefined"/>.
    /// </summary>
    public static Value.DateTime? ToDateTimeOrUndefined(Value v, DateTimeOptions options) =>
        v is Value.Null or Value.Undefined ? null : ToDateTime(v, options);
}
