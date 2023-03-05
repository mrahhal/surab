parser grammar Parser
	;
options {
	tokenVocab = Lexer;
}

unit
	: outer_declaration*
	;

outer_declaration
	: const_declaration
	| function_declaration
	| class_declaration
	;

const_declaration
	: 'const' Identifier colon_ty? '=' expression ';'
	;

function_declaration
	: function_header function_body
	;

function_header
	: 'function' Identifier parameters colon_ty
	;

function_body
	: block
	;

class_declaration
	: 'class' Identifier class_body
	;

class_body
	: '{' class_member_declaration* '}'
	;

class_member_declaration
	: field_declaration
	| method_declaration
	;

field_declaration
	: Identifier colon_ty? ';'
	;

// field_declarator : '=' expression ;

method_declaration
	: method_header function_body
	;

method_header
	: Identifier parameters colon_ty
	;

parameters
	: '(' parameter_list? ')'
	;

parameter_list
	: parameter (',' parameter)*
	;

parameter
	: 'field'? Identifier colon_ty
	;

argument_list
	: argument (',' argument)*
	;

argument
	: expression
	;

block
	: '{' statement_list? '}'
	;

statement_list
	: statement+
	;

statement
	: let_statement
	| expression_statement
	| if_statement
	;

if_statement
	: 'if' '(' expression ')' block
	;

// Expression parsing is handled using a Pratt operator precedence parser.
expression
	: literal_expression
	| this_expression
	| member_access_expression
	| call_expression
	| unary_expression
	| binary_expression
	| relational_expression
	| equality_expression
	;

expression_statement
	: (assignment_expression | call_expression) ';'
	;

assignment_expression
	: expression '=' expression
	;

unary_expression
	: '+' expression
	| '-' expression
	| '!' expression
	;

binary_expression
	: expression '+' expression
	| expression '-' expression
	| expression '*' expression
	| expression '/' expression
	;

relational_expression
	: expression '<' expression
	| expression '>' expression
	| expression '<=' expression
	| expression '>=' expression
	;

equality_expression
	: expression '==' expression
	| expression '!=' expression
	;

member_access_expression
	: expression '.' Identifier
	;

parenthesized_expression
	: '(' expression ')'
	;

call_expression
	: expression '(' argument_list? ')'
	;

let_statement
	: 'let' Identifier colon_ty? '=' expression ';'
	;

colon_ty
	: ':' ty
	;

ty
	: Identifier '?'?
	;

this_expression
	: 'this'
	;

literal_expression
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
