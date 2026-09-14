public enum OPERATOR
{
    ILLEGAL = -1, PLUS, MINUS, DIV, MUL
}

public class RUNTIME_CONTEXT{ }

public abstract class Exp
{
    public abstract double Evaluate(RUNTIME_CONTEXT cont);
}

public class NumericConstant : Exp
{
    private double _value;
    public NumericConstant(double value) { _value = value;}
    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        return _value;
    }
}

public class BinaryExp : Exp
{
    private Exp _ex1, _ex2;
    private OPERATOR _op;

    public BinaryExp(Exp a, Exp b, OPERATOR op)
    {
        _ex1=a;
        _ex2=b;
        _op=op;
    }

    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        switch(_op)
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
        switch(_op)
        {
            case OPERATOR.PLUS:
                return _ex1.Evaluate(cont);
            case OPERATOR.MINUS:
                return -_ex1.Evaluate(cont);
        }
        return Double.NaN;
    }
}


public class EntryPoint
{
    public static void Main(String[] args)
    {
        // 2+3
        Exp bin = new BinaryExp(new NumericConstant(2),new NumericConstant(3),OPERATOR.PLUS);

        // 2+3*4
        Exp bin2 = new BinaryExp(new NumericConstant(2),
         new BinaryExp(
            new NumericConstant(3),
            new NumericConstant(4),
            OPERATOR.MUL)
        ,OPERATOR.PLUS);

        Console.WriteLine(bin.Evaluate(null));
        Console.WriteLine(bin2.Evaluate(null));
    }
}
