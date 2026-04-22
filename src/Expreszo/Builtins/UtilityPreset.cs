using System.Text.Json;
using Expreszo.Errors;
using Expreszo.Json;

namespace Expreszo.Builtins;

internal static class UtilityPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        // `if` is intercepted by the evaluator for lazy evaluation. This entry
        // exists so the function table lookup doesn't fail if someone passes
        // `if` as a value (e.g. `f = if; f(cond, a, b)`).
        b.AddFunction("if", OperatorTableBuilder.Sync(args =>
        {
            if (args.Length < 3) throw new ExpressionArgumentException("if requires 3 arguments", "if");
            return args[0].IsTruthy() ? args[1] : args[2];
        }));

        b.AddFunction("json", OperatorTableBuilder.Sync(args =>
        {
            if (args.Length < 1) return Value.Undefined.Instance;
            try
            {
                return new Value.String(JsonBridge.ToJsonString(args[0]));
            }
            catch (InvalidOperationException)
            {
                // Thrown for Value.Function — not serialisable.
                return Value.Undefined.Instance;
            }
        }));
    }
}

internal static class TypeCheckPreset
{
    public static void RegisterInto(OperatorTableBuilder b)
    {
        Check(b, "isArray", v => v is Value.Array);
        Check(b, "isObject", v => v is Value.Object);
        Check(b, "isNumber", v => v is Value.Number);
        Check(b, "isString", v => v is Value.String);
        Check(b, "isBoolean", v => v is Value.Boolean);
        Check(b, "isNull", v => v is Value.Null);
        Check(b, "isUndefined", v => v is Value.Undefined);
        Check(b, "isFunction", v => v is Value.Function);
    }

    private static void Check(OperatorTableBuilder b, string name, Func<Value, bool> pred)
    {
        b.AddFunction(name, OperatorTableBuilder.Sync(args => Value.Boolean.Of(args.Length > 0 && pred(args[0]))));
    }
}
