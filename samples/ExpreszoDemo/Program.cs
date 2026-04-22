// End-user demo: parse, evaluate, simplify, and substitute using JsonDocument.

using System.Text.Json;
using Expreszo;

var parser = new Parser();

// --- 1. Basic evaluation with variables from a JsonDocument. ---
using var scope = JsonDocument.Parse("""{"items":[10,20,30],"tax":0.21}""");
var total = parser.Evaluate("sum(items) * (1 + tax)", scope);
Console.WriteLine($"Total with tax: {total}");

// --- 2. Parse once, evaluate many times. ---
var pricing = parser.Parse("base * quantity - (base * quantity * discount)");
var orders = new (double Base, int Qty, double Discount)[]
{
    (10d, 3, 0.0),
    (10d, 10, 0.15),
    (25d, 2, 0.05),
};
foreach (var (basePrice, qty, discount) in orders)
{
    using var values = JsonDocument.Parse(
        $$""" { "base": {{basePrice}}, "quantity": {{qty}}, "discount": {{discount}} } """);
    Console.WriteLine($"  qty={qty}: {pricing.Evaluate(values)}");
}

// --- 3. Simplify with known values to pre-compute constants. ---
var rule = parser.Parse("(x + 5) * (y + 10)");
using var partial = JsonDocument.Parse("""{"x":3}""");
var simplified = rule.Simplify(partial);
Console.WriteLine($"Partial simplify: {simplified}  (variables: [{string.Join(", ", simplified.Variables())}])");

// --- 4. Higher-order: array callbacks. ---
using var data = JsonDocument.Parse("""{"people":[{"age":25},{"age":17},{"age":42}]}""");
var adults = parser.Evaluate("filter(people, p => p.age >= 18)", data);
Console.WriteLine($"Adults: {adults}");

// --- 5. Custom resolver for computed variables. ---
VariableResolver computed = name => name switch
{
    "now" => new VariableResolveResult.Bound(new Value.String(DateTimeOffset.UtcNow.ToString("O"))),
    _ => VariableResolveResult.NotResolved,
};
var ts = parser.Evaluate("now", resolver: computed);
Console.WriteLine($"now via resolver: {ts}");
