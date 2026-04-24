using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;

namespace Expreszo.LanguageServer;

/// <summary>
/// Name-keyed index of definitions and references reached from a
/// <see cref="Node"/>. Produced on demand per request; cheap (one AST walk)
/// and therefore not cached.
/// </summary>
/// <remarks>
/// Scope-insensitive on purpose. A name shadowed by a lambda parameter
/// still matches the outer name — resolving true lexical scope needs
/// the Tier-3 type-and-scope pass. For a single-file expression language
/// in practice this is rarely confusing.
/// </remarks>
internal sealed class SymbolIndex
{
    private SymbolIndex(
        ImmutableDictionary<string, ImmutableArray<TextSpan>> definitions,
        ImmutableDictionary<string, ImmutableArray<TextSpan>> references
    )
    {
        Definitions = definitions;
        References = references;
    }

    /// <summary>Declaration spans (selection range), keyed by name.</summary>
    public ImmutableDictionary<string, ImmutableArray<TextSpan>> Definitions { get; }

    /// <summary>Reference spans, keyed by name. Never contains declaration spans.</summary>
    public ImmutableDictionary<string, ImmutableArray<TextSpan>> References { get; }

    /// <summary>All spans (definitions + references) for a given name.</summary>
    public ImmutableArray<TextSpan> AllOccurrences(string name)
    {
        var builder = ImmutableArray.CreateBuilder<TextSpan>();
        if (Definitions.TryGetValue(name, out ImmutableArray<TextSpan> defs))
        {
            builder.AddRange(defs);
        }
        if (References.TryGetValue(name, out ImmutableArray<TextSpan> refs))
        {
            builder.AddRange(refs);
        }
        return builder.ToImmutable();
    }

    public static SymbolIndex Build(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var defs = new Dictionary<string, List<TextSpan>>(StringComparer.Ordinal);
        var refs = new Dictionary<string, List<TextSpan>>(StringComparer.Ordinal);

        Ast.Ast.Walk(root, node =>
        {
            switch (node)
            {
                case FunctionDef fd:
                    Add(defs, fd.Name, SelectionSpan(fd));
                    break;

                case Binary { Op: "=", Left: NameRef nr }:
                    // Assignments like `x = …` define `x` at the NameRef span.
                    Add(defs, nr.Name, nr.Span);
                    break;

                case Ident id:
                    Add(refs, id.Name, id.Span);
                    break;
            }
        });

        return new SymbolIndex(Freeze(defs), Freeze(refs));
    }

    /// <summary>
    /// The selection span for a <see cref="FunctionDef"/> — the name
    /// portion, derived from the node's full span by taking the first
    /// <c>FunctionDef.Name.Length</c> characters. The library doesn't
    /// expose a per-name span on the node itself.
    /// </summary>
    private static TextSpan SelectionSpan(FunctionDef fd) =>
        new(fd.Span.Start, fd.Span.Start + fd.Name.Length);

    private static void Add(Dictionary<string, List<TextSpan>> bucket, string name, TextSpan span)
    {
        if (!bucket.TryGetValue(name, out List<TextSpan>? list))
        {
            list = [];
            bucket[name] = list;
        }

        list.Add(span);
    }

    private static ImmutableDictionary<string, ImmutableArray<TextSpan>> Freeze(
        Dictionary<string, List<TextSpan>> source
    )
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<TextSpan>>(
            StringComparer.Ordinal
        );
        foreach ((string name, List<TextSpan> spans) in source)
        {
            builder[name] = [.. spans];
        }

        return builder.ToImmutable();
    }
}
