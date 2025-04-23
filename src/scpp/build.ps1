cmake -B build -S . -DCMAKE_BUILD_TYPE=Release

cmake --build ./build

Copy-Item ./build/scpp/scpp.dll ../../src/compilation/scpp.dll
