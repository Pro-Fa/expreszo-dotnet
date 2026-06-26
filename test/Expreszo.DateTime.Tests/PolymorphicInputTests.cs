namespace Expreszo.DateTimes.Tests;

// Port of test/polymorphic-input.test.ts. The TS "JS Date" and "Luxon DateTime"
// shapes map to a native System.DateTimeOffset and a Value.DateTime respectively.
public class PolymorphicInputTests
{
    private const string Iso = "2026-01-01T00:00:00Z";

    private static readonly long Ms = DateTimeOffset
        .Parse(Iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        .ToUnixTimeMilliseconds();

    private static readonly DateTimeOffset NativeDate = DateTimeOffset.FromUnixTimeMilliseconds(Ms);

    private static readonly Value.DateTime LuxonLike = new(NativeDate, DateFixture.Utc);

    private static readonly Parser P = DateFixture.ParserUtc();

    private static VariableResolver AllShapes() =>
        DateFixture.Vars(("ms", Ms), ("d", NativeDate), ("dt", LuxonLike));

    [Test]
    public async Task AddDuration_accepts_string_number_native_and_value_datetime()
    {
        const string Expected = "2026-01-08";
        VariableResolver vars = AllShapes();

        await Assert.That(DateFixture.IsoDate(P.Eval($"addDuration('{Iso}', 7, 'days')", vars))).IsEqualTo(Expected);
        await Assert.That(DateFixture.IsoDate(P.Eval("addDuration(ms, 7, 'days')", vars))).IsEqualTo(Expected);
        await Assert.That(DateFixture.IsoDate(P.Eval("addDuration(d,  7, 'days')", vars))).IsEqualTo(Expected);
        await Assert.That(DateFixture.IsoDate(P.Eval("addDuration(dt, 7, 'days')", vars))).IsEqualTo(Expected);
    }

    [Test]
    public async Task Format_accepts_every_input_shape()
    {
        VariableResolver vars = AllShapes();
        await Assert.That(DateFixture.Str(P.Eval($"format('{Iso}', 'yyyy-MM-dd')", vars))).IsEqualTo("2026-01-01");
        await Assert.That(DateFixture.Str(P.Eval("format(ms, 'yyyy-MM-dd')", vars))).IsEqualTo("2026-01-01");
        await Assert.That(DateFixture.Str(P.Eval("format(d,  'yyyy-MM-dd')", vars))).IsEqualTo("2026-01-01");
        await Assert.That(DateFixture.Str(P.Eval("format(dt, 'yyyy-MM-dd')", vars))).IsEqualTo("2026-01-01");
    }

    [Test]
    public async Task Quarter_isLeapYear_toUnix_toUTC_accept_every_input_shape()
    {
        VariableResolver vars = AllShapes();
        foreach (string expr in new[] { $"'{Iso}'", "ms", "d", "dt" })
        {
            await Assert.That(DateFixture.Num(P.Eval($"quarter({expr})", vars))).IsEqualTo(1);
            await Assert.That(DateFixture.Bool(P.Eval($"isLeapYear({expr})", vars))).IsFalse();
            await Assert.That(DateFixture.Num(P.Eval($"toUnix({expr})", vars))).IsEqualTo(1767225600);
            await Assert.That(DateFixture.Date(P.Eval($"toUTC({expr})", vars)).Zone.Id).IsEqualTo("UTC");
        }
    }

    [Test]
    public async Task CompareDates_containsDate_clampDate_accept_any_input_combination()
    {
        VariableResolver vars = AllShapes();
        await Assert.That(DateFixture.Num(P.Eval($"compareDates('{Iso}', dt)", vars))).IsEqualTo(0);
        await Assert.That(DateFixture.Bool(P.Eval("containsDate('2025-01-01', '2027-01-01', d)", vars))).IsTrue();

        Value clamped = P.Eval("clampDate(d, '2026-06-01', '2026-12-31')", vars);
        await Assert.That(DateFixture.IsoDate(clamped)).IsEqualTo("2026-06-01");
    }
}
