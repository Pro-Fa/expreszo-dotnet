namespace Expreszo.DateTimes.Tests;

// Port of test/coverage-fillers.test.ts.
public class CoverageFillersTests
{
    private static readonly Parser P = DateFixture.ParserUtc();
    private static readonly DateTimeOptions O = DateFixture.UtcOptions;

    // ---- parseDate ----

    [Test]
    public async Task ParseDate_with_a_format_token()
    {
        Value r = P.Evaluate("parseDate('20/01/2026', 'dd/MM/yyyy')");
        await Assert.That(DateFixture.IsoDate(r)).IsEqualTo("2026-01-20");
    }

    [Test]
    public async Task ParseDate_with_an_explicit_zone()
    {
        Value r = P.Evaluate("parseDate('2026-01-20 12:00', 'yyyy-MM-dd HH:mm', 'America/New_York')");
        await Assert.That(DateFixture.Date(r).Zone.Id).IsEqualTo("America/New_York");
    }

    [Test]
    public async Task ParseDate_throws_when_input_or_format_not_string()
    {
        Action act = () => P.Evaluate("parseDate(42, 42)");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- dateTime constructor ----

    [Test]
    public async Task DateTime_builds_from_ymd()
    {
        Value.DateTime r = DateFixture.Date(P.Evaluate("dateTime(2026, 1, 20)"));
        await Assert.That(r.Local.Year).IsEqualTo(2026);
        await Assert.That(r.Local.Month).IsEqualTo(1);
        await Assert.That(r.Local.Day).IsEqualTo(20);
    }

    [Test]
    public async Task DateTime_accepts_optional_time_components()
    {
        Value.DateTime r = DateFixture.Date(P.Evaluate("dateTime(2026, 1, 20, 13, 45, 30)"));
        await Assert.That(r.Local.Hour).IsEqualTo(13);
        await Assert.That(r.Local.Minute).IsEqualTo(45);
        await Assert.That(r.Local.Second).IsEqualTo(30);
    }

    [Test]
    public async Task DateTime_throws_when_components_not_numbers()
    {
        Action act = () => P.Evaluate("dateTime('a', 'b', 'c')");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- parseISO / fromMillis edge cases ----

    [Test]
    public async Task ParseISO_throws_on_non_string()
    {
        Action act = () => P.Evaluate("parseISO(42)");
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task FromMillis_throws_on_non_number()
    {
        Action act = () => P.Evaluate("fromMillis('not a number')");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- Normalize.ToDateTimeOrUndefined ----

    [Test]
    public async Task ToDateTimeOrUndefined_returns_null_for_null_and_undefined()
    {
        await Assert.That(Normalize.ToDateTimeOrUndefined(Value.Null.Instance, O)).IsNull();
        await Assert.That(Normalize.ToDateTimeOrUndefined(Value.Undefined.Instance, O)).IsNull();
    }

    [Test]
    public async Task ToDateTimeOrUndefined_delegates_for_actual_values()
    {
        Value.DateTime? r = Normalize.ToDateTimeOrUndefined(new Value.String("2026-01-01"), O);
        await Assert.That(r).IsNotNull();
    }

    // ---- Normalize.ToDateTime rejects unsupported shapes ----

    [Test]
    public async Task ToDateTime_throws_on_object()
    {
        Action act = () => Normalize.ToDateTime(Value.Object.Empty, O);
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task ToDateTime_throws_on_boolean()
    {
        Action act = () => Normalize.ToDateTime(Value.Boolean.True, O);
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- format / setZone / isSame error paths ----

    [Test]
    public async Task Format_rejects_non_string_pattern()
    {
        Action act = () => P.Evaluate("format(now(), 42)");
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task SetZone_rejects_non_string_zone()
    {
        Action act = () => P.Evaluate("setZone(now(), 42)");
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task IsSame_rejects_non_string_unit()
    {
        Action act = () => P.Evaluate("isSame('2026-01-01', '2026-01-02', 42)");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- every inspector returns the expected value ----

    [Test]
    public async Task Calendar_part_inspectors()
    {
        const string Z = "setZone('2026-04-15T13:45:30.123Z', 'utc')";
        await Assert.That(DateFixture.Num(P.Evaluate($"year({Z})"))).IsEqualTo(2026);
        await Assert.That(DateFixture.Num(P.Evaluate($"month({Z})"))).IsEqualTo(4);
        await Assert.That(DateFixture.Num(P.Evaluate($"day({Z})"))).IsEqualTo(15);
        await Assert.That(DateFixture.Num(P.Evaluate($"hour({Z})"))).IsEqualTo(13);
        await Assert.That(DateFixture.Num(P.Evaluate($"minute({Z})"))).IsEqualTo(45);
        await Assert.That(DateFixture.Num(P.Evaluate($"second({Z})"))).IsEqualTo(30);
        await Assert.That(DateFixture.Num(P.Evaluate($"millisecond({Z})"))).IsEqualTo(123);
    }

    [Test]
    public async Task DayOfWeek_dayOfYear_weekOfYear_daysInMonth()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("dayOfWeek('2026-04-15')"))).IsGreaterThan(0);
        await Assert.That(DateFixture.Num(P.Evaluate("dayOfYear('2026-04-15')"))).IsGreaterThan(0);
        await Assert.That(DateFixture.Num(P.Evaluate("weekOfYear('2026-04-15')"))).IsGreaterThan(0);
        await Assert.That(DateFixture.Num(P.Evaluate("daysInMonth('2026-04-15')"))).IsEqualTo(30);
    }

    [Test]
    public async Task IsWeekend_covers_sunday()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isWeekend('2026-04-19')"))).IsTrue();
    }

    // ---- isValid for additional shapes ----

    [Test]
    public async Task IsValid_accepts_a_native_date()
    {
        var d = DateTimeOffset.Parse("2026-01-01", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        await Assert.That(DateFixture.Bool(P.Eval("isValid(d)", DateFixture.Vars(("d", d))))).IsTrue();
    }

    [Test]
    public async Task IsValid_accepts_a_value_datetime()
    {
        Value.DateTime dt = new(
            DateTimeOffset.Parse("2026-01-01", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateFixture.Utc
        );
        await Assert.That(DateFixture.Bool(P.Eval("isValid(dt)", DateFixture.Vars(("dt", dt))))).IsTrue();
    }

    [Test]
    public async Task IsValid_accepts_a_millisecond_number()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isValid(1767225600000)"))).IsTrue();
    }

    [Test]
    public async Task IsValid_rejects_nan()
    {
        await Assert.That(DateFixture.Bool(P.Eval("isValid(n)", DateFixture.Vars(("n", double.NaN))))).IsFalse();
    }

    [Test]
    public async Task IsValid_rejects_an_arbitrary_object()
    {
        VariableResolver resolver = name =>
            name == "o"
                ? new VariableResolveResult.Bound(Value.Object.Empty)
                : VariableResolveResult.NotResolved;
        await Assert.That(DateFixture.Bool(P.Eval("isValid(o)", resolver))).IsFalse();
    }
}
