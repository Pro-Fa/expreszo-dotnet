using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Expreszo;

namespace Expreszo.Benchmarks;

/// <summary>
/// One-shot parse + evaluate cycles — the worst-case pattern for scenarios
/// where an expression is received, evaluated once, and discarded.
/// Compare these numbers with <see cref="ParsingBenchmarks"/> plus
/// <see cref="EvaluationBenchmarks"/> to see the parse-once saving.
/// </summary>
[MemoryDiagnoser]
public class EndToEndBenchmarks
{
    private Parser _parser = null!;
    private JsonDocument _scalar = null!;
    private JsonDocument _array = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new Parser();
        _scalar = JsonDocument.Parse("""{"x":10,"y":20,"a":50,"b":5,"c":3}""");
        _array = JsonDocument.Parse("""{"xs":[1,2,3,4,5,6,7,8,9,10]}""");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scalar.Dispose();
        _array.Dispose();
    }

    [Benchmark(Description = "Parse + evaluate: (x + y) * (a - b) / c")]
    public Value Arithmetic() => _parser.Evaluate("(x + y) * (a - b) / c", _scalar);

    [Benchmark(Description = "Parse + evaluate: sum(map(xs, x => x * 2))")]
    public Value HigherOrder() => _parser.Evaluate("sum(map(xs, x => x * 2))", _array);

    [Benchmark(Description = "Parse + evaluate: case with 3 arms")]
    public Value Case() => _parser.Evaluate(
        "case x when 1 then \"one\" when 10 then \"ten\" else \"other\" end",
        _scalar);
}
