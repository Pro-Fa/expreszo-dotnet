namespace Expreszo.DateTimes.Tests;

// Port of test/plugin.test.ts.
public class PluginTests
{
    private static readonly Parser P = DateFixture.ParserUtc();

    // ---------- plugin identity ----------

    [Test]
    public async Task Declares_a_plugin_with_the_expected_identity()
    {
        var plugin = new ExpreszoDateTimePlugin();
        await Assert.That(plugin.Name).IsEqualTo("@pro-fa/expreszo-datetime");
        await Assert.That(ExpreszoDateTimePlugin.FunctionNames.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Registers_a_distinct_set_of_function_names()
    {
        var names = ExpreszoDateTimePlugin.FunctionNames;
        var distinct = new HashSet<string>(names, StringComparer.Ordinal);
        await Assert.That(distinct.Count).IsEqualTo(names.Count);
    }

    [Test]
    public async Task Registers_the_same_number_of_functions_as_the_typescript_plugin()
    {
        // Parity with DATETIME_FUNCTIONS in @pro-fa/expreszo-datetime (76 functions).
        await Assert.That(ExpreszoDateTimePlugin.FunctionNames.Count).IsEqualTo(76);
    }

    // ---------- construction ----------

    [Test]
    public async Task ParseISO_returns_a_datetime()
    {
        Value result = P.Evaluate("parseISO('2026-01-01T00:00:00Z')");
        await Assert.That(result).IsTypeOf<Value.DateTime>();
        await Assert.That(DateFixture.Date(result).Local.Year).IsEqualTo(2026);
    }

    [Test]
    public async Task FromMillis_builds_a_datetime_from_a_number()
    {
        long ms = DateTimeOffset.Parse(
            "2026-01-01T00:00:00Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind
        ).ToUnixTimeMilliseconds();
        Value result = P.Evaluate($"fromMillis({ms})");
        await Assert.That(DateFixture.Date(result).Local.Year).IsEqualTo(2026);
    }

    [Test]
    public async Task Now_and_today_are_evaluated_each_call()
    {
        Value a = P.Evaluate("now()");
        await Assert.That(a).IsTypeOf<Value.DateTime>();

        Value b = P.Evaluate("today()");
        await Assert.That(b).IsTypeOf<Value.DateTime>();
        await Assert.That(DateFixture.Date(b).Local.Hour).IsEqualTo(0);
        await Assert.That(DateFixture.Date(b).Local.Minute).IsEqualTo(0);
    }

    // ---------- arithmetic ----------

    [Test]
    public async Task AddDuration_moves_the_date_forward()
    {
        Value r = P.Evaluate("addDuration('2026-01-01', 7, 'days')");
        await Assert.That(DateFixture.IsoDate(r)).IsEqualTo("2026-01-08");
    }

    [Test]
    public async Task SubtractDuration_moves_the_date_backward()
    {
        Value r = P.Evaluate("subtractDuration('2026-01-08', 7, 'days')");
        await Assert.That(DateFixture.IsoDate(r)).IsEqualTo("2026-01-01");
    }

    [Test]
    public async Task Diff_returns_the_unit_converted_difference()
    {
        Value r = P.Evaluate("diff('2026-01-08', '2026-01-01', 'days')");
        await Assert.That(DateFixture.Num(r)).IsEqualTo(7);
    }

    [Test]
    public async Task StartOf_truncates_to_the_requested_unit()
    {
        Value r = P.Evaluate("startOf(setZone('2026-04-15T13:45:30Z', 'utc'), 'month')");
        await Assert.That(DateFixture.IsoDate(r)).IsEqualTo("2026-04-01");
    }

    [Test]
    public async Task Rejects_unknown_units()
    {
        Action act = () => P.Evaluate("addDuration('2026-01-01', 1, 'fortnights')");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---------- comparison ----------

    [Test]
    public async Task IsBefore_and_isAfter()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isBefore('2026-01-01', '2026-01-02')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isAfter('2026-01-02', '2026-01-01')"))).IsTrue();
    }

    [Test]
    public async Task IsSame_with_no_unit_checks_exact_equality()
    {
        await Assert
            .That(DateFixture.Bool(P.Evaluate("isSame('2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z')")))
            .IsTrue();
        await Assert
            .That(DateFixture.Bool(P.Evaluate("isSame('2026-01-01T00:00:00Z', '2026-01-01T00:00:01Z')")))
            .IsFalse();
    }

    [Test]
    public async Task IsSame_with_a_unit_truncates_before_comparing()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isSame('2026-01-01', '2026-01-31', 'month')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isSame('2026-01-01', '2026-02-01', 'month')"))).IsFalse();
    }

    // ---------- format and zone ----------

    [Test]
    public async Task Format_applies_a_pattern()
    {
        await Assert
            .That(DateFixture.Str(P.Evaluate("format('2026-01-08T00:00:00Z', 'yyyy-MM-dd')")))
            .IsEqualTo("2026-01-08");
    }

    [Test]
    public async Task ToISO_renders_iso_8601()
    {
        string iso = DateFixture.Str(P.Evaluate("toISO(parseISO('2026-01-01T00:00:00Z'))"));
        await Assert.That(iso.StartsWith("2026-01-01", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ToMillis_returns_unix_milliseconds()
    {
        long expected = DateTimeOffset.Parse(
            "2026-01-01T00:00:00Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind
        ).ToUnixTimeMilliseconds();
        await Assert.That(DateFixture.Num(P.Evaluate("toMillis('2026-01-01T00:00:00Z')"))).IsEqualTo(expected);
    }

    // ---------- inspection ----------

    [Test]
    public async Task Exposes_calendar_parts()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("year('2026-04-15T00:00:00Z')"))).IsEqualTo(2026);
        await Assert.That(DateFixture.Num(P.Evaluate("month('2026-04-15T00:00:00Z')"))).IsEqualTo(4);
        await Assert.That(DateFixture.Num(P.Evaluate("day('2026-04-15T00:00:00Z')"))).IsEqualTo(15);
    }

    [Test]
    public async Task IsWeekend_recognises_saturday_and_sunday()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isWeekend('2026-04-18')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isWeekend('2026-04-19')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isWeekend('2026-04-20')"))).IsFalse();
    }

