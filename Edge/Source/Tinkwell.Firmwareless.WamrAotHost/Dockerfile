FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends file
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
        build-essential \
        cmake \
        git

RUN git clone --depth=1 https://github.com/bytecodealliance/wasm-micro-runtime.git /tmp/wamr
RUN cd /tmp/wamr && \
    rm -rf /tmp/wamr/build && mkdir build && \
    cd build && \
    cmake .. \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX="/usr/local/wamr" \
        -DBUILD_SHARED_LIBS=ON \ 
        -DWAMR_BUILD_AOT=1 \
        -DWAMR_BUILD_INTERP=1 \
        -DWAMR_BUILD_JIT=0 \
        -DWAMR_BUILD_LIBC_BUILTIN=0 \
        -DWAMR_BUILD_LIBC_WASI=0 \
        -DWAMR_BUILD_GC=1 \
        -DWAMR_BUILD_TAIL_CALL=1 && \
    make -j$(nproc) && \
    make install

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Tinkwell.Firmwareless.WamrAotHost/Tinkwell.Firmwareless.WamrAotHost.csproj", "Tinkwell.Firmwareless.WamrAotHost/"]
RUN dotnet restore "./Tinkwell.Firmwareless.WamrAotHost/Tinkwell.Firmwareless.WamrAotHost.csproj"
COPY . .

WORKDIR "/src/Tinkwell.Firmwareless.WamrAotHost"
RUN dotnet build "./Tinkwell.Firmwareless.WamrAotHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tinkwell.Firmwareless.WamrAotHost.csproj" \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        -p:UseAppHost=true \
        --self-contained true && \
    cp /usr/local/wamr/lib/libiwasm.so /app/publish/

# This stage is used as the base for the final stage when launching from VS to support debugging in regular mode (Default when not using the Debug configuration)
FROM base AS aotdebug
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    gdb
USER $APP_UID
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Tinkwell.Firmwareless.WamrAotHost"]

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/Tinkwell.Firmwareless.WamrAotHost"]

