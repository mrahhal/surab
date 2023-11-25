namespace Surab.Analysis;

public class LexerTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("// Comment")]
    [InlineData("// Comment \t\r\n")]
    public void EmptyWhitespaceComments(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        Assert.Empty(tokens);
    }

    [Theory]
    [InlineData(" \t\r\n")]
    public void Whitespace(string src)
    {
        var tokens = Lexer.Lex(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.Whitespace);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12.0")]
    [InlineData("12.1")]
    [InlineData("0.01")]
    public void Number(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.NumberLiteral);
    }

    [Fact]
    public void Bool()
    {
        var tokens = Lexer.LexSkipTrivia("true.").ToList();
        tokens.Should().HaveCount(2);
        tokens[0].Kind.Should().Be(TokenKind.True);
        tokens[1].Kind.Should().Be(TokenKind.Dot);
    }

    [Theory]
    [InlineData("'a'", true)]
    [InlineData("'ab'", true)] // Invalid
    [InlineData("''", true)] // Invalid
    [InlineData("'", false)] // Invalid
    [InlineData("'ab", false)] // Invalid
    public void Char(string src, bool expectedIsTerminated)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.CharLiteral);
        tokens[0].Data.Should().BeOfType<CharLiteralTokenData>().Which.IsTerminated.Should().Be(expectedIsTerminated);
    }

    [Theory]
    [InlineData("'ab;")] // Invalid
    public void CharSemicolon(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(2);
        tokens[0].Kind.Should().Be(TokenKind.CharLiteral);
        tokens[0].Data.Should().BeOfType<CharLiteralTokenData>().Which.IsTerminated.Should().BeFalse();
        tokens[1].Kind.Should().Be(TokenKind.Semicolon);
    }

    [Theory]
    [InlineData("\"a\"", true)]
    [InlineData("\"ab\t😅\"", true)]
    [InlineData("\"a", false)] // Invalid
    public void String(string src, bool expectedIsTerminated)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.StringLiteral);
        tokens[0].Data.Should().BeOfType<StringLiteralTokenData>().Which.IsTerminated.Should().Be(expectedIsTerminated);
    }

    [Theory]
    [InlineData("\"a\n")]
    public void StringInvalidNewline(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.StringLiteral);
        tokens[0].Data.Should().BeOfType<StringLiteralTokenData>().Which.IsTerminated.Should().BeFalse();
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("_foo")]
    [InlineData("foo1")]
    [InlineData("_foo12")]
    [InlineData("trueFoo")]
    public void Identifier(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.Identifier);
    }

    [Theory]
    [InlineData("foo№bar")]
    public void IdentifierUnknownIdentifier(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(3);
        tokens[0].Kind.Should().Be(TokenKind.Identifier);
        tokens[1].Kind.Should().Be(TokenKind.Unknown);
        tokens[2].Kind.Should().Be(TokenKind.Identifier);
    }

    [Theory]
    [InlineData("@foo")]
    [InlineData("@_foo")]
    [InlineData("@foo1")]
    [InlineData("@_foo12")]
    [InlineData("@trueFoo")]
    public void IntrinsicIdentifier(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.IntrinsicIdentifier);
    }

    [Theory]
    [InlineData("@+foo")]
    public void AtTokenIdentifier(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(3);
        tokens[0].Kind.Should().Be(TokenKind.At);
        tokens[1].Kind.Should().Be(TokenKind.Plus);
        tokens[2].Kind.Should().Be(TokenKind.Identifier);
    }

    [Theory]
    [InlineData("№")]
    public void Unknown(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.Unknown);
    }

    [Theory]
    [InlineData("12.")]
    [InlineData("0.")]
    public void NumberDot(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src).ToList();
        tokens.Should().HaveCount(2);
        tokens[0].Kind.Should().Be(TokenKind.NumberLiteral);
        tokens[1].Kind.Should().Be(TokenKind.Dot);
    }

    [Fact]
    public void Span()
    {
        var tokens = Lexer.LexSkipTrivia("name\n.").ToList();
        tokens.Should().HaveCount(2);

        var token = tokens[0];
        token.Kind.Should().Be(TokenKind.Identifier);
        token.Span.GetRange().Should().Be(0..4);
        token.Span.Line.Start.Line.Should().Be(0);
        token.Span.Line.Start.Char.Should().Be(0);
        token.Span.Line.End.Line.Should().Be(0);
        token.Span.Line.End.Char.Should().Be(4);

        token = tokens[1];
        token.Kind.Should().Be(TokenKind.Dot);
        token.Span.GetRange().Should().Be(5..6);
        token.Span.Line.Start.Line.Should().Be(1);
        token.Span.Line.Start.Char.Should().Be(0);
        token.Span.Line.End.Line.Should().Be(1);
        token.Span.Line.End.Char.Should().Be(1);
    }
}
