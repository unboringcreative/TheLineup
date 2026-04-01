from __future__ import annotations

import json
import re
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


EXPECTED_SUSPECT_COUNT = 5
EXPECTED_EVIDENCE_COUNT = 3
EXPECTED_EVIDENCE_ROLES = {"red_herring", "valid", "neutral"}
NAME_RE = re.compile(r"^[A-Z][a-zA-Z'\-]+(?: [A-Z][a-zA-Z'\-]+)+$")
TEXTY_IMAGE_RE = re.compile(r"\b(text|label|caption|poster|signage|document text|readable words|legible words)\b", re.IGNORECASE)
LOCATION_NO_PEOPLE_RE = re.compile(r"\b(?:no|without)\s+(?:people|person|characters?|humans?)\b", re.IGNORECASE)


@dataclass
class ValidationResult:
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not self.errors


def load_case_json(path: str | Path) -> dict[str, Any]:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def validate_case_data(case_data: dict[str, Any], *, strict_quality: bool = False) -> ValidationResult:
    result = ValidationResult()

    _require_non_empty_string(case_data, "caseId", result)
    _require_non_empty_string(case_data, "caseTitle", result)
    _require_non_empty_string(case_data, "caseDescription", result)
    _require_non_empty_string(case_data, "locationImageStem", result)
    _require_non_empty_string(case_data, "locationImagePromptBase", result)
    _require_non_empty_string(case_data, "featuredImageStem", result)
    _require_non_empty_string(case_data, "featuredImagePromptBase", result)
    _require_non_empty_string(case_data, "instruction", result)
    _require_non_empty_string(case_data, "verdictTitle", result)
    _require_non_empty_string(case_data, "explanation", result)

    guilty_slot = case_data.get("guiltySlot")
    if not isinstance(guilty_slot, int) or not 1 <= guilty_slot <= EXPECTED_SUSPECT_COUNT:
        result.errors.append(f"guiltySlot must be an integer between 1 and {EXPECTED_SUSPECT_COUNT}.")

    location = case_data.get("location")
    if not isinstance(location, dict):
        result.errors.append("location must be an object with addressOrBusiness, city, and country.")
    else:
        _require_non_empty_string(location, "addressOrBusiness", result, prefix="location.")
        _require_non_empty_string(location, "city", result, prefix="location.")
        _require_non_empty_string(location, "country", result, prefix="location.")

    suspects = case_data.get("suspects")
    if not isinstance(suspects, list):
        result.errors.append("suspects must be an array.")
    else:
        _validate_suspects(suspects, result, strict_quality=strict_quality)

    evidence = case_data.get("evidence")
    if not isinstance(evidence, list):
        result.errors.append("evidence must be an array.")
    else:
        _validate_evidence(evidence, result, strict_quality=strict_quality)

    _validate_case_logic(case_data, result, strict_quality=strict_quality)
    return result


