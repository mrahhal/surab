# Runtime

Runtime for Surab written in Rust.

Build:

```
cargo build
```

Make sure this is built and up to date when tinkering with the runner project.

To get which static libs we need to link against:

```sh
cargo rustc -- --print native-static-libs
```

Some useful commands:

```sh
# Emit the LLVM IR file using various optimization levels. opt-level=3 does high opt.
rustc --emit llvm-ir .\src\lib.rs --crate-type lib -C link-dead-code=y -C inline-threshold=0 -C lto=n -C opt-level=0
```
