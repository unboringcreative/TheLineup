import argparse
import atexit
import json
import os
import shutil
import time
import traceback
from pathlib import Path

import torch
from diffusers import Flux2KleinPipeline

from case_validation import validate_case_data
from prompt_builder import load_json, build_render_queue, write_prompt_manifest
from paths import CASES_DIR, STYLES_DIR, OUTPUTS_DIR, UNITY_IMPORT_ROOT, model_path

try:
    from transparent_background import Remover
    INSPYRE_AVAILABLE = True
except Exception:
    Remover = None
    INSPYRE_AVAILABLE = False


INSPYRE_REMOVER = None


if torch.cuda.is_available():
    torch.backends.cuda.matmul.allow_tf32 = True
    torch.backends.cudnn.allow_tf32 = True


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def acquire_render_lock(output_dir: Path) -> Path:
    lock_path = output_dir / ".render.lock"
    payload = {
        "pid": os.getpid(),
        "started_unix": time.time(),
    }

    try:
        with lock_path.open("x", encoding="utf-8") as f:
            f.write(json.dumps(payload, indent=2))
    except FileExistsError:
        raise RuntimeError(
            f"Another render appears to be running for this output folder: {output_dir}\n"
            f"Lock file: {lock_path}"
        )

    def _cleanup_lock() -> None:
        try:
            if lock_path.exists():
                lock_path.unlink()
        except Exception:
            pass

    atexit.register(_cleanup_lock)
    return lock_path


def get_cuda_free_total_gb() -> tuple[float, float]:
    if not torch.cuda.is_available():
        return 0.0, 0.0

    try:
        free_bytes, total_bytes = torch.cuda.mem_get_info()
        gb = 1024.0 ** 3
        return free_bytes / gb, total_bytes / gb
    except Exception:
        return 0.0, 0.0


def default_case_path() -> Path:
    candidates = sorted(CASES_DIR.glob("*.json"))
    if not candidates:
        raise FileNotFoundError(f"No case json found in {CASES_DIR}")

    return max(candidates, key=lambda p: p.stat().st_mtime)


def default_style_path() -> Path:
    preferred = STYLES_DIR / "comic_noir.json"
    if preferred.exists():
        return preferred

    candidates = sorted(STYLES_DIR.glob("*.json"))
    if candidates:
        return candidates[0]

    raise FileNotFoundError(f"No style json found in {STYLES_DIR}")


def write_unity_import_bundle(output_dir: Path, case_id: str) -> Path:
    unity_case_dir = UNITY_IMPORT_ROOT / case_id
    ensure_dir(unity_case_dir)

    for png in output_dir.glob("*.png"):
        shutil.copy2(png, unity_case_dir / png.name)

    for name in ("case.json", "style.json", "prompt_manifest.json", "render_manifest.json"):
        src = output_dir / name
        if src.exists():
            shutil.copy2(src, unity_case_dir / name)

    return unity_case_dir


def get_inspyre_remover(device: str):
    global INSPYRE_REMOVER
    if INSPYRE_REMOVER is not None:
        return INSPYRE_REMOVER

    if not INSPYRE_AVAILABLE or Remover is None:
        return None

    mode = "base"
    remover_device = "cuda" if device == "cuda" else "cpu"
    INSPYRE_REMOVER = Remover(mode=mode, jit=False, device=remover_device, resize="dynamic")
    return INSPYRE_REMOVER


def apply_background_removal_if_needed(image, should_remove_bg: bool, device: str):
    if not should_remove_bg:
        return image

    remover = get_inspyre_remover(device)
    if remover is None:
        return image

    try:
        result = remover.process(image, type="rgba")
        if hasattr(result, "convert"):
            return result.convert("RGBA")
        return image
    except Exception:
        return image


def load_pipeline(model_dir: str, dtype: torch.dtype, device: str, use_cpu_offload: bool) -> Flux2KleinPipeline:
    model_path_resolved = Path(model_dir)
    if not model_path_resolved.is_dir():
        raise FileNotFoundError(
            f"Model directory not found: {model_path_resolved}\n"
            f"Expected local model folder with model_index.json."
        )

    model_index = model_path_resolved / "model_index.json"
    if not model_index.exists():
        raise FileNotFoundError(
            f"Missing model_index.json in model directory: {model_index}"
        )

    pipe = Flux2KleinPipeline.from_pretrained(
        str(model_path_resolved),
        torch_dtype=dtype,
        local_files_only=True,
    )

    if use_cpu_offload:
        pipe.enable_model_cpu_offload()
    else:
        pipe = pipe.to(device)

    try:
        pipe.vae.enable_slicing()
    except Exception:
        pass
    try:
        pipe.vae.enable_tiling()
    except Exception:
        pass

    return pipe


