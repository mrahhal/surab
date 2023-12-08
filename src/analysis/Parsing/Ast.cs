using System.Diagnostics;

namespace Surab.Analysis;

public class SyntaxTree(SourceFile sourceFile, SyntaxNode root, DiagnosticBag diagnostics)
{
    public SourceFile SourceFile { get; } = sourceFile;

    public SyntaxNode Root { get; } = root;

    public DiagnosticBag Diagnostics { get; } = diagnostics;
}

public abstract class SyntaxEntity : ISyntaxTreeVisitable
{
    public SyntaxNode? Parent { get; private set; }

    public TextSpan Span { get; init; }

    public SyntaxTree Tree { get; internal set; } = null!;

    internal void SetParent(SyntaxNode? parent)
    {
        Parent = parent;
    }

    public abstract void Accept(SyntaxTreeVisitor visitor);
}

public enum SyntaxTokenKind
{
    Simple,
    UnaryOperator,
    BinaryOperator,
    Identifier,
    Literal,
    Skipped,
}

public abstract class SyntaxToken(SyntaxTokenKind kind) : SyntaxEntity
{
    public SyntaxTokenKind Kind { get; } = kind;
}

public enum SimpleTokenKind
{
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Dot,
    Semicolon,
    Colon,
    Question,
    Equal,
    At,

    Fn,
    Struct,
    Class,
    Alias,
    New,
    This,
    True,
    False,
    Var,
    Const,
    Field,
    Extern,
    Public,
    Return,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{SimpleTokenKind}] {ToString()}")]
public sealed class SimpleToken : SyntaxToken
{
    public SimpleToken() : base(SyntaxTokenKind.Simple)
    {
    }

    public SimpleTokenKind SimpleTokenKind { get; init; }

    public bool IsMissing { get; init; }

    public static SimpleToken Missing(TokenKind tokenKind, TextSpan span) =>
        From(tokenKind, span, isMissing: true);

    public static SimpleToken From(Token token, bool isMissing = false) =>
        From(token.Kind, token.Span, isMissing);

    public static SimpleToken From(
        TokenKind tokenKind,
        TextSpan span,
        bool isMissing = false) => new()
        {
            Span = span,
            IsMissing = isMissing,
            SimpleTokenKind = tokenKind switch
            {
                TokenKind.LeftParen => SimpleTokenKind.LeftParen,
                TokenKind.RightParen => SimpleTokenKind.RightParen,
                TokenKind.LeftBrace => SimpleTokenKind.LeftBrace,
                TokenKind.RightBrace => SimpleTokenKind.RightBrace,
                TokenKind.Comma => SimpleTokenKind.Comma,
                TokenKind.Dot => SimpleTokenKind.Dot,
                TokenKind.Semicolon => SimpleTokenKind.Semicolon,
                TokenKind.Colon => SimpleTokenKind.Colon,
                TokenKind.Question => SimpleTokenKind.Question,
                TokenKind.Equal => SimpleTokenKind.Equal,
                TokenKind.At => SimpleTokenKind.At,
                TokenKind.AtBeforeIdentifier => SimpleTokenKind.At,

                TokenKind.Fn => SimpleTokenKind.Fn,
                TokenKind.Struct => SimpleTokenKind.Struct,
                TokenKind.Class => SimpleTokenKind.Class,
                TokenKind.Alias => SimpleTokenKind.Alias,
                TokenKind.New => SimpleTokenKind.New,
                TokenKind.This => SimpleTokenKind.This,
                TokenKind.True => SimpleTokenKind.True,
                TokenKind.False => SimpleTokenKind.False,
                TokenKind.Var => SimpleTokenKind.Var,
                TokenKind.Const => SimpleTokenKind.Const,
                TokenKind.Return => SimpleTokenKind.Return,
                _ => throw new Exception($"TokenKind {tokenKind} not supported."),
            },
        };

    public override string ToString() => SimpleTokenKind.ToString();

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitSimpleToken(this);
    }
}

public enum UnaryOperatorTokenKind
{
    Pos, // Usually redundant but must be represented in the AST.
    Neg,
    Not,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{UnaryOperatorTokenKind}] {ToString()}")]
