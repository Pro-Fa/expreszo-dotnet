using Expreszo.Ast;

namespace Expreszo.LanguageServer.Tests;

public class TypeInferenceTests
{
    private static ValueKind KindOfRoot(string source)
    {
        Node root = new Parser().TryParse(source).Expression.Root;
        TypeInference inf = TypeInference.Run(root);
        return inf.KindOf(root);
    }

    [Test]
    [Arguments("1", ValueKind.Number)]
    [Arguments("\"hi\"", ValueKind.String)]
    [Arguments("true", ValueKind.Boolean)]
    [Arguments("null", ValueKind.Null)]
    [Arguments("undefined", ValueKind.Undefined)]
    [Arguments("[1, 2, 3]", ValueKind.Array)]
    [Arguments("{a: 1}", ValueKind.Object)]
    [Arguments("x => x + 1", ValueKind.Function)]
    public async Task Literals_and_constructors_map_to_the_matching_kind(string src, ValueKind expected)
    {
        await Assert.That(KindOfRoot(src)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("1 + 2", ValueKind.Number)]
    [Arguments("\"a\" | \"b\"", ValueKind.String)]
    [Arguments("[1] | [2]", ValueKind.Array)]
    [Arguments("1 == 2", ValueKind.Boolean)]
    [Arguments("1 < 2", ValueKind.Boolean)]
    [Arguments("1 in [1, 2]", ValueKind.Boolean)]
    [Arguments("not true", ValueKind.Boolean)]
    [Arguments("x as \"number\"", ValueKind.Number)]
    [Arguments("x as \"boolean\"", ValueKind.Boolean)]
    public async Task Operators_produce_expected_kinds(string src, ValueKind expected)
    {
        await Assert.That(KindOfRoot(src)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("sum([1, 2, 3])", ValueKind.Number)]
    [Arguments("isArray(xs)", ValueKind.Boolean)]
    [Arguments("isString(y)", ValueKind.Boolean)]
    [Arguments("keys(obj)", ValueKind.Array)]
    [Arguments("json(x)", ValueKind.String)]
    public async Task Builtin_calls_use_return_metadata(string src, ValueKind expected)
    {
        await Assert.That(KindOfRoot(src)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("x and y")]
    [Arguments("x or y")]
    [Arguments("x ?? y")]
    public async Task Short_circuit_operators_remain_unknown(string src)
    {
        // and/or/?? can return operand values under short-circuit, so kind
        // must not be narrowed to Boolean — would cause false positives.
        await Assert.That(KindOfRoot(src)).IsEqualTo(ValueKind.Unknown);
    }

    [Test]
    public async Task Ternary_with_matching_branches_uses_that_kind()
    {
        await Assert.That(KindOfRoot("x ? 1 : 2")).IsEqualTo(ValueKind.Number);
    }

    [Test]
    public async Task Ternary_with_mismatched_branches_is_unknown()
    {
        await Assert.That(KindOfRoot("x ? 1 : \"foo\"")).IsEqualTo(ValueKind.Unknown);
    }
}
