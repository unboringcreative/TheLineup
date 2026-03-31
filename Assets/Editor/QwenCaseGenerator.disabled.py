import json
import os
import sys
import torch
import random
import numpy as np
from pathlib import Path
from PIL import Image

# Add ComfyUI to Python path
COMFYUI = Path(r"C:\SwarmUI\SwarmUI\dlbackend\comfy\ComfyUI")
sys.path.insert(0, str(COMFYUI))

# Model paths (verified in filesystem)
MODELS_DIR = COMFYUI / "models"
UNET_PATH = MODELS_DIR / "diffusion_models" / "qwen_image_2512_fp8_e4m3fn.safetensors"
VAE_PATH = MODELS_DIR / "vae" / "qwen_image_vae.safetensors"
CLIP_PATH = MODELS_DIR / "clip" / "qwen_2.5_vl_7b_fp8_scaled.safetensors"
LORA_PATH = MODELS_DIR / "Lora" / "Qwen-Image-2512-Lightning-4steps-V1.0-fp32.safetensors"

# Image dimensions
DIMENSIONS = {
    "suspect":  (928, 1328),   # Portrait
    "evidence": (1328, 1328),  # Square
    "featured": (1664, 936),   # 16:9
}


def verify():
    for p in [UNET_PATH, VAE_PATH, CLIP_PATH, LORA_PATH]:
        print(f"  [{'OK' if p.exists() else 'MISSING'}] {p.relative_to(MODELS_DIR)}")
    return all(p.exists() for p in [UNET_PATH, VAE_PATH, CLIP_PATH, LORA_PATH])


def load():
    import comfy.sd
    import comfy.utils

    print("\nLoading UNet...")
    unet = comfy.sd.load_diffusion_model(str(UNET_PATH), model_options={"custom_weight_dtype": "default"})

    print("Loading VAE...")
    vae_data = comfy.utils.load_torch_file(str(VAE_PATH))
    vae = comfy.sd.VAE(sd=vae_data)

    print("Loading CLIP...")
    clip = comfy.sd.load_clip(
        ckpt_paths=[str(CLIP_PATH)],
        embedding_directory=str(MODELS_DIR / "embeddings"),
        clip_type=comfy.sd.CLIPType.QWEN_IMAGE,
    )

    print("Loading LoRA...")
    lora_sd = comfy.utils.load_torch_file(str(LORA_PATH))
    unet, clip = comfy.sd.load_lora_for_models(unet, clip, lora_sd, 1.0, 0.0)

    # Apply QwenImage shift
    model = unet.clone()
    model.model_sampling.set_parameters(shift=1.15)

    return model, vae, clip


def generate(prompt, model, vae, clip, width, height, seed):
    import comfy.sample
    import comfy.model_management as mm
    import comfy.utils

    # Encode prompt
    tokens = clip.tokenize(prompt)
    cond, pooled = clip.encode_from_tokens(tokens, return_pooled=True)
    positive = [[cond, {"pooled_output": pooled}]]
    negative = [[]]  # Empty negative

    # Create empty latent
    latent = torch.zeros([1, model.model_config.latent_channels, height // 8, width // 8])
    latent = comfy.utils.common_upscale(latent, width // 8, height // 8, "nearest-exact", "disabled")

    # Generate noise
    noise = torch.randn_like(latent)

    # Sample
    print(f"  Sampling {width}x{height} seed={seed}...")
    samples = comfy.sample.sample(
        model=model,
        noise=noise,
        steps=4,
        cfg=1.0,
        sampler_name="euler",
        scheduler="simple",
        positive=positive,
        negative=negative,
        latent_image=latent,
        denoise=1.0,
        seed=seed,
    )

    # Decode VAE
    pixels = vae.decode(samples)

    # Convert to PIL
    pixel = pixels[0].cpu().numpy()
    pixel = ((pixel + 1.0) * 127.5).clip(0, 255).astype(np.uint8)
    pixel = pixel.transpose(1, 2, 0)
    return Image.fromarray(pixel)


def process(json_path, output_dir, seed=None):
    json_path = Path(json_path)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"Case: {json_path}")
    print(f"Out:  {output_dir}\n")

    with open(json_path, "r", encoding="utf-8") as f:
        case = json.load(f)

    if not verify():
        print("\nERROR: Missing models!")
        return

    model, vae, clip = load()

    n = 0

    # Featured (16:9)
    if case.get("featuredImagePrompt"):
        stem = case.get("featuredImageStem", "featured_00001_").rstrip("_")
        s = seed if seed is not None else random.randint(0, 2**63 - 1)
        print(f"\nFeatured: {stem}")
        img = generate(case["featuredImagePrompt"], model, vae, clip, *DIMENSIONS["featured"], s)
        img.save(output_dir / f"{stem}.png")
        n += 1

    # Suspects
    for i, sus in enumerate(case.get("suspects", []), 1):
        if sus.get("portraitPrompt"):
            stem = sus.get("portraitStem", f"suspect_{i:05d}_").rstrip("_")
            name = sus.get("displayName", f"Suspect {i}")
            s = seed if seed is not None else random.randint(0, 2**63 - 1)
            print(f"\nSuspect {i}: {name}")
            img = generate(sus["portraitPrompt"], model, vae, clip, *DIMENSIONS["suspect"], s)
            img.save(output_dir / f"{stem}.png")
            n += 1

    # Evidence
    for i, ev in enumerate(case.get("evidence", []), 1):
        if ev.get("imagePrompt"):
            stem = ev.get("imageStem", f"evidence_{i:05d}_").rstrip("_")
            title = ev.get("title", f"Evidence {i}")
            s = seed if seed is not None else random.randint(0, 2**63 - 1)
            print(f"\nEvidence {i}: {title}")
            img = generate(ev["imagePrompt"], model, vae, clip, *DIMENSIONS["evidence"], s)
            img.save(output_dir / f"{stem}.png")
            n += 1

    print(f"\nDone! {n} images -> {output_dir}")


if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("--json", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--seed", type=int, default=None)
    args = p.parse_args()
    process(args.json, args.output, seed=args.seed)
