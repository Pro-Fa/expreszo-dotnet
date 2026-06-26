using System.Linq;

namespace Expreszo.DateTimes.Tests;

// Port of test/range.test.ts.
public class RangeTests
{
    private static readonly Parser P = DateFixture.ParserUtc();

    private static string Dates(Value v) =>
        string.Join(",", ((Value.Array)v).Items.Select(DateFixture.IsoDate));

    [Test]
    public async Task DateRange_produces_a_half_open_daily_sequence()
    {
        Value r = P.Evaluate("dateRange('2026-01-01', '2026-01-04', 'days')");
        await Assert.That(Dates(r)).IsEqualTo("2026-01-01,2026-01-02,2026-01-03");
    }

    [Test]
    public async Task DateRange_honours_a_custom_step()
    {
        Value r = P.Evaluate("dateRange('2026-01-01', '2026-01-10', 'days', 3)");
        await Assert.That(Dates(r)).IsEqualTo("2026-01-01,2026-01-04,2026-01-07");
    }

    [Test]
    public async Task DateRange_supports_hours()
    {
        Value r = P.Evaluate("dateRange('2026-01-01T00:00:00Z', '2026-01-01T03:00:00Z', 'hours')");
        await Assert.That(((Value.Array)r).Items.Length).IsEqualTo(3);
    }

    [Test]
    public async Task DateRange_returns_empty_when_start_after_end()
    {
        Value r = P.Evaluate("dateRange('2026-01-10', '2026-01-01', 'days')");
        await Assert.That(((Value.Array)r).Items.Length).IsEqualTo(0);
    }

    [Test]
    public async Task DateRange_rejects_an_invalid_step()
    {
        Action negative = () => P.Evaluate("dateRange('2026-01-01', '2026-01-04', 'days', -1)");
        Action zero = () => P.Evaluate("dateRange('2026-01-01', '2026-01-04', 'days', 0)");
        await Assert.That(negative).Throws<EvaluationException>();
        await Assert.That(zero).Throws<EvaluationException>();
    }

    [Test]
    public async Task BusinessDaysBetween_counts_mon_to_fri()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("businessDaysBetween('2026-01-05', '2026-01-12')"))).IsEqualTo(5);
    }

    [Test]
    public async Task BusinessDaysBetween_skips_weekends()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("businessDaysBetween('2026-01-02', '2026-01-06')"))).IsEqualTo(2);
    }

    [Test]
    public async Task BusinessDaysBetween_zero_for_empty_or_reversed()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("businessDaysBetween('2026-01-01', '2026-01-01')"))).IsEqualTo(0);
        await Assert.That(DateFixture.Num(P.Evaluate("businessDaysBetween('2026-01-10', '2026-01-01')"))).IsEqualTo(0);
    }

    [Test]
    public async Task WeekdaysBetween_counts_a_single_weekday()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("weekdaysBetween('2026-01-05', '2026-02-02', 1)"))).IsEqualTo(4);
    }

    [Test]
    public async Task WeekdaysBetween_zero_for_empty_ranges()
    {
        await Assert.That(DateFixture.Num(P.Evaluate("weekdaysBetween('2026-01-01', '2026-01-01', 3)"))).IsEqualTo(0);
    }

    [Test]
    public async Task WeekdaysBetween_rejects_an_invalid_weekday()
    {
        Action low = () => P.Evaluate("weekdaysBetween('2026-01-01', '2026-01-31', 0)");
        Action high = () => P.Evaluate("weekdaysBetween('2026-01-01', '2026-01-31', 8)");
        Action fractional = () => P.Evaluate("weekdaysBetween('2026-01-01', '2026-01-31', 1.5)");
        await Assert.That(low).Throws<EvaluationException>();
        await Assert.That(high).Throws<EvaluationException>();
        await Assert.That(fractional).Throws<EvaluationException>();
    }
}
