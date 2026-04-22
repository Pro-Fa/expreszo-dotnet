namespace Expreszo.Tests;

public class SmokeTests
{
    [Test]
    public async Task Bootstrap_harness_runs()
    {
        // Guards that the TUnit source generator, MTP entry point, and
        // NSubstitute reference all link and run. Replaced with real tests
        // starting in Phase 1.
        var values = new[] { 1, 2, 3 };

        await Assert.That(values.Sum()).IsEqualTo(6);
    }
}