    [Test]
    public async Task IsValid_rejects_garbage_strings()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isValid('not a date')"))).IsFalse();
        await Assert.That(DateFixture.Bool(P.Evaluate("isValid('2026-01-01')"))).IsTrue();
    }

    // ---------- == and != on DateTime values via core operators ----------

    [Test]
    public async Task Two_datetimes_at_the_same_instant_are_equal()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') == parseISO('2026-01-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') != parseISO('2026-01-01')"))).IsFalse();
    }

    [Test]
    public async Task Two_datetimes_at_different_instants_are_not_equal()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') == parseISO('2026-01-02')"))).IsFalse();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') != parseISO('2026-01-02')"))).IsTrue();
    }

    [Test]
    public async Task A_datetime_equals_an_equivalent_native_date()
    {
        var d = DateTimeOffset.Parse(
            "2026-01-01T00:00:00Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind
        );
        Value result = P.Eval(
            "parseISO('2026-01-01T00:00:00Z') == d",
            DateFixture.Vars(("d", d))
        );
        await Assert.That(DateFixture.Bool(result)).IsTrue();
    }

    [Test]
    public async Task Relational_operators_work()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') <  parseISO('2026-02-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-02-01') >  parseISO('2026-01-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') <= parseISO('2026-01-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("parseISO('2026-01-01') >= parseISO('2026-01-01')"))).IsTrue();
    }

    // ---------- end-to-end pipeline ----------

    [Test]
    public async Task Chains_construction_arithmetic_and_formatting()
    {
        await Assert
            .That(DateFixture.Str(P.Evaluate("format(addDuration('2026-01-01', 7, 'days'), 'yyyy-MM-dd')")))
            .IsEqualTo("2026-01-08");
    }
}
