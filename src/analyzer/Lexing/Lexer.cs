using System.Collections;
using System.Diagnostics;

namespace Surab.Analyzer;

internal static class Lexer
{
    public static IEnumerable<Token> Lex(string src)
    {
        return new LexerImpl(src);
    }

    public static IEnumerable<Token> LexSkipTrivia(string src) => Lex(src).SkipTrivia();
}

internal class LexerImpl : IEnumerable<Token>, IEnumerator<Token>
{
    struct Position
    {
        // UTF-16

        public int File { get; set; }
        public int Line { get; set; }
        public int Char { get; set; }
    }

    private static readonly Dictionary<string, TokenKind> KeywordsMap = new()
    {
        { "function" , TokenKind.Function},
        { "class" , TokenKind.Class},
        { "new" , TokenKind.New},
        { "this" , TokenKind.This},
        { "true" , TokenKind.True},
        { "false" , TokenKind.False},
        { "let" , TokenKind.Let},
        { "const" , TokenKind.Const},
        { "field" , TokenKind.Field},
        { "public" , TokenKind.Public},
        { "return" , TokenKind.Return },
    };

    private readonly ReadOnlyMemory<char> _src;
    private ReadOnlyMemory<char> _window;
    private Position _pos;
    private Position _posIterStart;
    private Token? _current;
    private char _prev;

    public LexerImpl(string src)
    {
        _src = src.AsMemory();
        Reset();
    }

    public Token Current => _current!;
    object IEnumerator.Current => Current;

    public IEnumerator<Token> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool MoveNext()
    {
        IterStart();

        if (IsAtEnd()) return false;

        var ch = Advance();

        var token = ch switch
        {
            '/' => P1() switch
            {
                '/' => ConsumeLineComment(),
                '*' => ConsumeBlockComment(),
                _ => MakeToken(TokenKind.Slash),
            },

            _ when Chars.IsWhitespace(ch) => ConsumeWhitespace(),

            '(' => MakeToken(TokenKind.LeftParen),
            ')' => MakeToken(TokenKind.RightParen),
            '{' => MakeToken(TokenKind.LeftBrace),
            '}' => MakeToken(TokenKind.RightBrace),
            ',' => MakeToken(TokenKind.Comma),
            '.' => MakeToken(TokenKind.Dot),
            ';' => MakeToken(TokenKind.Semicolon),
            ':' => MakeToken(TokenKind.Colon),
            '?' => MakeToken(TokenKind.Question),
            '-' => MakeToken(TokenKind.Minus),
            '+' => MakeToken(TokenKind.Plus),
            '*' => MakeToken(TokenKind.Asterisk),

            '=' => CreateTokenIfMatchesOrToken('=', TokenKind.EqualEqual, TokenKind.Equal),
            '!' => CreateTokenIfMatchesOrToken('=', TokenKind.BangEqual, TokenKind.Bang),
            '>' => CreateTokenIfMatchesOrToken('=', TokenKind.GreaterEqual, TokenKind.Greater),
            '<' => CreateTokenIfMatchesOrToken('=', TokenKind.LessEqual, TokenKind.Less),
            '&' => CreateTokenIfMatchesOrToken('&', TokenKind.ConditionalAnd, TokenKind.And),
            '|' => CreateTokenIfMatchesOrToken('|', TokenKind.ConditionalOr, TokenKind.InclusiveOr),
            '^' => CreateTokenIfMatchesOrToken('^', TokenKind.CaretCaret, TokenKind.ExclusiveOr),

            '\'' => ConsumeChar(),
            '"' => ConsumeString(),
            _ when char.IsAsciiDigit(ch) => ConsumeNumber(),
            _ when Chars.IsIdentifierStart(ch) => ConsumeKeywordOrIdentifier(),

            _ => MakeToken(TokenKind.Unknown),

        };

        _current = token;

        return true;
    }

    public void Reset()
    {
        _window = _src;
        _pos = new();
        _posIterStart = new();
        _current = null;
        _prev = Chars.EOF;
    }

    public void Dispose()
    {
    }

    private char Advance()
    {
        if (IsAtEnd())
        {
            return Chars.EOF;
        }

        var ch = _window.Span[0];
        _window = _window[1..];

        _pos.File++;
        _pos.Char++;

        if (ch == '\n')
        {
            _pos.Line++;
            _pos.Char = 0;
        }

        _prev = ch;

        return ch;
    }

    private char P1() => Peek(0);
    private char P2() => Peek(1);

    private char Peek(int n)
    {
        if (_window.Length > n)
        {
            return _window.Span[n];
        }

        return Chars.EOF;
    }

    private void EatWhile(Func<char, bool> predicate)
    {
        while (predicate(P1()) && !IsAtEnd())
        {
            Advance();
        }
    }

