using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Expreszo;

/// <summary>
/// Discriminated union describing every value the evaluator manipulates. Use
/// pattern matching against the sealed nested record types to branch on kind.
/// </summary>
/// <remarks>
/// The union is intentionally separate from <see cref="System.Text.Json.JsonElement"/>:
/// <list type="bullet">
///   <item>JSON has no <c>undefined</c>; <see cref="Undefined"/> is distinct from <see cref="Null"/>.</item>
///   <item>Functions / lambdas are not representable in JSON but are first-class values here.</item>
///   <item><c>JsonElement</c> is tied to the owning <c>JsonDocument</c>'s lifetime; <see cref="Value"/> is not.</item>
/// </list>
/// See <c>Expreszo.Json.JsonBridge</c> for the I/O boundary.
/// </remarks>
public abstract record Value
{
    private protected Value() { }

    public sealed record Number(double V) : Value
    {
        private static readonly Number[] SmallCache = CreateSmallCache();

        private static Number[] CreateSmallCache()
        {
            var arr = new Number[256];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = new Number(i);
            }
            return arr;
        }

        /// <summary>
        /// Returns a cached instance for small non-negative integers in [0, 255]
        /// and a fresh instance otherwise. Safe for NaN / Infinity (never cached).
        /// </summary>
        public static Number Of(double v)
        {
            if (v >= 0 && v < SmallCache.Length && v == Math.Floor(v))
            {
                return SmallCache[(int)v];
            }
            return new Number(v);
        }

        public override string ToString() => V.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed record String(string V) : Value
    {
        public static String Empty { get; } = new(string.Empty);

        public override string ToString() => V;
    }

    public sealed record Boolean(bool V) : Value
    {
        public static Boolean True { get; } = new(true);
        public static Boolean False { get; } = new(false);

        public static Boolean Of(bool v) => v ? True : False;

        public override string ToString() => V ? "true" : "false";
    }

    public sealed record Null : Value
    {
        public static Null Instance { get; } = new();

        public override string ToString() => "null";
    }

    public sealed record Undefined : Value
    {
        public static Undefined Instance { get; } = new();

        public override string ToString() => "undefined";
    }

    public sealed record Array(ImmutableArray<Value> Items) : Value
    {
        public static Array Empty { get; } = new(ImmutableArray<Value>.Empty);

        public static Array Of(params Value[] items) => new([.. items]);

        public bool Equals(Array? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Items.Length != other.Items.Length)
            {
                return false;
            }

            for (int i = 0; i < Items.Length; i++)
            {
                if (!Equals(Items[i], other.Items[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (Value item in Items)
            {
                hc.Add(item);
            }
            return hc.ToHashCode();
        }
    }

    public sealed record Object(FrozenDictionary<string, Value> Props) : Value
    {
        public static Object Empty { get; } = new(FrozenDictionary<string, Value>.Empty);

        /// <summary>Builds an <see cref="Object"/> from an ordinary dictionary, freezing it for fast reads.</summary>
        public static Object From(IReadOnlyDictionary<string, Value> props)
        {
            if (props.Count == 0)
            {
                return Empty;
            }

            FrozenDictionary<string, Value> frozen = props.ToFrozenDictionary(
                StringComparer.Ordinal
            );
            return new Object(frozen);
        }

        public bool Equals(Object? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Props.Count != other.Props.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, Value> kv in Props)
            {
                if (!other.Props.TryGetValue(kv.Key, out Value? otherValue))
                {
                    return false;
                }

                if (!Equals(kv.Value, otherValue))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (
                KeyValuePair<string, Value> kv in Props.OrderBy(
                    kv => kv.Key,
                    StringComparer.Ordinal
                )
            )
            {
                hc.Add(kv.Key);
                hc.Add(kv.Value);
            }
            return hc.ToHashCode();
        }
    }

    public sealed record Function(ExprFunc Invoke, string? Name = null) : Value
    {
        /// <summary>
        /// True when this function was minted by the evaluator (arrow function
        /// or <c>f(x) = ...</c> definition) rather than supplied by a caller.
        /// The allow-list check trusts evaluator-minted lambdas because they
        /// close over AST nodes, not raw CLR code. Forgery is prevented by
        /// keeping the init-only setter internal.
        /// </summary>
        public bool IsExpressionLambda { get; internal init; }

        public override string ToString() => Name is null ? "[function]" : $"[function {Name}]";
    }

    /// <summary>
    /// Returns <c>true</c> when this value is JavaScript-falsy: <see cref="Null"/>,
    /// <see cref="Undefined"/>, <c>false</c>, <c>0</c>, <c>NaN</c>, or empty string.
    /// Every other value (including empty arrays and objects, matching JS) is truthy.
    /// </summary>
    public bool IsTruthy() =>
        this switch
        {
            Null or Undefined => false,
            Boolean b => b.V,
            Number n => n.V != 0 && !double.IsNaN(n.V),
            String s => s.V.Length > 0,
            _ => true,
        };

    /// <summary>Human-readable type tag - JavaScript-style <c>typeof</c> names.</summary>
    public string TypeName() =>
        this switch
        {
            Number => "number",
            String => "string",
            Boolean => "boolean",
            Null => "null",
            Undefined => "undefined",
            Array => "array",
            Object => "object",
            Function => "function",
            _ => "unknown",
        };
}

/// <summary>
/// Delegate every built-in and user-registered function implements. Returning
/// a non-completed <see cref="ValueTask{TResult}"/> marks the function async;
/// this triggers <see cref="Errors.AsyncRequiredException"/> on the synchronous
/// <c>Evaluate</c> path.
/// </summary>
public delegate ValueTask<Value> ExprFunc(Value[] args, EvalContext ctx);

/// <summary>
/// Delegate passed to <c>Expression.Evaluate(...)</c> when the caller wants to
/// resolve variables on the fly. Returning <see cref="VariableResolveResult.NotResolved"/>
/// falls through to the next resolution layer.
/// </summary>
public delegate VariableResolveResult VariableResolver(string name);

/// <summary>Result type for <see cref="VariableResolver"/>: either bound, aliased, or unresolved.</summary>
public abstract record VariableResolveResult
{
    public sealed record Bound(Value Value) : VariableResolveResult;

    public sealed record Alias(string Name) : VariableResolveResult;

    public sealed record NotResolvedResult : VariableResolveResult
    {
        public static NotResolvedResult Instance { get; } = new();
    }

    public static VariableResolveResult NotResolved => NotResolvedResult.Instance;

    [return: NotNullIfNotNull(nameof(value))]
    public static VariableResolveResult? FromNullable(Value? value) =>
        value is null ? null : new Bound(value);
}
