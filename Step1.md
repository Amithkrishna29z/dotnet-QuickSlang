# Step1.cs — Explanation

This file is the first step in building a small **expression evaluator** (the core of an interpreter). It represents arithmetic expressions as a tree of objects and evaluates them recursively. This design is a textbook example of the **Interpreter pattern** combined with the **Composite pattern**.

---

## `RUNTIME_CONTEXT`

```csharp
public class RUNTIME_CONTEXT{ }
```

An empty placeholder class. It is passed into every `Evaluate` call as `cont`. Right now it holds nothing, but it is a hook for the future — in later steps it will carry state like variable values, so an expression such as `x + 1` can look up what `x` is at runtime.

---

## `OPERATOR` enum

```csharp
public enum OPERATOR
{
    ILLEGAL = -1, PLUS, MINUS, DIV, MUL
}
```

Names the arithmetic operators instead of using raw strings or magic numbers.

- `ILLEGAL = -1` — a sentinel for "no valid operator".
- The rest auto-number from `0`: `PLUS = 0`, `MINUS = 1`, `DIV = 2`, `MUL = 3`.

Using an enum makes the `switch` statements in the expression classes readable and type-safe.

---

## `Exp` — the abstract base

```csharp
public abstract class Exp
{
    public abstract double Evaluate(RUNTIME_CONTEXT cont);
}
```

The common base type for **every** kind of expression. It declares one contract: *"any expression can be evaluated to a `double`."*

Because it is `abstract`, you can never create a plain `Exp` — only concrete subclasses. This is what lets a `BinaryExp` hold two children of type `Exp` without caring whether each child is a number, another binary expression, or a unary expression.

---

## `NumericConstant` — a literal number

```csharp
public class NumericConstant : Exp
{
    private double _value;
    public NumericConstant(double value) { _value = value; }
    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        return _value;
    }
}
```

The simplest ("leaf") expression. It just wraps a fixed number.

- The constructor stores the number in `_value`.
- `Evaluate` ignores the context and returns that number as-is.

`NumericConstant(2)` represents the literal `2`.

---

## `BinaryExp` — an operation with two operands

```csharp
public class BinaryExp : Exp
{
    private Exp _ex1, _ex2;
    private OPERATOR _op;

    public BinaryExp(Exp a, Exp b, OPERATOR op)
    {
        _ex1 = a;
        _ex2 = b;
        _op = op;
    }
    ...
}
```

Represents an operation with a left side, a right side, and an operator — like `2 + 3`.

Key point: `_ex1` and `_ex2` are of type `Exp`, not `double`. That means each side can itself be *any* expression, including another `BinaryExp`. This is what allows nesting to build a tree.

Its `Evaluate` first evaluates both children (recursively), then combines them based on `_op`:

```csharp
switch(_op)
{
    case OPERATOR.PLUS:  return _ex1.Evaluate(cont) + _ex2.Evaluate(cont);
    case OPERATOR.MINUS: return _ex1.Evaluate(cont) - _ex2.Evaluate(cont);
    case OPERATOR.DIV:   return _ex1.Evaluate(cont) / _ex2.Evaluate(cont);
    case OPERATOR.MUL:   return _ex1.Evaluate(cont) * _ex2.Evaluate(cont);
}
return Double.NaN;
```

If `_op` is none of the four (e.g. `ILLEGAL`), it returns `Double.NaN` ("Not a Number") as an error result rather than crashing.

---

## `UnaryExp` — an operation with one operand

```csharp
public class UnaryExp : Exp
{
    private Exp _ex1;
    private OPERATOR _op;

    public UnaryExp(Exp a, OPERATOR op) { _ex1 = a; _op = op; }
    ...
}
```

Represents an operator applied to a single operand, like `-5` or `+5`.

Its `Evaluate`:

```csharp
switch(_op)
{
    case OPERATOR.PLUS:  return  _ex1.Evaluate(cont);   // +x is just x
    case OPERATOR.MINUS: return -_ex1.Evaluate(cont);   // negate
}
return Double.NaN;
```

- Unary `PLUS` returns the value unchanged.
- Unary `MINUS` negates it.
- `DIV`/`MUL` make no sense with one operand, so they fall through to `Double.NaN`.

---

## `EntryPoint` — the program's `Main`

```csharp
public class EntryPoint
{
    public static void Main(String[] args)
    {
        // 2+3
        Exp bin = new BinaryExp(new NumericConstant(2), new NumericConstant(3), OPERATOR.PLUS);

        // 2+3*4
        Exp bin2 = new BinaryExp(new NumericConstant(2),
         new BinaryExp(
            new NumericConstant(3),
            new NumericConstant(4),
            OPERATOR.MUL)
        , OPERATOR.PLUS);

        Console.WriteLine(bin.Evaluate(null));
        Console.WriteLine(bin2.Evaluate(null));
    }
}
```

This builds two expression trees by hand and evaluates them.

### First tree — `2 + 3`

```
    (+)
   /   \
  2     3
```

`bin.Evaluate(null)` → `2 + 3` → **5**.

### Second tree — `2 + 3 * 4`

The `3 * 4` is a `BinaryExp` that becomes the *right child* of the outer `+`:

```
    (+)
   /   \
  2    (*)
      /   \
     3     4
```

Evaluation is recursive and bottom-up:
1. Outer `+` asks its right child (the `*`) to evaluate → `3 * 4 = 12`.
2. Outer `+` then computes `2 + 12` → **14**.

Note this tree hard-codes the correct precedence (multiply before add) via its *structure* — there is no parser yet, so the nesting is written manually.

`null` is passed as the context because these constants don't need any runtime state.

### Expected output

```
5
14
```

---

## Big picture

| Concept | Role in this file |
|---|---|
| `Exp` (abstract) | Common interface: everything can `Evaluate` |
| `NumericConstant` | Leaf node (a raw number) |
| `BinaryExp` / `UnaryExp` | Branch nodes (operations that hold child expressions) |
| Recursion | `Evaluate` calls `Evaluate` on children until it hits constants |
| `RUNTIME_CONTEXT` | Placeholder for future runtime state (variables, etc.) |

The takeaway: an expression is modeled as a **tree of objects**, and evaluating the whole thing is just asking the root to evaluate itself, which cascades down to the leaves.

> **Note:** This snippet uses `Console` and `Double` without a `using System;` at the top. It compiles as shown only if `System` is imported elsewhere or via global usings; otherwise add `using System;`.
