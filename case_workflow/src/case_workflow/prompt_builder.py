import json
from pathlib import Path

from .paths import CASES_DIR, OUTPUTS_DIR, STYLES_DIR


SIZE_PRESETS = {
    "debug": {
        "suspect": {"width": 832, "height": 1216},
        "evidence": {"width": 1024, "height": 1024},
        "location": {"width": 1344, "height": 768},
        "featured": {"width": 1344, "height": 768},
    },
    "final": {
        "suspect": {"width": 928, "height": 1664},
        "evidence": {"width": 1328, "height": 1328},
        "location": {"width": 1664, "height": 928},
        "featured": {"width": 1664, "height": 928},
    },
}

LOCATION_REQUIRED_EXCLUSIONS = (
    "empty environment plate",
    "unoccupied central floor area",
    "blank display panels",
    "no people",
    "no characters",
    "no human figures",
    "no silhouettes",
    "no letters",
    "no numbers",
    "no words",
)

LOCATION_NEGATIVE_EXCLUSIONS = (
    "people",
    "person",
    "characters",
    "character",
    "crowd",
    "letters",
    "numbers",
    "words",
    "glyphs",
    "signage",
    "poster",
    "labels",
)


def join_parts(*parts: str) -> str:
    return ", ".join(part.strip() for part in parts if part and part.strip())


def load_json(path: str | Path) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def enforce_location_prompt_rules(base_prompt: str, prefix: str = "") -> str:
    prompt = (base_prompt or "").strip()
    combined_lower = join_parts(prefix, prompt).lower()
    missing = [phrase for phrase in LOCATION_REQUIRED_EXCLUSIONS if phrase not in combined_lower]
    return join_parts(prompt, *missing)


def build_positive_prompt(base_prompt: str, style_data: dict, asset_type: str) -> str:
    asset_cfg = style_data.get("assetTypes", {}).get(asset_type, {})
    asset_prefix = asset_cfg.get("prefix", "")
    normalized_base_prompt = enforce_location_prompt_rules(base_prompt, asset_prefix) if asset_type == "location" else base_prompt
    return join_parts(
        style_data.get("globalPrefix", ""),
        asset_prefix,
        normalized_base_prompt,
        asset_cfg.get("suffix", ""),
        style_data.get("globalSuffix", ""),
    )


def build_negative_prompt(style_data: dict, asset_type: str) -> str:
    negative_cfg = style_data.get("negativePrompts", {})
    existing = join_parts(
        negative_cfg.get("global", ""),
        negative_cfg.get(asset_type, ""),
    ).lower()
    extras = tuple(term for term in LOCATION_NEGATIVE_EXCLUSIONS if asset_type == "location" and term not in existing)
    return join_parts(
        negative_cfg.get("global", ""),
        negative_cfg.get(asset_type, ""),
        *extras,
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
            "kind": "location",
            "slot": 0,
            "stem": case_data["locationImageStem"],
            "title": f"{case_data['caseTitle']} Lineup Background",
            "prompt": build_positive_prompt(
                case_data["locationImagePromptBase"],
                style_data,
                "location",
            ),
            "negative_prompt": build_negative_prompt(style_data, "location"),
            "width": sizes["location"]["width"],
            "height": sizes["location"]["height"],
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
