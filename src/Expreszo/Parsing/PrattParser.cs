using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;

// CA1859 asks us to tighten private method return types to concrete records.
// The parser caller always upcasts to Node, so the concrete type doesn't help
// at the call site — and tightening would force covariant-return dance or
// method duplication. The perf suggestion is not worth the clarity loss for
// a parser that runs once per expression.
#pragma warning disable CA1859

namespace Expreszo.Parsing;

/// <summary>
/// Pratt-style AST-emitting parser. Uses the immutable
/// <see cref="TokenCursor"/> for state — "save / restore" around speculative
/// parses (arrow functions, prefix-op-vs-call disambiguation) is a local
/// variable assignment.
/// </summary>
/// <remarks>
/// The parser emits <see cref="Paren"/> nodes at specific positions so the
/// AST round-trips through the string formatter with precedence explicit and
/// short-circuit / lazy-RHS positions identifiable:
/// <list type="bullet">
///   <item>Assignment RHS: <c>x = Paren(value)</c></item>
///   <item>Function definition body: <c>f(a) = Paren(body)</c></item>
///   <item>Ternary branches: <c>c ? Paren(a) : Paren(b)</c></item>
///   <item>Short-circuit RHS: <c>x and Paren(y)</c>, <c>x or Paren(y)</c></item>
///   <item>Arrow function body: <c>params =&gt; Paren(body)</c></item>
///   <item>Semicolon-separated statements: <c>Paren(Sequence(...))</c></item>
/// </list>
/// </remarks>
internal sealed class PrattParser
{
    private const int MaxDepth = 256;

    // Sentinel RawLit used inside finalizeStatements to mark a `;` boundary
    // within the statement accumulator. Never leaks into the returned AST.
    private static readonly Value EndStatementMarker = new Value.String("__pratt_end_statement__");
    private static readonly Node EndStatementSentinel = new RawLit(EndStatementMarker, new TextSpan(-1, -1));

    private readonly ParserConfig _config;
    private TokenCursor _cursor;
    private int _depth;

    private PrattParser(ParserConfig config, TokenCursor cursor)
    {
        _config = config;
        _cursor = cursor;
    }

    public static Node Parse(ParserConfig config, string expression)
    {
        ArgumentNullException.ThrowIfNull(config);
        var cursor = TokenCursor.From(config, expression ?? string.Empty);
        var parser = new PrattParser(config, cursor);
        var node = parser.ParseExpression();
        if (!parser._cursor.AtEnd)
        {
            parser.Error("Expected EOF");
        }
        return node;
    }

    // ---------- cursor helpers ----------

    private Token Peek() => _cursor.Peek();
    private Token PeekAt(int offset) => _cursor.PeekAt(offset);
    private bool AtEnd => _cursor.AtEnd;
    private int PeekStart() => _cursor.Peek().Index;
    private int PeekEnd() => _cursor.PeekEnd();
    private int PrevEnd() => _cursor.PreviousEnd();

    private bool Check(TokenKind kind, string? text = null)
    {
        var t = Peek();
        if (t.Kind != kind) return false;
        return text is null || t.Text == text;
    }

    private bool CheckAny(TokenKind kind, params string[] texts)
    {
        var t = Peek();
        if (t.Kind != kind) return false;
        foreach (var tx in texts)
        {
            if (t.Text == tx) return true;
        }
        return false;
    }

    private bool Accept(TokenKind kind, string? text = null)
    {
        var t = Peek();
        if (t.Kind != kind) return false;
        if (text is not null && t.Text != text) return false;
        _cursor = _cursor.Advance();
        return true;
    }

