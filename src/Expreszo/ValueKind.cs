namespace Expreszo;

/// <summary>
/// Coarse type tag matching the variants of <see cref="Value"/>, plus an
/// <see cref="Unknown"/> escape hatch for positions the static analyser
/// can't pin down without scope-aware flow analysis (free identifiers,
/// user-function return values, etc.). Used by the language-server tooling
/// for literal-driven type validation.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately flat: no union / intersection / generic shapes. A fuller
/// system is a non-goal of the current tooling pass; the enum is sized so
/// literal-constant propagation catches obvious runtime errors
/// (<c>"foo" + 1</c>, <c>x as "bogus"</c>) without producing noise on
/// idiomatic dynamic code.
/// </para>
/// <para>
/// <see cref="Unknown"/> means the analyser abstains; consumers must never
/// treat it as a concrete kind.
/// </para>
/// </remarks>
public enum ValueKind
{
    Unknown = 0,
    Number,
    String,
    Boolean,
    Null,
    Undefined,
    Array,
    Object,
    Function,
}
