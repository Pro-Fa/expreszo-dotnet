using System.Collections.Concurrent;

namespace Expreszo.Tests;

public class ThreadSafetyTests
{
    private const int ThreadCount = 16;
    private const int IterationsPerThread = 500;

    [Test]
    public async Task Single_parser_handles_concurrent_Parse_with_deterministic_output()
    {
        var parser = new Parser();
        const string source = "(a + b) * c - d / 2 + abs(-x) + foo(1, 2, 3)";
        string expected = parser.Parse(source).ToString();

        var failures = new ConcurrentBag<string>();

        await Parallel.ForAsync(
            0,
            ThreadCount,
            async (_, ct) =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    try
                    {
                        string actual = parser.Parse(source).ToString();
                        if (actual != expected)
                        {
                            failures.Add($"Parse mismatch: {actual}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex.ToString());
                    }
                }
                await Task.CompletedTask;
            }
        );

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task Single_parser_handles_concurrent_Evaluate_with_deterministic_results()
    {
        var parser = new Parser();
        (string Expr, double Expected)[] cases =
        [
            ("1 + 2 * 3", 7d),
            ("(10 - 3) * 2", 14d),
            ("abs(-5) + sqrt(16)", 9d),
            ("ceil(1.1) + floor(2.9)", 4d),
            ("2 ^ 10", 1024d),
            ("100 / 4 + 5", 30d),
        ];

        var failures = new ConcurrentBag<string>();

        await Parallel.ForAsync(
            0,
            ThreadCount,
            async (_, ct) =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    (string expr, double expected) = cases[i % cases.Length];
                    try
                    {
                        Value result = parser.Evaluate(expr);
                        if (result is not Value.Number n || n.V != expected)
                        {
                            failures.Add($"{expr} -> {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{expr} threw: {ex.Message}");
                    }
                }
                await Task.CompletedTask;
            }
        );

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task Single_parser_handles_concurrent_TryParse_with_consistent_error_count()
    {
        var parser = new Parser();
        const string source = "a = 1; b = 1 +; c = 3";
        ParseResult baseline = parser.TryParse(source);
        int expectedErrors = baseline.Errors.Length;

        var failures = new ConcurrentBag<string>();

        await Parallel.ForAsync(
            0,
            ThreadCount,
            async (_, ct) =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    try
                    {
                        ParseResult result = parser.TryParse(source);
                        if (result.Errors.Length != expectedErrors)
                        {
                            failures.Add($"error count {result.Errors.Length}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex.ToString());
                    }
                }
                await Task.CompletedTask;
            }
        );

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task Shared_Expression_ToString_is_thread_safe()
    {
        var parser = new Parser();
        Expression expr = parser.Parse("(a + b) * c - sqrt(d) + abs(-e) + f(1, 2, 3)");
        string expected = expr.ToString();

        var observed = new ConcurrentBag<string>();

        await Parallel.ForAsync(
            0,
            ThreadCount,
            async (_, ct) =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    observed.Add(expr.ToString());
                }
                await Task.CompletedTask;
            }
        );

        await Assert.That(observed.All(s => s == expected)).IsTrue();
    }

    [Test]
    public async Task Mixed_concurrent_workload_against_one_parser_succeeds()
    {
        var parser = new Parser();
        var failures = new ConcurrentBag<string>();

        await Parallel.ForAsync(
            0,
            ThreadCount,
            async (threadIndex, ct) =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    try
                    {
                        switch ((threadIndex + i) % 4)
                        {
                            case 0:
                                _ = parser.Parse("1 + 2 * 3").ToString();
                                break;
                            case 1:
                                _ = parser.Evaluate("(4 + 6) / 2");
                                break;
                            case 2:
                                _ = parser.TryParse("x = 1; y = x + 1");
                                break;
                            case 3:
                                _ = parser.Parse("foo(a, b, c) + bar(d)").Symbols();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex.ToString());
                    }
                }
                await Task.CompletedTask;
            }
        );

        await Assert.That(failures).IsEmpty();
    }
}
