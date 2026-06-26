namespace Expreszo.DateTimes.Tests;

// Port of test/distance.test.ts. The TS vi.setSystemTime(FIXED_NOW) is replaced
// by a parser whose clock is pinned to FIXED_NOW.
public class DistanceTests
{
    private static readonly Parser P = DateFixture.ParserAt(
        DateTimeOffset.Parse(
            "2026-04-15T12:00:00Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind
        )
    );

    [Test]
    public async Task DaysUntil_returns_positive_integers_for_future_dates() =>
        await Assert.That(DateFixture.Num(P.Evaluate("daysUntil('2026-04-20T12:00:00Z')"))).IsEqualTo(5);

    [Test]
    public async Task DaysUntil_returns_negative_integers_for_past_dates() =>
        await Assert.That(DateFixture.Num(P.Evaluate("daysUntil('2026-04-10T12:00:00Z')"))).IsEqualTo(-5);

    [Test]
    public async Task DaysSince_inverts_daysUntil()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("daysSince('2026-04-10T12:00:00Z')"))).IsEqualTo(5);
        await Assert.That(DateFixture.Num(P.Evaluate("daysSince('2026-04-20T12:00:00Z')"))).IsEqualTo(-5);
    }

    [Test]
    public async Task HoursUntil_and_hoursSince()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("hoursUntil('2026-04-15T18:00:00Z')"))).IsEqualTo(6);
        await Assert.That(DateFixture.Num(P.Evaluate("hoursSince('2026-04-15T06:00:00Z')"))).IsEqualTo(6);
    }

    [Test]
    public async Task MinutesUntil_and_minutesSince()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("minutesUntil('2026-04-15T12:30:00Z')"))).IsEqualTo(30);
        await Assert.That(DateFixture.Num(P.Evaluate("minutesSince('2026-04-15T11:30:00Z')"))).IsEqualTo(30);
    }

    [Test]
    public async Task Truncates_fractional_values_toward_zero()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("hoursUntil('2026-04-15T18:30:00Z')"))).IsEqualTo(6);
        await Assert.That(DateFixture.Num(P.Evaluate("hoursSince('2026-04-15T05:30:00Z')"))).IsEqualTo(6);
    }
}
