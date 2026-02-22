# Development

## Requirements

- C++ compiler with C++17 support
- .NET 10
- CMake (min 4.0) and add to path
- Rust (min 1.85)
- LLVM and Clang 20.1 are only needed when changing scpp (explained below) itself, otherwise prebuilt bins of scpp will be used

## scpp

The Surab compiler uses a native library called [scpp](./src/scpp/) to generate LLVM IR and compile to machine code. In order to make contributing to Surab easy, a prebuilt scpp for each supported platform is offered through nuget packages. So unless you need to change scpp itself, you don't have to do anything special here.

You'll only need a C++ compiler on your system if you want to change scpp, then you will need a prebuilt archive for llvm+clang (unless you want to build it yourself from scratch). For more info on that, check the README under [scpp](./src/scpp/).

For the scpp nuget packages, right now a GitHub packages feed (mrahhal) is used. You need to add that feed to the list of sources (replace OWNER and YOUR_GITHUB_PAT):

```
dotnet nuget add source --username OWNER --password YOUR_GITHUB_PAT --store-password-in-clear-text --name mrahhal-github "https://nuget.pkg.github.com/mrahhal/index.json"
```

The PAT you create needs to have the `read:packages` scope.

For more info: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry

For more info on creating a PAT (classic): https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-personal-access-token-classic

Later, scpp will be published to nuget.org so this step will become unnecessary.
