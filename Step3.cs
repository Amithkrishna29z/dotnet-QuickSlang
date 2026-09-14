using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

public enum TYPE_INFO
{
    TYPE_ILLEGAL = -1,
    TYPE_NUMERIC,
    TYPE_BOOL,
    TYPE_STRING,
    TYPE_ARRAY,
    TYPE_MAP
}

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
    TOK_DOUBLE, // 'number'
    TOK_NULL,
    TOK_PRINT,
    TOK_PRINTLN,
    TOK_UNQUOTED_STRING,
    TOK_SEMI
}

public struct ValueTable
{
    public TOKEN tok;
    public String Value;
    public ValueTable(TOKEN tok, String Value)
    {
        this.tok = tok;
        this.Value = Value;
    }
}

public class SYMBOL_INFO
{
    public String SymbolName;
    public TYPE_INFO Type;
    public String str_val;
    public double dbl_val;
    public bool bol_val;
}

public class RUNTIME_CONTEXT { }

public class Lexer
{
    private string _exp;
    private int _index;
    private int _length_string;
    private double _curr_num;
    private ValueTable[] _val = null;
    private string last_str;

    public Lexer(string exp)
    {
        _exp = exp;
        _length_string = exp.Length;
        _index = 0;

        _val = new ValueTable[2];
        _val[0] = new ValueTable(TOKEN.TOK_PRINT, "PRINT");
        _val[1] = new ValueTable(TOKEN.TOK_PRINTLN, "PRINTLINE");
    }

    public double Number
    {
        get { return _curr_num; }
    }

    public double GetNumber()
    {
        return _curr_num;
    }

    public TOKEN GetToken()
    {
    re_start: ///lable
        TOKEN tok = TOKEN.ILLEGAL_TOKEN;
        while ((_index < _length_string)
        && (_exp[_index] == ' ' || _exp[_index] == '\t'))
        {
            _index++;
        }

        if (_index == _length_string) return TOKEN.TOK_NULL;

        switch (_exp[_index])
        {
            case '\r':
            case '\n':
                _index++;
                goto re_start;
            case '+':
                tok = TOKEN.TOK_PLUS;
                _index++;
                break;
            case '-':
                tok = TOKEN.TOK_SUB;
                _index++;
                break;
            case '/':
                tok = TOKEN.TOK_DIV;
                _index++;
                break;
            case '*':
                tok = TOKEN.TOK_MUL;
                _index++;
                break;
            case '(':
                tok = TOKEN.TOK_OPAREN;
                _index++;
                break;
            case ')':
                tok = TOKEN.TOK_CPAREN;
                _index++;
                break;
            case ';':
                tok = TOKEN.TOK_SEMI;
                _index++;
                break;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                {
                    string str = "";
                    while ((_index < _length_string)
                    && (_exp[_index] == '0' ||
                    _exp[_index] == '1' ||
                    _exp[_index] == '2' ||
                    _exp[_index] == '3' ||
                    _exp[_index] == '4' ||
                    _exp[_index] == '5' ||
                    _exp[_index] == '6' ||
                    _exp[_index] == '7' ||
                    _exp[_index] == '8' ||
                    _exp[_index] == '9'))
                    {
                        str += Convert.ToString(_exp[_index]);
                        _index++;
                    }
                    _curr_num = Convert.ToDouble(str);
                    tok = TOKEN.TOK_DOUBLE;
                }
                break;
            default:
                {
                    if (char.IsLetter(_exp[_index]))
                    {
                        String tem = Convert.ToString(_exp[_index]);
                        _index++;
                        while (_index < _length_string && (char.IsLetterOrDigit(_exp[_index]) || _exp[_index] == '_'))
                        {
                            tem += _exp[_index];
                            _index++;
                        }
                        tem = tem.ToUpper();

                        for (int i = 0; i < this._val.Length; ++i)
                        {
                            if (_val[i].Value.CompareTo(tem) == 0) return _val[i].tok;
                        }
                        this.last_str = tem;
                        return TOKEN.TOK_UNQUOTED_STRING;
                    }
                    else
                    {
                        Console.WriteLine("Error");
                        throw new Exception();
                    }
                }
        }
        return tok;
    }
}

