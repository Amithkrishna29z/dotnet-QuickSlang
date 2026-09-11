# Step4.cs — Explanation

Step 3 turned the evaluator into a tiny scripting language with `PRINT` / `PRINTLINE` statements. Step 4 adds **variables**, so a script can store a value under a name and use it later:

```
NUMERIC x;          // declare a variable
x = 10;             // assign to it
y = x * 2 + 5;      // use it inside an expression
PRINTLINE y;        // → 25
```

Four things make this work:

1. **A symbol table** — `RUNTIME_CONTEXT` finally holds data: a dictionary from variable name → `SYMBOL_INFO`.
2. **New tokens** — `=` (`TOK_ASSIGN`) and the `NUMERIC` keyword (`TOK_NUMERIC`).
3. **A new expression node** — `Variable`, which reads a value from the symbol table.
4. **Two new statements** — `VariableDeclStatement` (`NUMERIC x;`) and `AssignmentStatement` (`x = expr;`).

Only numeric variables are supported in this step, so `Evaluate` still returns `double`. Step 4 also fixes two bugs carried over from Step 3 (see the end of this document).

---

## The symbol table: `RUNTIME_CONTEXT`

In Step 3, `RUNTIME_CONTEXT` was an empty class and every call passed `null`. Now it stores the variables:

```csharp
public class RUNTIME_CONTEXT
{
    private Dictionary<string, SYMBOL_INFO> _symbols = new Dictionary<string, SYMBOL_INFO>();

    public void Declare(string name, TYPE_INFO type)
    {
        if (_symbols.ContainsKey(name))
        {
            throw new Exception("Variable already declared: " + name);
        }
        _symbols[name] = new SYMBOL_INFO { SymbolName = name, Type = type };
    }

    public SYMBOL_INFO Lookup(string name)
    {
        if (!_symbols.TryGetValue(name, out SYMBOL_INFO info))
        {
            throw new Exception("Undeclared variable: " + name);
        }
        return info;
    }
}
```

- `Declare` creates a new entry, and refuses to declare the same name twice.
- `Lookup` returns the entry for a name, and fails if it was never declared.

This is where the Step 3 scaffolding pays off: each entry is a `SYMBOL_INFO`, and its `Type` is set to `TYPE_INFO.TYPE_NUMERIC`. The value itself lives in `dbl_val`.

Because `Lookup` returns the `SYMBOL_INFO` **object** (a class, so a reference), code that assigns to `Lookup(name).dbl_val` updates the value stored in the table directly.

---

## Lexer additions

### New tokens

```csharp
TOK_ASSIGN,  // '='
TOK_NUMERIC  // 'NUMERIC' keyword
```

### Recognizing `=`

A new case in `GetToken()`, alongside `;`, `(`, `)` and the operators:

```csharp
case '=':
    tok = TOKEN.TOK_ASSIGN;
    _index++;
    break;
```

### The `NUMERIC` keyword

The keyword table grows from 2 to 3 entries:

```csharp
_val = new ValueTable[3];
_val[0] = new ValueTable(TOKEN.TOK_PRINT, "PRINT");
_val[1] = new ValueTable(TOKEN.TOK_PRINTLN, "PRINTLINE");
_val[2] = new ValueTable(TOKEN.TOK_NUMERIC, "NUMERIC");
```

### Reading back the identifier: `LastString`

In Step 3 the lexer saved every non-keyword word in `last_str`, but nothing could read it. The parser now needs the variable's name, so a getter is added:

```csharp
public string LastString
{
    get { return last_str; }
}
```

Remember the lexer upper-cases every word, so `LastString` for `x` is `"X"`. This makes variable names **case-insensitive**: `x` and `X` are the same variable.

---

## The `Variable` expression

```csharp
public class Variable : Exp
{
    private string _name;
    public Variable(string name) { _name = name; }
    public override double Evaluate(RUNTIME_CONTEXT cont)
    {
        return cont.Lookup(_name).dbl_val;
    }
    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write(_name);
        return null;
    }
}
```

A new leaf node in the expression tree, next to `NumericConstant`. The difference:

