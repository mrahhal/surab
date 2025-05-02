# host: windows

cmake -S . -B build-windows -G "Visual Studio 17 2022"

cmake --build build-windows --config Release

Copy-Item ./build-windows/scpp/scpp.dll ./packages/scpp.runtime.win-x64
