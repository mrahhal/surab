#!/bin/bash

cmake -S . -B build-linux -G "Ninja" \
	-DCMAKE_BUILD_TYPE="Release"

cmake --build build-linux
