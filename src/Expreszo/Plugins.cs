namespace Expreszo;

/// <summary>
/// A plugin contributes extra functions (and, if needed, operators) to a
/// <see cref="Parser"/>. Faithful port of the TypeScript <c>Plugin</c> shape:
/// an identity (<see cref="Name"/> / <see cref="Version"/>) plus a
/// <see cref="Register"/> hook the parser invokes once at construction.
/// </summary>
/// <remarks>
/// Registration is explicit (no reflection-based discovery), which keeps plugin
/// wiring AOT- and trim-safe. Pass plugins via
/// <see cref="ParserOptions.Plugins"/> or
/// <see cref="Parser.WithPlugins(System.Collections.Generic.IEnumerable{IExpreszoPlugin}, ParserOptions?)"/>.
/// </remarks>
public interface IExpreszoPlugin
{
    /// <summary>Identifier used in diagnostics (e.g. <c>"@pro-fa/expreszo-datetime"</c>).</summary>
    string Name { get; }

    /// <summary>Informational semantic version of the plugin.</summary>
    string Version { get; }

    /// <summary>
    /// Called once by the <see cref="Parser"/> constructor. Implementations add
    /// their functions/operators through <paramref name="registration"/>.
    /// </summary>
    void Register(IPluginRegistration registration);
}

/// <summary>
/// Narrow, public registration surface handed to <see cref="IExpreszoPlugin.Register"/>.
/// A thin façade over the internal operator table builder so the builder itself
/// stays internal while plugins only see what they need.
/// </summary>
public interface IPluginRegistration
{
    /// <summary>Registers a named function callable as <c>name(...)</c> in expressions.</summary>
    IPluginRegistration AddFunction(string name, ExprFunc impl, bool isAsync = false);

    /// <summary>Registers a prefix unary operator implementation.</summary>
    IPluginRegistration AddUnary(string op, ExprFunc impl);

    /// <summary>Registers a binary operator implementation.</summary>
    IPluginRegistration AddBinary(string op, ExprFunc impl);
}

/// <summary>
/// Default <see cref="IPluginRegistration"/> implementation that forwards to the
/// internal <see cref="OperatorTableBuilder"/> the parser is assembling. Must be
/// used before <see cref="OperatorTableBuilder.Build"/> snapshots the table.
/// </summary>
internal sealed class PluginRegistration(OperatorTableBuilder builder) : IPluginRegistration
{
    public IPluginRegistration AddFunction(string name, ExprFunc impl, bool isAsync = false)
    {
        builder.AddFunction(name, impl, isAsync);
        return this;
    }

    public IPluginRegistration AddUnary(string op, ExprFunc impl)
    {
        builder.AddUnary(op, impl);
        return this;
    }

    public IPluginRegistration AddBinary(string op, ExprFunc impl)
    {
        builder.AddBinary(op, impl);
        return this;
    }
}
