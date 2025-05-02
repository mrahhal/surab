# host: windows

cmake -B build -S .
# cmake -B build -S . -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=[vcpkg-root]/scripts/buildsystems/vcpkg.cmake

cmake --build ./build --config Release

# Copy-Item ./build/scpp/scpp.dll ../../src/compilation/scpp.dll
