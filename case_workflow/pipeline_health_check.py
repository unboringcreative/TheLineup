from __future__ import annotations

import argparse
from pathlib import Path

from case_validation import validate_case_data
from paths import CASES_DIR, MODELS_DIR, OUTPUTS_DIR, STYLES_DIR, UNITY_IMPORT_ROOT, model_path
from prompt_builder import build_render_queue, load_json


def read_style(path: Path) -> dict:
    return load_json(path)


def list_render_locks() -> list[Path]:
    if not OUTPUTS_DIR.exists():
        return []
    return sorted(OUTPUTS_DIR.glob("**/.render.lock"))


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


def main() -> int:
    parser = argparse.ArgumentParser(description="Run a non-rendering pipeline health check.")
    parser.add_argument("--case", help="Optional case JSON path.")
    parser.add_argument("--style", help="Optional style JSON path.")
    parser.add_argument("--model", default=model_path("FLUX.2-klein-9B"), help="Model directory to verify.")
    parser.add_argument("--preset", default="final", choices=["debug", "final"])
    parser.add_argument("--strict", action="store_true", help="Use strict case validation.")
    args = parser.parse_args()

    case_path = Path(args.case) if args.case else default_case_path()
    style_path = Path(args.style) if args.style else default_style_path()
    model_dir = Path(args.model)

    print("Pipeline health check")
    print(f"- Cases dir: {CASES_DIR} [{'ok' if CASES_DIR.exists() else 'missing'}]")
    print(f"- Styles dir: {STYLES_DIR} [{'ok' if STYLES_DIR.exists() else 'missing'}]")
    print(f"- Outputs dir: {OUTPUTS_DIR} [{'ok' if OUTPUTS_DIR.exists() else 'missing'}]")
    print(f"- Unity import root: {UNITY_IMPORT_ROOT} [{'ok' if UNITY_IMPORT_ROOT.exists() else 'missing'}]")
    print(f"- Model root: {MODELS_DIR} [{'ok' if MODELS_DIR.exists() else 'missing'}]")
    print(f"- Selected case: {case_path}")
    print(f"- Selected style: {style_path}")
    print(f"- Selected model: {model_dir}")

    failures: list[str] = []

    if not case_path.exists():
        failures.append(f"Case file missing: {case_path}")
    if not style_path.exists():
        failures.append(f"Style file missing: {style_path}")
    if not model_dir.exists():
        failures.append(f"Model directory missing: {model_dir}")
    elif not (model_dir / "model_index.json").exists():
        failures.append(f"Model directory missing model_index.json: {model_dir}")

    locks = list_render_locks()
    if locks:
        print("- Active lock files:")
        for lock_path in locks:
            print(f"  - {lock_path}")
    else:
        print("- Active lock files: none")

    if failures:
        for failure in failures:
            print(f"FAIL: {failure}")
        return 1

    case_data = load_json(case_path)
    style_data = read_style(style_path)

    validation = validate_case_data(case_data, strict_quality=args.strict)
    print(f"- Case validation: {'ok' if validation.ok else 'failed'}")
    for error in validation.errors:
        print(f"  ERROR: {error}")
    for warning in validation.warnings:
        print(f"  WARN:  {warning}")

    if not validation.ok:
        return 1

    queue = build_render_queue(case_data, style_data, preset=args.preset)
    print(f"- Dry-run queue size: {len(queue)} item(s)")
    if queue:
        preview = queue[0]
        print(f"- First item: {preview['kind']} slot {preview['slot']} -> {preview['width']}x{preview['height']}")
        print(f"- First prompt preview: {preview['prompt'][:180]}")

    print("Health check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