def render_one(pipe: Flux2KleinPipeline, item: dict, output_dir: Path, seed: int, device: str, guidance_scale: float, num_steps: int) -> dict:
    generator = torch.Generator(device=device if device == "cuda" else "cpu").manual_seed(seed)

    start = time.time()
    with torch.inference_mode():
        result = pipe(
            prompt=item["prompt"],
            width=item["width"],
            height=item["height"],
            guidance_scale=guidance_scale,
            num_inference_steps=num_steps,
            generator=generator,
        )
    elapsed = time.time() - start

    image = result.images[0]
    image = apply_background_removal_if_needed(image, item.get("remove_bg", False), device)

    filename = f"{item['stem']}0001.png"
    out_path = output_dir / filename
    image.save(out_path)

    return {
        "kind": item["kind"],
        "slot": item["slot"],
        "title": item["title"],
        "output": str(out_path),
        "elapsed_seconds": elapsed,
        "seed": seed,
        "width": item["width"],
        "height": item["height"],
        "prompt": item["prompt"],
        "negative_prompt": item.get("negative_prompt", ""),
    }


def main() -> None:
    default_case = default_case_path()
    default_style = default_style_path()

    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default=model_path("FLUX.2-klein-9B"))
    parser.add_argument("--case", default=str(default_case))
    parser.add_argument("--style", default=str(default_style))
    parser.add_argument("--preset", default="final", choices=["debug", "final"])
    parser.add_argument("--guidance", type=float, default=1.0)
    parser.add_argument("--steps", type=int, default=4)
    parser.add_argument("--seed-base", type=int, default=123456)
    parser.add_argument("--bg-removal", choices=["inspyre", "none"], default="inspyre")
    parser.add_argument("--strict-validation", action="store_true")
    args = parser.parse_args()

    print(f"Using case: {args.case}")
    print(f"Using style: {args.style}")
    print(f"Using model: {args.model}")
    print(f"Output root: {OUTPUTS_DIR}")

    case = load_json(args.case)
    validation = validate_case_data(case, strict_quality=args.strict_validation)
    if validation.warnings:
        print("Case validation warnings:")
        for warning in validation.warnings:
            print(f"- {warning}")
    if not validation.ok:
        print("Case validation failed:")
        for error in validation.errors:
            print(f"- {error}")
        raise ValueError("Case JSON did not pass validation.")

    style = load_json(args.style)
    queue = build_render_queue(case, style, preset=args.preset)

    if args.bg_removal == "none":
        for item in queue:
            item["remove_bg"] = False

    case_id = case["caseId"]
    style_id = style["styleId"]
    output_dir = OUTPUTS_DIR / case_id / style_id
    ensure_dir(output_dir)
    acquire_render_lock(output_dir)

    shutil.copy2(args.case, output_dir / "case.json")
    shutil.copy2(args.style, output_dir / "style.json")
    write_prompt_manifest(output_dir / "prompt_manifest.json", case, style, queue, preset=args.preset)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.bfloat16 if torch.cuda.is_available() else torch.float32

    # Fastest proven path on this machine/project history.
    use_cpu_offload = True

    if device == "cuda":
        free_gb, total_gb = get_cuda_free_total_gb()
        print(f"CUDA memory free/total: {free_gb:.2f} GB / {total_gb:.2f} GB")
    print("Render mode: CPU offload")
    print(f"CPU offload enabled: {use_cpu_offload}")

    pipe = load_pipeline(args.model, dtype=dtype, device=device, use_cpu_offload=use_cpu_offload)

    results = []
    total_start = time.time()

    for idx, item in enumerate(queue):
        seed = args.seed_base + idx
        try:
            info = render_one(pipe, item, output_dir, seed, device, args.guidance, args.steps)
            results.append(info)
            print(f"Rendered {item['kind']} slot {item['slot']} in {info['elapsed_seconds']:.2f}s")
        except Exception as e:
            print(f"FAILED {item['kind']} slot {item['slot']}: {e}")
            results.append({"kind": item["kind"], "slot": item["slot"], "title": item["title"], "error": str(e)})

    total_elapsed = time.time() - total_start
    manifest = {
        "caseId": case_id,
        "styleId": style_id,
        "preset": args.preset,
        "guidance_scale": args.guidance,
        "num_steps": args.steps,
        "use_cpu_offload": use_cpu_offload,
        "device": device,
        "dtype": str(dtype),
        "total_elapsed_seconds": total_elapsed,
        "bg_removal": args.bg_removal,
        "inspyre_available": INSPYRE_AVAILABLE,
        "results": results,
    }
    (output_dir / "render_manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    unity_import_dir = write_unity_import_bundle(output_dir, case_id)

    print(f"Done. Output folder: {output_dir}")
    print(f"Unity import folder: {unity_import_dir}")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
