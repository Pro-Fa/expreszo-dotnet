using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Expreszo.Json;

namespace Expreszo;

/// <summary>
/// Variable environment used during evaluation. Scopes form a chain: lambda
/// bodies and function-definition calls push a child scope whose parent is the
/// defining scope, giving closure semantics without copying.
/// </summary>
/// <remarks>
/// <para>
/// Assignment (<c>=</c>) writes to the current frame (matching the
/// <c>{ ...values, [param]: arg }</c> shallow-copy pattern used by the
/// TypeScript evaluator). The <see cref="Assign(string, Value)"/> method does
/// not walk up the chain looking for an existing binding — assignments inside
/// a lambda body stay local to that lambda's scope, exactly as in TS.
/// </para>
/// <para>
/// Not thread-safe. A single <c>Expression</c> can be evaluated concurrently
/// as long as each evaluation uses its own <see cref="Scope"/> instance.
/// </para>
/// </remarks>
public sealed class Scope
{
    private readonly Dictionary<string, Value> _locals;
    private readonly Scope? _parent;

    public Scope()
        : this(parent: null) { }

    private Scope(Scope? parent)
    {
        _parent = parent;
        _locals = new Dictionary<string, Value>(StringComparer.Ordinal);
    }

    /// <summary>Creates a child scope with this scope as its parent.</summary>
    public Scope CreateChild() => new(this);

    /// <summary>Reads a binding, walking up the parent chain if needed.</summary>
    public bool TryGet(string name, [MaybeNullWhen(false)] out Value value)
    {
        if (_locals.TryGetValue(name, out value))
        {
            return true;
        }
        if (_parent is not null)
        {
            return _parent.TryGet(name, out value);
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Writes (or overwrites) a binding in the current frame only. This is the
    /// assignment operator's implementation — it never modifies the parent.
    /// </summary>
    public void Assign(string name, Value value) => _locals[name] = value;

    /// <summary>Alias for <see cref="Assign"/> — named to match lambda-parameter binding at call sites.</summary>
    public void SetLocal(string name, Value value) => _locals[name] = value;

    /// <summary>Removes a binding from the current frame; does not touch the parent.</summary>
    public bool Remove(string name) => _locals.Remove(name);

    /// <summary>Every binding visible in the current frame (excludes parent frames).</summary>
    public IReadOnlyDictionary<string, Value> Locals => _locals;

    /// <summary>
    /// Builds a root scope from a <see cref="JsonDocument"/> whose root is an
    /// object. Any other root shape (array, scalar, or <c>null</c> document)
    /// returns an empty scope.
    /// </summary>
    public static Scope FromJsonDocument(JsonDocument? document)
    {
        var scope = new Scope();
        if (document is null)
        {
            return scope;
        }
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return scope;
        }
        foreach (var property in root.EnumerateObject())
        {
            scope.SetLocal(property.Name, JsonBridge.FromJson(property.Value));
        }
        return scope;
    }

    /// <summary>
    /// Serialises the current frame (not parents) to a compact JSON object.
    /// <see cref="Value.Function"/> entries are skipped (not representable);
    /// <see cref="Value.Undefined"/> entries are also skipped. Lets callers
    /// retrieve post-evaluation assignments after calling
    /// <c>Expression.Evaluate(doc)</c>.
    /// </summary>
    public string ToJsonString()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonBridge.WriteObject(writer, _locals);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
