using BenchmarkDotNet.Attributes;
using Expreszo;

namespace Expreszo.Benchmarks;

/// <summary>
/// Measures the cost of parsing expression strings to an
/// <see cref="Expression"/>. Each benchmark instantiates a fresh
/// <see cref="Parser"/> inside the measurement to include parser setup time
/// — the combined "cold start" cost matters for per-request scenarios where
/// a parser is built once and discarded.
/// </summary>
[MemoryDiagnoser]
public class ParsingBenchmarks
{
    private Parser _parser = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Warm the AOT tables / static field initialisation once so each
        // benchmark measures parsing in isolation, not JIT of the framework.
        _parser = new Parser();
        _ = _parser.Parse("1 + 2");
    }

    [Benchmark(Description = "Trivial literal: 1 + 2")]
    public Expression Trivial() => _parser.Parse("1 + 2");

    [Benchmark(Description = "Arithmetic: (x + y) * (a - b) / c")]
    public Expression Arithmetic() => _parser.Parse("(x + y) * (a - b) / c");

    [Benchmark(Description = "Ternary: x > 0 ? x * 2 : -x")]
    public Expression Ternary() => _parser.Parse("x > 0 ? x * 2 : -x");

    [Benchmark(Description = "Higher-order: sum(map(xs, x => x * 2))")]
    public Expression HigherOrder() => _parser.Parse("sum(map(xs, x => x * 2))");

    [Benchmark(Description = "Nested object/array literal")]
    public Expression NestedLiteral() =>
        _parser.Parse("{ name: \"n\", scores: [1, 2, 3], meta: { active: true, count: 3 } }");

    [Benchmark(Description = "CASE with 6 arms")]
    public Expression CaseWith6Arms() =>
        _parser.Parse("case x when 1 then \"one\" when 2 then \"two\" when 3 then \"three\" when 4 then \"four\" when 5 then \"five\" else \"other\" end");

    [Benchmark(Description = "Deep chain: a.b.c.d.e + f[0][1][2]")]
    public Expression DeepChain() => _parser.Parse("a.b.c.d.e + f[0][1][2]");

    [Benchmark(Description = "Mixed sequence: a=1; b=2; a+b")]
    public Expression Sequence() => _parser.Parse("a = 1; b = 2; a + b");
}