- `NumericConstant` always evaluates to the number stored inside it.
- `Variable` stores only a **name**, and evaluates by looking the name up in the context *at run time*. So the same `Variable` node can produce different values as the script runs.

`GenerateJS` just writes the name.

### Parsing it: a new branch in `Factor()`

```csharp
else if (Current_Token == TOKEN.TOK_UNQUOTED_STRING)
{
    RetValue = new Variable(LastString);
    Current_Token = GetToken();
}
```

An identifier is now a valid operand, anywhere a number could appear. Note the order: `LastString` is read **before** `GetToken()`, because reading the next word would overwrite it.

---

## New statements

### `VariableDeclStatement` — `NUMERIC x;`

```csharp
public class VariableDeclStatement : Stmt
{
    private string _name;
    public VariableDeclStatement(string name) { _name = name; }
    public override bool Execute(RUNTIME_CONTEXT con)
    {
        con.Declare(_name, TYPE_INFO.TYPE_NUMERIC);
        return true;
    }
    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write("var " + _name + ";\r\n");
        return null;
    }
}
```

Executing it adds the variable to the symbol table. A declared but never-assigned variable reads as `0`, the default value of `dbl_val`.

### `AssignmentStatement` — `x = expr;`

```csharp
public class AssignmentStatement : Stmt
{
    private string _name;
    private Exp _ex;
    public AssignmentStatement(string name, Exp ex) { _name = name; _ex = ex; }
    public override bool Execute(RUNTIME_CONTEXT con)
    {
        double val = _ex.Evaluate(con);
        con.Lookup(_name).dbl_val = val;
        return true;
    }
    public override SYMBOL_INFO GenerateJS(RUNTIME_CONTEXT cont)
    {
        Console.Write(_name + "=");
        _ex.GenerateJS(cont);
        Console.Write(";\r\n");
        return null;
    }
}
```

`Execute` evaluates the right-hand side first, then stores the result. Evaluating first is what makes `x = x + 1;` work: the old `x` is read, then the new value is written.

---

## Parser additions (`RDParser`)

### Dispatching the new statements

Two new cases in `Statement()`:

```csharp
case TOKEN.TOK_NUMERIC:
    RetVal = ParseVariableDeclStatement();
    GetNext();
    break;
case TOKEN.TOK_UNQUOTED_STRING:
    RetVal = ParseAssignmentStatement();
    GetNext();
    break;
```

- A statement starting with `NUMERIC` is a declaration.
- A statement starting with a plain name is an assignment.

As with `PRINT`, the trailing `GetNext()` consumes the `;`.

### `ParseVariableDeclStatement`

```csharp
private Stmt ParseVariableDeclStatement()
{
    GetNext();                                   // consume NUMERIC
    if (Current_Token != TOKEN.TOK_UNQUOTED_STRING)
    {
        throw new Exception("Variable name expected");
    }
    string name = LastString;
    GetNext();                                   // consume the name
    if (Current_Token != TOKEN.TOK_SEMI)
    {
        throw new Exception("; is expected");
    }
    return new VariableDeclStatement(name);
}
```

Grammar: `NUMERIC <name> ;`. Using a keyword as the name (e.g. `NUMERIC print;`) fails with "Variable name expected", because `print` lexes as `TOK_PRINT`.

### `ParseAssignmentStatement`

```csharp
private Stmt ParseAssignmentStatement()
{
    string name = LastString;                    // grab it before GetNext() overwrites it
    GetNext();                                   // consume the name
    if (Current_Token != TOKEN.TOK_ASSIGN)
    {
        throw new Exception("= is expected");
    }
    GetNext();                                   // consume '='
    Exp e = Expr();
    if (Current_Token != TOKEN.TOK_SEMI)
    {
        throw new Exception("; is expected");
    }
    return new AssignmentStatement(name, e);
}
```

Grammar: `<name> = <expression> ;`. The right-hand side goes through the usual `Expr`/`Term`/`Factor` grammar, so it can contain numbers, variables, parentheses and operators.

---

## `Program` — the test driver

