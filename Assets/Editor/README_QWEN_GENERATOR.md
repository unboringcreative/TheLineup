# Qwen Case Generator

Generates images for The Lineup cases using ComfyUI's Qwen 2.5 Image model.

## Models (Verified Paths)

| Model | Path |
|-------|------|
| UNet | `E:\models\diffusion_models\qwen_image_2512_fp8_e4m3fn.safetensors` |
| VAE | `E:\models\vae\qwen_image_vae.safetensors` |
| CLIP | `E:\models\clip\qwen_2.5_vl_7b_fp8_scaled.safetensors` |
| LoRA | `E:\models\Lora\Qwen-Image-2512-Lightning-4steps-V1.0-fp32.safetensors` |

## Image Dimensions

| Type | Size | Ratio |
|------|------|-------|
| Suspects | 928×1328 | Portrait |
| Evidence | 1328×1328 | Square |
| Featured | 1664×936 | 16:9 |

## Usage

### From Command Line
```batch
generate_case.bat path\to\case.json output_dir [seed]
```

### From Unity
- `Tools > The Lineup > Generate Current Case Images`
- `Tools > The Lineup > Generate Case Images (Qwen 2.5)`

### Direct Python
```batch
C:\SwarmUI\SwarmUI\dlbackend\comfy\python_embeded\python.exe QwenCaseGenerator.py --json case.json --output ./out
```
