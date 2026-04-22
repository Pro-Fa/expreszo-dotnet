using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Expreszo;

namespace Expreszo.Benchmarks;

/// <summary>
/// Measures the effect of <see cref="Expression.Simplify"/> on subsequent
/// evaluation cost — the before / after comparison shows how much constant
/// folding actually saves when the same expression is evaluated repeatedly.
/// </summary>
[MemoryDiagnoser]
public class SimplifyBenchmarks
{
    private Parser _parser = null!;
    private Expression _original = null!;
    private Expression _simplified = null!;
    private JsonDocument _values = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new Parser();

        // Expression with a constant-heavy subtree that simplify can fold.
        _original = _parser.Parse("((2 * 3) + (10 / 2)) * x + (7 - 4) * y - (100 - 50)");

        // Simplify with no values — folds every constant subtree.
        _simplified = _original.Simplify();

        _values = JsonDocument.Parse("""{"x":7,"y":3}""");
    }

    [GlobalCleanup]
    public void Cleanup() => _values.Dispose();

    [Benchmark(Description = "Evaluate original (no simplification)")]
    public Value Original() => _original.Evaluate(_values);

    [Benchmark(Description = "Evaluate pre-simplified")]
    public Value Simplified() => _simplified.Evaluate(_values);

    [Benchmark(Description = "Simplify() cost (once, excluding evaluation)")]
    public Expression SimplifyOnce() => _original.Simplify();
}
