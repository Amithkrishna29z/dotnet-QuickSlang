# Step3.cs — Explanation

Step 2 could parse and evaluate a single arithmetic expression. Step 3 grows the project from an *expression evaluator* into the start of a real **scripting language interpreter**. Three big things are added:

1. **Statements** — the language now understands commands like `PRINT` and `PRINTLINE`, terminated by `;`, so a script can be a *list* of statements.
2. **Keyword & identifier lexing** — the lexer can now read words (letters/digits/underscore) and recognize reserved keywords.
3. **A second backend: `GenerateJS`** — besides evaluating (`Evaluate`), every node can now *emit code* (a compiler-style "code generation" pass) instead of just computing a number.

Groundwork for future steps (variables, types) also appears: `TYPE_INFO` and `SYMBOL_INFO`.

The `Exp` tree classes are mostly the same as Step 2 (see `Step2.md`); this document focuses on what's new.

---

## New type-system groundwork

### `TYPE_INFO`

```csharp
public enum TYPE_INFO
{
    TYPE_ILLEGAL = -1,
    TYPE_NUMERIC, TYPE_BOOL, TYPE_STRING, TYPE_ARRAY, TYPE_MAP
}
```

Names the value types the language will eventually support. Not really used yet — it's scaffolding for later steps when variables can hold numbers, booleans, strings, arrays, or maps.

### `SYMBOL_INFO`

```csharp
public class SYMBOL_INFO
{
    public String SymbolName;
    public TYPE_INFO Type;
    public String str_val;
    public double dbl_val;
    public bool bol_val;
}
```

A record for a named symbol (variable): its name, its type, and slots for each possible value kind. It's also the declared return type of the new `GenerateJS` methods — although those currently always `return null`, so it's a placeholder return for now.

---

## Lexer additions

### New tokens

```csharp
TOK_PRINT, TOK_PRINTLN, TOK_UNQUOTED_STRING, TOK_SEMI
```

- `TOK_PRINT` / `TOK_PRINTLN` — the `PRINT` / `PRINTLINE` keywords.
- `TOK_UNQUOTED_STRING` — an identifier/word that isn't a keyword.
- `TOK_SEMI` — the `;` statement terminator.

### The keyword table

```csharp
public struct ValueTable
{
    public TOKEN tok;
    public String Value;
    public ValueTable(TOKEN tok, String Value) { this.tok = tok; this.Value = Value; }
}
```

```csharp
_val = new ValueTable[2];
_val[0] = new ValueTable(TOKEN.TOK_PRINT,   "PRINT");
_val[1] = new ValueTable(TOKEN.TOK_PRINTLN, "PRINTLINE");
```

A small lookup table mapping keyword text → token. The lexer consults it whenever it reads a word.

### Newline handling with a `goto`

```csharp
re_start: ///lable
    ...
    case '\r':
    case '\n':
        _index++;
        goto re_start;
```

When the lexer hits a carriage-return or newline, it skips the character and **jumps back** to the label to start scanning the next token. This lets scripts span multiple lines. (A `while` loop would be the more conventional way to do this, but the `goto` works.)

### Reading words / keywords

```csharp
default:
{
    if (char.IsLetter(_exp[_index]))
    {
        String tem = Convert.ToString(_exp[_index]);
        _index++;
        while (_index < _length_string &&
               (char.IsLetterOrDigit(_exp[_index]) || _exp[_index] == '_'))
        {
            tem += _exp[_index];
            _index++;
        }
        tem = tem.ToUpper();

        for (int i = 0; i < this._val.Length; ++i)
            if (_val[i].Value.CompareTo(tem) == 0) return _val[i].tok;

        this.last_str = tem;
        return TOKEN.TOK_UNQUOTED_STRING;
    }
    else { Console.WriteLine("Error"); throw new Exception(); }
}
```

The lexer's fallback branch now:
1. If the character is a letter, read a full **word** (letters, digits, underscore).
2. Upper-case it (so keywords are case-insensitive: `print` == `PRINT`).
3. Look it up in the keyword table — if found, return that keyword token.
4. Otherwise remember it in `last_str` and return `TOK_UNQUOTED_STRING`.

The number-scanning and operator/`;` cases are otherwise like Step 2.

---

## Dual backends on `Exp` and `Stmt`

The abstract base now declares **two** operations:

```csharp
public abstract class Exp
{
    public abstract double Evaluate(RUNTIME_CONTEXT cont);        // interpret → a number
    public abstract SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont); // emit code
}
```

- `Evaluate` — the interpreter path from earlier steps (compute the value).
- `GenerateJS` — a code-generation path: instead of computing, it **prints out source code** for the expression.

For example, `BinaryExp.GenerateJS` writes a fully-parenthesized infix form:

```csharp
Console.Write("(");
_ex1.GenerateJS(cont);
Console.Write("+");   // or - * /
_ex2.GenerateJS(cont);
Console.Write(");");
```

So the tree for `2*10` emits `(2*10)`. This is the classic idea that one AST can drive multiple backends (an evaluator *and* a translator) via the same recursive structure.

---

## Statements

```csharp
public abstract class Stmt
{
    public abstract bool Execute(RUNTIME_CONTEXT con);
    public abstract SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont);
}
```

A `Stmt` is a top-level command. Like `Exp`, it has both an interpret path (`Execute`) and a codegen path (`GenerateJS`).

### `PrintStatement` / `PrintLineStatement`

