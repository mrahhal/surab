# Design Goals

Targets:

- High level, statically typed, expressive, feature-full
- Dynamic support
- Bytecode compiler and VM
- Garbage collection through reference counting

## Phases

1st phase:

- Minimal language features enough to write a simple program
- Compile to bytecode file or run through in one pass

2nd phase:

- vscode language service extension (colorize, intellisense, references, etc)
- Standard lib (especially for built in types like Number/String/etc -> think about value vs reference types)
- Convention docs
- Full fidelity analyzer
- Repl
- Additional language features:
	- Auto fields in class ctor
	- Mutable
	- Default function param value
	- Extension everything (useful for records)
	- Block expressions (with a non semi coloned last expressed to denote block return result)
	- Traits
	- Tuples
	- Records (not values by default)
	- Algebraic types
	- Generics
	- Extensions

Stretch phase:

- Incremental re-analysis, instead of full file re-analysis on code changes
- tooling: format, linter, etc

## Notes

- All types are non null, unless you append "?"
- No inheritance, only composition
- Garbage collector will have to solve the cyclic reference problem
- Everything is private by default
- Mutable feature instead of read/write accessors?
	- Immutable reference doesn't mean it won't change. It means mutation is more explicit and intentional.
- Classes drive behavior, Records drive pure data (and can be either by value or by ref)
- Entry point is always a file named main or lib (like rust) and has the same module as the project

## Improvements

- Use a register-based bytecode compiler instead of a stack
