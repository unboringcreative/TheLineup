import json
from pathlib import Path

from .paths import CASES_DIR, OUTPUTS_DIR, STYLES_DIR


SIZE_PRESETS = {
    "debug": {
        "suspect": {"width": 832, "height": 1216},
        "evidence": {"width": 1024, "height": 1024},
        "featured": {"width": 1344, "height": 768},
    },
    "final": {
        "suspect": {"width": 928, "height": 1664},
        "evidence": {"width": 1328, "height": 1328},
        "featured": {"width": 1664, "height": 928},
    },
}


def join_parts(*parts: str) -> str:
    return ", ".join(part.strip() for part in parts if part and part.strip())


def load_json(path: str | Path) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def build_positive_prompt(base_prompt: str, style_data: dict, asset_type: str) -> str:
    asset_cfg = style_data.get("assetTypes", {}).get(asset_type, {})
    return join_parts(
        style_data.get("globalPrefix", ""),
        asset_cfg.get("prefix", ""),
        base_prompt,
        asset_cfg.get("suffix", ""),
        style_data.get("globalSuffix", ""),
    )


def build_negative_prompt(style_data: dict, asset_type: str) -> str:
    negative_cfg = style_data.get("negativePrompts", {})
    return join_parts(
        negative_cfg.get("global", ""),
        negative_cfg.get(asset_type, ""),
    )


def build_visual_identity_text(suspect: dict) -> str:
    visual = suspect.get("visualIdentity") or {}
    if not isinstance(visual, dict):
        return ""

    ordered_keys = (
        "ageRead",
        "build",
        "silhouette",
        "wardrobe",
        "hair",
        "accessorySignature",
        "props",
        "paletteAccent",
        "poseEnergy",
    )
    return join_parts(*(visual.get(key, "") for key in ordered_keys))


def build_suspect_prompt_base(suspect: dict) -> str:
    explicit = (suspect.get("portraitPromptBase") or "").strip()
    visual_text = build_visual_identity_text(suspect)

    bio_parts = []
    for key in ("sex", "nationality", "height", "weight", "occupation"):
        value = (suspect.get(key) or "").strip()
        if value:
            bio_parts.append(value)

    trait = (suspect.get("keyPersonalityTrait") or "").strip()
    if trait:
        bio_parts.append(f"{trait} personality")

    # Keep these optional context fields short if provided.
    for key in ("profile", "motive", "notes"):
        value = (suspect.get(key) or "").strip()
        if value:
            bio_parts.append(value)

    bio_text = ", ".join(bio_parts)

    combined_context = join_parts(bio_text, visual_text)

    if not combined_context:
        return explicit
    if not explicit:
        return combined_context

    # Avoid obvious duplication when explicit base already includes the context chunk.
    if combined_context.lower() in explicit.lower():
        return explicit

    return join_parts(combined_context, explicit)


def build_render_queue(case_data: dict, style_data: dict, preset: str = "final") -> list[dict]:
    if preset not in SIZE_PRESETS:
        raise ValueError(f"Unknown preset: {preset}")

    sizes = SIZE_PRESETS[preset]
    queue: list[dict] = []

    for suspect in case_data.get("suspects", []):
        suspect_base = build_suspect_prompt_base(suspect)
        queue.append(
            {
                "kind": "suspect",
                "slot": suspect["slot"],
                "stem": suspect["portraitStem"],
                "title": suspect["displayName"],
                "prompt": build_positive_prompt(
                    suspect_base,
                    style_data,
                    "suspect",
                ),
                "negative_prompt": build_negative_prompt(style_data, "suspect"),
                "width": sizes["suspect"]["width"],
                "height": sizes["suspect"]["height"],
                "remove_bg": True,
            }
        )

    for evidence in case_data.get("evidence", []):
        queue.append(
            {
                "kind": "evidence",
                "slot": evidence["slot"],
                "stem": evidence["imageStem"],
                "title": evidence["title"],
                "prompt": build_positive_prompt(
                    evidence["imagePromptBase"],
                    style_data,
                    "evidence",
                ),
                "negative_prompt": build_negative_prompt(style_data, "evidence"),
                "width": sizes["evidence"]["width"],
                "height": sizes["evidence"]["height"],
                "remove_bg": False,
            }
        )

    queue.append(
        {
            "kind": "featured",
            "slot": 0,
            "stem": case_data["featuredImageStem"],
            "title": case_data["caseTitle"],
            "prompt": build_positive_prompt(
                case_data["featuredImagePromptBase"],
                style_data,
                "featured",
            ),
            "negative_prompt": build_negative_prompt(style_data, "featured"),
            "width": sizes["featured"]["width"],
            "height": sizes["featured"]["height"],
            "remove_bg": False,
        }
    )

    return queue


def write_prompt_manifest(
    output_path: str | Path,
    case_data: dict,
    style_data: dict,
    queue: list[dict],
    preset: str,
) -> None:
    manifest = {
        "caseId": case_data.get("caseId"),
        "caseTitle": case_data.get("caseTitle"),
        "styleId": style_data.get("styleId"),
        "styleLabel": style_data.get("label"),
        "preset": preset,
        "items": queue,
    }
    Path(output_path).write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


if __name__ == "__main__":
    case = load_json(CASES_DIR / "sample_case.json")
    style = load_json(STYLES_DIR / "comic_noir.json")
    queue = build_render_queue(case, style, preset="final")
    out = OUTPUTS_DIR / "prompt_manifest.preview.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    write_prompt_manifest(out, case, style, queue, preset="final")
    print(f"Wrote preview prompt manifest to: {out}")
