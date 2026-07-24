import os

_hf_home = os.environ.get("HF_HOME")
if _hf_home:
    _drive = os.path.splitdrive(_hf_home)[0]
    if _drive and not os.path.isdir(_drive + os.sep):
        print(f"HF_HOME={_hf_home!r} points to an unavailable drive; ignoring it for this run.", flush=True)
        os.environ.pop("HF_HOME", None)

import threading
import torch
from transformers import AutoTokenizer, AutoModelForCausalLM, BitsAndBytesConfig
from huggingface_hub import login

_model = None
_tokenizer = None
_lock = threading.Lock()


def get_model_and_tokenizer():
    global _model, _tokenizer
    if _model is not None:
        return _model, _tokenizer

    with _lock:
        if _model is not None:
            return _model, _tokenizer

        hf_token = os.environ.get("HF_TOKEN")
        if hf_token:
            try:
                login(token=hf_token)
            except Exception as e:
                print(f"HF login() failed to persist token, continuing anyway: {e}", flush=True)

        model_id = os.environ.get("AI_MODEL_ID", "google/gemma-4-E4B-it")
        print(f"Loading {model_id}...", flush=True)

        _tokenizer = AutoTokenizer.from_pretrained(model_id)

        if torch.cuda.is_available():
            try:
                bnb_config = BitsAndBytesConfig(
                    load_in_4bit=True,
                    bnb_4bit_compute_dtype=torch.bfloat16,
                    bnb_4bit_use_double_quant=True,
                    bnb_4bit_quant_type="nf4",
                    llm_int8_enable_fp32_cpu_offload=True,
                )
                _model = AutoModelForCausalLM.from_pretrained(
                    model_id,
                    quantization_config=bnb_config,
                    device_map="auto",
                    low_cpu_mem_usage=True,
                )
                print("Model loaded on GPU (4-bit) with CPU offload enabled.", flush=True)
            except Exception as e:
                print(f"GPU load failed: {e}\nFalling back to CPU...", flush=True)
                _model = None

        if _model is None:
            # bfloat16 is 2x faster than float32 on modern CPUs
            try:
                _model = AutoModelForCausalLM.from_pretrained(
                    model_id,
                    device_map={"": "cpu"},
                    torch_dtype=torch.bfloat16,
                    low_cpu_mem_usage=True,
                )
                print("Model loaded on CPU (bfloat16).", flush=True)
            except Exception:
                _model = AutoModelForCausalLM.from_pretrained(
                    model_id,
                    device_map={"": "cpu"},
                    torch_dtype=torch.float32,
                    low_cpu_mem_usage=True,
                )
                print("Model loaded on CPU (float32).", flush=True)

        _model.eval()
        print("Model ready!", flush=True)

    return _model, _tokenizer