public abstract class Exp
{
    public abstract double Evaluate(RUNTIME_CONTEXT cont);
    public abstract SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont);
}

public class NumericConstant : Exp
{
    private double _value;
    public NumericConstant(double value) { _value = value; }
    public override double Evaluate(RUNTIME_CONTEXT cont) { return _value; }
    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write(_value);
        return null;
    }
}

public class BinaryExp : Exp
{
    private Exp _ex1, _ex2;
    private OPERATOR _op;
    public BinaryExp(Exp a, Exp b, OPERATOR op)
    {
        _ex1 = a; _ex2 = b; _op = op;
    }
    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        switch (_op)
        {
            case OPERATOR.PLUS:
                return _ex1.Evaluate(cont) + _ex2.Evaluate(cont);
            case OPERATOR.MINUS:
                return _ex1.Evaluate(cont) - _ex2.Evaluate(cont);
            case OPERATOR.DIV:
                return _ex1.Evaluate(cont) / _ex2.Evaluate(cont);
            case OPERATOR.MUL:
                return _ex1.Evaluate(cont) * _ex2.Evaluate(cont);
        }
        return Double.NaN;
    }

    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write("(");
        _ex1.GenerateJS(cont);
        switch (_op)
        {
            case OPERATOR.PLUS:
                Console.Write("+");
                break;
            case OPERATOR.MINUS:
                Console.Write("-"); ;
                break;
            case OPERATOR.DIV:
                Console.Write("/");
                break;
            case OPERATOR.MUL:
                Console.Write("*");
                break;
        }
        _ex2.GenerateJS(cont);
        Console.Write(");");
        return null;
    }
}

public class UnaryExp : Exp
{
    private Exp _ex1;
    private OPERATOR _op;
    public UnaryExp(Exp a, OPERATOR op)
    {
        _ex1 = a;
        _op = op;
    }
    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        switch (_op)
        {
            case OPERATOR.PLUS:
                return _ex1.Evaluate(cont);
            case OPERATOR.MINUS:
                return -_ex1.Evaluate(cont);
        }
        return Double.NaN;
    }

    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write((_op == OPERATOR.PLUS) ? "+" : "-");
        _ex1.GenerateJS(cont);
        return null;
    }
}

public abstract class Stmt
{
    public abstract bool Execute(RUNTIME_CONTEXT con);
    public abstract SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont);
}

public class PrintStatement : Stmt
{
    private Exp _ex;
    public PrintStatement(Exp ex) { _ex = ex; }
    public override bool Execute(RUNTIME_CONTEXT con)
    {
        double a = _ex.Evaluate(con);
        Console.Write(a.ToString());
        return true;
    }
    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write("printf(");
        _ex.GenerateJS(cont);
        Console.Write(");\r\n");
        return null;
    }
}

public class PrintLineStatement : Stmt
{
    private Exp _ex;
    public PrintLineStatement(Exp ex) { _ex = ex; }
    public override bool Execute(RUNTIME_CONTEXT con)
    {
        double a = _ex.Evaluate(con);
        Console.WriteLine(a.ToString());
        return true;
    }

    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write("printf(");
        _ex.GenerateJS(cont);
        Console.Write(");" + "\r\n");
        return null;
    }
}

public class RDParser : Lexer
{
    TOKEN Current_Token;
    TOKEN Last_Token;
    public RDParser(String str) : base(str) { }

    public Exp CallExpr()
    {
        Current_Token = GetToken();
        return Expr();
    }

    protected TOKEN GetNext()
    {
        Last_Token = Current_Token;
        Current_Token = GetToken();
        return Current_Token;
    }

    public Exp Expr()
    {
        TOKEN l_token;
        Exp RetValue = Term();
        while (Current_Token == TOKEN.TOK_PLUS || Current_Token == TOKEN.TOK_SUB)
        {
            l_token = Current_Token;
            Current_Token = GetToken();
            Exp e1 = Expr();
            RetValue = new BinaryExp(RetValue, e1,
            l_token == TOKEN.TOK_PLUS ? OPERATOR.PLUS : OPERATOR.MINUS);
        }
        return RetValue;
    }

