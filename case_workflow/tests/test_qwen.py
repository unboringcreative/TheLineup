import os
import time
import math
import traceback
from pathlib import Path

import torch
from diffusers import QwenImagePipeline, FlowMatchEulerDiscreteScheduler

import sys

_ROOT_DIR = Path(__file__).resolve().parents[1]
_SRC_DIR = _ROOT_DIR / "src"
if str(_ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(_ROOT_DIR))
if str(_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(_SRC_DIR))

from paths import OUTPUTS_DIR, QWEN_LORA_DIR, model_path

# ------------------------------------------------------------------------------
# PERFORMANCE FLAGS
# ------------------------------------------------------------------------------

if torch.cuda.is_available():
    # Safe speed win on modern NVIDIA cards
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True

# ------------------------------------------------------------------------------
# PATHS
# ------------------------------------------------------------------------------

MODEL_DIR = model_path("Qwen-Image-diffusers")
LORA_DIR = str(QWEN_LORA_DIR)
LORA_NAME = "Qwen-Image-2512-Lightning-4steps-V1.0-fp32.safetensors"
OUTPUT_PATH = str(OUTPUTS_DIR / "test_qwen_optimized.png")

# ------------------------------------------------------------------------------
# SETTINGS
# ------------------------------------------------------------------------------

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
DTYPE = torch.bfloat16 if torch.cuda.is_available() else torch.float32
SEED = 123456

PROMPT = (
    "full-length, head-to-toe, full body visible, entire figure in frame, "
    "shoes visible, single person, standing pose, centered framing, no crop, "
    "super-detailed comic noir character portrait of a stern archive supervisor, "
    "tailored office coat, controlled posture, crisp inking, layered cel shading, "
    "no text, no watermark"
)

# Minimal negative prompt per Qwen docs pattern
NEGATIVE_PROMPT = " "

WIDTH = 928
HEIGHT = 1664
NUM_STEPS = 4
TRUE_CFG_SCALE = 1.0

# ------------------------------------------------------------------------------
# HELPERS
# ------------------------------------------------------------------------------

def print_header(title: str) -> None:
    print("\n" + "=" * 90)
    print(title)
    print("=" * 90)

def verify_paths() -> None:
    print_header("VERIFYING PATHS")
    print(f"MODEL_DIR:   {MODEL_DIR}")
    print(f"LORA_DIR:    {LORA_DIR}")
    print(f"LORA_NAME:   {LORA_NAME}")
    print(f"OUTPUT_PATH: {OUTPUT_PATH}")
    print(f"DEVICE:      {DEVICE}")
    print(f"DTYPE:       {DTYPE}")

    if not os.path.isdir(MODEL_DIR):
        raise FileNotFoundError(f"MODEL_DIR not found: {MODEL_DIR}")

    lora_path = os.path.join(LORA_DIR, LORA_NAME)
    if not os.path.isfile(lora_path):
        raise FileNotFoundError(f"LoRA file not found: {lora_path}")

    print("Path check passed.")

def build_scheduler() -> FlowMatchEulerDiscreteScheduler:
    print_header("BUILDING LIGHTNING SCHEDULER")

    scheduler_config = {
        "base_image_seq_len": 256,
        "base_shift": math.log(3),
        "invert_sigmas": False,
        "max_image_seq_len": 8192,
        "max_shift": math.log(3),
        "num_train_timesteps": 1000,
        "shift": 1.0,
        "shift_terminal": None,
        "stochastic_sampling": False,
        "time_shift_type": "exponential",
        "use_beta_sigmas": False,
        "use_dynamic_shifting": True,
        "use_exponential_sigmas": False,
        "use_karras_sigmas": False,
    }

    scheduler = FlowMatchEulerDiscreteScheduler.from_config(scheduler_config)
    print("Scheduler built.")
    return scheduler

