# Step2.cs — Explanation

Step 1 built expression trees **by hand**. Step 2 adds the two missing front-end pieces of an interpreter so you can type a string like `"2+3*4"` and have it turned into a tree automatically:

1. A **Lexer** (tokenizer) — turns raw characters into tokens.
2. A **Recursive-Descent Parser** (`RDParser`) — turns tokens into the same `Exp` tree from Step 1, respecting operator precedence and parentheses.

The `Exp` / `NumericConstant` / `BinaryExp` / `UnaryExp` / `RUNTIME_CONTEXT` / `OPERATOR` pieces are unchanged from Step 1 (see `Step1.md`). This document focuses on what's **new**.

---

## `TOKEN` enum — the lexer's vocabulary

```csharp
public enum TOKEN
{
    ILLEGAL_TOKEN = -1, // Not a Token
    TOK_PLUS = 1,       // '+'
    TOK_MUL,            // '*'
    TOK_DIV,            // '/'
    TOK_SUB,            // '-'
    TOK_OPAREN,         // '('
    TOK_CPAREN,         // ')'
    TOK_DOUBLE,         // a number literal
    TOK_NULL            // end of string
}
```

Each meaningful "chunk" of input has a token name. `TOK_DOUBLE` means "a number was read"; `TOK_NULL` marks the end of input. Note these are a different concept from `OPERATOR`: `TOKEN` describes what was **read from the text**, while `OPERATOR` describes what the **tree node does**. The parser translates between the two.

---

## `Lexer` — turning characters into tokens

```csharp
public class Lexer
{
    String IExpr;   // the input expression
    int index;      // current read position
    int length;     // total length
    double number;  // last number that was scanned
```

The lexer walks through the input string one character at a time, handing out one token per call to `GetToken()`.

### The constructor

```csharp
public Lexer(String Expr)
{
    IExpr = Expr;
    length = IExpr.Length;
    index = 0;
}
```

Stores the string and starts the cursor (`index`) at the beginning.

### `GetToken()` — the heart of the lexer

```csharp
while (index < length && (IExpr[index] == ' ' || IExpr[index] == '\t')) index++;
if (index == length) return TOKEN.TOK_NULL;
```

1. **Skip whitespace** (spaces and tabs).
2. If we've reached the end, return `TOK_NULL`.

Then a `switch` on the current character:

- `+ - / * ( )` → return the matching single-character token and advance `index` by one.
- **A digit `0`–`9`** → read a *run* of consecutive digits into a string, convert it to a `double` stored in `number`, and return `TOK_DOUBLE`:

  ```csharp
  String str = "";
  while (index < length && IExpr[index] is a digit)
  {
      str += Convert.ToString(IExpr[index]);
      index++;
  }
  number = Convert.ToDouble(str);
  tok = TOKEN.TOK_DOUBLE;
  ```

- **Anything else** → print an error and `throw new Exception()`.

### `GetNumber()`

```csharp
public double GetNumber() { return number; }
```

After `GetToken()` returns `TOK_DOUBLE`, the parser calls this to retrieve the actual numeric value that was scanned.

> **Note:** the lexer only recognizes integer digit runs — there is no handling of a decimal point, so `"3.5"` would fail at the `.`.

---

## `RDParser` — recursive-descent parser

```csharp
public class RDParser : Lexer
{
    TOKEN Current_Token;
    public RDParser(String str) : base(str) { }
```

`RDParser` **inherits** from `Lexer`, so it can call `GetToken()` / `GetNumber()` directly. It keeps a one-token lookahead in `Current_Token` — the "next token to be consumed".

It implements a classic **grammar** as three mutually-recursive methods. The grammar encodes precedence: `Expr` handles `+`/`-` (lowest precedence), `Term` handles `*`/`/` (higher), and `Factor` handles numbers, parentheses, and unary signs (highest).

```
Expr   → Term   { (+|-) ... }
Term   → Factor { (*|/) ... }
Factor → number | ( Expr ) | (+|-) Factor
```

### `CallExpr()` — entry point

```csharp
public Exp CallExpr()
{
    Current_Token = GetToken();  // prime the first token
    return Expr();
}
```

Reads the first token, then starts parsing at the top rule.

### `Expr()` — addition and subtraction

```csharp
Exp RetValue = Term();
while (Current_Token == TOK_PLUS || Current_Token == TOK_SUB)
{
    l_token = Current_Token;
    Current_Token = GetToken();
    Exp e1 = Expr();
    RetValue = new BinaryExp(RetValue, e1,
        l_token == TOK_PLUS ? OPERATOR.PLUS : OPERATOR.MINUS);
}
return RetValue;
```

