# Use a lean base image
FROM ubuntu:22.04

# Install only necessary tools for downloading and extracting
RUN apt-get update && apt-get install -y \
    wget \
    ca-certificates \
    tar \
    && rm -rf /var/lib/apt/lists/*

# Set WAMR version and asset details from the official release
ENV WAMR_VERSION=2.4.1
ENV WAMR_TAG=WAMR-${WAMR_VERSION}
ENV WAMR_ASSET=wamrc-${WAMR_VERSION}-x86_64-ubuntu-22.04

# Download and install the pre-built wamrc binary
WORKDIR /tmp
RUN wget https://github.com/bytecodealliance/wasm-micro-runtime/releases/download/${WAMR_TAG}/${WAMR_ASSET}.tar.gz
RUN tar -xzf ${WAMR_ASSET}.tar.gz

# Copy the wamrc tool to a directory in the PATH
# The binary is extracted to the current directory.
RUN cp ./wamrc /usr/local/bin/wamrc

# Clean up downloaded files
RUN rm -rf /tmp/*

# Set the final working directory for the container's runtime
WORKDIR /data

# This image does not run a command by default.
# The command will be supplied by the CompilationServer when it runs the container.
