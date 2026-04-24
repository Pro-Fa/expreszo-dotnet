namespace Expreszo.LanguageServer.Tests;

public class BuiltinMetadataTests
{
    [Test]
    [Arguments("sum")]
    [Arguments("filter")]
    [Arguments("keys")]
    [Arguments("if")]
    [Arguments("isArray")]
    public async Task Known_functions_are_classified_as_functions(string name)
    {
        await Assert.That(BuiltinMetadata.IsBuiltinFunction(name)).IsTrue();
    }

    [Test]
    [Arguments("case")]
    [Arguments("when")]
    [Arguments("then")]
    [Arguments("else")]
    [Arguments("end")]
    [Arguments("true")]
    [Arguments("false")]
    public async Task Keywords_are_classified_as_keywords(string name)
    {
        await Assert.That(BuiltinMetadata.IsKeyword(name)).IsTrue();
    }

    [Test]
    public async Task Unknown_name_returns_false_for_both_predicates()
    {
        await Assert.That(BuiltinMetadata.IsBuiltinFunction("nopeNotAThing")).IsFalse();
        await Assert.That(BuiltinMetadata.IsKeyword("nopeNotAThing")).IsFalse();
    }

    [Test]
    public async Task Hover_markdown_contains_signature_and_summary()
    {
        bool found = BuiltinMetadata.TryGet("sum", out BuiltinEntry? entry);

        await Assert.That(found).IsTrue();
        await Assert.That(entry!.ToMarkdown()).Contains("sum(array)");
        await Assert.That(entry.ToMarkdown()).Contains("Sum");
    }
}
