import json
import os
import random
import time
from datetime import datetime
from pathlib import Path
from typing import Any

import gradio as gr
import torch
from diffusers import Flux2KleinPipeline, Flux2Pipeline, FlowMatchEulerDiscreteScheduler, QwenImagePipeline

from paths import OUTPUTS_DIR, QWEN_LORA_DIR as DEFAULT_QWEN_LORA_DIR, model_path


DEFAULT_OUTPUT_DIR = str(OUTPUTS_DIR / "gradio")

FLUX2_KLEIN_MODEL_DIR = model_path("FLUX.2-klein-9B")
FLUX2_TURBO_BASE_MODEL_DIR = model_path("FLUX.2-dev")
FLUX2_TURBO_LORA_REPO_DIR = model_path("FLUX.2-dev-Turbo")
FLUX2_TURBO_LORA_NAME = "flux.2-turbo-lora.safetensors"

QWEN_MODEL_DIR = model_path("Qwen-Image-diffusers")
QWEN_LORA_DIR = str(DEFAULT_QWEN_LORA_DIR)
QWEN_LORA_NAME = "Qwen-Image-2512-Lightning-4steps-V1.0-fp32.safetensors"

TURBO_SIGMAS = [1.0, 0.6509, 0.4374, 0.2932, 0.1893, 0.1108, 0.0495, 0.00031]


PIPELINE_CACHE: dict[str, Any] = {
    "pipe": None,
    "key": None,
}


def _torch_device(requested: str) -> str:
    if requested == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    if requested == "cuda" and not torch.cuda.is_available():
        raise RuntimeError("CUDA selected, but CUDA is not available.")
    return requested


def _torch_dtype(dtype_name: str, device: str) -> torch.dtype:
    if dtype_name == "auto":
        if device == "cuda":
            if torch.cuda.is_bf16_supported():
                return torch.bfloat16
            return torch.float16
        return torch.float32

    mapping = {
        "bfloat16": torch.bfloat16,
        "float16": torch.float16,
        "float32": torch.float32,
    }
    if dtype_name not in mapping:
        raise ValueError(f"Unsupported dtype: {dtype_name}")
    return mapping[dtype_name]


def _parse_sigmas(sigmas_text: str) -> list[float] | None:
    if not sigmas_text or not sigmas_text.strip():
        return None
    parts = [p.strip() for p in sigmas_text.split(",") if p.strip()]
    return [float(p) for p in parts]


def _parse_layers(layer_text: str, default_layers: tuple[int, ...]) -> tuple[int, ...]:
    if not layer_text or not layer_text.strip():
        return default_layers
    parts = [p.strip() for p in layer_text.split(",") if p.strip()]
    values = tuple(int(p) for p in parts)
    if not values:
        raise ValueError("Layer list cannot be empty.")
    return values


def _save_outputs(images: list, output_dir: str, stem: str) -> list[str]:
    out = Path(output_dir)
    out.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    paths = []
    for i, image in enumerate(images, start=1):
        path = out / f"{stem}_{ts}_{i:04d}.png"
        image.save(path)
        paths.append(str(path))
    return paths


def _apply_runtime_toggles(pipe, offload_mode: str, device: str, tf32: bool, vae_slicing: bool, vae_tiling: bool, attention_slicing: bool, channels_last: bool):
    if tf32 and torch.cuda.is_available():
        torch.backends.cuda.matmul.allow_tf32 = True
        torch.backends.cudnn.allow_tf32 = True

    if offload_mode == "model_cpu_offload":
        pipe.enable_model_cpu_offload()
    elif offload_mode == "sequential_cpu_offload":
        pipe.enable_sequential_cpu_offload()
    else:
        pipe = pipe.to(device)

    if vae_slicing:
        try:
            pipe.vae.enable_slicing()
        except Exception:
            pass

    if vae_tiling:
        try:
            pipe.vae.enable_tiling()
        except Exception:
            pass

    if attention_slicing:
        try:
            pipe.enable_attention_slicing()
        except Exception:
            pass

    if channels_last:
        try:
            pipe.transformer.to(memory_format=torch.channels_last)
        except Exception:
            pass
        try:
            pipe.vae.to(memory_format=torch.channels_last)
        except Exception:
            pass

    return pipe