def _validate_suspects(suspects: list[Any], result: ValidationResult, *, strict_quality: bool) -> None:
    if len(suspects) != EXPECTED_SUSPECT_COUNT:
        result.errors.append(f"suspects must contain exactly {EXPECTED_SUSPECT_COUNT} entries.")

    seen_slots: set[int] = set()
    seen_names: set[str] = set()
    rich_fields = ("profile", "motive", "notes")
    visual_markers: dict[str, list[str]] = {
        "silhouette": [],
        "paletteAccent": [],
        "accessorySignature": [],
        "props": [],
    }

    for index, suspect in enumerate(suspects, start=1):
        prefix = f"suspects[{index - 1}]"
        if not isinstance(suspect, dict):
            result.errors.append(f"{prefix} must be an object.")
            continue

        slot = suspect.get("slot")
        if not isinstance(slot, int) or not 1 <= slot <= EXPECTED_SUSPECT_COUNT:
            result.errors.append(f"{prefix}.slot must be an integer between 1 and {EXPECTED_SUSPECT_COUNT}.")
        elif slot in seen_slots:
            result.errors.append(f"Duplicate suspect slot: {slot}.")
        else:
            seen_slots.add(slot)

        for key in (
            "displayName",
            "occupation",
            "nationality",
            "height",
            "weight",
            "keyPersonalityTrait",
            "dialogue",
            "portraitStem",
            "portraitPromptBase",
        ):
            _require_non_empty_string(suspect, key, result, prefix=f"{prefix}.")

        name = str(suspect.get("displayName") or "").strip()
        if name:
            normalized_name = name.lower()
            if normalized_name in seen_names:
                result.errors.append(f"Duplicate suspect displayName: {name}.")
            else:
                seen_names.add(normalized_name)

            if not NAME_RE.match(name):
                result.warnings.append(f"{prefix}.displayName should read like a grounded full name: {name!r}.")

        for key in rich_fields:
            value = str(suspect.get(key) or "").strip()
            if strict_quality and not value:
                result.errors.append(f"{prefix}.{key} is required in strict quality mode.")
            elif not value:
                result.warnings.append(f"{prefix}.{key} is missing; richer suspect bios are recommended.")
            elif len(value.split()) < 4:
                result.warnings.append(f"{prefix}.{key} is very short; add more concrete detail.")

        dialogue = str(suspect.get("dialogue") or "").strip()
        if dialogue and len(dialogue.split()) < 8:
            result.warnings.append(f"{prefix}.dialogue is short; interviews read better with a fuller voice.")

        visual = suspect.get("visualIdentity")
        if strict_quality and not isinstance(visual, dict):
            result.errors.append(f"{prefix}.visualIdentity is required in strict quality mode.")
        elif isinstance(visual, dict):
            _validate_visual_identity(visual, prefix, result, strict_quality=strict_quality)
            for key in visual_markers:
                value = str(visual.get(key) or "").strip().lower()
                if value:
                    visual_markers[key].append(value)

    for key, values in visual_markers.items():
        duplicates = [value for value, count in Counter(values).items() if count > 1]
        for value in duplicates:
            result.warnings.append(f"Multiple suspects share the same visualIdentity.{key}: {value!r}.")


