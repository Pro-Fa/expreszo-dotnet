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
        // Arrange

        // Act
        Value result = Parser.Evaluate(expr);

        // Assert
        // Tolerance covers libm divergences across platforms (e.g. glibc's
        // cbrt(27) returns 3.0000000000000004 while Windows' CRT returns 3).
        // The exact-integer cases (abs, ceil, floor, ...) still pass.
        await Assert.That(((Value.Number)result).V).IsEqualTo(expected).Within(1e-10);
    }

    [Test]
    public async Task Exp_and_log_round_trip()
    {
        // Arrange

        // Act
        double v = ((Value.Number)Parser.Evaluate("log(exp(1))")).V;

        // Assert
        await Assert.That(v).IsEqualTo(1d).Within(1e-10);
    }

    // ---------- Trig ----------

    [Test]
    public async Task Trig_functions()
    {
        // Arrange

        // Act
        double sin0 = ((Value.Number)Parser.Evaluate("sin(0)")).V;
        double cos0 = ((Value.Number)Parser.Evaluate("cos(0)")).V;

        // Assert
        await Assert.That(sin0).IsEqualTo(0d);
        await Assert.That(cos0).IsEqualTo(1d);
    }

    // ---------- Math functions ----------

    [Test]
    public async Task Max_variadic_and_array()
    {
        // Arrange

        // Act
        double variadic = ((Value.Number)Parser.Evaluate("max(1, 5, 3)")).V;
        double fromArray = ((Value.Number)Parser.Evaluate("max([1, 5, 3])")).V;

        // Assert
        await Assert.That(variadic).IsEqualTo(5d);
        await Assert.That(fromArray).IsEqualTo(5d);
    }

    [Test]
    public async Task Min_variadic_and_array()
    {
        // Arrange

        // Act
        double variadic = ((Value.Number)Parser.Evaluate("min(1, 5, 3)")).V;
        double fromArray = ((Value.Number)Parser.Evaluate("min([1, 5, 3])")).V;

        // Assert
        await Assert.That(variadic).IsEqualTo(1d);
        await Assert.That(fromArray).IsEqualTo(1d);
    }

    [Test]
    public async Task Hypot_computes_euclidean_distance()
    {
        // Arrange

        // Act
        double v = ((Value.Number)Parser.Evaluate("hypot(3, 4)")).V;

        // Assert
        await Assert.That(v).IsEqualTo(5d);
    }

    [Test]
    public async Task Clamp_bounds_values()
    {
        // Arrange

        // Act
        double upper = ((Value.Number)Parser.Evaluate("clamp(10, 0, 5)")).V;
        double lower = ((Value.Number)Parser.Evaluate("clamp(-3, 0, 5)")).V;
        double within = ((Value.Number)Parser.Evaluate("clamp(3, 0, 5)")).V;

        // Assert
        await Assert.That(upper).IsEqualTo(5d);
        await Assert.That(lower).IsEqualTo(0d);
        await Assert.That(within).IsEqualTo(3d);
    }

    [Test]
    public async Task Fac_computes_factorial()
    {
        // Arrange

        // Act
        double v = ((Value.Number)Parser.Evaluate("fac(5)")).V;

        // Assert
        await Assert.That(v).IsEqualTo(120d);
    }

    [Test]
    public async Task RoundTo_rounds_to_n_decimals()
    {
        // Arrange

        // Act
        double v = ((Value.Number)Parser.Evaluate("roundTo(1.234567, 2)")).V;

        // Assert
        await Assert.That(v).IsEqualTo(1.23);
    }

    [Test]
    public async Task Statistics()
    {
        // Arrange

        // Act
        double sum = ((Value.Number)Parser.Evaluate("sum([1, 2, 3, 4])")).V;
        double mean = ((Value.Number)Parser.Evaluate("mean([2, 4, 6])")).V;
        double median = ((Value.Number)Parser.Evaluate("median([1, 2, 3, 4, 5])")).V;

        // Assert
        await Assert.That(sum).IsEqualTo(10d);
        await Assert.That(mean).IsEqualTo(4d);
        await Assert.That(median).IsEqualTo(3d);
    }

    // ---------- String ----------

    [Test]
    public async Task String_functions()
    {
        // Arrange

        // Act
        string upper = ((Value.String)Parser.Evaluate("toUpper(\"hi\")")).V;
        string lower = ((Value.String)Parser.Evaluate("toLower(\"HI\")")).V;
        string trimmed = ((Value.String)Parser.Evaluate("trim(\"  hi  \")")).V;
        string repeated = ((Value.String)Parser.Evaluate("repeat(\"ab\", 3)")).V;
        string reversed = ((Value.String)Parser.Evaluate("reverse(\"abc\")")).V;
        string leftPart = ((Value.String)Parser.Evaluate("left(\"hello\", 3)")).V;
        string rightPart = ((Value.String)Parser.Evaluate("right(\"hello\", 3)")).V;

        // Assert
        await Assert.That(upper).IsEqualTo("HI");
        await Assert.That(lower).IsEqualTo("hi");
        await Assert.That(trimmed).IsEqualTo("hi");
        await Assert.That(repeated).IsEqualTo("ababab");
        await Assert.That(reversed).IsEqualTo("cba");
        await Assert.That(leftPart).IsEqualTo("hel");
        await Assert.That(rightPart).IsEqualTo("llo");
    }

    [Test]
    public async Task Contains_and_starts_ends()
    {
        // Arrange

        // Act
        bool contains = ((Value.Boolean)Parser.Evaluate("contains(\"hello\", \"ell\")")).V;
        bool startsWith = ((Value.Boolean)Parser.Evaluate("startsWith(\"hello\", \"hel\")")).V;
        bool endsWith = ((Value.Boolean)Parser.Evaluate("endsWith(\"hello\", \"llo\")")).V;

        // Assert
        await Assert.That(contains).IsTrue();
        await Assert.That(startsWith).IsTrue();
        await Assert.That(endsWith).IsTrue();
    }

    [Test]
    public async Task Split_and_join_round_trip()
    {
        // Arrange

        // Act
        var split = (Value.Array)Parser.Evaluate("split(\"a,b,c\", \",\")");
        string joined = ((Value.String)Parser.Evaluate("join([\"a\", \"b\", \"c\"], \"-\")")).V;

        // Assert
        await Assert.That(split.Items.Length).IsEqualTo(3);
        await Assert.That(joined).IsEqualTo("a-b-c");
    }

    [Test]
    public async Task Replace_and_replaceFirst()
    {
        // Arrange

        // Act
        string replacedAll = ((Value.String)Parser.Evaluate("replace(\"aaa\", \"a\", \"b\")")).V;
        string replacedFirst = (
            (Value.String)Parser.Evaluate("replaceFirst(\"aaa\", \"a\", \"b\")")
        ).V;

        // Assert
        await Assert.That(replacedAll).IsEqualTo("bbb");
        await Assert.That(replacedFirst).IsEqualTo("baa");
    }

    [Test]
    public async Task Base64_round_trip()
    {
        // Arrange

        // Act
        string encoded = ((Value.String)Parser.Evaluate("base64Encode(\"hello\")")).V;
        string decoded = ((Value.String)Parser.Evaluate("base64Decode(\"aGVsbG8=\")")).V;

        // Assert
        await Assert.That(encoded).IsEqualTo("aGVsbG8=");
        await Assert.That(decoded).IsEqualTo("hello");
    }

    [Test]
    public async Task Coalesce_picks_first_non_empty()
    {
        // Arrange

        // Act
        string v = ((Value.String)Parser.Evaluate("coalesce(null, \"\", \"hi\")")).V;

        // Assert
        await Assert.That(v).IsEqualTo("hi");
    }

    [Test]
    public async Task Slice_works_for_strings_and_arrays()
    {
        // Arrange

        // Act
        string stringSlice = ((Value.String)Parser.Evaluate("slice(\"abcdef\", 1, 4)")).V;
        var arraySlice = (Value.Array)Parser.Evaluate("slice([10, 20, 30, 40], 1, 3)");

        // Assert
        await Assert.That(stringSlice).IsEqualTo("bcd");
        await Assert.That(arraySlice.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Pad_functions()
    {
        // Arrange

        // Act
        string padded = ((Value.String)Parser.Evaluate("padLeft(\"ab\", 5)")).V;
        string paddedRight = ((Value.String)Parser.Evaluate("padRight(\"ab\", 5)")).V;
        string paddedBoth = ((Value.String)Parser.Evaluate("padBoth(\"ab\", 6, \"*\")")).V;

        // Assert
        await Assert.That(padded).IsEqualTo("   ab");
        await Assert.That(paddedRight).IsEqualTo("ab   ");
        await Assert.That(paddedBoth).IsEqualTo("**ab**");
    }

    // ---------- Array ----------

    [Test]
    public async Task Map_filter_fold()
    {
        // Arrange

        // Act
        double mapped = ((Value.Number)Parser.Evaluate("sum(map([1, 2, 3], x => x * 2))")).V;
        var filtered = (Value.Array)Parser.Evaluate("filter([1, 2, 3, 4], x => x > 2)");
        double folded = (
            (Value.Number)Parser.Evaluate("fold([1, 2, 3], 0, (acc, x) => acc + x)")
        ).V;

        // Assert
        await Assert.That(mapped).IsEqualTo(12d);
        await Assert.That(filtered.Items.Length).IsEqualTo(2);
        await Assert.That(folded).IsEqualTo(6d);
    }

    [Test]
    public async Task Find_some_every()
    {
        // Arrange

        // Act
        double found = (
            (Value.Number)Parser.Evaluate("find([1, 2, 3], x => x > 1)") as Value.Number
        )!.V;
        bool someGT2 = ((Value.Boolean)Parser.Evaluate("some([1, 2, 3], x => x > 2)")).V;
        bool allGT0 = ((Value.Boolean)Parser.Evaluate("every([1, 2, 3], x => x > 0)")).V;
        bool allGT1 = ((Value.Boolean)Parser.Evaluate("every([1, 2, 3], x => x > 1)")).V;

        // Assert
        await Assert.That(found).IsEqualTo(2d);
        await Assert.That(someGT2).IsTrue();
        await Assert.That(allGT0).IsTrue();
        await Assert.That(allGT1).IsFalse();
    }

    [Test]
    public async Task Unique_distinct()
    {
        // Arrange

        // Act
        var arr = (Value.Array)Parser.Evaluate("unique([1, 2, 2, 3, 1])");

        // Assert
        await Assert.That(arr.Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task IndexOf_join_range()
    {
        // Arrange

        // Act
        double idx = ((Value.Number)Parser.Evaluate("indexOf([10, 20, 30], 20)")).V;
        string joined = ((Value.String)Parser.Evaluate("join([1, 2, 3], \"-\")")).V;
        var r = (Value.Array)Parser.Evaluate("range(0, 5)");

        // Assert
        await Assert.That(idx).IsEqualTo(1d);
        await Assert.That(joined).IsEqualTo("1-2-3");
        await Assert.That(r.Items.Length).IsEqualTo(5);
    }

    [Test]
    public async Task Chunk_union_intersect()
    {
        // Arrange

        // Act
        var chunked = (Value.Array)Parser.Evaluate("chunk([1, 2, 3, 4, 5], 2)");
        var un = (Value.Array)Parser.Evaluate("union([1, 2], [2, 3])");
        var inter = (Value.Array)Parser.Evaluate("intersect([1, 2, 3], [2, 3, 4])");

        // Assert
        await Assert.That(chunked.Items.Length).IsEqualTo(3);
        await Assert.That(un.Items.Length).IsEqualTo(3);
        await Assert.That(inter.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task GroupBy_and_countBy()
    {
        // Arrange

        // Act
        var grouped = (Value.Object)
            Parser.Evaluate("groupBy([1, 2, 3, 4], x => x % 2 == 0 ? \"even\" : \"odd\")");
        var counts = (Value.Object)
            Parser.Evaluate("countBy([1, 2, 3, 4], x => x % 2 == 0 ? \"even\" : \"odd\")");

        // Assert
        await Assert.That(grouped.Props.Count).IsEqualTo(2);
        await Assert.That(((Value.Number)counts.Props["even"]).V).IsEqualTo(2d);
        await Assert.That(((Value.Number)counts.Props["odd"]).V).IsEqualTo(2d);
    }

    [Test]
    public async Task Sort_works_with_and_without_comparator()
    {
        // Arrange

        // Act
        var sorted = (Value.Array)Parser.Evaluate("sort([3, 1, 2])");
        var desc = (Value.Array)Parser.Evaluate("sort([1, 3, 2], (a, b) => b - a)");

        // Assert
        await Assert.That(((Value.Number)sorted.Items[0]).V).IsEqualTo(1d);
        await Assert.That(((Value.Number)sorted.Items[2]).V).IsEqualTo(3d);
        await Assert.That(((Value.Number)desc.Items[0]).V).IsEqualTo(3d);
    }

    [Test]
    public async Task Flatten_respects_depth()
    {
        // Arrange

        // Act
        var flat = (Value.Array)Parser.Evaluate("flatten([[1, 2], [3, [4]]])");

        // Assert
        await Assert.That(flat.Items.Length).IsEqualTo(4);
    }

    // ---------- Object ----------

    [Test]
    public async Task Merge_keys_values()
    {
        // Arrange

        // Act
        var merged = (Value.Object)Parser.Evaluate("merge({a: 1}, {b: 2})");
        var keys = (Value.Array)Parser.Evaluate("keys({a: 1, b: 2})");
        var vals = (Value.Array)Parser.Evaluate("values({a: 10, b: 20})");

        // Assert
        await Assert.That(merged.Props.Count).IsEqualTo(2);
        await Assert.That(keys.Items.Length).IsEqualTo(2);
        await Assert.That(vals.Items.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Pick_and_omit()
    {
        // Arrange

        // Act
        var picked = (Value.Object)Parser.Evaluate("pick({a: 1, b: 2, c: 3}, [\"a\", \"c\"])");
        var omitted = (Value.Object)Parser.Evaluate("omit({a: 1, b: 2, c: 3}, [\"b\"])");

        // Assert
        await Assert.That(picked.Props.Count).IsEqualTo(2);
        await Assert.That(picked.Props.ContainsKey("a")).IsTrue();
        await Assert.That(picked.Props.ContainsKey("b")).IsFalse();
        await Assert.That(omitted.Props.Count).IsEqualTo(2);
        await Assert.That(omitted.Props.ContainsKey("b")).IsFalse();
    }

    [Test]
    public async Task MapValues_applies_over_entries()
    {
        // Arrange

        // Act
        var mapped = (Value.Object)Parser.Evaluate("mapValues({a: 1, b: 2}, v => v * 10)");

        // Assert
        await Assert.That(((Value.Number)mapped.Props["a"]).V).IsEqualTo(10d);
        await Assert.That(((Value.Number)mapped.Props["b"]).V).IsEqualTo(20d);
    }

    // ---------- Utility ----------

    [Test]
    public async Task If_function_is_lazy()
    {
        // Arrange

        // Act
        // Division by zero would throw if eagerly evaluated.
        Value result = Parser.Evaluate("if(true, 42, 1 / 0)");

        // Assert
        await Assert.That(((Value.Number)result).V).IsEqualTo(42d);
    }

    [Test]
    public async Task Json_serializes_value()
    {
        // Arrange

        // Act
        string json = ((Value.String)Parser.Evaluate("json({a: 1, b: [2, 3]})")).V;

        // Assert
        await Assert.That(json).Contains("\"a\":1");
    }

    // ---------- Type-check ----------

    [Test]
    public async Task Type_predicates()
    {
        // Arrange

        // Act
        bool isNum = ((Value.Boolean)Parser.Evaluate("isNumber(1)")).V;
        bool isStr = ((Value.Boolean)Parser.Evaluate("isString(\"x\")")).V;
        bool isArr = ((Value.Boolean)Parser.Evaluate("isArray([1, 2])")).V;
        bool isObj = ((Value.Boolean)Parser.Evaluate("isObject({a: 1})")).V;
        bool isNil = ((Value.Boolean)Parser.Evaluate("isNull(null)")).V;
        bool isUnd = ((Value.Boolean)Parser.Evaluate("isUndefined(undefined)")).V;
        bool isBool = ((Value.Boolean)Parser.Evaluate("isBoolean(true)")).V;
        bool isFn = ((Value.Boolean)Parser.Evaluate("isFunction(x => x)")).V;

        // Assert
        await Assert.That(isNum).IsTrue();
        await Assert.That(isStr).IsTrue();
        await Assert.That(isArr).IsTrue();
        await Assert.That(isObj).IsTrue();
        await Assert.That(isNil).IsTrue();
        await Assert.That(isUnd).IsTrue();
        await Assert.That(isBool).IsTrue();
        await Assert.That(isFn).IsTrue();
    }
}