Parse a `Term`, then while the next token is `+` or `-`, consume the operator, parse the right side, and wrap both into a `BinaryExp`. Because `Term` is called first (and handles `*`/`/`), multiplication naturally binds tighter than addition.

### `Term()` — multiplication and division

Same shape as `Expr`, but for `*` and `/`, and it calls `Factor()` for its operands.

### `Factor()` — the atoms

```csharp
if (Current_Token == TOK_DOUBLE)
{
    RetValue = new NumericConstant(GetNumber());  // a literal number
    Current_Token = GetToken();
}
else if (Current_Token == TOK_OPAREN)
{
    Current_Token = GetToken();
    RetValue = Expr();                            // recurse for the inner expression
    if (Current_Token != TOK_CPAREN) { /* error: missing ')' */ }
    Current_Token = GetToken();
}
else if (Current_Token == TOK_PLUS || Current_Token == TOK_SUB)
{
    // unary + / -  →  UnaryExp
    l_token = Current_Token;
    Current_Token = GetToken();
    RetValue = Factor();
    RetValue = new UnaryExp(RetValue,
        l_token == TOK_PLUS ? OPERATOR.PLUS : OPERATOR.MINUS);
}
else { /* error: illegal token */ }
```

A factor is one of:
- a **number** → `NumericConstant`,
- a **parenthesized expression** `( … )` → recurse into `Expr()` and require a closing `)`,
- a **unary sign** `+x` / `-x` → `UnaryExp`.

Parentheses work because `Factor` calls back up to `Expr`, letting the whole grammar restart inside the brackets. This is why the parser is "recursive descent" — the rules call each other recursively.

---

## `ExpressionBuilder` — the convenience wrapper

```csharp
public class AbstractBuilder { }
public class ExpressionBuilder : AbstractBuilder
{
    public string _expr_string;
    public ExpressionBuilder(string expr) { _expr_string = expr; }

    public Exp GetExpression()
    {
        try
        {
            RDParser p = new RDParser(_expr_string);
            return p.CallExpr();
        }
        catch (Exception) { return null; }
    }
}
```

A thin **Builder** that hides the lexer/parser wiring. Give it a string, call `GetExpression()`, and get back an `Exp` tree — or `null` if parsing failed (any thrown exception is swallowed). `AbstractBuilder` is an empty base class, a placeholder for future builder variants.

---

## `Program.Main` — running it

```csharp
static void Main(string[] args)
{
    ExpressionBuilder b = new ExpressionBuilder(args[0]);
    Exp e = b.GetExpression();
    Console.WriteLine(e.Evaluate(null));
    Console.Read();
}
```

1. Takes the **first command-line argument** as the expression string.
2. Builds the tree, evaluates it (`RUNTIME_CONTEXT` still unused → `null`), and prints the result.
3. `Console.Read()` keeps the console window open until you press a key.

Example:

```
dotnet run -- "2+3*4"
14
```

The string flows: characters → **Lexer** → tokens → **RDParser** → `Exp` tree → `Evaluate()` → a number.

---

## The full pipeline

```
"2+3*4"
   │  Lexer.GetToken()
   ▼
[2] [+] [3] [*] [4] [NULL]
   │  RDParser (Expr/Term/Factor)
   ▼
        (+)
       /   \
      2    (*)
          /   \
         3     4
   │  Evaluate(null)
   ▼
      14
```

| Piece | Responsibility |
|---|---|
| `TOKEN` | Names the kinds of lexical chunks |
| `Lexer` | Characters → tokens (one per `GetToken()`) |
| `RDParser` | Tokens → `Exp` tree, enforcing precedence & parentheses |
| `ExpressionBuilder` | Simple façade over lexer + parser |
| `Program.Main` | Reads arg, builds, evaluates, prints |

---

## Things to be aware of

- **Right-associativity bug for `-` and `/`.** `Expr()` recurses with `Expr()` (and `Term()` with `Term()`) for the right operand instead of parsing the *next* single term. That makes them **right-associative**, so `10-3-2` parses as `10-(3-2)=9` instead of the correct `(10-3)-2=5`. Same issue for division. Addition/multiplication still give correct results because they're associative. To fix, the right-hand call should be `Term()` in `Expr()` and `Factor()` in `Term()`.
- **No decimal-point support** in the lexer (integers only).
- **Errors are swallowed** by `ExpressionBuilder`, so a bad expression yields `null`, and `e.Evaluate(null)` would then throw a `NullReferenceException` in `Main`.
- **No `using System;`** is shown; relies on a global using or an import elsewhere to compile (`Console`, `Convert`, `Double`, `Exception`).