    private Token Expect(TokenKind kind, string? text = null)
    {
        var t = Peek();
        if (Accept(kind, text)) return t;
        Error($"Expected {text ?? kind.ToString()}");
        return t; // unreachable
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Error(string message)
    {
        var coords = _cursor.GetCoordinates();
        var t = Peek();
        throw new ParseException(
            message,
            new ErrorContext
            {
                Expression = _cursor.Expression,
                Position = coords,
                Token = t.Text,
            });
    }

    private bool IsPrefixOperatorToken(Token t) =>
        t.Kind == TokenKind.Op && _config.IsPrefixOperator(t.Text);

    private void EnterRecursion()
    {
        if (++_depth > MaxDepth)
        {
            Error("Expression nesting exceeds maximum depth");
        }
    }

    private static TextSpan SpanBetween(int start, int end) => new(start, end);
    private static TextSpan UnionSpans(IReadOnlyList<Node> nodes)
    {
        if (nodes.Count == 0) return default;
        return new TextSpan(nodes[0].Span.Start, nodes[^1].Span.End);
    }

    private static bool IsEndStatementSentinel(Node n) =>
        n is RawLit r && ReferenceEquals(r.Value, EndStatementMarker);

    // ---------- top-level expression ----------

    public Node ParseExpression()
    {
        EnterRecursion();
        var instr = new List<Node>();

        // Attempt a leading `;` (empty leading statement — rare but allowed).
        if (ParseUntilEndStatement(instr))
        {
            _depth--;
            return FinalizeStatements(instr);
        }

        var first = ParseVariableAssignmentExpression();
        instr.Add(first);

        if (ParseUntilEndStatement(instr))
        {
            _depth--;
            return FinalizeStatements(instr);
        }

        _depth--;
        return first;
    }

    private static Node FinalizeStatements(IReadOnlyList<Node> instr)
    {
        var inner = BuildFromInstr(instr);
        return new Paren(inner, inner.Span);
    }

    private static Node BuildFromInstr(IReadOnlyList<Node> instr)
    {
        var statements = new List<Node>();
        var accum = new List<Node>();
        void Flush()
        {
            if (accum.Count == 0) return;
            if (accum.Count == 1)
            {
                statements.Add(accum[0]);
            }
            else
            {
                statements.Add(new Sequence([.. accum], UnionSpans(accum)));
            }
            accum.Clear();
        }
        foreach (var n in instr)
        {
            if (IsEndStatementSentinel(n))
            {
                Flush();
            }
            else
            {
                accum.Add(n);
            }
        }
        Flush();
        if (statements.Count == 0) return new UndefinedLit(Node.NoSpan);
        if (statements.Count == 1) return statements[0];
        return new Sequence([.. statements], UnionSpans(statements));
    }

    private bool ParseUntilEndStatement(List<Node> instr)
    {
        if (!Accept(TokenKind.Semicolon)) return false;
        if (ShouldAddEndStatement())
        {
            instr.Add(EndStatementSentinel);
        }
        if (!AtEnd)
        {
            instr.Add(ParseExpression());
        }
        if (ShouldAddEndStatement())
        {
            instr.Add(EndStatementSentinel);
        }
        return true;
    }

    private bool ShouldAddEndStatement()
    {
        var t = Peek();
        if (t.Kind == TokenKind.Eof) return false;
        if (t.Kind == TokenKind.Paren && t.Text == ")") return false;
        return true;
    }

    // ---------- assignment ----------

    private Node ParseVariableAssignmentExpression()
    {
        EnterRecursion();
        var left = ParseConditionalExpression();

        while (Accept(TokenKind.Op, "="))
        {
            var rhs = ParseVariableAssignmentExpression();
            var wrappedRhs = new Paren(rhs, rhs.Span);
            var sp = SpanBetween(left.Span.Start, rhs.Span.End);

            if (left is Call call)
            {
                if (!_config.IsOperatorEnabled("()="))
                {
                    Error("function definition is not permitted");
                }
                if (call.Callee is not Ident calleeIdent)
                {
                    Error("Function name must be an identifier in definition. Example: f(x) = x * 2");
                    return null!;
                }
                var paramsBuilder = ImmutableArray.CreateBuilder<string>(call.Args.Length);
                foreach (var arg in call.Args)
                {
                    if (arg is not Ident pIdent)
                    {
                        Error("Function parameters must be identifiers. Example: f(x, y) = x + y");
                        return null!;
                    }
                    paramsBuilder.Add(pIdent.Name);
                }
                left = new FunctionDef(calleeIdent.Name, paramsBuilder.ToImmutable(), wrappedRhs, sp);
            }
            else if (left is Ident idLeft)
            {
                left = new Binary("=", new NameRef(idLeft.Name, idLeft.Span), wrappedRhs, sp);
            }
            else if (left is Member memLeft)
            {
                // Member assignment: evaluate the object for side effects,
                // then bind the value as a top-level variable named after the
                // property. The evaluator diagnoses the non-standard shape.
                left = new Sequence(
                    [
                        memLeft.Object,
                        new Binary("=", new NameRef(memLeft.Property, memLeft.Span), wrappedRhs, sp),
                    ],
                    sp);
            }
            else
            {
                Error("Left side of assignment must be a variable name. Example: x = 5");
            }
        }

        _depth--;
        return left;
    }

    // ---------- ternary ----------

    private Node ParseConditionalExpression()
    {
        EnterRecursion();
        var expr = ParseOrExpression();

        while (Accept(TokenKind.Op, "?"))
        {
            var trueBranch = ParseConditionalExpression();
            Expect(TokenKind.Op, ":");
            var falseBranch = ParseConditionalExpression();
            var sp = SpanBetween(expr.Span.Start, falseBranch.Span.End);
            expr = new Ternary(
                "?",
                expr,
                new Paren(trueBranch, trueBranch.Span),
                new Paren(falseBranch, falseBranch.Span),
                sp);
        }

        _depth--;
        return expr;
    }

    // ---------- or / and (short-circuit with Paren RHS) ----------

    private Node ParseOrExpression()
    {
        var left = ParseAndExpression();
        while (CheckAny(TokenKind.Op, "or", "||"))
        {
            var op = Peek().Text;
            _cursor = _cursor.Advance();
            var right = ParseAndExpression();
            var sp = SpanBetween(left.Span.Start, right.Span.End);
            left = new Binary(op, left, new Paren(right, right.Span), sp);
        }
        return left;
    }

    private Node ParseAndExpression()
    {
        var left = ParseComparison();
        while (CheckAny(TokenKind.Op, "and", "&&"))
        {
            var op = Peek().Text;
            _cursor = _cursor.Advance();
            var right = ParseComparison();
            var sp = SpanBetween(left.Span.Start, right.Span.End);
            left = new Binary(op, left, new Paren(right, right.Span), sp);
        }
        return left;
    }

    // ---------- comparison / add-sub / term / coalesce ----------

    private static readonly HashSet<string> ComparisonOps =
        new(StringComparer.Ordinal) { "==", "!=", "<", "<=", ">=", ">", "in", "not in" };

    private Node ParseComparison()
    {
        var left = ParseAddSub();
        while (Peek().Kind == TokenKind.Op && ComparisonOps.Contains(Peek().Text))
        {
            var op = Peek().Text;
            _cursor = _cursor.Advance();
            var right = ParseAddSub();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> AddSubOps =
        new(StringComparer.Ordinal) { "+", "-", "|" };

    private Node ParseAddSub()
    {
        var left = ParseTerm();
        while (true)
        {
            var t = Peek();
            if (t.Kind != TokenKind.Op) break;
            if (!AddSubOps.Contains(t.Text)) break;
            if (t.Text == "||") break; // || handled by ParseOrExpression
            _cursor = _cursor.Advance();
            var right = ParseTerm();
            left = new Binary(t.Text, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> TermOps =
        new(StringComparer.Ordinal) { "*", "/", "%" };

    private Node ParseTerm()
    {
        var left = ParseCoalesceExpression();
        while (Peek().Kind == TokenKind.Op && TermOps.Contains(Peek().Text))
        {
            var op = Peek().Text;
            _cursor = _cursor.Advance();
            var right = ParseFactor();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> CoalesceOps =
        new(StringComparer.Ordinal) { "??", "as" };

    private Node ParseCoalesceExpression()
    {
        var left = ParseFactor();
        while (Peek().Kind == TokenKind.Op && CoalesceOps.Contains(Peek().Text))
        {
            var op = Peek().Text;
            _cursor = _cursor.Advance();
            var right = ParseFactor();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    // ---------- prefix unary / factor ----------

    private Node ParseFactor()
    {
        EnterRecursion();
        var saved = _cursor;
        var t = Peek();

        if (IsPrefixOperatorToken(t))
        {
            var tStart = t.Index;
            _cursor = _cursor.Advance();
            var v = t.Text;
            if (v != "-" && v != "+")
            {
                var next = Peek();
                if (next.Kind == TokenKind.Paren && next.Text == "(")
                {
                    // `sin(x)` — treat as function call, not prefix.
                    _cursor = saved;
                    _depth--;
                    return ParseExponential();
                }
                if (next.Kind == TokenKind.Semicolon
                    || next.Kind == TokenKind.Comma
                    || next.Kind == TokenKind.Eof
                    || (next.Kind == TokenKind.Paren && next.Text == ")"))
                {
                    // Bare identifier — parse as atom.
                    _cursor = saved;
                    _depth--;
                    return ParseAtom();
                }
            }
            var operand = ParseFactor();
            _depth--;
            return new Unary(v, operand, SpanBetween(tStart, operand.Span.End));
        }

        _depth--;
        return ParseExponential();
    }

    // ---------- exponential (right-associative ^) ----------

    private Node ParseExponential()
    {
        var left = ParsePostfixExpression();
        while (Accept(TokenKind.Op, "^"))
        {
            var right = ParseFactor();
            left = new Binary("^", left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    // ---------- postfix ! ----------

    private Node ParsePostfixExpression()
    {
        var expr = ParseFunctionCall();
        while (Accept(TokenKind.Op, "!"))
        {
            var end = PrevEnd();
            expr = new Unary("!", expr, SpanBetween(expr.Span.Start, end));
        }
        return expr;
    }

    // ---------- function call ----------

    private Node ParseFunctionCall()
    {
        var t = Peek();
        if (IsPrefixOperatorToken(t))
        {
            // `sin(x)` restored path: consume the prefix op and parse atom.
            var tStart = t.Index;
            _cursor = _cursor.Advance();
            var operand = ParseAtom();
            return new Unary(t.Text, operand, SpanBetween(tStart, operand.Span.End));
        }

        var expr = ParseMemberExpression();

        while (Accept(TokenKind.Paren, "("))
        {
            var argsBuilder = ImmutableArray.CreateBuilder<Node>();
            if (!Check(TokenKind.Paren, ")"))
            {
                argsBuilder.Add(ParseExpression());
                while (Accept(TokenKind.Comma))
                {
                    argsBuilder.Add(ParseExpression());
                }
            }
            Expect(TokenKind.Paren, ")");
            var end = PrevEnd();
            expr = new Call(expr, argsBuilder.ToImmutable(), SpanBetween(expr.Span.Start, end));
        }
        return expr;
    }

    // ---------- member access ----------

    private Node ParseMemberExpression()
    {
        var expr = ParseAtom();
        while (true)
        {
            if (Accept(TokenKind.Op, "."))
            {
                if (!_config.AllowMemberAccess)
                {
                    throw new AccessException(
                        "Member access (dot notation) is not permitted. Enable it with: new Parser(new ParserOptions { AllowMemberAccess = true }).",
                        context: new ErrorContext { Expression = _cursor.Expression });
                }
                var name = Expect(TokenKind.Name);
                var end = PrevEnd();
                expr = new Member(expr, name.Text, SpanBetween(expr.Span.Start, end));
            }
            else if (Accept(TokenKind.Bracket, "["))
            {
                if (!_config.IsOperatorEnabled("["))
                {
                    throw new AccessException(
                        "Array/bracket access is disabled.",
                        context: new ErrorContext { Expression = _cursor.Expression });
                }
                var index = ParseExpression();
                Expect(TokenKind.Bracket, "]");
                var end = PrevEnd();
                expr = new Binary("[", expr, index, SpanBetween(expr.Span.Start, end));
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    // ---------- atom ----------

    private Node ParseAtom()
    {
        var t = Peek();

        // Names and prefix-op identifiers (with arrow-function lookahead).
        if (t.Kind == TokenKind.Name || IsPrefixOperatorToken(t))
        {
            var tStart = t.Index;
            var tEnd = PeekEnd();
            _cursor = _cursor.Advance();

            if (t.Text == "undefined")
            {
                return new UndefinedLit(SpanBetween(tStart, tEnd));
            }

            var next = Peek();
            if (next.Kind == TokenKind.Op && next.Text == "=>")
            {
                return ParseArrowFunctionFromParameter(t.Text, tStart);
            }

            return new Ident(t.Text, SpanBetween(tStart, tEnd));
        }

        if (t.Kind == TokenKind.Number)
        {
            var start = t.Index;
            var end = PeekEnd();
            _cursor = _cursor.Advance();
            return new NumberLit(t.Number, SpanBetween(start, end));
        }
        if (t.Kind == TokenKind.String)
        {
            var start = t.Index;
            var end = PeekEnd();
            _cursor = _cursor.Advance();
            return new StringLit(t.Text, SpanBetween(start, end));
        }
        if (t.Kind == TokenKind.Const)
        {
            var start = t.Index;
            var end = PeekEnd();
            var sp = SpanBetween(start, end);
            _cursor = _cursor.Advance();
            return t.Const switch
            {
                Value.Boolean b => new BoolLit(b.V, sp),
                Value.Null => new NullLit(sp),
                Value.Undefined => new UndefinedLit(sp),
                Value.Number n => new NumberLit(n.V, sp),
                Value.String s => new StringLit(s.V, sp),
                _ => new RawLit(t.Const ?? Value.Undefined.Instance, sp),
            };
        }

        // Parenthesized expression or multi-param arrow function.
        if (Check(TokenKind.Paren, "("))
        {
            var openStart = PeekStart();
            _cursor = _cursor.Advance();
            var arrow = TryParseArrowFunction(openStart);
            if (arrow is not null) return arrow;

            var inner = ParseExpression();
            Expect(TokenKind.Paren, ")");
            return inner;
        }

        // Object literal.
        if (Check(TokenKind.Brace, "{"))
        {
            var openStart = PeekStart();
            _cursor = _cursor.Advance();
            return ParseObjectLiteral(openStart);
        }

        // Array literal.
        if (Check(TokenKind.Bracket, "["))
        {
            return ParseArrayLiteral();
        }

        // Keyword expression (case/when).
        if (Check(TokenKind.Keyword))
        {
            var kwStart = PeekStart();
            _cursor = _cursor.Advance();
            return ParseKeywordExpression(t, kwStart);
        }

        Error($"Unexpected token: {t}");
        return null!; // unreachable
    }

    private Node ParseArrayLiteral()
    {
        var openStart = PeekStart();
        _cursor = _cursor.Advance();
        var elements = ImmutableArray.CreateBuilder<ArrayEntry>();
        while (!Accept(TokenKind.Bracket, "]"))
        {
            if (Check(TokenKind.Op, "..."))
            {
                var spreadStart = PeekStart();
                _cursor = _cursor.Advance();
                var arg = ParseConditionalExpression();
                elements.Add(new ArraySpread(arg, SpanBetween(spreadStart, arg.Span.End)));
            }
            else
            {
                var n = ParseExpression();
                elements.Add(new ArrayElement(n, n.Span));
            }
            while (Accept(TokenKind.Comma))
            {
                if (Check(TokenKind.Op, "..."))
                {
                    var spreadStart = PeekStart();
                    _cursor = _cursor.Advance();
                    var arg = ParseConditionalExpression();
                    elements.Add(new ArraySpread(arg, SpanBetween(spreadStart, arg.Span.End)));
                }
                else
                {
                    var n = ParseExpression();
                    elements.Add(new ArrayElement(n, n.Span));
                }
            }
        }
        var closeEnd = PrevEnd();
        return new ArrayLit(elements.ToImmutable(), SpanBetween(openStart, closeEnd));
    }

    // ---------- arrow functions ----------

    private Node ParseArrowFunctionFromParameter(string paramName, int startPos)
    {
        if (!_config.IsOperatorEnabled("=>"))
        {
            Error("Arrow function syntax is not permitted");
        }
        Expect(TokenKind.Op, "=>");
        var body = ParseConditionalExpression();
        var sp = SpanBetween(startPos, body.Span.End);
        return new Lambda(ImmutableArray.Create(paramName), new Paren(body, body.Span), sp);
    }

    private Node? TryParseArrowFunction(int openStart)
    {
        var saved = _cursor;

        // Empty parameter list: () => body
        if (Accept(TokenKind.Paren, ")"))
        {
            if (!Accept(TokenKind.Op, "=>"))
            {
                _cursor = saved;
                return null;
            }
            if (!_config.IsOperatorEnabled("=>"))
            {
                _cursor = saved;
                return null;
            }
            var body = ParseExpression();
            var sp = SpanBetween(openStart, body.Span.End);
            return new Lambda(ImmutableArray<string>.Empty, new Paren(body, body.Span), sp);
        }

        var paramsBuilder = ImmutableArray.CreateBuilder<string>();
        if (!Check(TokenKind.Name))
        {
            _cursor = saved;
            return null;
        }
        paramsBuilder.Add(Peek().Text);
        _cursor = _cursor.Advance();

        while (Accept(TokenKind.Comma))
        {
            if (!Check(TokenKind.Name))
            {
                _cursor = saved;
                return null;
            }
            paramsBuilder.Add(Peek().Text);
            _cursor = _cursor.Advance();
        }

        if (!Accept(TokenKind.Paren, ")"))
        {
            _cursor = saved;
            return null;
        }
        if (!Accept(TokenKind.Op, "=>"))
        {
            _cursor = saved;
            return null;
        }
        if (!_config.IsOperatorEnabled("=>"))
        {
            Error("Arrow function syntax is not permitted");
        }

        var fnBody = ParseConditionalExpression();
        return new Lambda(
            paramsBuilder.ToImmutable(),
            new Paren(fnBody, fnBody.Span),
            SpanBetween(openStart, fnBody.Span.End));
    }

    // ---------- case / when / then / else / end ----------

    private Node ParseKeywordExpression(Token keyword, int startPos)
    {
        if (keyword.Text == "case")
        {
            return ParseCaseWhen(startPos);
        }
        Error($"unexpected keyword: {keyword.Text}");
        return null!;
    }

    private Node ParseCaseWhen(int startPos)
    {
        var caseWithInput = !Check(TokenKind.Keyword);
        Node? subject = null;
        if (caseWithInput)
        {
            subject = ParseConditionalExpression();
        }

        var arms = ImmutableArray.CreateBuilder<CaseArm>();
        Node? elseNode = null;

        while (Accept(TokenKind.Keyword, "when"))
        {
            var when = ParseConditionalExpression();
            if (!Accept(TokenKind.Keyword, "then"))
            {
                Error("Expected 'then' after 'when' condition in case block");
            }
            var then = ParseConditionalExpression();
            arms.Add(new CaseArm(when, then));
        }

        if (Accept(TokenKind.Keyword, "else"))
        {
            elseNode = ParseConditionalExpression();
        }

        if (!Accept(TokenKind.Keyword, "end"))
        {
            Error("Case block must be closed with 'end'");
        }

        var endPos = PrevEnd();
        return new Case(subject, arms.ToImmutable(), elseNode, SpanBetween(startPos, endPos));
    }

    // ---------- object literal ----------

    private Node ParseObjectLiteral(int openStart)
    {
        var properties = ImmutableArray.CreateBuilder<ObjectEntry>();
        if (Accept(TokenKind.Brace, "}"))
        {
            return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
        }

        var first = true;
        while (true)
        {
            if (!first)
            {
                if (!Accept(TokenKind.Comma))
                {
                    Error("Expected comma between object properties");
                }
                if (Accept(TokenKind.Brace, "}"))
                {
                    return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
                }
            }
            first = false;

            if (Check(TokenKind.Op, "..."))
            {
                var spreadStart = PeekStart();
                _cursor = _cursor.Advance();
                var arg = ParseConditionalExpression();
                properties.Add(new ObjectSpread(arg, SpanBetween(spreadStart, arg.Span.End)));
                if (Accept(TokenKind.Brace, "}"))
                {
                    return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
                }
                continue;
            }

            var nameToken = Peek();
            string key;
            var quoted = false;
            var entryStart = PeekStart();

            if (Accept(TokenKind.Name))
            {
                key = nameToken.Text;
            }
            else if (Accept(TokenKind.String))
            {
                key = nameToken.Text;
                quoted = true;
            }
            else
            {
                Error("Object property key must be an identifier or quoted string");
                return null!;
            }

            if (!Accept(TokenKind.Op, ":"))
            {
                Error($"Expected ':' after property name '{key}'");
            }

            var value = ParseExpression();
            properties.Add(new ObjectProperty(key, value, quoted, SpanBetween(entryStart, value.Span.End)));

            if (Accept(TokenKind.Brace, "}"))
            {
                return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
            }
        }
    }
}