    public Exp Term()
    {
        TOKEN l_token;
        Exp RetValue = Factor();
        while (Current_Token == TOKEN.TOK_MUL || Current_Token == TOKEN.TOK_DIV)
        {
            l_token = Current_Token;
            Current_Token = GetToken();
            Exp e1 = Term();
            RetValue = new BinaryExp(RetValue, e1,
            l_token == TOKEN.TOK_MUL ? OPERATOR.MUL : OPERATOR.DIV);
        }
        return RetValue;
    }

    public Exp Factor()
    {
        TOKEN l_token;
        Exp RetValue = null;
        if (Current_Token == TOKEN.TOK_DOUBLE)
        {
            RetValue = new NumericConstant(GetNumber());
            Current_Token = GetToken();
        }
        else if (Current_Token == TOKEN.TOK_OPAREN)
        {
            Current_Token = GetToken();
            RetValue = Expr();
            if (Current_Token != TOKEN.TOK_CPAREN)
            {
                Console.WriteLine("Missing Closing Parenthesis\n");
                throw new Exception();
            }
            Current_Token = GetToken();
        }
        else if (Current_Token == TOKEN.TOK_PLUS || Current_Token == TOKEN.TOK_SUB)
        {
            l_token = Current_Token;
            Current_Token = GetToken();
            RetValue = Factor();
            RetValue = new UnaryExp(RetValue,
            l_token == TOKEN.TOK_PLUS ? OPERATOR.PLUS : OPERATOR.MINUS);
        }
        else
        {
            Console.WriteLine("Illegal Token");
            throw new Exception();
        }
        return RetValue;
    }

    public ArrayList Parse()
    {
        GetNext();
        return StatementList();
    }
    private ArrayList StatementList()
    {
        ArrayList arr = new ArrayList();
        while (Current_Token != TOKEN.TOK_NULL)
        {
            Stmt temp = Statement();
            if (temp != null)
            {
                arr.Add(temp);
            }
        }
        return arr;
    }

    private Stmt Statement()
    {
        Stmt RetVal = null;
        switch (Current_Token)
        {
            case TOKEN.TOK_PRINT:
                RetVal = ParsePrintStatement();
                GetNext();
                break;
            case TOKEN.TOK_PRINTLN:
                RetVal = ParsePrintLNStatement();
                GetNext();
                break;
            default:
                throw new Exception("Invalid statement");
        }
        return RetVal;
    }

    private Stmt ParsePrintStatement()
    {
        GetNext();
        Exp a = Expr();
        if (Current_Token != TOKEN.TOK_SEMI)
        {
            throw new Exception("; is expected");
        }
        return new PrintStatement(a);
    }
    private Stmt ParsePrintLNStatement()
    {
        GetNext();
        Exp a = Expr();
        if (Current_Token != TOKEN.TOK_SEMI)
        {
            throw new Exception("; is expected");
        }
        return new PrintLineStatement(a);
    }
}

public class AbstractBuilder { }
public class ExpressionBuilder : AbstractBuilder
{
    public string _expr_string;
    public ExpressionBuilder(string expr)
    {
        _expr_string = expr;
    }
    public Exp GetExpression()
    {
        try
        {
            RDParser p = new RDParser(_expr_string);
            return p.CallExpr();
        }
        catch (Exception)
        { return null; }
    }
}

class Program
{
    static void TestFirstScript()
    {
        string a = "PRINTLINE 2*10;" + "\r\n" + "PRINTLINE 10; \r\n PRINT 2*10; \r\n";
        RDParser p = new RDParser(a);
        ArrayList arr = p.Parse();
        foreach (object obj in arr)
        {
            Stmt s = obj as Stmt;
            s.Execute(null);
            // s.GenerateJS(null);
        }
    }
    static void TestSecondScript()
    {
        string a = "PRINTLINE -2*10;" + "\r\n" + "PRINTLINE -10*-1;\r\n PRINT 2*10;\r\n";
        RDParser p = new RDParser(a);
        ArrayList arr = p.Parse();
        foreach (object obj in arr)
        {
            Stmt s = obj as Stmt;
            //s.Execute(null);
            s.GenerateJS(null);
        }
    }
    static void Main(string[] args)
    {
        TestFirstScript();
        Console.Read();
    }
}