```csharp
public class PrintStatement : Stmt
{
    private Exp _ex;
    public PrintStatement(Exp ex) { _ex = ex; }

    public override bool Execute(RUNTIME_CONTEXT con)
    {
        double a = _ex.Evaluate(con);
        Console.Write(a.ToString());   // WriteLine in PrintLineStatement
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
```

Both wrap an expression. `Execute` evaluates the expression and prints the number (`PrintLineStatement` adds a newline). `GenerateJS` emits a `printf(...)` call around the generated expression code. They differ only in `Write` vs `WriteLine`.

---

## Parser additions (`RDParser`)

### One-token lookahead helper

```csharp
protected TOKEN GetNext()
{
    Last_Token = Current_Token;
    Current_Token = GetToken();
    return Current_Token;
}
```

Advances the token stream while also remembering the previous token in `Last_Token`.

### Parsing a whole script

```csharp
public ArrayList Parse()
{
    GetNext();               // prime the first token
    return StatementList();
}

private ArrayList StatementList()
{
    ArrayList arr = new ArrayList();
    while (Current_Token != TOKEN.TOK_NULL)
    {
        Stmt temp = Statement();
        if (temp != null) arr.Add(temp);
    }
    return arr;
}
```

`Parse()` reads statements until end-of-input, collecting each into an `ArrayList`. This is what turns a multi-statement script into a list of `Stmt` objects.

### Dispatching by keyword

```csharp
private Stmt Statement()
{
    switch (Current_Token)
    {
        case TOKEN.TOK_PRINT:   RetVal = ParsePrintStatement();   GetNext(); break;
        case TOKEN.TOK_PRINTLN: RetVal = ParsePrintLNStatement(); GetNext(); break;
        default: throw new Exception("Invalid statement");
    }
    return RetVal;
}

private Stmt ParsePrintStatement()
{
    GetNext();                  // consume PRINT
    Exp a = Expr();             // parse the expression
    if (Current_Token != TOKEN.TOK_SEMI) throw new Exception("; is expected");
    return new PrintStatement(a);
}
```

`Statement()` looks at the current keyword and calls the matching parse routine. Each `ParseXxx` consumes the keyword, parses an expression, and requires a terminating `;`. The `Expr`/`Term`/`Factor` expression grammar is inherited from Step 2.

---

## `Program` — the test drivers

```csharp
static void TestFirstScript()
{
    string a = "PRINTLINE 2*10;\r\nPRINTLINE 10; \r\n PRINT 2*10; \r\n";
    RDParser p = new RDParser(a);
    ArrayList arr = p.Parse();
    foreach (object obj in arr)
    {
        Stmt s = obj as Stmt;
        // s.Execute(null);      // ← interpret path (commented out)
        s.GenerateJS(null);      // ← codegen path (active)
    }
}

static void Main(string[] args)
{
    TestFirstScript();
    Console.Read();
}
```

Each test builds a multi-line script string, parses it into a list of statements, then loops over them. The `Execute(null)` line (interpret and print numbers) is commented out; the active call is `GenerateJS(null)`, so the program **prints generated code** rather than results. For `TestFirstScript` the emitted output is roughly:

```
printf((2*10));
printf(10);
printf((2*10));
```

`Console.Read()` keeps the window open.

---

## The pipeline now

```
"PRINTLINE 2*10; ..."
   │  Lexer  (keywords, numbers, ; , newlines)
   ▼
[PRINTLN][2][*][10][;] ...
   │  RDParser.Parse → StatementList → Statement → Expr/Term/Factor
   ▼
ArrayList< Stmt >   (PrintLineStatement( BinaryExp(2,10,MUL) ), ... )
   │  for each stmt:  Execute(null)      →  runs it (prints numbers)
   │             or:  GenerateJS(null)   →  emits code (printf(...))
   ▼
output
```

| Piece | Role |
|---|---|
| `TYPE_INFO`, `SYMBOL_INFO` | Type/variable scaffolding for later steps |
| `ValueTable` + keyword loop | Lexer recognizes `PRINT`/`PRINTLINE` keywords |
| `TOK_SEMI`, newline `goto` | Multi-statement, multi-line scripts |
| `Stmt` / `PrintStatement` / `PrintLineStatement` | Statements with interpret + codegen paths |
| `GenerateJS` (on `Exp` & `Stmt`) | Second backend that emits source code |
| `RDParser.Parse` / `StatementList` / `Statement` | Parse a whole script into a `Stmt` list |

---

## Known issues in this file

- ~~**Compile error — `Factor()` never returns.**~~ **Fixed** — `RDParser.Factor()` now ends with `return RetValue;`, resolving the earlier `CS0161: not all code paths return a value`.
- **Right-associativity bug** carried from Step 2: `Expr()` recurses with `Expr()` and `Term()` with `Term()` for the right operand, making `-` and `/` right-associative (e.g. `10-3-2` → `9`). The sample scripts only use `*`, so it isn't visible.
- **`last_str` is write-only** — the identifier text for `TOK_UNQUOTED_STRING` is stored but has no getter, so it can't be retrieved yet.
- **Unused `using` directives** (`System.Linq.Expressions`, `System.Reflection`, `System.Security.Principal`) and a **duplicate number accessor** (`Number` property and `GetNumber()` both return `_curr_num`).
- **`ExpressionBuilder`** exists but is unused by `Main`, and it swallows exceptions (`catch → return null`).