def _load_flux2_klein_pipeline(model_dir: str, device: str, dtype: torch.dtype, offload_mode: str, tf32: bool, vae_slicing: bool, vae_tiling: bool, attention_slicing: bool, channels_last: bool):
    if not os.path.isfile(os.path.join(model_dir, "model_index.json")):
        raise FileNotFoundError(f"model_index.json not found: {model_dir}")

    pipe = Flux2KleinPipeline.from_pretrained(
        model_dir,
        torch_dtype=dtype,
        local_files_only=True,
    )
    return _apply_runtime_toggles(pipe, offload_mode, device, tf32, vae_slicing, vae_tiling, attention_slicing, channels_last)


def _load_flux2_turbo_pipeline(base_model_dir: str, turbo_lora_repo_dir: str, turbo_lora_name: str, device: str, dtype: torch.dtype, offload_mode: str, tf32: bool, vae_slicing: bool, vae_tiling: bool, attention_slicing: bool, channels_last: bool):
    if not os.path.isfile(os.path.join(base_model_dir, "model_index.json")):
        raise FileNotFoundError(f"Base model_index.json not found: {base_model_dir}")
    lora_path = os.path.join(turbo_lora_repo_dir, turbo_lora_name)
    if not os.path.isfile(lora_path):
        raise FileNotFoundError(f"Turbo LoRA not found: {lora_path}")

    pipe = Flux2Pipeline.from_pretrained(
        base_model_dir,
        torch_dtype=dtype,
        local_files_only=True,
    )
    pipe.load_lora_weights(
        turbo_lora_repo_dir,
        weight_name=turbo_lora_name,
        local_files_only=True,
    )
    return _apply_runtime_toggles(pipe, offload_mode, device, tf32, vae_slicing, vae_tiling, attention_slicing, channels_last)


