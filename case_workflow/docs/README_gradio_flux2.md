# Local Diffusion Studio

## Install

```bash
cd D:\LIVE PROJECTS\TheLineup\The Lineup\case_workflow
.venv\Scripts\python.exe -m pip install gradio
```

If needed, also install core deps:

```bash
cd D:\LIVE PROJECTS\TheLineup\The Lineup\case_workflow
.venv\Scripts\python.exe -m pip install torch diffusers transformers accelerate safetensors pillow
```

## Run

```bash
cd D:\LIVE PROJECTS\TheLineup\The Lineup\case_workflow
.venv\Scripts\python.exe gradio_flux2_studio.py
```

Open `http://127.0.0.1:7860`.

## Models included in UI

- FLUX2 Klein (`<DIFFUSION_MODEL_ROOT>\FLUX.2-klein-9B`)
- FLUX2 Turbo (base + LoRA)
  - base: `<DIFFUSION_MODEL_ROOT>\FLUX.2-dev`
  - turbo LoRA: `<DIFFUSION_MODEL_ROOT>\FLUX.2-dev-Turbo\flux.2-turbo-lora.safetensors`
- Qwen (`<DIFFUSION_MODEL_ROOT>\Qwen-Image-diffusers`) + optional 4-step LoRA from `<DIFFUSION_LORA_ROOT>`

Environment defaults used by scripts:

- `DIFFUSION_MODEL_ROOT=I:\models\case_workflow`
- `DIFFUSION_LORA_ROOT=I:\models\Lora`
- `CASE_OUTPUT_ROOT=D:\LIVE PROJECTS\TheLineup\The Lineup\case_workflow\outputs`

## FLUX2 Turbo download/setup

Already set up by download commands:

```bash
cd D:\LIVE PROJECTS\TheLineup\The Lineup\case_workflow
.venv\Scripts\python.exe download_flux2_turbo.py
```

## What it exposes per tab

- Prompt + optional init image
- Width/height
- Steps, guidance scale, images per prompt
- Seed and randomize seed
- Max sequence length
- Text encoder output layers
- Optional custom sigmas list
- Device, dtype, offload mode
- TF32, VAE slicing/tiling, attention slicing, channels_last
- Save toggle + output folder + filename stem
- Pipeline cache clear button

Qwen tab also exposes:

- Negative prompt
- True CFG scale
- Lightning-style scheduler toggle
- Load/skip Qwen 4-step LoRA toggle