```csharp
static void TestVariableScript()
{
    string a = "NUMERIC x;\r\n"+
               "NUMERIC y;\r\n"+
               "x=10;\r\n"+
               "y=x*2+5;\r\n"+
               "PRINTLINE y;\r\n"+
               "x=x+1;\r\n"+
                "PRINTLINE x;\r\n";
    RDParser p = new RDParser(a);
    ArrayList arr = p.Parse();
    RUNTIME_CONTEXT ctx = new RUNTIME_CONTEXT();
    foreach (object obj in arr)
    {
        Stmt s = obj as Stmt;
        s.Execute(ctx);
        // s.GenerateJS(null);
    }
}
```

The important change from Step 3: a real `RUNTIME_CONTEXT` is created and the **same** `ctx` is passed to every statement. Passing `null` would crash as soon as `NUMERIC x;` calls `con.Declare(...)`. Creating a new context per statement would lose `x` straight after declaring it.

Output:

```
25
11
```

If you switch to the `GenerateJS(null)` line instead, the program emits:

```
var X;
var Y;
X=10;
Y=((X*2)+5);
printf(Y);
X=(X+1);
printf(X);
```

`GenerateJS` doesn't use the context, so `null` is still fine there.

---

## The pipeline now

```
"NUMERIC x; x=10; PRINTLINE x*2; ..."
   │  Lexer  (keywords incl. NUMERIC, identifiers, '=', numbers, ';')
   ▼
[NUMERIC][X][;] [X][=][10][;] [PRINTLN][X][*][2][;] ...
   │  RDParser.Parse → Statement → ParseVariableDecl / ParseAssignment / ParsePrint...
   ▼
ArrayList< Stmt >   (VariableDeclStatement(X), AssignmentStatement(X, 10), ...)
   │  for each stmt:  Execute(ctx)       →  reads/writes variables in ctx
   │             or:  GenerateJS(null)   →  emits code
   ▼
output
```

| Piece | Role |
|---|---|
| `RUNTIME_CONTEXT` (`Declare` / `Lookup`) | Symbol table: variable name → `SYMBOL_INFO` |
| `TOK_ASSIGN`, `TOK_NUMERIC`, `LastString` | Lexer support for `=`, the `NUMERIC` keyword, and reading identifier names |
| `Variable` | Expression node that reads a variable's value at run time |
| `VariableDeclStatement` | `NUMERIC x;` — adds a variable to the table |
| `AssignmentStatement` | `x = expr;` — evaluates and stores a value |
| `ParseVariableDeclStatement` / `ParseAssignmentStatement` | Parse the two new statement forms |

---

## Bugs fixed since Step 3

- **Right-associativity of `-` and `/`.** In Step 3, `Expr()` called `Expr()` for its right operand and `Term()` called `Term()`, so `10-3-2` was parsed as `10-(3-2)` = `9`. Now the loop in `Expr()` calls `Term()` and the loop in `Term()` calls `Factor()`:

  ```csharp
  // in Expr()
  Exp e1 = Term();
  // in Term()
  Exp e1 = Factor();
  ```

  The `while` loop keeps folding operands in from the left, so `10-3-2` = `(10-3)-2` = `5` and `100/10/2` = `5`.

- **Stray `;` in generated code.** `BinaryExp.GenerateJS` used to finish with `Console.Write(");")`, producing output like `Y=((X*2);+5);;`. It now writes `")"`, giving `Y=((X*2)+5);`.

- **`last_str` is no longer write-only** — it is exposed through `LastString`.

---

## Known issues in this file

- **Only numbers.** `NUMERIC` is the only type. `TYPE_INFO` is passed to `Declare` but never checked, and the `str_val` / `bol_val` slots of `SYMBOL_INFO` are unused.
- **Undeclared variables are caught only at run time.** `PRINTLINE z;` parses fine and fails when executed with "Undeclared variable: Z". Catching it while parsing would need a compile-time symbol table.
- **No decimal numbers.** The number scanner reads only digits, so `x = 2.5;` throws on the `.`.
- **Identifiers can't start with `_`** — the lexer only starts a word on `char.IsLetter`.
- **Carried over from Step 3:** unused `using` directives (`System.Linq.Expressions`, `System.Reflection`, `System.Security.Principal`), a duplicate number accessor (`Number` and `GetNumber()`), and the unused `ExpressionBuilder` that swallows exceptions.
