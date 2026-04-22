namespace Expreszo.Tests;

public class EvalContextTests
{
    [Test]
    public async Task Ctor_populates_all_fields()
    {
        var scope = new Scope();
        using var cts = new CancellationTokenSource();
        VariableResolver resolver = name => VariableResolveResult.NotResolved;

        var ctx = new EvalContext(scope, ThrowingErrorHandler.Instance, resolver, cts.Token);

        await Assert.That(ctx.Scope).IsSameReferenceAs(scope);
        await Assert.That(ctx.ErrorHandler).IsSameReferenceAs(ThrowingErrorHandler.Instance);
        await Assert.That(ctx.Resolver).IsSameReferenceAs(resolver);
        await Assert.That(ctx.CancellationToken).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task WithChildScope_pushes_a_new_scope_and_preserves_other_fields()
    {
        var parent = new Scope();
        parent.Assign("x", Value.Number.Of(1));

        using var cts = new CancellationTokenSource();
        VariableResolver resolver = _ => VariableResolveResult.NotResolved;

        var outer = new EvalContext(parent, ThrowingErrorHandler.Instance, resolver, cts.Token);
        var inner = outer.WithChildScope();

        await Assert.That(inner.Scope).IsNotSameReferenceAs(outer.Scope);
        inner.Scope.TryGet("x", out var v);
        await Assert.That(v).IsEqualTo((Value)Value.Number.Of(1));

        await Assert.That(inner.ErrorHandler).IsSameReferenceAs(outer.ErrorHandler);
        await Assert.That(inner.Resolver).IsSameReferenceAs(outer.Resolver);
        await Assert.That(inner.CancellationToken).IsEqualTo(outer.CancellationToken);
    }

    [Test]
    public async Task Default_CancellationToken_is_None()
    {
        var ctx = new EvalContext(new Scope(), ThrowingErrorHandler.Instance);
        await Assert.That(ctx.CancellationToken).IsEqualTo(CancellationToken.None);
    }
}
