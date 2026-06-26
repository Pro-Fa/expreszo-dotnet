using Expreszo.Json;

namespace Expreszo.DateTimes.Tests;

// Regression tests for the code-review fixes.
public class ReviewFixesTests
{
    private static readonly Parser P = DateFixture.ParserUtc();

    // Fix #1: a DateTime result serializes to an ISO 8601 string instead of
    // throwing "Unknown Value variant".
    [Test]
    public async Task DateTime_result_serializes_to_json_iso_string()
    {
        Value result = P.Evaluate("parseISO('2026-01-01T00:00:00Z')");
        string json = JsonBridge.ToJsonString(result);
        await Assert.That(json.StartsWith("\"2026-01-01", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task DateTime_inside_object_serializes_to_json()
    {
        // Object property carrying a DateTime must serialize, not be skipped or throw.
        Value result = P.Evaluate("{ 'when': parseISO('2026-01-01T00:00:00Z') }");
        string json = JsonBridge.ToJsonString(result);
        await Assert.That(json.Contains("2026-01-01", StringComparison.Ordinal)).IsTrue();
    }

    // Fix #2: under-applying a function surfaces a controlled EvaluationException,
    // not a raw IndexOutOfRangeException.
    [Test]
    public async Task Under_applied_function_throws_controlled_exception()
    {
        Action year = () => P.Evaluate("year()");
        Action diff = () => P.Evaluate("diff('2026-01-01', '2026-01-02')");
        await Assert.That(year).Throws<EvaluationException>();
        await Assert.That(diff).Throws<EvaluationException>();
    }

    // A fractional calendar amount is rejected rather than silently truncating
    // to a non-advancing step (which would hang dateRange).
    [Test]
    public async Task Fractional_calendar_amount_is_rejected()
    {
        Action add = () => P.Evaluate("addDuration('2026-01-01', 0.5, 'months')");
        Action range = () => P.Evaluate("dateRange('2026-01-01', '2026-06-01', 'months', 0.5)");
        await Assert.That(add).Throws<EvaluationException>();
        await Assert.That(range).Throws<EvaluationException>();
    }

    // Fix #3: a DateTime coerces to Unix milliseconds in numeric contexts, so
    // mixed-type relational operators are meaningful.
    [Test]
    public async Task DateTime_coerces_to_millis_in_numeric_comparison()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01T00:00:00Z') > 0"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01T00:00:00Z') < 9999999999999"))).IsTrue();
        await Assert.That(DateFixture.Num(P.Evaluate("parseISO('2026-01-01T00:00:00Z') as 'number'"))).IsEqualTo(1767225600000d);
    }
}
