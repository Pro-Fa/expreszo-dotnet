using System.Text.Json;

namespace Expreszo.Tests;

public class BuiltinsTests
{
    private static readonly Parser Parser = new();

    // ---------- Math unary ----------

    [Test]
    [Arguments("abs(-5)", 5d)]
    [Arguments("ceil(1.1)", 2d)]
    [Arguments("floor(1.9)", 1d)]
    [Arguments("round(1.5)", 2d)]
    [Arguments("sqrt(16)", 4d)]
    [Arguments("cbrt(27)", 3d)]
    [Arguments("trunc(1.9)", 1d)]
    [Arguments("sign(-3)", -1d)]
    public async Task Math_unary(string expr, double expected)
    {
        await Assert.That(((Value.Number)Parser.Evaluate(expr)).V).IsEqualTo(expected);
    }

    [Test]
    public async Task Exp_and_log_round_trip()
    {
        var v = ((Value.Number)Parser.Evaluate("log(exp(1))")).V;
        await Assert.That(v).IsEqualTo(1d).Within(1e-10);
    }

    // ---------- Trig ----------

    [Test]
    public async Task Trig_functions()
    {
        var sin0 = ((Value.Number)Parser.Evaluate("sin(0)")).V;
        var cos0 = ((Value.Number)Parser.Evaluate("cos(0)")).V;
        await Assert.That(sin0).IsEqualTo(0d);
        await Assert.That(cos0).IsEqualTo(1d);
    }

    // ---------- Math functions ----------