    /// <summary>
    /// Checks if next char matches `expected`, advances and returns 'true' if so.
    /// </summary>
    private bool AdvanceIfMatches(char expected)
    {
        if (P1() == expected)
        {
            Advance();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if next char matches `expected`, advances and makes a `matched` token, otherwise makes and returns the `or` token.
    /// </summary>
    private Token CreateTokenIfMatchesOrToken(char nextExpected, TokenKind matched, TokenKind or)
    {
        if (AdvanceIfMatches(nextExpected))
        {
            return MakeToken(matched);
        }
        else
        {
            return MakeToken(or);
        }
    }

    /// <summary>
    /// Used by Char and String parsers. Advances until we see the given terminal,
    /// or we reach the end or a new line leaving out the final semicolon (flawed token).
    /// Returns `true` if properly terminated.
    /// </summary>
    private bool AdvanceUntil(char terminal)
    {
        while (true)
        {
            switch (P1(), P2())
            {
                case (var ch1, _) when ch1 == terminal:
                    Advance();
                    return true;

                case (var ch1, _) when ch1 == Chars.EOF:
                    return false;

                case (';', var ch2) when Chars.IsNewlineStart(ch2) || ch2 == Chars.EOF:
                    return false;

                default:
                    Advance();
                    break;
            }
        }
    }

    private bool IsAtEnd() => _window.IsEmpty;

    private Token MakeToken(TokenKind tokenKind, object? data = null) => new(tokenKind, ConsumedTextSpan(), data);

    private Token ConsumeLineComment()
    {
        Debug.Assert(_prev == '/' && P1() == '/');

        EatWhile(ch => ch != '\n');
        return MakeToken(TokenKind.LineComment);
    }

    private Token ConsumeBlockComment()
    {
        Debug.Assert(_prev == '/' && P1() == '*');

        var depth = 1;
        while (true)
        {
            var ch = Advance();

            if (ch == '/' && P1() == '*')
            {
                Advance();
                depth++;
            }

            if (ch == '*' && P1() == '/')
            {
                Advance();
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }
        }

        return MakeToken(
            TokenKind.BlockComment,
            new BlockCommentTokenData { IsTerminated = depth == 0 });
    }

    private Token ConsumeWhitespace()
    {
        Debug.Assert(Chars.IsWhitespace(_prev));

        EatWhile(Chars.IsWhitespace);
        return MakeToken(TokenKind.Whitespace);
    }

    private Token ConsumeChar()
    {
        Debug.Assert(_prev == '\'');

        var terminated = AdvanceUntil('\'');
        return MakeToken(TokenKind.CharLiteral, new CharLiteralTokenData { IsTerminated = terminated });
    }

    private Token ConsumeString()
    {
        Debug.Assert(_prev == '"');

        var terminated = AdvanceUntil('"');
        return MakeToken(TokenKind.StringLiteral, new StringLiteralTokenData { IsTerminated = terminated });
    }

    private bool EatDecimalDigits()
    {
        var hasDigits = false;

        // So that we can break the loop.
        void Loop()
        {
            while (true)
            {
                switch (P1())
                {
                    case var ch when char.IsAsciiDigit(ch):
                        hasDigits = true;
                        Advance();
                        break;

                    case '_':
                        Advance();
                        break;

                    default:
                        return;
                }
            }
        }
        Loop();

        return hasDigits;
    }

    private Token ConsumeNumber()
    {
        Debug.Assert(char.IsAsciiDigit(_prev));

        EatDecimalDigits();

        if (P1() == '.' && char.IsAsciiDigit(P2()))
        {
            Advance();
            EatDecimalDigits();
        }

        return MakeToken(TokenKind.NumberLiteral);
    }

    private Token ConsumeKeywordOrIdentifier()
    {
        Debug.Assert(Chars.IsIdentifierStart(_prev));

        while (true)
        {
            switch (P1())
            {
                case var ch when Chars.IsIdentifierContinue(ch):
                    Advance();
                    break;

                default:
                    var lexeme = SliceConsumed();
                    // TODO: Optimize this when dictionaries start supporting spans.
                    if (!KeywordsMap.TryGetValue(lexeme.ToString(), out var tokenKind))
                    {
                        tokenKind = TokenKind.Identifier;
                    }
                    return MakeToken(tokenKind);
            }
        }
    }

    private ReadOnlySpan<char> SliceConsumed()
    {
        return _src.Span[ConsumedFileSpan().GetRange()];
    }

    private FileSpan ConsumedFileSpan() => new()
    {
        Start = _posIterStart.File,
        End = _pos.File,
    };

    private TextSpan ConsumedTextSpan() => new()
    {
        File = new()
        {
            Start = _posIterStart.File,
            End = _pos.File,
        },
        Line = new()
        {
            Start = new()
            {
                Line = _posIterStart.Line,
                Char = _posIterStart.Char,
            },
            End = new()
            {
                Line = _pos.Line,
                Char = _pos.Char,
            },
        },
    };

    private void IterStart()
    {
        _posIterStart = _pos;
    }
}

internal static class LexerExtensions
{
    public static IEnumerable<Token> SkipTrivia(this IEnumerable<Token> tokens) =>
        tokens.Where(t => !t.Kind.IsTrivia());
}
