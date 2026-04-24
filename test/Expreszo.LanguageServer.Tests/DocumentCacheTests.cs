using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Expreszo.LanguageServer.Tests;

public class DocumentCacheTests
{
    private static readonly DocumentUri Uri = DocumentUri.From("file:///tmp/test.zo");

    [Test]
    public async Task Update_with_valid_source_populates_root_and_clears_errors()
    {
        var cache = new DocumentCache();

        ExpreszoTextDocument doc = cache.Update(Uri, "1 + 2", version: 1);

        await Assert.That(doc.Root).IsNotNull();
        await Assert.That(doc.HasErrors).IsFalse();
    }

    [Test]
    public async Task Update_with_invalid_source_records_at_least_one_error()
    {
        var cache = new DocumentCache();

        ExpreszoTextDocument doc = cache.Update(Uri, "1 +", version: 1);

        await Assert.That(doc.HasErrors).IsTrue();
    }

    [Test]
    public async Task Update_with_partial_failure_keeps_healthy_statements()
    {
        var cache = new DocumentCache();

        ExpreszoTextDocument doc = cache.Update(Uri, "a = 1; b = 1 +; c = 3", version: 1);

        await Assert.That(doc.HasErrors).IsTrue();
        await Assert.That(doc.Errors.Length).IsEqualTo(1);
        // The a = ... and c = ... statements should still produce AST.
        await Assert.That(doc.Root).IsNotNull();
    }

    [Test]
    public async Task Remove_deletes_the_entry()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "1", version: 1);

        cache.Remove(Uri);

        await Assert.That(cache.TryGet(Uri)).IsNull();
    }

    [Test]
    public async Task TryGet_returns_the_latest_update()
    {
        var cache = new DocumentCache();
        cache.Update(Uri, "1", version: 1);

        cache.Update(Uri, "2 + 3", version: 2);
        ExpreszoTextDocument? doc = cache.TryGet(Uri);

        await Assert.That(doc).IsNotNull();
        await Assert.That(doc!.Version).IsEqualTo(2);
        await Assert.That(doc.Text).IsEqualTo("2 + 3");
    }
}
