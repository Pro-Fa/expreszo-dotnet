using Expreszo.Errors;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expreszo.LanguageServer.Tests;

public class DiagnosticMapperTests
{
    [Test]
    public async Task Maps_span_to_zero_based_range()
    {
        var error = new ParseException(
            "bad",
            new ErrorContext
            {
                Span = new TextSpan(2, 5),
                Position = new ErrorPosition(1, 3),
            });

        Diagnostic[] diagnostics = [.. DiagnosticMapper.Map(error, new LineIndex("01234567"))];

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        Range r = diagnostics[0].Range;
        await Assert.That(r.Start.Line).IsEqualTo(0);
        await Assert.That(r.Start.Character).IsEqualTo(2);
        await Assert.That(r.End.Line).IsEqualTo(0);
        await Assert.That(r.End.Character).IsEqualTo(5);
    }

    [Test]
    public async Task Falls_back_to_position_when_span_is_absent()
    {
        var error = new ParseException(
            "bad",
            new ErrorContext { Position = new ErrorPosition(1, 3) });

        Diagnostic[] diagnostics = [.. DiagnosticMapper.Map(error, new LineIndex("01234"))];

        Range r = diagnostics[0].Range;
        await Assert.That(r.Start.Line).IsEqualTo(0);
        await Assert.That(r.Start.Character).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_errors_are_severity_error()
    {
        var error = new ParseException("bad", new ErrorContext { Span = new TextSpan(0, 1) });

        Diagnostic[] diagnostics = [.. DiagnosticMapper.Map(error, new LineIndex("x"))];

        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task Variable_errors_are_severity_warning()
    {
        var error = new VariableException("foo", new ErrorContext { Span = new TextSpan(0, 3) });

        Diagnostic[] diagnostics = [.. DiagnosticMapper.Map(error, new LineIndex("foo"))];

        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }
}
