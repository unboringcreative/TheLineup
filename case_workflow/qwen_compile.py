import os
import math
import time
import traceback
import torch
import torch._dynamo
from diffusers import QwenImagePipeline, FlowMatchEulerDiscreteScheduler

from paths import OUTPUTS_DIR, QWEN_LORA_DIR, model_path

# ---- Paths ----
MODEL_DIR = model_path("Qwen-Image-diffusers")
LORA_DIR = str(QWEN_LORA_DIR)
LORA_NAME = "Qwen-Image-2512-Lightning-4steps-V1.0-fp32.safetensors"
OUTPUT_PATH = str(OUTPUTS_DIR / "test_qwen_compile_workaround.png")

# ---- Performance flags ----
if torch.cuda.is_available():
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
DTYPE = torch.bfloat16 if torch.cuda.is_available() else torch.float32

PROMPT = (
    "full-length, head-to-toe, full body visible, entire figure in frame, "
    "shoes visible, single person, standing pose, centered framing, no crop, "
    "super-detailed comic noir character portrait of a stern archive supervisor, "
    "tailored office coat, controlled posture, crisp inking, layered cel shading, "
    "no text, no watermark"
)
NEGATIVE_PROMPT = " "
WIDTH = 928
HEIGHT = 1664
NUM_STEPS = 4
TRUE_CFG_SCALE = 1.0
SEED = 123456

# ---- TorchDynamo fallback ----
torch._dynamo.config.suppress_errors = True

# ---- Surgical compile disable for known-problem Qwen bits ----
import diffusers.models.transformers.transformer_qwenimage as tq

if hasattr(tq, "apply_rotary_emb_qwen"):
    tq.apply_rotary_emb_qwen = torch.compiler.disable(tq.apply_rotary_emb_qwen)

# Your crash mentioned pos_embed / _compute_video_freqs, so disable those too if present.
for cls_name in [
    "QwenImageVisionRotaryEmbedding",
    "Qwen2_5_VLVisionRotaryEmbedding",
    "QwenImageRotaryPosEmbed",
]:
    cls = getattr(tq, cls_name, None)
    if cls is not None:
        if hasattr(cls, "forward"):
            cls.forward = torch.compiler.disable(cls.forward)
        if hasattr(cls, "_compute_video_freqs"):
            cls._compute_video_freqs = torch.compiler.disable(cls._compute_video_freqs)

def build_scheduler():
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
    return FlowMatchEulerDiscreteScheduler.from_config(scheduler_config)

def main():
    print(f"DEVICE={DEVICE} DTYPE={DTYPE}")

    pipe = QwenImagePipeline.from_pretrained(
        MODEL_DIR,
        scheduler=build_scheduler(),
        torch_dtype=DTYPE,
        local_files_only=True,
    )

    pipe.load_lora_weights(
        LORA_DIR,
        weight_name=LORA_NAME,
        adapter_name="lightning_4step",
        local_files_only=True,
    )

    try:
        pipe.set_adapters(["lightning_4step"], adapter_weights=[1.0])
    except Exception as e:
        print(f"set_adapters skipped: {e}")

    pipe = pipe.to(DEVICE)

    try:
        pipe.vae.enable_slicing()
    except Exception:
        pass

    try:
        pipe.vae.enable_tiling()
    except Exception:
        pass

    # Optional: try a native attention backend that may behave better with compile
    try:
        pipe.transformer.set_attention_backend("_native_cudnn")
        print("Using _native_cudnn attention backend")
    except Exception as e:
        print(f"Could not set _native_cudnn backend: {e}")

    # Regional compilation, but NOT fullgraph
    try:
        pipe.transformer.compile_repeated_blocks(
            fullgraph=False,
            dynamic=True,
            mode="reduce-overhead",
        )
        print("Regional compilation enabled.")
    except TypeError:
        try:
            pipe.transformer.compile_repeated_blocks(fullgraph=False)
            print("Regional compilation enabled with fallback signature.")
        except Exception as e:
            print(f"Regional compilation failed: {e}")
    except Exception as e:
        print(f"Regional compilation failed: {e}")

    generator = torch.Generator(device=DEVICE).manual_seed(SEED)

    start = time.time()
    result = pipe(
        prompt=PROMPT,
        negative_prompt=NEGATIVE_PROMPT,
        width=WIDTH,
        height=HEIGHT,
        num_inference_steps=NUM_STEPS,
        true_cfg_scale=TRUE_CFG_SCALE,
        generator=generator,
    )
    print(f"Generation finished in {time.time() - start:.2f}s")

    image = result.images[0]
    image.save(OUTPUT_PATH)
    print(f"Saved: {OUTPUT_PATH}")

if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