def _validate_visual_identity(visual: dict[str, Any], prefix: str, result: ValidationResult, *, strict_quality: bool) -> None:
    required_visual_keys = (
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

    for key in required_visual_keys:
        value = str(visual.get(key) or "").strip()
        if strict_quality and not value:
            result.errors.append(f"{prefix}.visualIdentity.{key} is required in strict quality mode.")
        elif not value:
            result.warnings.append(f"{prefix}.visualIdentity.{key} is missing; use visual markers to separate suspects.")
        elif len(value.split()) < 2:
            result.warnings.append(f"{prefix}.visualIdentity.{key} is too short to be visually useful.")


def _validate_evidence(evidence: list[Any], result: ValidationResult, *, strict_quality: bool) -> None:
    if len(evidence) != EXPECTED_EVIDENCE_COUNT:
        result.errors.append(f"evidence must contain exactly {EXPECTED_EVIDENCE_COUNT} entries.")

    seen_slots: set[int] = set()
    for index, item in enumerate(evidence, start=1):
        prefix = f"evidence[{index - 1}]"
        if not isinstance(item, dict):
            result.errors.append(f"{prefix} must be an object.")
            continue

        slot = item.get("slot")
        if not isinstance(slot, int) or not 1 <= slot <= EXPECTED_EVIDENCE_COUNT:
            result.errors.append(f"{prefix}.slot must be an integer between 1 and {EXPECTED_EVIDENCE_COUNT}.")
        elif slot in seen_slots:
            result.errors.append(f"Duplicate evidence slot: {slot}.")
        else:
            seen_slots.add(slot)

        for key in ("title", "description", "discoveryLocation", "imageStem", "imagePromptBase"):
            _require_non_empty_string(item, key, result, prefix=f"{prefix}.")

        role = str(item.get("role") or "").strip().lower()
        if role not in EXPECTED_EVIDENCE_ROLES:
            result.errors.append(f"{prefix}.role must be one of {sorted(EXPECTED_EVIDENCE_ROLES)}.")

        points_to = item.get("pointsToSuspectSlot")
        if role in {"red_herring", "valid"}:
            if not isinstance(points_to, int) or not 1 <= points_to <= EXPECTED_SUSPECT_COUNT:
                result.errors.append(f"{prefix}.pointsToSuspectSlot must be an integer between 1 and {EXPECTED_SUSPECT_COUNT} for role {role!r}.")
        elif role == "neutral":
            if points_to not in (None, 0):
                result.warnings.append(f"{prefix}.pointsToSuspectSlot should be omitted or 0 for neutral evidence.")

        description = str(item.get("description") or "").strip()
        if description and len(description.split()) < 8:
            result.warnings.append(f"{prefix}.description is short; evidence should explain why it matters.")

        prompt = str(item.get("imagePromptBase") or "").strip()
        if prompt and TEXTY_IMAGE_RE.search(prompt):
            level = result.errors if strict_quality else result.warnings
            level.append(f"{prefix}.imagePromptBase appears to rely on readable text, which conflicts with image rules.")


def _validate_case_logic(case_data: dict[str, Any], result: ValidationResult, *, strict_quality: bool) -> None:
    suspects = case_data.get("suspects") or []
    evidence = case_data.get("evidence") or []
    explanation = str(case_data.get("explanation") or "").strip()
    case_description = str(case_data.get("caseDescription") or "").strip()
    location_prompt = str(case_data.get("locationImagePromptBase") or "").strip()
    featured_prompt = str(case_data.get("featuredImagePromptBase") or "").strip()

    if explanation and "red herring" not in explanation.lower():
        result.warnings.append("Explanation does not identify the red herring yet.")

    if case_description and len(case_description.split()) < 18:
        result.warnings.append("caseDescription is brief; stronger setup helps tone and stakes.")

    if featured_prompt and TEXTY_IMAGE_RE.search(featured_prompt):
        level = result.errors if strict_quality else result.warnings
        level.append("featuredImagePromptBase appears to rely on readable text, which conflicts with image rules.")

    if location_prompt and TEXTY_IMAGE_RE.search(location_prompt):
        level = result.errors if strict_quality else result.warnings
        level.append("locationImagePromptBase appears to rely on readable text, which conflicts with image rules.")

    if location_prompt and not LOCATION_NO_PEOPLE_RE.search(location_prompt):
        result.errors.append(
            "locationImagePromptBase must explicitly exclude people and characters (for example: 'no people, no characters')."
        )

    guilty_slot = case_data.get("guiltySlot")
    if isinstance(guilty_slot, int) and explanation:
        expected_phrase = f"slot {guilty_slot}"
        if expected_phrase.lower() not in explanation.lower():
            result.warnings.append("Explanation should explicitly name the guilty slot for downstream clarity.")

    if suspects:
        countries = {str((suspect or {}).get("nationality") or "").strip().lower() for suspect in suspects}
        countries.discard("")
        if len(countries) == 1:
            result.warnings.append("All suspects share the same nationality; consider a more varied social mix if the setting allows.")

    if strict_quality and evidence and explanation:
        for item in evidence:
            title = str((item or {}).get("title") or "").strip()
            if title and title.lower() not in explanation.lower():
                result.warnings.append(f"Explanation does not explicitly call back evidence title {title!r}.")

    if evidence:
        role_counts = Counter(str((item or {}).get("role") or "").strip().lower() for item in evidence)
        for role in EXPECTED_EVIDENCE_ROLES:
            if role_counts.get(role, 0) != 1:
                result.errors.append(f"evidence must include exactly one {role!r} item.")

        guilty_slot = case_data.get("guiltySlot")
        for item in evidence:
            role = str((item or {}).get("role") or "").strip().lower()
            points_to = (item or {}).get("pointsToSuspectSlot")
            title = str((item or {}).get("title") or "").strip()
            if role == "valid" and guilty_slot is not None and points_to != guilty_slot:
                result.errors.append(f"Valid evidence {title!r} must point to guiltySlot {guilty_slot}.")
            if role == "red_herring" and guilty_slot is not None and points_to == guilty_slot:
                result.errors.append(f"Red herring evidence {title!r} cannot point to guiltySlot {guilty_slot}.")
            if role == "neutral" and points_to not in (None, 0):
                result.warnings.append(f"Neutral evidence {title!r} should not point to a suspect.")


def _require_non_empty_string(source: dict[str, Any], key: str, result: ValidationResult, *, prefix: str = "") -> None:
    value = source.get(key)
    if not isinstance(value, str) or not value.strip():
        result.errors.append(f"{prefix}{key} must be a non-empty string.")
