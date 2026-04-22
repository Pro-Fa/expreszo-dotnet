using System.Collections.Immutable;
using Expreszo.Ast;
using Expreszo.Errors;

// CA1859 asks us to tighten private method return types to concrete records.
// The parser caller always upcasts to Node, so the concrete type doesn't help
// at the call site - and tightening would force covariant-return dance or
// method duplication. The perf suggestion is not worth the clarity loss for
// a parser that runs once per expression.
#pragma warning disable CA1859

namespace Expreszo.Parsing;

/// <summary>
/// Pratt-style AST-emitting parser. Uses the immutable
/// <see cref="TokenCursor"/> for state - "save / restore" around speculative
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
    private static readonly Node EndStatementSentinel = new RawLit(
        EndStatementMarker,
        new TextSpan(-1, -1)
    );

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
        TokenCursor cursor = TokenCursor.From(config, expression ?? string.Empty);
        var parser = new PrattParser(config, cursor);
        Node node = parser.ParseExpression();
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
        Token t = Peek();
        if (t.Kind != kind)
        {
            return false;
        }

        return text is null || t.Text == text;
    }

    private bool CheckAny(TokenKind kind, params string[] texts)
    {
        Token t = Peek();
        if (t.Kind != kind)
        {
            return false;
        }

        foreach (string tx in texts)
        {
            if (t.Text == tx)
            {
                return true;
            }
        }
        return false;
    }

    private bool Accept(TokenKind kind, string? text = null)
    {
        Token t = Peek();
        if (t.Kind != kind)
        {
            return false;
        }

        if (text is not null && t.Text != text)
        {
            return false;
        }

        _cursor = _cursor.Advance();
        return true;
    }

    private Token Expect(TokenKind kind, string? text = null)
    {
        Token t = Peek();
        if (Accept(kind, text))
        {
            return t;
        }

        Error($"Expected {text ?? kind.ToString()}");
        return t; // unreachable
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Error(string message)
    {
        ErrorPosition coords = _cursor.GetCoordinates();
        Token t = Peek();
        throw new ParseException(
            message,
            new ErrorContext
            {
                Expression = _cursor.Expression,
                Position = coords,
                Token = t.Text,
            }
        );
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
        if (nodes.Count == 0)
        {
            return default;
        }

        return new TextSpan(nodes[0].Span.Start, nodes[^1].Span.End);
    }

    private static bool IsEndStatementSentinel(Node n) =>
        n is RawLit r && ReferenceEquals(r.Value, EndStatementMarker);

    // ---------- top-level expression ----------

    public Node ParseExpression()
    {
        EnterRecursion();
        var instr = new List<Node>();

        // Attempt a leading `;` (empty leading statement - rare but allowed).
        if (ParseUntilEndStatement(instr))
        {
            _depth--;
            return FinalizeStatements(instr);
        }

        Node first = ParseVariableAssignmentExpression();
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
        Node inner = BuildFromInstr(instr);
        return new Paren(inner, inner.Span);
    }

    private static Node BuildFromInstr(IReadOnlyList<Node> instr)
    {
        var statements = new List<Node>();
        var accum = new List<Node>();
        void Flush()
        {
            if (accum.Count == 0)
            {
                return;
            }

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
        foreach (Node n in instr)
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
        if (statements.Count == 0)
        {
            return new UndefinedLit(Node.NoSpan);
        }

        if (statements.Count == 1)
        {
            return statements[0];
        }

        return new Sequence([.. statements], UnionSpans(statements));
    }

    private bool ParseUntilEndStatement(List<Node> instr)
    {
        if (!Accept(TokenKind.Semicolon))
        {
            return false;
        }

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
        Token t = Peek();
        if (t.Kind == TokenKind.Eof)
        {
            return false;
        }

        if (t.Kind == TokenKind.Paren && t.Text == ")")
        {
            return false;
        }

        return true;
    }

    // ---------- assignment ----------

    private Node ParseVariableAssignmentExpression()
    {
        EnterRecursion();
        Node left = ParseConditionalExpression();

        while (Accept(TokenKind.Op, "="))
        {
            Node rhs = ParseVariableAssignmentExpression();
            var wrappedRhs = new Paren(rhs, rhs.Span);
            TextSpan sp = SpanBetween(left.Span.Start, rhs.Span.End);
            left = BuildAssignment(left, wrappedRhs, sp);
        }

        _depth--;
        return left;
    }

    private Node BuildAssignment(Node left, Paren wrappedRhs, TextSpan sp) =>
        left switch
        {
            Call call => BuildFunctionDefinition(call, wrappedRhs, sp),
            Ident id => new Binary("=", new NameRef(id.Name, id.Span), wrappedRhs, sp),
            Member member => BuildMemberAssignment(member, wrappedRhs, sp),
            _ => FailAssignment(),
        };

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private Node FailAssignment()
    {
        Error("Left side of assignment must be a variable name. Example: x = 5");
        return null!; // unreachable - Error throws
    }

    private Node BuildFunctionDefinition(Call call, Paren wrappedRhs, TextSpan sp)
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
        ImmutableArray<string> paramNames = ExtractParameterNames(call.Args);
        return new FunctionDef(calleeIdent.Name, paramNames, wrappedRhs, sp);
    }

    private ImmutableArray<string> ExtractParameterNames(ImmutableArray<Node> args)
    {
        ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(args.Length);
        foreach (Node arg in args)
        {
            if (arg is not Ident ident)
            {
                Error("Function parameters must be identifiers. Example: f(x, y) = x + y");
                return default; // unreachable
            }
            builder.Add(ident.Name);
        }
        return builder.ToImmutable();
    }

    // Member assignment: evaluate the object for side effects, then bind the
    // value as a top-level variable named after the property. The evaluator
    // diagnoses the non-standard shape.
    private static Node BuildMemberAssignment(Member member, Paren wrappedRhs, TextSpan sp) =>
        new Sequence(
            [
                member.Object,
                new Binary("=", new NameRef(member.Property, member.Span), wrappedRhs, sp),
            ],
            sp
        );

    // ---------- ternary ----------

    private Node ParseConditionalExpression()
    {
        EnterRecursion();
        Node expr = ParseOrExpression();

        while (Accept(TokenKind.Op, "?"))
        {
            Node trueBranch = ParseConditionalExpression();
            Expect(TokenKind.Op, ":");
            Node falseBranch = ParseConditionalExpression();
            TextSpan sp = SpanBetween(expr.Span.Start, falseBranch.Span.End);
            expr = new Ternary(
                "?",
                expr,
                new Paren(trueBranch, trueBranch.Span),
                new Paren(falseBranch, falseBranch.Span),
                sp
            );
        }

        _depth--;
        return expr;
    }

    // ---------- or / and (short-circuit with Paren RHS) ----------

    private Node ParseOrExpression()
    {
        Node left = ParseAndExpression();
        while (CheckAny(TokenKind.Op, "or", "||"))
        {
            string op = Peek().Text;
            _cursor = _cursor.Advance();
            Node right = ParseAndExpression();
            TextSpan sp = SpanBetween(left.Span.Start, right.Span.End);
            left = new Binary(op, left, new Paren(right, right.Span), sp);
        }
        return left;
    }

    private Node ParseAndExpression()
    {
        Node left = ParseComparison();
        while (CheckAny(TokenKind.Op, "and", "&&"))
        {
            string op = Peek().Text;
            _cursor = _cursor.Advance();
            Node right = ParseComparison();
            TextSpan sp = SpanBetween(left.Span.Start, right.Span.End);
            left = new Binary(op, left, new Paren(right, right.Span), sp);
        }
        return left;
    }

    // ---------- comparison / add-sub / term / coalesce ----------

    private static readonly HashSet<string> ComparisonOps = new(StringComparer.Ordinal)
    {
        "==",
        "!=",
        "<",
        "<=",
        ">=",
        ">",
        "in",
        "not in",
    };

    private Node ParseComparison()
    {
        Node left = ParseAddSub();
        while (Peek().Kind == TokenKind.Op && ComparisonOps.Contains(Peek().Text))
        {
            string op = Peek().Text;
            _cursor = _cursor.Advance();
            Node right = ParseAddSub();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> AddSubOps = new(StringComparer.Ordinal)
    {
        "+",
        "-",
        "|",
    };

    private Node ParseAddSub()
    {
        Node left = ParseTerm();
        while (true)
        {
            Token t = Peek();
            if (t.Kind != TokenKind.Op)
            {
                break;
            }

            if (!AddSubOps.Contains(t.Text))
            {
                break;
            }

            if (t.Text == "||")
            {
                break; // || handled by ParseOrExpression
            }

            _cursor = _cursor.Advance();
            Node right = ParseTerm();
            left = new Binary(t.Text, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> TermOps = new(StringComparer.Ordinal) { "*", "/", "%" };

    private Node ParseTerm()
    {
        Node left = ParseCoalesceExpression();
        while (Peek().Kind == TokenKind.Op && TermOps.Contains(Peek().Text))
        {
            string op = Peek().Text;
            _cursor = _cursor.Advance();
            Node right = ParseFactor();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private static readonly HashSet<string> CoalesceOps = new(StringComparer.Ordinal)
    {
        "??",
        "as",
    };

    private Node ParseCoalesceExpression()
    {
        Node left = ParseFactor();
        while (Peek().Kind == TokenKind.Op && CoalesceOps.Contains(Peek().Text))
        {
            string op = Peek().Text;
            _cursor = _cursor.Advance();
            Node right = ParseFactor();
            left = new Binary(op, left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    // ---------- prefix unary / factor ----------

    private Node ParseFactor()
    {
        EnterRecursion();
        TokenCursor saved = _cursor;
        Token t = Peek();

        if (IsPrefixOperatorToken(t))
        {
            int tStart = t.Index;
            _cursor = _cursor.Advance();
            string v = t.Text;
            if (v != "-" && v != "+")
            {
                Token next = Peek();
                if (next.Kind == TokenKind.Paren && next.Text == "(")
                {
                    // `sin(x)` - treat as function call, not prefix.
                    _cursor = saved;
                    _depth--;
                    return ParseExponential();
                }
                if (
                    next.Kind == TokenKind.Semicolon
                    || next.Kind == TokenKind.Comma
                    || next.Kind == TokenKind.Eof
                    || (next.Kind == TokenKind.Paren && next.Text == ")")
                )
                {
                    // Bare identifier - parse as atom.
                    _cursor = saved;
                    _depth--;
                    return ParseAtom();
                }
            }
            Node operand = ParseFactor();
            _depth--;
            return new Unary(v, operand, SpanBetween(tStart, operand.Span.End));
        }

        _depth--;
        return ParseExponential();
    }

    // ---------- exponential (right-associative ^) ----------

    private Node ParseExponential()
    {
        Node left = ParsePostfixExpression();
        while (Accept(TokenKind.Op, "^"))
        {
            Node right = ParseFactor();
            left = new Binary("^", left, right, SpanBetween(left.Span.Start, right.Span.End));
        }
        return left;
    }

    // ---------- postfix ! ----------

    private Node ParsePostfixExpression()
    {
        Node expr = ParseFunctionCall();
        while (Accept(TokenKind.Op, "!"))
        {
            int end = PrevEnd();
            expr = new Unary("!", expr, SpanBetween(expr.Span.Start, end));
        }
        return expr;
    }

    // ---------- function call ----------

    private Node ParseFunctionCall()
    {
        Token t = Peek();
        if (IsPrefixOperatorToken(t))
        {
            // `sin(x)` restored path: consume the prefix op and parse atom.
            int tStart = t.Index;
            _cursor = _cursor.Advance();
            Node operand = ParseAtom();
            return new Unary(t.Text, operand, SpanBetween(tStart, operand.Span.End));
        }

        Node expr = ParseMemberExpression();

        while (Accept(TokenKind.Paren, "("))
        {
            ImmutableArray<Node>.Builder argsBuilder = ImmutableArray.CreateBuilder<Node>();
            if (!Check(TokenKind.Paren, ")"))
            {
                argsBuilder.Add(ParseExpression());
                while (Accept(TokenKind.Comma))
                {
                    argsBuilder.Add(ParseExpression());
                }
            }
            Expect(TokenKind.Paren, ")");
            int end = PrevEnd();
            expr = new Call(expr, argsBuilder.ToImmutable(), SpanBetween(expr.Span.Start, end));
        }
        return expr;
    }

    // ---------- member access ----------

    private Node ParseMemberExpression()
    {
        Node expr = ParseAtom();
        while (true)
        {
            if (Accept(TokenKind.Op, "."))
            {
                if (!_config.AllowMemberAccess)
                {
                    throw new AccessException(
                        "Member access (dot notation) is not permitted. Enable it with: new Parser(new ParserOptions { AllowMemberAccess = true }).",
                        context: new ErrorContext { Expression = _cursor.Expression }
                    );
                }
                Token name = Expect(TokenKind.Name);
                int end = PrevEnd();
                expr = new Member(expr, name.Text, SpanBetween(expr.Span.Start, end));
            }
            else if (Accept(TokenKind.Bracket, "["))
            {
                if (!_config.IsOperatorEnabled("["))
                {
                    throw new AccessException(
                        "Array/bracket access is disabled.",
                        context: new ErrorContext { Expression = _cursor.Expression }
                    );
                }
                Node index = ParseExpression();
                Expect(TokenKind.Bracket, "]");
                int end = PrevEnd();
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
        Token t = Peek();

        // Names and prefix-op identifiers (with arrow-function lookahead).
        if (t.Kind == TokenKind.Name || IsPrefixOperatorToken(t))
        {
            int tStart = t.Index;
            int tEnd = PeekEnd();
            _cursor = _cursor.Advance();

            if (t.Text == "undefined")
            {
                return new UndefinedLit(SpanBetween(tStart, tEnd));
            }

            Token next = Peek();
            if (next.Kind == TokenKind.Op && next.Text == "=>")
            {
                return ParseArrowFunctionFromParameter(t.Text, tStart);
            }

            return new Ident(t.Text, SpanBetween(tStart, tEnd));
        }

        if (t.Kind == TokenKind.Number)
        {
            int start = t.Index;
            int end = PeekEnd();
            _cursor = _cursor.Advance();
            return new NumberLit(t.Number, SpanBetween(start, end));
        }
        if (t.Kind == TokenKind.String)
        {
            int start = t.Index;
            int end = PeekEnd();
            _cursor = _cursor.Advance();
            return new StringLit(t.Text, SpanBetween(start, end));
        }
        if (t.Kind == TokenKind.Const)
        {
            int start = t.Index;
            int end = PeekEnd();
            TextSpan sp = SpanBetween(start, end);
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
            int openStart = PeekStart();
            _cursor = _cursor.Advance();
            Node? arrow = TryParseArrowFunction(openStart);
            if (arrow is not null)
            {
                return arrow;
            }

            Node inner = ParseExpression();
            Expect(TokenKind.Paren, ")");
            return inner;
        }

        // Object literal.
        if (Check(TokenKind.Brace, "{"))
        {
            int openStart = PeekStart();
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
            int kwStart = PeekStart();
            _cursor = _cursor.Advance();
            return ParseKeywordExpression(t, kwStart);
        }

        Error($"Unexpected token: {t}");
        return null!; // unreachable
    }

    private Node ParseArrayLiteral()
    {
        int openStart = PeekStart();
        _cursor = _cursor.Advance();
        ImmutableArray<ArrayEntry>.Builder elements = ImmutableArray.CreateBuilder<ArrayEntry>();
        while (!Accept(TokenKind.Bracket, "]"))
        {
            elements.Add(ParseArrayEntry());
            while (Accept(TokenKind.Comma))
            {
                elements.Add(ParseArrayEntry());
            }
        }
        return new ArrayLit(elements.ToImmutable(), SpanBetween(openStart, PrevEnd()));
    }

    private ArrayEntry ParseArrayEntry()
    {
        if (Check(TokenKind.Op, "..."))
        {
            int spreadStart = PeekStart();
            _cursor = _cursor.Advance();
            Node arg = ParseConditionalExpression();
            return new ArraySpread(arg, SpanBetween(spreadStart, arg.Span.End));
        }

        Node node = ParseExpression();
        return new ArrayElement(node, node.Span);
    }

    // ---------- arrow functions ----------

    private Node ParseArrowFunctionFromParameter(string paramName, int startPos)
    {
        if (!_config.IsOperatorEnabled("=>"))
        {
            Error("Arrow function syntax is not permitted");
        }
        Expect(TokenKind.Op, "=>");
        Node body = ParseConditionalExpression();
        TextSpan sp = SpanBetween(startPos, body.Span.End);
        return new Lambda(ImmutableArray.Create(paramName), new Paren(body, body.Span), sp);
    }

    private Node? TryParseArrowFunction(int openStart)
    {
        TokenCursor saved = _cursor;

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
            Node body = ParseExpression();
            TextSpan sp = SpanBetween(openStart, body.Span.End);
            return new Lambda(ImmutableArray<string>.Empty, new Paren(body, body.Span), sp);
        }

        ImmutableArray<string>.Builder paramsBuilder = ImmutableArray.CreateBuilder<string>();
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

        Node fnBody = ParseConditionalExpression();
        return new Lambda(
            paramsBuilder.ToImmutable(),
            new Paren(fnBody, fnBody.Span),
            SpanBetween(openStart, fnBody.Span.End)
        );
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
        bool caseWithInput = !Check(TokenKind.Keyword);
        Node? subject = null;
        if (caseWithInput)
        {
            subject = ParseConditionalExpression();
        }

        ImmutableArray<CaseArm>.Builder arms = ImmutableArray.CreateBuilder<CaseArm>();
        Node? elseNode = null;

        while (Accept(TokenKind.Keyword, "when"))
        {
            Node when = ParseConditionalExpression();
            if (!Accept(TokenKind.Keyword, "then"))
            {
                Error("Expected 'then' after 'when' condition in case block");
            }
            Node then = ParseConditionalExpression();
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

        int endPos = PrevEnd();
        return new Case(subject, arms.ToImmutable(), elseNode, SpanBetween(startPos, endPos));
    }

    // ---------- object literal ----------

    private Node ParseObjectLiteral(int openStart)
    {
        ImmutableArray<ObjectEntry>.Builder properties =
            ImmutableArray.CreateBuilder<ObjectEntry>();

        if (Accept(TokenKind.Brace, "}"))
        {
            return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
        }

        while (true)
        {
            properties.Add(ParseObjectEntry());

            if (Accept(TokenKind.Brace, "}"))
            {
                return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
            }
            if (!Accept(TokenKind.Comma))
            {
                Error("Expected comma between object properties");
            }
            // Trailing comma: allow "{a: 1, }".
            if (Accept(TokenKind.Brace, "}"))
            {
                return new ObjectLit(properties.ToImmutable(), SpanBetween(openStart, PrevEnd()));
            }
        }
    }

    private ObjectEntry ParseObjectEntry()
    {
        if (Check(TokenKind.Op, "..."))
        {
            int spreadStart = PeekStart();
            _cursor = _cursor.Advance();
            Node arg = ParseConditionalExpression();
            return new ObjectSpread(arg, SpanBetween(spreadStart, arg.Span.End));
        }

        int entryStart = PeekStart();
        (string key, bool quoted) = ReadObjectKey();

        if (!Accept(TokenKind.Op, ":"))
        {
            Error($"Expected ':' after property name '{key}'");
        }

        Node value = ParseExpression();
        return new ObjectProperty(key, value, quoted, SpanBetween(entryStart, value.Span.End));
    }

    private (string Key, bool Quoted) ReadObjectKey()
    {
        Token token = Peek();
        if (Accept(TokenKind.Name))
        {
            return (token.Text, false);
        }
        if (Accept(TokenKind.String))
        {
            return (token.Text, true);
        }
        Error("Object property key must be an identifier or quoted string");
        return default; // unreachable
    }
}
