using Expreszo.DateTimes.Internal;

namespace Expreszo.DateTimes;

/// <summary>
/// Helpers for feeding native CLR values — in particular
/// <see cref="System.DateTime"/> and <see cref="DateTimeOffset"/> — into an
/// expression as variables. JSON-document input cannot carry a native date, so
/// native dates enter through the existing <see cref="VariableResolver"/> hook,
/// converted to <see cref="Value.DateTime"/> here.
/// </summary>
public static class DateTimeVariables
{
    /// <summary>
    /// Builds a <see cref="VariableResolver"/> over a dictionary of CLR values.
    /// <see cref="DateTimeOffset"/> / <see cref="System.DateTime"/> become
    /// <see cref="Value.DateTime"/> (displayed in <paramref name="zone"/>, or the
    /// machine local zone when omitted); other scalars map to their natural
    /// <see cref="Value"/> kinds.
    /// </summary>
    public static VariableResolver FromObjects(
        IReadOnlyDictionary<string, object?> variables,
        TimeZoneInfo? zone = null
    )
    {
        ArgumentNullException.ThrowIfNull(variables);
        TimeZoneInfo z = zone ?? TimeZoneInfo.Local;
        return name =>
            variables.TryGetValue(name, out object? raw)
                ? new VariableResolveResult.Bound(ToValue(raw, z))
                : VariableResolveResult.NotResolved;
    }

    /// <summary>Converts a single CLR value to an Expreszo <see cref="Value"/>.</summary>
    public static Value ToValue(object? raw, TimeZoneInfo zone) =>
        raw switch
        {
            null => Value.Null.Instance,
            Value v => v,
            DateTimeOffset dto => Dt.FromInstant(dto, zone),
            DateTime dt => FromNativeDateTime(dt, zone),
            bool b => Value.Boolean.Of(b),
            string s => new Value.String(s),
            float f => Value.Number.Of(f),
            double d => Value.Number.Of(d),
            decimal m => Value.Number.Of((double)m),
            sbyte or byte or short or ushort or int or uint or long or ulong =>
                Value.Number.Of(Convert.ToDouble(raw, CultureInfo.InvariantCulture)),
            _ => throw new EvaluationException(
                $"Cannot convert value of type {raw.GetType().Name} to an Expreszo value"
            ),
        };

    private static Value.DateTime FromNativeDateTime(DateTime dt, TimeZoneInfo zone) =>
        dt.Kind == DateTimeKind.Unspecified
            ? Dt.FromWall(dt, zone)
            : Dt.FromInstant(new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero), zone);
}
