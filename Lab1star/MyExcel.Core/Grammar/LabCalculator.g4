grammar LabCalculator;

compileUnit
    : EQUALS? expression EOF
    ;

expression
    : LPAREN expression RPAREN                                            # ParenthesizedExpr
    | operatorToken=(INC | DEC) LPAREN expression RPAREN                  # IncDecExpr
    | operatorToken=(ADD | SUBTRACT) expression                           # UnaryExpr
    | expression operatorToken=(MULTIPLY | DIVIDE | DIV | MOD) expression # MultiplicativeExpr
    | expression operatorToken=(ADD | SUBTRACT) expression                # AdditiveExpr
    | NUMBER                                                              # NumberExpr
    | CELL_REFERENCE                                                      # CellReferenceExpr
    ;

INC : [iI] [nN] [cC] ;
DEC : [dD] [eE] [cC] ;
DIV : [dD] [iI] [vV] ;
MOD : [mM] [oO] [dD] ;

CELL_REFERENCE : [a-zA-Z]+ [1-9] [0-9]* ;

NUMBER : [0-9]+ ;

ADD      : '+' ;
SUBTRACT : '-' ;
MULTIPLY : '*' ;
DIVIDE   : '/' ;
LPAREN   : '(' ;
RPAREN   : ')' ;
EQUALS   : '=' ;

WS : [ \t\r\n]+ -> skip ;