public sealed class UnaryOperatorToken : SyntaxToken
{
    public UnaryOperatorToken() : base(SyntaxTokenKind.UnaryOperator)
    {
    }

    public UnaryOperatorTokenKind UnaryOperatorTokenKind { get; init; }

    public static UnaryOperatorToken From(
        Token token) => new()
        {
            Span = token.Span,
            UnaryOperatorTokenKind = token.Kind switch
            {
                TokenKind.Plus => UnaryOperatorTokenKind.Pos,
                TokenKind.Minus => UnaryOperatorTokenKind.Neg,
                TokenKind.Bang => UnaryOperatorTokenKind.Not,
                _ => throw new Exception($"TokenKind {token.Kind} not supported."),
            },
        };

    public override string ToString() => UnaryOperatorTokenKind switch
    {
        UnaryOperatorTokenKind.Pos => "+",
        UnaryOperatorTokenKind.Neg => "-",
        UnaryOperatorTokenKind.Not => "!",
        _ => throw new NotImplementedException(),
    };

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitUnaryOperatorToken(this);
    }
}

public enum BinaryOperatorTokenKind
{
    Add,
    Sub,
    Mul,
    Div,
    Pow,
    And,
    Or,
    BitAnd,
    BitOr,
    BitXor,
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{BinaryOperatorTokenKind}] {ToString()}")]
public sealed class BinaryOperatorToken : SyntaxToken
{
    public BinaryOperatorToken() : base(SyntaxTokenKind.BinaryOperator)
    {
    }

    public BinaryOperatorTokenKind BinaryOperatorTokenKind { get; init; }

    public static BinaryOperatorToken From(Token token) => new()
    {
        Span = token.Span,
        BinaryOperatorTokenKind = token.Kind switch
        {
            TokenKind.Plus => BinaryOperatorTokenKind.Add,
            TokenKind.Minus => BinaryOperatorTokenKind.Sub,
            TokenKind.Asterisk => BinaryOperatorTokenKind.Mul,
            TokenKind.Slash => BinaryOperatorTokenKind.Div,
            TokenKind.CaretCaret => BinaryOperatorTokenKind.Pow,
            TokenKind.ConditionalAnd => BinaryOperatorTokenKind.And,
            TokenKind.ConditionalOr => BinaryOperatorTokenKind.Or,
            TokenKind.And => BinaryOperatorTokenKind.BitAnd,
            TokenKind.InclusiveOr => BinaryOperatorTokenKind.BitOr,
            TokenKind.ExclusiveOr => BinaryOperatorTokenKind.BitXor,
            TokenKind.EqualEqual => BinaryOperatorTokenKind.Eq,
            TokenKind.BangEqual => BinaryOperatorTokenKind.Ne,
            TokenKind.Less => BinaryOperatorTokenKind.Lt,
            TokenKind.LessEqual => BinaryOperatorTokenKind.Le,
            TokenKind.Greater => BinaryOperatorTokenKind.Gt,
            TokenKind.GreaterEqual => BinaryOperatorTokenKind.Ge,
            _ => throw new Exception($"TokenKind {token.Kind} not supported."),
        },
    };

    public override string ToString() => BinaryOperatorTokenKind switch
    {
        BinaryOperatorTokenKind.Add => "+",
        BinaryOperatorTokenKind.Sub => "-",
        BinaryOperatorTokenKind.Mul => "*",
        BinaryOperatorTokenKind.Div => "/",
        BinaryOperatorTokenKind.Pow => "^^",
        BinaryOperatorTokenKind.And => "&&",
        BinaryOperatorTokenKind.Or => "||",
        BinaryOperatorTokenKind.BitAnd => "&",
        BinaryOperatorTokenKind.BitOr => "|",
        BinaryOperatorTokenKind.BitXor => "^",
        BinaryOperatorTokenKind.Eq => "==",
        BinaryOperatorTokenKind.Ne => "!=",
        BinaryOperatorTokenKind.Lt => "<",
        BinaryOperatorTokenKind.Le => "<=",
        BinaryOperatorTokenKind.Gt => ">",
        BinaryOperatorTokenKind.Ge => ">=",
        _ => throw new NotImplementedException(),
    };

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitBinaryOperatorToken(this);
    }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public sealed class IdentifierToken : SyntaxToken
{
    public IdentifierToken(TextSpan span, string value)
        : base(SyntaxTokenKind.Identifier)
    {
        Span = span;
        Value = value;
    }

    public string Value { get; init; }

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitIdentifierToken(this);
    }

    public override string ToString() => Value;
}