def _build_qwen_scheduler(use_lightning_scheduler: bool):
    if not use_lightning_scheduler:
        return None

    scheduler_config = {
        "base_image_seq_len": 256,
        "base_shift": 1.0986122886681098,
        "invert_sigmas": False,
        "max_image_seq_len": 8192,
        "max_shift": 1.0986122886681098,
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


def _load_qwen_pipeline(model_dir: str, lora_dir: str, lora_name: str, load_lora: bool, use_lightning_scheduler: bool, device: str, dtype: torch.dtype, offload_mode: str, tf32: bool, vae_slicing: bool, vae_tiling: bool, attention_slicing: bool, channels_last: bool):
    if not os.path.isfile(os.path.join(model_dir, "model_index.json")):
        raise FileNotFoundError(f"model_index.json not found: {model_dir}")

    scheduler = _build_qwen_scheduler(use_lightning_scheduler)

    pipe = QwenImagePipeline.from_pretrained(
        model_dir,
        scheduler=scheduler,
        torch_dtype=dtype,
        local_files_only=True,
    )

    if load_lora:
        lora_path = os.path.join(lora_dir, lora_name)
        if not os.path.isfile(lora_path):
            raise FileNotFoundError(f"Qwen LoRA not found: {lora_path}")
        pipe.load_lora_weights(
            lora_dir,
            weight_name=lora_name,
            adapter_name="qwen_lightning_4step",
            local_files_only=True,
        )
        try:
            pipe.set_adapters(["qwen_lightning_4step"], adapter_weights=[1.0])
        except Exception:
            pass

    return _apply_runtime_toggles(pipe, offload_mode, device, tf32, vae_slicing, vae_tiling, attention_slicing, channels_last)


def _get_pipeline(kind: str, settings: dict[str, Any]):
    key = json.dumps({"kind": kind, **settings}, sort_keys=True, default=str)
    if PIPELINE_CACHE["pipe"] is not None and PIPELINE_CACHE["key"] == key:
        return PIPELINE_CACHE["pipe"], "Reused cached pipeline"

    t0 = time.time()
    if kind == "flux2_klein":
        pipe = _load_flux2_klein_pipeline(**settings)
    elif kind == "flux2_turbo":
        pipe = _load_flux2_turbo_pipeline(**settings)
    elif kind == "qwen":
        pipe = _load_qwen_pipeline(**settings)
    else:
        raise ValueError(f"Unknown pipeline kind: {kind}")

    PIPELINE_CACHE["pipe"] = pipe
    PIPELINE_CACHE["key"] = key
    return pipe, f"Loaded pipeline in {time.time() - t0:.2f}s"


def _build_generator(seed: int, randomize_seed: bool, device: str):
    use_seed = random.randint(0, 2**31 - 1) if randomize_seed else int(seed)
    gen_device = "cuda" if device == "cuda" else "cpu"
    generator = torch.Generator(device=gen_device).manual_seed(use_seed)
    return generator, use_seed


def _info_json(base: dict[str, Any]) -> str:
    return json.dumps(base, indent=2)


def generate_flux2_klein(
    model_dir,
    prompt,
    init_image,
    width,
    height,
    steps,
    guidance,
    max_seq_len,
    layers_text,
    sigmas_text,
    num_images,
    seed,
    randomize_seed,
    device_choice,
    dtype_choice,
    offload_mode,
    tf32,
    vae_slicing,
    vae_tiling,
    attention_slicing,
    channels_last,
    save_images,
    output_dir,
    output_stem,
    progress=gr.Progress(track_tqdm=True),
):
    if not prompt or not prompt.strip():
        raise gr.Error("Prompt is required.")

    device = _torch_device(device_choice)
    dtype = _torch_dtype(dtype_choice, device)
    layers = _parse_layers(layers_text, (9, 18, 27))
    sigmas = _parse_sigmas(sigmas_text)

    settings = {
        "model_dir": model_dir,
        "device": device,
        "dtype": dtype,
        "offload_mode": offload_mode,
        "tf32": tf32,
        "vae_slicing": vae_slicing,
        "vae_tiling": vae_tiling,
        "attention_slicing": attention_slicing,
        "channels_last": channels_last,
    }

    progress(0.02, desc="Loading FLUX2 Klein pipeline")
    pipe, load_status = _get_pipeline("flux2_klein", settings)

    generator, use_seed = _build_generator(seed, randomize_seed, device)
    kwargs = {
        "prompt": prompt,
        "width": int(width),
        "height": int(height),
        "num_inference_steps": int(steps),
        "guidance_scale": float(guidance),
        "num_images_per_prompt": int(num_images),
        "generator": generator,
        "max_sequence_length": int(max_seq_len),
        "text_encoder_out_layers": layers,
    }
    if init_image is not None:
        kwargs["image"] = init_image
    if sigmas is not None:
        kwargs["sigmas"] = sigmas

    if device == "cuda":
        torch.cuda.reset_peak_memory_stats()

    progress(0.15, desc="Generating")
    t0 = time.time()
    with torch.inference_mode():
        result = pipe(**kwargs)
    elapsed = time.time() - t0

    saved_paths = _save_outputs(result.images, output_dir, output_stem or "flux2_klein") if save_images else []
    info = {
        "pipeline": "flux2_klein",
        "status": "ok",
        "cache": load_status,
        "device": device,
        "dtype": str(dtype),
        "seed": use_seed,
        "elapsed_seconds": round(elapsed, 3),
        "steps": int(steps),
        "guidance_scale": float(guidance),
        "sigmas": sigmas,
        "text_encoder_out_layers": layers,
        "saved_paths": saved_paths,
    }
    if device == "cuda":
        info["cuda_max_reserved_gb"] = round(torch.cuda.max_memory_reserved() / (1024**3), 3)
        info["cuda_max_allocated_gb"] = round(torch.cuda.max_memory_allocated() / (1024**3), 3)

    progress(1.0, desc="Done")
    return result.images, _info_json(info)


def generate_flux2_turbo(
    base_model_dir,
    turbo_lora_repo_dir,
    turbo_lora_name,
    prompt,
    init_image,
    width,
    height,
    steps,
    guidance,
    max_seq_len,
    layers_text,
    use_turbo_sigmas,
    sigmas_text,
    num_images,
    seed,
    randomize_seed,
    device_choice,
    dtype_choice,
    offload_mode,
    tf32,
    vae_slicing,
    vae_tiling,
    attention_slicing,
    channels_last,
    save_images,
    output_dir,
    output_stem,
    progress=gr.Progress(track_tqdm=True),
):
    if not prompt or not prompt.strip():
        raise gr.Error("Prompt is required.")

    device = _torch_device(device_choice)
    dtype = _torch_dtype(dtype_choice, device)
    layers = _parse_layers(layers_text, (10, 20, 30))
    sigmas = TURBO_SIGMAS if use_turbo_sigmas else _parse_sigmas(sigmas_text)

    settings = {
        "base_model_dir": base_model_dir,
        "turbo_lora_repo_dir": turbo_lora_repo_dir,
        "turbo_lora_name": turbo_lora_name,
        "device": device,
        "dtype": dtype,
        "offload_mode": offload_mode,
        "tf32": tf32,
        "vae_slicing": vae_slicing,
        "vae_tiling": vae_tiling,
        "attention_slicing": attention_slicing,
        "channels_last": channels_last,
    }

    progress(0.02, desc="Loading FLUX2 Turbo pipeline")
    pipe, load_status = _get_pipeline("flux2_turbo", settings)

    generator, use_seed = _build_generator(seed, randomize_seed, device)
    kwargs = {
        "prompt": prompt,
        "width": int(width),
        "height": int(height),
        "num_inference_steps": int(steps),
        "guidance_scale": float(guidance),
        "num_images_per_prompt": int(num_images),
        "generator": generator,
        "max_sequence_length": int(max_seq_len),
        "text_encoder_out_layers": layers,
    }
    if init_image is not None:
        kwargs["image"] = init_image
    if sigmas is not None:
        kwargs["sigmas"] = sigmas

    if device == "cuda":
        torch.cuda.reset_peak_memory_stats()

    progress(0.15, desc="Generating")
    t0 = time.time()
    with torch.inference_mode():
        result = pipe(**kwargs)
    elapsed = time.time() - t0

    saved_paths = _save_outputs(result.images, output_dir, output_stem or "flux2_turbo") if save_images else []
    info = {
        "pipeline": "flux2_turbo",
        "status": "ok",
        "cache": load_status,
        "device": device,
        "dtype": str(dtype),
        "seed": use_seed,
        "elapsed_seconds": round(elapsed, 3),
        "steps": int(steps),
        "guidance_scale": float(guidance),
        "sigmas": sigmas,
        "text_encoder_out_layers": layers,
        "saved_paths": saved_paths,
    }
    if device == "cuda":
        info["cuda_max_reserved_gb"] = round(torch.cuda.max_memory_reserved() / (1024**3), 3)
        info["cuda_max_allocated_gb"] = round(torch.cuda.max_memory_allocated() / (1024**3), 3)

    progress(1.0, desc="Done")
    return result.images, _info_json(info)


def generate_qwen(
    model_dir,
    prompt,
    negative_prompt,
    width,
    height,
    steps,
    true_cfg_scale,
    max_seq_len,
    use_lightning_scheduler,
    load_lora,
    lora_dir,
    lora_name,
    sigmas_text,
    num_images,
    seed,
    randomize_seed,
    device_choice,
    dtype_choice,
    offload_mode,
    tf32,
    vae_slicing,
    vae_tiling,
    attention_slicing,
    channels_last,
    save_images,
    output_dir,
    output_stem,
    progress=gr.Progress(track_tqdm=True),
):
    if not prompt or not prompt.strip():
        raise gr.Error("Prompt is required.")

    device = _torch_device(device_choice)
    dtype = _torch_dtype(dtype_choice, device)
    sigmas = _parse_sigmas(sigmas_text)

    settings = {
        "model_dir": model_dir,
        "lora_dir": lora_dir,
        "lora_name": lora_name,
        "load_lora": load_lora,
        "use_lightning_scheduler": use_lightning_scheduler,
        "device": device,
        "dtype": dtype,
        "offload_mode": offload_mode,
        "tf32": tf32,
        "vae_slicing": vae_slicing,
        "vae_tiling": vae_tiling,
        "attention_slicing": attention_slicing,
        "channels_last": channels_last,
    }

    progress(0.02, desc="Loading Qwen pipeline")
    pipe, load_status = _get_pipeline("qwen", settings)

    generator, use_seed = _build_generator(seed, randomize_seed, device)
    kwargs = {
        "prompt": prompt,
        "negative_prompt": negative_prompt if negative_prompt is not None else " ",
        "width": int(width),
        "height": int(height),
        "num_inference_steps": int(steps),
        "true_cfg_scale": float(true_cfg_scale),
        "num_images_per_prompt": int(num_images),
        "generator": generator,
        "max_sequence_length": int(max_seq_len),
    }
    if sigmas is not None:
        kwargs["sigmas"] = sigmas

    if device == "cuda":
        torch.cuda.reset_peak_memory_stats()

    progress(0.15, desc="Generating")
    t0 = time.time()
    with torch.inference_mode():
        result = pipe(**kwargs)
    elapsed = time.time() - t0

    saved_paths = _save_outputs(result.images, output_dir, output_stem or "qwen") if save_images else []
    info = {
        "pipeline": "qwen",
        "status": "ok",
        "cache": load_status,
        "device": device,
        "dtype": str(dtype),
        "seed": use_seed,
        "elapsed_seconds": round(elapsed, 3),
        "steps": int(steps),
        "true_cfg_scale": float(true_cfg_scale),
        "sigmas": sigmas,
        "load_lora": load_lora,
        "use_lightning_scheduler": use_lightning_scheduler,
        "saved_paths": saved_paths,
    }
    if device == "cuda":
        info["cuda_max_reserved_gb"] = round(torch.cuda.max_memory_reserved() / (1024**3), 3)
        info["cuda_max_allocated_gb"] = round(torch.cuda.max_memory_allocated() / (1024**3), 3)

    progress(1.0, desc="Done")
    return result.images, _info_json(info)


def clear_cache():
    PIPELINE_CACHE["pipe"] = None
    PIPELINE_CACHE["key"] = None
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    return "Pipeline cache cleared."


with gr.Blocks(title="Local Diffusion Studio") as demo:
    gr.Markdown("# Local Diffusion Studio")
    gr.Markdown("FLUX2 Klein, FLUX2 Turbo, and Qwen in one UI.")

    with gr.Row():
        clear_btn = gr.Button("Clear Pipeline Cache")
        cache_status = gr.Textbox(label="Cache", interactive=False)
    clear_btn.click(fn=clear_cache, inputs=[], outputs=[cache_status])

    with gr.Tabs():
        with gr.Tab("FLUX2 Klein"):
            with gr.Row():
                with gr.Column(scale=3):
                    fk_model_dir = gr.Textbox(label="Model Directory", value=FLUX2_KLEIN_MODEL_DIR)
                    fk_prompt = gr.Textbox(label="Prompt", lines=8)
                    fk_init = gr.Image(label="Init Image (optional)", type="pil")
                    with gr.Row():
                        fk_w = gr.Slider(label="Width", minimum=256, maximum=2048, value=1024, step=64)
                        fk_h = gr.Slider(label="Height", minimum=256, maximum=2048, value=1024, step=64)
                    with gr.Row():
                        fk_steps = gr.Slider(label="Steps", minimum=1, maximum=80, value=4, step=1)
                        fk_guidance = gr.Slider(label="Guidance", minimum=0.0, maximum=20.0, value=1.0, step=0.1)
                        fk_images = gr.Slider(label="Images", minimum=1, maximum=8, value=1, step=1)
                    with gr.Row():
                        fk_seed = gr.Number(label="Seed", value=123456, precision=0)
                        fk_rand_seed = gr.Checkbox(label="Randomize Seed", value=False)
                    fk_max_seq = gr.Slider(label="Max Sequence Length", minimum=128, maximum=1024, value=512, step=32)
                    fk_layers = gr.Textbox(label="Text Encoder Out Layers", value="9,18,27")
                    fk_sigmas = gr.Textbox(label="Custom Sigmas (optional)", value="")
                    with gr.Accordion("Runtime", open=False):
                        fk_device = gr.Dropdown(label="Device", choices=["auto", "cuda", "cpu"], value="auto")
                        fk_dtype = gr.Dropdown(label="Dtype", choices=["auto", "bfloat16", "float16", "float32"], value="auto")
                        fk_offload = gr.Dropdown(label="Offload", choices=["model_cpu_offload", "sequential_cpu_offload", "none"], value="model_cpu_offload")
                        fk_tf32 = gr.Checkbox(label="TF32", value=True)
                        fk_vae_slicing = gr.Checkbox(label="VAE Slicing", value=True)
                        fk_vae_tiling = gr.Checkbox(label="VAE Tiling", value=True)
                        fk_attn = gr.Checkbox(label="Attention Slicing", value=False)
                        fk_channels = gr.Checkbox(label="channels_last", value=False)
                    with gr.Accordion("Output", open=False):
                        fk_save = gr.Checkbox(label="Save Images", value=True)
                        fk_out_dir = gr.Textbox(label="Output Directory", value=DEFAULT_OUTPUT_DIR)
                        fk_stem = gr.Textbox(label="Filename Stem", value="flux2_klein")
                    fk_go = gr.Button("Generate", variant="primary")
                with gr.Column(scale=2):
                    fk_gallery = gr.Gallery(label="Images", columns=2, rows=2, height=760)
                    fk_info = gr.Code(label="Run Info", language="json")

            fk_go.click(
                fn=generate_flux2_klein,
                inputs=[
                    fk_model_dir, fk_prompt, fk_init, fk_w, fk_h, fk_steps, fk_guidance, fk_max_seq,
                    fk_layers, fk_sigmas, fk_images, fk_seed, fk_rand_seed, fk_device, fk_dtype,
                    fk_offload, fk_tf32, fk_vae_slicing, fk_vae_tiling, fk_attn, fk_channels,
                    fk_save, fk_out_dir, fk_stem,
                ],
                outputs=[fk_gallery, fk_info],
            )

        with gr.Tab("FLUX2 Turbo"):
            with gr.Row():
                with gr.Column(scale=3):
                    ft_base_model_dir = gr.Textbox(label="Base Model Directory", value=FLUX2_TURBO_BASE_MODEL_DIR)
                    ft_lora_repo_dir = gr.Textbox(label="Turbo LoRA Repo Directory", value=FLUX2_TURBO_LORA_REPO_DIR)
                    ft_lora_name = gr.Textbox(label="Turbo LoRA Filename", value=FLUX2_TURBO_LORA_NAME)
                    ft_prompt = gr.Textbox(label="Prompt", lines=8)
                    ft_init = gr.Image(label="Init Image (optional)", type="pil")
                    with gr.Row():
                        ft_w = gr.Slider(label="Width", minimum=256, maximum=2048, value=1024, step=64)
                        ft_h = gr.Slider(label="Height", minimum=256, maximum=2048, value=1024, step=64)
                    with gr.Row():
                        ft_steps = gr.Slider(label="Steps", minimum=1, maximum=80, value=8, step=1)
                        ft_guidance = gr.Slider(label="Guidance", minimum=0.0, maximum=20.0, value=2.5, step=0.1)
                        ft_images = gr.Slider(label="Images", minimum=1, maximum=8, value=1, step=1)
                    with gr.Row():
                        ft_seed = gr.Number(label="Seed", value=123456, precision=0)
                        ft_rand_seed = gr.Checkbox(label="Randomize Seed", value=False)
                    ft_max_seq = gr.Slider(label="Max Sequence Length", minimum=128, maximum=1024, value=512, step=32)
                    ft_layers = gr.Textbox(label="Text Encoder Out Layers", value="10,20,30")
                    ft_use_turbo_sigmas = gr.Checkbox(label="Use Recommended Turbo Sigmas", value=True)
                    ft_sigmas = gr.Textbox(label="Custom Sigmas (used when turbo sigmas off)", value="")
                    with gr.Accordion("Runtime", open=False):
                        ft_device = gr.Dropdown(label="Device", choices=["auto", "cuda", "cpu"], value="auto")
                        ft_dtype = gr.Dropdown(label="Dtype", choices=["auto", "bfloat16", "float16", "float32"], value="auto")
                        ft_offload = gr.Dropdown(label="Offload", choices=["model_cpu_offload", "sequential_cpu_offload", "none"], value="model_cpu_offload")
                        ft_tf32 = gr.Checkbox(label="TF32", value=True)
                        ft_vae_slicing = gr.Checkbox(label="VAE Slicing", value=True)
                        ft_vae_tiling = gr.Checkbox(label="VAE Tiling", value=True)
                        ft_attn = gr.Checkbox(label="Attention Slicing", value=False)
                        ft_channels = gr.Checkbox(label="channels_last", value=False)
                    with gr.Accordion("Output", open=False):
                        ft_save = gr.Checkbox(label="Save Images", value=True)
                        ft_out_dir = gr.Textbox(label="Output Directory", value=DEFAULT_OUTPUT_DIR)
                        ft_stem = gr.Textbox(label="Filename Stem", value="flux2_turbo")
                    ft_go = gr.Button("Generate", variant="primary")
                with gr.Column(scale=2):
                    ft_gallery = gr.Gallery(label="Images", columns=2, rows=2, height=760)
                    ft_info = gr.Code(label="Run Info", language="json")

            ft_go.click(
                fn=generate_flux2_turbo,
                inputs=[
                    ft_base_model_dir, ft_lora_repo_dir, ft_lora_name, ft_prompt, ft_init, ft_w, ft_h,
                    ft_steps, ft_guidance, ft_max_seq, ft_layers, ft_use_turbo_sigmas, ft_sigmas, ft_images,
                    ft_seed, ft_rand_seed, ft_device, ft_dtype, ft_offload, ft_tf32, ft_vae_slicing,
                    ft_vae_tiling, ft_attn, ft_channels, ft_save, ft_out_dir, ft_stem,
                ],
                outputs=[ft_gallery, ft_info],
            )

        with gr.Tab("Qwen"):
            with gr.Row():
                with gr.Column(scale=3):
                    q_model_dir = gr.Textbox(label="Model Directory", value=QWEN_MODEL_DIR)
                    q_prompt = gr.Textbox(label="Prompt", lines=8)
                    q_negative = gr.Textbox(label="Negative Prompt", lines=3, value=" ")
                    with gr.Row():
                        q_w = gr.Slider(label="Width", minimum=256, maximum=2048, value=928, step=64)
                        q_h = gr.Slider(label="Height", minimum=256, maximum=2048, value=1664, step=64)
                    with gr.Row():
                        q_steps = gr.Slider(label="Steps", minimum=1, maximum=80, value=4, step=1)
                        q_true_cfg = gr.Slider(label="True CFG Scale", minimum=0.0, maximum=20.0, value=1.0, step=0.1)
                        q_images = gr.Slider(label="Images", minimum=1, maximum=8, value=1, step=1)
                    with gr.Row():
                        q_seed = gr.Number(label="Seed", value=123456, precision=0)
                        q_rand_seed = gr.Checkbox(label="Randomize Seed", value=False)
                    q_max_seq = gr.Slider(label="Max Sequence Length", minimum=128, maximum=1024, value=512, step=32)
                    q_sigmas = gr.Textbox(label="Custom Sigmas (optional)", value="")
                    q_use_lightning_scheduler = gr.Checkbox(label="Use Lightning-style Scheduler", value=True)
                    q_load_lora = gr.Checkbox(label="Load Qwen 4-step LoRA", value=True)
                    q_lora_dir = gr.Textbox(label="Qwen LoRA Directory", value=QWEN_LORA_DIR)
                    q_lora_name = gr.Textbox(label="Qwen LoRA Filename", value=QWEN_LORA_NAME)
                    with gr.Accordion("Runtime", open=False):
                        q_device = gr.Dropdown(label="Device", choices=["auto", "cuda", "cpu"], value="auto")
                        q_dtype = gr.Dropdown(label="Dtype", choices=["auto", "bfloat16", "float16", "float32"], value="auto")
                        q_offload = gr.Dropdown(label="Offload", choices=["model_cpu_offload", "sequential_cpu_offload", "none"], value="model_cpu_offload")
                        q_tf32 = gr.Checkbox(label="TF32", value=True)
                        q_vae_slicing = gr.Checkbox(label="VAE Slicing", value=True)
                        q_vae_tiling = gr.Checkbox(label="VAE Tiling", value=True)
                        q_attn = gr.Checkbox(label="Attention Slicing", value=False)
                        q_channels = gr.Checkbox(label="channels_last", value=False)
                    with gr.Accordion("Output", open=False):
                        q_save = gr.Checkbox(label="Save Images", value=True)
                        q_out_dir = gr.Textbox(label="Output Directory", value=DEFAULT_OUTPUT_DIR)
                        q_stem = gr.Textbox(label="Filename Stem", value="qwen")
                    q_go = gr.Button("Generate", variant="primary")
                with gr.Column(scale=2):
                    q_gallery = gr.Gallery(label="Images", columns=2, rows=2, height=760)
                    q_info = gr.Code(label="Run Info", language="json")

            q_go.click(
                fn=generate_qwen,
                inputs=[
                    q_model_dir, q_prompt, q_negative, q_w, q_h, q_steps, q_true_cfg, q_max_seq,
                    q_use_lightning_scheduler, q_load_lora, q_lora_dir, q_lora_name, q_sigmas, q_images,
                    q_seed, q_rand_seed, q_device, q_dtype, q_offload, q_tf32, q_vae_slicing,
                    q_vae_tiling, q_attn, q_channels, q_save, q_out_dir, q_stem,
                ],
                outputs=[q_gallery, q_info],
            )


if __name__ == "__main__":
    demo.queue(default_concurrency_limit=1)
    demo.launch(server_name="127.0.0.1", server_port=7860, show_error=True)
