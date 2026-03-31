import json
import shutil
from pathlib import Path

from gradio_flux2_studio import (
    FLUX2_TURBO_BASE_MODEL_DIR,
    FLUX2_TURBO_LORA_NAME,
    FLUX2_TURBO_LORA_REPO_DIR,
    generate_flux2_turbo,
)
from paths import OUTPUTS_DIR
from prompt_builder import build_render_queue, load_json, write_prompt_manifest


def main() -> None:
    case_path = Path("cases/case_008_the_last_lantern.json")
    style_path = Path("styles/rainlit_detective_pulp_v01.json")

    case = load_json(case_path)
    style = load_json(style_path)
    queue = build_render_queue(case, style, preset="final")

    out_dir = OUTPUTS_DIR / case["caseId"] / style["styleId"]
    out_dir.mkdir(parents=True, exist_ok=True)

    shutil.copy2(case_path, out_dir / "case.json")
    shutil.copy2(style_path, out_dir / "style.json")
    write_prompt_manifest(out_dir / "prompt_manifest.json", case, style, queue, preset="final")

    results = []
    for idx, item in enumerate(queue):
        seed = 880000 + idx
        _, info_json = generate_flux2_turbo(
            base_model_dir=FLUX2_TURBO_BASE_MODEL_DIR,
            turbo_lora_repo_dir=FLUX2_TURBO_LORA_REPO_DIR,
            turbo_lora_name=FLUX2_TURBO_LORA_NAME,
            prompt=item["prompt"],
            init_image=None,
            width=item["width"],
            height=item["height"],
            steps=8,
            guidance=2.5,
            max_seq_len=512,
            layers_text="10,20,30",
            use_turbo_sigmas=True,
            sigmas_text="",
            num_images=1,
            seed=seed,
            randomize_seed=False,
            device_choice="auto",
            dtype_choice="auto",
            offload_mode="model_cpu_offload",
            tf32=True,
            vae_slicing=True,
            vae_tiling=True,
            attention_slicing=False,
            channels_last=False,
            save_images=True,
            output_dir=str(out_dir),
            output_stem=item["stem"],
        )
        info = json.loads(info_json)
        info["kind"] = item["kind"]
        info["slot"] = item["slot"]
        info["title"] = item["title"]
        results.append(info)
        print(f"Rendered {item['kind']} slot {item['slot']}")

    (out_dir / "render_manifest_turbo.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"Done: {out_dir}")


if __name__ == "__main__":
    main()
