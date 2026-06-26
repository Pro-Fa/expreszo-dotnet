namespace Expreszo.DateTimes.Tests;

// Port of test/additions-0.2.0.test.ts.
public class Additions020Tests
{
    private static DateTimeOffset Instant(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static readonly Parser PConstruct = DateFixture.ParserAt(Instant("2026-04-15T10:30:00Z"));
    private static readonly Parser PNow = DateFixture.ParserAt(Instant("2026-04-15T12:00:00Z"));
    private static readonly Parser P = DateFixture.ParserUtc();

    // ---- construction additions (now = 2026-04-15T10:30:00Z) ----

    [Test]
    public async Task Yesterday_is_start_of_the_day_before_now()
    {
        Value r = PConstruct.Evaluate("yesterday()");
        await Assert.That(DateFixture.Millis(r)).IsEqualTo(Instant("2026-04-14T00:00:00Z").ToUnixTimeMilliseconds());
    }

    [Test]
    public async Task Tomorrow_is_start_of_the_day_after_now()
    {
        Value r = PConstruct.Evaluate("tomorrow()");
        await Assert.That(DateFixture.Millis(r)).IsEqualTo(Instant("2026-04-16T00:00:00Z").ToUnixTimeMilliseconds());
    }

    [Test]
    public async Task Date_builds_a_datetime_at_midnight()
    {
        Value.DateTime r = DateFixture.Date(PConstruct.Evaluate("date(2026, 1, 15)"));
        await Assert.That(r.Local.Year).IsEqualTo(2026);
        await Assert.That(r.Local.Month).IsEqualTo(1);
        await Assert.That(r.Local.Day).IsEqualTo(15);
        await Assert.That(r.Local.Hour).IsEqualTo(0);
        await Assert.That(r.Local.Minute).IsEqualTo(0);
    }

    [Test]
    public async Task Time_sets_today_at_the_given_clock_time()
    {
        Value.DateTime r = DateFixture.Date(PConstruct.Evaluate("time(13, 45)"));
        await Assert.That(r.Local.Hour).IsEqualTo(13);
        await Assert.That(r.Local.Minute).IsEqualTo(45);
        await Assert.That(r.Local.Second).IsEqualTo(0);
    }

    [Test]
    public async Task Time_accepts_optional_second_and_millisecond()
    {
        Value.DateTime r = DateFixture.Date(PConstruct.Evaluate("time(13, 45, 30, 500)"));
        await Assert.That(r.Local.Second).IsEqualTo(30);
        await Assert.That(r.Local.Millisecond).IsEqualTo(500);
    }

    [Test]
    public async Task Time_rejects_non_numeric_hour_or_minute()
    {
        Action act = () => PConstruct.Evaluate("time('a', 'b')");
        await Assert.That(act).Throws<EvaluationException>();
    }

    [Test]
    public async Task FromUnix_builds_from_unix_seconds()
    {
        Value r = PConstruct.Evaluate("fromUnix(1767225600)");
        await Assert.That(DateFixture.IsoDate(r)).IsEqualTo("2026-01-01");
    }

    [Test]
    public async Task FromUnix_throws_on_non_number()
    {
        Action act = () => PConstruct.Evaluate("fromUnix('1767225600')");
        await Assert.That(act).Throws<EvaluationException>();
    }

    // ---- inspection: extra calendar parts ----

    [Test]
    public async Task Quarter()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("quarter('2026-01-15')"))).IsEqualTo(1);
        await Assert.That(DateFixture.Num(P.Evaluate("quarter('2026-04-15')"))).IsEqualTo(2);
        await Assert.That(DateFixture.Num(P.Evaluate("quarter('2026-09-15')"))).IsEqualTo(3);
        await Assert.That(DateFixture.Num(P.Evaluate("quarter('2026-12-15')"))).IsEqualTo(4);
    }

    [Test]
    public async Task IsoWeekYear_differs_around_the_boundary()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("isoWeekYear('2026-01-01')"))).IsEqualTo(2026);
        await Assert.That(DateFixture.Num(P.Evaluate("isoWeekYear('2025-12-29')"))).IsEqualTo(2026);
    }

    [Test]
    public async Task IsLeapYear()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isLeapYear('2024-06-15')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isLeapYear('2026-06-15')"))).IsFalse();
    }

    [Test]
    public async Task DaysInYear()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("daysInYear('2024-01-01')"))).IsEqualTo(366);
        await Assert.That(DateFixture.Num(P.Evaluate("daysInYear('2026-01-01')"))).IsEqualTo(365);
    }

    [Test]
    public async Task WeeksInYear()
    {
        // weeksInYear returns an integral count; compare as an int.
        int weeks = (int)DateFixture.Num(P.Evaluate("weeksInYear('2026-06-01')"));
        await Assert.That(weeks is 52 or 53).IsTrue();
    }

    [Test]
    public async Task IsDST_returns_a_boolean()
    {
        Value r = P.Evaluate("isDST('2026-07-15T12:00:00')");
        await Assert.That(r).IsTypeOf<Value.Boolean>();
    }

    [Test]
    public async Task OffsetMinutes_and_offsetHours()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("offsetMinutes(setZone('2026-01-15T00:00:00Z', 'utc'))"))).IsEqualTo(0);
        await Assert.That(DateFixture.Num(P.Evaluate("offsetHours(setZone('2026-01-15T00:00:00Z', 'utc'))"))).IsEqualTo(0);
    }

    [Test]
    public async Task ZoneName()
    {
        await Assert
            .That(DateFixture.Str(P.Evaluate("zoneName(setZone('2026-01-15T00:00:00Z', 'America/New_York'))")))
            .IsEqualTo("America/New_York");
    }

    // ---- inspection: relative-to-now predicates (now = 2026-04-15T12:00:00Z, Wednesday) ----

    [Test]
    public async Task IsToday_isYesterday_isTomorrow()
    {
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isToday(now())"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isYesterday(yesterday())"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isTomorrow(tomorrow())"))).IsTrue();

        await Assert.That(DateFixture.Bool(PNow.Evaluate("isToday(yesterday())"))).IsFalse();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isYesterday(now())"))).IsFalse();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isTomorrow(now())"))).IsFalse();
    }

    [Test]
    public async Task IsThisWeek_isThisMonth_isThisYear()
    {
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isThisWeek(now())"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isThisMonth(now())"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isThisYear(now())"))).IsTrue();

        await Assert.That(DateFixture.Bool(PNow.Evaluate("isThisYear('2025-04-15')"))).IsFalse();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isThisMonth('2026-03-15')"))).IsFalse();
    }

    [Test]
    public async Task IsInPast_isInFuture()
    {
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isInPast('2026-01-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isInFuture('2027-01-01')"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isInPast('2027-01-01')"))).IsFalse();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isInFuture('2026-01-01')"))).IsFalse();
    }

    [Test]
    public async Task IsWeekday_isWeekend()
    {
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isWeekday(now())"))).IsTrue();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isWeekend(now())"))).IsFalse();
        await Assert.That(DateFixture.Bool(PNow.Evaluate("isWeekday('2026-04-18')"))).IsFalse();
    }

    [Test]
    public async Task Age_returns_whole_years_floored_and_clamped()
    {
        await Assert.That(DateFixture.Num(PNow.Evaluate("age('2000-04-15')"))).IsEqualTo(26);
        await Assert.That(DateFixture.Num(PNow.Evaluate("age('2000-04-16')"))).IsEqualTo(25);
        await Assert.That(DateFixture.Num(PNow.Evaluate("age('2030-01-01')"))).IsEqualTo(0);
    }

    // ---- arithmetic: clampDate / minDate / maxDate ----

    [Test]
    public async Task ClampDate_clamps_within_bounds()
    {
        await Assert.That(DateFixture.IsoDate(P.Evaluate("clampDate('2026-01-15', '2026-01-10', '2026-01-20')"))).IsEqualTo("2026-01-15");
        await Assert.That(DateFixture.IsoDate(P.Evaluate("clampDate('2026-01-05', '2026-01-10', '2026-01-20')"))).IsEqualTo("2026-01-10");
        await Assert.That(DateFixture.IsoDate(P.Evaluate("clampDate('2026-01-25', '2026-01-10', '2026-01-20')"))).IsEqualTo("2026-01-20");
    }

    [Test]
    public async Task MinDate_maxDate_handle_multiple_inputs()
    {
        await Assert.That(DateFixture.IsoDate(P.Evaluate("minDate('2026-03-01', '2026-01-01', '2026-02-01')"))).IsEqualTo("2026-01-01");
        await Assert.That(DateFixture.IsoDate(P.Evaluate("maxDate('2026-03-01', '2026-01-01', '2026-02-01')"))).IsEqualTo("2026-03-01");
    }

    [Test]
    public async Task MinDate_maxDate_throw_on_no_arguments()
    {
        Action min = () => P.Evaluate("minDate()");
        Action max = () => P.Evaluate("maxDate()");
        await Assert.That(min).Throws<EvaluationException>();
        await Assert.That(max).Throws<EvaluationException>();
    }

    // ---- comparison additions ----

    [Test]
    public async Task IsBetween_defaults_to_inclusive()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isBetween('2026-01-15', '2026-01-01', '2026-01-31')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isBetween('2026-01-01', '2026-01-01', '2026-01-31')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("isBetween('2026-02-01', '2026-01-01', '2026-01-31')"))).IsFalse();
    }

    [Test]
    public async Task IsBetween_exclusive_when_inclusive_false()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("isBetween('2026-01-01', '2026-01-01', '2026-01-31', false)"))).IsFalse();
        await Assert.That(DateFixture.Bool(P.Evaluate("isBetween('2026-01-15', '2026-01-01', '2026-01-31', false)"))).IsTrue();
    }

    [Test]
    public async Task CompareDates_returns_minus_one_zero_one()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("compareDates('2026-01-01', '2026-01-02')"))).IsEqualTo(-1);
        await Assert.That(DateFixture.Num(P.Evaluate("compareDates('2026-01-02', '2026-01-01')"))).IsEqualTo(1);
        await Assert.That(DateFixture.Num(P.Evaluate("compareDates('2026-01-01', '2026-01-01')"))).IsEqualTo(0);
    }

    [Test]
    public async Task OverlapsRange_detects_overlap()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("overlapsRange('2026-01-01', '2026-01-10', '2026-01-05', '2026-01-15')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("overlapsRange('2026-01-01', '2026-01-10', '2026-02-01', '2026-02-10')"))).IsFalse();
    }

    [Test]
    public async Task ContainsDate()
    {
        await Assert.That(DateFixture.Bool(P.Evaluate("containsDate('2026-01-01', '2026-01-31', '2026-01-15')"))).IsTrue();
        await Assert.That(DateFixture.Bool(P.Evaluate("containsDate('2026-01-01', '2026-01-31', '2026-02-15')"))).IsFalse();
    }

    // ---- format / zone additions (now = 2026-04-15T12:00:00Z) ----

    [Test]
    public async Task ToRelative_produces_a_string()
    {
        string r = DateFixture.Str(PNow.Evaluate("toRelative('2026-04-20T12:00:00Z')"));
        await Assert.That(r.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ToRelative_accepts_an_explicit_base()
    {
        string r = DateFixture.Str(PNow.Evaluate("toRelative('2026-04-20T12:00:00Z', '2026-04-15T12:00:00Z')"));
        await Assert.That(r.Contains('5', StringComparison.Ordinal)).IsTrue();
        await Assert.That(r.Contains("day", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ToRelativeCalendar_produces_a_calendar_string()
    {
        await Assert.That(DateFixture.Str(PNow.Evaluate("toRelativeCalendar(tomorrow())"))).IsEqualTo("tomorrow");
    }

    [Test]
    public async Task ToUnix_returns_whole_seconds()
    {
        await Assert.That(DateFixture.Num(PNow.Evaluate("toUnix('2026-01-01T00:00:00Z')"))).IsEqualTo(1767225600);
    }

    [Test]
    public async Task ToUTC_sets_the_zone_to_utc()
    {
        await Assert.That(DateFixture.Date(PNow.Evaluate("toUTC('2026-01-01T00:00:00Z')")).Zone.Id).IsEqualTo("UTC");
    }

    [Test]
    public async Task ToLocal_sets_the_zone_to_local()
    {
        await Assert
            .That(DateFixture.Date(PNow.Evaluate("toLocal('2026-01-01T00:00:00Z')")).Zone.Id)
            .IsEqualTo(DateFixture.Utc.Id);
    }
}
