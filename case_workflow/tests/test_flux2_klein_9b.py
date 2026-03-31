import os
import time
import traceback
from pathlib import Path

import torch
from diffusers import Flux2KleinPipeline

import sys

_ROOT_DIR = Path(__file__).resolve().parents[1]
_SRC_DIR = _ROOT_DIR / "src"
if str(_ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(_ROOT_DIR))
if str(_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(_SRC_DIR))

from paths import OUTPUTS_DIR, model_path

# ------------------------------------------------------------------------------
# PERFORMANCE FLAGS
# ------------------------------------------------------------------------------

if torch.cuda.is_available():
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True

# ------------------------------------------------------------------------------
# PATHS
# ------------------------------------------------------------------------------

MODEL_DIR = model_path("FLUX.2-klein-9B")
OUTPUT_PATH = str(OUTPUTS_DIR / "test_flux2_klein_9b.png")

# ------------------------------------------------------------------------------
# SETTINGS
# ------------------------------------------------------------------------------

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
DTYPE = torch.bfloat16 if torch.cuda.is_available() else torch.float32

# Start with True for best odds it runs on your box.
# Flip to False if it fits and you want maximum speed.
USE_CPU_OFFLOAD = True

SEED = 123456

PROMPT = (
    "full-length, head-to-toe, full body visible, entire figure in frame, "
    "shoes visible, single person, standing pose, centered framing, no crop, "
    "super-detailed comic noir character portrait of a stern archive supervisor, "
    "tailored office coat, controlled posture, crisp inking, layered cel shading, "
    "no text, no watermark"
)

# Official FLUX.2 klein example uses 4 steps and guidance_scale=1.0
WIDTH = 1024
HEIGHT = 1024
NUM_STEPS = 4
GUIDANCE_SCALE = 1.0

# ------------------------------------------------------------------------------
# HELPERS
# ------------------------------------------------------------------------------

def print_header(title: str) -> None:
    print("\n" + "=" * 90)
    print(title)
    print("=" * 90)

def verify_paths() -> None:
    print_header("VERIFYING PATHS")
    print(f"MODEL_DIR:         {MODEL_DIR}")
    print(f"OUTPUT_PATH:       {OUTPUT_PATH}")
    print(f"DEVICE:            {DEVICE}")
    print(f"DTYPE:             {DTYPE}")
    print(f"USE_CPU_OFFLOAD:   {USE_CPU_OFFLOAD}")

    if not os.path.isdir(MODEL_DIR):
        raise FileNotFoundError(f"MODEL_DIR not found: {MODEL_DIR}")

    model_index = os.path.join(MODEL_DIR, "model_index.json")
    if not os.path.isfile(model_index):
        raise FileNotFoundError(f"Missing model_index.json: {model_index}")

    print("Path check passed.")

def load_pipeline() -> Flux2KleinPipeline:
    print_header("LOADING FLUX2KLEIN PIPELINE")

    t0 = time.time()
    pipe = Flux2KleinPipeline.from_pretrained(
        MODEL_DIR,
        torch_dtype=DTYPE,
        local_files_only=True,
    )
    print(f"Base pipeline loaded in {time.time() - t0:.2f}s.")

    if USE_CPU_OFFLOAD:
        print("\nEnabling model CPU offload...")
        pipe.enable_model_cpu_offload()
        print("CPU offload enabled.")
    else:
        print("\nMoving full pipeline to GPU...")
        t1 = time.time()
        pipe = pipe.to(DEVICE)
        print(f"Pipeline moved to device in {time.time() - t1:.2f}s.")

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

def generate_test_image(pipe: Flux2KleinPipeline) -> None:
    print_header("GENERATING TEST IMAGE")
    print(f"Prompt: {PROMPT}")
    print(f"Size: {WIDTH}x{HEIGHT}")
    print(f"Steps: {NUM_STEPS}")
    print(f"Guidance scale: {GUIDANCE_SCALE}")
    print(f"Seed: {SEED}")

    if DEVICE == "cuda":
        torch.cuda.reset_peak_memory_stats()

    generator = torch.Generator(device=DEVICE if DEVICE == "cuda" else "cpu").manual_seed(SEED)

    start = time.time()
    with torch.inference_mode():
        result = pipe(
            prompt=PROMPT,
            width=WIDTH,
            height=HEIGHT,
            guidance_scale=GUIDANCE_SCALE,
            num_inference_steps=NUM_STEPS,
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
        print("FLUX.2 klein 9B smoke test completed successfully.")

    except Exception as e:
        print_header("ERROR")
        print(f"Test failed: {e}")
        print("\nFull traceback:")
        traceback.print_exc()
