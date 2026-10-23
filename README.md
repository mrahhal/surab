# Surab

[![CI](https://github.com/mrahhal/surab/actions/workflows/ci.yml/badge.svg)](https://github.com/mrahhal/surab/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Surab is an experimental programming language written in Rust and C#.

> [!NOTE]
> AI is not used and is explicitly forbidden in most cases. It could only be used for a refactoring that touches a large number of files and cannot be accomplished using deterministic language server tools.

## Guiding principles

- Consistent syntax -> Easy to read, write, and learn
- One proper way to do something -> Decrease noise and mental overhead associated with choices
- Builtin syntax to support common patterns -> Less boilerplate
- Module system -> Better organization and more compilation trimming
- Functional patterns only where it makes sense -> Succinct language syntax to support useful functional features
- Mutability as intent, not type behavior -> Mutability is a first-class usage capability, not a property of types

## Example

This is an example that works right now but does not represent the final design of the language.

```srb
import math;

@public
alias Area as f32;

alias fn () Area as AreaGetter;

fn print_area(getter: AreaGetter) {
	var area = getter();
	print(area);
}

/// Marks a type that has an [Area].
@public
trait HasArea {
	get_area() Area;
}

struct Square {
	side: f32;

	impl HasArea {
		get_area() Area {
			math::pow(this.side, 2)
		}
	}
}

@extern
fn call_into_c(num: f32);

fn main() {
	var square = Square.{
		side: 4.0,
	};
	print_area(square.get_area);
	call_into_c(square.side);
}
```

## Features

Features marked as completed usually meet the completion criteria for an MVP version, not the final planned version. This list is not exhaustive.

- [x] Full working pipeline: lexer, parser, analyzer, compiler, runner, language server
- [x] C ABI and calling into Rust
- [x] Local types and functions
- [x] Type inference
- [x] Module system
- [x] Aliasing system
- [x] Trait system
	- [ ] Trait based restrictions
- [ ] Pattern matching
- [ ] Monadic system
- [x] Unsafe pointers
- [x] Bultins and intrinsics
- [x] Generics
- [ ] Mutable intent
- [ ] Arrays, Slices, Enums, etc
- [ ] Classes?, boxing, and heap allocations
- [ ] Flow analysis
- [ ] First class immutability
- [ ] Runtime: GC
- [ ] Testing framework
- [ ] Builtin formatting
- [ ] Code generation support (committed generated inline snippets)
- [x] LSP (vscode)
- [ ] Self-hosting: Eventual goal is to rewrite the compiler frontend in Surab and remove C#.

## Spec

Surab is a compiled garbage collected language with functional features.

The full spec is not available in this repo right now as it's heavily subject to change.

## Platforms

Supported platforms to build from:

- Windows x86_64 - ✅
- Windows aarch64 - TBD
- Linux x86_64 - ✅
- Linux aarch64 - TBD
- macOS - TBD

## Components

### [Analysis](src/analysis)

Written in C#, this includes lexing, parsing, and analyzing Surab source code. This is meant to be used as a drop in library to analyze any Surab source code.

### [Compilation](src/compilation)

Written in C#, this includes lowering and compiling into IR using LLVM (scpp). This is meant to be used as a drop in library that can compile any Surab source code.

### [scpp](src/scpp)

Written in C++, this is a native wrapper around the LLVM C++ library and is meant to be used by the compiler frontend to call into LLVM. Prebuilt bins of this native lib are provided to avoid the dependency on LLVM/Clang in the majority of cases. For more check [DEVELOPMENT.md](DEVELOPMENT.md).

### [Runner](src/runner)

Written in C#, this is an easy way to run through the full pipeline, to script some quick Surab code.

### [lib](src/lib)

Written in Surab, this contains the Surab libraries. Right now it includes a prelude module that is implicitly included in all Surab projects.

### [Runtime](src/runtime)

Written in Rust, this is the Surab runtime. Right now this only has some code to test the C ABI when Surab calls into Rust. Later it will include the GC and any other runtime requirement.

### [LSP](src/LSP)

Written in C#, this is a fully working language server that currently supports vscode and is to be developed along with the compiler. Currently has a syntax tokenizer and various analysis features along with diagnostic reporting.

### [Scripts](scripts)

Written in C#, contains a generator script that produces parts of the compiler frontend using json files (e.g. AST classes).

### [Samples](samples)

Samples written in Surab.

## Roadmap

There's no roadmap. I work on this when I feel like it and sometimes take long breaks. Give [CONTRIBUTING.md](CONTRIBUTING.md) and [DEVELOPMENT.md](DEVELOPMENT.md) a read if interested.
