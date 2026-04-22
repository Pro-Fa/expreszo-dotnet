// AOT canary. CI publishes this with PublishAot=true so the trim / AOT
// analyzers see the library's entire call graph - including the JsonBridge,
// the Pratt parser, the evaluator walker, every built-in preset, and the
// validator. If anything the library does requires dynamic code generation
// or untrimmable reflection, the publish fails here and the build is red.

using System.Text.Json;
using Expreszo;

var parser = new Parser();

// Exercise every major surface at least once.
Value result = parser.Evaluate("max(1, 2, 3) + PI * 2");
Console.WriteLine($"arithmetic:  {result}");

using JsonDocument doc = JsonDocument.Parse("""{"xs":[1,2,3,4,5],"threshold":3}""");
Value filtered = parser.Evaluate("sum(filter(xs, x => x > threshold))", doc);
Console.WriteLine($"higher-order:{filtered}");

Expression expr = parser.Parse("(a + b) * 2");
Expression simplified = expr.Simplify(JsonDocument.Parse("""{"a":3,"b":4}"""));
Console.WriteLine($"simplify:    {simplified}");

Expression sub = expr.Substitute("a", "x * y");
Console.WriteLine($"substitute:  {sub}");

Console.WriteLine($"variables:   [{string.Join(", ", expr.Variables())}]");

// Member access + validator: __proto__ is rejected.
try
{
    parser.Evaluate("obj.__proto__", JsonDocument.Parse("""{"obj":{}}"""));
    Console.WriteLine("validator:   FAIL (should have thrown)");
}
catch (Expreszo.Errors.AccessException ex)
{
    Console.WriteLine($"validator:   OK ({ex.Message})");
}

Console.WriteLine("Expreszo AOT canary: OK");