public enum LiteralTokenKind
{
    /// <summary>
    /// Token's Data stored as ulong. Covers: i32, i64, u32, u64.
    /// </summary>
    NumericInteger,
    /// <summary>
    /// Token's Data stored as double. Covers: f32, f64.
    /// </summary>
    NumericFloat,
    Char,
    String,
    Bool,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{LiteralTokenKind}] {Data}")]
public sealed class LiteralToken : SyntaxToken
{
    public LiteralToken(TextSpan span, LiteralTokenKind literalTokenKind, object? data) : base(SyntaxTokenKind.Literal)
    {
        Span = span;
        LiteralTokenKind = literalTokenKind;
        Data = data;
    }

    public LiteralTokenKind LiteralTokenKind { get; init; }

    public object? Data { get; init; }

    public string? NumericType { get; internal set; }

    public static LiteralToken FromBool(Token token, bool value) =>
        new(token.Span, LiteralTokenKind.Bool, value);

    public static LiteralToken FromNumericInteger(Token token, ulong value) =>
        new(token.Span, LiteralTokenKind.NumericInteger, value);

    public static LiteralToken FromNumericFloat(Token token, double value) =>
        new(token.Span, LiteralTokenKind.NumericFloat, value);

    public static LiteralToken FromChar(Token token, char? value) =>
        new(token.Span, LiteralTokenKind.Char, value);

    public static LiteralToken FromString(Token token, string? value) =>
        new(token.Span, LiteralTokenKind.String, value);

    public bool TryGetNumericIntegerValue(out ulong value)
    {
        value = default!;
        if (LiteralTokenKind != LiteralTokenKind.NumericInteger)
        {
            return false;
        }
        value = (ulong)Data!;
        return true;
    }

    public bool TryGetNumericFloatValue(out double value)
    {
        value = default!;
        if (LiteralTokenKind != LiteralTokenKind.NumericFloat)
        {
            return false;
        }
        value = (double)Data!;
        return true;
    }

