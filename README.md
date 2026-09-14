# QuickSlang

A small **scripting-language interpreter** built incrementally in C#. Each `StepN.cs` file is a self-contained snapshot that adds one new capability, so you can watch a simple arithmetic evaluator grow into a real interpreter with a lexer, parser, statements, and a code-generation backend.

Every step has a matching `StepN.md` that walks through the code in plain English.

---

## Steps

| Step | Source | Explanation | What it adds |
|------|--------|-------------|--------------|
| 1 | [`Step1.cs`](Step1.cs) | [`Step1.md`](Step1.md) | Expression trees (`Exp`, `NumericConstant`, `BinaryExp`, `UnaryExp`) built **by hand** and evaluated recursively |
| 2 | [`Step2.cs`](Step2.cs) | [`Step2.md`](Step2.md) | A **Lexer** (tokenizer) and a **recursive-descent parser** that turn a string like `"2+3*4"` into the tree automatically |
| 3 | [`Step3.cs`](Step3.cs) | [`Step3.md`](Step3.md) | **Statements** (`PRINT` / `PRINTLINE`), keyword lexing, multi-statement scripts, and a second backend `GenerateJS` that emits code instead of evaluating |
| 4 | [`Step4.cs`](Step4.cs) | [`Step4.md`](Step4.md) | **Variables** — `NUMERIC` declarations, assignment, and variable use in expressions, backed by a symbol table in `RUNTIME_CONTEXT` |

---

## Core idea

An expression (and later a whole script) is modeled as a **tree of objects** (an AST). The same tree can be driven by multiple backends:

- **`Evaluate`** — interpret the tree and compute a value.
- **`GenerateJS`** (from Step 3) — walk the tree and emit source code.

```
source text ──▶ Lexer ──▶ tokens ──▶ Parser ──▶ AST ──▶ Evaluate / GenerateJS ──▶ output
```

This is a classic combination of the **Interpreter** and **Composite** design patterns.

---

## Running

Each step is a standalone program with its own `Main`. To run one, compile just that file — for example, Step 3:

```bash
dotnet run Step3.cs        # .NET 10+ file-based apps
# or, with a project:
csc Step3.cs && ./Step3
```

- **Step 1** prints the results of two hard-coded expressions (`5` and `14`).
- **Step 2** reads the expression from the first command-line argument (e.g. `"2+3*4"`).
- **Step 3** runs a built-in test script and prints the generated code.
- **Step 4** runs a built-in script that uses variables (`25` and `11`).

> Note: the steps define overlapping type names (`Exp`, `Lexer`, `OPERATOR`, …) on purpose — each is meant to be compiled **on its own**, not all together.

---

## Status & known issues

This is a learning project, so some steps contain deliberate or in-progress bugs (documented in each `StepN.md`):

- Subtraction and division are **right-associative** in Steps 2–3 due to right-recursion in the parser (e.g. `10-3-2` → `9`). Fixed in Step 4.

See the per-step `.md` files for full explanations and the complete issue list.
