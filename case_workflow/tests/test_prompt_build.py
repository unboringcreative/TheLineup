from pathlib import Path
import sys

_ROOT_DIR = Path(__file__).resolve().parents[1]
_SRC_DIR = _ROOT_DIR / "src"
if str(_ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(_ROOT_DIR))
if str(_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(_SRC_DIR))

from prompt_builder import load_json, build_render_queue, write_prompt_manifest
from paths import CASES_DIR, OUTPUTS_DIR, STYLES_DIR

CASE_PATH = CASES_DIR / "case_012_the_ivory_switch.json"
STYLE_PATH = STYLES_DIR / "comic_noir.json"
PRESET = "final"


def main() -> None:
    case = load_json(CASE_PATH)
    style = load_json(STYLE_PATH)
    queue = build_render_queue(case, style, preset=PRESET)

    out_path = OUTPUTS_DIR / "prompt_manifest.test.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    write_prompt_manifest(out_path, case, style, queue, preset=PRESET)

    for item in queue:
        print("=" * 100)
        print(f"{item['kind'].upper()} | slot={item['slot']} | stem={item['stem']}")
        print(f"SIZE: {item['width']}x{item['height']}")
        print(f"POSITIVE: {item['prompt']}")
        print(f"NEGATIVE: {item['negative_prompt']}")

    print(f"\nPrompt manifest written to: {out_path}")


if __name__ == "__main__":
    main()