    [Test]
    public async Task Max_variadic_and_array()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("max(1, 5, 3)")).V).IsEqualTo(5d);
        await Assert.That(((Value.Number)Parser.Evaluate("max([1, 5, 3])")).V).IsEqualTo(5d);
    }

    [Test]
    public async Task Min_variadic_and_array()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("min(1, 5, 3)")).V).IsEqualTo(1d);
        await Assert.That(((Value.Number)Parser.Evaluate("min([1, 5, 3])")).V).IsEqualTo(1d);
    }

    [Test]
    public async Task Hypot_computes_euclidean_distance()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("hypot(3, 4)")).V).IsEqualTo(5d);
    }

    [Test]
    public async Task Clamp_bounds_values()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("clamp(10, 0, 5)")).V).IsEqualTo(5d);
        await Assert.That(((Value.Number)Parser.Evaluate("clamp(-3, 0, 5)")).V).IsEqualTo(0d);
        await Assert.That(((Value.Number)Parser.Evaluate("clamp(3, 0, 5)")).V).IsEqualTo(3d);
    }

    [Test]
    public async Task Fac_computes_factorial()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("fac(5)")).V).IsEqualTo(120d);
    }

    [Test]
    public async Task RoundTo_rounds_to_n_decimals()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("roundTo(1.234567, 2)")).V).IsEqualTo(1.23);
    }

    [Test]
    public async Task Statistics()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("sum([1, 2, 3, 4])")).V).IsEqualTo(10d);
        await Assert.That(((Value.Number)Parser.Evaluate("mean([2, 4, 6])")).V).IsEqualTo(4d);
        await Assert.That(((Value.Number)Parser.Evaluate("median([1, 2, 3, 4, 5])")).V).IsEqualTo(3d);
    }

    // ---------- String ----------

    [Test]
    public async Task String_functions()
    {
        await Assert.That(((Value.String)Parser.Evaluate("toUpper(\"hi\")")).V).IsEqualTo("HI");
        await Assert.That(((Value.String)Parser.Evaluate("toLower(\"HI\")")).V).IsEqualTo("hi");
        await Assert.That(((Value.String)Parser.Evaluate("trim(\"  hi  \")")).V).IsEqualTo("hi");
        await Assert.That(((Value.String)Parser.Evaluate("repeat(\"ab\", 3)")).V).IsEqualTo("ababab");
        await Assert.That(((Value.String)Parser.Evaluate("reverse(\"abc\")")).V).IsEqualTo("cba");
        await Assert.That(((Value.String)Parser.Evaluate("left(\"hello\", 3)")).V).IsEqualTo("hel");
        await Assert.That(((Value.String)Parser.Evaluate("right(\"hello\", 3)")).V).IsEqualTo("llo");
    }

    [Test]
    public async Task Contains_and_starts_ends()
    {
        await Assert.That(((Value.Boolean)Parser.Evaluate("contains(\"hello\", \"ell\")")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("startsWith(\"hello\", \"hel\")")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("endsWith(\"hello\", \"llo\")")).V).IsTrue();
    }

    [Test]
    public async Task Split_and_join_round_trip()
    {
        var split = (Value.Array)Parser.Evaluate("split(\"a,b,c\", \",\")");
        await Assert.That(split.Items.Length).IsEqualTo(3);
        await Assert.That(((Value.String)Parser.Evaluate("join([\"a\", \"b\", \"c\"], \"-\")")).V).IsEqualTo("a-b-c");
    }

    [Test]
    public async Task Replace_and_replaceFirst()
    {
        await Assert.That(((Value.String)Parser.Evaluate("replace(\"aaa\", \"a\", \"b\")")).V).IsEqualTo("bbb");
        await Assert.That(((Value.String)Parser.Evaluate("replaceFirst(\"aaa\", \"a\", \"b\")")).V).IsEqualTo("baa");
    }

    [Test]
    public async Task Base64_round_trip()
    {
        var encoded = ((Value.String)Parser.Evaluate("base64Encode(\"hello\")")).V;
        await Assert.That(encoded).IsEqualTo("aGVsbG8=");
        var decoded = ((Value.String)Parser.Evaluate("base64Decode(\"aGVsbG8=\")")).V;
        await Assert.That(decoded).IsEqualTo("hello");
    }

    [Test]
    public async Task Coalesce_picks_first_non_empty()
    {
        await Assert.That(((Value.String)Parser.Evaluate("coalesce(null, \"\", \"hi\")")).V).IsEqualTo("hi");
    }

    [Test]
    public async Task Slice_works_for_strings_and_arrays()
    {
        await Assert.That(((Value.String)Parser.Evaluate("slice(\"abcdef\", 1, 4)")).V).IsEqualTo("bcd");
        var arr = (Value.Array)Parser.Evaluate("slice([10, 20, 30, 40], 1, 3)");
        await Assert.That(arr.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Pad_functions()
    {
        await Assert.That(((Value.String)Parser.Evaluate("padLeft(\"ab\", 5)")).V).IsEqualTo("   ab");
        await Assert.That(((Value.String)Parser.Evaluate("padRight(\"ab\", 5)")).V).IsEqualTo("ab   ");
        await Assert.That(((Value.String)Parser.Evaluate("padBoth(\"ab\", 6, \"*\")")).V).IsEqualTo("**ab**");
    }

    // ---------- Array ----------

    [Test]
    public async Task Map_filter_fold()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("sum(map([1, 2, 3], x => x * 2))")).V).IsEqualTo(12d);
        var filtered = (Value.Array)Parser.Evaluate("filter([1, 2, 3, 4], x => x > 2)");
        await Assert.That(filtered.Items.Length).IsEqualTo(2);
        await Assert.That(((Value.Number)Parser.Evaluate("fold([1, 2, 3], 0, (acc, x) => acc + x)")).V).IsEqualTo(6d);
    }

    [Test]
    public async Task Find_some_every()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("find([1, 2, 3], x => x > 1)") as Value.Number)!.V).IsEqualTo(2d);
        await Assert.That(((Value.Boolean)Parser.Evaluate("some([1, 2, 3], x => x > 2)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("every([1, 2, 3], x => x > 0)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("every([1, 2, 3], x => x > 1)")).V).IsFalse();
    }

    [Test]
    public async Task Unique_distinct()
    {
        var arr = (Value.Array)Parser.Evaluate("unique([1, 2, 2, 3, 1])");
        await Assert.That(arr.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task IndexOf_join_range()
    {
        await Assert.That(((Value.Number)Parser.Evaluate("indexOf([10, 20, 30], 20)")).V).IsEqualTo(1d);
        await Assert.That(((Value.String)Parser.Evaluate("join([1, 2, 3], \"-\")")).V).IsEqualTo("1-2-3");
        var r = (Value.Array)Parser.Evaluate("range(0, 5)");
        await Assert.That(r.Items.Length).IsEqualTo(5);
    }

    [Test]
    public async Task Chunk_union_intersect()
    {
        var chunked = (Value.Array)Parser.Evaluate("chunk([1, 2, 3, 4, 5], 2)");
        await Assert.That(chunked.Items.Length).IsEqualTo(3);
        var un = (Value.Array)Parser.Evaluate("union([1, 2], [2, 3])");
        await Assert.That(un.Items.Length).IsEqualTo(3);
        var inter = (Value.Array)Parser.Evaluate("intersect([1, 2, 3], [2, 3, 4])");
        await Assert.That(inter.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task GroupBy_and_countBy()
    {
        var grouped = (Value.Object)Parser.Evaluate("groupBy([1, 2, 3, 4], x => x % 2 == 0 ? \"even\" : \"odd\")");
        await Assert.That(grouped.Props.Count).IsEqualTo(2);
        var counts = (Value.Object)Parser.Evaluate("countBy([1, 2, 3, 4], x => x % 2 == 0 ? \"even\" : \"odd\")");
        await Assert.That(((Value.Number)counts.Props["even"]).V).IsEqualTo(2d);
        await Assert.That(((Value.Number)counts.Props["odd"]).V).IsEqualTo(2d);
    }

    [Test]
    public async Task Sort_works_with_and_without_comparator()
    {
        var sorted = (Value.Array)Parser.Evaluate("sort([3, 1, 2])");
        await Assert.That(((Value.Number)sorted.Items[0]).V).IsEqualTo(1d);
        await Assert.That(((Value.Number)sorted.Items[2]).V).IsEqualTo(3d);

        var desc = (Value.Array)Parser.Evaluate("sort([1, 3, 2], (a, b) => b - a)");
        await Assert.That(((Value.Number)desc.Items[0]).V).IsEqualTo(3d);
    }

    [Test]
    public async Task Flatten_respects_depth()
    {
        var flat = (Value.Array)Parser.Evaluate("flatten([[1, 2], [3, [4]]])");
        await Assert.That(flat.Items.Length).IsEqualTo(4);
    }

    // ---------- Object ----------

    [Test]
    public async Task Merge_keys_values()
    {
        var merged = (Value.Object)Parser.Evaluate("merge({a: 1}, {b: 2})");
        await Assert.That(merged.Props.Count).IsEqualTo(2);

        var keys = (Value.Array)Parser.Evaluate("keys({a: 1, b: 2})");
        await Assert.That(keys.Items.Length).IsEqualTo(2);

        var vals = (Value.Array)Parser.Evaluate("values({a: 10, b: 20})");
        await Assert.That(vals.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Pick_and_omit()
    {
        var picked = (Value.Object)Parser.Evaluate("pick({a: 1, b: 2, c: 3}, [\"a\", \"c\"])");
        await Assert.That(picked.Props.Count).IsEqualTo(2);
        await Assert.That(picked.Props.ContainsKey("a")).IsTrue();
        await Assert.That(picked.Props.ContainsKey("b")).IsFalse();

        var omitted = (Value.Object)Parser.Evaluate("omit({a: 1, b: 2, c: 3}, [\"b\"])");
        await Assert.That(omitted.Props.Count).IsEqualTo(2);
        await Assert.That(omitted.Props.ContainsKey("b")).IsFalse();
    }

    [Test]
    public async Task MapValues_applies_over_entries()
    {
        var mapped = (Value.Object)Parser.Evaluate("mapValues({a: 1, b: 2}, v => v * 10)");
        await Assert.That(((Value.Number)mapped.Props["a"]).V).IsEqualTo(10d);
        await Assert.That(((Value.Number)mapped.Props["b"]).V).IsEqualTo(20d);
    }

    // ---------- Utility ----------

    [Test]
    public async Task If_function_is_lazy()
    {
        // Division by zero would throw if eagerly evaluated.
        var result = Parser.Evaluate("if(true, 42, 1 / 0)");
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    [Test]
    public async Task Json_serializes_value()
    {
        var json = ((Value.String)Parser.Evaluate("json({a: 1, b: [2, 3]})")).V;
        await Assert.That(json).Contains("\"a\":1");
    }

    // ---------- Type-check ----------

    [Test]
    public async Task Type_predicates()
    {
        await Assert.That(((Value.Boolean)Parser.Evaluate("isNumber(1)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isString(\"x\")")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isArray([1, 2])")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isObject({a: 1})")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isNull(null)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isUndefined(undefined)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isBoolean(true)")).V).IsTrue();
        await Assert.That(((Value.Boolean)Parser.Evaluate("isFunction(x => x)")).V).IsTrue();
    }
}
