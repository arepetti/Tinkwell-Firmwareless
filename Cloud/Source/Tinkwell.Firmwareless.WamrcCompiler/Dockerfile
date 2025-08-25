FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Etc/UTC

# Install essential build tools and various cross-compilation toolchains.
# - `ca-certificates`, `wget`, `git`, `build-essential` (gcc, g++, make), `cmake`, `ninja-build`: Core development tools.
# - `wabt`: WASM binary tools (WASM validation, etc).
# - `python3`, `python3-requests`: Required in WAMRC build scripts.
# - `lsb-release`, `software-properties-common`, `gnupg`: Required by the LLVM installation script.
# - `curl`: Needed to download the Wasmer installation script.
# - `zlib1g-dev`: Development headers and libraries for Zlib, required by WAMRC.
# - `ccache`: A compiler cache to speed up repeated compilations.
# - `libgcc-s1`, `lib32gcc-s1`: Standard GCC runtime libraries.
# - `mingw-w64`: Cross-compilation toolchain for Windows targets.
# - `gcc-aarch64-linux-gnu`, `g++-aarch64-linux-gnu`: Cross-compilation tools for AArch64 Linux.
# - `gcc-arm-linux-gnueabi`, `g++-arm-linux-gnueabi`: Cross-compilation tools for ARM Linux (e.g., Raspberry Pi).
# - `gcc-riscv64-linux-gnu`, `g++-riscv64-linux-gnu`: Cross-compilation tools for RISC-V 64-bit Linux.
RUN apt-get update && apt-get install -y \
    ca-certificates \
    wget \
    git \
    build-essential \
    cmake \
    ninja-build \
    python3 \
    python3-requests \
    lsb-release \
    software-properties-common \
    gnupg \
    curl \
    zlib1g-dev \
    ccache \
    libgcc-s1 \
    lib32gcc-s1 \
    mingw-w64 \
    gcc-aarch64-linux-gnu \
    g++-aarch64-linux-gnu \
    gcc-arm-linux-gnueabi \
    g++-arm-linux-gnueabi \
    gcc-riscv64-linux-gnu \
    g++-riscv64-linux-gnu \
    wabt \
    --no-install-recommends \
    && apt-get clean -y \
    && rm -rf /var/lib/apt/lists/*

RUN wget https://apt.llvm.org/llvm.sh && \
    chmod +x llvm.sh && \
    ./llvm.sh 18

ENV PATH="/usr/lib/ccache:${PATH}"

RUN git clone https://github.com/bytecodealliance/wasm-micro-runtime.git /root/wamr && \
    cd /root/wamr/wamr-compiler && \
    mkdir build && \
    cd build && \
    cmake .. \
        -DWAMR_BUILD_AOT=1 \
        -DWAMR_BUILD_AOT_LLVM=1 \
        -DWAMR_BUILD_WITH_CUSTOM_LLVM=1 \
        -DWAMR_BUILD_AOT_LLVM_ROOT="/usr/lib/llvm-18" \
        -DCMAKE_INSTALL_PREFIX="/usr/local/wamr" \
        -DCMAKE_BUILD_TYPE=Release \
        -DZLIB_LIBRARY="/usr/lib/x86_64-linux-gnu/libz.so" \
        -DZLIB_INCLUDE_DIR="/usr/include" && \
    make -j$(nproc) && \
    make install

ENV PATH="/usr/local/wamr/bin:${PATH}"
WORKDIR /app