parser grammar Parser
	;
options {
	tokenVocab = Lexer;
}

unit
	: outer_decl*
	;

outer_decl
	: const_decl
	| fn_decl
	| class_decl
	;

const_decl
	: 'const' Identifier colon_ty? '=' expr ';'
	;

fn_decl
	: fn_header fn_body
	;

fn_header
	: 'fn' Identifier params colon_ty
	;

fn_body
	: block
	;

class_decl
	: 'class' Identifier class_body
	;

class_body
	: '{' class_member_decl* '}'
	;

class_member_decl
	: field_decl
	| method_decl
	;

field_decl
	: Identifier colon_ty? ';'
	;

// field_declarator : '=' expr ;

method_decl
	: method_header fn_body
	;

method_header
	: Identifier params colon_ty
	;

params
	: '(' param_list? ')'
	;

param_list
	: param (',' param)*
	;

param
	: 'field'? Identifier colon_ty
	;

arg_list
	: arg (',' arg)*
	;

arg
	: expr
	;

block
	: '{' stmt_list? '}'
	;

stmt_list
	: stmt+
	;

stmt
	: let_stmt
	| expr_stmt
	| if_stmt
	;

if_stmt
	: 'if' '(' expr ')' block
	;

// Expr parsing is handled using a Pratt operator precedence parser.
expr
	: literal_expr
	| this_expr
	| member_access_expr
	| call_expr
	| unary_expr
	| binary_expr
	| relational_expr
	| equality_expr
	;

expr_stmt
	: (assignment_expr | call_expr) ';'
	;

assignment_expr
	: expr '=' expr
	;

unary_expr
	: '+' expr
	| '-' expr
	| '!' expr
	;

binary_expr
	: expr '+' expr
	| expr '-' expr
	| expr '*' expr
	| expr '/' expr
	;

relational_expr
	: expr '<' expr
	| expr '>' expr
	| expr '<=' expr
	| expr '>=' expr
	;

equality_expr
	: expr '==' expr
	| expr '!=' expr
	;

member_access_expr
	: expr '.' Identifier
	;

parenthesized_expr
	: '(' expr ')'
	;

call_expr
	: expr '(' arg_list? ')'
	;

let_stmt
	: 'let' Identifier colon_ty? '=' expr ';'
	;

colon_ty
	: ':' ty
	;

ty
	: Identifier '?'?
	;

this_expr
	: 'this'
	;

literal_expr
	: Number
	| Char
	| String
	| boolean
	| 'null'
	;

boolean
	: 'true'
	| 'false'
	;
