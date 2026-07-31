# syntax=docker/dockerfile:1

# ==============================================================================
# DEVPREP AI CORE - PRODUCTION DOCKERFILE
# TARGET: NVIDIA CUDA RUNTIME WITH FASTAPI & PYTORCH
# ==============================================================================

# 1. BASE IMAGE
# Utilizing official NVIDIA CUDA runtime for seamless hardware acceleration
# Version 12.1.1 is highly compatible with PyTorch 2.2+ and bitsandbytes
FROM nvidia/cuda:12.1.1-runtime-ubuntu22.04

# 2. ENVIRONMENT CONFIGURATION
# Disable interactive prompts during apt-get execution
ENV DEBIAN_FRONTEND=noninteractive
# Force Python stdout/stderr to be unbuffered to ensure real-time log ingestion
ENV PYTHONUNBUFFERED=1
# Prevent Python from writing .pyc bytecode files to disk
ENV PYTHONDONTWRITEBYTECODE=1

ENV HF_HOME=/model_cache

# 3. SYSTEM DEPENDENCIES
# Update package lists and install Python 3.10 along with essential build tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.10 \
    python3-pip \
    python3.10-dev \
    python3.10-venv \
    git \
    build-essential \
    ffmpeg \
    libgl1 \
    libglib2.0-0 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Map the system 'python' command to python3.10 for consistency
RUN ln -s /usr/bin/python3.10 /usr/bin/python

# 4. WORKSPACE INITIALIZATION
WORKDIR /app

# 5. PYTHON DEPENDENCIES
# Copy only the requirements first to leverage Docker layer caching
COPY requirements.txt .

# Upgrade pip and install core AI dependencies without caching wheel archives
RUN --mount=type=cache,target=/root/.cache/pip \
    python -m pip install --upgrade pip && \
    python -m pip install -r requirements.txt

# 5b. PIPER TTS VOICE MODEL
# Baked into the image at build time (~79MB) so no network call / volume mount is
# needed at runtime. Voice: en_US-libritts_r-medium (multi-speaker, dataset license
# CC BY 4.0 — see https://huggingface.co/rhasspy/piper-voices). Kept as its own layer,
# before COPY . ., so app code changes don't invalidate this download.
ENV PIPER_MODEL_DIR=/app/models/piper
RUN mkdir -p ${PIPER_MODEL_DIR} && \
    curl -fSL -o ${PIPER_MODEL_DIR}/en_US-libritts_r-medium.onnx \
        https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/libritts_r/medium/en_US-libritts_r-medium.onnx && \
    curl -fSL -o ${PIPER_MODEL_DIR}/en_US-libritts_r-medium.onnx.json \
        https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/libritts_r/medium/en_US-libritts_r-medium.onnx.json

# 6. SOURCE CODE INTEGRATION
# Copy the entire ai-service directory content into the container workspace
COPY . .

# 7. NETWORK EXPOSURE
# Standardize internal microservice port binding
EXPOSE 8000

# 8. EXECUTION PROTOCOL
# Launch the ASGI server. 
# CRITICAL: Restrict to a single worker process to prevent GPU VRAM duplication/OOM errors.
# The internal asyncio.to_thread configuration handles the concurrency model.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000","--reload", "--workers", "1"]