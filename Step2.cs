public class RUNTIME_CONTEXT { }

public enum OPERATOR
{
    ILLEGAL = -1, PLUS, MINUS, DIV, MUL
}

public enum TOKEN
{
    ILLEGAL_TOKEN = -1, // Not a Token
    TOK_PLUS = 1, // '+'
    TOK_MUL, // '*'
    TOK_DIV, // '/'
    TOK_SUB, // '-'
    TOK_OPAREN, // '('
    TOK_CPAREN, // ')'
    TOK_DOUBLE, 
    TOK_NULL // End of string
}