    public bool TryGetCharValue(out char? value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Char)
        {
            return false;
        }
        value = (char?)Data;
        return true;
    }

    public bool TryGetStringValue(out string? value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.String)
        {
            return false;
        }
        value = (string?)Data;
        return true;
    }

    public bool TryGetBoolValue(out bool value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Bool)
        {
            return false;
        }
        value = (bool)Data!;
        return true;
    }

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitLiteralToken(this);
    }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public sealed class SkippedToken : SyntaxToken
{
    public SkippedToken(TextSpan span, List<Token> tokens)
        : base(SyntaxTokenKind.Skipped)
    {
        Span = span;
        Tokens = tokens;
    }

    public List<Token> Tokens { get; init; }

    public override void Accept(SyntaxTreeVisitor visitor)
    {
        visitor.VisitSkippedToken(this);
    }

    public override string ToString()
    {
        return string.Join(' ', Tokens.Select(x => x.ToString()));
    }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public abstract class SyntaxNode(SyntaxNodeKind kind) : SyntaxEntity
{
    // Set by the generated nodes.
    protected List<SyntaxToken?> _childTokens = default!;
    protected List<SyntaxNode?> _childNodes = default!;

    public SyntaxNodeKind Kind { get; init; } = kind;

    public IEnumerable<SyntaxToken> ChildTokens
    {
        get
        {
            foreach (var token in _childTokens)
            {
                if (token != null)
                {
                    yield return token;
                }
            }
        }
    }

    public IEnumerable<SyntaxNode> ChildNodes
    {
        get
        {
            foreach (var node in _childNodes)
            {
                if (node != null)
                {
                    yield return node;
                }
            }
        }
    }

    protected void SetChildrenParent()
    {
        foreach (var c in ChildNodes)
        {
            c.SetParent(this);
        }
        foreach (var c in ChildTokens)
        {
            c.SetParent(this);
        }
    }

    /// <summary>
    /// Enumerates ancestor nodes in breadth first order.
    /// </summary>
    public IEnumerable<SyntaxNode> Ancestors() => AncestorsCore(includeSelf: false);

    /// <summary>
    /// Enumerates ancestor nodes and self in breadth first order.
    /// </summary>
    public IEnumerable<SyntaxNode> AncestorsAndSelf() => AncestorsCore(includeSelf: true);

    private IEnumerable<SyntaxNode> AncestorsCore(bool includeSelf)
    {
        if (includeSelf)
        {
            yield return this;
        }

        for (var current = Parent; current != null; current = current.Parent)
        {
            yield return current;
        }
    }

    /// <summary>
    /// Enumerates descendant nodes in breadth first order.
    /// </summary>
    public IEnumerable<SyntaxNode> DescendantNodes() => DescendantNodesCore(includeSelf: false);

    /// <summary>
    /// Enumerates descendant nodes and self in breadth first order.
    /// </summary>
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf() => DescendantNodesCore(includeSelf: true);

    private IEnumerable<SyntaxNode> DescendantNodesCore(bool includeSelf)
    {
        if (includeSelf)
        {
            yield return this;
        }

        foreach (var child in ChildNodes)
        {
            yield return child;
        }

        foreach (var child in ChildNodes)
        {
            foreach (var item in child.DescendantNodes())
            {
                yield return item;
            }
        }
    }

    public override string ToString()
    {
        return Tree.SourceFile.Text[Span.File.Start..Span.File.End];
    }
}

public abstract class DeclNode(SyntaxNodeKind kind) : SyntaxNode(kind)
{
}

public abstract class StmtNode(SyntaxNodeKind kind) : SyntaxNode(kind)
{
}

public abstract class ExprNode(SyntaxNodeKind kind) : SyntaxNode(kind)
{
}

public abstract class TypeNode(SyntaxNodeKind kind) : SyntaxNode(kind)
{
}

public partial class BareExprStmtNode
{
    private bool? _isTreatedAsStmt;

    public bool IsTreatedAsStmt
    {
        get
        {
            if (_isTreatedAsStmt.HasValue)
                return _isTreatedAsStmt.Value;

            _isTreatedAsStmt = Expr is BlockExprNode childBlock && !childBlock.ReturnsExpr;
            return _isTreatedAsStmt.Value;
        }
    }
}

public partial class BlockExprNode
{
    private bool? _returnsExpr;

    public bool ReturnsExpr
    {
        get
        {
            if (_returnsExpr.HasValue)
                return _returnsExpr.Value;

            _returnsExpr = Stmts.Any() && Stmts.Last() is BareExprStmtNode bareExprStmt && !bareExprStmt.IsTreatedAsStmt;
            return _returnsExpr.Value;
        }
    }
}

public partial class FnSigNode
{
    public AttrsListNode? Attrs => Parent switch
    {
        FnDeclNode fnDecl => fnDecl.Attrs,
        MethodDeclNode fnDecl => fnDecl.Attrs,
        _ => throw new UnreachableException(),
    };

    public bool FnHasBody => Parent switch
    {
        FnDeclNode fnDecl => fnDecl.Body != null,
        MethodDeclNode fnDecl => fnDecl.Body != null,
        _ => throw new UnreachableException(),
    };
}

public partial class MapInitializerNode
{
    public IEnumerable<AssignmentExprNode> WellFormedExprs => Exprs.Cast<AssignmentExprNode>();
}

public static class AttrsListNodeExtensions
{
    // Extension instead of a method in a partial to be able to accept a nullable AttrsListNode.
    public static bool HasAttr(this AttrsListNode? @this, string attrName)
    {
        return @this?.Attrs.Any(x => x.Identifier.Value == attrName) ?? false;
    }
}