def load_pipeline() -> QwenImagePipeline:
    print_header("LOADING QWEN IMAGE PIPELINE")

    scheduler = build_scheduler()

    t0 = time.time()
    pipe = QwenImagePipeline.from_pretrained(
        MODEL_DIR,
        scheduler=scheduler,
        torch_dtype=DTYPE,
        local_files_only=True,
    )
    print(f"Base pipeline loaded in {time.time() - t0:.2f}s.")

    print("\nLoading LoRA...")
    t1 = time.time()
    pipe.load_lora_weights(
        LORA_DIR,
        weight_name=LORA_NAME,
        adapter_name="lightning_4step",
        local_files_only=True,
    )
    print(f"LoRA loaded in {time.time() - t1:.2f}s.")

    try:
        pipe.set_adapters(["lightning_4step"], adapter_weights=[1.0])
        print("Adapter activated.")
    except Exception as e:
        print(f"set_adapters skipped or unsupported: {e}")

    print("\nMoving pipeline to device...")
    t2 = time.time()
    pipe = pipe.to(DEVICE)
    print(f"Pipeline moved to device in {time.time() - t2:.2f}s.")

    try:
        pipe.transformer.to(memory_format=torch.channels_last)
        print("Transformer set to channels_last.")
    except Exception as e:
        print(f"channels_last skipped: {e}")

    try:
        pipe.vae.to(memory_format=torch.channels_last)
        print("VAE set to channels_last.")
    except Exception as e:
        print(f"VAE channels_last skipped: {e}")

    try:
        pipe.vae.enable_slicing()
        print("Enabled VAE slicing.")
    except Exception as e:
        print(f"VAE slicing not available: {e}")

    try:
        pipe.vae.enable_tiling()
        print("Enabled VAE tiling.")
    except Exception as e:
        print(f"VAE tiling not available: {e}")

    try:
        pipe.set_progress_bar_config(disable=False)
    except Exception:
        pass

    return pipe

def generate_test_image(pipe: QwenImagePipeline) -> None:
    print_header("GENERATING TEST IMAGE")
    print(f"Prompt: {PROMPT}")
    print(f"Negative prompt: {repr(NEGATIVE_PROMPT)}")
    print(f"Size: {WIDTH}x{HEIGHT}")
    print(f"Steps: {NUM_STEPS}")
    print(f"True CFG scale: {TRUE_CFG_SCALE}")
    print(f"Seed: {SEED}")

    if DEVICE == "cuda":
        torch.cuda.reset_peak_memory_stats()

    generator = torch.Generator(device=DEVICE).manual_seed(SEED)

    start = time.time()
    with torch.inference_mode():
        result = pipe(
            prompt=PROMPT,
            negative_prompt=NEGATIVE_PROMPT,
            width=WIDTH,
            height=HEIGHT,
            num_inference_steps=NUM_STEPS,
            true_cfg_scale=TRUE_CFG_SCALE,
            generator=generator,
        )

    elapsed = time.time() - start
    print(f"\nGeneration finished in {elapsed:.2f}s.")

    if DEVICE == "cuda":
        reserved_gb = torch.cuda.max_memory_reserved() / (1024 ** 3)
        allocated_gb = torch.cuda.max_memory_allocated() / (1024 ** 3)
        print(f"CUDA max reserved:  {reserved_gb:.2f} GB")
        print(f"CUDA max allocated: {allocated_gb:.2f} GB")

    image = result.images[0]
    image.save(OUTPUT_PATH)
    print(f"Saved image to: {OUTPUT_PATH}")

# ------------------------------------------------------------------------------
# MAIN
# ------------------------------------------------------------------------------

if __name__ == "__main__":
    try:
        verify_paths()
        pipe = load_pipeline()
        generate_test_image(pipe)

        print_header("SUCCESS")
        print("Optimized QwenImagePipeline test completed successfully.")

    except Exception as e:
        print_header("ERROR")
        print(f"Test failed: {e}")
        print("\nFull traceback:")
        traceback.print_exc()
