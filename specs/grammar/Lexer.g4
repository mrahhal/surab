lexer grammar Lexer
	;

fragment ANY
	: .
	;

fragment DIGIT
	: [0-9]
	;
fragment ALPHA
	: [a-z]
	| [A-Z]
	;

Number
	: DIGIT+ ('.' DIGIT+)?
	;

Char
	: '\'' ANY '\''
	;

String
	: '"' ANY* '"'
	;

Identifier
	: Identifier_Start_Character Identifier_Part_Character*
	;

fragment Identifier_Start_Character
	: '_'
	| ALPHA
	;

fragment Identifier_Part_Character
	: '_'
	| ALPHA
	| DIGIT
	;
