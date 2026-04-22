using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Expreszo;

namespace Expreszo.Benchmarks;

/// <summary>
/// Measures repeated evaluation of pre-parsed expressions — the hot path for
/// rule engines and per-row evaluation. Each benchmark parses once in
/// <see cref="Setup"/> and evaluates inside the measured loop.
/// </summary>
[MemoryDiagnoser]
public class EvaluationBenchmarks
{
    private Parser _parser = null!;

    private Expression _arithmetic = null!;
    private Expression _ternary = null!;
    private Expression _higherOrder = null!;
    private Expression _member = null!;
    private Expression _booleanExpr = null!;
    private Expression _statisticsExpr = null!;

    private JsonDocument _smallValues = null!;
    private JsonDocument _arrayValues = null!;
    private JsonDocument _nestedValues = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new Parser();

        _arithmetic = _parser.Parse("(x + y) * (a - b) / c");
        _ternary = _parser.Parse("x > 0 ? x * 2 : -x");
        _higherOrder = _parser.Parse("sum(map(xs, x => x * 2))");
        _member = _parser.Parse("user.profile.name | \" (\" | (user.profile.age as \"integer\") | \")\"");
        _booleanExpr = _parser.Parse("(age >= 18 and country == \"NL\") or override");
        _statisticsExpr = _parser.Parse("mean(xs) + stddev(xs) * 2");

        _smallValues = JsonDocument.Parse("""{"x":10,"y":20,"a":50,"b":5,"c":3}""");
        _arrayValues = JsonDocument.Parse("""{"xs":[1,2,3,4,5,6,7,8,9,10]}""");
        _nestedValues = JsonDocument.Parse(
            """{"user":{"profile":{"name":"Alice","age":30}},"age":30,"country":"NL","override":false}""");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _smallValues.Dispose();
        _arrayValues.Dispose();
        _nestedValues.Dispose();
    }

    [Benchmark(Description = "Arithmetic on scalar variables")]
    public Value Arithmetic() => _arithmetic.Evaluate(_smallValues);

    [Benchmark(Description = "Ternary with scalar variable")]
    public Value Ternary() => _ternary.Evaluate(_smallValues);

    [Benchmark(Description = "Higher-order: sum(map(xs, x => x * 2))")]
    public Value HigherOrder() => _higherOrder.Evaluate(_arrayValues);

    [Benchmark(Description = "Nested member access with concat and cast")]
    public Value Member() => _member.Evaluate(_nestedValues);

    [Benchmark(Description = "Short-circuit boolean chain")]
    public Value Boolean() => _booleanExpr.Evaluate(_nestedValues);

    [Benchmark(Description = "Statistics aggregate (mean + stddev)")]
    public Value Statistics() => _statisticsExpr.Evaluate(_arrayValues);
}
